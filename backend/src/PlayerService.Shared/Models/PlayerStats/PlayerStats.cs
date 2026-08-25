namespace PlayerService.Shared.Models.PlayerStats
{
    public class PlayerStats
    {
        public required long Score { get; set; }
        public required long GiftsSent { get; set; }
        public required long GiftsReceived { get; set; }
        public required DateTime LastActiveUtc { get; set; }
    }
}
