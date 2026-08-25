using PlayerService.Shared.Models.Leaderboards;
using PlayerService.Shared.Services;

namespace PlayerService.Tests.Leaderboards
{
    public class LeaderboardRefreshTestService : ILeaderboardService
    {
        private readonly int _refreshFailures;
        private int _refreshCount;

        public int RefreshCount => Volatile.Read(ref _refreshCount);
        public TaskCompletionSource<bool> FirstRefreshObserved { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        public TaskCompletionSource<bool> SecondRefreshObserved { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public LeaderboardRefreshTestService(int refreshFailures = 0)
        {
            _refreshFailures = refreshFailures;
        }

        public Task<bool> TryRefreshAsync(
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            var refreshCount = Interlocked.Increment(ref _refreshCount);

            if (refreshCount == 1)
                FirstRefreshObserved.TrySetResult(true);

            if (refreshCount >= 2)
                SecondRefreshObserved.TrySetResult(true);

            if (refreshCount <= _refreshFailures)
                throw new InvalidOperationException("Leaderboard refresh failed.");

            var task = Task.FromResult(true);

            return task;
        }

        public Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
            CancellationToken cancellationToken = default
        )
        {
            throw new NotImplementedException();
        }
    }
}
