using Microsoft.Extensions.Configuration;
using PlayerService.Lib.DAL.Providers.Memory;
using PlayerService.Lib.DAL.Sources;
using PlayerService.Shared.Models.Leaderboards;
using PlayerService.Shared.Models.PlayerGifts;

namespace PlayerService.Tests.Leaderboards
{
    public class MemoryLeaderboardIntegrationTests
    {
        [Fact]
        public void NonPositiveTopSizeIsRejected()
        {
            var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlayerService:leaderboardTopSize"] = "0"
            })
            .Build();
            using var gameDataSource = new MemoryGameDataSource();

            Assert.ThrowsAny<Exception>(() =>
                new LeaderboardMemoryProvider(
                    gameDataSource,
                    configuration
                )
            );
        }

        [Fact]
        public async Task MissingAndEmptySnapshotsRemainDistinct()
        {
            using var context = new MemoryLeaderboardTestContext(5);

            var missing = await context.LeaderboardProvider
            .GetLeaderboardAsync();
            var refreshed = await context.LeaderboardProvider
            .TryRefreshLeaderboardAsync();
            var empty = await context.LeaderboardProvider
            .GetLeaderboardAsync();

            Assert.Null(missing);
            Assert.True(refreshed);
            Assert.NotNull(empty);
            Assert.Empty(empty);
        }

        [Fact]
        public async Task TopPlayersUseScoreAndDeterministicPlayerIdOrdering()
        {
            using var context = new MemoryLeaderboardTestContext(4);
            context.ScoresByPlayerId["player-d"] = 70;
            context.ScoresByPlayerId["player-c"] = 90;
            context.ScoresByPlayerId["player-b"] = 90;
            context.ScoresByPlayerId["player-a"] = 100;
            context.ScoresByPlayerId["player-e"] = 20;
            context.ScoresByPlayerId["player-f"] = 120;

            await context.LeaderboardProvider.TryRefreshLeaderboardAsync();
            var leaderboard = await context.LeaderboardProvider
            .GetLeaderboardAsync();

            Assert.NotNull(leaderboard);
            Assert.Equal(
                new List<(string PlayerId, long Score)>
                {
                    ("player-f", 120),
                    ("player-a", 100),
                    ("player-b", 90),
                    ("player-c", 90)
                },
                leaderboard
                .Select(entry => (entry.PlayerId, entry.Score))
                .ToList()
            );
        }

        [Fact]
        public async Task BoundedHeapMatchesFullSortForRandomizedScores()
        {
            const int playerCount = 2000;
            const int topSize = 37;
            using var context = new MemoryLeaderboardTestContext(topSize);
            var random = new Random(1979);

            for (var index = 0; index < playerCount; index++)
            {
                context.ScoresByPlayerId[$"player-{index:D4}"] = random.Next(
                    0,
                    500
                );
            }

            var expected = context.ScoresByPlayerId
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Take(topSize)
            .Select(entry => (PlayerId: entry.Key, Score: entry.Value))
            .ToList();

            await context.LeaderboardProvider.TryRefreshLeaderboardAsync();
            var leaderboard = await context.LeaderboardProvider
            .GetLeaderboardAsync();

            Assert.NotNull(leaderboard);
            Assert.Equal(
                expected,
                leaderboard
                .Select(entry => (entry.PlayerId, entry.Score))
                .ToList()
            );
        }

        [Fact]
        public async Task PublishedSnapshotIsReadOnlyAndPersistsUntilRefresh()
        {
            using var context = new MemoryLeaderboardTestContext(2);
            context.ScoresByPlayerId["player-a"] = 100;

            await context.LeaderboardProvider.TryRefreshLeaderboardAsync();
            var firstRead = await context.LeaderboardProvider
            .GetLeaderboardAsync();

            Assert.NotNull(firstRead);
            var mutableView = Assert.IsAssignableFrom<
                IList<LeaderboardEntry>
            >(firstRead);

            Assert.True(mutableView.IsReadOnly);
            Assert.Throws<NotSupportedException>(() =>
                mutableView[0] = new LeaderboardEntry
                {
                    PlayerId = "changed-player",
                    Score = -1
                }
            );
            context.ScoresByPlayerId["player-a"] = 200;

            var retainedSnapshot = await context.LeaderboardProvider
            .GetLeaderboardAsync();

            Assert.NotNull(retainedSnapshot);
            Assert.Same(firstRead, retainedSnapshot);
            Assert.Equal(100, retainedSnapshot[0].Score);

            await context.LeaderboardProvider.TryRefreshLeaderboardAsync();
            var replacedSnapshot = await context.LeaderboardProvider
            .GetLeaderboardAsync();

            Assert.NotNull(replacedSnapshot);
            Assert.Equal(200, replacedSnapshot[0].Score);
        }

        [Fact]
        public async Task BusyRefreshReturnsFalseWithoutReplacingSnapshot()
        {
            using var context = new MemoryLeaderboardTestContext(2);
            context.ScoresByPlayerId["player-a"] = 100;
            await context.LeaderboardProvider.TryRefreshLeaderboardAsync();
            var refreshGate = context.GetSourceProperty<SemaphoreSlim>(
                "LeaderboardRefreshGate"
            );
            await refreshGate.WaitAsync();

            try
            {
                context.ScoresByPlayerId["player-a"] = 200;

                var refreshed = await context.LeaderboardProvider
                .TryRefreshLeaderboardAsync();
                var retained = await context.LeaderboardProvider
                .GetLeaderboardAsync();

                Assert.False(refreshed);
                Assert.NotNull(retained);
                Assert.Equal(100, retained[0].Score);
            }
            finally
            {
                refreshGate.Release();
            }
        }

        [Fact]
        public async Task CancelledRefreshPreservesPreviousSnapshot()
        {
            using var context = new MemoryLeaderboardTestContext(2);
            context.ScoresByPlayerId["player-a"] = 100;
            await context.LeaderboardProvider.TryRefreshLeaderboardAsync();
            context.ScoresByPlayerId["player-a"] = 200;
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                context.LeaderboardProvider.TryRefreshLeaderboardAsync(
                    cancellationTokenSource.Token
                )
            );
            var retained = await context.LeaderboardProvider
            .GetLeaderboardAsync();

            Assert.NotNull(retained);
            Assert.Equal(100, retained[0].Score);
        }

        [Fact]
        public async Task ConcurrentGiftsAndRefreshesPublishOnlyConservedSnapshots()
        {
            const long initialScore = 1000;
            const int giftCount = 300;
            using var context = new MemoryLeaderboardTestContext(2);
            await context.CreatePlayerAsync("player-a", initialScore);
            await context.CreatePlayerAsync("player-b", initialScore);
            await context.LeaderboardProvider.TryRefreshLeaderboardAsync();
            var snapshots = new List<IReadOnlyList<LeaderboardEntry>>();
            var snapshotsLock = new object();

            var giftTasks = Enumerable.Range(0, giftCount)
            .Select(index => context.GiftProvider.ExecuteGiftAsync(
                new GiftOperation
                {
                    SenderPlayerId = index % 2 == 0
                    ? "player-a"
                    : "player-b",
                    RecipientPlayerId = index % 2 == 0
                    ? "player-b"
                    : "player-a",
                    Points = 1,
                    RequestId = Guid.NewGuid()
                }
            ))
            .ToList();
            var refreshTasks = Enumerable.Range(0, 150)
            .Select(async _ =>
            {
                await context.LeaderboardProvider.TryRefreshLeaderboardAsync();
                var snapshot = await context.LeaderboardProvider
                .GetLeaderboardAsync();

                Assert.NotNull(snapshot);

                lock (snapshotsLock)
                    snapshots.Add(snapshot);
            })
            .ToList();

            await Task.WhenAll(giftTasks.Concat(refreshTasks));
            await context.LeaderboardProvider.TryRefreshLeaderboardAsync();
            var finalSnapshot = await context.LeaderboardProvider
            .GetLeaderboardAsync();

            Assert.NotNull(finalSnapshot);
            Assert.NotEmpty(snapshots);
            Assert.All(
                snapshots.Append(finalSnapshot),
                snapshot => Assert.Equal(
                    initialScore * 2,
                    snapshot.Sum(entry => entry.Score)
                )
            );
            Assert.Equal(
                initialScore * 2,
                context.ScoresByPlayerId.Values.Sum()
            );
        }
    }
}
