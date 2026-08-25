using Microsoft.OpenApi.Models;
using PlayerService.Shared.Configuration;

namespace PlayerService.WebApi.Infrastructure
{
    public static class SessionAuthenticationSwaggerExtensions
    {
        public static IServiceCollection AddSessionAuthenticationSwagger(
            this IServiceCollection services
        )
        {
            services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition(
                    ConstantValues.SessionBearerSecurityScheme,
                    new OpenApiSecurityScheme
                    {
                        Description = "Session ID returned by POST /login.",
                        In = ParameterLocation.Header,
                        Name = "Authorization",
                        Scheme = "bearer",
                        Type = SecuritySchemeType.Http
                    }
                );

                options.AddSecurityDefinition(
                    ConstantValues.SessionDeviceSecurityScheme,
                    new OpenApiSecurityScheme
                    {
                        Description = "Device ID associated with the active session.",
                        In = ParameterLocation.Header,
                        Name = ConstantValues.DeviceIdHeader,
                        Type = SecuritySchemeType.ApiKey
                    }
                );

                options.OperationFilter<SessionAuthenticationOperationFilter>();
            });

            return services;
        }
    }
}
