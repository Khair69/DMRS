namespace DMRS.Shared.Constants;

/// <summary>
/// A Syrian governorate and its 2-letter code used as the prefix of the readable patient number.
/// </summary>
public sealed record SyrianGovernorate(string Code, string EnglishName, string ArabicName);

/// <summary>
/// Single source of truth for the governorate &rarr; prefix mapping. Shared between the client
/// (the city dropdown on the patient form) and the server (patient-number generation).
/// </summary>
public static class SyrianGovernorates
{
    /// <summary>Prefix used when the city is empty or not one of the known governorates.</summary>
    public const string UnknownPrefix = "XX";

    /// <summary>All 14 Syrian governorates and their patient-number prefixes.</summary>
    public static readonly IReadOnlyList<SyrianGovernorate> All =
    [
        new("HM", "Hama", "حماة"),
        new("HO", "Homs", "حمص"),
        new("DM", "Damascus", "دمشق"),
        new("RD", "Rif Dimashq", "ريف دمشق"),
        new("AP", "Aleppo", "حلب"),
        new("AQ", "Ar-Raqqa", "الرقة"),
        new("HK", "Hasaka", "الحسكة"),
        new("ID", "Idleb", "إدلب"),
        new("DZ", "Deir ez-Zor", "دير الزور"),
        new("LK", "Latakia", "اللاذقية"),
        new("TR", "Tartous", "طرطوس"),
        new("AS", "As-Suwayda", "السويداء"),
        new("DA", "Daraa", "درعا"),
        new("QN", "Quneitra", "القنيطرة"),
    ];

    /// <summary>
    /// Resolves a city/governorate name (English, Arabic, or its 2-letter code) to a prefix.
    /// Returns <see cref="UnknownPrefix"/> when the value is blank or unrecognised.
    /// </summary>
    public static string PrefixForCity(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return UnknownPrefix;
        }

        var trimmed = city.Trim();
        foreach (var g in All)
        {
            if (string.Equals(trimmed, g.Code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, g.EnglishName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, g.ArabicName, StringComparison.Ordinal))
            {
                return g.Code;
            }
        }

        return UnknownPrefix;
    }
}
