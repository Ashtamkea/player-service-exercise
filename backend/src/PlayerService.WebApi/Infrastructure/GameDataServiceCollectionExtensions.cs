using PlayerService.Lib.DAL.Providers.Memory;
using PlayerService.Lib.DAL.Sources;
using PlayerService.Shared.DAL.Providers;

namespace PlayerService.WebApi.Infrastructure
{
    public static class GameDataServiceCollectionExtensions
    {
        public static IServiceCollection AddMemoryGameData(
            this IServiceCollection services
        )
        {
            services.AddSingleton<MemoryGameDataSource>();
            services.AddSingleton<ISessionProvider, SessionMemoryProvider>();
            services.AddSingleton<IPlayerStatsProvider, PlayerStatsMemoryProvider>();
            services.AddSingleton<IPlayerGiftProvider, PlayerGiftMemoryProvider>();
            services.AddSingleton<ILeaderboardProvider, LeaderboardMemoryProvider>();

            return services;
        }
    }
}
