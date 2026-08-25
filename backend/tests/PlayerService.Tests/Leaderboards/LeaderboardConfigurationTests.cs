using System.Text.Json;

namespace PlayerService.Tests.Leaderboards
{
    public class LeaderboardConfigurationTests
    {
        [Fact]
        public void ProductionLeaderboardConfigurationMatchesContract()
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
                100,
                playerService.GetProperty("leaderboardTopSize").GetInt32()
            );
            Assert.Equal(
                180,
                playerService
                .GetProperty("leaderboardPollIntervalInSeconds")
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
