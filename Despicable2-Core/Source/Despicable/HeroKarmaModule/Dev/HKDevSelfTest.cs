using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

// Guardrail-Reason: Hero Karma self-test probes stay together so startup diagnostics remain one browseable developer surface.
namespace Despicable.HeroKarma;
/// <summary>
/// DevMode-only hook health check. Runs after startup to report which gameplay hooks exist
/// and whether they are patched by our Harmony instance.
/// </summary>
[StaticConstructorOnStartup]
public static class HKDevSelfTest
{
    // Must match ModMain/Bootstrap Harmony ID.
    private const string HarmonyId = "com.DCSzar.Despicable";

    static HKDevSelfTest()
    {
        if (!Prefs.DevMode) return;

        LongEventHandler.ExecuteWhenFinished(delegate
        {
            try { RunInternal(); }
            catch (Exception ex) { HKDiagnostics.Catch(ex, "HKDevSelfTest.RunInternal"); }
        });
    }

    /// <summary>Manually run the self-test (DevMode only).</summary>
    public static void RunNow()
    {
        if (!Prefs.DevMode) return;
        try { RunInternal(); }
        catch (Exception ex) { HKDiagnostics.Catch(ex, "HKDevSelfTest.RunNow"); }
    }

/// <summary>
/// Manually run the self-test with verbose output (lists overload signatures and whether we own a patch).
/// DevMode only.
/// </summary>
public static void RunNowVerbose()
{
    if (!Prefs.DevMode) return;
    try { RunInternalVerbose(); }
    catch (Exception ex) { HKDiagnostics.Catch(ex, "HKDevSelfTest.RunNowVerbose"); }
}

private static void RunInternalVerbose()
{
    var lines = new List<string>();
    lines.Add("[HeroKarma] Dev self-test (hook health, verbose):");

    // Same targets as RunInternal, but with overload listings.
    TestPatchVerbose(lines, "ExecutePrisoner", "RimWorld.ExecutionUtility", "DoExecutionByCut",
        new[] { "Verse.Pawn", "Verse.Pawn", "System.Int32", "System.Boolean" });

    TestPatchVerbose(lines, "TendOutsider", "RimWorld.TendUtility", "DoTend",
        new[] { "Verse.Pawn", "Verse.Pawn", "RimWorld.Medicine" });

    TestPatchVerbose(lines, "ReleasePrisoner", "Verse.AI.JobDriver_ReleasePrisoner", "TryMakePreToilReservations",
        null);

    TestPatchVerbose(lines, "EnslaveAttempt", "RimWorld.InteractionWorker_EnslaveAttempt", "Interacted",
        null);

    TestPatchVerbose(lines, "EnslaveAttempt (fallback)", "RimWorld.InteractionWorker_Enslave", "Interacted",
        null);

    TestPatchVerbose(lines, "OrganHarvest", "RimWorld.Recipe_RemoveBodyPart", "ApplyOnPawn",
        null);

    TestPatchVerbose(lines, "CharityGift (tradeables)", "RimWorld.Planet.FactionGiftUtility", "GiveGift", null);
    TestPatchVerbose(lines, "CharityGift (pods)", "RimWorld.Planet.FactionGiftUtility", "GiveGift", null);
    TestPatchVerbose(lines, "CharityGift (capture negotiator)", "RimWorld.Planet.FactionGiftUtility", "OfferGiftsCommand", null);

    TestPatchVerbose(lines, "AttackNeutral / HarmGuest / KillDownedNeutral", "Verse.Pawn_HealthTracker", "PostApplyDamage",
        new[] { "Verse.DamageInfo", "System.Single" });

    TestPatchVerbose(lines, "ArrestNeutral", "Verse.Pawn", "CheckAcceptArrest", new[] { "Verse.Pawn" });
    TestPatchVerbose(lines, "RescueOutsider", "RimWorld.JobDriver_TakeToBed", "TryMakePreToilReservations", null);
    TestOptionalPatchAnyVerbose(lines, "FreeSlave", new[] { "Verse.AI.JobDriver_EmancipateSlave", "Verse.AI.JobDriver_ReleaseSlave", "Verse.AI.JobDriver_FreeSlave", "RimWorld.JobDriver_EmancipateSlave", "RimWorld.JobDriver_ReleaseSlave", "RimWorld.JobDriver_FreeSlave" }, "TryMakePreToilReservations");
    TestOptionalPatchAnyVerbose(lines, "DonateToBeggars", new[] { "Verse.AI.JobDriver_GiveToPawn", "Verse.AI.JobDriver_DeliverToPawn", "Verse.AI.JobDriver_GiveToPackAnimal", "RimWorld.JobDriver_GiveToPawn", "RimWorld.JobDriver_DeliverToPawn", "RimWorld.JobDriver_GiveToPackAnimal" }, "TryMakePreToilReservations");
    TestPatchVerbose(lines, "SellCaptive", "RimWorld.TradeDeal", "TryExecute", null);

    Log.Message(string.Join("\n", lines.ToArray()));
}



    private static void RunInternal()
    {
        var lines = new List<string>();
        lines.Add("[HeroKarma] Dev self-test (hook health):");

        // 1.6 confirmed targets (ILSpy):
        TestPatch(lines, "ExecutePrisoner", "RimWorld.ExecutionUtility", "DoExecutionByCut",
            new[] { "Verse.Pawn", "Verse.Pawn", "System.Int32", "System.Boolean" }); // optional params exist, exact may fail, overload scan covers

        TestPatch(lines, "TendOutsider", "RimWorld.TendUtility", "DoTend",
            new[] { "Verse.Pawn", "Verse.Pawn", "RimWorld.Medicine" });

        TestPatch(lines, "ReleasePrisoner", "Verse.AI.JobDriver_ReleasePrisoner", "TryMakePreToilReservations",
            null);

        TestPatch(lines, "EnslaveAttempt", "RimWorld.InteractionWorker_EnslaveAttempt", "Interacted",
            null);

        TestPatch(lines, "EnslaveAttempt (fallback)", "RimWorld.InteractionWorker_Enslave", "Interacted",
            null);

        TestPatch(lines, "OrganHarvest", "RimWorld.Recipe_RemoveBodyPart", "ApplyOnPawn",
            null);

        TestPatch(lines, "CharityGift (tradeables)", "RimWorld.Planet.FactionGiftUtility", "GiveGift", null);
        TestPatch(lines, "CharityGift (pods)", "RimWorld.Planet.FactionGiftUtility", "GiveGift", null);
        TestPatch(lines, "CharityGift (capture negotiator)", "RimWorld.Planet.FactionGiftUtility", "OfferGiftsCommand", null);

        TestPatch(lines, "AttackNeutral / HarmGuest / KillDownedNeutral", "Verse.Pawn_HealthTracker", "PostApplyDamage",
            new[] { "Verse.DamageInfo", "System.Single" });

        TestPatch(lines, "ArrestNeutral", "Verse.Pawn", "CheckAcceptArrest", new[] { "Verse.Pawn" });
        TestPatch(lines, "RescueOutsider", "RimWorld.JobDriver_TakeToBed", "TryMakePreToilReservations", null);
        TestOptionalPatchAny(lines, "FreeSlave", new[] { "Verse.AI.JobDriver_EmancipateSlave", "Verse.AI.JobDriver_ReleaseSlave", "Verse.AI.JobDriver_FreeSlave", "RimWorld.JobDriver_EmancipateSlave", "RimWorld.JobDriver_ReleaseSlave", "RimWorld.JobDriver_FreeSlave" }, "TryMakePreToilReservations");
        TestOptionalPatchAny(lines, "DonateToBeggars", new[] { "Verse.AI.JobDriver_GiveToPawn", "Verse.AI.JobDriver_DeliverToPawn", "Verse.AI.JobDriver_GiveToPackAnimal", "RimWorld.JobDriver_GiveToPawn", "RimWorld.JobDriver_DeliverToPawn", "RimWorld.JobDriver_GiveToPackAnimal" }, "TryMakePreToilReservations");
        TestPatch(lines, "SellCaptive", "RimWorld.TradeDeal", "TryExecute", null);

        Log.Message(string.Join("\n", lines.ToArray()));
    }

    private static void TestPatch(List<string> lines, string label, string typeName, string methodName, string[] paramTypeNamesOrNull)
    {
        Type t = AccessTools.TypeByName(typeName);
        if (t == null)
        {
            lines.Add("  ❌ " + label + " | type missing: " + typeName);
            return;
        }

        var candidates = new List<MethodBase>();

        try
        {
            // Exact signature attempt (best effort, never fatal).
            if (paramTypeNamesOrNull != null)
            {
                Type[] parms = ResolveParamTypes(paramTypeNamesOrNull);
                if (parms == null)
                {
                    lines.Add("  ⚠️ " + label + " | param types unresolved (exact signature check skipped)");
                }
                else
                {
                    MethodBase exact = AccessTools.Method(t, methodName, parms);
                    if (exact != null) candidates.Add(exact);
                }
            }

            // Always scan overloads by name to avoid AmbiguousMatchException.
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (m == null) continue;
                if (!string.Equals(m.Name, methodName, StringComparison.Ordinal)) continue;
                if (!Contains(candidates, m)) candidates.Add(m);
            }
        }
        catch (Exception ex)
        {
            lines.Add("  ⚠️ " + label + " | reflection error: " + ex.GetType().Name);
            return;
        }

        if (candidates.Count == 0)
        {
            lines.Add("  ❌ " + label + " | method missing: " + typeName + "." + methodName);
            return;
        }

        bool anyPatchedByUs = false;
        bool anyPatchedAtAll = false;

        for (int i = 0; i < candidates.Count; i++)
        {
            MethodBase m = candidates[i];
            var info = Harmony.GetPatchInfo(m);
            if (info == null) continue;

            anyPatchedAtAll = true;

            bool ours =
                HasOwner(info.Prefixes, HarmonyId) ||
                HasOwner(info.Postfixes, HarmonyId) ||
                HasOwner(info.Transpilers, HarmonyId) ||
                HasOwner(info.Finalizers, HarmonyId);

            if (ours) { anyPatchedByUs = true; break; }
        }

        if (anyPatchedByUs)
            lines.Add("  ✅ " + label + " | patched by us (" + candidates.Count + " overloads checked)");
        else if (anyPatchedAtAll)
            lines.Add("  ⚠️ " + label + " | patched, but NOT by us (" + candidates.Count + " overloads checked)");
        else
            lines.Add("  ⚠️ " + label + " | method exists but has no patches (" + candidates.Count + " overloads found)");
    }

    private static void TestOptionalPatchAny(List<string> lines, string label, string[] typeNames, string methodName)
    {
        if (typeNames == null || typeNames.Length == 0)
        {
            lines.Add("  ℹ️ " + label + " | optional target list empty");
            return;
        }

        var candidates = new List<MethodBase>();
        for (int i = 0; i < typeNames.Length; i++)
        {
            string typeName = typeNames[i];
            Type t = AccessTools.TypeByName(typeName);
            if (t == null) continue;

            try
            {
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (m == null) continue;
                    if (!string.Equals(m.Name, methodName, StringComparison.Ordinal)) continue;
                    if (!Contains(candidates, m)) candidates.Add(m);
                }
            }
            catch (Exception ex)
            {
                lines.Add("  ⚠️ " + label + " | reflection error: " + ex.GetType().Name);
                return;
            }
        }

        if (candidates.Count == 0)
        {
            lines.Add("  ℹ️ " + label + " | optional target unavailable in current environment");
            return;
        }

        bool anyPatchedByUs = false;
        bool anyPatchedAtAll = false;
        for (int i = 0; i < candidates.Count; i++)
        {
            MethodBase m = candidates[i];
            var info = Harmony.GetPatchInfo(m);
            if (info == null) continue;

            anyPatchedAtAll = true;
            bool ours =
                HasOwner(info.Prefixes, HarmonyId) ||
                HasOwner(info.Postfixes, HarmonyId) ||
                HasOwner(info.Transpilers, HarmonyId) ||
                HasOwner(info.Finalizers, HarmonyId);

            if (ours) { anyPatchedByUs = true; break; }
        }

        if (anyPatchedByUs)
            lines.Add("  ✅ " + label + " | patched by us (" + candidates.Count + " optional targets checked)");
        else if (anyPatchedAtAll)
            lines.Add("  ⚠️ " + label + " | optional targets patched, but NOT by us (" + candidates.Count + " methods checked)");
        else
            lines.Add("  ⚠️ " + label + " | optional target exists but has no patches (" + candidates.Count + " methods found)");
    }


private static void TestPatchVerbose(List<string> lines, string label, string typeName, string methodName, string[] paramTypeNamesOrNull)
{
    // Run normal check first.
    TestPatch(lines, label, typeName, methodName, paramTypeNamesOrNull);

    // Then dump candidate overloads + ownership.
    DumpOverloads(lines, typeName, methodName, paramTypeNamesOrNull);
}

private static void TestOptionalPatchAnyVerbose(List<string> lines, string label, string[] typeNames, string methodName)
{
    TestOptionalPatchAny(lines, label, typeNames, methodName);

    if (typeNames == null) return;
    for (int i = 0; i < typeNames.Length; i++)
    {
        DumpOverloads(lines, typeNames[i], methodName, null);
    }
}

private static void DumpOverloads(List<string> lines, string typeName, string methodName, string[] paramTypeNamesOrNull)
{
    Type t = AccessTools.TypeByName(typeName);
    if (t == null)
    {
        lines.Add("      • (verbose) type missing: " + typeName);
        return;
    }

    var candidates = new List<MethodBase>();
    try
    {
        if (paramTypeNamesOrNull != null)
        {
            Type[] parms = ResolveParamTypes(paramTypeNamesOrNull);
            if (parms != null)
            {
                MethodBase exact = AccessTools.Method(t, methodName, parms);
                if (exact != null) candidates.Add(exact);
            }
        }

        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            if (m == null) continue;
            if (!string.Equals(m.Name, methodName, StringComparison.Ordinal)) continue;
            if (!Contains(candidates, m)) candidates.Add(m);
        }
    }
    catch (Exception ex)
    {
        lines.Add("      • (verbose) reflection error: " + ex.GetType().Name);
        return;
    }

    if (candidates.Count == 0)
    {
        lines.Add("      • (verbose) no overloads found for: " + typeName + "." + methodName);
        return;
    }

    for (int i = 0; i < candidates.Count; i++)
    {
        MethodBase m = candidates[i];
        var info = Harmony.GetPatchInfo(m);

        string status;
        if (info == null) status = "no patches";
        else if (HasOwner(info.Prefixes, HarmonyId) || HasOwner(info.Postfixes, HarmonyId) || HasOwner(info.Transpilers, HarmonyId) || HasOwner(info.Finalizers, HarmonyId))
            status = "patched by us";
        else
            status = "patched by others";

        lines.Add("      • " + FormatSignature(m) + " | " + status);
    }
}

private static string FormatSignature(MethodBase m)
{
    if (m == null) return "<null>";
    try
    {
        string t = m.DeclaringType != null ? m.DeclaringType.FullName : "<no-type>";
        var ps = m.GetParameters();
        string args = "";
        if (ps != null && ps.Length > 0)
        {
            var parts = new List<string>(ps.Length);
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                if (p == null) continue;
                parts.Add((p.ParameterType != null ? p.ParameterType.FullName : "<null>"));
            }
            args = string.Join(", ", parts.ToArray());
        }
        return t + "." + m.Name + "(" + args + ")";
    }
    catch
    {
        return m.ToString();
    }
}

    private static bool HasOwner(IEnumerable<Patch> patches, string owner)
    {
        if (patches == null) return false;
        foreach (var p in patches)
            if (p != null && p.owner == owner) return true;
        return false;
    }

    private static Type[] ResolveParamTypes(string[] typeNames)
    {
        try
        {
            var list = new List<Type>();
            for (int i = 0; i < typeNames.Length; i++)
            {
                Type t = AccessTools.TypeByName(typeNames[i]);
                if (t == null) return null;
                list.Add(t);
            }
            return list.ToArray();
        }
        catch { return null; }
    }

    private static bool Contains(List<MethodBase> list, MethodBase m)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == m) return true;
        return false;
    }
}
