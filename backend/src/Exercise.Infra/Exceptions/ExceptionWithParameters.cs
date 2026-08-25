namespace Exercise.Infra.Exceptions
{
    public class ExceptionWithParameters<TParameters> : Exception, IExceptionWithParameters<TParameters>
    {
        public TParameters Parameters { get; }

        public ExceptionWithParameters(
            string message,
            TParameters parameters
        )
            : base(message)
        {
            Parameters = parameters;
        }
    }
}
