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

            foreach (var resource in resources)
            {
                if (await CanAccessResource(resource, "read"))
                {
                    bundle.Entry.Add(new Bundle.EntryComponent
                    {
                        Resource = resource,
                        FullUrl = $"{Request.Scheme}://{Request.Host}/fhir/{typeof(T).Name}/{resource.Id}"
                    });
                }
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



