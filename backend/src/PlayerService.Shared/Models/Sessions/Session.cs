namespace PlayerService.Shared.Models.Sessions
{
    public class Session
    {
        public required string SessionId { get; set; }
        public required string PlayerId { get; set; }
        public required string DeviceId { get; set; }
    }
}
