using System.Net;
using Exercise.Infra.Exceptions;
using Exercise.Infra.Logging;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.PlayerGifts;
using PlayerService.Shared.Models.PlayerGifts.Enums;
using PlayerService.Shared.Repositories;
using PlayerService.Shared.Services;

namespace PlayerService.Lib.Services
{
    public class PlayerGiftService : IPlayerGiftService
    {
        private readonly IPlayerGiftRepository _playerGiftRepository;
        private readonly IExerciseLogger _logger;

        public PlayerGiftService(
            IPlayerGiftRepository playerGiftRepository,
            IExerciseLogger logger
        )
        {
            _playerGiftRepository = playerGiftRepository;
            _logger = logger;
        }

        public async Task<GiftExecutionResult> GiftAsync(
            string senderPlayerId,
            string authenticatedPlayerId,
            GiftRequest request,
            CancellationToken cancellationToken = default
        )
        {
            ValidateSender(senderPlayerId, authenticatedPlayerId);
            ValidateGiftRequest(senderPlayerId, request);

            var operation = new GiftOperation
            {
                SenderPlayerId = senderPlayerId,
                RecipientPlayerId = request.RecipientPlayerId,
                Points = request.Points,
                RequestId = request.RequestId
            };
            var operationResult = await _playerGiftRepository.ExecuteGiftAsync(
                operation,
                cancellationToken
            );
            var executionResult = CreateExecutionResult(operationResult);

            await LogGiftOutcomeAsync(
                operation,
                operationResult,
                cancellationToken
            );

            return executionResult;
        }

        private Task LogGiftOutcomeAsync(
            GiftOperation operation,
            GiftOperationResult operationResult,
            CancellationToken cancellationToken
        )
        {
            var parameters = new
            {
                operation.SenderPlayerId,
                operation.RecipientPlayerId,
                operation.RequestId,
                operationResult.Status,
                operationResult.SenderScore,
                operationResult.RecipientScore,
                RetryAfterMilliseconds = operationResult.RetryAfter?.TotalMilliseconds
            };

            if (operationResult.Status != GiftOperationStatus.Succeeded)
            {
                #region Log

                return _logger.LogWarningAsync(
                    "Gift request rejected",
                    parameters,
                    cancellationToken
                );

                #endregion
            }

            if (operationResult.Applied)
            {
                #region Log

                return _logger.LogDebugAsync(
                    "Gift completed",
                    parameters,
                    cancellationToken
                );

                #endregion
            }

            #region Log

            return _logger.LogDebugAsync(
                "Gift duplicate returned",
                parameters,
                cancellationToken
            );

            #endregion
        }

        private static GiftExecutionResult CreateExecutionResult(
            GiftOperationResult operationResult
        )
        {
            var (statusCode, outcome) = operationResult.Status switch
            {
                GiftOperationStatus.Succeeded => (
                    HttpStatusCode.OK,
                    ConstantValues.GiftSucceededOutcome
                ),
                GiftOperationStatus.RecipientOffline => (
                    HttpStatusCode.Conflict,
                    ConstantValues.GiftRecipientOfflineOutcome
                ),
                GiftOperationStatus.InsufficientPoints => (
                    HttpStatusCode.Conflict,
                    ConstantValues.GiftInsufficientPointsOutcome
                ),
                GiftOperationStatus.RecipientScoreLimitExceeded => (
                    HttpStatusCode.Conflict,
                    ConstantValues.GiftRecipientScoreLimitExceededOutcome
                ),
                GiftOperationStatus.RateLimited => (
                    HttpStatusCode.TooManyRequests,
                    "rateLimited"
                ),
                _ => (
                    HttpStatusCode.ServiceUnavailable,
                    "temporarilyUnavailable"
                )
            };
            var response = new GiftResponse
            {
                RequestId = operationResult.RequestId,
                Outcome = outcome,
                SenderScore = operationResult.SenderScore,
                RecipientScore = operationResult.RecipientScore
            };
            var result = new GiftExecutionResult
            {
                StatusCode = (int)statusCode,
                Response = response
            };

            return result;
        }

        private static void ValidateSender(
            string senderPlayerId,
            string authenticatedPlayerId
        )
        {
            ValidatePlayerId(senderPlayerId, nameof(senderPlayerId));
            ValidatePlayerId(authenticatedPlayerId, nameof(authenticatedPlayerId));

            if (string.Equals(
                senderPlayerId,
                authenticatedPlayerId,
                StringComparison.Ordinal
            ))
                return;

            #region Exception

            throw ExceptionConstructor.CreateHttp(
                "A player cannot gift points from another player's balance.",
                HttpStatusCode.Forbidden
            );

            #endregion
        }

        private static void ValidateGiftRequest(
            string senderPlayerId,
            GiftRequest request
        )
        {
            ValidatePlayerId(request.RecipientPlayerId, nameof(request.RecipientPlayerId));

            if (request.RequestId == Guid.Empty)
            {
                #region Exception

                throw ExceptionConstructor.CreateHttp(
                    "Request ID must not be empty.",
                    HttpStatusCode.BadRequest
                );

                #endregion
            }

            if (request.Points <= 0)
            {
                #region Exception

                throw ExceptionConstructor.CreateParameterizedHttp(
                    "Gift points must be greater than zero.",
                    HttpStatusCode.BadRequest,
                    new
                    {
                        request.Points
                    }
                );

                #endregion
            }

            if (!string.Equals(
                senderPlayerId,
                request.RecipientPlayerId,
                StringComparison.Ordinal
            ))
                return;

            #region Exception

            throw ExceptionConstructor.CreateHttp(
                "A player cannot gift points to themselves.",
                HttpStatusCode.BadRequest
            );

            #endregion
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
