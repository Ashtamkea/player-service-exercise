using PlayerService.Shared.Models.PlayerStats;

namespace PlayerService.Shared.DAL.Providers
{
    public interface IPlayerStatsProvider
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

        Task<List<ScoreRequestCleanupCandidate>> GetExpiredScoreRequestCandidatesAsync(
            CancellationToken cancellationToken = default
        );

        Task<int> DeleteExpiredScoreRequestsAsync(
            List<ScoreRequestCleanupCandidate> candidates,
            CancellationToken cancellationToken = default
        );
    }
}
