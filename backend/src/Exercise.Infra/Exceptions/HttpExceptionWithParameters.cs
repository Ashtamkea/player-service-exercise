using System.Net;

namespace Exercise.Infra.Exceptions
{
    public class HttpExceptionWithParameters<TParameters> : HttpException, IExceptionWithParameters<TParameters>
    {
        public TParameters Parameters { get; }

        public HttpExceptionWithParameters(
            string message,
            HttpStatusCode statusCode,
            TParameters parameters
        )
            : base(
                message,
                statusCode
            )
        {
            Parameters = parameters;
        }
    }
}
