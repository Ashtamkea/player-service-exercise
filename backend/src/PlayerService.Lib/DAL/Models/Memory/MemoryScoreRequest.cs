namespace PlayerService.Lib.DAL.Models.Memory
{
    public class MemoryScoreRequest
    {
        public required DateTime ExpiresAtUtc { get; init; }
    }
}
