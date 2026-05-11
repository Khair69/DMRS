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

        private readonly IClinicalKnowledgeService _clinicalKnowledgeService;
        private readonly IFhirRepository _fhirRepository;

        public CdsContextBuilder(
            IClinicalKnowledgeService clinicalKnowledgeService,
            IFhirRepository fhirRepository)
        {
            _clinicalKnowledgeService = clinicalKnowledgeService;
            _fhirRepository = fhirRepository;
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
            if (medicationInput == null)
            {
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
        }

        private async System.Threading.Tasks.Task<IReadOnlyList<string>> GetPatientAllergyCodesAsync(string? patientId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return [];
            }

            var patientRef = patientId.StartsWith("Patient/", StringComparison.OrdinalIgnoreCase)
                ? patientId
                : $"Patient/{patientId}";

            var allergies = await _fhirRepository.SearchAsync<AllergyIntolerance>(new Dictionary<string, string>
            {
                ["patient"] = patientRef
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

            return new MedicationInput(rxCui, name, requestedSingleMg, requestedDailyMg);
        }

        private static MedicationInput BuildMedicationInputFromMedicationElement(JsonElement medication)
        {
            string? rxCui = null;
            string? name = null;
            ExtractMedicationIdentity(medication, ref rxCui, ref name);
            return new MedicationInput(rxCui, name, null, null);
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
            string? RxCui,
            string? Name,
            decimal? RequestedSingleMg,
            decimal? RequestedDailyMg);
    }
}
