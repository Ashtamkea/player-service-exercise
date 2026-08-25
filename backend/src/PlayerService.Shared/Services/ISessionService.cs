using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Models.Sessions.Enums;

namespace PlayerService.Shared.Services
{
    public interface ISessionService
    {
        Task<string> CreateSessionAsync(
            string playerId,
            string deviceId,
            CancellationToken cancellationToken = default
        );

        Task<SessionAuthenticationResult> AuthenticateAndExtendSessionAsync(
            string deviceId,
            string sessionId,
            CancellationToken cancellationToken = default
        );
    }
}
