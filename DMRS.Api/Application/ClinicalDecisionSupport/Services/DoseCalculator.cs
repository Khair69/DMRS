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

        public static bool TryCalculateDailyDoseMg(MedicationRequest request, Medication? medication, out double dailyDoseMg)
        {
            dailyDoseMg = 0;
            if (request.DosageInstruction.Count == 0)
            {
                return false;
            }

            foreach (var dosage in request.DosageInstruction)
            {
                if (!TryGetDoseMg(dosage, medication, out var doseMg))
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

        private static bool TryGetDoseMg(Dosage dosage, Medication? medication, out double doseMg)
        {
            doseMg = 0;
            foreach (var doseAndRate in dosage.DoseAndRate)
            {
                if (doseAndRate.Dose is Quantity quantity && quantity.Value.HasValue)
                {
                    var unitKey = quantity.Code ?? quantity.Unit ?? string.Empty;
                    if (UnitToMg.TryGetValue(unitKey, out var multiplier))
                    {
                        doseMg = Convert.ToDouble(quantity.Value.Value) * multiplier;
                        return true;
                    }

                    if (TryGetMedicationStrengthMgPerUnit(medication, unitKey, out var mgPerUnit))
                    {
                        doseMg = Convert.ToDouble(quantity.Value.Value) * mgPerUnit;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetMedicationStrengthMgPerUnit(Medication? medication, string unitKey, out double mgPerUnit)
        {
            mgPerUnit = 0;
            if (medication == null)
            {
                return false;
            }

            if (medication.Ingredient.Count != 1)
            {
                return false;
            }

            var ingredient = medication.Ingredient[0];
            if (ingredient.Strength is not Ratio strength)
            {
                return false;
            }

            if (strength.Numerator is not Quantity numerator || strength.Denominator is not Quantity denominator)
            {
                return false;
            }

            if (!numerator.Value.HasValue || !denominator.Value.HasValue)
            {
                return false;
            }

            var numeratorUnit = numerator.Code ?? numerator.Unit ?? string.Empty;
            if (!UnitToMg.TryGetValue(numeratorUnit, out var numeratorMultiplier))
            {
                return false;
            }

            var denominatorUnit = (denominator.Code ?? denominator.Unit ?? string.Empty).ToLowerInvariant();
            var inputUnit = unitKey.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(denominatorUnit) && denominatorUnit != inputUnit)
            {
                return false;
            }

            var strengthMg = Convert.ToDouble(numerator.Value.Value) * numeratorMultiplier;
            var perUnit = Convert.ToDouble(denominator.Value.Value);
            if (perUnit <= 0)
            {
                return false;
            }

            mgPerUnit = strengthMg / perUnit;
            return mgPerUnit > 0;
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
