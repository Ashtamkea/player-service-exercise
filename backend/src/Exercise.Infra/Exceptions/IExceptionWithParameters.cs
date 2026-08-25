namespace Exercise.Infra.Exceptions
{
    public interface IExceptionWithParameters<out TParameters>
    {
        TParameters Parameters { get; }
    }
}
