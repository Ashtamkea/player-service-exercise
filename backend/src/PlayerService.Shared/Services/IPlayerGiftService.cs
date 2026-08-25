using PlayerService.Shared.Models.PlayerGifts;

namespace PlayerService.Shared.Services
{
    public interface IPlayerGiftService
    {
        Task<GiftExecutionResult> GiftAsync(
            string senderPlayerId,
            string authenticatedPlayerId,
            GiftRequest request,
            CancellationToken cancellationToken = default
        );
    }
}
