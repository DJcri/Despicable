using System.Collections.Generic;

namespace Despicable;

/// <summary>
/// Additive request container for opening a vanilla FloatMenu through the shared manual-interaction mini-framework.
/// Existing callers can ignore this until they are ready to migrate from ad hoc option lists.
/// </summary>
public sealed class ManualMenuRequest
{
    public string Title { get; set; }

    public List<ManualMenuOptionSpec> Options { get; } = new();

    /// <summary>
    /// UI-origin menus should usually keep this false so the menu behaves like UI, not a colonist order picker.
    /// </summary>
    public bool GivesColonistOrders { get; set; }

    /// <summary>
    /// UI-origin menus should usually keep this false for stability while hovering extra parts.
    /// </summary>
    public bool VanishIfMouseDistant { get; set; }

    public ManualMenuRequest()
    {
        GivesColonistOrders = false;
        VanishIfMouseDistant = false;
    }

    public ManualMenuRequest(string title, IEnumerable<ManualMenuOptionSpec> options = null) : this()
    {
        Title = title;
        if (options == null)
            return;

        foreach (ManualMenuOptionSpec option in options)
            Options.Add(option);
    }
}
