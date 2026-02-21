using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers
{
    [ApiController]
    [Route("api/test")]
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
