using DMRS.Client.Services;
using DMRS.Shared.Constants;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Patients.Services;

/// <summary>
/// Resolves between the human-readable patient number (<c>XX12345</c>, a FHIR Identifier) and the
/// Patient's logical id (the GUID that clinical references actually target). Clinical forms let staff
/// type the patient number; the reference graph still uses the GUID, so this service bridges the two.
/// </summary>
public sealed class PatientLookupService
{
    private readonly FhirApiService _fhirApiService;

    public PatientLookupService(FhirApiService fhirApiService)
    {
        _fhirApiService = fhirApiService;
    }

    /// <summary>
    /// Resolves a typed patient number (or any patient identifier / bare logical id) to the matching
    /// patient. Returns <c>null</c> when nothing matches. Prefers a match on the patient-number system,
    /// but any identifier value works so a national id or raw GUID also resolves.
    /// </summary>
    public async Task<PatientLookupResult?> ResolveByNumberAsync(string numberOrId)
    {
        var query = numberOrId?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var matches = await _fhirApiService.SearchAsync<Patient>("identifier", query);

        var patient = matches.FirstOrDefault(p => p.Identifier.Any(i =>
                          i.System == PatientIdentifierSystems.PatientNumber
                          && string.Equals(i.Value, query, StringComparison.OrdinalIgnoreCase)))
                      ?? matches.FirstOrDefault();

        // Fall back to a direct id read so a pasted GUID (not an identifier) still resolves.
        if (patient is null)
        {
            patient = await _fhirApiService.GetResourceAsync<Patient>(query);
        }

        return patient is null ? null : ToResult(patient);
    }

    /// <summary>
    /// Reverse lookup used by edit forms: given a patient's logical id, returns its readable number and
    /// display name so the form can show the number the caller expects instead of the GUID.
    /// </summary>
    public async Task<PatientLookupResult?> GetByIdAsync(string patientId)
    {
        if (string.IsNullOrWhiteSpace(patientId))
        {
            return null;
        }

        var patient = await _fhirApiService.GetResourceAsync<Patient>(patientId);
        return patient is null ? null : ToResult(patient);
    }

    private static PatientLookupResult ToResult(Patient patient)
    {
        var number = patient.Identifier.FirstOrDefault(i => i.System == PatientIdentifierSystems.PatientNumber)?.Value;

        var name = patient.Name.FirstOrDefault();
        var displayName = string.Join(" ", new[] { name?.Given?.FirstOrDefault(), name?.Family }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        return new PatientLookupResult(
            patient.Id ?? string.Empty,
            number,
            string.IsNullOrWhiteSpace(displayName) ? (patient.Id ?? string.Empty) : displayName);
    }
}

/// <param name="PatientId">The Patient's logical id (GUID) — the value clinical references target.</param>
/// <param name="PatientNumber">The readable patient number (<c>XX12345</c>), or null if not backfilled.</param>
/// <param name="DisplayName">The patient's name for confirmation, falling back to the id.</param>
public sealed record PatientLookupResult(string PatientId, string? PatientNumber, string DisplayName);
