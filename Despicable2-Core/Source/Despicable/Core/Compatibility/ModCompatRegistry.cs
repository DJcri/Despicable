using System;
using System.Collections.Generic;
using Verse;

namespace Despicable.Core.Compatibility;
/// <summary>
/// Central runner for optional compat modules. This keeps "detect, activate, report"
/// behavior consistent across assemblies and avoids bespoke one-off bootstraps.
/// </summary>
public static class ModCompatRegistry
{
    private static readonly HashSet<string> Activated = new();

    public static void EnsureRegistered(IModCompat compat, string logPrefix)
    {
        if (compat == null)
            return;

        string key = compat.Id ?? compat.GetType().FullName ?? "UnknownCompat";
        if (Activated.Contains(key))
            return;

        bool canActivate;
        try
        {
            canActivate = compat.CanActivate();
        }
        catch (Exception e)
        {
            Log.Warning($"{logPrefix} Compat '{key}' detection failed: {e}");
            return;
        }

        if (!canActivate)
        {
            SafeReport(compat, logPrefix, key, "inactive");
            return;
        }

        if (!Activated.Add(key))
            return;

        try
        {
            compat.Activate();
            SafeReport(compat, logPrefix, key, "active");
        }
        catch (Exception e)
        {
            Log.Error($"{logPrefix} Compat '{key}' activation failed: {e}");
        }
    }

    private static void SafeReport(IModCompat compat, string logPrefix, string key, string fallbackState)
    {
        try
        {
            string status = compat.ReportStatus();
            if (!string.IsNullOrEmpty(status))
            {
                Log.Message($"{logPrefix} Compat '{key}': {status}");
                return;
            }
        }
        catch (Exception e)
        {
            Log.Warning($"{logPrefix} Compat '{key}' status reporting failed: {e}");
            return;
        }

        Log.Message($"{logPrefix} Compat '{key}': {fallbackState}");
    }
}
