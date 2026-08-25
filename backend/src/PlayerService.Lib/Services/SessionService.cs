using System.Net;
using System.Security.Cryptography;
using Exercise.Infra.Exceptions;
using Exercise.Infra.Logging;
using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Models.Sessions.Enums;
using PlayerService.Shared.Repositories;
using PlayerService.Shared.Services;

namespace PlayerService.Lib.Services
{
    public class SessionService : ISessionService
    {
        private readonly IExerciseLogger _logger;
        private readonly ISessionRepository _sessionRepository;

        public SessionService(
            ISessionRepository sessionRepository,
            IExerciseLogger logger
        )
        {
            _sessionRepository = sessionRepository;
            _logger = logger;
        }

        public async Task<string> CreateSessionAsync(
            string playerId,
            string deviceId,
            CancellationToken cancellationToken = default
        )
        {
            ValidateIdentifier(playerId, nameof(playerId));
            ValidateDeviceId(deviceId);

            var session = new Session
            {
                SessionId = CreateSessionId(),
                PlayerId = playerId,
                DeviceId = deviceId
            };

            var status = await _sessionRepository.TryCreateSessionAsync(
                session,
                cancellationToken
            );

            if (status == SessionCreationStatus.DeviceAlreadyActive)
            {
                #region Exception

                throw ExceptionConstructor.CreateHttp(
                    "Device already has an active session.",
                    HttpStatusCode.Conflict
                );

                #endregion
            }

            #region Log

            await _logger.LogInfoAsync(
                "Session created",
                new
                {
                    session.PlayerId,
                    session.DeviceId
                },
                cancellationToken
            );

            #endregion

            return session.SessionId;
        }

        public Task<SessionAuthenticationResult> AuthenticateAndExtendSessionAsync(
            string deviceId,
            string sessionId,
            CancellationToken cancellationToken = default
        )
        {
            ValidateSessionReference(deviceId, sessionId);

            return _sessionRepository.AuthenticateAndExtendSessionAsync(
                deviceId,
                sessionId,
                cancellationToken
            );
        }

        private static string CreateSessionId()
        {
            var sessionIdBytes = RandomNumberGenerator.GetBytes(32);
            var sessionId = Convert.ToHexString(sessionIdBytes);

            return sessionId;
        }

        private static void ValidateSessionReference(string deviceId, string sessionId)
        {
            ValidateDeviceId(deviceId);
            ValidateIdentifier(sessionId, nameof(sessionId));
        }

        private static void ValidateDeviceId(string deviceId)
        {
            ValidateIdentifier(deviceId, nameof(deviceId));

            if (deviceId.Contains('{') || deviceId.Contains('}'))
            {
                #region Exception

                throw ExceptionConstructor.CreateParameterizedHttp(
                    "Device ID cannot contain '{' or '}'.",
                    HttpStatusCode.BadRequest,
                    new
                    {
                        ParameterName = nameof(deviceId)
                    }
                );

                #endregion
            }
        }

        private static void ValidateIdentifier(string value, string parameterName)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return;

            #region Exception

            throw ExceptionConstructor.CreateParameterizedHttp(
                "A required session identifier is missing.",
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
