using Exercise.Infra.Configuration;
using Exercise.Infra.Exceptions;
using Exercise.Infra.Logging;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.DAL.Providers;

namespace PlayerService.WebApi.Infrastructure
{
    public class ScoreRequestCleanupHostedService : BackgroundService
    {
        private readonly IExerciseLogger _logger;
        private readonly IPlayerStatsProvider _playerStatsProvider;
        private readonly TimeSpan _cleanupInterval;

        public ScoreRequestCleanupHostedService(
            IPlayerStatsProvider playerStatsProvider,
            IExerciseLogger logger,
            IConfiguration configuration
        )
        {
            _playerStatsProvider = playerStatsProvider;
            _logger = logger;
            _cleanupInterval = TimeSpan.FromSeconds(
                configuration.GetSectionValue<int>(
                    ConfigurationKeys.PlayerServiceSection,
                    ConfigurationKeys.SessionCleanupIntervalInSeconds
                )
            );

            if (_cleanupInterval <= TimeSpan.Zero)
            {
                #region Exception

                throw ExceptionConstructor.CreateParameterized(
                    "Score request cleanup interval must be greater than zero.",
                    new
                    {
                        CleanupInterval = _cleanupInterval
                    }
                );

                #endregion
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_cleanupInterval);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        var candidates = await _playerStatsProvider
                        .GetExpiredScoreRequestCandidatesAsync(stoppingToken);

                        if (candidates.Count == 0)
                            continue;

                        var deletedCount = await _playerStatsProvider.DeleteExpiredScoreRequestsAsync(
                            candidates,
                            stoppingToken
                        );

                        #region Log

                        await _logger.LogInfoAsync(
                            "Bulk operation completed",
                            new
                            {
                                OperationName = "Score request cleanup",
                                EntityName = "Score request",
                                RequestedCount = candidates.Count,
                                AffectedCount = deletedCount,
                                SkippedCount = candidates.Count - deletedCount,
                                FailureCount = 0
                            },
                            stoppingToken
                        );

                        #endregion
                    }
                    catch (OperationCanceledException) when (
                        stoppingToken.IsCancellationRequested
                    )
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        #region Log

                        await _logger.LogExceptionAsync(
                            "Score request cleanup failed",
                            exception,
                            stoppingToken
                        );

                        #endregion
                    }
                }
            }
            catch (OperationCanceledException) when (
                stoppingToken.IsCancellationRequested
            )
            {
            }
        }
    }
}
