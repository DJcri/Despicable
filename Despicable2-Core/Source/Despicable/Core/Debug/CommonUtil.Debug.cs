using Verse;

namespace Despicable;
/// <summary>
/// Thin debug logging shim retained for compatibility across core and NSFW modules.
/// </summary>
public static partial class CommonUtil
{
    public static void DebugLog(string msg)
    {
        if (DespicableRuntimeConfig.DebugLoggingEnabled)
        {
            Log.Message(msg);
        }
    }
}
