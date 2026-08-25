namespace PlayerService.Shared.Models.PlayerGifts
{
    public class GiftResponse
    {
        public required Guid RequestId { get; set; }
        public required string Outcome { get; set; }
        public long? SenderScore { get; set; }
        public long? RecipientScore { get; set; }
    }
}
