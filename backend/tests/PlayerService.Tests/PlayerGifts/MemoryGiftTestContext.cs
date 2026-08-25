using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using PlayerService.Lib.DAL.Models.Memory;
using PlayerService.Lib.DAL.Providers.Memory;
using PlayerService.Lib.DAL.Sources;
using PlayerService.Lib.Repositories;
using PlayerService.Lib.Services;
using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Models.Sessions.Enums;
using PlayerService.Shared.Repositories;
using PlayerService.Shared.Services;
using PlayerService.Tests.Sessions;

namespace PlayerService.Tests.PlayerGifts
{
    public class MemoryGiftTestContext : IDisposable
    {
        private readonly MemoryGameDataSource _gameDataSource;

        public ConcurrentDictionary<string, MemoryPlayer> PlayersById { get; }
        public ConcurrentDictionary<string, long> ScoresByPlayerId { get; }
        public SessionMemoryProvider SessionProvider { get; }
        public PlayerStatsMemoryProvider StatsProvider { get; }
        public PlayerGiftMemoryProvider GiftProvider { get; }
        public IPlayerGiftService GiftService { get; }

        public MemoryGiftTestContext(
            int giftRequestTtlInSeconds = 1,
            int giftRateLimitWindowInSeconds = 60,
            int giftRateLimitMaxRequests = 10000
        )
        {
            var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlayerService:sessionTtlInSeconds"] = "300",
                ["PlayerService:sessionCleanupIntervalInSeconds"] = "1",
                ["PlayerService:scoreRequestTtlInSeconds"] = "1",
                ["PlayerService:giftRequestTtlInSeconds"] = giftRequestTtlInSeconds.ToString(),
                ["PlayerService:giftRateLimitWindowInSeconds"] = giftRateLimitWindowInSeconds.ToString(),
                ["PlayerService:giftRateLimitMaxRequests"] = giftRateLimitMaxRequests.ToString()
            })
            .Build();

            _gameDataSource = new MemoryGameDataSource();
            PlayersById = GetSourceProperty<
                ConcurrentDictionary<string, MemoryPlayer>
            >("PlayersById");
            ScoresByPlayerId = GetSourceProperty<
                ConcurrentDictionary<string, long>
            >("ScoresByPlayerId");
            SessionProvider = new SessionMemoryProvider(
                _gameDataSource,
                configuration
            );
            StatsProvider = new PlayerStatsMemoryProvider(
                _gameDataSource,
                configuration
            );
            GiftProvider = new PlayerGiftMemoryProvider(
                _gameDataSource,
                SessionProvider,
                configuration
            );
            IPlayerGiftRepository repository = new PlayerGiftRepository(
                GiftProvider
            );
            GiftService = new PlayerGiftService(
                repository,
                new NullExerciseLogger()
            );
        }

        public async Task<MemoryPlayer> CreatePlayerAsync(
            string playerId,
            bool online = true,
            long score = 0
        )
        {
            var deviceId = CreateIdentifier("device");
            var status = await SessionProvider.TryCreateSessionAsync(
                new Session
                {
                    SessionId = CreateIdentifier("session"),
                    PlayerId = playerId,
                    DeviceId = deviceId
                }
            );
            var player = PlayersById[playerId];

            Assert.Equal(SessionCreationStatus.Created, status);
            ScoresByPlayerId[playerId] = score;

            if (!online)
            {
                player.ActiveSessionsByDeviceId[deviceId].LastActiveUtc =
                    DateTime.UtcNow.AddMinutes(-10);
            }

            return player;
        }

        public long GetScore(string playerId)
        {
            ScoresByPlayerId.TryGetValue(playerId, out var score);

            return score;
        }

        public void SetScore(string playerId, long score)
        {
            ScoresByPlayerId[playerId] = score;
        }

        public void Dispose()
        {
            _gameDataSource.Dispose();
            GC.SuppressFinalize(this);
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

        private static string CreateIdentifier(string prefix)
        {
            var identifier = $"{prefix}-{Guid.NewGuid():N}";

            return identifier;
        }
    }
}
