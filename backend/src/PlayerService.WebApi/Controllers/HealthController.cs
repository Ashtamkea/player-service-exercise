using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlayerService.Shared.Configuration;

namespace PlayerService.WebApi.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public ActionResult<object> Get()
        {
            var result = new
            {
                service = ConstantValues.ServiceName,
                status = "Healthy",
                timestampUtc = DateTimeOffset.UtcNow
            };

            return Ok(result);
        }
    }
}
