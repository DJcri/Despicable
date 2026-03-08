using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HarmonyPatches = HarmonyLib.Patches;
using Verse;

// Guardrail-Reason: Hero Karma patch guard stays centralized because prepare-time discovery, diagnostics, and finalize verification share one startup seam.
namespace Despicable.HeroKarma.Patches.HeroKarma;

/// <summary>
/// Centralized Harmony patch safeguarding + diagnostics for Hero Karma.
///
/// Pattern:
/// - Patches that locate their target(s) via reflection should use HKPatchGuard.PrepareSingle/PrepareMany
///   inside a [HarmonyPrepare] method.
/// - If no target exists for this game version, the patch is skipped (fail soft) AND a warning is logged
///   if the related feature is enabled (fail loud).
/// - After all patches apply, HKPatchGuard.Finalize(...) verifies which expected targets ended up owned
///   by our Harmony instance so missing patches are visible in the Help/Debug UI.
/// </summary>
public static class HKPatchGuard
{
    public enum PatchState
    {
        Unknown = 0,
        Active = 1,
        Skipped = 2,
        Failed = 3,
    }

    public sealed class PatchStatus
    {
        public string id;
        public string label;
        public string featureKey;
        public bool required;
        public PatchState state;
        public string detail;
    }

    // Guardrail-Allow-Static: owned by mod lifecycle, only used to report diagnostics.
    private static readonly Dictionary<string, PatchStatus> _statusById = new();
    private static readonly Dictionary<string, List<MethodBase>> _targetsById = new();
    private static readonly object _lock = new();
    // Guardrail-Allow-Static: One HK patch-diagnostics finalize gate owned by HKPatchGuard; reset whenever targets are re-registered during startup.
    private static bool _finalized;

    // Feature keys are used to decide whether to warn/show issues.
    public const string FeatureCoreKarma = "CoreKarma";
    public const string FeatureStandingEffects = "StandingEffects";
    public const string FeatureLocalRepTrade = "LocalRepTrade";
    public const string FeatureLocalRepGoodwill = "LocalRepGoodwill";
    public const string FeatureLocalRepArrest = "LocalRepArrest";
    public const string FeatureLocalRepPrisoners = "LocalRepPrisoners";

    public static bool IsFeatureEnabled(string featureKey)
    {
        if (!HKSettingsUtil.ModuleEnabled)
            return false;

        switch (featureKey)
        {
            case FeatureCoreKarma:
                return HKSettingsUtil.EnableGlobalKarma || HKSettingsUtil.EnableLocalRep || HKIdeologyCompat.IsStandingEnabled;
            case FeatureStandingEffects:
                return HKIdeologyCompat.IsStandingEffectsEnabled;
            case FeatureLocalRepTrade:
                return HKSettingsUtil.EnableLocalRep && HKSettingsUtil.LocalRepTradePricing;
            case FeatureLocalRepGoodwill:
                return HKSettingsUtil.EnableLocalRep && HKSettingsUtil.LocalRepGoodwillBias;
            case FeatureLocalRepArrest:
                return HKSettingsUtil.EnableLocalRep && HKSettingsUtil.LocalRepArrestCompliance;
            case FeatureLocalRepPrisoners:
                return HKSettingsUtil.EnableLocalRep && HKSettingsUtil.LocalRepInfluencePrisoners;
            default:
                return true;
        }
    }

    public static bool PrepareSingle(string id, string label, string featureKey, bool required, MethodBase target, out MethodBase cached)
    {
        cached = target;
        if (cached == null)
        {
            MarkSkipped(id, label, featureKey, required, "Target method not found.");
            return false;
        }

        RegisterTargets(id, label, featureKey, required, new[] { cached });
        return true;
    }

    public static bool PrepareMany(string id, string label, string featureKey, bool required, IEnumerable<MethodBase> candidates, out List<MethodBase> cached)
    {
        cached = new List<MethodBase>();
        if (candidates != null)
        {
            foreach (var m in candidates)
            {
                if (m != null) cached.Add(m);
            }
        }

        if (cached.Count == 0)
        {
            MarkSkipped(id, label, featureKey, required, "No compatible target methods found.");
            return false;
        }

        RegisterTargets(id, label, featureKey, required, cached);
        return true;
    }

    public static void RegisterTargets(string id, string label, string featureKey, bool required, IEnumerable<MethodBase> targets)
    {
        lock (_lock)
        {
            _finalized = false;

            if (!_statusById.TryGetValue(id, out var st) || st == null)
            {
                st = new PatchStatus
                {
                    id = id,
                    label = label,
                    featureKey = featureKey,
                    required = required,
                    state = PatchState.Unknown,
                    detail = "Awaiting verification",
                };
                _statusById[id] = st;
            }
            else
            {
                st.label = label;
                st.featureKey = featureKey;
                st.required = required;
            }

            if (!_targetsById.TryGetValue(id, out var list) || list == null)
            {
                list = new List<MethodBase>();
                _targetsById[id] = list;
            }
            else
            {
                list.Clear();
            }

            if (targets != null)
            {
                foreach (var t in targets)
                {
                    if (t != null) list.Add(t);
                }
            }
        }
    }

    public static void MarkSkipped(string id, string label, string featureKey, bool required, string reason)
    {
        lock (_lock)
        {
            _finalized = false;
            _statusById[id] = new PatchStatus
            {
                id = id,
                label = label,
                featureKey = featureKey,
                required = required,
                state = PatchState.Skipped,
                detail = reason ?? "Skipped.",
            };

            _targetsById.Remove(id);
        }

        // Fail loud only when the related gameplay feature is enabled.
        if (IsFeatureEnabled(featureKey))
            Log.Warning("[Despicable2.Core] Hero Karma patch skipped: " + label + " (" + id + "). " + reason);
    }

    public static void Finalize(Harmony harmony, string harmonyId)
    {
        if (harmony == null || string.IsNullOrEmpty(harmonyId))
            return;

        lock (_lock)
        {
            if (_finalized)
                return;
        }

        try
        {
            List<PatchStatus> statuses = GetAllStatusesInternal();
            for (int i = 0; i < statuses.Count; i++)
            {
                var st = statuses[i];
                if (st == null) continue;

                // Skipped stays skipped.
                if (st.state == PatchState.Skipped)
                    continue;

                List<MethodBase> targets;
                lock (_lock)
                {
                    _targetsById.TryGetValue(st.id, out targets);
                }

                bool active = false;
                if (targets != null)
                {
                    for (int t = 0; t < targets.Count; t++)
                    {
                        MethodBase mb = targets[t];
                        if (mb == null) continue;

						HarmonyPatches info = Harmony.GetPatchInfo(mb);
                        if (info == null) continue;

                        if (CountOwned(info, harmonyId) > 0)
                        {
                            active = true;
                            break;
                        }
                    }
                }

                if (active)
                {
                    SetState(st.id, PatchState.Active, "Active");
                }
                else
                {
                    // Feature disabled: treat as skipped.
                    if (!IsFeatureEnabled(st.featureKey))
                    {
                        SetState(st.id, PatchState.Skipped, "Feature disabled (no need to patch right now).");
                    }
                    else
                    {
                        string msg = "Patch did not apply (no owned patch entries found).";
                        SetState(st.id, PatchState.Failed, msg);
                        Log.Warning("[Despicable2.Core] Hero Karma patch failed: " + st.label + " (" + st.id + "). " + msg);
                    }
                }
            }

			// Developer safeguard: warn if any patch class uses TargetMethod(s) without a HarmonyPrepare guard.
			AuditForUnguardedPatchClasses();
        }
        catch (Exception ex)
        {
            Log.Warning("[Despicable2.Core] HKPatchGuard.Finalize failed: " + ex);
        }
        finally
        {
            lock (_lock) _finalized = true;
        }
    }

	private static void AuditForUnguardedPatchClasses()
	{
		try
		{
			Assembly asm = typeof(HKPatchGuard).Assembly;
			Type harmonyPatchAttr = typeof(HarmonyPatch);
			Type harmonyPrepareAttr = typeof(HarmonyPrepare);

			foreach (Type t in asm.GetTypes())
			{
				if (t == null || !t.IsClass)
					continue;
				if (t.Namespace == null || !t.Namespace.StartsWith("Despicable.HeroKarma.Patches"))
					continue;

				// Only consider types that are Harmony patch containers.
				if (!Attribute.IsDefined(t, harmonyPatchAttr, inherit: false))
					continue;

				// If they don't use TargetMethod(s), they might be using fixed [HarmonyPatch(typeof..)] which is fine.
				MethodInfo tm = t.GetMethod("TargetMethod", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
				MethodInfo tms = t.GetMethod("TargetMethods", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
				if (tm == null && tms == null)
					continue;

				bool hasPrepare = false;
				foreach (MethodInfo m in t.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
				{
					if (m == null) continue;
					if (Attribute.IsDefined(m, harmonyPrepareAttr, inherit: false))
					{
						hasPrepare = true;
						break;
					}
				}

				if (!hasPrepare)
				{
					Log.Warning("[Despicable2.Core] Harmony patch container missing [HarmonyPrepare] safeguard: " + t.FullName +
					    " (uses TargetMethod/TargetMethods). Recommend routing through HKPatchGuard to avoid hard failures when targets change.");
				}
			}
		}
		catch (Exception ex)
		{
			// Never allow diagnostics to crash mod init.
			Log.Warning("[Despicable2.Core] HKPatchGuard.Audit failed: " + ex);
		}
	}

    public static List<PatchStatus> GetAllStatuses()
    {
        lock (_lock)
        {
            return GetAllStatusesInternal();
        }
    }

    public static List<PatchStatus> GetIssues(bool includeSkippedWhenFeatureDisabled = false)
    {
        List<PatchStatus> all = GetAllStatuses();
        var list = new List<PatchStatus>();

        for (int i = 0; i < all.Count; i++)
        {
            var st = all[i];
            if (st == null) continue;
            if (st.state == PatchState.Active) continue;

            if (!includeSkippedWhenFeatureDisabled && st.state == PatchState.Skipped && !IsFeatureEnabled(st.featureKey))
                continue;

            list.Add(st);
        }

        return list;
    }

    private static List<PatchStatus> GetAllStatusesInternal()
    {
        var list = new List<PatchStatus>(_statusById.Count);
        foreach (var kv in _statusById)
            list.Add(kv.Value);

        list.Sort((a, b) => string.Compare(a?.label ?? a?.id, b?.label ?? b?.id, StringComparison.Ordinal));
        return list;
    }

    private static void SetState(string id, PatchState state, string detail)
    {
        lock (_lock)
        {
            if (!_statusById.TryGetValue(id, out var st) || st == null)
                return;

            st.state = state;
            st.detail = detail;
        }
    }

	private static int CountOwned(HarmonyPatches info, string harmonyId)
    {
        int owned = 0;
        owned += CountOwned(info.Prefixes, harmonyId);
        owned += CountOwned(info.Postfixes, harmonyId);
        owned += CountOwned(info.Transpilers, harmonyId);
        owned += CountOwned(info.Finalizers, harmonyId);
        return owned;
    }

    private static int CountOwned(IEnumerable<Patch> patches, string harmonyId)
    {
        if (patches == null) return 0;

        int count = 0;
        foreach (var p in patches)
        {
            if (p != null && string.Equals(p.owner, harmonyId, StringComparison.Ordinal))
                count++;
        }

        return count;
    }
}
