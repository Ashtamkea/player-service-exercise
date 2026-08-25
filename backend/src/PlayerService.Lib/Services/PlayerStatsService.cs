using System.Net;
using Exercise.Infra.Exceptions;
using Exercise.Infra.Logging;
using PlayerService.Shared.Models.PlayerStats;
using PlayerService.Shared.Repositories;
using PlayerService.Shared.Services;

namespace PlayerService.Lib.Services
{
    public class PlayerStatsService : IPlayerStatsService
    {
        private readonly IExerciseLogger _logger;
        private readonly IPlayerStatsRepository _playerStatsRepository;

        public PlayerStatsService(
            IPlayerStatsRepository playerStatsRepository,
            IExerciseLogger logger
        )
        {
            _playerStatsRepository = playerStatsRepository;
            _logger = logger;
        }

        public async Task<PlayerStats> GetPlayerStatsAsync(
            string playerId,
            CancellationToken cancellationToken = default
        )
        {
            ValidatePlayerId(playerId, nameof(playerId));

            var playerStats = await _playerStatsRepository.GetPlayerStatsAsync(
                playerId,
                cancellationToken
            );

            if (playerStats is null)
            {
                #region Exception

                throw ExceptionConstructor.CreateParameterizedHttp(
                    "Player stats could not be found.",
                    HttpStatusCode.NotFound,
                    new
                    {
                        PlayerId = playerId
                    }
                );

                #endregion
            }

            return playerStats;
        }

        public async Task<PlayerStats> AddScoreAsync(
            string playerId,
            string authenticatedPlayerId,
            AddScoreRequest request,
            CancellationToken cancellationToken = default
        )
        {
            ValidatePlayerId(playerId, nameof(playerId));
            ValidatePlayerId(authenticatedPlayerId, nameof(authenticatedPlayerId));

            if (!string.Equals(playerId, authenticatedPlayerId, StringComparison.Ordinal))
            {
                #region Exception

                throw ExceptionConstructor.CreateHttp(
                    "A player cannot modify another player's score.",
                    HttpStatusCode.Forbidden
                );

                #endregion
            }

            if (request.Points <= 0)
            {
                #region Exception

                throw ExceptionConstructor.CreateParameterizedHttp(
                    "Points must be greater than zero.",
                    HttpStatusCode.BadRequest,
                    new
                    {
                        request.Points
                    }
                );

                #endregion
            }

            if (request.RequestId == Guid.Empty)
            {
                #region Exception

                throw ExceptionConstructor.CreateHttp(
                    "Request ID must not be empty.",
                    HttpStatusCode.BadRequest
                );

                #endregion
            }

            var result = await _playerStatsRepository.AddScoreAsync(
                playerId,
                request.Points,
                request.RequestId,
                cancellationToken
            );

            if (result.Applied)
            {
                #region Log

                await _logger.LogDebugAsync(
                    "Player score updated",
                    new
                    {
                        PlayerId = playerId,
                        request.Points,
                        request.RequestId,
                        result.Stats.Score
                    },
                    cancellationToken
                );

                #endregion
            }
            else
            {
                #region Log

                await _logger.LogDebugAsync(
                    "Score request duplicate returned",
                    new
                    {
                        PlayerId = playerId,
                        request.RequestId,
                        result.Stats.Score
                    },
                    cancellationToken
                );

                #endregion
            }

            return result.Stats;
        }

        private static void ValidatePlayerId(string playerId, string parameterName)
        {
            if (!string.IsNullOrWhiteSpace(playerId))
                return;

            #region Exception

            throw ExceptionConstructor.CreateParameterizedHttp(
                "A required player identifier is missing.",
                HttpStatusCode.BadRequest,
                new
                {
                    ParameterName = parameterName
                }
            );

            #endregion
        }
    }
}
