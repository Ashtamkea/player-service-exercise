using System.Runtime.CompilerServices;
using Exercise.Infra.Logging;

namespace PlayerService.Tests.Sessions
{
    public class NullExerciseLogger : IExerciseLogger
    {
        public Task LogTraceAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        )
        {
            return Task.CompletedTask;
        }

        public Task LogDebugAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        )
        {
            return Task.CompletedTask;
        }

        public Task LogInfoAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        )
        {
            return Task.CompletedTask;
        }

        public Task LogWarningAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        )
        {
            return Task.CompletedTask;
        }

        public Task LogErrorAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        )
        {
            return Task.CompletedTask;
        }

        public Task LogExceptionAsync(
            string message,
            Exception exception,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        )
        {
            return Task.CompletedTask;
        }
    }
}
