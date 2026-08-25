using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using PlayerService.Lib.DAL.Models.Memory;
using PlayerService.Lib.DAL.Providers.Memory;
using PlayerService.Lib.DAL.Sources;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Models.Sessions.Enums;

namespace PlayerService.Tests.Sessions
{
    public class MemorySessionIntegrationTests : IAsyncLifetime
    {
        private readonly MemoryGameDataSource _gameDataSource;
        private readonly SessionMemoryProvider _provider;

        public MemorySessionIntegrationTests()
        {
            var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlayerService:sessionTtlInSeconds"] = "1",
                ["PlayerService:sessionCleanupIntervalInSeconds"] = "1"
            })
            .Build();

            _gameDataSource = new MemoryGameDataSource();
            _provider = new SessionMemoryProvider(
                _gameDataSource,
                configuration
            );
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            _gameDataSource.Dispose();

            return Task.CompletedTask;
        }

        [Fact]
        public async Task FirstLoginInitializesScoreAndPlayerActivity()
        {
            var playerId = CreateIdentifier("player");
            var before = DateTime.UtcNow;

            var status = await _provider.TryCreateSessionAsync(
                CreateSession(playerId, CreateIdentifier("device"))
            );
            var after = DateTime.UtcNow;
            var player = GetPlayersById()[playerId];

            Assert.Equal(SessionCreationStatus.Created, status);
            Assert.Equal(
                ConstantValues.InitialPlayerScore,
                GetScoresByPlayerId()[playerId]
            );
            Assert.InRange(player.LastActiveUtc, before, after);
        }

        [Fact]
        public async Task AdditionalDeviceDoesNotResetExistingScore()
        {
            var playerId = CreateIdentifier("player");
            await _provider.TryCreateSessionAsync(
                CreateSession(playerId, CreateIdentifier("device-1"))
            );
            GetScoresByPlayerId()[playerId] = 1750;

            var status = await _provider.TryCreateSessionAsync(
                CreateSession(playerId, CreateIdentifier("device-2"))
            );

            Assert.Equal(SessionCreationStatus.Created, status);
            Assert.Equal(1750, GetScoresByPlayerId()[playerId]);
        }

        [Fact]
        public async Task ConcurrentFirstLoginsInitializeScoreExactlyOnce()
        {
            var playerId = CreateIdentifier("player");

            var statuses = await Task.WhenAll(
                _provider.TryCreateSessionAsync(
                    CreateSession(playerId, CreateIdentifier("device-1"))
                ),
                _provider.TryCreateSessionAsync(
                    CreateSession(playerId, CreateIdentifier("device-2"))
                )
            );

            Assert.All(
                statuses,
                status => Assert.Equal(SessionCreationStatus.Created, status)
            );
            Assert.Equal(
                ConstantValues.InitialPlayerScore,
                GetScoresByPlayerId()[playerId]
            );
            Assert.Single(
                GetScoresByPlayerId(),
                entry => entry.Key == playerId
            );
        }

        [Fact]
        public async Task ActiveDeviceRejectsEveryLoginWithoutChangingLastActivity()
        {
            var deviceId = CreateIdentifier("device");
            var firstSession = CreateSession("player-1", deviceId);
            var firstStatus = await _provider.TryCreateSessionAsync(firstSession);
            var memorySession = GetSessionsByDevice()[deviceId];
            var lastActiveBefore = memorySession.LastActiveUtc;

            await Task.Delay(100);

            var samePlayerStatus = await _provider.TryCreateSessionAsync(
                CreateSession(firstSession.PlayerId, deviceId)
            );
            var otherPlayerStatus = await _provider.TryCreateSessionAsync(
                CreateSession("player-2", deviceId)
            );

            Assert.Equal(SessionCreationStatus.Created, firstStatus);
            Assert.Equal(
                SessionCreationStatus.DeviceAlreadyActive,
                samePlayerStatus
            );
            Assert.Equal(
                SessionCreationStatus.DeviceAlreadyActive,
                otherPlayerStatus
            );
            Assert.Equal(lastActiveBefore, memorySession.LastActiveUtc);
            Assert.Equal(firstSession.SessionId, memorySession.SessionId);
            Assert.False(GetPlayersById().ContainsKey("player-2"));
            Assert.False(GetScoresByPlayerId().ContainsKey("player-2"));
        }

        [Fact]
        public async Task ConcurrentLoginsForOneDeviceProduceOneSession()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            var firstSession = CreateSession(playerId, deviceId);
            var secondSession = CreateSession(playerId, deviceId);

            var statuses = await Task.WhenAll(
                _provider.TryCreateSessionAsync(firstSession),
                _provider.TryCreateSessionAsync(secondSession)
            );
            var storedSession = GetSessionsByDevice()[deviceId];

            Assert.Single(
                statuses,
                status => status == SessionCreationStatus.Created
            );
            Assert.Single(
                statuses,
                status => status == SessionCreationStatus.DeviceAlreadyActive
            );
            Assert.Contains(
                storedSession.SessionId,
                new[]
                {
                    firstSession.SessionId,
                    secondSession.SessionId
                }
            );
        }

        [Fact]
        public async Task OnlySuccessfulAuthenticationUpdatesLastActivity()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            var session = CreateSession(playerId, deviceId);
            await _provider.TryCreateSessionAsync(session);
            var memorySession = GetSessionsByDevice()[deviceId];
            var player = GetPlayersById()[playerId];

            await Task.Delay(200);

            var lastActiveBeforeMismatch = memorySession.LastActiveUtc;
            var playerLastActiveBeforeMismatch = player.LastActiveUtc;
            var mismatchResult = await _provider.AuthenticateAndExtendSessionAsync(
                deviceId,
                CreateIdentifier("wrong-session")
            );
            var lastActiveAfterMismatch = memorySession.LastActiveUtc;
            var playerLastActiveAfterMismatch = player.LastActiveUtc;
            var successResult = await _provider.AuthenticateAndExtendSessionAsync(
                deviceId,
                session.SessionId
            );

            Assert.Equal(
                SessionAuthenticationStatus.SessionIdMismatch,
                mismatchResult.Status
            );
            Assert.Null(mismatchResult.Context);
            Assert.Equal(lastActiveBeforeMismatch, lastActiveAfterMismatch);
            Assert.Equal(
                playerLastActiveBeforeMismatch,
                playerLastActiveAfterMismatch
            );
            Assert.Equal(
                SessionAuthenticationStatus.Succeeded,
                successResult.Status
            );
            Assert.NotNull(successResult.Context);
            Assert.Equal(playerId, successResult.Context.PlayerId);
            Assert.True(memorySession.LastActiveUtc > lastActiveAfterMismatch);
            Assert.True(player.LastActiveUtc > playerLastActiveAfterMismatch);
        }

        [Fact]
        public async Task LoginAfterExpiryTransfersDeviceToNewPlayer()
        {
            var firstPlayerId = CreateIdentifier("player-1");
            var secondPlayerId = CreateIdentifier("player-2");
            var deviceId = CreateIdentifier("device");
            var firstSession = CreateSession(firstPlayerId, deviceId);
            await _provider.TryCreateSessionAsync(firstSession);

            await Task.Delay(1100);

            var secondSession = CreateSession(secondPlayerId, deviceId);
            var status = await _provider.TryCreateSessionAsync(secondSession);
            var sessionsByDevice = GetSessionsByDevice();
            var playersById = GetPlayersById();
            var currentSession = sessionsByDevice[deviceId];
            var firstPlayerOnline = await _provider.IsPlayerOnlineAsync(
                firstPlayerId
            );
            var secondPlayerOnline = await _provider.IsPlayerOnlineAsync(
                secondPlayerId
            );

            Assert.Equal(SessionCreationStatus.Created, status);
            Assert.False(firstPlayerOnline);
            Assert.True(secondPlayerOnline);
            Assert.Equal(secondSession.SessionId, currentSession.SessionId);
            Assert.Empty(
                playersById[firstPlayerId].ActiveSessionsByDeviceId
            );
            Assert.Same(
                currentSession,
                playersById[secondPlayerId].ActiveSessionsByDeviceId[deviceId]
            );
        }

        [Fact]
        public async Task OnlineCheckUsesActiveSessionsWithoutExtendingThem()
        {
            var playerId = CreateIdentifier("player");
            var firstDeviceId = CreateIdentifier("device-1");
            var secondDeviceId = CreateIdentifier("device-2");
            await _provider.TryCreateSessionAsync(
                CreateSession(playerId, firstDeviceId)
            );
            await _provider.TryCreateSessionAsync(
                CreateSession(playerId, secondDeviceId)
            );
            var lastActiveBefore = GetSessionsByDevice()[firstDeviceId].LastActiveUtc;

            var initiallyOnline = await _provider.IsPlayerOnlineAsync(playerId);
            var lastActiveAfter = GetSessionsByDevice()[firstDeviceId].LastActiveUtc;

            await Task.Delay(1100);

            var onlineAfterExpiry = await _provider.IsPlayerOnlineAsync(playerId);

            Assert.True(initiallyOnline);
            Assert.Equal(lastActiveBefore, lastActiveAfter);
            Assert.False(onlineAfterExpiry);
        }

        [Fact]
        public async Task OnlineCheckDoesNotWaitForPlayerLock()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            await _provider.TryCreateSessionAsync(
                CreateSession(playerId, deviceId)
            );
            var player = GetPlayersById()[playerId];
            await player.PlayerLock.WaitAsync();

            try
            {
                var onlineTask = _provider.IsPlayerOnlineAsync(playerId);
                var isOnline = await onlineTask.WaitAsync(
                    TimeSpan.FromSeconds(1)
                );

                Assert.True(isOnline);
            }
            finally
            {
                player.PlayerLock.Release();
            }
        }

        [Fact]
        public async Task ExpiredSessionDiscoveryReturnsAllExpiredDevicesOnly()
        {
            var playerId = CreateIdentifier("player");
            var firstExpiredDeviceId = CreateIdentifier("expired-device-1");
            var secondExpiredDeviceId = CreateIdentifier("expired-device-2");
            var activeDeviceId = CreateIdentifier("active-device");
            await _provider.TryCreateSessionAsync(
                CreateSession(playerId, firstExpiredDeviceId)
            );
            await _provider.TryCreateSessionAsync(
                CreateSession(playerId, secondExpiredDeviceId)
            );

            await Task.Delay(1100);

            await _provider.TryCreateSessionAsync(
                CreateSession(playerId, activeDeviceId)
            );
            var deviceIds = await _provider.GetExpiredSessionDeviceIdsAsync();

            Assert.Equal(
                [firstExpiredDeviceId, secondExpiredDeviceId],
                deviceIds.Order(StringComparer.Ordinal).ToList()
            );
        }

        [Fact]
        public async Task BulkDeletionRemovesExpiredSessionsFromBothIndexes()
        {
            var playerId = CreateIdentifier("player");
            var firstDeviceId = CreateIdentifier("device-1");
            var secondDeviceId = CreateIdentifier("device-2");
            await _provider.TryCreateSessionAsync(
                CreateSession(playerId, firstDeviceId)
            );
            await _provider.TryCreateSessionAsync(
                CreateSession(playerId, secondDeviceId)
            );
            var playerLastActiveUtc = GetPlayersById()[playerId].LastActiveUtc;

            await Task.Delay(1100);

            var deviceIds = await _provider.GetExpiredSessionDeviceIdsAsync();
            var deletedCount = await _provider.DeleteExpiredSessionsAsync(
                deviceIds
            );

            Assert.Equal(2, deletedCount);
            Assert.DoesNotContain(firstDeviceId, GetSessionsByDevice().Keys);
            Assert.DoesNotContain(secondDeviceId, GetSessionsByDevice().Keys);
            Assert.Empty(GetPlayersById()[playerId].ActiveSessionsByDeviceId);
            Assert.Equal(
                playerLastActiveUtc,
                GetPlayersById()[playerId].LastActiveUtc
            );
        }

        [Fact]
        public async Task BulkDeletionIgnoresDuplicateMissingAndActiveCandidates()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            await _provider.TryCreateSessionAsync(
                CreateSession(playerId, deviceId)
            );

            var deletedCount = await _provider.DeleteExpiredSessionsAsync(
                [deviceId, deviceId, CreateIdentifier("missing-device")]
            );

            Assert.Equal(0, deletedCount);
            Assert.True(GetSessionsByDevice().ContainsKey(deviceId));
            Assert.True(
                GetPlayersById()[playerId]
                .ActiveSessionsByDeviceId
                .ContainsKey(deviceId)
            );
        }

        [Fact]
        public async Task BulkDeletionRechecksExpirationBeforeRemovingCandidate()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            await _provider.TryCreateSessionAsync(
                CreateSession(playerId, deviceId)
            );

            await Task.Delay(1100);

            var deviceIds = await _provider.GetExpiredSessionDeviceIdsAsync();
            GetSessionsByDevice()[deviceId].LastActiveUtc = DateTime.UtcNow;
            var deletedCount = await _provider.DeleteExpiredSessionsAsync(
                deviceIds
            );

            Assert.Equal(0, deletedCount);
            Assert.True(GetSessionsByDevice().ContainsKey(deviceId));
        }

        [Fact]
        public async Task BulkDeletionDoesNotRemoveReplacementSession()
        {
            var oldPlayerId = CreateIdentifier("old-player");
            var newPlayerId = CreateIdentifier("new-player");
            var deviceId = CreateIdentifier("device");
            await _provider.TryCreateSessionAsync(
                CreateSession(oldPlayerId, deviceId)
            );

            await Task.Delay(1100);

            var expiredDeviceIds = await _provider
            .GetExpiredSessionDeviceIdsAsync();
            var replacementSession = CreateSession(newPlayerId, deviceId);
            var creationStatus = await _provider.TryCreateSessionAsync(
                replacementSession
            );
            var deletedCount = await _provider.DeleteExpiredSessionsAsync(
                expiredDeviceIds
            );
            var storedSession = GetSessionsByDevice()[deviceId];

            Assert.Equal(SessionCreationStatus.Created, creationStatus);
            Assert.Equal(0, deletedCount);
            Assert.Equal(replacementSession.SessionId, storedSession.SessionId);
            Assert.Empty(
                GetPlayersById()[oldPlayerId].ActiveSessionsByDeviceId
            );
            Assert.Same(
                storedSession,
                GetPlayersById()[newPlayerId].ActiveSessionsByDeviceId[deviceId]
            );
        }

        [Fact]
        public async Task CleanupAndAuthenticationPreserveAnActiveSession()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            var session = CreateSession(playerId, deviceId);
            await _provider.TryCreateSessionAsync(session);

            var operations = Enumerable.Range(0, 50)
            .SelectMany(_ => new Task[]
            {
                _provider.AuthenticateAndExtendSessionAsync(
                    deviceId,
                    session.SessionId
                ),
                CleanupOnceAsync()
            })
            .ToList();

            await Task.WhenAll(operations);

            var authenticationResult = await _provider
            .AuthenticateAndExtendSessionAsync(
                deviceId,
                session.SessionId
            );

            Assert.Equal(
                SessionAuthenticationStatus.Succeeded,
                authenticationResult.Status
            );
            Assert.True(GetSessionsByDevice().ContainsKey(deviceId));
        }

        private async Task CleanupOnceAsync()
        {
            var deviceIds = await _provider.GetExpiredSessionDeviceIdsAsync();

            await _provider.DeleteExpiredSessionsAsync(deviceIds);
        }

        private ConcurrentDictionary<string, MemorySession> GetSessionsByDevice()
        {
            return GetSourceProperty<ConcurrentDictionary<string, MemorySession>>(
                "SessionsByDeviceId"
            );
        }

        private ConcurrentDictionary<string, MemoryPlayer> GetPlayersById()
        {
            return GetSourceProperty<ConcurrentDictionary<string, MemoryPlayer>>(
                "PlayersById"
            );
        }

        private ConcurrentDictionary<string, long> GetScoresByPlayerId()
        {
            return GetSourceProperty<ConcurrentDictionary<string, long>>(
                "ScoresByPlayerId"
            );
        }

        private ConcurrentDictionary<string, SemaphoreSlim> GetSessionLocksByDevice()
        {
            return GetSourceProperty<ConcurrentDictionary<string, SemaphoreSlim>>(
                "SessionLocksByDeviceId"
            );
        }

        private TProperty GetSourceProperty<TProperty>(string propertyName)
        {
            var property = typeof(MemoryGameDataSource).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.NotNull(property);
            var value = Assert.IsType<TProperty>(
                property.GetValue(_gameDataSource)
            );

            return value;
        }

        private static Session CreateSession(string playerId, string deviceId)
        {
            var session = new Session
            {
                SessionId = CreateIdentifier("session"),
                PlayerId = playerId,
                DeviceId = deviceId
            };

            return session;
        }

        private static string CreateIdentifier(string prefix)
        {
            var identifier = $"{prefix}-{Guid.NewGuid():N}";

            return identifier;
        }
    }
}
