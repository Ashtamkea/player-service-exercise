using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Exercise.Infra.Logging
{
    public class ExerciseConsoleLoggerProvider : IExerciseLoggerProvider
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };
        private readonly ILogger<ExerciseConsoleLoggerProvider> _logger;

        public ExerciseConsoleLoggerProvider(ILogger<ExerciseConsoleLoggerProvider> logger)
        {
            _logger = logger;
        }

        public Task WriteAsync<TParameters>(
            Log<TParameters> log,
            CancellationToken cancellationToken = default
        )
        {
            var logLevel = ToMicrosoftLogLevel(log.Level);
            var serializedLog = JsonSerializer.Serialize(
                log,
                SerializerOptions
            );

            _logger.Log(
                logLevel,
                "{Log}",
                serializedLog
            );

            return Task.CompletedTask;
        }

        private static LogLevel ToMicrosoftLogLevel(ExerciseLogLevel logLevel)
        {
            var microsoftLogLevel = logLevel switch
            {
                ExerciseLogLevel.Trace => LogLevel.Trace,
                ExerciseLogLevel.Debug => LogLevel.Debug,
                ExerciseLogLevel.Information => LogLevel.Information,
                ExerciseLogLevel.Warning => LogLevel.Warning,
                ExerciseLogLevel.Error => LogLevel.Error,
                _ => LogLevel.Information
            };

            return microsoftLogLevel;
        }
    }
}
