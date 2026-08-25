using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using PlayerService.Shared.Configuration;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PlayerService.WebApi.Infrastructure
{
    public class SessionAuthenticationOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var allowsAnonymous = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<IAllowAnonymous>()
            .Any();

            if (allowsAnonymous)
                return;

            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [CreateSchemeReference(ConstantValues.SessionBearerSecurityScheme)] = [],
                    [CreateSchemeReference(ConstantValues.SessionDeviceSecurityScheme)] = []
                }
            ];
        }

        private static OpenApiSecurityScheme CreateSchemeReference(string schemeId)
        {
            var scheme = new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = schemeId,
                    Type = ReferenceType.SecurityScheme
                }
            };

            return scheme;
        }
    }
}
