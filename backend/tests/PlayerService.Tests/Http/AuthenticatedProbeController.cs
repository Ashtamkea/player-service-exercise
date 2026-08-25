using Microsoft.AspNetCore.Mvc;
using PlayerService.Shared.Configuration;
using PlayerService.Shared.Models.Sessions;

namespace PlayerService.Tests.Http
{
    [ApiController]
    [Route("test/protected")]
    public class AuthenticatedProbeController : ControllerBase
    {
        [HttpGet]
        public ActionResult<SessionAuthenticationContext> Get()
        {
            var sessionContext = HttpContext.Items[ConstantValues.SessionContextItemName]
                as SessionAuthenticationContext;

            if (sessionContext is null)
                return Unauthorized();

            return Ok(sessionContext);
        }

        [HttpGet("failure")]
        public ActionResult GetFailure()
        {
            return BadRequest();
        }
    }
}
