using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Models.Sessions.Enums;

namespace PlayerService.Shared.Repositories
{
    public interface ISessionRepository
    {
        Task<SessionCreationStatus> TryCreateSessionAsync(
            Session session,
            CancellationToken cancellationToken = default
        );

        Task<SessionAuthenticationResult> AuthenticateAndExtendSessionAsync(
            string deviceId,
            string sessionId,
            CancellationToken cancellationToken = default
        );
    }
}
