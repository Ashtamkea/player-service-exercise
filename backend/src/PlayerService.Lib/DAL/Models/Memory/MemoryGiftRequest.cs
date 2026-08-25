using PlayerService.Shared.Models.PlayerGifts.Enums;

namespace PlayerService.Lib.DAL.Models.Memory
{
    public class MemoryGiftRequest
    {
        public required GiftOperationStatus Status { get; init; }
        public required Guid RequestId { get; init; }
        public long? SenderScore { get; init; }
        public long? RecipientScore { get; init; }
        public TimeSpan? RetryAfter { get; init; }
        public required DateTime ExpiresAtUtc { get; init; }
    }
}
