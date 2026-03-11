using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Security
{
    public interface IResourceOwnershipResolver
    {
        Task<IEnumerable<string>> ResolveOrganizationsAsync(Resource resource);
        Task<IEnumerable<string>> ResolveOrganizationsAsync(string resourceType, string resourceId);
    }

    public class ResourceOwnershipResolver : IResourceOwnershipResolver
    {
        private readonly IFhirRepository _repo;

        public ResourceOwnershipResolver(IFhirRepository repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<string>> ResolveOrganizationsAsync(Resource resource)
        {
            switch (resource)
            {
                case Organization org:
                    return string.IsNullOrWhiteSpace(org.Id) ? Enumerable.Empty<string>() : [org.Id];

                case PractitionerRole role:
                    return ReferenceToIds(role.Organization?.Reference, "Organization");

                case Location loc:
                    return ReferenceToIds(loc.ManagingOrganization?.Reference, "Organization");

                case HealthcareService svc:
                    return ReferenceToIds(svc.ProvidedBy?.Reference, "Organization");

                case Patient patient:
                    return ReferenceToIds(patient.ManagingOrganization?.Reference, "Organization");

                case Encounter encounter:
                    return await ResolveOrganizationsByPatientReferenceAsync(encounter.Subject?.Reference);

                case Condition condition:
                    return await ResolveOrganizationsByPatientReferenceAsync(condition.Subject?.Reference);

                case Observation observation:
                    return await ResolveOrganizationsByPatientReferenceAsync(observation.Subject?.Reference);

                case Procedure procedure:
                    return await ResolveOrganizationsByPatientReferenceAsync(procedure.Subject?.Reference);

                case MedicationRequest medicationRequest:
                    return await ResolveOrganizationsByPatientReferenceAsync(medicationRequest.Subject?.Reference);

                case ServiceRequest serviceRequest:
                    return await ResolveOrganizationsByPatientReferenceAsync(serviceRequest.Subject?.Reference);

                case AllergyIntolerance allergyIntolerance:
                    return await ResolveOrganizationsByPatientReferenceAsync(allergyIntolerance.Patient?.Reference);

                case Appointment appointment:
                    return await ResolveOrganizationsByPatientAppointmentAsync(appointment);

                case Practitioner practitioner:
                    if (string.IsNullOrWhiteSpace(practitioner.Id))
                    {
                        return Enumerable.Empty<string>();
                    }

                    return await ResolvePractitionerOrganizations(practitioner.Id);

                default:
                    return Enumerable.Empty<string>();
            }
        }

        public async Task<IEnumerable<string>> ResolveOrganizationsAsync(string resourceType, string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceType) || string.IsNullOrWhiteSpace(resourceId))
            {
                return Enumerable.Empty<string>();
            }

            return resourceType.Trim() switch
            {
                "Organization" => [resourceId],
                "Patient" => await ResolveByResourceAsync<Patient>(resourceId),
                "Practitioner" => await ResolvePractitionerOrganizations(resourceId),
                "PractitionerRole" => await ResolveByResourceAsync<PractitionerRole>(resourceId),
                "Location" => await ResolveByResourceAsync<Location>(resourceId),
                "HealthcareService" => await ResolveByResourceAsync<HealthcareService>(resourceId),
                "Encounter" => await ResolveByResourceAsync<Encounter>(resourceId),
                "Condition" => await ResolveByResourceAsync<Condition>(resourceId),
                "Observation" => await ResolveByResourceAsync<Observation>(resourceId),
                "Procedure" => await ResolveByResourceAsync<Procedure>(resourceId),
                "MedicationRequest" => await ResolveByResourceAsync<MedicationRequest>(resourceId),
                "ServiceRequest" => await ResolveByResourceAsync<ServiceRequest>(resourceId),
                "AllergyIntolerance" => await ResolveByResourceAsync<AllergyIntolerance>(resourceId),
                "Appointment" => await ResolveByResourceAsync<Appointment>(resourceId),
                _ => Enumerable.Empty<string>()
            };
        }

        private async Task<IEnumerable<string>> ResolveByResourceAsync<T>(string resourceId) where T : Resource
        {
            var resource = await _repo.GetAsync<T>(resourceId);
            if (resource is null)
            {
                return Enumerable.Empty<string>();
            }

            return await ResolveOrganizationsAsync(resource);
        }

        private async Task<IEnumerable<string>> ResolvePractitionerOrganizations(string practitionerId)
        {
            var queryParams = new Dictionary<string, string>
            {
                { "practitioner", $"Practitioner/{practitionerId}" }
            };

            var roles = await _repo.SearchAsync<PractitionerRole>(queryParams);

            return roles
                .SelectMany(r => ReferenceToIds(r.Organization?.Reference, "Organization"))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private async Task<IEnumerable<string>> ResolveOrganizationsByPatientReferenceAsync(string? reference)
        {
            var patientId = ReferenceToId(reference, "Patient");
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return Enumerable.Empty<string>();
            }

            var patient = await _repo.GetAsync<Patient>(patientId);
            if (patient is null)
            {
                return Enumerable.Empty<string>();
            }

            return ReferenceToIds(patient.ManagingOrganization?.Reference, "Organization");
        }

        private async Task<IEnumerable<string>> ResolveOrganizationsByPatientAppointmentAsync(Appointment appointment)
        {
            if (appointment.Participant is null || appointment.Participant.Count == 0)
            {
                return Enumerable.Empty<string>();
            }

            var organizations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var participant in appointment.Participant)
            {
                var patientId = ReferenceToId(participant.Actor?.Reference, "Patient");
                if (string.IsNullOrWhiteSpace(patientId))
                {
                    continue;
                }

                var patient = await _repo.GetAsync<Patient>(patientId);
                if (patient is null)
                {
                    continue;
                }

                foreach (var orgId in ReferenceToIds(patient.ManagingOrganization?.Reference, "Organization"))
                {
                    organizations.Add(orgId);
                }
            }

            return organizations;
        }

        private static string? ReferenceToId(string? reference, string expectedType)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            var trimmed = reference.Trim();
            var prefix = $"{expectedType}/";
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var id = trimmed[prefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            return id;
        }

        private static IEnumerable<string> ReferenceToIds(string? reference, string expectedType)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return Enumerable.Empty<string>();
            }

            var trimmed = reference.Trim();
            var prefix = $"{expectedType}/";
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Enumerable.Empty<string>();
            }

            var id = trimmed[prefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                return Enumerable.Empty<string>();
            }

            return [id];
        }
    }
}
