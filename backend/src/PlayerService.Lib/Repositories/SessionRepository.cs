using PlayerService.Shared.DAL.Providers;
using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Models.Sessions.Enums;
using PlayerService.Shared.Repositories;

namespace PlayerService.Lib.Repositories
{
    public class SessionRepository : ISessionRepository
    {
        private readonly ISessionProvider _sessionProvider;

        public SessionRepository(ISessionProvider sessionProvider)
        {
            _sessionProvider = sessionProvider;
        }

        public Task<SessionCreationStatus> TryCreateSessionAsync(
            Session session,
            CancellationToken cancellationToken = default
        )
        {
            return _sessionProvider.TryCreateSessionAsync(
                session,
                cancellationToken
            );
        }

        public Task<SessionAuthenticationResult> AuthenticateAndExtendSessionAsync(
            string deviceId,
            string sessionId,
            CancellationToken cancellationToken = default
        )
        {
            return _sessionProvider.AuthenticateAndExtendSessionAsync(
                deviceId,
                sessionId,
                cancellationToken
            );
        }
    }
}
