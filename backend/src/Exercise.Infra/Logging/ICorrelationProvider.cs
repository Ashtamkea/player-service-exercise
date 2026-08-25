namespace Exercise.Infra.Logging
{
    public interface ICorrelationProvider
    {
        Correlation GetCorrelation();

        void SetCorrelation(Correlation correlation);
    }
}
