using System.Net;

namespace Exercise.Infra.Exceptions
{
    public class HttpException : Exception, IHttpException
    {
        public HttpStatusCode StatusCode { get; }

        public HttpException(
            string message,
            HttpStatusCode statusCode
        )
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
