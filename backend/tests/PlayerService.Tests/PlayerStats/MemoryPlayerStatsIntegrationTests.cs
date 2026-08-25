using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using PlayerService.Lib.DAL.Models.Memory;
using PlayerService.Lib.DAL.Providers.Memory;
using PlayerService.Lib.DAL.Sources;
using PlayerService.Shared.Models.PlayerStats;
using PlayerStatsModel = PlayerService.Shared.Models.PlayerStats.PlayerStats;

namespace PlayerService.Tests.PlayerStats
{
    public class MemoryPlayerStatsIntegrationTests : IDisposable
    {
        private readonly MemoryGameDataSource _gameDataSource;
        private readonly PlayerStatsMemoryProvider _provider;
        private readonly ConcurrentDictionary<string, MemoryPlayer> _playersById;
        private readonly ConcurrentDictionary<string, long> _scoresByPlayerId;

        public MemoryPlayerStatsIntegrationTests()
        {
            var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlayerService:scoreRequestTtlInSeconds"] = "1"
            })
            .Build();

            _gameDataSource = new MemoryGameDataSource();
            _provider = new PlayerStatsMemoryProvider(
                _gameDataSource,
                configuration
            );
            _playersById = GetSourceProperty<
                ConcurrentDictionary<string, MemoryPlayer>
            >("PlayersById");
            _scoresByPlayerId = GetSourceProperty<
                ConcurrentDictionary<string, long>
            >("ScoresByPlayerId");
        }

        [Fact]
        public async Task FirstRequestStoresScoreAndConfiguredExpiration()
        {
            var playerId = CreateIdentifier("player");
            var requestId = Guid.NewGuid();
            var before = DateTime.UtcNow;

            var result = await AddScoreAsync(
                playerId,
                100,
                requestId
            );
            var after = DateTime.UtcNow;
            var player = _playersById[playerId];
            var request = player.ScoreRequestsByRequestId[requestId];

            Assert.True(result.Applied);
            Assert.Equal(100, result.Stats.Score);
            Assert.Equal(100, _scoresByPlayerId[playerId]);
            Assert.InRange(
                request.ExpiresAtUtc,
                before.AddSeconds(1),
                after.AddSeconds(1)
            );
        }

        [Fact]
        public async Task ScoreUpdateRequiresAnExistingCanonicalPlayer()
        {
            var playerId = CreateIdentifier("missing-player");

            var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
                _provider.AddScoreAsync(
                    playerId,
                    10,
                    Guid.NewGuid()
                )
            );

            Assert.Contains(
                "Canonical player state could not be found.",
                exception.Message,
                StringComparison.Ordinal
            );
            Assert.False(_playersById.ContainsKey(playerId));
            Assert.False(_scoresByPlayerId.ContainsKey(playerId));
        }

        [Fact]
        public async Task StatsReadReturnsDetachedAuthoritativeSnapshot()
        {
            var playerId = CreateIdentifier("player");
            var player = EnsurePlayer(playerId);
            var lastActiveUtc = DateTime.UtcNow.AddMinutes(1);
            player.GiftsSent = 3;
            player.GiftsReceived = 4;
            player.RecordActivity(lastActiveUtc);
            _scoresByPlayerId[playerId] = 1250;

            var playerStats = await _provider.GetPlayerStatsAsync(playerId);

            Assert.NotNull(playerStats);
            Assert.Equal(1250, playerStats.Score);
            Assert.Equal(3, playerStats.GiftsSent);
            Assert.Equal(4, playerStats.GiftsReceived);
            Assert.Equal(lastActiveUtc, playerStats.LastActiveUtc);

            player.GiftsSent = 10;
            _scoresByPlayerId[playerId] = 2000;

            Assert.Equal(1250, playerStats.Score);
            Assert.Equal(3, playerStats.GiftsSent);
        }

        [Fact]
        public async Task MissingPlayerStatsReturnsNull()
        {
            var playerStats = await _provider.GetPlayerStatsAsync(
                CreateIdentifier("missing-player")
            );

            Assert.Null(playerStats);
        }

        [Fact]
        public async Task StatsReadDoesNotWaitForPlayerLock()
        {
            var playerId = CreateIdentifier("player");
            var player = EnsurePlayer(playerId);
            _scoresByPlayerId[playerId] = 1000;
            await player.PlayerLock.WaitAsync();

            try
            {
                var playerStatsTask = _provider.GetPlayerStatsAsync(playerId);
                var playerStats = Assert.IsType<PlayerStatsModel>(
                    await playerStatsTask.WaitAsync(TimeSpan.FromSeconds(1))
                );

                Assert.Equal(1000, playerStats.Score);
                Assert.Equal(0, playerStats.GiftsSent);
            }
            finally
            {
                player.PlayerLock.Release();
            }
        }

        [Fact]
        public async Task CheckedAdditionRejectsOverflowWithoutMutation()
        {
            var playerId = CreateIdentifier("player");
            var requestId = Guid.NewGuid();
            var player = EnsurePlayer(playerId);
            _scoresByPlayerId[playerId] = long.MaxValue;

            await Assert.ThrowsAsync<OverflowException>(() =>
                _provider.AddScoreAsync(
                    playerId,
                    1,
                    requestId
                )
            );

            Assert.Equal(long.MaxValue, _scoresByPlayerId[playerId]);
            Assert.False(player.ScoreRequestsByRequestId.ContainsKey(requestId));
        }

        [Fact]
        public async Task SequentialDuplicateReturnsCurrentStatsWithoutRefreshingMarker()
        {
            var playerId = CreateIdentifier("player");
            var firstRequestId = Guid.NewGuid();
            var secondRequestId = Guid.NewGuid();
            var first = await AddScoreAsync(
                playerId,
                10,
                firstRequestId
            );
            var player = _playersById[playerId];
            var originalMarker = player.ScoreRequestsByRequestId[firstRequestId];

            await AddScoreAsync(playerId, 20, secondRequestId);
            var duplicate = await AddScoreAsync(
                playerId,
                999,
                firstRequestId
            );
            var retainedMarker = player.ScoreRequestsByRequestId[firstRequestId];

            Assert.Equal(10, first.Stats.Score);
            Assert.False(duplicate.Applied);
            Assert.Equal(30, duplicate.Stats.Score);
            Assert.Equal(30, _scoresByPlayerId[playerId]);
            Assert.Same(originalMarker, retainedMarker);
            Assert.Equal(
                originalMarker.ExpiresAtUtc,
                retainedMarker.ExpiresAtUtc
            );
        }

        [Fact]
        public async Task SimultaneousDuplicateAppliesExactlyOnce()
        {
            var playerId = CreateIdentifier("player");
            var requestId = Guid.NewGuid();

            var results = await Task.WhenAll(
                AddScoreAsync(playerId, 25, requestId),
                AddScoreAsync(playerId, 25, requestId)
            );

            Assert.All(results, result => Assert.Equal(25, result.Stats.Score));
            Assert.Single(results, result => result.Applied);
            Assert.Single(results, result => !result.Applied);
            Assert.Equal(25, _scoresByPlayerId[playerId]);
        }

        [Fact]
        public async Task ManyConcurrentUniqueRequestsProduceExactSum()
        {
            const int requestCount = 200;
            var playerId = CreateIdentifier("player");
            var tasks = Enumerable.Range(1, requestCount)
            .Select(points => AddScoreAsync(
                playerId,
                points,
                Guid.NewGuid()
            ))
            .ToList();

            var results = await Task.WhenAll(tasks);
            var expectedScore = Enumerable.Range(1, requestCount).Sum(
                points => (long)points
            );

            Assert.All(results, result => Assert.True(result.Applied));
            Assert.Equal(expectedScore, _scoresByPlayerId[playerId]);
        }

        [Fact]
        public async Task IdenticalRequestIdsAreIndependentAcrossPlayers()
        {
            var firstPlayerId = CreateIdentifier("player-a");
            var secondPlayerId = CreateIdentifier("player-b");
            var requestId = Guid.NewGuid();

            var results = await Task.WhenAll(
                AddScoreAsync(firstPlayerId, 20, requestId),
                AddScoreAsync(secondPlayerId, 30, requestId)
            );

            Assert.Equal(
                [20L, 30L],
                results.Select(result => result.Stats.Score)
            );
            Assert.All(results, result => Assert.True(result.Applied));
        }

        [Fact]
        public async Task ExpiredMarkerRemainsAuthoritativeUntilPhysicallyDeleted()
        {
            var playerId = CreateIdentifier("player");
            var requestId = Guid.NewGuid();
            var first = await AddScoreAsync(
                playerId,
                10,
                requestId
            );
            var player = _playersById[playerId];
            player.ScoreRequestsByRequestId[requestId] = new MemoryScoreRequest
            {
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
            };

            var duplicate = await AddScoreAsync(
                playerId,
                100,
                requestId
            );

            Assert.False(duplicate.Applied);
            Assert.Equal(first.Stats.Score, duplicate.Stats.Score);
            Assert.Equal(10, _scoresByPlayerId[playerId]);
        }

        [Fact]
        public async Task DiscoveryReturnsOnlyExpiredPlayerRequestPairs()
        {
            var firstPlayerId = CreateIdentifier("player-a");
            var secondPlayerId = CreateIdentifier("player-b");
            var expiredRequestId = Guid.NewGuid();
            var activeRequestId = Guid.NewGuid();
            var otherExpiredRequestId = Guid.NewGuid();
            SetMarker(firstPlayerId, expiredRequestId, DateTime.UtcNow.AddMinutes(-1));
            SetMarker(firstPlayerId, activeRequestId, DateTime.UtcNow.AddMinutes(1));
            SetMarker(
                secondPlayerId,
                otherExpiredRequestId,
                DateTime.UtcNow.AddMinutes(-1)
            );

            var candidates = await _provider
            .GetExpiredScoreRequestCandidatesAsync();

            Assert.Equal(2, candidates.Count);
            Assert.Contains(
                candidates,
                candidate => candidate.PlayerId == firstPlayerId
                    && candidate.RequestId == expiredRequestId
            );
            Assert.Contains(
                candidates,
                candidate => candidate.PlayerId == secondPlayerId
                    && candidate.RequestId == otherExpiredRequestId
            );
            Assert.DoesNotContain(
                candidates,
                candidate => candidate.RequestId == activeRequestId
            );
        }

        [Fact]
        public async Task DeletionRevalidatesCandidatesAndHandlesDuplicatesAndMissingMarkers()
        {
            var playerId = CreateIdentifier("player");
            var expiredRequestId = Guid.NewGuid();
            var refreshedRequestId = Guid.NewGuid();
            var missingRequestId = Guid.NewGuid();
            SetMarker(playerId, expiredRequestId, DateTime.UtcNow.AddMinutes(-1));
            SetMarker(playerId, refreshedRequestId, DateTime.UtcNow.AddMinutes(-1));
            var discovered = await _provider
            .GetExpiredScoreRequestCandidatesAsync();

            SetMarker(playerId, refreshedRequestId, DateTime.UtcNow.AddMinutes(1));
            discovered.Add(new ScoreRequestCleanupCandidate
            {
                PlayerId = playerId,
                RequestId = expiredRequestId
            });
            discovered.Add(new ScoreRequestCleanupCandidate
            {
                PlayerId = playerId,
                RequestId = missingRequestId
            });

            var deletedCount = await _provider.DeleteExpiredScoreRequestsAsync(
                discovered
            );
            var player = _playersById[playerId];

            Assert.Equal(1, deletedCount);
            Assert.False(player.ScoreRequestsByRequestId.ContainsKey(expiredRequestId));
            Assert.True(player.ScoreRequestsByRequestId.ContainsKey(refreshedRequestId));
        }

        [Fact]
        public async Task RequestAppliesAgainAfterCleanupDeletesMarker()
        {
            var playerId = CreateIdentifier("player");
            var requestId = Guid.NewGuid();
            var first = await AddScoreAsync(
                playerId,
                10,
                requestId
            );
            var player = _playersById[playerId];
            player.ScoreRequestsByRequestId[requestId] = new MemoryScoreRequest
            {
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
            };
            var candidates = await _provider
            .GetExpiredScoreRequestCandidatesAsync();

            var deletedCount = await _provider.DeleteExpiredScoreRequestsAsync(
                candidates
            );
            var second = await AddScoreAsync(
                playerId,
                10,
                requestId
            );

            Assert.Equal(1, deletedCount);
            Assert.True(second.Applied);
            Assert.Equal(20, second.Stats.Score);
        }

        public void Dispose()
        {
            _gameDataSource.Dispose();
            GC.SuppressFinalize(this);
        }

        private Task<ScoreUpdateResult> AddScoreAsync(
            string playerId,
            int points,
            Guid requestId
        )
        {
            EnsurePlayer(playerId);
            var task = _provider.AddScoreAsync(
                playerId,
                points,
                requestId
            );

            return task;
        }

        private MemoryPlayer EnsurePlayer(string playerId)
        {
            if (_playersById.TryGetValue(playerId, out var existingPlayer))
                return existingPlayer;

            var candidate = new MemoryPlayer
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
            var player = _playersById.GetOrAdd(playerId, candidate);

            if (!ReferenceEquals(player, candidate))
                candidate.PlayerLock.Dispose();

            return player;
        }

        private void SetMarker(
            string playerId,
            Guid requestId,
            DateTime expiresAtUtc
        )
        {
            var player = EnsurePlayer(playerId);
            player.ScoreRequestsByRequestId[requestId] = new MemoryScoreRequest
            {
                ExpiresAtUtc = expiresAtUtc
            };
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

        private static string CreateIdentifier(string prefix)
        {
            var identifier = $"{prefix}-{Guid.NewGuid():N}";

            return identifier;
        }
    }
}
