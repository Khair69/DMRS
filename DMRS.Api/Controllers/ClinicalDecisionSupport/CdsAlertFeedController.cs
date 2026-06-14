using DMRS.Api.Application.ClinicalDecisionSupport.Services;
using DMRS.Api.Infrastructure.Security;
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
        private readonly ISmartAuthorizationService _authorizationService;

        public CdsAlertFeedController(CdsAlertFeed feed, ISmartAuthorizationService authorizationService)
        {
            _feed = feed;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Returns the most recent CDS card fire events, newest first — scoped to the caller's
        /// accessible patients (workspace-wide for a system caller).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetFeed([FromQuery] int count = 20)
        {
            var take = Math.Clamp(count, 1, 50);
            var accessiblePatientIds = await _authorizationService.ResolveAccessiblePatientIdsAsync(User);

            if (accessiblePatientIds is null)
            {
                return Ok(_feed.GetRecent(take));
            }

            var patientIdSet = accessiblePatientIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (patientIdSet.Count == 0)
            {
                return Ok(Array.Empty<CdsAlertEvent>());
            }

            // Filter the full buffer first, then take, so scoping never starves the result of
            // accessible events that sit behind out-of-scope ones.
            var scoped = _feed.GetRecent(100)
                .Where(e => patientIdSet.Contains(NormalizePatientId(e.PatientId)))
                .Take(take)
                .ToList();

            return Ok(scoped);
        }

        private static string NormalizePatientId(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return string.Empty;
            }

            const string prefix = "Patient/";
            return patientId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? patientId[prefix.Length..]
                : patientId;
        }
    }
}
