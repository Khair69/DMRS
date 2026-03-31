using Hl7.Fhir.Model;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public static class DoseCalculator
    {
        private static readonly Dictionary<string, double> UnitToMg = new(StringComparer.OrdinalIgnoreCase)
        {
            ["mg"] = 1,
            ["milligram"] = 1,
            ["milligrams"] = 1,
            ["g"] = 1000,
            ["gram"] = 1000,
            ["grams"] = 1000,
            ["mcg"] = 0.001,
            ["ug"] = 0.001,
            ["microgram"] = 0.001,
            ["micrograms"] = 0.001
        };

        public static bool TryCalculateDailyDoseMg(MedicationRequest request, out double dailyDoseMg)
        {
            dailyDoseMg = 0;
            if (request.DosageInstruction.Count == 0)
            {
                return false;
            }

            foreach (var dosage in request.DosageInstruction)
            {
                if (!TryGetDoseMg(dosage, out var doseMg))
                {
                    continue;
                }

                if (!TryGetAdministrationsPerDay(dosage, out var administrationsPerDay))
                {
                    continue;
                }

                dailyDoseMg += doseMg * administrationsPerDay;
            }

            return dailyDoseMg > 0;
        }

        private static bool TryGetDoseMg(Dosage dosage, out double doseMg)
        {
            doseMg = 0;
            foreach (var doseAndRate in dosage.DoseAndRate)
            {
                if (doseAndRate.Dose is Quantity quantity && quantity.Value.HasValue)
                {
                    var unitKey = quantity.Code ?? quantity.Unit ?? string.Empty;
                    if (!UnitToMg.TryGetValue(unitKey, out var multiplier))
                    {
                        return false;
                    }

                    doseMg = Convert.ToDouble(quantity.Value.Value) * multiplier;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetAdministrationsPerDay(Dosage dosage, out double administrationsPerDay)
        {
            administrationsPerDay = 0;
            var repeat = dosage.Timing?.Repeat;
            if (repeat == null)
            {
                return false;
            }

            var frequency = repeat.Frequency ?? repeat.FrequencyMax;
            if (!frequency.HasValue || !repeat.Period.HasValue || !repeat.PeriodUnit.HasValue)
            {
                return false;
            }

            var periodInDays = Convert.ToDouble(repeat.Period.Value) * PeriodUnitToDays(repeat.PeriodUnit.Value);
            if (periodInDays <= 0)
            {
                return false;
            }

            administrationsPerDay = frequency.Value / periodInDays;
            return administrationsPerDay > 0;
        }

        private static double PeriodUnitToDays(Timing.UnitsOfTime unit)
        {
            return unit switch
            {
                Timing.UnitsOfTime.S => 1d / 86400d,
                Timing.UnitsOfTime.Min => 1d / 1440d,
                Timing.UnitsOfTime.H => 1d / 24d,
                Timing.UnitsOfTime.D => 1d,
                Timing.UnitsOfTime.Wk => 7d,
                Timing.UnitsOfTime.Mo => 30d,
                Timing.UnitsOfTime.A => 365d,
                _ => 0d
            };
        }
    }
}
