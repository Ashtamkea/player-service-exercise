using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.PlayerGifts;
using PlayerService.Shared.Models.PlayerStats;
using PlayerService.Shared.Models.Sessions;
using PlayerStatsModel = PlayerService.Shared.Models.PlayerStats.PlayerStats;

namespace PlayerService.Tests.Http
{
    public class PlayerGiftHttpIntegrationTests : IClassFixture<PlayerServiceHttpFixture>
    {
        private readonly PlayerServiceHttpFixture _fixture;

        public PlayerGiftHttpIntegrationTests(PlayerServiceHttpFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SuccessfulGiftAndChangedDuplicateReturnOriginalOutcome()
        {
            var sender = await LoginAsync(CreateIdentifier("sender"));
            var recipient = await LoginAsync(CreateIdentifier("recipient"));
            var requestId = Guid.NewGuid();

            var first = await GiftAsync(sender, recipient.PlayerId, 10, requestId);
            var duplicate = await GiftAsync(sender, CreateIdentifier("changed"), 999, requestId);
            var firstResult = await first.Content.ReadFromJsonAsync<GiftResponse>();
            var duplicateResult = await duplicate.Content.ReadFromJsonAsync<GiftResponse>();

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
            Assert.NotNull(firstResult);
            Assert.NotNull(duplicateResult);
            Assert.Equal(ConstantValues.GiftSucceededOutcome, firstResult.Outcome);
            Assert.Equal(990, firstResult.SenderScore);
            Assert.Equal(1010, firstResult.RecipientScore);
            Assert.Equal(firstResult.Outcome, duplicateResult.Outcome);
            Assert.Equal(firstResult.SenderScore, duplicateResult.SenderScore);
            Assert.Equal(firstResult.RecipientScore, duplicateResult.RecipientScore);
            Assert.Equal(990, (await GetStatsAsync(sender, sender.PlayerId)).Score);
            Assert.Equal(1010, (await GetStatsAsync(sender, recipient.PlayerId)).Score);
        }

        [Fact]
        public async Task ConcurrentDuplicatesReturnSameOutcomeAndApplyOnce()
        {
            var sender = await LoginAsync(CreateIdentifier("sender"));
            var recipient = await LoginAsync(CreateIdentifier("recipient"));
            var requestId = Guid.NewGuid();

            var responses = await Task.WhenAll(
                GiftAsync(sender, recipient.PlayerId, 15, requestId),
                GiftAsync(sender, recipient.PlayerId, 15, requestId)
            );
            var results = await Task.WhenAll(
                responses.Select(response => response.Content.ReadFromJsonAsync<GiftResponse>())
            );

            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Assert.All(results, result => Assert.Equal(985, result!.SenderScore));
            Assert.All(results, result => Assert.Equal(1015, result!.RecipientScore));
            Assert.Equal(985, (await GetStatsAsync(sender, sender.PlayerId)).Score);
            Assert.Equal(1015, (await GetStatsAsync(sender, recipient.PlayerId)).Score);
        }

        [Fact]
        public async Task OfflineAndInsufficientGiftsAreRejectedWithoutMutation()
        {
            var sender = await LoginAsync(CreateIdentifier("sender"));
            var recipient = await LoginAsync(CreateIdentifier("recipient"));
            var offline = await GiftAsync(sender, CreateIdentifier("missing"), 1, Guid.NewGuid());
            var insufficient = await GiftAsync(sender, recipient.PlayerId, 1001, Guid.NewGuid());

            Assert.Equal(HttpStatusCode.Conflict, offline.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode);
            Assert.Equal(1000, (await GetStatsAsync(sender, sender.PlayerId)).Score);
            Assert.Equal(1000, (await GetStatsAsync(sender, recipient.PlayerId)).Score);
        }

        [Fact]
        public async Task GiftEndpointEnforcesAuthenticationOwnershipAndValidation()
        {
            var sender = await LoginAsync(CreateIdentifier("sender"));
            var recipient = await LoginAsync(CreateIdentifier("recipient"));
            var unauthorized = await _fixture.Client.PostAsJsonAsync(
                $"/players/{sender.PlayerId}/gifts",
                new GiftRequest
                {
                    RecipientPlayerId = recipient.PlayerId,
                    Points = 1,
                    RequestId = Guid.NewGuid()
                }
            );
            var forbiddenSender = sender with { PlayerId = recipient.PlayerId };
            var forbidden = await GiftAsync(forbiddenSender, sender.PlayerId, 1, Guid.NewGuid());
            var selfGift = await GiftAsync(sender, sender.PlayerId, 1, Guid.NewGuid());
            var invalid = await GiftAsync(sender, recipient.PlayerId, 0, Guid.NewGuid());

            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, selfGift.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }

        [Fact]
        public async Task FirstFiftyUniqueGiftsSucceedAndFiftyFirstIsRateLimited()
        {
            await using var factory = new PlayerServiceWebApplicationFactory(
                sessionTtlInSeconds: 300,
                giftRateLimitMaxRequests: 50
            );
            using var client = factory.CreateClient();
            var sender = await LoginAsync(
                client,
                CreateIdentifier("rate-sender")
            );
            var recipient = await LoginAsync(
                client,
                CreateIdentifier("rate-recipient")
            );
            var firstRequestId = Guid.NewGuid();
            var acceptedResponses = new List<HttpResponseMessage>();

            for (var requestNumber = 0; requestNumber < 50; requestNumber++)
            {
                var response = await GiftAsync(
                    client,
                    sender,
                    recipient.PlayerId,
                    1,
                    requestNumber == 0
                        ? firstRequestId
                        : Guid.NewGuid()
                );
                acceptedResponses.Add(response);
            }

            var duplicateResponse = await GiftAsync(
                client,
                sender,
                recipient.PlayerId,
                999,
                firstRequestId
            );
            var limitedResponse = await GiftAsync(
                client,
                sender,
                recipient.PlayerId,
                1,
                Guid.NewGuid()
            );
            var limitedResult = await limitedResponse.Content
            .ReadFromJsonAsync<GiftResponse>();
            var senderStats = await GetStatsAsync(
                client,
                sender,
                sender.PlayerId
            );
            var recipientStats = await GetStatsAsync(
                client,
                sender,
                recipient.PlayerId
            );

            Assert.All(
                acceptedResponses,
                response => Assert.Equal(
                    HttpStatusCode.OK,
                    response.StatusCode
                )
            );
            Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
            Assert.Equal(
                HttpStatusCode.TooManyRequests,
                limitedResponse.StatusCode
            );
            Assert.NotNull(limitedResult);
            Assert.Equal("rateLimited", limitedResult.Outcome);
            Assert.Equal(950, senderStats.Score);
            Assert.Equal(1050, recipientStats.Score);
        }

        [Fact]
        public async Task SwaggerMarksGiftEndpointAsProtected()
        {
            var response = await _fixture.Client.GetAsync("/swagger/v1/swagger.json");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var operation = document.RootElement
                .GetProperty("paths")
                .GetProperty("/players/{playerId}/gifts")
                .GetProperty("post");
            var security = operation.GetProperty("security")[0];

            Assert.True(security.TryGetProperty(ConstantValues.SessionBearerSecurityScheme, out _));
            Assert.True(security.TryGetProperty(ConstantValues.SessionDeviceSecurityScheme, out _));
        }

        private async Task<PlayerIdentity> LoginAsync(string playerId)
        {
            var identity = await LoginAsync(_fixture.Client, playerId);

            return identity;
        }

        private static async Task<PlayerIdentity> LoginAsync(
            HttpClient client,
            string playerId
        )
        {
            var deviceId = CreateIdentifier("device");
            var response = await client.PostAsJsonAsync(
                "/login",
                new LoginRequest { PlayerId = playerId, DeviceId = deviceId }
            );
            response.EnsureSuccessStatusCode();
            var identity = new PlayerIdentity(
                playerId,
                deviceId,
                await response.Content.ReadAsStringAsync()
            );

            return identity;
        }

        private Task<HttpResponseMessage> GiftAsync(
            PlayerIdentity sender,
            string recipientPlayerId,
            int points,
            Guid requestId
        )
        {
            var task = GiftAsync(
                _fixture.Client,
                sender,
                recipientPlayerId,
                points,
                requestId
            );

            return task;
        }

        private static Task<HttpResponseMessage> GiftAsync(
            HttpClient client,
            PlayerIdentity sender,
            string recipientPlayerId,
            int points,
            Guid requestId
        )
        {
            var request = CreateAuthenticatedRequest(
                HttpMethod.Post,
                $"/players/{sender.PlayerId}/gifts",
                sender
            );
            request.Content = JsonContent.Create(new GiftRequest
            {
                RecipientPlayerId = recipientPlayerId,
                Points = points,
                RequestId = requestId
            });
            var task = client.SendAsync(request);

            return task;
        }

        private async Task<PlayerStatsModel> GetStatsAsync(PlayerIdentity reader, string playerId)
        {
            var playerStats = await GetStatsAsync(
                _fixture.Client,
                reader,
                playerId
            );

            return playerStats;
        }

        private static async Task<PlayerStatsModel> GetStatsAsync(
            HttpClient client,
            PlayerIdentity reader,
            string playerId
        )
        {
            var response = await client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/players/{playerId}/stats",
                reader
            ));
            response.EnsureSuccessStatusCode();
            var playerStats = await response.Content
            .ReadFromJsonAsync<PlayerStatsModel>();

            return playerStats!;
        }

        private static HttpRequestMessage CreateAuthenticatedRequest(
            HttpMethod method,
            string path,
            PlayerIdentity identity
        )
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", identity.SessionId);
            request.Headers.Add(ConstantValues.DeviceIdHeader, identity.DeviceId);
            return request;
        }

        private static string CreateIdentifier(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

        private sealed record PlayerIdentity(string PlayerId, string DeviceId, string SessionId);
    }
}
