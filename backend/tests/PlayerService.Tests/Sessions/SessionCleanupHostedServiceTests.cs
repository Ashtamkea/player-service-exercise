using Microsoft.Extensions.Configuration;
using PlayerService.WebApi.Infrastructure;

namespace PlayerService.Tests.Sessions
{
    public class SessionCleanupHostedServiceTests
    {
        [Fact]
        public async Task WorkerDiscoversThenDeletesExpiredDeviceIds()
        {
            var expectedDeviceIds = new List<string>
            {
                "device-1",
                "device-2"
            };
            var provider = new SessionCleanupTestProvider(expectedDeviceIds);
            using var worker = CreateWorker(provider);

            await worker.StartAsync(CancellationToken.None);
            var deletedDeviceIds = await provider.DeletionObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(3)
            );
            await worker.StopAsync(CancellationToken.None);

            Assert.Equal(expectedDeviceIds, deletedDeviceIds);
            Assert.Equal(1, provider.DeletionCount);
            Assert.True(provider.DiscoveryCount >= 1);
        }

        [Fact]
        public async Task WorkerSkipsDeletionWhenDiscoveryIsEmpty()
        {
            var provider = new SessionCleanupTestProvider([]);
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
            var provider = new SessionCleanupTestProvider(
                ["device-1"],
                discoveryFailures: 1
            );
            using var worker = CreateWorker(provider);

            await worker.StartAsync(CancellationToken.None);
            var deletedDeviceIds = await provider.DeletionObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(4)
            );
            await worker.StopAsync(CancellationToken.None);

            Assert.Equal(["device-1"], deletedDeviceIds);
            Assert.True(provider.DiscoveryCount >= 2);
            Assert.Equal(1, provider.DeletionCount);
        }

        private static SessionCleanupHostedService CreateWorker(
            SessionCleanupTestProvider provider
        )
        {
            var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlayerService:sessionCleanupIntervalInSeconds"] = "1"
            })
            .Build();
            var worker = new SessionCleanupHostedService(
                provider,
                new NullExerciseLogger(),
                configuration
            );

            return worker;
        }
    }
}
