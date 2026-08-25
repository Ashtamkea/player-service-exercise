namespace PlayerService.Shared.Models.Leaderboards
{
    public class LeaderboardEntry
    {
        public required string PlayerId { get; init; }
        public required long Score { get; init; }
    }
}
