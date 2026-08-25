using Exercise.Infra.Configuration;
using Exercise.Infra.Exceptions;
using Exercise.Infra.Logging;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.DAL.Providers;

namespace PlayerService.WebApi.Infrastructure
{
    public class SessionCleanupHostedService : BackgroundService
    {
        private readonly IExerciseLogger _logger;
        private readonly ISessionProvider _sessionProvider;
        private readonly TimeSpan _cleanupInterval;

        public SessionCleanupHostedService(
            ISessionProvider sessionProvider,
            IExerciseLogger logger,
            IConfiguration configuration
        )
        {
            _sessionProvider = sessionProvider;
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
                    "Session cleanup interval must be greater than zero.",
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
                        var deviceIds = await _sessionProvider
                        .GetExpiredSessionDeviceIdsAsync(stoppingToken);

                        if (deviceIds.Count == 0)
                            continue;

                        var deletedCount = await _sessionProvider.DeleteExpiredSessionsAsync(
                            deviceIds,
                            stoppingToken
                        );

                        #region Log

                        await _logger.LogInfoAsync(
                            "Bulk operation completed",
                            new
                            {
                                OperationName = "Session cleanup",
                                EntityName = "Session",
                                RequestedCount = deviceIds.Count,
                                AffectedCount = deletedCount,
                                SkippedCount = deviceIds.Count - deletedCount,
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
                            "Session cleanup failed",
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
