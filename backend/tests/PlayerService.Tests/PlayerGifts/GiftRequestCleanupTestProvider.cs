using PlayerService.Shared.DAL.Providers;
using PlayerService.Shared.Models.PlayerGifts;

namespace PlayerService.Tests.PlayerGifts
{
    public class GiftRequestCleanupTestProvider : IPlayerGiftProvider
    {
        private readonly List<GiftRequestCleanupCandidate> _candidates;
        private readonly int _discoveryFailures;
        private int _deletionCount;
        private int _discoveryCount;

        public int DeletionCount => Volatile.Read(ref _deletionCount);
        public int DiscoveryCount => Volatile.Read(ref _discoveryCount);
        public TaskCompletionSource<bool> DiscoveryObserved { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        public TaskCompletionSource<List<GiftRequestCleanupCandidate>> DeletionObserved { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public GiftRequestCleanupTestProvider(
            List<GiftRequestCleanupCandidate> candidates,
            int discoveryFailures = 0
        )
        {
            _candidates = candidates;
            _discoveryFailures = discoveryFailures;
        }

        public Task<GiftOperationResult> ExecuteGiftAsync(
            GiftOperation operation,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotImplementedException();
        }

        public Task<List<GiftRequestCleanupCandidate>> GetExpiredGiftRequestCandidatesAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var discoveryCount = Interlocked.Increment(ref _discoveryCount);
            DiscoveryObserved.TrySetResult(true);

            if (discoveryCount <= _discoveryFailures)
                throw new InvalidOperationException("Cleanup discovery failed.");

            var candidates = _candidates
            .Select(candidate => new GiftRequestCleanupCandidate
            {
                SenderPlayerId = candidate.SenderPlayerId,
                RequestId = candidate.RequestId
            })
            .ToList();
            var task = Task.FromResult(candidates);

            return task;
        }

        public Task<int> DeleteExpiredGiftRequestsAsync(
            List<GiftRequestCleanupCandidate> candidates,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            Interlocked.Increment(ref _deletionCount);
            var deletedCandidates = candidates
            .Select(candidate => new GiftRequestCleanupCandidate
            {
                SenderPlayerId = candidate.SenderPlayerId,
                RequestId = candidate.RequestId
            })
            .ToList();
            DeletionObserved.TrySetResult(deletedCandidates);
            var task = Task.FromResult(deletedCandidates.Count);

            return task;
        }
    }
}
