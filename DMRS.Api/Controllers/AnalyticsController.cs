using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers
{
    [ApiController]
    [Route("analytics")]
    [Authorize(Policy = "FhirScope")]
    public sealed class AnalyticsController : ControllerBase
    {
        private readonly IFhirRepository _fhirRepository;
        private readonly ISmartAuthorizationService _authorizationService;

        public AnalyticsController(
            IFhirRepository fhirRepository,
            ISmartAuthorizationService authorizationService)
        {
            _fhirRepository = fhirRepository;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Returns totals for the dashboard metric tiles. For a system caller these are workspace-wide
        /// SQL COUNTs; for an org admin / practitioner / patient they are scoped to the patients that
        /// caller may see (see <see cref="ISmartAuthorizationService.ResolveAccessiblePatientIdsAsync"/>).
        /// </summary>
        [HttpGet("dashboard-summary")]
        public async Task<IActionResult> GetDashboardSummary([FromQuery] bool mine, CancellationToken cancellationToken)
        {
            var accessiblePatientIds = await _authorizationService.ResolveViewPatientIdsAsync(User, mine);

            if (accessiblePatientIds is null)
            {
                var patients = await _fhirRepository.CountByTypeAsync("Patient", cancellationToken);
                var encounters = await _fhirRepository.CountByTypeAsync("Encounter", cancellationToken);
                var conditions = await _fhirRepository.CountByTypeAsync("Condition", cancellationToken);
                var serviceRequests = await _fhirRepository.CountByTypeAsync("ServiceRequest", cancellationToken);
                var activeMedications = await _fhirRepository.SearchCountAsync<MedicationRequest>(
                    new Dictionary<string, string> { ["status"] = "active" });

                return Ok(new DashboardSummaryResponse(
                    patients, encounters, activeMedications, conditions, serviceRequests));
            }

            var patientIdSet = accessiblePatientIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (patientIdSet.Count == 0)
            {
                return Ok(new DashboardSummaryResponse(0, 0, 0, 0, 0));
            }

            // Per-patient index counts (no deserialization); sum over the accessible patient set.
            var encounterCounts = await _fhirRepository.CountByPatientAsync("Encounter", cancellationToken);
            var conditionCounts = await _fhirRepository.CountByPatientAsync("Condition", cancellationToken);
            var serviceRequestCounts = await _fhirRepository.CountByPatientAsync("ServiceRequest", cancellationToken);

            var scopedEncounters = SumOverPatients(encounterCounts, patientIdSet);
            var scopedConditions = SumOverPatients(conditionCounts, patientIdSet);
            var scopedServiceRequests = SumOverPatients(serviceRequestCounts, patientIdSet);

            // Active-med status is not in the index, so count from loaded active requests whose
            // subject is in scope (mirrors HighUtilizationRiskService's active/on-hold rule).
            var activeMeds = await _fhirRepository.SearchAsync<MedicationRequest>(
                new Dictionary<string, string> { ["status"] = "active" });
            var scopedActiveMedications = activeMeds.Count(m =>
            {
                var pid = ExtractPatientId(m.Subject?.Reference);
                return pid is not null && patientIdSet.Contains(pid);
            });

            return Ok(new DashboardSummaryResponse(
                patientIdSet.Count, scopedEncounters, scopedActiveMedications, scopedConditions, scopedServiceRequests));
        }

        /// <summary>
        /// Returns the top 10 most common conditions, scoped to the caller's accessible patients
        /// (workspace-wide for a system caller).
        /// </summary>
        [HttpGet("condition-prevalence")]
        public async Task<IActionResult> GetConditionPrevalence([FromQuery] bool mine, CancellationToken cancellationToken)
        {
            var accessiblePatientIds = await _authorizationService.ResolveViewPatientIdsAsync(User, mine);

            var allConditions = await _fhirRepository.SearchAsync<Condition>(
                new Dictionary<string, string>());

            IEnumerable<Condition> scopedConditions = allConditions;
            if (accessiblePatientIds is not null)
            {
                var patientIdSet = accessiblePatientIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
                scopedConditions = allConditions.Where(c =>
                {
                    var pid = ExtractPatientId(c.Subject?.Reference);
                    return pid is not null && patientIdSet.Contains(pid);
                });
            }

            var prevalence = scopedConditions
                .Select(c =>
                    c.Code?.Text
                    ?? c.Code?.Coding?.FirstOrDefault()?.Display
                    ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { name = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(10)
                .ToList();

            return Ok(prevalence);
        }

        private static int SumOverPatients(Dictionary<string, int> countsByPatient, HashSet<string> patientIds)
        {
            var total = 0;
            foreach (var patientId in patientIds)
            {
                total += countsByPatient.GetValueOrDefault(patientId);
            }

            return total;
        }

        private static string? ExtractPatientId(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            var slash = reference.IndexOf('/');
            return slash >= 0 && slash < reference.Length - 1 ? reference[(slash + 1)..] : reference;
        }
    }

    /// <summary>Workspace-wide totals for the dashboard metric tiles.</summary>
    public sealed record DashboardSummaryResponse(
        int Patients,
        int Encounters,
        int ActiveMedications,
        int Conditions,
        int ServiceRequests);
}
