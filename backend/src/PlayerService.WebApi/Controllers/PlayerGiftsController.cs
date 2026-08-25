using Microsoft.AspNetCore.Mvc;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.PlayerGifts;
using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Services;

namespace PlayerService.WebApi.Controllers
{
    [ApiController]
    [Route("players/{playerId}/gifts")]
    public class PlayerGiftsController : ControllerBase
    {
        private readonly IPlayerGiftService _playerGiftService;

        public PlayerGiftsController(IPlayerGiftService playerGiftService)
        {
            _playerGiftService = playerGiftService;
        }

        [HttpPost]
        [ProducesResponseType<GiftResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<GiftResponse>(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GiftAsync(
            string playerId,
            [FromBody] GiftRequest request,
            CancellationToken cancellationToken
        )
        {
            var sessionContext = (SessionAuthenticationContext)
                HttpContext.Items[ConstantValues.SessionContextItemName]!;
            var result = await _playerGiftService.GiftAsync(
                playerId,
                sessionContext.PlayerId,
                request,
                cancellationToken
            );

            return StatusCode(result.StatusCode, result.Response);
        }
    }
}
