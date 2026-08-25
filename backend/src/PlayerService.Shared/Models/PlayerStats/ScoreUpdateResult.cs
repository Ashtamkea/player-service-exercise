namespace PlayerService.Shared.Models.PlayerStats
{
    public class ScoreUpdateResult
    {
        public required PlayerStats Stats { get; set; }
        public required bool Applied { get; set; }
    }
}
