using PlayerService.Shared.Models.Sessions.Enums;

namespace PlayerService.Shared.Models.Sessions
{
    public class SessionAuthenticationResult
    {
        public required SessionAuthenticationStatus Status { get; set; }
        public SessionAuthenticationContext? Context { get; set; }
    }
}
