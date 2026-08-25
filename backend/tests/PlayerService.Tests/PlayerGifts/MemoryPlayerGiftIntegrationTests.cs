using System.Net;
using PlayerService.Lib.DAL.Models.Memory;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.PlayerGifts;
using PlayerService.Shared.Models.PlayerGifts.Enums;

namespace PlayerService.Tests.PlayerGifts
{
    public class MemoryPlayerGiftIntegrationTests
    {
        [Fact]
        public async Task SuccessfulGiftMutatesBothBalancesCountersAndResultMarker()
        {
            using var context = new MemoryGiftTestContext();
            var senderId = CreateIdentifier("sender");
            var recipientId = CreateIdentifier("recipient");
            var sender = await context.CreatePlayerAsync(senderId);
            var recipient = await context.CreatePlayerAsync(recipientId);
            context.SetScore(senderId, 100);
            context.SetScore(recipientId, 20);
            var requestId = Guid.NewGuid();
            var before = DateTime.UtcNow;

            var result = await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                10,
                requestId
            );
            var marker = sender.GiftRequestsByRequestId[requestId];

            Assert.Equal(GiftOperationStatus.Succeeded, result.Status);
            Assert.True(result.Applied);
            Assert.Equal(90, result.SenderScore);
            Assert.Equal(30, result.RecipientScore);
            Assert.Equal(90, context.GetScore(senderId));
            Assert.Equal(30, context.GetScore(recipientId));
            Assert.Equal(1, sender.GiftsSent);
            Assert.Equal(1, recipient.GiftsReceived);
            Assert.Equal(GiftOperationStatus.Succeeded, marker.Status);
            Assert.InRange(
                marker.ExpiresAtUtc,
                before.AddSeconds(1),
                DateTime.UtcNow.AddSeconds(1)
            );
        }

        [Fact]
        public async Task ChangedDuplicateReturnsOriginalSnapshotWithoutMutation()
        {
            using var context = new MemoryGiftTestContext();
            var senderId = CreateIdentifier("sender");
            var recipientId = CreateIdentifier("recipient");
            var sender = await context.CreatePlayerAsync(senderId);
            await context.CreatePlayerAsync(recipientId);
            context.SetScore(senderId, 100);
            context.SetScore(recipientId, 20);
            var requestId = Guid.NewGuid();
            var first = await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                10,
                requestId
            );
            var marker = sender.GiftRequestsByRequestId[requestId];

            var duplicate = await ExecuteGiftAsync(
                context,
                senderId,
                CreateIdentifier("missing-recipient"),
                999,
                requestId
            );

            Assert.Equal(first.Status, duplicate.Status);
            Assert.Equal(first.SenderScore, duplicate.SenderScore);
            Assert.Equal(first.RecipientScore, duplicate.RecipientScore);
            Assert.False(duplicate.Applied);
            Assert.Equal(90, context.GetScore(senderId));
            Assert.Equal(30, context.GetScore(recipientId));
            Assert.Same(marker, sender.GiftRequestsByRequestId[requestId]);
        }

        [Fact]
        public async Task SimultaneousDuplicateAppliesExactlyOnce()
        {
            using var context = new MemoryGiftTestContext();
            var senderId = CreateIdentifier("sender");
            var recipientId = CreateIdentifier("recipient");
            await context.CreatePlayerAsync(senderId);
            await context.CreatePlayerAsync(recipientId);
            context.SetScore(senderId, 100);
            context.SetScore(recipientId, 20);
            var requestId = Guid.NewGuid();

            var results = await Task.WhenAll(
                ExecuteGiftAsync(
                    context,
                    senderId,
                    recipientId,
                    10,
                    requestId
                ),
                ExecuteGiftAsync(
                    context,
                    senderId,
                    recipientId,
                    10,
                    requestId
                )
            );

            Assert.All(
                results,
                result =>
                {
                    Assert.Equal(GiftOperationStatus.Succeeded, result.Status);
                    Assert.Equal(90, result.SenderScore);
                    Assert.Equal(30, result.RecipientScore);
                }
            );
            Assert.Single(results, result => result.Applied);
            Assert.Single(results, result => !result.Applied);
            Assert.Equal(90, context.GetScore(senderId));
            Assert.Equal(30, context.GetScore(recipientId));
        }

        [Fact]
        public async Task IdenticalRequestIdsAreIndependentAcrossSenders()
        {
            using var context = new MemoryGiftTestContext();
            var firstSenderId = CreateIdentifier("sender-a");
            var secondSenderId = CreateIdentifier("sender-b");
            var recipientId = CreateIdentifier("recipient");
            await context.CreatePlayerAsync(firstSenderId);
            await context.CreatePlayerAsync(secondSenderId);
            await context.CreatePlayerAsync(recipientId);
            context.SetScore(firstSenderId, 10);
            context.SetScore(secondSenderId, 10);
            var requestId = Guid.NewGuid();

            var results = await Task.WhenAll(
                ExecuteGiftAsync(
                    context,
                    firstSenderId,
                    recipientId,
                    1,
                    requestId
                ),
                ExecuteGiftAsync(
                    context,
                    secondSenderId,
                    recipientId,
                    1,
                    requestId
                )
            );

            Assert.All(results, result => Assert.True(result.Applied));
            Assert.Equal(9, context.GetScore(firstSenderId));
            Assert.Equal(9, context.GetScore(secondSenderId));
            Assert.Equal(2, context.GetScore(recipientId));
        }

        [Fact]
        public async Task MissingCanonicalSenderThrowsWithoutCreatingState()
        {
            using var context = new MemoryGiftTestContext();
            var senderId = CreateIdentifier("missing-sender");

            var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
                ExecuteGiftAsync(
                    context,
                    senderId,
                    CreateIdentifier("recipient"),
                    1,
                    Guid.NewGuid()
                )
            );

            Assert.Contains(
                "Canonical player state could not be found.",
                exception.Message,
                StringComparison.Ordinal
            );
            Assert.False(context.PlayersById.ContainsKey(senderId));
            Assert.False(context.ScoresByPlayerId.ContainsKey(senderId));
        }

        [Fact]
        public async Task MissingRecipientOutcomeIsCachedAcrossLaterLogin()
        {
            using var context = new MemoryGiftTestContext();
            var senderId = CreateIdentifier("sender");
            var recipientId = CreateIdentifier("recipient");
            await context.CreatePlayerAsync(senderId);
            context.SetScore(senderId, 100);
            var requestId = Guid.NewGuid();

            var first = await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                10,
                requestId
            );
            await context.CreatePlayerAsync(recipientId);
            var duplicate = await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                10,
                requestId
            );

            Assert.Equal(GiftOperationStatus.RecipientOffline, first.Status);
            Assert.Equal(first.Status, duplicate.Status);
            Assert.False(first.Applied);
            Assert.False(duplicate.Applied);
            Assert.Equal(100, context.GetScore(senderId));
            Assert.Equal(0, context.GetScore(recipientId));
        }

        [Fact]
        public async Task InactiveRecipientAndInsufficientFundsDoNotMutateBalances()
        {
            using var context = new MemoryGiftTestContext();
            var senderId = CreateIdentifier("sender");
            var inactiveRecipientId = CreateIdentifier("inactive-recipient");
            var onlineRecipientId = CreateIdentifier("online-recipient");
            await context.CreatePlayerAsync(senderId);
            await context.CreatePlayerAsync(inactiveRecipientId, online: false);
            await context.CreatePlayerAsync(onlineRecipientId);
            context.SetScore(senderId, 5);

            var offline = await ExecuteGiftAsync(
                context,
                senderId,
                inactiveRecipientId,
                1,
                Guid.NewGuid()
            );
            var insufficient = await ExecuteGiftAsync(
                context,
                senderId,
                onlineRecipientId,
                10,
                Guid.NewGuid()
            );

            Assert.Equal(GiftOperationStatus.RecipientOffline, offline.Status);
            Assert.Equal(
                GiftOperationStatus.InsufficientPoints,
                insufficient.Status
            );
            Assert.Equal(5, insufficient.SenderScore);
            Assert.Equal(5, context.GetScore(senderId));
            Assert.Equal(0, context.GetScore(inactiveRecipientId));
            Assert.Equal(0, context.GetScore(onlineRecipientId));
        }

        [Fact]
        public async Task InsufficientOutcomeRemainsCachedAfterBalanceChanges()
        {
            using var context = new MemoryGiftTestContext();
            var senderId = CreateIdentifier("sender");
            var recipientId = CreateIdentifier("recipient");
            await context.CreatePlayerAsync(senderId);
            await context.CreatePlayerAsync(recipientId);
            context.SetScore(senderId, 5);
            var requestId = Guid.NewGuid();
            var first = await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                10,
                requestId
            );
            context.SetScore(senderId, 100);

            var duplicate = await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                10,
                requestId
            );

            Assert.Equal(GiftOperationStatus.InsufficientPoints, first.Status);
            Assert.Equal(first.Status, duplicate.Status);
            Assert.Equal(first.SenderScore, duplicate.SenderScore);
            Assert.False(duplicate.Applied);
            Assert.Equal(100, context.GetScore(senderId));
            Assert.Equal(0, context.GetScore(recipientId));
        }

        [Fact]
        public async Task RateLimitCountsUniqueAttemptsAndCachesRejection()
        {
            using var context = new MemoryGiftTestContext(
                giftRateLimitWindowInSeconds: 1,
                giftRateLimitMaxRequests: 2
            );
            var senderId = CreateIdentifier("sender");
            var recipientId = CreateIdentifier("recipient");
            await context.CreatePlayerAsync(senderId);
            await context.CreatePlayerAsync(recipientId);
            context.SetScore(senderId, 100);
            var rateLimitedRequestId = Guid.NewGuid();
            await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                1,
                Guid.NewGuid()
            );
            await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                1,
                Guid.NewGuid()
            );

            var limited = await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                1,
                rateLimitedRequestId
            );
            await Task.Delay(1100);
            var duplicate = await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                1,
                rateLimitedRequestId
            );
            var newWindow = await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                1,
                Guid.NewGuid()
            );

            Assert.Equal(GiftOperationStatus.RateLimited, limited.Status);
            Assert.Equal(limited.Status, duplicate.Status);
            Assert.Equal(limited.RetryAfter, duplicate.RetryAfter);
            Assert.Equal(GiftOperationStatus.Succeeded, newWindow.Status);
            Assert.Equal(97, context.GetScore(senderId));
            Assert.Equal(3, context.GetScore(recipientId));
        }

        [Fact]
        public async Task RecipientOrCounterOverflowIsCachedBeforeMutation()
        {
            using var context = new MemoryGiftTestContext();
            var senderId = CreateIdentifier("sender");
            var recipientId = CreateIdentifier("recipient");
            var sender = await context.CreatePlayerAsync(senderId);
            await context.CreatePlayerAsync(recipientId);
            context.SetScore(senderId, 10);
            context.SetScore(recipientId, long.MaxValue);
            var requestId = Guid.NewGuid();

            var execution = await context.GiftService.GiftAsync(
                senderId,
                senderId,
                new GiftRequest
                {
                    RecipientPlayerId = recipientId,
                    Points = 1,
                    RequestId = requestId
                }
            );
            sender.GiftsSent = long.MaxValue;
            context.SetScore(recipientId, 0);
            var counterOverflow = await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                1,
                Guid.NewGuid()
            );

            Assert.Equal((int)HttpStatusCode.Conflict, execution.StatusCode);
            Assert.Equal(
                ConstantValues.GiftRecipientScoreLimitExceededOutcome,
                execution.Response.Outcome
            );
            Assert.Equal(
                GiftOperationStatus.RecipientScoreLimitExceeded,
                counterOverflow.Status
            );
            Assert.Equal(10, context.GetScore(senderId));
            Assert.Equal(0, context.GetScore(recipientId));
        }

        [Fact]
        public async Task ReverseGiftsCompleteWithoutDeadlockAndConservePoints()
        {
            using var context = new MemoryGiftTestContext();
            var firstPlayerId = CreateIdentifier("player-a");
            var secondPlayerId = CreateIdentifier("player-b");
            await context.CreatePlayerAsync(firstPlayerId);
            await context.CreatePlayerAsync(secondPlayerId);
            context.SetScore(firstPlayerId, 100);
            context.SetScore(secondPlayerId, 100);

            var results = await Task.WhenAll(
                ExecuteGiftAsync(
                    context,
                    firstPlayerId,
                    secondPlayerId,
                    10,
                    Guid.NewGuid()
                ),
                ExecuteGiftAsync(
                    context,
                    secondPlayerId,
                    firstPlayerId,
                    20,
                    Guid.NewGuid()
                )
            ).WaitAsync(TimeSpan.FromSeconds(2));

            Assert.All(
                results,
                result => Assert.Equal(
                    GiftOperationStatus.Succeeded,
                    result.Status
                )
            );
            Assert.Equal(110, context.GetScore(firstPlayerId));
            Assert.Equal(90, context.GetScore(secondPlayerId));
            Assert.Equal(
                200,
                context.GetScore(firstPlayerId)
                    + context.GetScore(secondPlayerId)
            );
        }

        [Fact]
        public async Task ConcurrentGiftsFromOneSenderNeverCreateNegativeBalance()
        {
            using var context = new MemoryGiftTestContext();
            var senderId = CreateIdentifier("sender");
            var recipientId = CreateIdentifier("recipient");
            var sender = await context.CreatePlayerAsync(senderId);
            var recipient = await context.CreatePlayerAsync(recipientId);
            context.SetScore(senderId, 100);
            var tasks = Enumerable.Range(0, 200)
            .Select(_ => ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                1,
                Guid.NewGuid()
            ))
            .ToList();

            var results = await Task.WhenAll(tasks);

            Assert.Equal(
                100,
                results.Count(result =>
                    result.Status == GiftOperationStatus.Succeeded
                )
            );
            Assert.Equal(0, context.GetScore(senderId));
            Assert.Equal(100, context.GetScore(recipientId));
            Assert.Equal(100, sender.GiftsSent);
            Assert.Equal(100, recipient.GiftsReceived);
        }

        [Fact]
        public async Task ConcurrentScoreUpdatesAndGiftsUseTheSamePlayerLock()
        {
            using var context = new MemoryGiftTestContext();
            var senderId = CreateIdentifier("sender");
            var recipientId = CreateIdentifier("recipient");
            await context.CreatePlayerAsync(senderId);
            await context.CreatePlayerAsync(recipientId);
            context.SetScore(senderId, 100);
            var scoreTasks = Enumerable.Range(0, 100)
            .Select(_ => context.StatsProvider.AddScoreAsync(
                senderId,
                1,
                Guid.NewGuid()
            ))
            .Cast<Task>()
            .ToList();
            var giftTasks = Enumerable.Range(0, 50)
            .Select(_ => ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                1,
                Guid.NewGuid()
            ))
            .Cast<Task>()
            .ToList();

            await Task.WhenAll(scoreTasks.Concat(giftTasks));

            Assert.Equal(150, context.GetScore(senderId));
            Assert.Equal(50, context.GetScore(recipientId));
            Assert.Equal(
                200,
                context.GetScore(senderId) + context.GetScore(recipientId)
            );
        }

        [Fact]
        public async Task RandomOverlappingDuplicateBurstConservesEveryPoint()
        {
            const int playerCount = 16;
            const int giftCount = 1000;
            using var context = new MemoryGiftTestContext(
                giftRateLimitMaxRequests: giftCount
            );
            var playerIds = Enumerable.Range(0, playerCount)
            .Select(index => $"player-{index}")
            .ToList();

            foreach (var playerId in playerIds)
            {
                await context.CreatePlayerAsync(playerId);
                context.SetScore(playerId, 1000);
            }

            var random = new Random(1729);
            var operations = Enumerable.Range(0, giftCount)
            .Select(_ =>
            {
                var senderIndex = random.Next(playerCount);
                var recipientIndex = random.Next(playerCount - 1);

                if (recipientIndex >= senderIndex)
                    recipientIndex++;

                return new GiftOperation
                {
                    SenderPlayerId = playerIds[senderIndex],
                    RecipientPlayerId = playerIds[recipientIndex],
                    Points = 1,
                    RequestId = Guid.NewGuid()
                };
            })
            .ToList();
            var tasks = operations
            .SelectMany(operation => new[]
            {
                context.GiftProvider.ExecuteGiftAsync(operation),
                context.GiftProvider.ExecuteGiftAsync(operation)
            })
            .ToList();

            var results = await Task.WhenAll(tasks);

            for (var index = 0; index < results.Length; index += 2)
            {
                var first = results[index];
                var second = results[index + 1];

                Assert.Equal(GiftOperationStatus.Succeeded, first.Status);
                Assert.Equal(first.Status, second.Status);
                Assert.Equal(first.SenderScore, second.SenderScore);
                Assert.Equal(first.RecipientScore, second.RecipientScore);
                Assert.Single(
                    new[] { first, second },
                    result => result.Applied
                );
            }

            Assert.Equal(
                playerCount * 1000L,
                playerIds.Sum(context.GetScore)
            );
            Assert.All(
                playerIds,
                playerId => Assert.True(context.GetScore(playerId) >= 0)
            );
            Assert.Equal(
                giftCount,
                playerIds.Sum(playerId => context.PlayersById[playerId].GiftsSent)
            );
            Assert.Equal(
                giftCount,
                playerIds.Sum(playerId =>
                    context.PlayersById[playerId].GiftsReceived
                )
            );
        }

        [Fact]
        public async Task ExpiredMarkerRemainsDuplicateUntilCleanupRemovesIt()
        {
            using var context = new MemoryGiftTestContext();
            var senderId = CreateIdentifier("sender");
            var recipientId = CreateIdentifier("recipient");
            var sender = await context.CreatePlayerAsync(senderId);
            await context.CreatePlayerAsync(recipientId);
            context.SetScore(senderId, 100);
            var requestId = Guid.NewGuid();
            var first = await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                10,
                requestId
            );
            sender.GiftRequestsByRequestId[requestId] = CreateMarker(
                first,
                DateTime.UtcNow.AddMinutes(-1)
            );

            var duplicate = await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                10,
                requestId
            );
            var candidates = await context.GiftProvider
            .GetExpiredGiftRequestCandidatesAsync();
            var deleted = await context.GiftProvider
            .DeleteExpiredGiftRequestsAsync(candidates);
            var reapplied = await ExecuteGiftAsync(
                context,
                senderId,
                recipientId,
                10,
                requestId
            );

            Assert.False(duplicate.Applied);
            Assert.Equal(first.SenderScore, duplicate.SenderScore);
            Assert.Equal(1, deleted);
            Assert.True(reapplied.Applied);
            Assert.Equal(80, context.GetScore(senderId));
            Assert.Equal(20, context.GetScore(recipientId));
        }

        [Fact]
        public async Task CleanupRevalidatesChangedDuplicateAndMissingCandidates()
        {
            using var context = new MemoryGiftTestContext();
            var senderId = CreateIdentifier("sender");
            var sender = await context.CreatePlayerAsync(senderId);
            var expiredRequestId = Guid.NewGuid();
            var refreshedRequestId = Guid.NewGuid();
            sender.GiftRequestsByRequestId[expiredRequestId] = CreateMarker(
                GiftOperationStatus.RecipientOffline,
                expiredRequestId,
                DateTime.UtcNow.AddMinutes(-1)
            );
            sender.GiftRequestsByRequestId[refreshedRequestId] = CreateMarker(
                GiftOperationStatus.RecipientOffline,
                refreshedRequestId,
                DateTime.UtcNow.AddMinutes(-1)
            );
            var candidates = await context.GiftProvider
            .GetExpiredGiftRequestCandidatesAsync();
            sender.GiftRequestsByRequestId[refreshedRequestId] = CreateMarker(
                GiftOperationStatus.RecipientOffline,
                refreshedRequestId,
                DateTime.UtcNow.AddMinutes(1)
            );
            candidates.Add(new GiftRequestCleanupCandidate
            {
                SenderPlayerId = senderId,
                RequestId = expiredRequestId
            });
            candidates.Add(new GiftRequestCleanupCandidate
            {
                SenderPlayerId = senderId,
                RequestId = Guid.NewGuid()
            });

            var deleted = await context.GiftProvider
            .DeleteExpiredGiftRequestsAsync(candidates);

            Assert.Equal(1, deleted);
            Assert.False(sender.GiftRequestsByRequestId.ContainsKey(expiredRequestId));
            Assert.True(sender.GiftRequestsByRequestId.ContainsKey(refreshedRequestId));
        }

        [Fact]
        public async Task DisjointGiftCompletesWhileUnrelatedSenderLockIsHeld()
        {
            using var context = new MemoryGiftTestContext();
            var firstSenderId = CreateIdentifier("sender-a");
            var firstRecipientId = CreateIdentifier("recipient-a");
            var secondSenderId = CreateIdentifier("sender-b");
            var secondRecipientId = CreateIdentifier("recipient-b");
            var firstSender = await context.CreatePlayerAsync(firstSenderId);
            await context.CreatePlayerAsync(firstRecipientId);
            await context.CreatePlayerAsync(secondSenderId);
            await context.CreatePlayerAsync(secondRecipientId);
            context.SetScore(firstSenderId, 10);
            context.SetScore(secondSenderId, 10);
            await firstSender.PlayerLock.WaitAsync();
            Task<GiftOperationResult> blockedTask;

            try
            {
                blockedTask = ExecuteGiftAsync(
                    context,
                    firstSenderId,
                    firstRecipientId,
                    1,
                    Guid.NewGuid()
                );
                var disjointResult = await ExecuteGiftAsync(
                    context,
                    secondSenderId,
                    secondRecipientId,
                    1,
                    Guid.NewGuid()
                ).WaitAsync(TimeSpan.FromSeconds(1));

                Assert.False(blockedTask.IsCompleted);
                Assert.Equal(
                    GiftOperationStatus.Succeeded,
                    disjointResult.Status
                );
            }
            finally
            {
                firstSender.PlayerLock.Release();
            }

            var blockedResult = await blockedTask.WaitAsync(
                TimeSpan.FromSeconds(1)
            );

            Assert.Equal(GiftOperationStatus.Succeeded, blockedResult.Status);
        }

        [Fact]
        public async Task CancellationWhileWaitingForPlayerLockDoesNotMutate()
        {
            using var context = new MemoryGiftTestContext();
            var senderId = CreateIdentifier("sender");
            var recipientId = CreateIdentifier("recipient");
            var sender = await context.CreatePlayerAsync(senderId);
            await context.CreatePlayerAsync(recipientId);
            context.SetScore(senderId, 10);
            var requestId = Guid.NewGuid();
            await sender.PlayerLock.WaitAsync();
            using var cancellation = new CancellationTokenSource(
                TimeSpan.FromMilliseconds(100)
            );

            try
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    context.GiftProvider.ExecuteGiftAsync(
                        new GiftOperation
                        {
                            SenderPlayerId = senderId,
                            RecipientPlayerId = recipientId,
                            Points = 1,
                            RequestId = requestId
                        },
                        cancellation.Token
                    )
                );
            }
            finally
            {
                sender.PlayerLock.Release();
            }

            Assert.Equal(10, context.GetScore(senderId));
            Assert.Equal(0, context.GetScore(recipientId));
            Assert.False(sender.GiftRequestsByRequestId.ContainsKey(requestId));
        }

        private static Task<GiftOperationResult> ExecuteGiftAsync(
            MemoryGiftTestContext context,
            string senderId,
            string recipientId,
            int points,
            Guid requestId
        )
        {
            var task = context.GiftProvider.ExecuteGiftAsync(
                new GiftOperation
                {
                    SenderPlayerId = senderId,
                    RecipientPlayerId = recipientId,
                    Points = points,
                    RequestId = requestId
                }
            );

            return task;
        }

        private static MemoryGiftRequest CreateMarker(
            GiftOperationResult result,
            DateTime expiresAtUtc
        )
        {
            var marker = new MemoryGiftRequest
            {
                Status = result.Status,
                RequestId = result.RequestId,
                SenderScore = result.SenderScore,
                RecipientScore = result.RecipientScore,
                RetryAfter = result.RetryAfter,
                ExpiresAtUtc = expiresAtUtc
            };

            return marker;
        }

        private static MemoryGiftRequest CreateMarker(
            GiftOperationStatus status,
            Guid requestId,
            DateTime expiresAtUtc
        )
        {
            var marker = new MemoryGiftRequest
            {
                Status = status,
                RequestId = requestId,
                ExpiresAtUtc = expiresAtUtc
            };

            return marker;
        }

        private static string CreateIdentifier(string prefix)
        {
            var identifier = $"{prefix}-{Guid.NewGuid():N}";

            return identifier;
        }
    }
}
