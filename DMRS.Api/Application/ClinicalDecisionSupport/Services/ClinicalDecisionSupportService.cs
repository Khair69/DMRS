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
        private readonly ISessionFactory _sessionFactory;

        public ClinicalDecisionSupportService(
            IFhirRepository repository,
            IDrugKnowledgeService drugKnowledgeService,
            ISessionFactory sessionFactory)
        {
            _repository = repository;
            _drugKnowledgeService = drugKnowledgeService;
            _sessionFactory = sessionFactory;
        }

        public async Task<CdsEvaluationResult?> EvaluateMedicationRequestAsync(MedicationRequest request, CancellationToken cancellationToken = default)
        {
            var patientReference = request.Subject?.Reference;
            if (string.IsNullOrWhiteSpace(patientReference))
            {
                return null;
            }

            var medicationCodes = ExtractMedicationCodes(request);
            if (medicationCodes.Count == 0)
            {
                return null;
            }

            var allergies = await _repository.SearchAsync<AllergyIntolerance>(new Dictionary<string, string>
            {
                ["patient"] = patientReference
            });

            var allergyFacts = allergies
                .Select(a => new AllergyFact(
                    patientReference,
                    ExtractAllergyCodes(a),
                    a.Code?.Text))
                .Where(a => a.AllergyCodes.Count > 0)
                .ToList();

            var knowledge = await _drugKnowledgeService.FindByCodesAsync(medicationCodes, cancellationToken);

            var session = _sessionFactory.CreateSession();
            var collector = new CdsAlertCollector();
            session.Insert(collector);
            session.Insert(new MedicationOrderFact(patientReference, medicationCodes, request.Medication?.Concept?.Text));

            foreach (var allergy in allergyFacts)
            {
                session.Insert(allergy);
            }

            foreach (var item in knowledge)
            {
                session.Insert(item);
            }

            if (DoseCalculator.TryCalculateDailyDoseMg(request, out var dailyDoseMg))
            {
                session.Insert(new DoseFact(dailyDoseMg));
            }

            session.Fire();

            var alerts = collector.Alerts;
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
                    Code = OperationOutcome.IssueType.Processing,
                    Details = new CodeableConcept("urn:dmrs:cds", alert.Code, alert.Message)
                });
            }

            return new CdsEvaluationResult(alerts, outcome);
        }

        private static IReadOnlyCollection<string> ExtractMedicationCodes(MedicationRequest request)
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (request.Medication?.Concept != null)
            {
                if (!string.IsNullOrWhiteSpace(request.Medication.Concept.Text))
                {
                    codes.Add(request.Medication.Concept.Text.Trim().ToLowerInvariant());
                }

                foreach (var coding in request.Medication.Concept.Coding)
                {
                    if (!string.IsNullOrWhiteSpace(coding.Code))
                    {
                        codes.Add(coding.Code.Trim().ToLowerInvariant());
                    }
                }
            }

            return codes;
        }

        private static IReadOnlyCollection<string> ExtractAllergyCodes(AllergyIntolerance allergy)
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(allergy.Code?.Text))
            {
                codes.Add(allergy.Code.Text.Trim().ToLowerInvariant());
            }

            foreach (var coding in allergy.Code?.Coding ?? [])
            {
                if (!string.IsNullOrWhiteSpace(coding.Code))
                {
                    codes.Add(coding.Code.Trim().ToLowerInvariant());
                }
            }

            return codes;
        }
    }
}

