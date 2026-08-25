using PlayerService.Shared.Models.Leaderboards;

namespace PlayerService.Shared.Repositories
{
    public interface ILeaderboardRepository
    {
        Task<bool> TryRefreshLeaderboardAsync(
            CancellationToken cancellationToken = default
        );

        Task<IReadOnlyList<LeaderboardEntry>?> GetLeaderboardAsync(
            CancellationToken cancellationToken = default
        );
    }
}
