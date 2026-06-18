using System.Collections.Generic;
using System.Globalization;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace DMRS.Client.Services;

/// <summary>
/// Runtime localization for the client. Loads per-language JSON dictionaries from
/// <c>wwwroot/i18n</c>, tracks the active language, and lets components look up
/// translated strings via the indexer (<c>Loc["key"]</c>). Switching language is
/// instant — no page reload — and components re-render by subscribing to
/// <see cref="OnChange"/> (see <see cref="DMRS.Client.Components.LocalizedComponentBase"/>).
/// </summary>
public sealed class LocalizationService
{
    public const string DefaultLanguage = "en";

    private static readonly IReadOnlyList<LanguageOption> SupportedLanguages =
    [
        new LanguageOption("en", "English", Rtl: false),
        new LanguageOption("ar", "العربية", Rtl: true),
    ];

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    // language -> (key -> value). Loaded once on startup.
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new();

    public LocalizationService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    /// <summary>Raised after the active language changes so subscribers can re-render.</summary>
    public event Action? OnChange;

    public string CurrentLanguage { get; private set; } = DefaultLanguage;

    public bool IsRtl => GetOption(CurrentLanguage).Rtl;

    public IReadOnlyList<LanguageOption> Languages => SupportedLanguages;

    /// <summary>Translated string for <paramref name="key"/>, falling back to the key itself.</summary>
    public string this[string key] => Translate(key);

    /// <summary>Translated string with positional <c>{0}</c>, <c>{1}</c>… arguments filled in.</summary>
    public string this[string key, params object?[] args]
        => string.Format(GetOption(CurrentLanguage).Culture, Translate(key), args);

    /// <summary>
    /// Loads every language dictionary and applies the persisted (or browser-preferred) language.
    /// Call once during host startup, before the app renders.
    /// </summary>
    public async Task InitializeAsync()
    {
        foreach (var option in SupportedLanguages)
        {
            try
            {
                var dict = await _http.GetFromJsonAsync<Dictionary<string, string>>($"i18n/{option.Code}.json");
                _translations[option.Code] = dict ?? new Dictionary<string, string>();
            }
            catch
            {
                // A missing/invalid dictionary just means that language falls back to keys.
                _translations[option.Code] = new Dictionary<string, string>();
            }
        }

        var saved = await _js.InvokeAsync<string?>("dmrsLocalization.get");
        var language = !string.IsNullOrWhiteSpace(saved) && IsSupported(saved)
            ? saved!
            : DefaultLanguage;

        CurrentLanguage = language;
        await ApplyDocumentAsync(language);
    }

    /// <summary>Switches the active language, persists it, flips RTL, and notifies subscribers.</summary>
    public async Task SetLanguageAsync(string language)
    {
        if (!IsSupported(language) || language == CurrentLanguage)
        {
            return;
        }

        CurrentLanguage = language;

        var option = GetOption(language);
        CultureInfo.DefaultThreadCurrentCulture = option.Culture;
        CultureInfo.DefaultThreadCurrentUICulture = option.Culture;

        await _js.InvokeVoidAsync("dmrsLocalization.set", language);
        await ApplyDocumentAsync(language);

        OnChange?.Invoke();
    }

    private async Task ApplyDocumentAsync(string language)
    {
        var option = GetOption(language);
        CultureInfo.DefaultThreadCurrentCulture = option.Culture;
        CultureInfo.DefaultThreadCurrentUICulture = option.Culture;
        await _js.InvokeVoidAsync("dmrsLocalization.apply", language, option.Rtl);
    }

    private string Translate(string key)
    {
        if (_translations.TryGetValue(CurrentLanguage, out var dict)
            && dict.TryGetValue(key, out var value))
        {
            return value;
        }

        // Fall back to English, then to the raw key, so untranslated strings stay readable.
        if (CurrentLanguage != DefaultLanguage
            && _translations.TryGetValue(DefaultLanguage, out var fallback)
            && fallback.TryGetValue(key, out var fallbackValue))
        {
            return fallbackValue;
        }

        return key;
    }

    private static bool IsSupported(string language)
        => SupportedLanguages.Any(l => l.Code == language);

    private static LanguageOption GetOption(string language)
        => SupportedLanguages.FirstOrDefault(l => l.Code == language)
            ?? SupportedLanguages[0];
}

/// <summary>A selectable UI language.</summary>
/// <param name="Code">BCP-47 language code (e.g. <c>en</c>, <c>ar</c>).</param>
/// <param name="DisplayName">Name shown in the switcher, in its own language.</param>
/// <param name="Rtl">Whether the language reads right-to-left.</param>
public sealed record LanguageOption(string Code, string DisplayName, bool Rtl)
{
    public CultureInfo Culture { get; } = CultureInfo.GetCultureInfo(Code);
}
