using DMRS.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.Security;

/// <summary>
/// Lightweight "current user" context for the SPA. The client can read some claims directly from the
/// token, but organization membership for staff is established through FHIR data (Keycloak user →
/// Practitioner identifier → PractitionerRole → Organization) and is not present as a token claim, so
/// it must be resolved server-side.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController : ControllerBase
{
    private readonly ISmartAuthorizationService _authorizationService;

    public MeController(ISmartAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    /// <summary>Returns the organization ids the caller belongs to (empty if none).</summary>
    [HttpGet("organizations")]
    public async Task<ActionResult<IReadOnlyCollection<string>>> GetOrganizations()
    {
        var organizationIds = await _authorizationService.ResolveOrganizationIdsAsync(User);
        return Ok(organizationIds);
    }
}
