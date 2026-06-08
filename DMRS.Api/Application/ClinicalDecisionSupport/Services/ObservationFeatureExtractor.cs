using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    /// <summary>
    /// Pulls numeric feature values out of a patient's FHIR Observations for the AI risk models.
    /// A single FHIR search loads every Observation for the patient; <see cref="LatestValue"/> then
    /// picks the most recent matching measurement. Both the Observation's own <c>value[x]</c> and its
    /// <c>component[]</c> values are inspected, because vitals such as blood pressure are recorded as
    /// one panel Observation (LOINC 85354-9) with systolic/diastolic carried in components.
    /// </summary>
    public sealed class ObservationFeatureExtractor
    {
        private readonly IFhirRepository _fhirRepository;

        public ObservationFeatureExtractor(IFhirRepository fhirRepository)
        {
            _fhirRepository = fhirRepository;
        }

        /// <summary>Loads every Observation recorded against the given (already normalized) patient id.</summary>
        public async Task<IReadOnlyList<Observation>> GetObservationsAsync(string normalizedPatientId)
        {
            var patientRef = $"Patient/{normalizedPatientId}";
            return await _fhirRepository.SearchAsync<Observation>(
                new Dictionary<string, string> { ["patient"] = patientRef });
        }

        /// <summary>
        /// Returns the most recent numeric value among the supplied observations whose code (or one of
        /// its component codes) matches any of <paramref name="loincCodes"/>, or null if none match.
        /// </summary>
        public static double? LatestValue(IEnumerable<Observation> observations, params string[] loincCodes)
        {
            var codes = new HashSet<string>(loincCodes, StringComparer.OrdinalIgnoreCase);
            (DateTimeOffset When, double Value)? best = null;

            foreach (var obs in observations)
            {
                var when = GetEffective(obs);

                if (CodeMatches(obs.Code, codes) && TryQuantity(obs.Value, out var topValue))
                {
                    Consider(ref best, when, topValue);
                }

                if (obs.Component is { Count: > 0 })
                {
                    foreach (var component in obs.Component)
                    {
                        if (CodeMatches(component.Code, codes) && TryQuantity(component.Value, out var compValue))
                        {
                            Consider(ref best, when, compValue);
                        }
                    }
                }
            }

            return best?.Value;
        }

        private static void Consider(ref (DateTimeOffset When, double Value)? best, DateTimeOffset when, double value)
        {
            if (best is null || when >= best.Value.When)
            {
                best = (when, value);
            }
        }

        private static bool CodeMatches(CodeableConcept? code, HashSet<string> codes)
        {
            if (code?.Coding is null)
            {
                return false;
            }

            foreach (var coding in code.Coding)
            {
                if (!string.IsNullOrWhiteSpace(coding.Code) && codes.Contains(coding.Code))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryQuantity(DataType? value, out double result)
        {
            if (value is Quantity quantity && quantity.Value.HasValue)
            {
                result = (double)quantity.Value.Value;
                return true;
            }

            result = 0;
            return false;
        }

        private static DateTimeOffset GetEffective(Observation observation)
        {
            switch (observation.Effective)
            {
                case FhirDateTime dateTime when DateTimeOffset.TryParse(dateTime.Value, out var dto):
                    return dto;
                case Instant instant when instant.Value.HasValue:
                    return instant.Value.Value;
                case Period period when !string.IsNullOrWhiteSpace(period.Start)
                                        && DateTimeOffset.TryParse(period.Start, out var start):
                    return start;
                default:
                    // Unknown / missing timing — rank lowest so dated measurements win.
                    return DateTimeOffset.MinValue;
            }
        }
    }
}
