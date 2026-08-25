using Exercise.Infra.Configuration;
using Exercise.Infra.Exceptions;
using Microsoft.Extensions.Configuration;
using PlayerService.Lib.DAL.Sources;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.DAL.Providers;
using PlayerService.Shared.Models.Leaderboards;

namespace PlayerService.Lib.DAL.Providers.Memory
{
    public class LeaderboardMemoryProvider : ILeaderboardProvider
    {
        private static readonly IComparer<LeaderboardEntry> HeapComparer =
            Comparer<LeaderboardEntry>.Create(CompareHeapPriority);

        private readonly MemoryGameDataSource _gameDataSource;
        private readonly int _leaderboardTopSize;

        public LeaderboardMemoryProvider(
            MemoryGameDataSource gameDataSource,
            IConfiguration configuration
        )
        {
            _gameDataSource = gameDataSource;
            _leaderboardTopSize = configuration.GetSectionValue<int>(
                ConfigurationKeys.PlayerServiceSection,
                ConfigurationKeys.LeaderboardTopSize
            );

            if (_leaderboardTopSize <= 0)
            {
                #region Exception

                throw ExceptionConstructor.CreateParameterized(
                    "Leaderboard top size must be greater than zero.",
                    new
                    {
                        LeaderboardTopSize = _leaderboardTopSize
                    }
                );

                #endregion
            }
        }

        public async Task<bool> TryRefreshLeaderboardAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var acquired = await _gameDataSource.LeaderboardRefreshGate.WaitAsync(
                0,
                cancellationToken
            );

            if (!acquired)
                return false;

            try
            {
                var scores = _gameDataSource.CaptureScores();
                var topPlayers = new PriorityQueue<
                    LeaderboardEntry,
                    LeaderboardEntry
                >(HeapComparer);

                foreach (var score in scores)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var candidate = new LeaderboardEntry
                    {
                        PlayerId = score.Key,
                        Score = score.Value
                    };

                    if (topPlayers.Count < _leaderboardTopSize)
                    {
                        topPlayers.Enqueue(candidate, candidate);

                        continue;
                    }

                    topPlayers.TryPeek(
                        out _,
                        out var worstIncludedPlayer
                    );

                    if (
                        HeapComparer.Compare(
                            candidate,
                            worstIncludedPlayer!
                        ) <= 0
                    )
                        continue;

                    topPlayers.Dequeue();
                    topPlayers.Enqueue(candidate, candidate);
                }

                var snapshot = topPlayers.UnorderedItems
                .Select(item => item.Element)
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.PlayerId, StringComparer.Ordinal)
                .ToArray();

                cancellationToken.ThrowIfCancellationRequested();
                _gameDataSource.PublishLeaderboardSnapshot(snapshot);

                return true;
            }
            finally
            {
                _gameDataSource.LeaderboardRefreshGate.Release();
            }
        }

        public Task<IReadOnlyList<LeaderboardEntry>?> GetLeaderboardAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = _gameDataSource.GetLeaderboardSnapshot();

            if (snapshot is null)
                return Task.FromResult<
                    IReadOnlyList<LeaderboardEntry>?
                >(null);

            return Task.FromResult<
                IReadOnlyList<LeaderboardEntry>?
            >(snapshot);
        }

        private static int CompareHeapPriority(
            LeaderboardEntry left,
            LeaderboardEntry right
        )
        {
            var scoreComparison = left.Score.CompareTo(right.Score);

            if (scoreComparison != 0)
                return scoreComparison;

            var playerComparison = StringComparer.Ordinal.Compare(
                right.PlayerId,
                left.PlayerId
            );

            return playerComparison;
        }
    }
}
