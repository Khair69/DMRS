namespace DMRS.Client.Features.Staff.Models;

public sealed record StaffSummaryViewModel(
    string PractitionerId,
    string PractitionerRoleId,
    string DisplayName,
    string Email,
    string? Phone,
    bool Active,
    string RoleCode,
    string RoleDisplay);
