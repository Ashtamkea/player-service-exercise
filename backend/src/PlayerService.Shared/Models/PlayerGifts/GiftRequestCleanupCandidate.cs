namespace PlayerService.Shared.Models.PlayerGifts
{
    public class GiftRequestCleanupCandidate
    {
        public required string SenderPlayerId { get; set; }
        public required Guid RequestId { get; set; }
    }
}
