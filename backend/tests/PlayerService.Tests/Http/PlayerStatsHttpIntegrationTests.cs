using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.PlayerStats;
using PlayerService.Shared.Models.Sessions;
using PlayerStatsModel = PlayerService.Shared.Models.PlayerStats.PlayerStats;

namespace PlayerService.Tests.Http
{
    public class PlayerStatsHttpIntegrationTests : IClassFixture<PlayerServiceHttpFixture>
    {
        private readonly PlayerServiceHttpFixture _fixture;

        public PlayerStatsHttpIntegrationTests(PlayerServiceHttpFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task DuplicateScoreUpdateReturnsCurrentStatsWithoutApplyingAgain()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            var sessionId = await LoginAsync(playerId, deviceId);
            var requestId = Guid.NewGuid();

            var first = await AddScoreAsync(playerId, deviceId, sessionId, 25, requestId);
            var later = await AddScoreAsync(
                playerId,
                deviceId,
                sessionId,
                10,
                Guid.NewGuid()
            );
            var duplicate = await AddScoreAsync(playerId, deviceId, sessionId, 999, requestId);
            var stats = await GetStatsAsync(playerId, deviceId, sessionId);
            var firstStats = await first.Content.ReadFromJsonAsync<PlayerStatsModel>();
            var laterStats = await later.Content.ReadFromJsonAsync<PlayerStatsModel>();
            var duplicateStats = await duplicate.Content.ReadFromJsonAsync<PlayerStatsModel>();

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, later.StatusCode);
            Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
            Assert.NotNull(firstStats);
            Assert.NotNull(laterStats);
            Assert.NotNull(duplicateStats);
            Assert.Equal(1025, firstStats.Score);
            Assert.Equal(1035, laterStats.Score);
            Assert.Equal(1035, duplicateStats.Score);
            Assert.NotNull(stats);
            Assert.Equal(1035, stats.Score);
            Assert.Equal(0, stats.GiftsSent);
            Assert.Equal(0, stats.GiftsReceived);
        }

        [Fact]
        public async Task SimultaneousDuplicateScoreRequestsApplyExactlyOnce()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            var sessionId = await LoginAsync(playerId, deviceId);
            var requestId = Guid.NewGuid();

            var responses = await Task.WhenAll(
                AddScoreAsync(playerId, deviceId, sessionId, 10, requestId),
                AddScoreAsync(playerId, deviceId, sessionId, 10, requestId)
            );
            var stats = await Task.WhenAll(
                responses.Select(
                    response => response.Content.ReadFromJsonAsync<PlayerStatsModel>()
                )
            );

            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Assert.All(stats, value => Assert.Equal(1010, value!.Score));
            Assert.Equal(1010, (await GetStatsAsync(playerId, deviceId, sessionId))!.Score);
        }

        [Fact]
        public async Task StatsMayBeReadAcrossPlayersButScoreOwnershipIsEnforced()
        {
            var readerId = CreateIdentifier("reader");
            var targetId = CreateIdentifier("target");
            var deviceId = CreateIdentifier("device");
            var sessionId = await LoginAsync(readerId, deviceId);
            await LoginAsync(targetId, CreateIdentifier("target-device"));

            var stats = await GetStatsResponseAsync(targetId, deviceId, sessionId);
            var forbidden = await AddScoreAsync(targetId, deviceId, sessionId, 1, Guid.NewGuid());

            Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        [Fact]
        public async Task ScoreEndpointValidatesAuthenticationAndBody()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            var sessionId = await LoginAsync(playerId, deviceId);
            var unauthorized = await _fixture.Client.PostAsJsonAsync(
                $"/players/{playerId}/stats/score",
                new AddScoreRequest { Points = 1, RequestId = Guid.NewGuid() }
            );
            var invalidPoints = await AddScoreAsync(playerId, deviceId, sessionId, 0, Guid.NewGuid());
            var emptyRequestId = await AddScoreAsync(playerId, deviceId, sessionId, 1, Guid.Empty);

            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalidPoints.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, emptyRequestId.StatusCode);
        }

        [Fact]
        public async Task SwaggerMarksStatsEndpointsAsProtected()
        {
            var response = await _fixture.Client.GetAsync("/swagger/v1/swagger.json");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var paths = document.RootElement.GetProperty("paths");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertProtected(paths
                .GetProperty("/players/{playerId}/stats")
                .GetProperty("get"));
            AssertProtected(paths
                .GetProperty("/players/{playerId}/stats/score")
                .GetProperty("post"));
        }

        private async Task<string> LoginAsync(string playerId, string deviceId)
        {
            var response = await _fixture.Client.PostAsJsonAsync(
                "/login",
                new LoginRequest { PlayerId = playerId, DeviceId = deviceId }
            );
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private Task<HttpResponseMessage> AddScoreAsync(
            string playerId,
            string deviceId,
            string sessionId,
            int points,
            Guid requestId
        )
        {
            var request = CreateRequest(HttpMethod.Post, $"/players/{playerId}/stats/score", deviceId, sessionId);
            request.Content = JsonContent.Create(new AddScoreRequest { Points = points, RequestId = requestId });
            return _fixture.Client.SendAsync(request);
        }

        private async Task<PlayerStatsModel?> GetStatsAsync(string playerId, string deviceId, string sessionId)
        {
            var response = await GetStatsResponseAsync(playerId, deviceId, sessionId);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PlayerStatsModel>();
        }

        private Task<HttpResponseMessage> GetStatsResponseAsync(string playerId, string deviceId, string sessionId)
        {
            return _fixture.Client.SendAsync(CreateRequest(
                HttpMethod.Get,
                $"/players/{playerId}/stats",
                deviceId,
                sessionId
            ));
        }

        private static HttpRequestMessage CreateRequest(
            HttpMethod method,
            string path,
            string deviceId,
            string sessionId
        )
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
            request.Headers.Add(ConstantValues.DeviceIdHeader, deviceId);
            return request;
        }

        private static void AssertProtected(JsonElement operation)
        {
            var security = operation.GetProperty("security")[0];
            Assert.True(security.TryGetProperty(ConstantValues.SessionBearerSecurityScheme, out _));
            Assert.True(security.TryGetProperty(ConstantValues.SessionDeviceSecurityScheme, out _));
        }

        private static string CreateIdentifier(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
    }
}
