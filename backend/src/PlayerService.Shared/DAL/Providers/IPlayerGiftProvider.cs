using PlayerService.Shared.Models.PlayerGifts;

namespace PlayerService.Shared.DAL.Providers
{
    public interface IPlayerGiftProvider
    {
        Task<GiftOperationResult> ExecuteGiftAsync(
            GiftOperation operation,
            CancellationToken cancellationToken = default
        );

        Task<List<GiftRequestCleanupCandidate>> GetExpiredGiftRequestCandidatesAsync(
            CancellationToken cancellationToken = default
        );

        Task<int> DeleteExpiredGiftRequestsAsync(
            List<GiftRequestCleanupCandidate> candidates,
            CancellationToken cancellationToken = default
        );
    }
}
