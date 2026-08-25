using System.Net;
using Exercise.Infra.Common;
using Exercise.Infra.Exceptions;
using Microsoft.AspNetCore.Authorization;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.Sessions.Enums;
using PlayerService.Shared.Services;

namespace PlayerService.WebApi.Infrastructure
{
    public class SessionAuthenticationMiddleware
    {
        private const string BearerPrefix = "Bearer ";
        private readonly RequestDelegate _next;

        public SessionAuthenticationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ISessionService sessionService
        )
        {
            var endpoint = context.GetEndpoint();

            if (endpoint is null || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                await _next(context);

                return;
            }

            var deviceId = GetRequiredHeader(
                context,
                ConstantValues.DeviceIdHeader
            );
            var sessionId = GetBearerSessionId(context);

            if (deviceId.Contains('{') || deviceId.Contains('}'))
                throw CreateUnauthorizedException();

            var authenticationResult = await sessionService.AuthenticateAndExtendSessionAsync(
                deviceId,
                sessionId,
                context.RequestAborted
            );

            if (
                authenticationResult.Status != SessionAuthenticationStatus.Succeeded
                || authenticationResult.Context is null
            )
                throw CreateUnauthorizedException();

            context.Items[ConstantValues.SessionContextItemName] = authenticationResult.Context;

            await _next(context);
        }

        private static string GetRequiredHeader(HttpContext context, string headerName)
        {
            if (
                !context.Request.Headers.TryGetValue(headerName, out var headerValues)
                || headerValues.Count != 1
                || string.IsNullOrWhiteSpace(headerValues[0])
            )
                throw CreateUnauthorizedException();

            var headerValue = headerValues[0]!.Trim();

            return headerValue;
        }

        private static string GetBearerSessionId(HttpContext context)
        {
            var authorization = GetRequiredHeader(
                context,
                CommonConstantValues.AuthorizationHeader
            );

            if (!authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
                throw CreateUnauthorizedException();

            var sessionId = authorization[BearerPrefix.Length..].Trim();

            if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Any(char.IsWhiteSpace))
                throw CreateUnauthorizedException();

            return sessionId;
        }

        private static Exception CreateUnauthorizedException()
        {
            var exception = ExceptionConstructor.CreateHttp(
                "Unauthorized.",
                HttpStatusCode.Unauthorized
            );

            return exception;
        }
    }
}
