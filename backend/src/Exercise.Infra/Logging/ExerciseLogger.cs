using System.Runtime.CompilerServices;
using Exercise.Infra.Configuration;
using Exercise.Infra.Exceptions;
using Microsoft.Extensions.Configuration;

namespace Exercise.Infra.Logging
{
    public class ExerciseLogger : IExerciseLogger
    {
        private readonly ICorrelationProvider _correlationProvider;
        private readonly IExerciseLoggerProvider _loggerProvider;
        private readonly string _serviceName;

        public ExerciseLogger(
            IConfiguration configuration,
            ICorrelationProvider correlationProvider,
            IExerciseLoggerProvider loggerProvider
        )
        {
            _correlationProvider = correlationProvider;
            _loggerProvider = loggerProvider;
            _serviceName = configuration.GetServiceName();
        }

        public Task LogTraceAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        )
        {
            return WriteAsync(
                ExerciseLogLevel.Trace,
                message,
                parameters,
                cancellationToken,
                callerFilePath
            );
        }

        public Task LogDebugAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        )
        {
            return WriteAsync(
                ExerciseLogLevel.Debug,
                message,
                parameters,
                cancellationToken,
                callerFilePath
            );
        }

        public Task LogInfoAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        )
        {
            return WriteAsync(
                ExerciseLogLevel.Information,
                message,
                parameters,
                cancellationToken,
                callerFilePath
            );
        }

        public Task LogWarningAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        )
        {
            return WriteAsync(
                ExerciseLogLevel.Warning,
                message,
                parameters,
                cancellationToken,
                callerFilePath
            );
        }

        public Task LogErrorAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        )
        {
            return WriteAsync(
                ExerciseLogLevel.Error,
                message,
                parameters,
                cancellationToken,
                callerFilePath
            );
        }

        public Task LogExceptionAsync(
            string message,
            Exception exception,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        )
        {
            var parameters = new ExceptionLogParameters
            {
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                Message = exception.Message,
                Parameters = exception.GetParameters(),
                StackTrace = exception.StackTrace
            };

            return WriteAsync(
                ExerciseLogLevel.Error,
                message,
                parameters,
                cancellationToken,
                callerFilePath
            );
        }

        private Task WriteAsync<TParameters>(
            ExerciseLogLevel level,
            string message,
            TParameters parameters,
            CancellationToken cancellationToken,
            string callerFilePath
        )
        {
            var log = new Log<TParameters>
            {
                CallerFilePath = Path.GetFileName(callerFilePath),
                Correlation = _correlationProvider.GetCorrelation(),
                Level = level,
                MachineName = Environment.MachineName,
                Message = message,
                Parameters = parameters,
                ServiceName = _serviceName,
                TimestampUtc = DateTimeOffset.UtcNow
            };

            return _loggerProvider.WriteAsync(
                log,
                cancellationToken
            );
        }
    }
}
