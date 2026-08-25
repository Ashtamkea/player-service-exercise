using Microsoft.AspNetCore.Mvc;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.PlayerStats;
using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Services;

namespace PlayerService.WebApi.Controllers
{
    [ApiController]
    [Route("players/{playerId}/stats")]
    public class PlayerStatsController : ControllerBase
    {
        private readonly IPlayerStatsService _playerStatsService;

        public PlayerStatsController(IPlayerStatsService playerStatsService)
        {
            _playerStatsService = playerStatsService;
        }

        [HttpGet]
        [ProducesResponseType<PlayerStats>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPlayerStatsAsync(
            string playerId,
            CancellationToken cancellationToken
        )
        {
            var playerStats = await _playerStatsService.GetPlayerStatsAsync(
                playerId,
                cancellationToken
            );

            return Ok(playerStats);
        }

        [HttpPost("score")]
        [ProducesResponseType<PlayerStats>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AddScoreAsync(
            string playerId,
            [FromBody] AddScoreRequest request,
            CancellationToken cancellationToken
        )
        {
            var sessionContext = (SessionAuthenticationContext)
                HttpContext.Items[ConstantValues.SessionContextItemName]!;
            var playerStats = await _playerStatsService.AddScoreAsync(
                playerId,
                sessionContext.PlayerId,
                request,
                cancellationToken
            );

            return Ok(playerStats);
        }
    }
}
