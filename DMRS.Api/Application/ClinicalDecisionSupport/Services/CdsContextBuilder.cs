using System.Text.Json;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class CdsContextBuilder : ICdsContextBuilder
    {
        private const string RxNormSystem = "http://www.nlm.nih.gov/research/umls/rxnorm";

        private static readonly HashSet<string> ActiveMedicationStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "active",
            "on-hold"
        };

        private static readonly HashSet<string> ActiveConditionClinicalStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "active",
            "recurrence",
            "relapse"
        };

        private readonly IClinicalKnowledgeService _clinicalKnowledgeService;
        private readonly IFhirRepository _fhirRepository;
        private readonly IHighUtilizationRiskService _highUtilizationRiskService;

        public CdsContextBuilder(
            IClinicalKnowledgeService clinicalKnowledgeService,
            IFhirRepository fhirRepository,
            IHighUtilizationRiskService highUtilizationRiskService)
        {
            _clinicalKnowledgeService = clinicalKnowledgeService;
            _fhirRepository = fhirRepository;
            _highUtilizationRiskService = highUtilizationRiskService;
        }

        public async System.Threading.Tasks.Task<CdsContext> BuildAsync(CdsHookRequest request, CancellationToken cancellationToken)
        {
            var patientId = ExtractPatientId(request.Context);
            var data = new Dictionary<string, object?>
            {
                ["hook"] = request.Hook,
                ["hookInstance"] = request.HookInstance,
                ["patientId"] = patientId,
                ["context"] = request.Context,
                ["prefetch"] = request.Prefetch
            };

            if (string.Equals(request.Hook, "medication-prescribe", StringComparison.OrdinalIgnoreCase))
            {
                await EnrichMedicationContextAsync(data, request, patientId, cancellationToken);
            }
            else if (string.Equals(request.Hook, "patient-view", StringComparison.OrdinalIgnoreCase))
            {
                await EnrichPatientViewContextAsync(data, patientId, cancellationToken);
            }

            return CdsContext.Create(
                request.Hook,
                request.HookInstance,
                patientId,
                data,
                request.Context,
                request.Prefetch);
        }

        private async System.Threading.Tasks.Task EnrichMedicationContextAsync(
            IDictionary<string, object?> data,
            CdsHookRequest request,
            string? patientId,
            CancellationToken cancellationToken)
        {
            var medicationInput = ExtractMedicationInput(request.Context, request.Prefetch);
            await EnrichPatientContextAsync(data, patientId);
            data["conditions"] = await BuildConditionContextAsync(patientId);
            data["ai"] = await BuildAiContextAsync(patientId, cancellationToken);

            if (medicationInput == null)
            {
                data["therapy"] = CreateEmptyTherapyContext();
                return;
            }

            MedicineKnowledge? knowledge = null;
            var lookupCode = !string.IsNullOrWhiteSpace(medicationInput.RxCui)
                ? medicationInput.RxCui
                : medicationInput.Name;

            if (!string.IsNullOrWhiteSpace(lookupCode))
            {
                knowledge = await _clinicalKnowledgeService.GetMedicationKnowledgeAsync(lookupCode, cancellationToken);
            }

            var allergyCodes = await GetPatientAllergyCodesAsync(patientId, cancellationToken);
            var ingredientCodes = knowledge?.Ingredients
                .Select(ingredient => ingredient.Code)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];

            var matches = ingredientCodes
                .Where(code => allergyCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            data["medication"] = new Dictionary<string, object?>
            {
                ["rxCui"] = knowledge?.RxCui ?? medicationInput.RxCui,
                ["name"] = knowledge?.Name ?? medicationInput.Name,
                ["ingredients"] = ingredientCodes,
                ["ingredientNames"] = knowledge?.Ingredients
                    .Select(ingredient => ingredient.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray() ?? [],
                ["indications"] = knowledge?.Indications ?? []
            };

            data["dose"] = new Dictionary<string, object?>
            {
                ["requestedSingleMg"] = medicationInput.RequestedSingleMg,
                ["requestedDailyMg"] = medicationInput.RequestedDailyMg,
                ["maxDailyMg"] = knowledge?.MaxDailyMg,
                ["maxSingleMg"] = knowledge?.MaxSingleMg,
                ["warningThresholdMg"] = knowledge?.WarningThresholdMg
            };

            data["safety"] = new Dictionary<string, object?>
            {
                ["pregnancyCategory"] = knowledge?.PregnancyCategory,
                ["isControlled"] = knowledge?.IsControlled,
                ["allergyConflict"] = matches.Length > 0
            };

            data["allergies"] = new Dictionary<string, object?>
            {
                ["codes"] = allergyCodes,
                ["matches"] = matches
            };

            data["therapy"] = await BuildActiveTherapyContextAsync(
                patientId,
                medicationInput,
                ingredientCodes,
                cancellationToken);
        }

        private async System.Threading.Tasks.Task EnrichPatientViewContextAsync(
            IDictionary<string, object?> data,
            string? patientId,
            CancellationToken cancellationToken)
        {
            await EnrichPatientContextAsync(data, patientId);
            data["conditions"] = await BuildConditionContextAsync(patientId);
            data["ai"] = await BuildAiContextAsync(patientId, cancellationToken);

            // Medication count for polypharmacy detection on patient view
            if (!string.IsNullOrWhiteSpace(patientId))
            {
                var patientRef = NormalizePatientReference(patientId);
                var allMeds = await _fhirRepository.SearchAsync<MedicationRequest>(new Dictionary<string, string>
                {
                    ["patient"] = patientRef
                });
                var activeCount = allMeds.Count(IsActiveMedicationRequest);
                data["therapy"] = new Dictionary<string, object?>
                {
                    ["activeMedicationCount"] = activeCount,
                    ["activeMedicationRxCuis"] = Array.Empty<string>(),
                    ["activeMedicationNames"] = Array.Empty<string>(),
                    ["activeIngredientCodes"] = Array.Empty<string>(),
                    ["duplicateIngredientMatches"] = Array.Empty<string>(),
                    ["duplicateIngredientConflict"] = false
                };
            }
            else
            {
                data["therapy"] = CreateEmptyTherapyContext();
            }
        }

        private async System.Threading.Tasks.Task EnrichPatientContextAsync(
            IDictionary<string, object?> data,
            string? patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                data["patient"] = new Dictionary<string, object?>();
                return;
            }

            var normalizedPatientId = patientId.StartsWith("Patient/", StringComparison.OrdinalIgnoreCase)
                ? patientId["Patient/".Length..]
                : patientId;

            var patient = await _fhirRepository.GetAsync<Patient>(normalizedPatientId);
            if (patient == null)
            {
                data["patient"] = new Dictionary<string, object?>();
                return;
            }

            data["patient"] = new Dictionary<string, object?>
            {
                ["id"] = normalizedPatientId,
                ["gender"] = patient.Gender?.ToString().ToLowerInvariant(),
                ["birthDate"] = patient.BirthDate,
                ["ageYears"] = CalculateAgeYears(patient.BirthDate)
            };
        }

        private async System.Threading.Tasks.Task<Dictionary<string, object?>> BuildConditionContextAsync(string? patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return new Dictionary<string, object?>
                {
                    ["codes"] = Array.Empty<string>(),
                    ["texts"] = Array.Empty<string>()
                };
            }

            var patientRef = NormalizePatientReference(patientId);
            var conditions = await _fhirRepository.SearchAsync<Condition>(new Dictionary<string, string>
            {
                ["patient"] = patientRef
            });

            var activeConditions = conditions
                .Where(IsActiveCondition)
                .ToArray();

            return new Dictionary<string, object?>
            {
                ["codes"] = activeConditions
                    .SelectMany(condition => condition.Code?.Coding?.Select(coding => coding.Code) ?? [])
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()!,
                ["texts"] = activeConditions
                    .Select(condition => condition.Code?.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()!
            };
        }

        private async System.Threading.Tasks.Task<Dictionary<string, object?>> BuildActiveTherapyContextAsync(
            string? patientId,
            MedicationInput currentMedication,
            IReadOnlyList<string> currentIngredientCodes,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return CreateEmptyTherapyContext();
            }

            var patientRef = NormalizePatientReference(patientId);
            var medicationRequests = await _fhirRepository.SearchAsync<MedicationRequest>(new Dictionary<string, string>
            {
                ["patient"] = patientRef
            });

            var activeMedicationRequests = medicationRequests
                .Where(IsActiveMedicationRequest)
                .ToArray();

            var activeRxCuis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeIngredientCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var medicationRequest in activeMedicationRequests)
            {
                if (IsSameMedicationRequest(medicationRequest, currentMedication))
                {
                    continue;
                }

                var medicationCode = ExtractMedicationCode(medicationRequest);
                if (string.IsNullOrWhiteSpace(medicationCode))
                {
                    continue;
                }

                var knowledge = await _clinicalKnowledgeService.GetMedicationKnowledgeAsync(medicationCode, cancellationToken);
                if (!string.IsNullOrWhiteSpace(knowledge?.RxCui))
                {
                    activeRxCuis.Add(knowledge.RxCui);
                }
                else if (medicationCode.All(char.IsDigit))
                {
                    activeRxCuis.Add(medicationCode);
                }

                if (!string.IsNullOrWhiteSpace(knowledge?.Name))
                {
                    activeNames.Add(knowledge.Name);
                }
                else if (!string.IsNullOrWhiteSpace(medicationRequest.Medication?.Concept?.Text))
                {
                    activeNames.Add(medicationRequest.Medication.Concept.Text.Trim());
                }

                foreach (var ingredientCode in knowledge?.Ingredients.Select(ingredient => ingredient.Code) ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(ingredientCode))
                    {
                        activeIngredientCodes.Add(ingredientCode);
                    }
                }
            }

            var duplicateIngredientMatches = currentIngredientCodes
                .Where(code => activeIngredientCodes.Contains(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new Dictionary<string, object?>
            {
                ["activeMedicationCount"] = activeMedicationRequests.Length,
                ["activeMedicationRxCuis"] = activeRxCuis.ToArray(),
                ["activeMedicationNames"] = activeNames.ToArray(),
                ["activeIngredientCodes"] = activeIngredientCodes.ToArray(),
                ["duplicateIngredientMatches"] = duplicateIngredientMatches,
                ["duplicateIngredientConflict"] = duplicateIngredientMatches.Length > 0
            };
        }

        private static Dictionary<string, object?> CreateEmptyTherapyContext()
            => new()
            {
                ["activeMedicationCount"] = 0,
                ["activeMedicationRxCuis"] = Array.Empty<string>(),
                ["activeMedicationNames"] = Array.Empty<string>(),
                ["activeIngredientCodes"] = Array.Empty<string>(),
                ["duplicateIngredientMatches"] = Array.Empty<string>(),
                ["duplicateIngredientConflict"] = false
            };

        private async System.Threading.Tasks.Task<Dictionary<string, object?>> BuildAiContextAsync(
            string? patientId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return CreateEmptyAiContext();
            }

            var assessment = await _highUtilizationRiskService.AssessPatientAsync(patientId, cancellationToken);
            if (assessment == null)
            {
                return CreateEmptyAiContext();
            }

            return new Dictionary<string, object?>
            {
                ["highUtilizationRisk"] = assessment.IsHighRisk,
                ["highUtilizationProbability"] = assessment.Probability,
                ["highUtilizationModel"] = assessment.ModelName,
                ["highUtilizationEvaluatedAt"] = assessment.EvaluatedAt,
                ["highUtilizationFeaturesComplete"] = assessment.FeaturesComplete,
                ["highUtilizationAge"] = assessment.Age,
                ["highUtilizationGender"] = assessment.Gender,
                ["compositeScore"] = assessment.CompositeScore,
                ["riskLevel"] = assessment.RiskLevel,
                ["hasChronicConditions"] = assessment.HasChronicConditions,
                ["conditionCount"] = assessment.ConditionCount,
                ["medicationCount"] = assessment.MedicationCount
            };
        }

        private static Dictionary<string, object?> CreateEmptyAiContext()
            => new()
            {
                ["highUtilizationRisk"] = false,
                ["highUtilizationProbability"] = null,
                ["highUtilizationModel"] = null,
                ["highUtilizationEvaluatedAt"] = null,
                ["highUtilizationFeaturesComplete"] = false,
                ["highUtilizationAge"] = null,
                ["highUtilizationGender"] = null,
                ["compositeScore"] = 0f,
                ["riskLevel"] = "Unknown",
                ["hasChronicConditions"] = false,
                ["conditionCount"] = 0,
                ["medicationCount"] = 0
            };

        private async System.Threading.Tasks.Task<IReadOnlyList<string>> GetPatientAllergyCodesAsync(string? patientId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return [];
            }

            var allergies = await _fhirRepository.SearchAsync<AllergyIntolerance>(new Dictionary<string, string>
            {
                ["patient"] = NormalizePatientReference(patientId)
            });

            return allergies
                .SelectMany(allergy => (allergy.Code?.Coding ?? [])
                    .Select(coding => coding.Code)
                    .Concat(string.IsNullOrWhiteSpace(allergy.Code?.Text) ? [] : [allergy.Code.Text]))
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()!;
        }

        private static MedicationInput? ExtractMedicationInput(JsonElement context, JsonElement? prefetch)
        {
            if (TryGetMedicationRequest(context, out var medicationRequest))
            {
                return BuildMedicationInputFromMedicationRequest(medicationRequest);
            }

            if (prefetch.HasValue && TryGetMedicationRequest(prefetch.Value, out medicationRequest))
            {
                return BuildMedicationInputFromMedicationRequest(medicationRequest);
            }

            if (TryGetPropertyCaseInsensitive(context, "medication", out var medicationElement))
            {
                return BuildMedicationInputFromMedicationElement(medicationElement);
            }

            return null;
        }

        private static bool TryGetMedicationRequest(JsonElement source, out JsonElement medicationRequest)
        {
            medicationRequest = default;

            if (source.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (TryGetPropertyCaseInsensitive(source, "medicationRequest", out medicationRequest))
            {
                return true;
            }

            if (TryGetPropertyCaseInsensitive(source, "draftOrders", out var draftOrders)
                && TryFindMedicationRequestInBundle(draftOrders, out medicationRequest))
            {
                return true;
            }

            return source.TryGetProperty("resourceType", out var typeElement)
                && typeElement.ValueKind == JsonValueKind.String
                && string.Equals(typeElement.GetString(), "MedicationRequest", StringComparison.OrdinalIgnoreCase)
                && (medicationRequest = source).ValueKind == JsonValueKind.Object;
        }

        private static bool TryFindMedicationRequestInBundle(JsonElement bundle, out JsonElement medicationRequest)
        {
            medicationRequest = default;

            if (bundle.ValueKind != JsonValueKind.Object
                || !TryGetPropertyCaseInsensitive(bundle, "entry", out var entries)
                || entries.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object
                    || !TryGetPropertyCaseInsensitive(entry, "resource", out var resource)
                    || resource.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (resource.TryGetProperty("resourceType", out var resourceType)
                    && resourceType.ValueKind == JsonValueKind.String
                    && string.Equals(resourceType.GetString(), "MedicationRequest", StringComparison.OrdinalIgnoreCase))
                {
                    medicationRequest = resource;
                    return true;
                }
            }

            return false;
        }

        private static MedicationInput BuildMedicationInputFromMedicationRequest(JsonElement medicationRequest)
        {
            var requestId = GetStringPropertyCaseInsensitive(medicationRequest, "id")?.Trim();
            string? rxCui = null;
            string? name = null;
            decimal? requestedSingleMg = null;
            decimal? requestedDailyMg = null;

            if (TryGetPropertyCaseInsensitive(medicationRequest, "medication", out var medication)
                && medication.ValueKind == JsonValueKind.Object)
            {
                if (TryGetPropertyCaseInsensitive(medication, "concept", out var concept))
                {
                    ExtractMedicationIdentity(concept, ref rxCui, ref name);
                }
                else
                {
                    ExtractMedicationIdentity(medication, ref rxCui, ref name);
                }
            }

            if (TryGetPropertyCaseInsensitive(medicationRequest, "dosageInstruction", out var dosageInstructions)
                && dosageInstructions.ValueKind == JsonValueKind.Array)
            {
                var first = dosageInstructions.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object)
                {
                    requestedSingleMg = ExtractSingleDoseMg(first);
                    requestedDailyMg = ExtractDailyDoseMg(first, requestedSingleMg);
                }
            }

            return new MedicationInput(requestId, rxCui, name, requestedSingleMg, requestedDailyMg);
        }

        private static MedicationInput BuildMedicationInputFromMedicationElement(JsonElement medication)
        {
            string? rxCui = null;
            string? name = null;
            ExtractMedicationIdentity(medication, ref rxCui, ref name);
            return new MedicationInput(null, rxCui, name, null, null);
        }

        private static void ExtractMedicationIdentity(JsonElement source, ref string? rxCui, ref string? name)
        {
            if (TryGetPropertyCaseInsensitive(source, "coding", out var codingArray)
                && codingArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var coding in codingArray.EnumerateArray())
                {
                    if (coding.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var system = GetStringPropertyCaseInsensitive(coding, "system");
                    var code = GetStringPropertyCaseInsensitive(coding, "code");
                    var display = GetStringPropertyCaseInsensitive(coding, "display");

                    if (string.Equals(system, RxNormSystem, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(code))
                    {
                        rxCui = code.Trim();
                    }

                    if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(display))
                    {
                        name = display.Trim();
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = GetStringPropertyCaseInsensitive(source, "text")?.Trim();
            }

            if (string.IsNullOrWhiteSpace(rxCui))
            {
                rxCui = GetStringPropertyCaseInsensitive(source, "rxCui")?.Trim();
            }
        }

        private static decimal? ExtractSingleDoseMg(JsonElement dosageInstruction)
        {
            if (!TryGetPropertyCaseInsensitive(dosageInstruction, "doseAndRate", out var doseAndRate)
                || doseAndRate.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in doseAndRate.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !TryGetPropertyCaseInsensitive(item, "doseQuantity", out var doseQuantity)
                    || doseQuantity.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var unit = GetStringPropertyCaseInsensitive(doseQuantity, "code")
                    ?? GetStringPropertyCaseInsensitive(doseQuantity, "unit");

                if (!string.Equals(unit, "mg", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryGetDecimalPropertyCaseInsensitive(doseQuantity, "value", out var value))
                {
                    return value;
                }
            }

            return null;
        }

        private static decimal? ExtractDailyDoseMg(JsonElement dosageInstruction, decimal? singleDoseMg)
        {
            if (singleDoseMg == null)
            {
                return null;
            }

            if (!TryGetPropertyCaseInsensitive(dosageInstruction, "timing", out var timing)
                || !TryGetPropertyCaseInsensitive(timing, "repeat", out var repeat)
                || repeat.ValueKind != JsonValueKind.Object)
            {
                return singleDoseMg;
            }

            if (!TryGetDecimalPropertyCaseInsensitive(repeat, "frequency", out var frequency))
            {
                return singleDoseMg;
            }

            var period = TryGetDecimalPropertyCaseInsensitive(repeat, "period", out var periodValue) ? periodValue : 1m;
            var periodUnit = GetStringPropertyCaseInsensitive(repeat, "periodUnit");
            if (period <= 0)
            {
                return singleDoseMg;
            }

            decimal dosesPerDay = periodUnit?.ToLowerInvariant() switch
            {
                "h" => frequency * (24m / period),
                "d" or null => frequency / period,
                "wk" => frequency / (period * 7m),
                "mo" => frequency / (period * 30m),
                _ => frequency
            };

            return singleDoseMg * dosesPerDay;
        }

        private static string? ExtractPatientId(JsonElement context)
        {
            if (context.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (context.TryGetProperty("patientId", out var patientIdElement)
                && patientIdElement.ValueKind == JsonValueKind.String)
            {
                return patientIdElement.GetString();
            }

            if (context.TryGetProperty("patient", out var patientElement)
                && patientElement.ValueKind == JsonValueKind.String)
            {
                return patientElement.GetString();
            }

            return null;
        }

        private static string NormalizePatientReference(string patientId)
            => patientId.StartsWith("Patient/", StringComparison.OrdinalIgnoreCase)
                ? patientId
                : $"Patient/{patientId}";

        private static int? CalculateAgeYears(string? birthDate)
        {
            if (string.IsNullOrWhiteSpace(birthDate) || !DateOnly.TryParse(birthDate, out var birthDateValue))
            {
                return null;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - birthDateValue.Year;
            if (today < birthDateValue.AddYears(age))
            {
                age--;
            }

            return age < 0 ? null : age;
        }

        private static bool IsActiveCondition(Condition condition)
        {
            var clinicalCodes = condition.ClinicalStatus?.Coding?
                .Select(coding => coding.Code)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToArray() ?? [];

            if (clinicalCodes.Length == 0)
            {
                return true;
            }

            return clinicalCodes.Any(code => ActiveConditionClinicalStatuses.Contains(code!));
        }

        private static bool IsActiveMedicationRequest(MedicationRequest medicationRequest)
        {
            var status = medicationRequest.Status?.ToString();
            return !string.IsNullOrWhiteSpace(status) && ActiveMedicationStatuses.Contains(status);
        }

        private static bool IsSameMedicationRequest(MedicationRequest medicationRequest, MedicationInput currentMedication)
        {
            if (!string.IsNullOrWhiteSpace(currentMedication.RequestId)
                && string.Equals(medicationRequest.Id, currentMedication.RequestId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var medicationCode = ExtractMedicationCode(medicationRequest);
            return !string.IsNullOrWhiteSpace(currentMedication.RxCui)
                && !string.IsNullOrWhiteSpace(medicationCode)
                && string.Equals(currentMedication.RxCui, medicationCode, StringComparison.OrdinalIgnoreCase);
        }

        private static string? ExtractMedicationCode(MedicationRequest medicationRequest)
        {
            var concept = medicationRequest.Medication?.Concept;
            var coding = concept?.Coding?
                .FirstOrDefault(entry =>
                    string.Equals(entry.System, RxNormSystem, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(entry.Code));

            if (!string.IsNullOrWhiteSpace(coding?.Code))
            {
                return coding.Code.Trim();
            }

            return !string.IsNullOrWhiteSpace(concept?.Text)
                ? concept.Text.Trim()
                : null;
        }

        private static bool TryGetPropertyCaseInsensitive(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        private static string? GetStringPropertyCaseInsensitive(JsonElement element, string name)
        {
            return TryGetPropertyCaseInsensitive(element, name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static bool TryGetDecimalPropertyCaseInsensitive(JsonElement element, string name, out decimal value)
        {
            if (TryGetPropertyCaseInsensitive(element, name, out var property) && property.ValueKind == JsonValueKind.Number)
            {
                return property.TryGetDecimal(out value);
            }

            value = 0;
            return false;
        }

        private sealed record MedicationInput(
            string? RequestId,
            string? RxCui,
            string? Name,
            decimal? RequestedSingleMg,
            decimal? RequestedDailyMg);
    }
}
