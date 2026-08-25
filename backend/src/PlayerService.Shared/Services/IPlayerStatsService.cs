using PlayerService.Shared.Models.PlayerStats;

namespace PlayerService.Shared.Services
{
    public interface IPlayerStatsService
    {
        Task<PlayerStats> GetPlayerStatsAsync(
            string playerId,
            CancellationToken cancellationToken = default
        );

        Task<PlayerStats> AddScoreAsync(
            string playerId,
            string authenticatedPlayerId,
            AddScoreRequest request,
            CancellationToken cancellationToken = default
        );
    }
}
