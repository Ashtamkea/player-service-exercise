using System.Collections.Concurrent;
using Exercise.Infra.Exceptions;
using PlayerService.Lib.DAL.Models.Memory;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.Leaderboards;

namespace PlayerService.Lib.DAL.Sources
{
    public class MemoryGameDataSource : IDisposable
    {
        private IReadOnlyList<LeaderboardEntry>? _leaderboardSnapshot;

        internal ConcurrentDictionary<string, long> ScoresByPlayerId { get; } = new();
        internal ConcurrentDictionary<string, MemorySession> SessionsByDeviceId { get; } = new();
        internal ConcurrentDictionary<string, MemoryPlayer> PlayersById { get; } = new();
        internal ConcurrentDictionary<string, SemaphoreSlim> SessionLocksByDeviceId { get; } = new();
        internal ReaderWriterLockSlim ScoreSnapshotGate { get; } = new();
        internal SemaphoreSlim LeaderboardRefreshGate { get; } = new(1, 1);

        internal MemoryPlayer GetOrCreatePlayer(
            string playerId,
            DateTime lastActiveUtc
        )
        {
            if (PlayersById.TryGetValue(playerId, out var existingPlayer))
                return existingPlayer;

            var candidate = new MemoryPlayer
            {
                PlayerId = playerId,
                GiftsSent = 0,
                GiftsReceived = 0,
                LastActiveUtc = lastActiveUtc,
                ActiveSessionsByDeviceId = new ConcurrentDictionary<
                    string,
                    MemorySession
                >(),
                ScoreRequestsByRequestId = new ConcurrentDictionary<
                    Guid,
                    MemoryScoreRequest
                >(),
                GiftRequestsByRequestId = new ConcurrentDictionary<
                    Guid,
                    MemoryGiftRequest
                >(),
                PlayerLock = new SemaphoreSlim(1, 1)
            };

            ScoreSnapshotGate.EnterReadLock();

            try
            {
                if (PlayersById.TryGetValue(playerId, out existingPlayer))
                {
                    candidate.PlayerLock.Dispose();

                    return existingPlayer;
                }

                ScoresByPlayerId.TryAdd(
                    playerId,
                    ConstantValues.InitialPlayerScore
                );

                var player = PlayersById.GetOrAdd(playerId, candidate);

                if (!ReferenceEquals(player, candidate))
                    candidate.PlayerLock.Dispose();

                return player;
            }
            finally
            {
                ScoreSnapshotGate.ExitReadLock();
            }
        }

        internal MemoryPlayer GetRequiredPlayer(string playerId)
        {
            if (PlayersById.TryGetValue(playerId, out var player))
                return player;

            #region Exception

            throw ExceptionConstructor.CreateParameterized(
                "Canonical player state could not be found.",
                new
                {
                    PlayerId = playerId
                }
            );

            #endregion
        }

        internal SemaphoreSlim GetSessionLock(string deviceId)
        {
            var sessionLock = SessionLocksByDeviceId.GetOrAdd(
                deviceId,
                _ => new SemaphoreSlim(1, 1)
            );

            return sessionLock;
        }

        internal KeyValuePair<string, long>[] CaptureScores()
        {
            ScoreSnapshotGate.EnterWriteLock();

            try
            {
                var scores = ScoresByPlayerId.ToArray();

                return scores;
            }
            finally
            {
                ScoreSnapshotGate.ExitWriteLock();
            }
        }

        internal IReadOnlyList<LeaderboardEntry>? GetLeaderboardSnapshot()
        {
            var snapshot = Volatile.Read(ref _leaderboardSnapshot);

            return snapshot;
        }

        internal void PublishLeaderboardSnapshot(
            LeaderboardEntry[] snapshot
        )
        {
            var readOnlySnapshot = Array.AsReadOnly(snapshot);

            Interlocked.Exchange(
                ref _leaderboardSnapshot,
                readOnlySnapshot
            );
        }

        internal async Task<(MemoryPlayer First, MemoryPlayer? Second)> AcquirePlayerLocksAsync(
            MemoryPlayer player,
            MemoryPlayer? secondPlayer = null,
            CancellationToken cancellationToken = default
        )
        {
            var firstPlayer = player;
            MemoryPlayer? lastPlayer = null;

            if (
                secondPlayer is not null
                && !string.Equals(
                    player.PlayerId,
                    secondPlayer.PlayerId,
                    StringComparison.Ordinal
                )
            )
            {
                if (
                    StringComparer.Ordinal.Compare(
                        player.PlayerId,
                        secondPlayer.PlayerId
                    ) <= 0
                )
                    lastPlayer = secondPlayer;
                else
                {
                    firstPlayer = secondPlayer;
                    lastPlayer = player;
                }
            }

            await firstPlayer.PlayerLock.WaitAsync(cancellationToken);

            if (lastPlayer is null)
            {
                var singlePlayerLocks = (
                    First: firstPlayer,
                    Second: (MemoryPlayer?)null
                );

                return singlePlayerLocks;
            }

            try
            {
                await lastPlayer.PlayerLock.WaitAsync(cancellationToken);

                var playerLocks = (
                    First: firstPlayer,
                    Second: lastPlayer
                );

                return playerLocks;
            }
            catch
            {
                firstPlayer.PlayerLock.Release();

                throw;
            }
        }

        internal void ReleasePlayerLocks(
            (MemoryPlayer First, MemoryPlayer? Second) players
        )
        {
            players.Second?.PlayerLock.Release();
            players.First.PlayerLock.Release();
        }

        public void Dispose()
        {
            foreach (var sessionLock in SessionLocksByDeviceId.Values)
                sessionLock.Dispose();

            foreach (var player in PlayersById.Values)
                player.PlayerLock.Dispose();

            LeaderboardRefreshGate.Dispose();
            ScoreSnapshotGate.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
