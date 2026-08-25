using PlayerService.Shared.Models.PlayerGifts.Enums;

namespace PlayerService.Shared.Models.PlayerGifts
{
    public class GiftOperationResult
    {
        public required GiftOperationStatus Status { get; set; }
        public required Guid RequestId { get; set; }
        public long? SenderScore { get; set; }
        public long? RecipientScore { get; set; }
        public required bool Applied { get; set; }
        public TimeSpan? RetryAfter { get; set; }
    }
}
