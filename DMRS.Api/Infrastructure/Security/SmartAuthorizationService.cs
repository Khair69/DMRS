using DMRS.Api.Domain;
using DMRS.Api.Infrastructure.Persistence;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DMRS.Api.Infrastructure.Security
{
    public enum SmartAccessLevel
    {
        None,
        Patient,
        User,
        System
    }

    public interface ISmartAuthorizationService
    {
        SmartAccessLevel GetAccessLevel(ClaimsPrincipal user, string resourceType, string action);
        string? ResolvePatientId(ClaimsPrincipal user);
        string? ResolvePractitionerId(ClaimsPrincipal user);
        Task<IReadOnlyCollection<string>> ResolveOrganizationIdsAsync(ClaimsPrincipal user);
        Task<bool> IsResourceOwnedByPatientAsync(string resourceType, string resourceId, string patientId);
        bool IsResourceOwnedByPatient(Resource resource, string patientId, IEnumerable<ResourceIndex> resourceIndices);
        Task<bool> IsResourceOwnedByOrganizationsAsync(string resourceType, string resourceId, IReadOnlyCollection<string> organizationIds);
        bool IsResourceOwnedByOrganizations(Resource resource, IEnumerable<ResourceIndex> resourceIndices, IReadOnlyCollection<string> organizationIds);
    }

    public sealed class SmartAuthorizationService : ISmartAuthorizationService
    {
        private static readonly string[] PatientIdClaimTypes =
        [
            "patient",
            "patient_id",
            "launch_patient",
            "launch/patient"
        ];

        private static readonly string[] PractitionerIdClaimTypes =
        [
            "practitioner",
            "practitioner_id",
            "launch_practitioner",
            "launch/practitioner"
        ];

        private static readonly string[] OrganizationIdClaimTypes =
        [
            "organization",
            "organization_id",
            "org_id",
            "launch_organization",
            "launch/organization"
        ];

        private readonly AppDbContext _dbContext;

        public SmartAuthorizationService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public SmartAccessLevel GetAccessLevel(ClaimsPrincipal user, string resourceType, string action)
        {
            var scopes = GetScopes(user).ToList();
            if (scopes.Count == 0)
            {
                return SmartAccessLevel.None;
            }

            resourceType = resourceType.ToLowerInvariant();
            action = action.ToLowerInvariant();

            if (HasScope(scopes, "system", resourceType, action))
            {
                return SmartAccessLevel.System;
            }

            if (HasScope(scopes, "user", resourceType, action))
            {
                return SmartAccessLevel.User;
            }

            if (HasScope(scopes, "patient", resourceType, action))
            {
                return SmartAccessLevel.Patient;
            }

            return SmartAccessLevel.None;
        }

        public string? ResolvePatientId(ClaimsPrincipal user)
        {
            var patientId = ResolveFirstClaimReference(user, PatientIdClaimTypes, "patient");
            if (!string.IsNullOrWhiteSpace(patientId))
            {
                return patientId;
            }

            var fhirUser = user.FindFirst("fhirUser")?.Value;
            if (!string.IsNullOrWhiteSpace(fhirUser))
            {
                return ParsePatientId(fhirUser);
            }

            return null;
        }

        public string? ResolvePractitionerId(ClaimsPrincipal user)
        {
            var practitionerId = ResolveFirstClaimReference(user, PractitionerIdClaimTypes, "practitioner");
            if (!string.IsNullOrWhiteSpace(practitionerId))
            {
                return practitionerId;
            }

            var fhirUser = user.FindFirst("fhirUser")?.Value;
            if (!string.IsNullOrWhiteSpace(fhirUser))
            {
                return ParseReferenceId(fhirUser, "practitioner");
            }

            return null;
        }

        public async Task<IReadOnlyCollection<string>> ResolveOrganizationIdsAsync(ClaimsPrincipal user)
        {
            var organizations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var claimType in OrganizationIdClaimTypes)
            {
                foreach (var claim in user.FindAll(claimType))
                {
                    foreach (var value in SplitClaimValues(claim.Value))
                    {
                        var organizationId = ParseReferenceId(value, "organization");
                        if (!string.IsNullOrWhiteSpace(organizationId))
                        {
                            organizations.Add(organizationId);
                        }
                    }
                }
            }

            var practitionerId = ResolvePractitionerId(user);
            if (!string.IsNullOrWhiteSpace(practitionerId))
            {
                var practitionerReference = $"practitioner/{practitionerId}";

                var practitionerRoleIds = await _dbContext.ResourceIndices
                    .Where(i => i.ResourceType == "PractitionerRole"
                        && i.SearchParamCode == "practitioner"
                        && i.Value == practitionerReference)
                    .Select(i => i.ResourceId)
                    .Distinct()
                    .ToListAsync();

                if (practitionerRoleIds.Count > 0)
                {
                    var roleOrganizations = await _dbContext.ResourceIndices
                        .Where(i => i.ResourceType == "PractitionerRole"
                            && practitionerRoleIds.Contains(i.ResourceId)
                            && i.SearchParamCode == "organization")
                        .Select(i => i.Value)
                        .Distinct()
                        .ToListAsync();

                    foreach (var roleOrganization in roleOrganizations)
                    {
                        var organizationId = ParseReferenceId(roleOrganization, "organization");
                        if (!string.IsNullOrWhiteSpace(organizationId))
                        {
                            organizations.Add(organizationId);
                        }
                    }
                }
            }

            return organizations.ToList();
        }

        public async Task<bool> IsResourceOwnedByPatientAsync(string resourceType, string resourceId, string patientId)
        {
            if (resourceType.Equals("Patient", StringComparison.OrdinalIgnoreCase))
            {
                return resourceId.Equals(patientId, StringComparison.OrdinalIgnoreCase);
            }

            var expectedReference = $"patient/{patientId}";

            return await _dbContext.ResourceIndices
                .AnyAsync(i => i.ResourceType == resourceType
                            && i.ResourceId == resourceId
                            && (i.SearchParamCode == "patient" || i.SearchParamCode == "subject")
                            && i.Value == expectedReference.ToLowerInvariant());
        }

        public bool IsResourceOwnedByPatient(Resource resource, string patientId, IEnumerable<ResourceIndex> resourceIndices)
        {
            if (resource is Patient patient)
            {
                return string.Equals(patient.Id, patientId, StringComparison.OrdinalIgnoreCase);
            }

            var expectedReference = $"patient/{patientId}";

            return resourceIndices.Any(i => (i.SearchParamCode == "patient" || i.SearchParamCode == "subject")
                && string.Equals(i.Value, expectedReference, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> IsResourceOwnedByOrganizationsAsync(string resourceType, string resourceId, IReadOnlyCollection<string> organizationIds)
        {
            if (organizationIds.Count == 0)
            {
                return false;
            }

            if (resourceType.Equals("Organization", StringComparison.OrdinalIgnoreCase))
            {
                return organizationIds.Contains(resourceId, StringComparer.OrdinalIgnoreCase);
            }

            var expectedOrganizationReferences = organizationIds
                .Select(i => $"organization/{i}".ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return await _dbContext.ResourceIndices
                .AnyAsync(i => i.ResourceType == resourceType
                    && i.ResourceId == resourceId
                    && i.SearchParamCode == "organization"
                    && expectedOrganizationReferences.Contains(i.Value));
        }

        public bool IsResourceOwnedByOrganizations(Resource resource, IEnumerable<ResourceIndex> resourceIndices, IReadOnlyCollection<string> organizationIds)
        {
            if (organizationIds.Count == 0)
            {
                return false;
            }

            if (resource is Organization organization)
            {
                return organizationIds.Contains(organization.Id, StringComparer.OrdinalIgnoreCase);
            }

            var expectedOrganizationReferences = organizationIds
                .Select(i => $"organization/{i}".ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return resourceIndices.Any(i => i.SearchParamCode == "organization" && expectedOrganizationReferences.Contains(i.Value));
        }

        private static IEnumerable<string> GetScopes(ClaimsPrincipal user)
        {
            var scopeValues = user.FindAll("scope")
                .Select(c => c.Value)
                .Concat(user.FindAll("scp").Select(c => c.Value));

            foreach (var value in scopeValues)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                foreach (var token in value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    yield return token.ToLowerInvariant();
                }
            }
        }

        private static bool HasScope(IEnumerable<string> scopes, string context, string resourceType, string action)
        {
            return scopes.Contains($"{context}/{resourceType}.{action}")
                || scopes.Contains($"{context}/{resourceType}.*")
                || scopes.Contains($"{context}/*.{action}")
                || scopes.Contains($"{context}/*.*")
                || (action == "write" &&
                    (scopes.Contains($"{context}/{resourceType}.create")
                    || scopes.Contains($"{context}/{resourceType}.update")
                    || scopes.Contains($"{context}/{resourceType}.delete")
                    || scopes.Contains($"{context}/*.create")
                    || scopes.Contains($"{context}/*.update")
                    || scopes.Contains($"{context}/*.delete")));
        }

        private static string? ResolveFirstClaimReference(ClaimsPrincipal user, IEnumerable<string> claimTypes, string expectedResourceType)
        {
            foreach (var claimType in claimTypes)
            {
                var claimValue = user.FindFirst(claimType)?.Value;
                if (!string.IsNullOrWhiteSpace(claimValue))
                {
                    return ParseReferenceId(claimValue, expectedResourceType);
                }
            }

            return null;
        }

        private static IEnumerable<string> SplitClaimValues(string claimValue)
        {
            return claimValue.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static string? ParsePatientId(string value)
        {
            return ParseReferenceId(value, "patient");
        }

        private static string? ParseReferenceId(string value, string expectedResourceType)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            var prefix = $"{expectedResourceType}/";
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[prefix.Length..];
            }

            if (trimmed.Contains('/'))
            {
                return null;
            }

            return trimmed;
        }
    }
}
