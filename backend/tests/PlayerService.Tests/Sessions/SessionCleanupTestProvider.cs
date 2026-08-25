using PlayerService.Shared.DAL.Providers;
using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Models.Sessions.Enums;

namespace PlayerService.Tests.Sessions
{
    public class SessionCleanupTestProvider : ISessionProvider
    {
        private readonly int _discoveryFailures;
        private readonly List<string> _expiredDeviceIds;
        private int _deletionCount;
        private int _discoveryCount;

        public int DeletionCount => Volatile.Read(ref _deletionCount);
        public int DiscoveryCount => Volatile.Read(ref _discoveryCount);
        public TaskCompletionSource<bool> DiscoveryObserved { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        public TaskCompletionSource<List<string>> DeletionObserved { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public SessionCleanupTestProvider(
            List<string> expiredDeviceIds,
            int discoveryFailures = 0
        )
        {
            _expiredDeviceIds = expiredDeviceIds;
            _discoveryFailures = discoveryFailures;
        }

        public Task<SessionCreationStatus> TryCreateSessionAsync(
            Session session,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsPlayerOnlineAsync(
            string playerId,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotImplementedException();
        }

        public Task<List<string>> GetExpiredSessionDeviceIdsAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var discoveryCount = Interlocked.Increment(ref _discoveryCount);
            DiscoveryObserved.TrySetResult(true);

            if (discoveryCount <= _discoveryFailures)
                throw new InvalidOperationException("Cleanup discovery failed.");

            var deviceIds = _expiredDeviceIds.ToList();
            var task = Task.FromResult(deviceIds);

            return task;
        }

        public Task<int> DeleteExpiredSessionsAsync(
            List<string> deviceIds,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            Interlocked.Increment(ref _deletionCount);
            var deletedDeviceIds = deviceIds.ToList();
            DeletionObserved.TrySetResult(deletedDeviceIds);
            var task = Task.FromResult(deletedDeviceIds.Count);

            return task;
        }

        public Task<SessionAuthenticationResult> AuthenticateAndExtendSessionAsync(
            string deviceId,
            string sessionId,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotImplementedException();
        }
    }
}
