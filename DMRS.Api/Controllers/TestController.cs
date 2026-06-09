using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers
{
    // Internal diagnostics only — hidden from Swagger so it doesn't appear in the API surface during demos.
    [ApiController]
    [Route("api/test")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class TestController : ControllerBase
    {
        [HttpGet]
        [Authorize]
        public IActionResult Secure()
        {
            return Ok("You are authenticated 🎉");
        }

        [HttpGet("test-doctor")]
        [Authorize(Roles = "doctor")]
        public IActionResult TestDoctor()
        {
            return Ok("You are a doctor 🎉");
        }

        [HttpGet("debug-claims")]
        [Authorize]
        public IActionResult GetClaims()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            return Ok(claims);
        }

        [HttpGet("test-api")]
        public IActionResult TestApi() 
        {
            return Ok("the api is working");
        }
    }
}
