using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Administrative
{
    public class PatientIndexer : ResourceSearchIndexerBase
    {
        protected override string ResourceType => "Patient";
        public override List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is Patient patient)
            {
                AddIndex(indices, patient.Id, "_id", patient.Id);
                AddIndex(indices, patient.Id, "_lastUpdated", patient.Meta?.LastUpdated?.ToString("o"));
                AddIndex(indices, patient.Id, "active", patient.Active?.ToString());
                AddIndex(indices, patient.Id, "birthdate", patient.BirthDate);
                AddIndex(indices, patient.Id, "deceased", patient.Deceased is FhirBoolean deceasedBoolean ? deceasedBoolean.Value?.ToString() : null);
                AddIndex(indices, patient.Id, "death-date", patient.Deceased is FhirDateTime deathDate ? deathDate.Value : null);

                if (patient.Gender.HasValue)
                    AddIndex(indices, patient.Id, "gender", patient.Gender.Value.ToString());

                foreach (var identifier in patient.Identifier)
                {
                    AddIndex(indices, patient.Id, "identifier", identifier.Value);
                    AddIndex(indices, patient.Id, "identifier", string.IsNullOrWhiteSpace(identifier.System) ? null : $"{identifier.System}|{identifier.Value}");
                }

                foreach (var telecom in patient.Telecom)
                {
                    AddIndex(indices, patient.Id, "telecom", telecom.Value);

                    if (telecom.System == ContactPoint.ContactPointSystem.Phone)
                        AddIndex(indices, patient.Id, "phone", telecom.Value);

                    if (telecom.System == ContactPoint.ContactPointSystem.Email)
                        AddIndex(indices, patient.Id, "email", telecom.Value);
                }

                foreach (var address in patient.Address)
                {
                    AddIndex(indices, patient.Id, "address", address.Text);
                    AddIndex(indices, patient.Id, "address-city", address.City);
                    AddIndex(indices, patient.Id, "address-state", address.State);
                    AddIndex(indices, patient.Id, "address-postalcode", address.PostalCode);
                    AddIndex(indices, patient.Id, "address-country", address.Country);
                    AddIndex(indices, patient.Id, "address-use", address.Use.ToString());
                }

                foreach (var name in patient.Name)
                {
                    AddIndex(indices, patient.Id, "name", name.Text);
                    AddIndex(indices, patient.Id, "family", name.Family);

                    foreach (var given in name.Given)
                    {
                        AddIndex(indices, patient.Id, "given", given);
                    }
                }

                foreach (var communication in patient.Communication)
                {
                    AddIndex(indices, patient.Id, "language", communication.Language?.Text);

                    foreach (var coding in communication.Language?.Coding ?? [])
                    {
                        AddIndex(indices, patient.Id, "language", coding.Code);
                    }
                }

                foreach (var practitioner in patient.GeneralPractitioner)
                    AddIndex(indices, patient.Id, "general-practitioner", practitioner.Reference);

                AddIndex(indices, patient.Id, "organization", patient.ManagingOrganization?.Reference);

                foreach (var link in patient.Link)
                    AddIndex(indices, patient.Id, "link", link.Other?.Reference);
            }
            return indices;
        }
    }
}
