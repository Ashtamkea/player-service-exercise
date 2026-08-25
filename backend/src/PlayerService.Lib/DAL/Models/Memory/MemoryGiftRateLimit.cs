namespace PlayerService.Lib.DAL.Models.Memory
{
    public class MemoryGiftRateLimit
    {
        public required int RequestCount { get; set; }
        public required DateTime ExpiresAtUtc { get; init; }
    }
}
