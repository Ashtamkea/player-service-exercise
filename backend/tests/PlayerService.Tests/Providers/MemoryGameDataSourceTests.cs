using System.Collections.Concurrent;
using System.Reflection;
using PlayerService.Lib.DAL.Models.Memory;
using PlayerService.Lib.DAL.Sources;
using PlayerService.Shared.Configuration;

namespace PlayerService.Tests.Providers
{
    public class MemoryGameDataSourceTests : IDisposable
    {
        private readonly MemoryGameDataSource _gameDataSource;

        public MemoryGameDataSourceTests()
        {
            _gameDataSource = new MemoryGameDataSource();
        }

        public void Dispose()
        {
            _gameDataSource.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task PlayerAndInitialScoreArePublishedTogether()
        {
            const string playerId = "atomic-player";
            var players = GetSourceProperty<
                ConcurrentDictionary<string, MemoryPlayer>
            >("PlayersById");
            var scores = GetSourceProperty<
                ConcurrentDictionary<string, long>
            >("ScoresByPlayerId");
            var gate = GetSourceProperty<ReaderWriterLockSlim>(
                "ScoreSnapshotGate"
            );
            gate.EnterWriteLock();

            Task<MemoryPlayer> creationTask;

            try
            {
                creationTask = Task.Run(() => InvokeGetOrCreatePlayer(playerId));
                Thread.Sleep(100);

                Assert.False(players.ContainsKey(playerId));
                Assert.False(scores.ContainsKey(playerId));
            }
            finally
            {
                gate.ExitWriteLock();
            }

            var player = await creationTask;

            Assert.Same(player, players[playerId]);
            Assert.Equal(ConstantValues.InitialPlayerScore, scores[playerId]);
        }

        [Fact]
        public async Task PlayerActivityKeepsMaximumConcurrentTimestamp()
        {
            var player = AddPlayer("player-activity");
            var latestActivityUtc = DateTime.UtcNow.AddMinutes(2);
            var earlierActivityUtc = latestActivityUtc.AddMinutes(-1);

            await Task.WhenAll(
                Task.Run(() => player.RecordActivity(latestActivityUtc)),
                Task.Run(() => player.RecordActivity(earlierActivityUtc))
            );

            Assert.Equal(latestActivityUtc, player.LastActiveUtc);
        }

        [Fact]
        public async Task OnePlayerLockAcquisitionLocksAndReleasesPlayer()
        {
            var player = AddPlayer("player-1");
            var lockedPlayers = await AcquirePlayerLocksAsync(player);

            Assert.Same(player, lockedPlayers.First);
            Assert.Null(lockedPlayers.Second);
            Assert.Equal(0, player.PlayerLock.CurrentCount);

            ReleasePlayerLocks(lockedPlayers);

            Assert.Equal(1, player.PlayerLock.CurrentCount);
        }

        [Fact]
        public async Task TwoPlayerLockAcquisitionUsesOrdinalPlayerIdOrder()
        {
            var laterPlayer = AddPlayer("player-2");
            var earlierPlayer = AddPlayer("player-1");
            var lockedPlayers = await AcquirePlayerLocksAsync(
                laterPlayer,
                earlierPlayer
            );

            Assert.Same(earlierPlayer, lockedPlayers.First);
            Assert.Same(laterPlayer, lockedPlayers.Second);
            Assert.Equal(0, earlierPlayer.PlayerLock.CurrentCount);
            Assert.Equal(0, laterPlayer.PlayerLock.CurrentCount);

            ReleasePlayerLocks(lockedPlayers);

            Assert.Equal(1, earlierPlayer.PlayerLock.CurrentCount);
            Assert.Equal(1, laterPlayer.PlayerLock.CurrentCount);
        }

        [Fact]
        public async Task SamePlayerLockAcquisitionLocksPlayerOnlyOnce()
        {
            var player = AddPlayer("player-1");
            var lockedPlayers = await AcquirePlayerLocksAsync(
                player,
                player
            );

            Assert.Same(player, lockedPlayers.First);
            Assert.Null(lockedPlayers.Second);
            Assert.Equal(0, player.PlayerLock.CurrentCount);

            ReleasePlayerLocks(lockedPlayers);

            Assert.Equal(1, player.PlayerLock.CurrentCount);
        }

        [Fact]
        public async Task CancellationWaitingForSecondPlayerReleasesFirstPlayer()
        {
            var firstPlayer = AddPlayer("player-1");
            var secondPlayer = AddPlayer("player-2");
            await secondPlayer.PlayerLock.WaitAsync();
            using var cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var acquisitionTask = AcquirePlayerLocksAsync(
                    firstPlayer,
                    secondPlayer,
                    cancellationTokenSource.Token
                );

                await WaitUntilAsync(
                    () => firstPlayer.PlayerLock.CurrentCount == 0
                );
                cancellationTokenSource.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => acquisitionTask
                );
                Assert.Equal(1, firstPlayer.PlayerLock.CurrentCount);
            }
            finally
            {
                secondPlayer.PlayerLock.Release();
            }
        }

        private TProperty GetSourceProperty<TProperty>(string propertyName)
        {
            var property = typeof(MemoryGameDataSource).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.NotNull(property);
            var value = Assert.IsType<TProperty>(
                property.GetValue(_gameDataSource)
            );

            return value;
        }

        private MemoryPlayer AddPlayer(string playerId)
        {
            var player = new MemoryPlayer
            {
                PlayerId = playerId,
                GiftsSent = 0,
                GiftsReceived = 0,
                LastActiveUtc = DateTime.UtcNow,
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
            var players = GetSourceProperty<
                ConcurrentDictionary<string, MemoryPlayer>
            >("PlayersById");

            Assert.True(players.TryAdd(playerId, player));

            return player;
        }

        private MemoryPlayer InvokeGetOrCreatePlayer(string playerId)
        {
            var method = typeof(MemoryGameDataSource).GetMethod(
                "GetOrCreatePlayer",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.NotNull(method);

            return Assert.IsType<MemoryPlayer>(method.Invoke(
                _gameDataSource,
                new object[]
                {
                    playerId,
                    DateTime.UtcNow
                }
            ));
        }

        private async Task<(MemoryPlayer First, MemoryPlayer? Second)> AcquirePlayerLocksAsync(
            MemoryPlayer player,
            MemoryPlayer? secondPlayer = null,
            CancellationToken cancellationToken = default
        )
        {
            var method = typeof(MemoryGameDataSource).GetMethod(
                "AcquirePlayerLocksAsync",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.NotNull(method);
            var invocationResult = method.Invoke(
                _gameDataSource,
                new object?[]
                {
                    player,
                    secondPlayer,
                    cancellationToken
                }
            );
            var task = Assert.IsAssignableFrom<
                Task<(MemoryPlayer First, MemoryPlayer? Second)>
            >(invocationResult);
            var lockedPlayers = await task;

            return lockedPlayers;
        }

        private void ReleasePlayerLocks(
            (MemoryPlayer First, MemoryPlayer? Second) players
        )
        {
            var method = typeof(MemoryGameDataSource).GetMethod(
                "ReleasePlayerLocks",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.NotNull(method);
            method.Invoke(
                _gameDataSource,
                new object[]
                {
                    players
                }
            );
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var timeoutAt = DateTime.UtcNow.AddSeconds(2);

            while (!condition() && DateTime.UtcNow < timeoutAt)
                await Task.Delay(10);

            Assert.True(condition());
        }

    }
}
