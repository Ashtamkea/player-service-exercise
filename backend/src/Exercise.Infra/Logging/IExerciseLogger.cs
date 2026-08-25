using System.Runtime.CompilerServices;

namespace Exercise.Infra.Logging
{
    public interface IExerciseLogger
    {
        Task LogTraceAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        );

        Task LogDebugAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        );

        Task LogInfoAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        );

        Task LogWarningAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        );

        Task LogErrorAsync<TParameters>(
            string message,
            TParameters parameters,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        );

        Task LogExceptionAsync(
            string message,
            Exception exception,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string callerFilePath = ""
        );
    }
}
