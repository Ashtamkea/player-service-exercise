namespace Exercise.Infra.Logging
{
    public interface ICorrelationCreator
    {
        Correlation CreateCorrelation(string? correlationId = null);
    }
}
