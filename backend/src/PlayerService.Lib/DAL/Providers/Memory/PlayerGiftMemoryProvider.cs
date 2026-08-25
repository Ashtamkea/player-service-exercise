using Exercise.Infra.Configuration;
using Exercise.Infra.Exceptions;
using Microsoft.Extensions.Configuration;
using PlayerService.Lib.DAL.Models.Memory;
using PlayerService.Lib.DAL.Sources;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.DAL.Providers;
using PlayerService.Shared.Models.PlayerGifts;
using PlayerService.Shared.Models.PlayerGifts.Enums;

namespace PlayerService.Lib.DAL.Providers.Memory
{
    public class PlayerGiftMemoryProvider : IPlayerGiftProvider
    {
        private readonly MemoryGameDataSource _gameDataSource;
        private readonly ISessionProvider _sessionProvider;
        private readonly TimeSpan _giftRequestTtl;
        private readonly TimeSpan _giftRateLimitWindow;
        private readonly int _giftRateLimitMaxRequests;

        public PlayerGiftMemoryProvider(
            MemoryGameDataSource gameDataSource,
            ISessionProvider sessionProvider,
            IConfiguration configuration
        )
        {
            _gameDataSource = gameDataSource;
            _sessionProvider = sessionProvider;
            _giftRequestTtl = TimeSpan.FromSeconds(
                configuration.GetSectionValue<int>(
                    ConfigurationKeys.PlayerServiceSection,
                    ConfigurationKeys.GiftRequestTtlInSeconds
                )
            );
            _giftRateLimitWindow = TimeSpan.FromSeconds(
                configuration.GetSectionValue<int>(
                    ConfigurationKeys.PlayerServiceSection,
                    ConfigurationKeys.GiftRateLimitWindowInSeconds
                )
            );
            _giftRateLimitMaxRequests = configuration.GetSectionValue<int>(
                ConfigurationKeys.PlayerServiceSection,
                ConfigurationKeys.GiftRateLimitMaxRequests
            );

            if (
                _giftRequestTtl <= TimeSpan.Zero
                || _giftRateLimitWindow <= TimeSpan.Zero
                || _giftRateLimitMaxRequests <= 0
            )
            {
                #region Exception

                throw ExceptionConstructor.CreateParameterized(
                    "Memory gift timings and rate limit must be greater than zero.",
                    new
                    {
                        GiftRequestTtl = _giftRequestTtl,
                        GiftRateLimitWindow = _giftRateLimitWindow,
                        GiftRateLimitMaxRequests = _giftRateLimitMaxRequests
                    }
                );

                #endregion
            }
        }

        public async Task<GiftOperationResult> ExecuteGiftAsync(
            GiftOperation operation,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sender = _gameDataSource.GetRequiredPlayer(
                operation.SenderPlayerId
            );
            MemoryPlayer recipient;
            await sender.PlayerLock.WaitAsync(cancellationToken);

            try
            {
                if (
                    sender.GiftRequestsByRequestId.TryGetValue(
                        operation.RequestId,
                        out var existingRequest
                    )
                )
                {
                    var duplicateResult = CreateResult(
                        existingRequest,
                        false
                    );

                    return duplicateResult;
                }

                if (
                    !_gameDataSource.PlayersById.TryGetValue(
                        operation.RecipientPlayerId,
                        out recipient!
                    )
                )
                {
                    var now = DateTime.UtcNow;

                    if (!TryConsumeRateLimit(sender, now, out var retryAfter))
                    {
                        var rateLimitedResult = StoreResult(
                            sender,
                            GiftOperationStatus.RateLimited,
                            operation.RequestId,
                            null,
                            null,
                            retryAfter,
                            false,
                            now
                        );

                        return rateLimitedResult;
                    }

                    var offlineResult = StoreResult(
                        sender,
                        GiftOperationStatus.RecipientOffline,
                        operation.RequestId,
                        null,
                        null,
                        null,
                        false,
                        now
                    );

                    return offlineResult;
                }
            }
            finally
            {
                sender.PlayerLock.Release();
            }

            var lockedPlayers = await _gameDataSource.AcquirePlayerLocksAsync(
                sender,
                recipient,
                cancellationToken
            );

            try
            {
                if (
                    sender.GiftRequestsByRequestId.TryGetValue(
                        operation.RequestId,
                        out var existingRequest
                    )
                )
                {
                    var duplicateResult = CreateResult(
                        existingRequest,
                        false
                    );

                    return duplicateResult;
                }

                var now = DateTime.UtcNow;

                if (!TryConsumeRateLimit(sender, now, out var retryAfter))
                {
                    var rateLimitedResult = StoreResult(
                        sender,
                        GiftOperationStatus.RateLimited,
                        operation.RequestId,
                        null,
                        null,
                        retryAfter,
                        false,
                        now
                    );

                    return rateLimitedResult;
                }

                var recipientOnline = await _sessionProvider.IsPlayerOnlineAsync(
                    operation.RecipientPlayerId,
                    cancellationToken
                );

                if (!recipientOnline)
                {
                    var offlineResult = StoreResult(
                        sender,
                        GiftOperationStatus.RecipientOffline,
                        operation.RequestId,
                        null,
                        null,
                        null,
                        false,
                        now
                    );

                    return offlineResult;
                }

                _gameDataSource.ScoresByPlayerId.TryGetValue(
                    operation.SenderPlayerId,
                    out var senderScore
                );
                _gameDataSource.ScoresByPlayerId.TryGetValue(
                    operation.RecipientPlayerId,
                    out var recipientScore
                );

                if (senderScore < operation.Points)
                {
                    var insufficientResult = StoreResult(
                        sender,
                        GiftOperationStatus.InsufficientPoints,
                        operation.RequestId,
                        senderScore,
                        null,
                        null,
                        false,
                        now
                    );

                    return insufficientResult;
                }

                long senderScoreAfter;
                long recipientScoreAfter;
                long giftsSentAfter;
                long giftsReceivedAfter;

                try
                {
                    senderScoreAfter = checked(
                        senderScore - operation.Points
                    );
                    recipientScoreAfter = checked(
                        recipientScore + operation.Points
                    );
                    giftsSentAfter = checked(sender.GiftsSent + 1);
                    giftsReceivedAfter = checked(recipient.GiftsReceived + 1);
                }
                catch (OverflowException)
                {
                    var limitResult = StoreResult(
                        sender,
                        GiftOperationStatus.RecipientScoreLimitExceeded,
                        operation.RequestId,
                        senderScore,
                        recipientScore,
                        null,
                        false,
                        now
                    );

                    return limitResult;
                }

                _gameDataSource.ScoreSnapshotGate.EnterReadLock();

                try
                {
                    _gameDataSource.ScoresByPlayerId[operation.SenderPlayerId] =
                        senderScoreAfter;
                    _gameDataSource.ScoresByPlayerId[operation.RecipientPlayerId] =
                        recipientScoreAfter;
                }
                finally
                {
                    _gameDataSource.ScoreSnapshotGate.ExitReadLock();
                }

                sender.GiftsSent = giftsSentAfter;
                recipient.GiftsReceived = giftsReceivedAfter;

                var result = StoreResult(
                    sender,
                    GiftOperationStatus.Succeeded,
                    operation.RequestId,
                    senderScoreAfter,
                    recipientScoreAfter,
                    null,
                    true,
                    now
                );

                return result;
            }
            finally
            {
                _gameDataSource.ReleasePlayerLocks(lockedPlayers);
            }
        }

        public Task<List<GiftRequestCleanupCandidate>> GetExpiredGiftRequestCandidatesAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = DateTime.UtcNow;
            var candidates = _gameDataSource.PlayersById.Values
            .SelectMany(player => player.GiftRequestsByRequestId
                .Where(entry => entry.Value.ExpiresAtUtc <= now)
                .Select(entry => new GiftRequestCleanupCandidate
                {
                    SenderPlayerId = player.PlayerId,
                    RequestId = entry.Key
                })
            )
            .ToList();
            return Task.FromResult(candidates);
        }

        public async Task<int> DeleteExpiredGiftRequestsAsync(
            List<GiftRequestCleanupCandidate> candidates,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deletedCount = 0;
            var candidatesBySender = candidates
            .GroupBy(candidate => candidate.SenderPlayerId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();

            foreach (var senderCandidates in candidatesBySender)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (
                    !_gameDataSource.PlayersById.TryGetValue(
                        senderCandidates.Key,
                        out var sender
                    )
                )
                    continue;

                await sender.PlayerLock.WaitAsync(cancellationToken);

                try
                {
                    var requestIds = senderCandidates
                    .Select(candidate => candidate.RequestId)
                    .Distinct()
                    .ToList();

                    foreach (var requestId in requestIds)
                    {
                        if (
                            !sender.GiftRequestsByRequestId.TryGetValue(
                                requestId,
                                out var request
                            )
                            || request.ExpiresAtUtc > DateTime.UtcNow
                        )
                            continue;

                        if (
                            sender.GiftRequestsByRequestId.TryRemove(
                                new KeyValuePair<Guid, MemoryGiftRequest>(
                                    requestId,
                                    request
                                )
                            )
                        )
                            deletedCount++;
                    }
                }
                finally
                {
                    sender.PlayerLock.Release();
                }
            }

            return deletedCount;
        }

        private bool TryConsumeRateLimit(
            MemoryPlayer sender,
            DateTime now,
            out TimeSpan? retryAfter
        )
        {
            var rateLimit = sender.GiftRateLimit;

            if (rateLimit is null || rateLimit.ExpiresAtUtc <= now)
            {
                sender.GiftRateLimit = new MemoryGiftRateLimit
                {
                    RequestCount = 1,
                    ExpiresAtUtc = now.Add(_giftRateLimitWindow)
                };
                retryAfter = null;

                return true;
            }

            if (rateLimit.RequestCount >= _giftRateLimitMaxRequests)
            {
                retryAfter = rateLimit.ExpiresAtUtc - now;

                return false;
            }

            rateLimit.RequestCount++;
            retryAfter = null;

            return true;
        }

        private GiftOperationResult StoreResult(
            MemoryPlayer sender,
            GiftOperationStatus status,
            Guid requestId,
            long? senderScore,
            long? recipientScore,
            TimeSpan? retryAfter,
            bool applied,
            DateTime now
        )
        {
            var request = new MemoryGiftRequest
            {
                Status = status,
                RequestId = requestId,
                SenderScore = senderScore,
                RecipientScore = recipientScore,
                RetryAfter = retryAfter,
                ExpiresAtUtc = now.Add(_giftRequestTtl)
            };
            sender.GiftRequestsByRequestId[requestId] = request;
            var result = CreateResult(request, applied);

            return result;
        }

        private static GiftOperationResult CreateResult(
            MemoryGiftRequest request,
            bool applied
        )
        {
            var result = new GiftOperationResult
            {
                Status = request.Status,
                RequestId = request.RequestId,
                SenderScore = request.SenderScore,
                RecipientScore = request.RecipientScore,
                RetryAfter = request.RetryAfter,
                Applied = applied
            };

            return result;
        }
    }
}
