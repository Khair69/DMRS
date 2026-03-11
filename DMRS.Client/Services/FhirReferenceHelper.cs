namespace DMRS.Client.Services;

public static class FhirReferenceHelper
{
    public static string? NormalizeReference(string? value, string expectedType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Contains('/'))
        {
            return trimmed;
        }

        return $"{expectedType}/{trimmed}";
    }

    public static string? ExtractReferenceId(string? reference, string expectedType)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var trimmed = reference.Trim();
        var prefix = $"{expectedType}/";
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[prefix.Length..];
        }

        if (trimmed.Contains('/'))
        {
            return null;
        }

        return trimmed;
    }
}
