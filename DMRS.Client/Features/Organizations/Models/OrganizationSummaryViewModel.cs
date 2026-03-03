namespace DMRS.Client.Features.Organizations.Models;

public sealed record OrganizationSummaryViewModel(
    string Id,
    string Name,
    bool Active,
    string? Identifier,
    string? Email);
