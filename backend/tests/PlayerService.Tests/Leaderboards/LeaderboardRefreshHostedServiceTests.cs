using Microsoft.Extensions.Configuration;
using PlayerService.WebApi.Infrastructure;
using PlayerService.Tests.Sessions;

namespace PlayerService.Tests.Leaderboards
{
    public class LeaderboardRefreshHostedServiceTests
    {
        [Fact]
        public async Task WorkerRefreshesImmediatelyAndRepeatsAtConfiguredInterval()
        {
            var service = new LeaderboardRefreshTestService();
            using var worker = CreateWorker(service);

            await worker.StartAsync(CancellationToken.None);
            await service.FirstRefreshObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(2)
            );
            await service.SecondRefreshObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(3)
            );
            await worker.StopAsync(CancellationToken.None);

            Assert.True(service.RefreshCount >= 2);
        }

        [Fact]
        public async Task WorkerContinuesAfterRefreshFailure()
        {
            var service = new LeaderboardRefreshTestService(
                refreshFailures: 1
            );
            using var worker = CreateWorker(service);

            await worker.StartAsync(CancellationToken.None);
            await service.SecondRefreshObserved.Task.WaitAsync(
                TimeSpan.FromSeconds(3)
            );
            await worker.StopAsync(CancellationToken.None);

            Assert.True(service.RefreshCount >= 2);
        }

        private static LeaderboardRefreshHostedService CreateWorker(
            LeaderboardRefreshTestService service
        )
        {
            var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlayerService:leaderboardPollIntervalInSeconds"] = "1"
            })
            .Build();
            var worker = new LeaderboardRefreshHostedService(
                service,
                new NullExerciseLogger(),
                configuration
            );

            return worker;
        }
    }
}
