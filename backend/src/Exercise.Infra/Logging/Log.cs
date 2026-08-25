namespace Exercise.Infra.Logging
{
    public class Log<TParameters>
    {
        public required string CallerFilePath { get; set; }
        public Correlation? Correlation { get; set; }
        public required ExerciseLogLevel Level { get; set; }
        public required string MachineName { get; set; }
        public required string Message { get; set; }
        public required TParameters Parameters { get; set; }
        public required string ServiceName { get; set; }
        public required DateTimeOffset TimestampUtc { get; set; }
    }
}
