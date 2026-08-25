using PlayerService.Shared.Models.PlayerGifts;

namespace PlayerService.Shared.Repositories
{
    public interface IPlayerGiftRepository
    {
        Task<GiftOperationResult> ExecuteGiftAsync(
            GiftOperation operation,
            CancellationToken cancellationToken = default
        );
    }
}
