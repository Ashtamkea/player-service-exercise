using Exercise.Infra.Common;
using Exercise.Infra.Logging;
using Microsoft.AspNetCore.Http;

namespace Exercise.Infra.WebApi
{
    public class HttpCorrelationSetter
    {
        private readonly RequestDelegate _next;

        public HttpCorrelationSetter(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ICorrelationCreator correlationCreator,
            ICorrelationProvider correlationProvider
        )
        {
            var correlationId = context.Request.Headers[CommonConstantValues.CorrelationHeader].FirstOrDefault();
            var correlation = correlationCreator.CreateCorrelation(correlationId);
            correlationProvider.SetCorrelation(correlation);
            context.Response.Headers[CommonConstantValues.CorrelationHeader] = correlation.Id;

            await _next(context);
        }
    }
}
