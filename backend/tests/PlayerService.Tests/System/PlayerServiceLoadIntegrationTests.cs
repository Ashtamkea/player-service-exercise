using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.PlayerGifts;
using PlayerService.Shared.Models.PlayerStats;
using PlayerService.Shared.Models.Sessions;
using PlayerService.Tests.Http;
using PlayerStatsModel = PlayerService.Shared.Models.PlayerStats.PlayerStats;

namespace PlayerService.Tests.System
{
    public class PlayerServiceLoadIntegrationTests : IAsyncLifetime
    {
        private PlayerServiceWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;

        public Task InitializeAsync()
        {
            _factory = new PlayerServiceWebApplicationFactory(
                sessionTtlInSeconds: 300,
                giftRateLimitMaxRequests: 10000
            );
            _client = _factory.CreateClient();
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _factory.DisposeAsync();
        }

        [Fact]
        public async Task ConcurrentUniqueAndDuplicateScoresProduceExactTotal()
        {
            var player = await LoginAsync("score-load-player");
            var requests = Enumerable.Range(0, 200)
                .Select(_ => (RequestId: Guid.NewGuid(), Points: 2))
                .ToList();
            var calls = requests.SelectMany(request => new[]
            {
                AddScoreAsync(player, request.Points, request.RequestId),
                AddScoreAsync(player, request.Points, request.RequestId)
            });

            var responses = await Task.WhenAll(calls);
            var stats = await GetStatsAsync(player, player.PlayerId);

            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Assert.Equal(1400, stats.Score);
        }

        [Fact]
        public async Task OverlappingGiftBurstConservesPointsAndReturnsStableDuplicates()
        {
            var players = new List<PlayerIdentity>();

            for (var index = 0; index < 16; index++)
                players.Add(await LoginAsync($"gift-load-{index:D2}"));

            var random = new Random(918273);
            var gifts = Enumerable.Range(0, 160)
                .Select(_ =>
                {
                    var senderIndex = random.Next(players.Count);
                    var recipientIndex = random.Next(players.Count - 1);

                    if (recipientIndex >= senderIndex)
                        recipientIndex++;

                    return (
                        Sender: players[senderIndex],
                        Recipient: players[recipientIndex],
                        RequestId: Guid.NewGuid()
                    );
                })
                .ToList();
            var calls = gifts.SelectMany(gift => new[]
            {
                GiftAsync(gift.Sender, gift.Recipient.PlayerId, gift.RequestId),
                GiftAsync(gift.Sender, gift.Recipient.PlayerId, gift.RequestId)
            });

            var responses = await Task.WhenAll(calls);
            var results = await Task.WhenAll(
                responses.Select(response => response.Content.ReadFromJsonAsync<GiftResponse>())
            );
            var stats = await Task.WhenAll(
                players.Select(player => GetStatsAsync(players[0], player.PlayerId))
            );

            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Assert.Equal(16000, stats.Sum(playerStats => playerStats.Score));
            Assert.All(stats, playerStats => Assert.True(playerStats.Score >= 0));
            Assert.Equal(160, stats.Sum(playerStats => playerStats.GiftsSent));
            Assert.Equal(160, stats.Sum(playerStats => playerStats.GiftsReceived));

            for (var index = 0; index < results.Length; index += 2)
            {
                Assert.Equal(results[index]!.Outcome, results[index + 1]!.Outcome);
                Assert.Equal(results[index]!.SenderScore, results[index + 1]!.SenderScore);
                Assert.Equal(results[index]!.RecipientScore, results[index + 1]!.RecipientScore);
            }
        }

        private async Task<PlayerIdentity> LoginAsync(string playerId)
        {
            var deviceId = $"device-{Guid.NewGuid():N}";
            var response = await _client.PostAsJsonAsync(
                "/login",
                new LoginRequest { PlayerId = playerId, DeviceId = deviceId }
            );
            response.EnsureSuccessStatusCode();
            return new PlayerIdentity(playerId, deviceId, await response.Content.ReadAsStringAsync());
        }

        private Task<HttpResponseMessage> AddScoreAsync(
            PlayerIdentity player,
            int points,
            Guid requestId
        )
        {
            var request = CreateRequest(
                HttpMethod.Post,
                $"/players/{player.PlayerId}/stats/score",
                player
            );
            request.Content = JsonContent.Create(new AddScoreRequest
            {
                Points = points,
                RequestId = requestId
            });
            return _client.SendAsync(request);
        }

        private Task<HttpResponseMessage> GiftAsync(
            PlayerIdentity sender,
            string recipientPlayerId,
            Guid requestId
        )
        {
            var request = CreateRequest(
                HttpMethod.Post,
                $"/players/{sender.PlayerId}/gifts",
                sender
            );
            request.Content = JsonContent.Create(new GiftRequest
            {
                RecipientPlayerId = recipientPlayerId,
                Points = 1,
                RequestId = requestId
            });
            return _client.SendAsync(request);
        }

        private async Task<PlayerStatsModel> GetStatsAsync(PlayerIdentity reader, string playerId)
        {
            var response = await _client.SendAsync(CreateRequest(
                HttpMethod.Get,
                $"/players/{playerId}/stats",
                reader
            ));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<PlayerStatsModel>())!;
        }

        private static HttpRequestMessage CreateRequest(
            HttpMethod method,
            string path,
            PlayerIdentity player
        )
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", player.SessionId);
            request.Headers.Add(ConstantValues.DeviceIdHeader, player.DeviceId);
            return request;
        }

        private sealed record PlayerIdentity(string PlayerId, string DeviceId, string SessionId);
    }
}
