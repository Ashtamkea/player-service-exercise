using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using PlayerService.Lib.DAL.Models.Memory;
using PlayerService.Lib.DAL.Providers.Memory;
using PlayerService.Lib.DAL.Sources;
using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Models.Sessions.Enums;

namespace PlayerService.Tests.Leaderboards
{
    public class MemoryLeaderboardTestContext : IDisposable
    {
        private readonly MemoryGameDataSource _gameDataSource;

        public ConcurrentDictionary<string, long> ScoresByPlayerId { get; }
        public ConcurrentDictionary<string, MemoryPlayer> PlayersById { get; }
        public SessionMemoryProvider SessionProvider { get; }
        public PlayerStatsMemoryProvider StatsProvider { get; }
        public PlayerGiftMemoryProvider GiftProvider { get; }
        public LeaderboardMemoryProvider LeaderboardProvider { get; }

        public MemoryLeaderboardTestContext(int topSize)
        {
            var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PlayerService:sessionTtlInSeconds"] = "300",
                ["PlayerService:scoreRequestTtlInSeconds"] = "1200",
                ["PlayerService:giftRequestTtlInSeconds"] = "1200",
                ["PlayerService:giftRateLimitWindowInSeconds"] = "60",
                ["PlayerService:giftRateLimitMaxRequests"] = "10000",
                ["PlayerService:leaderboardTopSize"] = topSize.ToString()
            })
            .Build();

            _gameDataSource = new MemoryGameDataSource();
            ScoresByPlayerId = GetSourceProperty<
                ConcurrentDictionary<string, long>
            >("ScoresByPlayerId");
            PlayersById = GetSourceProperty<
                ConcurrentDictionary<string, MemoryPlayer>
            >("PlayersById");
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
            LeaderboardProvider = new LeaderboardMemoryProvider(
                _gameDataSource,
                configuration
            );
        }

        public async Task CreatePlayerAsync(
            string playerId,
            long score
        )
        {
            var status = await SessionProvider.TryCreateSessionAsync(
                new Session
                {
                    SessionId = $"session-{Guid.NewGuid():N}",
                    PlayerId = playerId,
                    DeviceId = $"device-{Guid.NewGuid():N}"
                }
            );

            Assert.Equal(SessionCreationStatus.Created, status);
            ScoresByPlayerId[playerId] = score;
        }

        public TProperty GetSourceProperty<TProperty>(string propertyName)
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

        public void Dispose()
        {
            _gameDataSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
