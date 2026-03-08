using Verse;

namespace Despicable;
/// <summary>
/// Shared settings access for runtime helpers.
/// Keep CommonUtil focused on cross-cutting compatibility helpers only.
/// </summary>
public static partial class CommonUtil
{
    public static Settings GetSettings()
    {
        ModMain mod = ModMain.Instance;
        if (mod == null)
        {
            // Return a default settings object to prevent NullReferenceException
            return new Settings();
        }

        return mod.GetSettings<Settings>();
    }
}
