using System.Text.Json;

namespace PlayerService.Tests.PlayerStats
{
    public class PlayerStatsConfigurationTests
    {
        [Fact]
        public void ProductionScoreRequestTtlIsOneHour()
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

            var ttlInSeconds = document.RootElement
            .GetProperty("PlayerService")
            .GetProperty("scoreRequestTtlInSeconds")
            .GetInt32();

            Assert.Equal(3600, ttlInSeconds);
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
