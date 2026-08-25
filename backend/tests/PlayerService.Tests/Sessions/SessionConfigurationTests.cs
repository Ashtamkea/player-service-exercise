using System.Text.Json;

namespace PlayerService.Tests.Sessions
{
    public class SessionConfigurationTests
    {
        [Fact]
        public void ProductionSessionTtlIsFiveMinutes()
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
            var ttlInSeconds = playerService
            .GetProperty("sessionTtlInSeconds")
            .GetInt32();
            var cleanupIntervalInSeconds = playerService
            .GetProperty("sessionCleanupIntervalInSeconds")
            .GetInt32();

            Assert.Equal(300, ttlInSeconds);
            Assert.Equal(30, cleanupIntervalInSeconds);
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
