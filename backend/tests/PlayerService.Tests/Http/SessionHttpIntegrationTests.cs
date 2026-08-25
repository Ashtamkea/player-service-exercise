using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.Sessions;

namespace PlayerService.Tests.Http
{
    public class SessionHttpIntegrationTests : IClassFixture<PlayerServiceHttpFixture>
    {
        private readonly PlayerServiceHttpFixture _fixture;

        public SessionHttpIntegrationTests(PlayerServiceHttpFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task LoginHealthAndSwaggerAreAnonymous()
        {
            var loginResponse = await LoginAsync(
                CreateIdentifier("player"),
                CreateIdentifier("device")
            );
            var healthResponse = await _fixture.Client.GetAsync("/api/Health");
            var swaggerResponse = await _fixture.Client.GetAsync("/swagger/v1/swagger.json");
            var swaggerDocument = await swaggerResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, swaggerResponse.StatusCode);
            Assert.Contains(ConstantValues.SessionBearerSecurityScheme, swaggerDocument);
            Assert.Contains(ConstantValues.DeviceIdHeader, swaggerDocument);
        }

        [Fact]
        public async Task RemovedSessionRoutesReturnNotFound()
        {
            var responses = await Task.WhenAll(
                _fixture.Client.PostAsJsonAsync("/logout", new { }),
                _fixture.Client.PostAsJsonAsync("/session/extend", new { }),
                _fixture.Client.PostAsJsonAsync("/session/validate", new { }),
                _fixture.Client.GetAsync("/sessions/device/device"),
                _fixture.Client.GetAsync("/sessions/player/player")
            );

            Assert.All(
                responses,
                response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)
            );
        }

        [Fact]
        public async Task ActiveDeviceLoginReturnsConflict()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            var firstResponse = await LoginAsync(playerId, deviceId);

            var secondResponse = await LoginAsync(playerId, deviceId);

            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        }

        [Fact]
        public async Task ConcurrentLoginsReturnOneSuccessAndOneConflict()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");

            var responses = await Task.WhenAll(
                LoginAsync(playerId, deviceId),
                LoginAsync(playerId, deviceId)
            );

            Assert.Single(
                responses,
                response => response.StatusCode == HttpStatusCode.OK
            );
            Assert.Single(
                responses,
                response => response.StatusCode == HttpStatusCode.Conflict
            );

            var successfulResponse = responses.Single(
                response => response.StatusCode == HttpStatusCode.OK
            );
            var sessionId = await successfulResponse.Content.ReadAsStringAsync();
            var protectedResponse = await GetProtectedAsync(deviceId, sessionId);

            Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
        }

        [Fact]
        public async Task ProtectedEndpointRejectsMissingMalformedAndInvalidCredentials()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            var loginResponse = await LoginAsync(playerId, deviceId);
            var sessionId = await loginResponse.Content.ReadAsStringAsync();

            var missingCredentialsResponse = await _fixture.Client.GetAsync("/test/protected");

            using var malformedRequest = new HttpRequestMessage(
                HttpMethod.Get,
                "/test/protected"
            );
            malformedRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Token",
                sessionId
            );
            malformedRequest.Headers.Add(ConstantValues.DeviceIdHeader, deviceId);
            var malformedResponse = await _fixture.Client.SendAsync(malformedRequest);

            using var missingDeviceRequest = new HttpRequestMessage(
                HttpMethod.Get,
                "/test/protected"
            );
            missingDeviceRequest.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                sessionId
            );
            var missingDeviceResponse = await _fixture.Client.SendAsync(missingDeviceRequest);
            var invalidSessionResponse = await GetProtectedAsync(
                deviceId,
                CreateIdentifier("invalid-session")
            );

            Assert.Equal(HttpStatusCode.Unauthorized, missingCredentialsResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, malformedResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, missingDeviceResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, invalidSessionResponse.StatusCode);
        }

        [Fact]
        public async Task ValidCredentialsRefreshTtlAndPopulateRequestContextEachTime()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            var loginResponse = await LoginAsync(playerId, deviceId);
            var sessionId = await loginResponse.Content.ReadAsStringAsync();

            await Task.Delay(2000);
            var firstResponse = await GetProtectedAsync(deviceId, sessionId);
            var context = await firstResponse.Content
            .ReadFromJsonAsync<SessionAuthenticationContext>();

            await Task.Delay(2000);
            var secondResponse = await GetProtectedAsync(deviceId, sessionId);

            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            Assert.NotNull(context);
            Assert.Equal(sessionId, context.SessionId);
            Assert.Equal(playerId, context.PlayerId);
            Assert.Equal(deviceId, context.DeviceId);
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        }

        [Fact]
        public async Task ExpiredCredentialsReturnUnauthorized()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            var loginResponse = await LoginAsync(playerId, deviceId);
            var sessionId = await loginResponse.Content.ReadAsStringAsync();

            await Task.Delay(TimeSpan.FromMilliseconds(3300));

            var response = await GetProtectedAsync(deviceId, sessionId);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AuthenticationRefreshesTtlBeforeControllerFailure()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            var loginResponse = await LoginAsync(playerId, deviceId);
            var sessionId = await loginResponse.Content.ReadAsStringAsync();

            await Task.Delay(2000);
            var response = await GetProtectedAsync(
                deviceId,
                sessionId,
                "/test/protected/failure"
            );
            await Task.Delay(2000);
            var activeResponse = await GetProtectedAsync(deviceId, sessionId);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        }

        [Fact]
        public async Task SupersededCredentialsDoNotRefreshNewSession()
        {
            var playerId = CreateIdentifier("player");
            var deviceId = CreateIdentifier("device");
            var firstLoginResponse = await LoginAsync(playerId, deviceId);
            var oldSessionId = await firstLoginResponse.Content.ReadAsStringAsync();

            await Task.Delay(TimeSpan.FromMilliseconds(3300));

            var secondLoginResponse = await LoginAsync(playerId, deviceId);
            var newSessionId = await secondLoginResponse.Content.ReadAsStringAsync();

            await Task.Delay(2000);
            var oldResponse = await GetProtectedAsync(deviceId, oldSessionId);
            await Task.Delay(2000);
            var newResponse = await GetProtectedAsync(deviceId, newSessionId);

            Assert.Equal(HttpStatusCode.Unauthorized, oldResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, newResponse.StatusCode);
        }

        private Task<HttpResponseMessage> LoginAsync(string playerId, string deviceId)
        {
            var request = new LoginRequest
            {
                PlayerId = playerId,
                DeviceId = deviceId
            };
            var task = _fixture.Client.PostAsJsonAsync("/login", request);

            return task;
        }

        private async Task<HttpResponseMessage> GetProtectedAsync(
            string deviceId,
            string sessionId,
            string path = "/test/protected"
        )
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                path
            );
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                sessionId
            );
            request.Headers.Add(ConstantValues.DeviceIdHeader, deviceId);

            var response = await _fixture.Client.SendAsync(request);

            return response;
        }

        private static string CreateIdentifier(string prefix)
        {
            var identifier = $"{prefix}-{Guid.NewGuid():N}";

            return identifier;
        }
    }
}
