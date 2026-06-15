namespace DMRS.Client.Components;

/// <summary>
/// One option in a resource Index page's "Search Field" dropdown: the FHIR search parameter
/// (<see cref="Value"/>), the label shown to the user, and an example placeholder.
/// </summary>
public sealed record SearchField(string Value, string Label, string? Placeholder = null);
