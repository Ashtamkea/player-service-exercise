using Exercise.Infra.Common;
using Exercise.Infra.Configuration;
using Exercise.Infra.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Exercise.Infra.WebApi
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBasicServices(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSingleton<RequestCorrelationProvider>();
            services.AddSingleton<ICorrelationCreator>(provider => provider.GetRequiredService<RequestCorrelationProvider>());
            services.AddSingleton<ICorrelationProvider>(provider => provider.GetRequiredService<RequestCorrelationProvider>());
            services.AddSingleton<IExerciseLogger, ExerciseLogger>();
            services.AddSingleton<IExerciseLoggerProvider, ExerciseConsoleLoggerProvider>();

            return services;
        }

        public static IServiceCollection CreateAddSwaggerGen(
            this IServiceCollection services,
            IConfiguration configuration
        )
        {
            var serviceName = configuration.GetServiceName();

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc(
                    "v1",
                    new OpenApiInfo
                    {
                        Title = serviceName,
                        Version = "v1"
                    }
                );
            });

            return services;
        }
    }
}
