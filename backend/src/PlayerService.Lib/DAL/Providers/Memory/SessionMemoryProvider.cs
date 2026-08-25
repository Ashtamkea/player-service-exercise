using Exercise.Infra.Configuration;
using Exercise.Infra.Exceptions;
using Microsoft.Extensions.Configuration;
using PlayerService.Lib.DAL.Models.Memory;
using PlayerService.Lib.DAL.Sources;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.DAL.Providers;
using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Models.Sessions.Enums;

namespace PlayerService.Lib.DAL.Providers.Memory
{
    public class SessionMemoryProvider : ISessionProvider
    {
        private readonly MemoryGameDataSource _gameDataSource;
        private readonly TimeSpan _sessionTtl;

        public SessionMemoryProvider(
            MemoryGameDataSource gameDataSource,
            IConfiguration configuration
        )
        {
            _gameDataSource = gameDataSource;
            _sessionTtl = TimeSpan.FromSeconds(
                configuration.GetSectionValue<int>(
                    ConfigurationKeys.PlayerServiceSection,
                    ConfigurationKeys.SessionTtlInSeconds
                )
            );

            if (_sessionTtl <= TimeSpan.Zero)
            {
                #region Exception

                throw ExceptionConstructor.CreateParameterized(
                    "Memory session TTL must be greater than zero.",
                    new
                    {
                        SessionTtl = _sessionTtl
                    }
                );

                #endregion
            }
        }

        public async Task<SessionCreationStatus> TryCreateSessionAsync(
            Session session,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deviceLock = GetSessionLock(session.DeviceId);
            await deviceLock.WaitAsync(cancellationToken);

            try
            {
                var now = DateTime.UtcNow;
                _gameDataSource.SessionsByDeviceId.TryGetValue(
                    session.DeviceId,
                    out var existingSession
                );

                if (
                    existingSession is not null
                    && !IsSessionExpired(existingSession, now)
                )
                    return SessionCreationStatus.DeviceAlreadyActive;

                var player = _gameDataSource.GetOrCreatePlayer(
                    session.PlayerId,
                    now
                );
                MemoryPlayer? existingPlayer = null;

                if (existingSession is not null)
                {
                    _gameDataSource.PlayersById.TryGetValue(
                        existingSession.PlayerId,
                        out existingPlayer
                    );
                }

                var lockedPlayers = await _gameDataSource.AcquirePlayerLocksAsync(
                    player,
                    existingPlayer,
                    cancellationToken
                );

                try
                {
                    if (existingSession is not null)
                        RemoveSessionFromIndexes(
                            existingSession,
                            existingPlayer
                        );

                    var memorySession = new MemorySession
                    {
                        SessionId = session.SessionId,
                        PlayerId = session.PlayerId,
                        DeviceId = session.DeviceId,
                        LastActiveUtc = now
                    };

                    _gameDataSource.SessionsByDeviceId[session.DeviceId] = memorySession;
                    player.ActiveSessionsByDeviceId[session.DeviceId] = memorySession;
                    player.RecordActivity(now);
                }
                finally
                {
                    _gameDataSource.ReleasePlayerLocks(lockedPlayers);
                }

                return SessionCreationStatus.Created;
            }
            finally
            {
                deviceLock.Release();
            }
        }

        public Task<bool> IsPlayerOnlineAsync(
            string playerId,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isOnline = false;

            if (_gameDataSource.PlayersById.TryGetValue(playerId, out var player))
            {
                var now = DateTime.UtcNow;
                isOnline = player.ActiveSessionsByDeviceId.Values.Any(
                    session => !IsSessionExpired(session, now)
                );
            }

            return Task.FromResult(isOnline);
        }

        public Task<List<string>> GetExpiredSessionDeviceIdsAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = DateTime.UtcNow;
            var deviceIds = _gameDataSource.SessionsByDeviceId
            .Where(entry => IsSessionExpired(entry.Value, now))
            .Select(entry => entry.Key)
            .ToList();
            return Task.FromResult(deviceIds);
        }

        public async Task<int> DeleteExpiredSessionsAsync(
            List<string> deviceIds,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deletedCount = 0;
            var distinctDeviceIds = deviceIds
            .Distinct(StringComparer.Ordinal)
            .ToList();

            foreach (var deviceId in distinctDeviceIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var removed = await RemoveSessionIfExpiredAsync(
                    deviceId,
                    cancellationToken
                );

                if (removed)
                    deletedCount++;
            }

            return deletedCount;
        }

        public async Task<SessionAuthenticationResult> AuthenticateAndExtendSessionAsync(
            string deviceId,
            string sessionId,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deviceLock = GetSessionLock(deviceId);
            await deviceLock.WaitAsync(cancellationToken);

            try
            {
                if (!
                    _gameDataSource.SessionsByDeviceId.TryGetValue(
                        deviceId,
                        out var memorySession
                    )
                )
                    return CreateAuthenticationResult(
                        SessionAuthenticationStatus.NotFound
                    );

                var now = DateTime.UtcNow;

                if (IsSessionExpired(memorySession, now))
                {
                    await RemoveSessionAsync(memorySession, cancellationToken);

                    return CreateAuthenticationResult(
                        SessionAuthenticationStatus.NotFound
                    );
                }

                if (!string.Equals(
                    memorySession.SessionId,
                    sessionId,
                    StringComparison.Ordinal
                ))
                    return CreateAuthenticationResult(
                        SessionAuthenticationStatus.SessionIdMismatch
                    );

                memorySession.LastActiveUtc = now;

                if (
                    _gameDataSource.PlayersById.TryGetValue(
                        memorySession.PlayerId,
                        out var player
                    )
                )
                    player.RecordActivity(now);

                var context = new SessionAuthenticationContext
                {
                    SessionId = memorySession.SessionId,
                    PlayerId = memorySession.PlayerId,
                    DeviceId = memorySession.DeviceId
                };

                var result = CreateAuthenticationResult(
                    SessionAuthenticationStatus.Succeeded,
                    context
                );

                return result;
            }
            finally
            {
                deviceLock.Release();
            }
        }

        private async Task<bool> RemoveSessionIfExpiredAsync(
            string deviceId,
            CancellationToken cancellationToken
        )
        {
            var deviceLock = GetSessionLock(deviceId);
            await deviceLock.WaitAsync(cancellationToken);

            try
            {
                if (!
                    _gameDataSource.SessionsByDeviceId.TryGetValue(
                        deviceId,
                        out var memorySession
                    )
                )
                    return false;

                if (!IsSessionExpired(memorySession, DateTime.UtcNow))
                    return false;

                await RemoveSessionAsync(memorySession, cancellationToken);

                return true;
            }
            finally
            {
                deviceLock.Release();
            }
        }

        private async Task RemoveSessionAsync(
            MemorySession memorySession,
            CancellationToken cancellationToken
        )
        {
            if (!
                _gameDataSource.PlayersById.TryGetValue(
                    memorySession.PlayerId,
                    out var player
                )
            )
            {
                _gameDataSource.SessionsByDeviceId.TryRemove(
                    memorySession.DeviceId,
                    out _
                );

                return;
            }

            await player.PlayerLock.WaitAsync(cancellationToken);

            try
            {
                RemoveSessionFromIndexes(memorySession, player);
            }
            finally
            {
                player.PlayerLock.Release();
            }
        }

        private void RemoveSessionFromIndexes(
            MemorySession memorySession,
            MemoryPlayer? player
        )
        {
            _gameDataSource.SessionsByDeviceId.TryRemove(
                memorySession.DeviceId,
                out _
            );

            if (player is null)
                return;

            if (
                player.ActiveSessionsByDeviceId.TryGetValue(
                    memorySession.DeviceId,
                    out var indexedSession
                )
                && ReferenceEquals(indexedSession, memorySession)
            )
                player.ActiveSessionsByDeviceId.TryRemove(
                    memorySession.DeviceId,
                    out _
                );
        }

        private bool IsSessionExpired(MemorySession session, DateTime now)
        {
            return now - session.LastActiveUtc >= _sessionTtl;
        }

        private SemaphoreSlim GetSessionLock(string deviceId)
        {
            var sessionLock = _gameDataSource.GetSessionLock(deviceId);

            return sessionLock;
        }

        private static SessionAuthenticationResult CreateAuthenticationResult(
            SessionAuthenticationStatus status,
            SessionAuthenticationContext? context = null
        )
        {
            var result = new SessionAuthenticationResult
            {
                Status = status,
                Context = context
            };

            return result;
        }
    }
}
