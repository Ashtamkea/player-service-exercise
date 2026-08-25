using PlayerService.Shared.Models.Leaderboards;

namespace PlayerService.Shared.Services
{
    public interface ILeaderboardService
    {
        Task<bool> TryRefreshAsync(
            CancellationToken cancellationToken = default
        );

        Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
            CancellationToken cancellationToken = default
        );
    }
}
