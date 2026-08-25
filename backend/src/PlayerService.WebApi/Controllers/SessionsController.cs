using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayerService.Shared.Models.Sessions;
using PlayerService.Shared.Services;

namespace PlayerService.WebApi.Controllers
{
    [ApiController]
    [Route("")]
    public class SessionsController : ControllerBase
    {
        private readonly ISessionService _sessionService;

        public SessionsController(ISessionService sessionService)
        {
            _sessionService = sessionService;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        [ProducesResponseType<string>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<string>> LoginAsync(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken
        )
        {
            var sessionId = await _sessionService.CreateSessionAsync(
                request.PlayerId,
                request.DeviceId,
                cancellationToken
            );

            return Ok(sessionId);
        }
    }
}
