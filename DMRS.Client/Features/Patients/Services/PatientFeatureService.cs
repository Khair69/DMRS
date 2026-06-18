using DMRS.Client.Features.Patients.Models;
using DMRS.Client.Services;
using DMRS.Shared.Constants;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Patients.Services;

public class PatientFeatureService : FhirFeatureServiceBase<Patient, PatientEditModel, PatientSummaryViewModel>
{
    private readonly FhirApiService _fhirApiService;

    public PatientFeatureService(FhirApiService fhirApiService) : base(fhirApiService)
    {
        _fhirApiService = fhirApiService;
    }

    protected override Patient ToResource(PatientEditModel model)
        => model.ToFhirPatient();

    // Type-ahead suggestions for the Patients search box, scoped server-side to readable patients.
    public Task<IReadOnlyList<string>> SuggestAsync(string field, string value)
        => _fhirApiService.SuggestAsync<Patient>(field, value);

    protected override PatientSummaryViewModel MapToSummary(Patient patient)
    {
        var name = patient.Name.FirstOrDefault();
        var displayName = string.Join(" ", new[] { name?.Given?.FirstOrDefault(), name?.Family }.Where(x => !string.IsNullOrWhiteSpace(x)));

        // Headline identifier is the readable patient number; fall back to the first identifier for
        // legacy rows that have not been backfilled yet.
        var headline = patient.Identifier.FirstOrDefault(i => i.System == PatientIdentifierSystems.PatientNumber)?.Value
            ?? patient.Identifier.FirstOrDefault()?.Value;

        return new PatientSummaryViewModel(
            patient.Id ?? "(no-id)",
            string.IsNullOrWhiteSpace(displayName) ? "Unnamed patient" : displayName,
            patient.Gender.ToString(),
            patient.BirthDate,
            headline);
    }

    // Patient updates must NOT clobber system-managed identifiers (patient number, invite code,
    // Keycloak account link). The base UpdateAsync rebuilds the resource from the form alone, which
    // would drop them — so patients use this merge-aware path instead: it overlays the edited
    // demographics + city + the single editable identifier onto the existing resource.
    public async Task<Patient?> UpdatePatientAsync(string id, PatientEditModel model)
    {
        var existing = await GetAsync(id)
            ?? throw new InvalidOperationException($"Patient {id} was not found.");

        var updated = model.ToFhirPatient();
        updated.Id = id;

        // Preserve every existing identifier except the slot the form owns (matched by its original
        // system+value); then re-add the form's current editable identifier from `updated`.
        var preserved = existing.Identifier
            .Where(i => !(string.Equals(i.System, model.OriginalIdentifierSystem, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(i.Value, model.OriginalIdentifierValue, StringComparison.Ordinal)))
            .ToList();
        var editable = updated.Identifier?.ToList() ?? [];
        updated.Identifier = [.. preserved, .. editable];

        // Preserve any richer address captured elsewhere (e.g. the patient portal); only set the city.
        var existingAddress = existing.Address?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(model.City) || existingAddress is not null)
        {
            var address = existingAddress ?? new Address { Country = "SY" };
            if (!string.IsNullOrWhiteSpace(model.City))
            {
                address.City = model.City.Trim();
            }
            updated.Address = [address];
        }

        return await _fhirApiService.UpdateResourceAsync(id, updated);
    }

    public async Task<PatientInviteResult> CreateInviteAsync(PatientEditModel model, string appBaseUri)
    {
        if (string.IsNullOrWhiteSpace(model.ManagingOrganizationId))
        {
            throw new InvalidOperationException("Managing organization is required to create a patient invite.");
        }

        var response = await _fhirApiService.PostApiJsonAsync<CreatePatientInviteRequest, CreatePatientInviteResponse>(
            "api/patients/create-invite",
            new CreatePatientInviteRequest
            {
                OrganizationId = model.ManagingOrganizationId,
                AppBaseUri = appBaseUri,
                ClaimPath = "/patients/claim",
                GivenName = model.GivenName,
                FamilyName = model.FamilyName,
                BirthDate = model.BirthDate,
                Gender = model.Gender,
                City = model.City,
                IdentifierSystem = model.IdentifierSystem,
                IdentifierValue = model.IdentifierValue
            });

        if (response is null || string.IsNullOrWhiteSpace(response.PatientId))
        {
            throw new InvalidOperationException("Invite creation failed: empty response from API.");
        }

        return new PatientInviteResult(
            response.PatientId,
            response.InviteCode,
            response.ClaimLink,
            response.RegistrationLink);
    }

    public async Task<PatientInviteResult> GenerateInviteAsync(string patientId, string appBaseUri)
    {
        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new InvalidOperationException("Patient id is required to generate an invite.");
        }

        var response = await _fhirApiService.PostApiJsonAsync<GeneratePatientInviteRequest, CreatePatientInviteResponse>(
            $"api/patients/{patientId}/create-invite",
            new GeneratePatientInviteRequest
            {
                AppBaseUri = appBaseUri,
                ClaimPath = "/patients/claim"
            });

        if (response is null || string.IsNullOrWhiteSpace(response.PatientId))
        {
            throw new InvalidOperationException("Invite generation failed: empty response from API.");
        }

        return new PatientInviteResult(
            response.PatientId,
            response.InviteCode,
            response.ClaimLink,
            response.RegistrationLink);
    }

    public async Task<PatientClaimResult> ClaimInviteAsync(string inviteCode, string keycloakUserId, string? keycloakUsername)
    {
        var response = await _fhirApiService.PostApiJsonAsync<PatientClaimRequest, PatientClaimApiResponse>(
            "api/patients/claim-invite",
            new PatientClaimRequest
            {
                InviteCode = inviteCode,
                KeycloakUserId = keycloakUserId,
                KeycloakUsername = keycloakUsername
            });

        if (response is null || string.IsNullOrWhiteSpace(response.PatientId))
        {
            throw new InvalidOperationException("Claim failed: empty response from API.");
        }

        return new PatientClaimResult(response.PatientId, response.AssignedRealmRole);
    }
}

public sealed record PatientInviteResult(
    string PatientId,
    string InviteCode,
    string ClaimLink,
    string RegistrationLink);

public sealed record PatientClaimResult(
    string PatientId,
    string AssignedRealmRole);

internal sealed class CreatePatientInviteRequest
{
    public string OrganizationId { get; set; } = string.Empty;
    public string AppBaseUri { get; set; } = string.Empty;
    public string? ClaimPath { get; set; }
    public string GivenName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? City { get; set; }
    public string? IdentifierSystem { get; set; }
    public string? IdentifierValue { get; set; }
}

internal sealed class CreatePatientInviteResponse
{
    public string PatientId { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public string ClaimLink { get; set; } = string.Empty;
    public string RegistrationLink { get; set; } = string.Empty;
}

internal sealed class GeneratePatientInviteRequest
{
    public string AppBaseUri { get; set; } = string.Empty;
    public string? ClaimPath { get; set; }
}

internal sealed class PatientClaimRequest
{
    public string InviteCode { get; set; } = string.Empty;
    public string KeycloakUserId { get; set; } = string.Empty;
    public string? KeycloakUsername { get; set; }
}

internal sealed class PatientClaimApiResponse
{
    public string PatientId { get; set; } = string.Empty;
    public string AssignedRealmRole { get; set; } = string.Empty;
}
