using Exercise.Infra.Configuration;
using Exercise.Infra.Exceptions;
using Exercise.Infra.Logging;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Services;

namespace PlayerService.WebApi.Infrastructure
{
    public class LeaderboardRefreshHostedService : BackgroundService
    {
        private readonly ILeaderboardService _leaderboardService;
        private readonly IExerciseLogger _logger;
        private readonly TimeSpan _pollInterval;

        public LeaderboardRefreshHostedService(
            ILeaderboardService leaderboardService,
            IExerciseLogger logger,
            IConfiguration configuration
        )
        {
            _leaderboardService = leaderboardService;
            _logger = logger;
            _pollInterval = TimeSpan.FromSeconds(
                configuration.GetSectionValue<int>(
                    ConfigurationKeys.PlayerServiceSection,
                    ConfigurationKeys.LeaderboardPollIntervalInSeconds
                )
            );

            if (_pollInterval <= TimeSpan.Zero)
            {
                #region Exception

                throw ExceptionConstructor.CreateParameterized(
                    "Leaderboard poll interval must be greater than zero.",
                    new
                    {
                        PollInterval = _pollInterval
                    }
                );

                #endregion
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _leaderboardService.TryRefreshAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    #region Log

                    await _logger.LogExceptionAsync(
                        "Leaderboard refresh failed",
                        exception,
                        stoppingToken
                    );

                    #endregion
                }

                await Task.Delay(_pollInterval, stoppingToken);
            }
        }
    }
}
