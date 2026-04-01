using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;
using NRules;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class ClinicalDecisionSupportService : IClinicalDecisionSupportService
    {
        private readonly IFhirRepository _repository;
        private readonly IDrugKnowledgeService _drugKnowledgeService;
        private readonly IDrugNormalizationService _drugNormalizationService;
        private readonly ISessionFactory _sessionFactory;

        public ClinicalDecisionSupportService(
            IFhirRepository repository,
            IDrugKnowledgeService drugKnowledgeService,
            IDrugNormalizationService drugNormalizationService,
            ISessionFactory sessionFactory)
        {
            _repository = repository;
            _drugKnowledgeService = drugKnowledgeService;
            _drugNormalizationService = drugNormalizationService;
            _sessionFactory = sessionFactory;
        }

        public async Task<CdsEvaluationResult?> EvaluateMedicationRequestAsync(MedicationRequest request, CancellationToken cancellationToken = default)
        {
            var patientReference = request.Subject?.Reference;
            if (string.IsNullOrWhiteSpace(patientReference))
            {
                return null;
            }

            var medicationConcepts = ExtractMedicationConcepts(request);
            if (medicationConcepts.Count == 0)
            {
                return null;
            }

            var allergies = await _repository.SearchAsync<AllergyIntolerance>(new Dictionary<string, string>
            {
                ["patient"] = patientReference
            });

            var warningAlerts = new List<CdsAlert>();
            var normalizedMedicationIds = await NormalizeConceptsAsync(medicationConcepts, warningAlerts, cancellationToken);
            if (normalizedMedicationIds.Count == 0)
            {
                return BuildWarningOnlyResult(warningAlerts);
            }

            var allergyFacts = new List<AllergyFact>();
            foreach (var allergy in allergies)
            {
                var allergyConcepts = ExtractAllergyConcepts(allergy);
                var normalizedAllergyIds = await NormalizeConceptsAsync(allergyConcepts, warningAlerts, cancellationToken);
                if (normalizedAllergyIds.Count == 0)
                {
                    continue;
                }

                allergyFacts.Add(new AllergyFact(patientReference, normalizedAllergyIds, allergy.Code?.Text));
            }

            var knowledge = await _drugKnowledgeService.FindByCodesAsync(normalizedMedicationIds, cancellationToken);

            var session = _sessionFactory.CreateSession();
            var collector = new CdsAlertCollector();
            session.Insert(collector);
            session.Insert(new MedicationOrderFact(patientReference, normalizedMedicationIds, request.Medication?.Concept?.Text));

            foreach (var allergy in allergyFacts)
            {
                session.Insert(allergy);
            }

            foreach (var item in knowledge)
            {
                session.Insert(item);
            }

            var medicationResource = await ResolveMedicationAsync(request);
            if (DoseCalculator.TryCalculateDailyDoseMg(request, medicationResource, out var dailyDoseMg))
            {
                session.Insert(new DoseFact(dailyDoseMg));
            }

            session.Fire();

            var alerts = collector.Alerts.Concat(warningAlerts).ToList();
            if (alerts.Count == 0)
            {
                return null;
            }

            var outcome = new OperationOutcome();
            foreach (var alert in alerts)
            {
                outcome.Issue.Add(new OperationOutcome.IssueComponent
                {
                    Severity = alert.Severity,
                    Code = alert.Severity == OperationOutcome.IssueSeverity.Warning
                        ? OperationOutcome.IssueType.Incomplete
                        : OperationOutcome.IssueType.Processing,
                    Details = new CodeableConcept("urn:dmrs:cds", alert.Code, alert.Message)
                });
            }

            return new CdsEvaluationResult(alerts, outcome);
        }

        private static IReadOnlyList<DrugConcept> ExtractMedicationConcepts(MedicationRequest request)
        {
            var concepts = new List<DrugConcept>();

            if (request.Medication?.Concept != null)
            {
                foreach (var coding in request.Medication.Concept.Coding)
                {
                    if (!string.IsNullOrWhiteSpace(coding.Code) || !string.IsNullOrWhiteSpace(coding.Display))
                    {
                        concepts.Add(new DrugConcept(coding.Code?.Trim(), coding.System?.Trim(), coding.Display?.Trim()));
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.Medication.Concept.Text))
                {
                    concepts.Add(new DrugConcept(null, "text", request.Medication.Concept.Text.Trim()));
                }
            }

            return concepts;
        }

        private static IReadOnlyList<DrugConcept> ExtractAllergyConcepts(AllergyIntolerance allergy)
        {
            var concepts = new List<DrugConcept>();
            if (allergy.Code != null)
            {
                foreach (var coding in allergy.Code.Coding)
                {
                    if (!string.IsNullOrWhiteSpace(coding.Code) || !string.IsNullOrWhiteSpace(coding.Display))
                    {
                        concepts.Add(new DrugConcept(coding.Code?.Trim(), coding.System?.Trim(), coding.Display?.Trim()));
                    }
                }

                if (!string.IsNullOrWhiteSpace(allergy.Code.Text))
                {
                    concepts.Add(new DrugConcept(null, "text", allergy.Code.Text.Trim()));
                }
            }

            return concepts;
        }

        private async Task<IReadOnlyList<string>> NormalizeConceptsAsync(
            IEnumerable<DrugConcept> concepts,
            List<CdsAlert> warnings,
            CancellationToken cancellationToken)
        {
            var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var concept in concepts)
            {
                var ids = await _drugNormalizationService.NormalizeAsync(concept, cancellationToken);
                foreach (var id in ids)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        normalized.Add(id);
                    }
                }
            }

            if (normalized.Count == 0)
            {
                warnings.Add(new CdsAlert(
                    "normalization-failed",
                    "Unable to normalize medication or allergy concepts to ingredient identifiers. CDS checks may be incomplete.",
                    OperationOutcome.IssueSeverity.Warning));
            }

            return normalized.ToList();
        }

        private static CdsEvaluationResult? BuildWarningOnlyResult(List<CdsAlert> warnings)
        {
            if (warnings.Count == 0)
            {
                return null;
            }

            var outcome = new OperationOutcome();
            foreach (var alert in warnings)
            {
                outcome.Issue.Add(new OperationOutcome.IssueComponent
                {
                    Severity = alert.Severity,
                    Code = OperationOutcome.IssueType.Incomplete,
                    Details = new CodeableConcept("urn:dmrs:cds", alert.Code, alert.Message)
                });
            }

            return new CdsEvaluationResult(warnings, outcome);
        }

        private async Task<Medication?> ResolveMedicationAsync(MedicationRequest request)
        {
            if (request.Medication?.Reference == null)
            {
                return null;
            }

            var reference = request.Medication.Reference.Reference;
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            var parts = reference.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !string.Equals(parts[0], "Medication", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return await _repository.GetAsync<Medication>(parts[1]);
        }
    }
}
