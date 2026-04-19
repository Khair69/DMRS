using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class MedicationRequestKnowledgeWarmup : IMedicationRequestKnowledgeWarmup
    {
        private const string RxNormSystem = "http://www.nlm.nih.gov/research/umls/rxnorm";

        private readonly IClinicalKnowledgeService _clinicalKnowledgeService;
        private readonly ILogger<MedicationRequestKnowledgeWarmup> _logger;

        public MedicationRequestKnowledgeWarmup(
            IClinicalKnowledgeService clinicalKnowledgeService,
            ILogger<MedicationRequestKnowledgeWarmup> logger)
        {
            _clinicalKnowledgeService = clinicalKnowledgeService;
            _logger = logger;
        }

        public async System.Threading.Tasks.Task WarmAsync(MedicationRequest medicationRequest, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(medicationRequest);

            var medicationCode = ExtractMedicationCode(medicationRequest);
            if (string.IsNullOrWhiteSpace(medicationCode))
            {
                return;
            }

            try
            {
                await _clinicalKnowledgeService.GetMaxDoseAsync(medicationCode, cancellationToken);
                await _clinicalKnowledgeService.GetMedicationIngredientsAsync(medicationCode, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to warm drug knowledge cache for MedicationRequest {MedicationRequestId} using medication code {MedicationCode}",
                    medicationRequest.Id,
                    medicationCode);
            }
        }

        private static string? ExtractMedicationCode(MedicationRequest medicationRequest)
        {
            var concept = medicationRequest.Medication?.Concept;
            var coding = concept?.Coding
                ?.FirstOrDefault(c =>
                    string.Equals(c.System, RxNormSystem, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(c.Code));

            if (!string.IsNullOrWhiteSpace(coding?.Code))
            {
                return coding.Code;
            }

            if (!string.IsNullOrWhiteSpace(concept?.Text))
            {
                return concept.Text.Trim();
            }

            return concept?.Coding
                ?.Select(c => c.Display?.Trim())
                .FirstOrDefault(display => !string.IsNullOrWhiteSpace(display));
        }
    }
}
