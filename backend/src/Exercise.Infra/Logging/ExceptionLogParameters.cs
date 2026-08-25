namespace Exercise.Infra.Logging
{
    public class ExceptionLogParameters
    {
        public required string ExceptionType { get; set; }
        public required string Message { get; set; }
        public object? Parameters { get; set; }
        public string? StackTrace { get; set; }
    }
}
