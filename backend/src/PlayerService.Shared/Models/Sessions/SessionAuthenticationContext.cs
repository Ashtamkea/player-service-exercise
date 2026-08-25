namespace PlayerService.Shared.Models.Sessions
{
    public class SessionAuthenticationContext
    {
        public required string SessionId { get; set; }
        public required string PlayerId { get; set; }
        public required string DeviceId { get; set; }
    }
}
