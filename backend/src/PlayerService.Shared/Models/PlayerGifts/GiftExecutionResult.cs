namespace PlayerService.Shared.Models.PlayerGifts
{
    public class GiftExecutionResult
    {
        public required int StatusCode { get; set; }
        public required GiftResponse Response { get; set; }
    }
}
