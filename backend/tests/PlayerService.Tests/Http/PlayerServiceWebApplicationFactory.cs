using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlayerService.WebApi;

namespace PlayerService.Tests.Http
{
    public class PlayerServiceWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _configurationValues;

        public PlayerServiceWebApplicationFactory(
            int sessionTtlInSeconds = 3,
            int scoreRequestTtlInSeconds = 3600,
            int giftRequestTtlInSeconds = 3600,
            int giftRateLimitWindowInSeconds = 60,
            int giftRateLimitMaxRequests = 50,
            int leaderboardTopSize = 100
        )
        {
            _configurationValues = new Dictionary<string, string?>
            {
                ["PlayerService:sessionTtlInSeconds"] = sessionTtlInSeconds.ToString(),
                ["PlayerService:sessionCleanupIntervalInSeconds"] = "30",
                ["PlayerService:scoreRequestTtlInSeconds"] = scoreRequestTtlInSeconds.ToString(),
                ["PlayerService:giftRequestTtlInSeconds"] = giftRequestTtlInSeconds.ToString(),
                ["PlayerService:giftRateLimitWindowInSeconds"] = giftRateLimitWindowInSeconds.ToString(),
                ["PlayerService:giftRateLimitMaxRequests"] = giftRateLimitMaxRequests.ToString(),
                ["PlayerService:leaderboardTopSize"] = leaderboardTopSize.ToString(),
                ["PlayerService:leaderboardPollIntervalInSeconds"] = "180"
            };
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("SystemTests");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(
                    _configurationValues
                );
            });
            builder.ConfigureServices(services =>
            {
                services
                .AddControllers()
                .AddApplicationPart(
                    typeof(AuthenticatedProbeController).Assembly
                );
            });
        }
    }
}
