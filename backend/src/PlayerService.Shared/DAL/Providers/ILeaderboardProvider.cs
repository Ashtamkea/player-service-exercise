using PlayerService.Shared.Models.Leaderboards;

namespace PlayerService.Shared.DAL.Providers
{
    public interface ILeaderboardProvider
    {
        Task<bool> TryRefreshLeaderboardAsync(
            CancellationToken cancellationToken = default
        );

        Task<IReadOnlyList<LeaderboardEntry>?> GetLeaderboardAsync(
            CancellationToken cancellationToken = default
        );
    }
}
