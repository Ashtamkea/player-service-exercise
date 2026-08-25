using System.Text.Json;

namespace PlayerService.Tests.PlayerGifts
{
    public class PlayerGiftConfigurationTests
    {
        [Fact]
        public void ProductionGiftConfigurationMatchesContract()
        {
            var backendDirectory = FindBackendDirectory();
            var appSettingsPath = Path.Combine(
                backendDirectory,
                "src",
                "PlayerService.WebApi",
                "appsettings.json"
            );
            using var appSettingsStream = File.OpenRead(appSettingsPath);
            using var document = JsonDocument.Parse(appSettingsStream);
            var playerService = document.RootElement.GetProperty(
                "PlayerService"
            );

            Assert.Equal(
                3600,
                playerService.GetProperty("giftRequestTtlInSeconds").GetInt32()
            );
            Assert.Equal(
                60,
                playerService
                .GetProperty("giftRateLimitWindowInSeconds")
                .GetInt32()
            );
            Assert.Equal(
                50,
                playerService
                .GetProperty("giftRateLimitMaxRequests")
                .GetInt32()
            );
        }

        private static string FindBackendDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                var solutionPath = Path.Combine(
                    directory.FullName,
                    "PlayerServiceExercise.sln"
                );

                if (File.Exists(solutionPath))
                    return directory.FullName;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the backend solution directory."
            );
        }
    }
}
