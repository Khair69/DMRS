using DMRS.Client.Services;
using Microsoft.AspNetCore.Components;

namespace DMRS.Client.Components;

/// <summary>
/// Base class for components that display translated text. Inherit this instead of
/// <see cref="ComponentBase"/> and use <see cref="Loc"/> for lookups; the component
/// re-renders automatically whenever the active language changes.
/// </summary>
public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    [Inject]
    protected LocalizationService Loc { get; set; } = default!;

    protected override void OnInitialized()
    {
        Loc.OnChange += OnLanguageChanged;
    }

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    public virtual void Dispose()
    {
        Loc.OnChange -= OnLanguageChanged;
    }
}
