namespace PlayerService.Shared.Models.PlayerGifts
{
    public class GiftOperation
    {
        public required string SenderPlayerId { get; set; }
        public required string RecipientPlayerId { get; set; }
        public required int Points { get; set; }
        public required Guid RequestId { get; set; }
    }
}
