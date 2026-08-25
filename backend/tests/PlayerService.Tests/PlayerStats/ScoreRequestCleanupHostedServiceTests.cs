using Microsoft.Extensions.Configuration;
using PlayerService.Shared.Models.PlayerStats;
using PlayerService.Tests.Sessions;
using PlayerService.WebApi.Infrastructure;

namespace PlayerService.Tests.PlayerStats
{
    public class ScoreRequestCleanupHostedServiceTests
    {
        [Fact]
        public async Task WorkerDiscoversThenDeletesExpiredCandidates()
        {
            var candidates = CreateCandidates();
            var provider = new ScoreRequestCleanupTestProvider(candidates);
            using var worker = CreateWorker(provider);

            await worker.StartAsync(CancellationToken.None);
            var deletedCandidates = await provider.DeletionObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(3)
            );
            await worker.StopAsync(CancellationToken.None);

            Assert.Equal(
                candidates.Select(CreateIdentity),
                deletedCandidates.Select(CreateIdentity)
            );
            Assert.Equal(1, provider.DeletionCount);
            Assert.True(provider.DiscoveryCount >= 1);
        }

        [Fact]
        public async Task WorkerSkipsDeletionWhenDiscoveryIsEmpty()
        {
            var provider = new ScoreRequestCleanupTestProvider([]);
            using var worker = CreateWorker(provider);

            await worker.StartAsync(CancellationToken.None);
            await provider.DiscoveryObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(3)
            );
            await worker.StopAsync(CancellationToken.None);

            Assert.Equal(0, provider.DeletionCount);
        }

        [Fact]
        public async Task WorkerContinuesAfterCleanupFailure()
        {
            var candidates = CreateCandidates();
            var provider = new ScoreRequestCleanupTestProvider(
                candidates,
                discoveryFailures: 1
            );
            using var worker = CreateWorker(provider);

            await worker.StartAsync(CancellationToken.None);
            var deletedCandidates = await provider.DeletionObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(4)
            );
            await worker.StopAsync(CancellationToken.None);

            Assert.Equal(
                candidates.Select(CreateIdentity),
                deletedCandidates.Select(CreateIdentity)
            );
            Assert.True(provider.DiscoveryCount >= 2);
            Assert.Equal(1, provider.DeletionCount);
        }

        private static ScoreRequestCleanupHostedService CreateWorker(
            ScoreRequestCleanupTestProvider provider
        )
        {
            var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlayerService:sessionCleanupIntervalInSeconds"] = "1"
            })
            .Build();
            var worker = new ScoreRequestCleanupHostedService(
                provider,
                new NullExerciseLogger(),
                configuration
            );

            return worker;
        }

        private static List<ScoreRequestCleanupCandidate> CreateCandidates()
        {
            var candidates = new List<ScoreRequestCleanupCandidate>
            {
                new()
                {
                    PlayerId = "player-1",
                    RequestId = Guid.NewGuid()
                },
                new()
                {
                    PlayerId = "player-2",
                    RequestId = Guid.NewGuid()
                }
            };

            return candidates;
        }

        private static string CreateIdentity(
            ScoreRequestCleanupCandidate candidate
        )
        {
            var identity = $"{candidate.PlayerId}:{candidate.RequestId}";

            return identity;
        }
    }
}
