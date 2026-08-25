using System.Net;

namespace Exercise.Infra.Exceptions
{
    public static class ExceptionConstructor
    {
        public static Exception CreateBasic(string message)
        {
            var exception = new Exception(message);

            return exception;
        }

        public static ExceptionWithParameters<TParameters> CreateParameterized<TParameters>(
            string message,
            TParameters parameters
        )
        {
            var exception = new ExceptionWithParameters<TParameters>(
                message,
                parameters
            );

            return exception;
        }

        public static HttpException CreateHttp(
            string message,
            HttpStatusCode statusCode
        )
        {
            var exception = new HttpException(
                message,
                statusCode
            );

            return exception;
        }

        public static HttpExceptionWithParameters<TParameters> CreateParameterizedHttp<TParameters>(
            string message,
            HttpStatusCode statusCode,
            TParameters parameters
        )
        {
            var exception = new HttpExceptionWithParameters<TParameters>(
                message,
                statusCode,
                parameters
            );

            return exception;
        }
    }
}
