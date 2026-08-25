using Microsoft.AspNetCore.Mvc;
using PlayerService.Shared.Models.Leaderboards;
using PlayerService.Shared.Services;

namespace PlayerService.WebApi.Controllers
{
    [ApiController]
    [Route("leaderboard")]
    public class LeaderboardController : ControllerBase
    {
        private readonly ILeaderboardService _leaderboardService;

        public LeaderboardController(ILeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        [HttpGet]
        [ProducesResponseType<IReadOnlyList<LeaderboardEntry>>(
            StatusCodes.Status200OK
        )]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
        {
            var leaderboard = await _leaderboardService.GetLeaderboardAsync(
                cancellationToken
            );

            return Ok(leaderboard);
        }
    }
}
