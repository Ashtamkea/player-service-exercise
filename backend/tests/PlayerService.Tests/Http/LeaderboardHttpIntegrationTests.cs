using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.Leaderboards;
using PlayerService.Shared.Models.PlayerStats;
using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Services;

namespace PlayerService.Tests.Http
{
    public class LeaderboardHttpIntegrationTests : IClassFixture<PlayerServiceHttpFixture>
    {
        private readonly PlayerServiceHttpFixture _fixture;

        public LeaderboardHttpIntegrationTests(PlayerServiceHttpFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task EndpointRequiresAuthenticationAndReturnsRefreshedTopPlayers()
        {
            var first = await CreatePlayerAsync("leader-a", 50);
            await CreatePlayerAsync("leader-b", 25);
            using var scope = _fixture.Factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ILeaderboardService>();
            Assert.True(await service.TryRefreshAsync());

            var unauthorized = await _fixture.Client.GetAsync("/leaderboard");
            using var request = CreateAuthenticatedRequest(first.DeviceId, first.SessionId);
            var response = await _fixture.Client.SendAsync(request);
            var result = await response.Content.ReadFromJsonAsync<List<LeaderboardEntry>>();

            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(result);
            Assert.Equal("leader-a", result[0].PlayerId);
            Assert.Equal(1050, result[0].Score);
            Assert.Equal("leader-b", result[1].PlayerId);
            Assert.Equal(1025, result[1].Score);
        }

        [Fact]
        public async Task SwaggerMarksLeaderboardAsSessionProtected()
        {
            var response = await _fixture.Client.GetAsync("/swagger/v1/swagger.json");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var operation = document.RootElement
                .GetProperty("paths")
                .GetProperty("/leaderboard")
                .GetProperty("get");
            var security = operation.GetProperty("security")[0];

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(security.TryGetProperty(ConstantValues.SessionBearerSecurityScheme, out _));
            Assert.True(security.TryGetProperty(ConstantValues.SessionDeviceSecurityScheme, out _));
        }

        private async Task<(string DeviceId, string SessionId)> CreatePlayerAsync(
            string playerId,
            int addedScore
        )
        {
            var deviceId = $"device-{Guid.NewGuid():N}";
            var login = await _fixture.Client.PostAsJsonAsync(
                "/login",
                new LoginRequest { PlayerId = playerId, DeviceId = deviceId }
            );
            login.EnsureSuccessStatusCode();
            var sessionId = await login.Content.ReadAsStringAsync();
            var request = new HttpRequestMessage(HttpMethod.Post, $"/players/{playerId}/stats/score");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
            request.Headers.Add(ConstantValues.DeviceIdHeader, deviceId);
            request.Content = JsonContent.Create(new AddScoreRequest
            {
                Points = addedScore,
                RequestId = Guid.NewGuid()
            });
            (await _fixture.Client.SendAsync(request)).EnsureSuccessStatusCode();
            return (deviceId, sessionId);
        }

        private static HttpRequestMessage CreateAuthenticatedRequest(string deviceId, string sessionId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/leaderboard");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
            request.Headers.Add(ConstantValues.DeviceIdHeader, deviceId);
            return request;
        }
    }
}
