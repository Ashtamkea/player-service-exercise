using PlayerService.Shared.Models.PlayerStats;

namespace PlayerService.Shared.Repositories
{
    public interface IPlayerStatsRepository
    {
        Task<ScoreUpdateResult> AddScoreAsync(
            string playerId,
            int points,
            Guid requestId,
            CancellationToken cancellationToken = default
        );

        Task<PlayerStats?> GetPlayerStatsAsync(
            string playerId,
            CancellationToken cancellationToken = default
        );
    }
}
