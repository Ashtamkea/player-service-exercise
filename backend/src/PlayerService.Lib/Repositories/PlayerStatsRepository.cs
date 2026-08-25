using PlayerService.Shared.DAL.Providers;
using PlayerService.Shared.Models.PlayerStats;
using PlayerService.Shared.Repositories;

namespace PlayerService.Lib.Repositories
{
    public class PlayerStatsRepository : IPlayerStatsRepository
    {
        private readonly IPlayerStatsProvider _playerStatsProvider;

        public PlayerStatsRepository(IPlayerStatsProvider playerStatsProvider)
        {
            _playerStatsProvider = playerStatsProvider;
        }

        public Task<ScoreUpdateResult> AddScoreAsync(
            string playerId,
            int points,
            Guid requestId,
            CancellationToken cancellationToken = default
        )
        {
            return _playerStatsProvider.AddScoreAsync(
                playerId,
                points,
                requestId,
                cancellationToken
            );
        }

        public Task<PlayerStats?> GetPlayerStatsAsync(
            string playerId,
            CancellationToken cancellationToken = default
        )
        {
            return _playerStatsProvider.GetPlayerStatsAsync(
                playerId,
                cancellationToken
            );
        }
    }
}
