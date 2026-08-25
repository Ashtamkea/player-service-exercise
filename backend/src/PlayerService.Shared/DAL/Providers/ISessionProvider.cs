using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Models.Sessions.Enums;

namespace PlayerService.Shared.DAL.Providers
{
    public interface ISessionProvider
    {
        Task<SessionCreationStatus> TryCreateSessionAsync(
            Session session,
            CancellationToken cancellationToken = default
        );

        Task<bool> IsPlayerOnlineAsync(
            string playerId,
            CancellationToken cancellationToken = default
        );

        Task<List<string>> GetExpiredSessionDeviceIdsAsync(
            CancellationToken cancellationToken = default
        );

        Task<int> DeleteExpiredSessionsAsync(
            List<string> deviceIds,
            CancellationToken cancellationToken = default
        );

        Task<SessionAuthenticationResult> AuthenticateAndExtendSessionAsync(
            string deviceId,
            string sessionId,
            CancellationToken cancellationToken = default
        );
    }
}
