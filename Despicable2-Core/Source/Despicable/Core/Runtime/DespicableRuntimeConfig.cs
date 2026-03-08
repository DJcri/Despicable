namespace Despicable;
/// <summary>
/// Centralized runtime config surface for internal debug logging.
/// Player-facing diagnostics toggles have been retired for the release build.
/// </summary>
public static class DespicableRuntimeConfig
{
    public static bool DebugLoggingEnabled => false;
}
