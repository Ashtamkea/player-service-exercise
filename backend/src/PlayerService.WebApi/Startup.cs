using Exercise.Infra.WebApi;
using PlayerService.Lib.Repositories;
using PlayerService.Lib.Services;
using PlayerService.Shared.DAL.Providers;
using PlayerService.Shared.Repositories;
using PlayerService.Shared.Services;
using PlayerService.WebApi.Infrastructure;

namespace PlayerService.WebApi
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddBasicServices();
            services.CreateAddSwaggerGen(_configuration);
            services.AddSessionAuthenticationSwagger();
            services.AddMemoryGameData();
            services.AddSingleton<ISessionRepository, SessionRepository>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<IPlayerStatsRepository, PlayerStatsRepository>();
            services.AddSingleton<IPlayerStatsService, PlayerStatsService>();
            services.AddSingleton<IPlayerGiftRepository, PlayerGiftRepository>();
            services.AddSingleton<IPlayerGiftService, PlayerGiftService>();
            services.AddSingleton<ILeaderboardRepository, LeaderboardRepository>();
            services.AddSingleton<ILeaderboardService, LeaderboardService>();
            services.AddHostedService<SessionCleanupHostedService>();
            services.AddHostedService<ScoreRequestCleanupHostedService>();
            services.AddHostedService<GiftRequestCleanupHostedService>();
            services.AddHostedService<LeaderboardRefreshHostedService>();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.HeadContent = """
                    <style>
                    @media only screen and (prefers-color-scheme: dark) {
                      body, .swagger-ui { background: #111827; color: #e5e7eb; }
                      .swagger-ui .info .title, .swagger-ui .opblock-tag, .swagger-ui .tab li, .swagger-ui p { color: #e5e7eb; }
                      .swagger-ui .scheme-container, .swagger-ui .opblock, .swagger-ui section.models { background: #1f2937; }
                    }
                    </style>
                    """;
            });

            app.UseMiddleware<HttpExceptionHandler>();
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseMiddleware<HttpCorrelationSetter>();
            app.UseMiddleware<SessionAuthenticationMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
