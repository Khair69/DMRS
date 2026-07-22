using DMRS.Api.Infrastructure.Security;
using DMRS.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using System.Security.Claims;

namespace DMRS.UnitTests.Security;

/// <summary>
/// Covers <see cref="FhirScopeHandler"/> — the authorization handler every FHIR request passes
/// through. It decides, from the caller's access level and the requested route, whether the caller
/// may touch that specific resource instance. The ownership lookups are substituted so these tests
/// exercise the decision logic alone.
/// </summary>
public class FhirScopeHandlerTests
{
    private const string PatientId = "patient-1";
    private const string OtherPatientId = "patient-2";

    private readonly ISmartAuthorizationService _authorization = Substitute.For<ISmartAuthorizationService>();

    /// <summary>Runs the handler against a request and reports whether the requirement was met.</summary>
    private async Task<bool> IsAllowedAsync(
        ClaimsPrincipal user,
        string method,
        string controller,
        string? id = null,
        string path = "/api/fhir")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Path = path;
        httpContext.Request.RouteValues["controller"] = controller;
        if (id is not null)
        {
            httpContext.Request.RouteValues["id"] = id;
        }

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var requirement = new FhirScopeRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new FhirScopeHandler(accessor, _authorization).HandleAsync(context);

        return context.HasSucceeded;
    }

    private void GivenAccessLevel(SmartAccessLevel level) =>
        _authorization.GetAccessLevel(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(level);

    // ---------------------------------------------------------------- gate: caller has no access

    [Fact]
    public async Task Request_is_denied_when_the_caller_has_no_matching_scope()
    {
        GivenAccessLevel(SmartAccessLevel.None);

        var allowed = await IsAllowedAsync(TestPrincipal.Anonymous(), "GET", "Patient", PatientId);

        allowed.ShouldBeFalse();
    }

    [Fact]
    public async Task Request_with_an_unsupported_method_is_denied()
    {
        GivenAccessLevel(SmartAccessLevel.User);

        var allowed = await IsAllowedAsync(TestPrincipal.Practitioner(), "OPTIONS", "Patient", PatientId);

        allowed.ShouldBeFalse();
    }

    [Fact]
    public async Task System_caller_is_allowed_without_any_ownership_check()
    {
        GivenAccessLevel(SmartAccessLevel.System);

        var allowed = await IsAllowedAsync(TestPrincipal.SystemAdmin(), "DELETE", "Organization", "org-1");

        allowed.ShouldBeTrue();
        await _authorization.DidNotReceive().IsResourceOwnedByOrganizationsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>());
    }

    // ---------------------------------------------------------------- patient level

    [Fact]
    public async Task Patient_may_read_their_own_record()
    {
        GivenAccessLevel(SmartAccessLevel.Patient);
        _authorization.ResolvePatientId(Arg.Any<ClaimsPrincipal>()).Returns(PatientId);
        _authorization.IsResourceOwnedByPatientAsync("Patient", PatientId, PatientId).Returns(true);

        var allowed = await IsAllowedAsync(TestPrincipal.Patient(PatientId), "GET", "Patient", PatientId);

        allowed.ShouldBeTrue();
    }

    /// <summary>
    /// The headline guarantee of the patient scope: a signed-in patient who edits the id in the URL
    /// to point at somebody else's record is refused, because authorization is enforced on the server
    /// against the patient id carried by the token — not by what the client asks for.
    /// </summary>
    [Fact]
    public async Task Patient_tampering_with_the_id_cannot_reach_another_patients_record()
    {
        GivenAccessLevel(SmartAccessLevel.Patient);
        _authorization.ResolvePatientId(Arg.Any<ClaimsPrincipal>()).Returns(PatientId);
        _authorization.IsResourceOwnedByPatientAsync("Patient", OtherPatientId, PatientId).Returns(false);

        var allowed = await IsAllowedAsync(TestPrincipal.Patient(PatientId), "GET", "Patient", OtherPatientId);

        allowed.ShouldBeFalse();
    }

    [Fact]
    public async Task Patient_cannot_read_an_observation_belonging_to_another_patient()
    {
        GivenAccessLevel(SmartAccessLevel.Patient);
        _authorization.ResolvePatientId(Arg.Any<ClaimsPrincipal>()).Returns(PatientId);
        _authorization.IsResourceOwnedByPatientAsync("Observation", "obs-9", PatientId).Returns(false);

        var allowed = await IsAllowedAsync(TestPrincipal.Patient(PatientId), "GET", "Observation", "obs-9");

        allowed.ShouldBeFalse();
    }

    [Fact]
    public async Task Patient_whose_token_resolves_to_no_patient_record_is_denied()
    {
        GivenAccessLevel(SmartAccessLevel.Patient);
        _authorization.ResolvePatientId(Arg.Any<ClaimsPrincipal>()).Returns((string?)null);

        var allowed = await IsAllowedAsync(TestPrincipal.Patient(), "GET", "Patient", PatientId);

        allowed.ShouldBeFalse();
    }

    [Fact]
    public async Task Patient_may_issue_a_type_level_search_which_is_narrowed_further_downstream()
    {
        GivenAccessLevel(SmartAccessLevel.Patient);
        _authorization.ResolvePatientId(Arg.Any<ClaimsPrincipal>()).Returns(PatientId);

        var allowed = await IsAllowedAsync(TestPrincipal.Patient(PatientId), "GET", "Observation");

        allowed.ShouldBeTrue();
        await _authorization.DidNotReceive().IsResourceOwnedByPatientAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ---------------------------------------------------------------- user (organization) level

    [Fact]
    public async Task Practitioner_may_read_a_patient_from_another_organization()
    {
        GivenAccessLevel(SmartAccessLevel.User);
        _authorization.IsCrossOrganizationReadableType("Patient").Returns(true);

        var allowed = await IsAllowedAsync(TestPrincipal.Practitioner(), "GET", "Patient", OtherPatientId);

        allowed.ShouldBeTrue("a treating clinician must be able to view any patient's record");
        await _authorization.DidNotReceive().ResolveOrganizationIdsAsync(Arg.Any<ClaimsPrincipal>());
    }

    [Fact]
    public async Task Practitioner_may_write_a_clinical_resource_for_a_patient_of_another_organization()
    {
        GivenAccessLevel(SmartAccessLevel.User);
        _authorization.IsCrossOrganizationWritableType("Observation").Returns(true);

        var allowed = await IsAllowedAsync(TestPrincipal.Practitioner(), "PUT", "Observation", "obs-9");

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task Practitioner_cannot_read_an_administrative_resource_of_another_organization()
    {
        GivenAccessLevel(SmartAccessLevel.User);
        _authorization.IsCrossOrganizationReadableType("Organization").Returns(false);
        _authorization.ResolveOrganizationIdsAsync(Arg.Any<ClaimsPrincipal>()).Returns(["org-1"]);
        _authorization.IsResourceOwnedByOrganizationsAsync("Organization", "org-2", Arg.Any<IReadOnlyCollection<string>>())
            .Returns(false);

        var allowed = await IsAllowedAsync(TestPrincipal.Practitioner(), "GET", "Organization", "org-2");

        allowed.ShouldBeFalse();
    }

    /// <summary>
    /// Deletion is never relaxed across organizations, even for a type that staff may read and write
    /// across them — so the organization ownership check must still run for a DELETE.
    /// </summary>
    [Fact]
    public async Task Practitioner_cannot_delete_a_clinical_resource_owned_by_another_organization()
    {
        GivenAccessLevel(SmartAccessLevel.User);
        _authorization.IsCrossOrganizationReadableType("Observation").Returns(true);
        _authorization.IsCrossOrganizationWritableType("Observation").Returns(true);
        _authorization.ResolveOrganizationIdsAsync(Arg.Any<ClaimsPrincipal>()).Returns(["org-1"]);
        _authorization.IsResourceOwnedByOrganizationsAsync("Observation", "obs-9", Arg.Any<IReadOnlyCollection<string>>())
            .Returns(false);

        var allowed = await IsAllowedAsync(TestPrincipal.Practitioner(), "DELETE", "Observation", "obs-9");

        allowed.ShouldBeFalse();
    }

    [Fact]
    public async Task Practitioner_may_delete_a_resource_owned_by_their_own_organization()
    {
        GivenAccessLevel(SmartAccessLevel.User);
        _authorization.ResolveOrganizationIdsAsync(Arg.Any<ClaimsPrincipal>()).Returns(["org-1"]);
        _authorization.IsResourceOwnedByOrganizationsAsync("Observation", "obs-9", Arg.Any<IReadOnlyCollection<string>>())
            .Returns(true);

        var allowed = await IsAllowedAsync(TestPrincipal.Practitioner(), "DELETE", "Observation", "obs-9");

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task Creating_a_resource_is_not_treated_as_an_instance_request()
    {
        GivenAccessLevel(SmartAccessLevel.User);

        // A POST carrying a client-supplied id must not be gated on ownership of that id — the
        // resource does not exist yet.
        var allowed = await IsAllowedAsync(TestPrincipal.Practitioner(), "POST", "Organization", "org-new");

        allowed.ShouldBeTrue();
        await _authorization.DidNotReceive().IsResourceOwnedByOrganizationsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>());
    }
}
