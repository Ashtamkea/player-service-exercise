using System.Net;
using System.Diagnostics;
using Exercise.Infra.Exceptions;
using Exercise.Infra.Logging;
using PlayerService.Shared.Models.Leaderboards;
using PlayerService.Shared.Repositories;
using PlayerService.Shared.Services;

namespace PlayerService.Lib.Services
{
    public class LeaderboardService : ILeaderboardService
    {
        private readonly ILeaderboardRepository _leaderboardRepository;
        private readonly IExerciseLogger _logger;

        public LeaderboardService(
            ILeaderboardRepository leaderboardRepository,
            IExerciseLogger logger
        )
        {
            _leaderboardRepository = leaderboardRepository;
            _logger = logger;
        }

        public async Task<bool> TryRefreshAsync(
            CancellationToken cancellationToken = default
        )
        {
            var stopwatch = Stopwatch.StartNew();
            var refreshed = await _leaderboardRepository.TryRefreshLeaderboardAsync(
                cancellationToken
            );

            if (refreshed)
            {
                var leaderboard = await _leaderboardRepository.GetLeaderboardAsync(
                    cancellationToken
                );

                #region Log

                await _logger.LogInfoAsync(
                    "Leaderboard snapshot refreshed",
                    new
                    {
                        EntriesCount = leaderboard?.Count ?? 0,
                        DurationMilliseconds = stopwatch.Elapsed.TotalMilliseconds
                    },
                    cancellationToken
                );

                #endregion
            }
            else
            {
                #region Log

                await _logger.LogDebugAsync(
                    "Leaderboard refresh skipped",
                    new
                    {
                        RefreshReason = "Another refresh is active"
                    },
                    cancellationToken
                );

                #endregion
            }

            return refreshed;
        }

        public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
            CancellationToken cancellationToken = default
        )
        {
            var leaderboard = await _leaderboardRepository.GetLeaderboardAsync(
                cancellationToken
            );

            if (leaderboard is not null)
                return leaderboard;

            #region Exception

            throw ExceptionConstructor.CreateHttp(
                "Leaderboard is not available.",
                HttpStatusCode.NotFound
            );

            #endregion
        }
    }
}
