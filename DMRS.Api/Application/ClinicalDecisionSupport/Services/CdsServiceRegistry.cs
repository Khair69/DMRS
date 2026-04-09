using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class CdsServiceRegistry : ICdsServiceRegistry
    {
        private static readonly IReadOnlyList<CdsServiceDefinition> Services =
        [
            new CdsServiceDefinition(
                Id: "medication-prescribe",
                Hook: "medication-prescribe",
                Title: "Medication Prescribe",
                Description: "Evaluate medication prescribing context for CDS cards.")
        ];

        public IReadOnlyList<CdsServiceDefinition> ListServices() => Services;

        public CdsServiceDefinition? GetService(string id)
            => Services.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
