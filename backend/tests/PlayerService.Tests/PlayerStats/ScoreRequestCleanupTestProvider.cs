using PlayerService.Shared.DAL.Providers;
using PlayerService.Shared.Models.PlayerStats;

namespace PlayerService.Tests.PlayerStats
{
    public class ScoreRequestCleanupTestProvider : IPlayerStatsProvider
    {
        private readonly List<ScoreRequestCleanupCandidate> _candidates;
        private readonly int _discoveryFailures;
        private int _deletionCount;
        private int _discoveryCount;

        public int DeletionCount => Volatile.Read(ref _deletionCount);
        public int DiscoveryCount => Volatile.Read(ref _discoveryCount);
        public TaskCompletionSource<bool> DiscoveryObserved { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        public TaskCompletionSource<List<ScoreRequestCleanupCandidate>> DeletionObserved { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public ScoreRequestCleanupTestProvider(
            List<ScoreRequestCleanupCandidate> candidates,
            int discoveryFailures = 0
        )
        {
            _candidates = candidates;
            _discoveryFailures = discoveryFailures;
        }

        public Task<ScoreUpdateResult> AddScoreAsync(
            string playerId,
            int points,
            Guid requestId,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotImplementedException();
        }

        public Task<PlayerService.Shared.Models.PlayerStats.PlayerStats?> GetPlayerStatsAsync(
            string playerId,
            CancellationToken cancellationToken = default
        )
        {
            throw new NotImplementedException();
        }

        public Task<List<ScoreRequestCleanupCandidate>> GetExpiredScoreRequestCandidatesAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var discoveryCount = Interlocked.Increment(ref _discoveryCount);
            DiscoveryObserved.TrySetResult(true);

            if (discoveryCount <= _discoveryFailures)
                throw new InvalidOperationException("Cleanup discovery failed.");

            var candidates = _candidates
            .Select(candidate => new ScoreRequestCleanupCandidate
            {
                PlayerId = candidate.PlayerId,
                RequestId = candidate.RequestId
            })
            .ToList();
            var task = Task.FromResult(candidates);

            return task;
        }

        public Task<int> DeleteExpiredScoreRequestsAsync(
            List<ScoreRequestCleanupCandidate> candidates,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            Interlocked.Increment(ref _deletionCount);
            var deletedCandidates = candidates
            .Select(candidate => new ScoreRequestCleanupCandidate
            {
                PlayerId = candidate.PlayerId,
                RequestId = candidate.RequestId
            })
            .ToList();
            DeletionObserved.TrySetResult(deletedCandidates);
            var task = Task.FromResult(deletedCandidates.Count);

            return task;
        }
    }
}
