using DMRS.Api.Infrastructure.Security;
using DMRS.UnitTests.TestDoubles;
using Shouldly;

namespace DMRS.UnitTests.Security;

/// <summary>
/// Covers <see cref="SmartAuthorizationService.GetAccessLevel"/> — the function that decides whether a
/// caller acts at patient, user (organization) or system level. It reads only the token's claims, so
/// the service is built with no database here.
/// </summary>
public class SmartAccessLevelTests
{
    private static SmartAuthorizationService CreateService()
        // GetAccessLevel is a pure function over the ClaimsPrincipal — it touches neither the
        // DbContext nor the ownership resolver, so both dependencies stay unused in these tests.
        => new(dbContext: null!, ownershipResolver: null!);

    [Fact]
    public void Caller_without_any_scope_gets_no_access()
    {
        var level = CreateService().GetAccessLevel(TestPrincipal.Anonymous(), "Patient", "read");

        level.ShouldBe(SmartAccessLevel.None);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("delete")]
    public void Practitioner_acts_at_user_level_for_every_action(string action)
    {
        var level = CreateService().GetAccessLevel(TestPrincipal.Practitioner(), "Patient", action);

        level.ShouldBe(SmartAccessLevel.User);
    }

    [Fact]
    public void System_admin_acts_at_system_level()
    {
        var level = CreateService().GetAccessLevel(TestPrincipal.SystemAdmin(), "Patient", "read");

        level.ShouldBe(SmartAccessLevel.System);
    }

    /// <summary>
    /// Regression test for a real defect. The Keycloak realm grants user/*.* and system/*.* as DEFAULT
    /// client scopes, so a patient's token carries them too. Before the role gate was added, a patient
    /// matched the user branch and was treated as an organization caller — belonging to no organization,
    /// they saw nothing, and the patient-ownership check that confines them to their own record was
    /// skipped entirely. Access level must be driven by the role, not by the scope alone.
    /// </summary>
    [Fact]
    public void Patient_holding_the_realms_default_user_scope_is_still_patient_level()
    {
        var patient = TestPrincipal.Patient("patient-1").Build();

        patient.HasClaim(c => c.Type == "scope" && c.Value.Contains("user/*.*")).ShouldBeTrue(
            "the test principal must reproduce the realm's default scopes for this test to mean anything");

        CreateService().GetAccessLevel(patient, "Patient", "read").ShouldBe(SmartAccessLevel.Patient);
    }

    [Fact]
    public void System_level_requires_the_system_admin_role_not_just_the_system_scope()
    {
        // A practitioner's token carries system/*.* by default, but lacks ROLE_SYSTEM_ADMIN.
        var level = CreateService().GetAccessLevel(TestPrincipal.Practitioner(), "Organization", "delete");

        level.ShouldBe(SmartAccessLevel.User);
    }

    [Theory]
    [InlineData("patient/Observation.read", "Observation", "read", SmartAccessLevel.Patient)]
    [InlineData("patient/Observation.read", "Observation", "write", SmartAccessLevel.None)]
    [InlineData("patient/Observation.read", "Condition", "read", SmartAccessLevel.None)]
    [InlineData("patient/*.read", "Condition", "read", SmartAccessLevel.Patient)]
    [InlineData("patient/Observation.*", "Observation", "delete", SmartAccessLevel.Patient)]
    public void Scope_narrowing_is_honoured_per_resource_and_action(
        string scope, string resourceType, string action, SmartAccessLevel expected)
    {
        var principal = TestPrincipal.Create().WithScopes(scope).WithRoles(TestPrincipal.RolePatient);

        CreateService().GetAccessLevel(principal, resourceType, action).ShouldBe(expected);
    }

    [Theory]
    [InlineData("patient/Observation.create")]
    [InlineData("patient/Observation.update")]
    [InlineData("patient/Observation.delete")]
    public void Granular_v2_scopes_satisfy_a_write_request(string scope)
    {
        var principal = TestPrincipal.Create().WithScopes(scope).WithRoles(TestPrincipal.RolePatient);

        CreateService().GetAccessLevel(principal, "Observation", "write").ShouldBe(SmartAccessLevel.Patient);
    }

    [Fact]
    public void Resource_type_matching_is_case_insensitive()
    {
        var principal = TestPrincipal.Create().WithScopes("patient/observation.read").WithRoles(TestPrincipal.RolePatient);

        CreateService().GetAccessLevel(principal, "Observation", "read").ShouldBe(SmartAccessLevel.Patient);
    }

    [Theory]
    [InlineData("Patient", true)]
    [InlineData("Observation", true)]
    [InlineData("MedicationRequest", true)]
    [InlineData("Organization", false)]
    [InlineData("Practitioner", false)]
    [InlineData("PractitionerRole", false)]
    public void Only_the_patient_record_and_clinical_types_are_readable_across_organizations(
        string resourceType, bool expected)
    {
        CreateService().IsCrossOrganizationReadableType(resourceType).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Observation", true)]
    [InlineData("Condition", true)]
    // The Patient record itself is readable across organizations but NOT writable — demographic
    // edits stay with the managing organization.
    [InlineData("Patient", false)]
    [InlineData("Organization", false)]
    public void Cross_organization_writes_exclude_the_patient_record_and_administrative_types(
        string resourceType, bool expected)
    {
        CreateService().IsCrossOrganizationWritableType(resourceType).ShouldBe(expected);
    }
}
