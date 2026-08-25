using System.Net;
using Exercise.Infra.Exceptions;
using Exercise.Infra.Logging;
using Microsoft.AspNetCore.Http;

namespace Exercise.Infra.WebApi
{
    public class HttpExceptionHandler
    {
        private readonly RequestDelegate _next;

        public HttpExceptionHandler(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IExerciseLogger logger
        )
        {
            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                var statusCode = exception is IHttpException httpException
                ? httpException.StatusCode
                : HttpStatusCode.InternalServerError;

                if ((int)statusCode < StatusCodes.Status500InternalServerError)
                {
                    #region Log

                    await logger.LogWarningAsync(
                        "Request rejected",
                        new
                        {
                            Method = context.Request.Method,
                            Path = context.Request.Path.Value,
                            StatusCode = (int)statusCode,
                            Reason = exception.Message
                        },
                        context.RequestAborted
                    );

                    #endregion
                }
                else
                {
                    #region Log

                    await logger.LogExceptionAsync(
                        "Unhandled request failed",
                        exception,
                        context.RequestAborted
                    );

                    #endregion
                }

                var message = exception is IHttpException
                ? exception.Message
                : "An internal server error occurred.";

                var response = new
                {
                    error = message,
                    statusCode = (int)statusCode
                };

                context.Response.StatusCode = (int)statusCode;
                await context.Response.WriteAsJsonAsync(
                    response,
                    context.RequestAborted
                );
            }
        }
    }
}
