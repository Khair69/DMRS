using DMRS.Api.Application.ClinicalDecisionSupport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.ClinicalDecisionSupport
{
    [ApiController]
    [Route("cds/alerts")]
    [Authorize(Policy = "FhirScope")]
    public sealed class CdsAlertFeedController : ControllerBase
    {
        private readonly CdsAlertFeed _feed;

        public CdsAlertFeedController(CdsAlertFeed feed)
        {
            _feed = feed;
        }

        /// <summary>Returns the most recent CDS card fire events, newest first.</summary>
        [HttpGet]
        public IActionResult GetFeed([FromQuery] int count = 20)
            => Ok(_feed.GetRecent(Math.Clamp(count, 1, 50)));
    }
}
