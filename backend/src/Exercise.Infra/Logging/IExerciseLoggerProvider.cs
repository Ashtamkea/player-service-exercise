namespace Exercise.Infra.Logging
{
    public interface IExerciseLoggerProvider
    {
        Task WriteAsync<TParameters>(
            Log<TParameters> log,
            CancellationToken cancellationToken = default
        );
    }
}
