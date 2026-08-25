namespace PlayerService.Shared.Models.PlayerStats
{
    public class ScoreRequestCleanupCandidate
    {
        public required string PlayerId { get; set; }
        public required Guid RequestId { get; set; }
    }
}
