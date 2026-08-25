using Microsoft.Extensions.Configuration;
using PlayerService.Shared.Models.PlayerGifts;
using PlayerService.Tests.Sessions;
using PlayerService.WebApi.Infrastructure;

namespace PlayerService.Tests.PlayerGifts
{
    public class GiftRequestCleanupHostedServiceTests
    {
        [Fact]
        public async Task WorkerDiscoversThenDeletesExpiredCandidates()
        {
            var candidates = CreateCandidates();
            var provider = new GiftRequestCleanupTestProvider(candidates);
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
            var provider = new GiftRequestCleanupTestProvider([]);
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
            var provider = new GiftRequestCleanupTestProvider(
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

        private static GiftRequestCleanupHostedService CreateWorker(
            GiftRequestCleanupTestProvider provider
        )
        {
            var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlayerService:sessionCleanupIntervalInSeconds"] = "1"
            })
            .Build();
            var worker = new GiftRequestCleanupHostedService(
                provider,
                new NullExerciseLogger(),
                configuration
            );

            return worker;
        }

        private static List<GiftRequestCleanupCandidate> CreateCandidates()
        {
            var candidates = new List<GiftRequestCleanupCandidate>
            {
                new()
                {
                    SenderPlayerId = "player-1",
                    RequestId = Guid.NewGuid()
                },
                new()
                {
                    SenderPlayerId = "player-2",
                    RequestId = Guid.NewGuid()
                }
            };

            return candidates;
        }

        private static string CreateIdentity(
            GiftRequestCleanupCandidate candidate
        )
        {
            var identity = $"{candidate.SenderPlayerId}:{candidate.RequestId}";

            return identity;
        }
    }
}
