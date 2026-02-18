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
        Task<bool> IsResourceOwnedByPatientAsync(string resourceType, string resourceId, string patientId);
        bool IsResourceOwnedByPatient(Resource resource, string patientId, IEnumerable<ResourceIndex> resourceIndices);
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
            foreach (var claimType in PatientIdClaimTypes)
            {
                var claimValue = user.FindFirst(claimType)?.Value;
                if (!string.IsNullOrWhiteSpace(claimValue))
                {
                    return ParsePatientId(claimValue);
                }
            }

            var fhirUser = user.FindFirst("fhirUser")?.Value;
            if (!string.IsNullOrWhiteSpace(fhirUser))
            {
                return ParsePatientId(fhirUser);
            }

            return null;
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

        private static string? ParsePatientId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (trimmed.StartsWith("Patient/", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed["Patient/".Length..];
            }

            if (trimmed.StartsWith("patient/", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed["patient/".Length..];
            }

            return trimmed;
        }
    }
}
