using PlayerService.Shared.DAL.Providers;
using PlayerService.Shared.Models.Leaderboards;
using PlayerService.Shared.Repositories;

namespace PlayerService.Lib.Repositories
{
    public class LeaderboardRepository : ILeaderboardRepository
    {
        private readonly ILeaderboardProvider _leaderboardProvider;

        public LeaderboardRepository(ILeaderboardProvider leaderboardProvider)
        {
            _leaderboardProvider = leaderboardProvider;
        }

        public Task<bool> TryRefreshLeaderboardAsync(
            CancellationToken cancellationToken = default
        )
        {
            return _leaderboardProvider.TryRefreshLeaderboardAsync(
                cancellationToken
            );
        }

        public Task<IReadOnlyList<LeaderboardEntry>?> GetLeaderboardAsync(
            CancellationToken cancellationToken = default
        )
        {
            return _leaderboardProvider.GetLeaderboardAsync(cancellationToken);
        }
    }
}
