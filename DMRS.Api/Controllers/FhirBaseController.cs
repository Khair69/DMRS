using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers
{
    [ApiController]
    [Route("fhir/[controller]")]
    [Authorize(Policy = "FhirScope")]
    [ProducesResponseType(typeof(OperationOutcome), 200)]
    [ProducesResponseType(typeof(OperationOutcome), 201)]
    [ProducesResponseType(typeof(OperationOutcome), 400)]
    [ProducesResponseType(typeof(OperationOutcome), 403)]
    [ProducesResponseType(typeof(OperationOutcome), 404)]
    [ProducesResponseType(typeof(OperationOutcome), 500)]
    public abstract class FhirBaseController<T> : ControllerBase where T : Resource
    {
        protected readonly IFhirRepository _repository;
        protected readonly ILogger _logger;
        protected readonly FhirJsonDeserializer _deserializer;
        protected readonly FhirJsonSerializer _serializer;
        protected readonly IFhirValidatorService _validator;
        protected readonly ISearchIndexer _searchIndexer;
        protected readonly ISmartAuthorizationService _authorizationService;

        public FhirBaseController(
            IFhirRepository repository,
            ILogger logger,
            FhirJsonDeserializer deserializer,
            FhirJsonSerializer serializer,
            IFhirValidatorService validator,
            ISearchIndexer searchIndexer,
            ISmartAuthorizationService authorizationService)
        {
            _repository = repository;
            _logger = logger;
            _deserializer = deserializer;
            _serializer = serializer;
            _validator = validator;
            _searchIndexer = searchIndexer;
            _authorizationService = authorizationService;
        }

        [HttpGet("{id}")]
        public virtual async Task<IActionResult> Read(string id)
        {
            var resource = await _repository.GetAsync<T>(id);
            if (resource == null) return NotFound();

            if (!await CanAccessResource(resource, "read"))
            {
                return Forbid();
            }

            var jsonString = _serializer.SerializeToString(resource);
            return Content(jsonString, "application/fhir+json");
        }

        [HttpGet("{id}/_history/{vid}")]
        public virtual async Task<IActionResult> VRead(string id, string vid)
        {
            if (!int.TryParse(vid, out var versionId) || versionId <= 0)
            {
                return BadRequest("Invalid version id.");
            }

            var resource = await _repository.GetVersionAsync<T>(id, versionId);
            if (resource == null)
            {
                return NotFound();
            }

            if (!await CanAccessResource(resource, "read", useResourceOwnership: true))
            {
                return Forbid();
            }

            var jsonString = _serializer.SerializeToString(resource);
            return Content(jsonString, "application/fhir+json");
        }


        [HttpGet]
        public virtual async Task<IActionResult> Search()
        {
            var queryParams = Request.Query
                .ToDictionary(q => q.Key, q => q.Value.ToString());

            var resources = await _repository.SearchAsync<T>(queryParams);

            var bundle = new Bundle
            {
                Type = Bundle.BundleType.Searchset
            };

            var accessibleResources = await FilterReadableAsync(resources);
            foreach (var resource in accessibleResources)
            {
                bundle.Entry.Add(new Bundle.EntryComponent
                {
                    Resource = resource,
                    FullUrl = $"{Request.Scheme}://{Request.Host}/fhir/{typeof(T).Name}/{resource.Id}"
                });
            }

            bundle.Total = bundle.Entry.Count;


            var json = _serializer.SerializeToString(bundle);

            return Content(json, "application/fhir+json");
        }

        [HttpPost]
        public virtual async Task<IActionResult> Create([FromBody] System.Text.Json.JsonElement body)
        {
            string jsonString = body.GetRawText();
            T resource;

            try
            {
                resource = _deserializer.Deserialize<T>(jsonString);
            }
            catch (DeserializationFailedException ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(T).Name);
                return BadRequest("Invalid FHIR content: " + ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(T).Name);
                return BadRequest("Invalid FHIR content: " + ex.Message);
            }

            if (resource == null) return BadRequest("No resource provided.");

            if (!await CanCreateResource(resource))
            {
                return Forbid();
            }

            var outcome = await _validator.ValidateAsync(resource);

            if (!outcome.Success)
            {
                return BadRequest(outcome);
            }

            try
            {
                var id = await _repository.CreateAsync(resource, _searchIndexer);
                Response.Headers.ETag = $"W/\"{resource.Meta?.VersionId}\"";
                Response.Headers.Location = $"/fhir/{typeof(T).Name}/{id}";

                return StatusCode(201, _serializer.SerializeToString(resource));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating {ResourceType}", typeof(T).Name);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPut("{id}")]
        public virtual async Task<IActionResult> Update(string id, [FromBody] System.Text.Json.JsonElement body)
        {
            var existingResource = await _repository.GetAsync<T>(id);
            if (existingResource == null)
            {
                return NotFound();
            }

            string jsonString = body.GetRawText();
            T resource;

            try
            {
                resource = _deserializer.Deserialize<T>(jsonString);
            }
            catch (DeserializationFailedException ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(T).Name);
                return BadRequest("Invalid FHIR content: " + ex.Message);
            }

            if (resource == null) return BadRequest("No resource provided.");
            if (id != resource.Id) return BadRequest("ID mismatch");

            if (!await CanUpdateResource(existingResource, resource))
            {
                return Forbid();
            }

            var outcome = await _validator.ValidateAsync(resource);
            if (!outcome.Success)
            {
                return BadRequest(outcome);
            }

            try
            {
                await _repository.UpdateAsync(id, resource, _searchIndexer);
                return Ok(_serializer.SerializeToString(resource));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating {ResourceType} {Id}", typeof(T).Name, id);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPatch("{id}")]
        public virtual async Task<IActionResult> Patch(string id, [FromBody] System.Text.Json.JsonElement body)
        {
            var existingResource = await _repository.GetAsync<T>(id);
            if (existingResource == null)
            {
                return NotFound();
            }

            var jsonString = body.GetRawText();
            T resource;

            try
            {
                resource = _deserializer.Deserialize<T>(jsonString);
            }
            catch (DeserializationFailedException ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(T).Name);
                return BadRequest("Invalid FHIR content: " + ex.Message);
            }

            if (resource == null)
            {
                return BadRequest("No resource provided.");
            }

            if (string.IsNullOrWhiteSpace(resource.Id))
            {
                resource.Id = id;
            }
            else if (!string.Equals(id, resource.Id, StringComparison.Ordinal))
            {
                return BadRequest("ID mismatch");
            }

            if (!await CanUpdateResource(existingResource, resource))
            {
                return Forbid();
            }

            var outcome = await _validator.ValidateAsync(resource);
            if (!outcome.Success)
            {
                return BadRequest(outcome);
            }

            try
            {
                await _repository.UpdateAsync(id, resource, _searchIndexer);
                return Ok(_serializer.SerializeToString(resource));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error patching {ResourceType} {Id}", typeof(T).Name, id);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpDelete("{id}")]
        public virtual async Task<IActionResult> Delete(string id)
        {
            var existingResource = await _repository.GetAsync<T>(id);
            if (existingResource == null)
            {
                return NotFound();
            }

            if (!await CanDeleteResource(existingResource))
            {
                return Forbid();
            }

            try
            {
                await _repository.DeleteAsync(typeof(T).Name, id);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting {ResourceType} {Id}", typeof(T).Name, id);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpGet("{id}/_history")]
        public virtual async Task<IActionResult> History(string id)
        {
            var resources = await _repository.GetHistoryAsync<T>(id);
            if (resources.Count == 0)
            {
                return NotFound();
            }

            var bundle = new Bundle
            {
                Type = Bundle.BundleType.History
            };

            foreach (var resource in resources)
            {
                if (await CanAccessResource(resource, "read", useResourceOwnership: true))
                {
                    bundle.Entry.Add(new Bundle.EntryComponent
                    {
                        Resource = resource,
                        FullUrl = $"{Request.Scheme}://{Request.Host}/fhir/{typeof(T).Name}/{resource.Id}/_history/{resource.Meta.VersionId}",
                        Request = new Bundle.RequestComponent
                        {
                            Method = Bundle.HTTPVerb.PUT,
                            Url = $"{typeof(T).Name}/{resource.Id}"
                        },
                        Response = new Bundle.ResponseComponent
                        {
                            Status = "200 OK",
                            LastModified = resource.Meta.LastUpdated
                        }
                    });
                }
            }

            bundle.Total = bundle.Entry.Count;

            if (bundle.Total == 0)
            {
                return Forbid();
            }

            var json = _serializer.SerializeToString(bundle);

            return Content(json, "application/fhir+json");
        }

        protected async Task<bool> CanCreateResource(T resource)
        {
            var accessLevel = _authorizationService.GetAccessLevel(User, typeof(T).Name, "write");
            if (accessLevel == SmartAccessLevel.None)
            {
                return false;
            }

            if (accessLevel == SmartAccessLevel.User)
            {
                // Practitioner ownership is indirect and established only after PractitionerRole creation.
                if (resource is Practitioner)
                {
                    return true;
                }

                // Patient-owned clinical resources are writable by any staff caller across organizations,
                // mirroring the cross-organization read model: a treating clinician at any facility can
                // add to a patient's longitudinal record. Administrative types and the Patient record
                // itself remain org-scoped below.
                if (_authorizationService.IsCrossOrganizationWritableType(typeof(T).Name))
                {
                    return true;
                }

                var organizationIds = await _authorizationService.ResolveOrganizationIdsAsync(User);
                return await _authorizationService.IsResourceOwnedByOrganizationsAsync(resource, organizationIds);
            }

            if (accessLevel == SmartAccessLevel.System)
            {
                return true;
            }

            var patientId = _authorizationService.ResolvePatientId(User);
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return false;
            }

            var patientIndices = _searchIndexer.Extract(resource);
            return _authorizationService.IsResourceOwnedByPatient(resource, patientId, patientIndices);
        }

        protected async Task<bool> CanUpdateResource(T existingResource, T updatedResource)
        {
            var accessLevel = _authorizationService.GetAccessLevel(User, typeof(T).Name, "write");
            if (accessLevel == SmartAccessLevel.None)
            {
                return false;
            }

            if (accessLevel == SmartAccessLevel.System)
            {
                return true;
            }

            if (accessLevel == SmartAccessLevel.User)
            {
                if (string.IsNullOrWhiteSpace(existingResource.Id))
                {
                    return false;
                }

                // Patient-owned clinical resources are writable across organizations (see
                // CanCreateResource): a treating clinician may also correct or update the clinical
                // records they contribute to any patient's longitudinal record.
                if (_authorizationService.IsCrossOrganizationWritableType(typeof(T).Name))
                {
                    return true;
                }

                var organizationIds = await _authorizationService.ResolveOrganizationIdsAsync(User);
                if (organizationIds.Count == 0)
                {
                    return false;
                }

                var isCurrentOwned = await _authorizationService.IsResourceOwnedByOrganizationsAsync(typeof(T).Name, existingResource.Id, organizationIds);
                if (!isCurrentOwned)
                {
                    return false;
                }

                if (updatedResource is Practitioner)
                {
                    return true;
                }

                return await _authorizationService.IsResourceOwnedByOrganizationsAsync(updatedResource, organizationIds);
            }

            var patientId = _authorizationService.ResolvePatientId(User);
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return false;
            }

            var existingIndices = _searchIndexer.Extract(existingResource);
            if (!_authorizationService.IsResourceOwnedByPatient(existingResource, patientId, existingIndices))
            {
                return false;
            }

            var updatedResourceIndices = _searchIndexer.Extract(updatedResource);
            return _authorizationService.IsResourceOwnedByPatient(updatedResource, patientId, updatedResourceIndices);
        }

        protected async Task<bool> CanDeleteResource(T existingResource)
        {
            var accessLevel = _authorizationService.GetAccessLevel(User, typeof(T).Name, "delete");
            if (accessLevel == SmartAccessLevel.None)
            {
                return false;
            }

            if (accessLevel == SmartAccessLevel.System)
            {
                return true;
            }

            if (accessLevel == SmartAccessLevel.User)
            {
                if (string.IsNullOrWhiteSpace(existingResource.Id))
                {
                    return false;
                }

                var organizationIds = await _authorizationService.ResolveOrganizationIdsAsync(User);
                return await _authorizationService.IsResourceOwnedByOrganizationsAsync(typeof(T).Name, existingResource.Id, organizationIds);
            }

            var patientId = _authorizationService.ResolvePatientId(User);
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return false;
            }

            var indices = _searchIndexer.Extract(existingResource);
            return _authorizationService.IsResourceOwnedByPatient(existingResource, patientId, indices);
        }

        /// <summary>
        /// Filters a search result set to the resources the caller may read. For org-scoped (User)
        /// callers this resolves the accessible patient-id set ONCE and checks ownership in-memory via
        /// the search index, instead of reloading each resource (and its patient) from the database per
        /// row — that N+1 made org-admin searches of large collections take tens of seconds.
        /// Semantics are unchanged: a patient-owned resource is accessible iff its patient's managing
        /// organization is one of the caller's, which is exactly membership in the accessible-patient set.
        /// </summary>
        private async Task<List<T>> FilterReadableAsync(IReadOnlyList<T> resources)
        {
            var accessLevel = _authorizationService.GetAccessLevel(User, typeof(T).Name, "read");

            if (accessLevel == SmartAccessLevel.None)
            {
                return [];
            }

            if (accessLevel == SmartAccessLevel.System)
            {
                return resources.ToList();
            }

            if (accessLevel == SmartAccessLevel.User)
            {
                // Staff may read any patient + clinical record across orgs (search is always a read);
                // return the full result set for those types. Write/delete remain org-scoped elsewhere.
                if (_authorizationService.IsCrossOrganizationReadableType(typeof(T).Name))
                {
                    return resources.ToList();
                }

                var organizationIds = await _authorizationService.ResolveOrganizationIdsAsync(User);
                if (organizationIds.Count == 0)
                {
                    return [];
                }

                var accessiblePatientIds = await _authorizationService.ResolveAccessiblePatientIdsAsync(User) ?? [];
                var patientIdSet = accessiblePatientIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

                var result = new List<T>();
                foreach (var resource in resources)
                {
                    var indices = _searchIndexer.Extract(resource);

                    // Direct org ownership: Organization (by id), PractitionerRole, Location,
                    // Patient (managingOrganization) — all carry an in-memory "organization" index.
                    if (_authorizationService.IsResourceOwnedByOrganizations(resource, indices, organizationIds))
                    {
                        result.Add(resource);
                        continue;
                    }

                    // Patient-owned resources (clinical + appointments) index their patient under
                    // "patient"; membership in the accessible-patient set is equivalent to org ownership.
                    if (indices.Any(i => i.SearchParamCode == "patient" && patientIdSet.Contains(ExtractReferenceId(i.Value))))
                    {
                        result.Add(resource);
                        continue;
                    }

                    // Types whose org ownership isn't resolvable in-memory (e.g. Practitioner via
                    // PractitionerRole) fall back to the authoritative DB check. These collections are
                    // small, so the per-row cost is acceptable.
                    if (!string.IsNullOrWhiteSpace(resource.Id)
                        && await _authorizationService.IsResourceOwnedByOrganizationsAsync(typeof(T).Name, resource.Id, organizationIds))
                    {
                        result.Add(resource);
                    }
                }

                return result;
            }

            // Patient-level caller: keep the existing in-memory patient-ownership check.
            var patientId = _authorizationService.ResolvePatientId(User);
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return [];
            }

            var patientResult = new List<T>();
            foreach (var resource in resources)
            {
                var indices = _searchIndexer.Extract(resource);
                if (_authorizationService.IsResourceOwnedByPatient(resource, patientId, indices))
                {
                    patientResult.Add(resource);
                }
            }

            return patientResult;
        }

        private static string ExtractReferenceId(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return string.Empty;
            }

            var slash = reference.IndexOf('/');
            return slash >= 0 && slash < reference.Length - 1 ? reference[(slash + 1)..] : reference;
        }

        protected async Task<bool> CanAccessResource(T resource, string action, bool useResourceOwnership = false)
        {
            var accessLevel = _authorizationService.GetAccessLevel(User, typeof(T).Name, action);
            if (accessLevel == SmartAccessLevel.None)
            {
                return false;
            }

            if (accessLevel == SmartAccessLevel.System)
            {
                return true;
            }

            if (accessLevel == SmartAccessLevel.User)
            {
                // Staff may read any patient + clinical record across orgs; write/delete stay org-scoped.
                if (action == "read" && _authorizationService.IsCrossOrganizationReadableType(typeof(T).Name))
                {
                    return true;
                }

                var organizationIds = await _authorizationService.ResolveOrganizationIdsAsync(User);

                if (!useResourceOwnership && !string.IsNullOrWhiteSpace(resource.Id))
                {
                    return await _authorizationService.IsResourceOwnedByOrganizationsAsync(typeof(T).Name, resource.Id, organizationIds);
                }

                return await _authorizationService.IsResourceOwnedByOrganizationsAsync(resource, organizationIds);
            }

            var patientId = _authorizationService.ResolvePatientId(User);
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return false;
            }

            var resourceIndices = _searchIndexer.Extract(resource);
            return _authorizationService.IsResourceOwnedByPatient(resource, patientId, resourceIndices);
        }
    }
}



