namespace DMRS.Client.Features.Locations.Models;

public sealed record LocationSummaryViewModel(
    string Id,
    string Name,
    string Status,
    string? ManagingOrganizationId);
