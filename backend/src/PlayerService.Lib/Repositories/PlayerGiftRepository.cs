using PlayerService.Shared.DAL.Providers;
using PlayerService.Shared.Models.PlayerGifts;
using PlayerService.Shared.Repositories;

namespace PlayerService.Lib.Repositories
{
    public class PlayerGiftRepository : IPlayerGiftRepository
    {
        private readonly IPlayerGiftProvider _playerGiftProvider;

        public PlayerGiftRepository(IPlayerGiftProvider playerGiftProvider)
        {
            _playerGiftProvider = playerGiftProvider;
        }

        public Task<GiftOperationResult> ExecuteGiftAsync(
            GiftOperation operation,
            CancellationToken cancellationToken = default
        )
        {
            return _playerGiftProvider.ExecuteGiftAsync(
                operation,
                cancellationToken
            );
        }
    }
}
