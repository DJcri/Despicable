using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
using RimWorld;
using Verse;

namespace Despicable;
// Guardrail-Reason: Face-part blacklist queries, policy, and persistence still revolve around one ownership surface.
public static partial class FacePartsUtil
{
    public static bool IsSystemBlacklisted(HeadTypeDef headType)
    {
        string defName = headType?.defName;
        if (string.IsNullOrEmpty(defName))
            return false;

        return HardBlacklistedHeadDefNames.Any(name => defName.Equals(name, System.StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsHeadBlacklisted(HeadTypeDef headType)
    {
        if (headType == null)
            return false;

        if (IsSystemBlacklisted(headType))
            return true;

        if (IsDefaultDisabledHead(headType) && !IsDefaultDisabledHeadExplicitlyAllowed(headType))
            return true;

        HeadBlacklistDef blacklistDef = DefDatabase<HeadBlacklistDef>.GetNamed("Despicable_HeadBlacklist");
        return blacklistDef.blacklistedHeads.Contains(headType);
    }

    public static bool IsDefaultDisabledHead(HeadTypeDef headType)
    {
        if (headType == null || IsSystemBlacklisted(headType))
            return false;

        ModContentPack contentPack = headType.modContentPack;
        return contentPack != null && DependsOnHumanoidAlienRaces(contentPack);
    }

    public static bool IsDefaultDisabledHeadExplicitlyAllowed(HeadTypeDef headType)
    {
        if (headType == null)
            return false;

        string defName = headType.defName;
        return !defName.NullOrEmpty() && AllowedDefaultDisabledHeadDefNames.Contains(defName);
    }

    public static void AddHeadToBlacklist(HeadTypeDef headType)
    {
        HeadBlacklistDef blacklistDef = DefDatabase<HeadBlacklistDef>.GetNamed("Despicable_HeadBlacklist");
        if (blacklistDef == null || headType == null || IsSystemBlacklisted(headType))
            return;

        if (IsDefaultDisabledHead(headType))
        {
            RemoveDefaultDisabledHeadAllowance(headType);
            return;
        }

        if (blacklistDef.blacklistedHeads.Contains(headType))
            return;

        blacklistDef.blacklistedHeads.Add(headType);
        Log.Message($"Added head '{headType.defName}' to the blacklist.");
    }

    public static void RemoveHeadFromBlacklist(HeadTypeDef headType)
    {
        HeadBlacklistDef blacklistDef = DefDatabase<HeadBlacklistDef>.GetNamed("Despicable_HeadBlacklist");
        if (blacklistDef == null || headType == null || IsSystemBlacklisted(headType))
            return;

        if (IsDefaultDisabledHead(headType))
        {
            AllowDefaultDisabledHead(headType);
            return;
        }

        if (!blacklistDef.blacklistedHeads.Contains(headType))
            return;

        blacklistDef.blacklistedHeads.Remove(headType);
        Log.Message($"Removed head '{headType.defName}' from the blacklist.");
    }


    public static void SaveHeadTypeBlacklist()
    {
        HeadBlacklistDef blacklistDef = DefDatabase<HeadBlacklistDef>.GetNamedSilentFail("Despicable_HeadBlacklist");
        if (blacklistDef == null)
        {
            Log.Error("Could not find the HeadBlacklistDef to save.");
            return;
        }

        Settings settings = TryGetSettings();
        if (settings == null)
        {
            Log.Error("Could not resolve Despicable settings while saving the head blacklist.");
            return;
        }

        settings.headTypeBlacklistDefNames ??= new List<string>();
        settings.allowedDefaultDisabledHeadDefNames ??= new List<string>();

        settings.headTypeBlacklistDefNames.Clear();
        settings.allowedDefaultDisabledHeadDefNames.Clear();

        foreach (HeadTypeDef head in blacklistDef.blacklistedHeads)
        {
            if (head == null || IsSystemBlacklisted(head) || IsDefaultDisabledHead(head) || head.defName.NullOrEmpty())
                continue;

            if (!settings.headTypeBlacklistDefNames.Contains(head.defName))
                settings.headTypeBlacklistDefNames.Add(head.defName);
        }

        foreach (string allowedDefName in AllowedDefaultDisabledHeadDefNames)
        {
            if (!allowedDefName.NullOrEmpty() && !settings.allowedDefaultDisabledHeadDefNames.Contains(allowedDefName))
                settings.allowedDefaultDisabledHeadDefNames.Add(allowedDefName);
        }

        try
        {
            (ModMain.Instance ?? LoadedModManager.GetMod<ModMain>())?.WriteSettings();
            RefreshAllFaceParts();
        }
        catch (System.Exception e)
        {
            Log.Error($"Failed to save head blacklist settings. Error: {e}");
        }
    }

    public static void LoadHeadTypeBlacklist()
    {
        HeadBlacklistDef blacklistDef = DefDatabase<HeadBlacklistDef>.GetNamedSilentFail("Despicable_HeadBlacklist");
        if (blacklistDef == null)
        {
            Log.Error("Could not find the HeadBlacklistDef to load into.");
            return;
        }

        blacklistDef.blacklistedHeads ??= new List<HeadTypeDef>();
        blacklistDef.blacklistedHeads.Clear();
        AllowedDefaultDisabledHeadDefNames.Clear();

        Settings settings = TryGetSettings();
        if (settings != null)
        {
            settings.headTypeBlacklistDefNames ??= new List<string>();
            settings.allowedDefaultDisabledHeadDefNames ??= new List<string>();

            if (settings.headTypeBlacklistDefNames.Count > 0 || settings.allowedDefaultDisabledHeadDefNames.Count > 0)
            {
                ApplySettingsBlacklist(settings, blacklistDef);
                EnsureSystemBlacklist(blacklistDef);
                return;
            }
        }

        if (TryLoadLegacyBlacklist(blacklistDef, settings))
        {
            EnsureSystemBlacklist(blacklistDef);
            return;
        }

        EnsureSystemBlacklist(blacklistDef);
    }

    public static void RefreshAllFacePartsForSettingsChange()
    {
        AutoEyePatchRuntime.EnsureGenerated();
        RefreshAllFaceParts();
    }

    private static void RefreshAllFaceParts()
    {
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null)
            return;

        IEnumerable<Pawn> pawns = PawnsFinder.AllMapsAndWorld_Alive;
        if (pawns == null)
            return;

        foreach (Pawn pawn in pawns)
        {
            if (pawn == null)
                continue;

            try
            {
                CompFaceParts comp = pawn.TryGetComp<CompFaceParts>();
                if (comp != null)
                    comp.InitializeFacePartState();
            }
            catch (System.Exception e)
            {
                Log.Warning($"[Despicable] Failed refreshing face parts for {pawn.LabelShortCap}: {e.Message}");
            }
        }
    }

    private static Settings TryGetSettings()
    {
        if (ModMain.Instance?.settings != null)
            return ModMain.Instance.settings;

        return LoadedModManager.GetMod<ModMain>()?.GetSettings<Settings>();
    }

    private static void ApplySettingsBlacklist(Settings settings, HeadBlacklistDef blacklistDef)
    {
        if (settings == null || blacklistDef == null)
            return;

        for (int i = 0; i < settings.headTypeBlacklistDefNames.Count; i++)
        {
            string headName = settings.headTypeBlacklistDefNames[i];
            if (headName.NullOrEmpty())
                continue;

            HeadTypeDef headDef = DefDatabase<HeadTypeDef>.GetNamedSilentFail(headName);
            if (headDef != null)
            {
                if (!IsSystemBlacklisted(headDef) && !IsDefaultDisabledHead(headDef) && !blacklistDef.blacklistedHeads.Contains(headDef))
                    blacklistDef.blacklistedHeads.Add(headDef);
            }
            else
            {
                Log.Warning($"Blacklisted head '{headName}' was not found in Defs and will not be loaded.");
            }
        }

        for (int i = 0; i < settings.allowedDefaultDisabledHeadDefNames.Count; i++)
        {
            string headName = settings.allowedDefaultDisabledHeadDefNames[i];
            if (headName.NullOrEmpty())
                continue;

            HeadTypeDef headDef = DefDatabase<HeadTypeDef>.GetNamedSilentFail(headName);
            if (headDef != null && IsDefaultDisabledHead(headDef))
                AllowedDefaultDisabledHeadDefNames.Add(headName);
        }
    }

    private static bool TryLoadLegacyBlacklist(HeadBlacklistDef blacklistDef, Settings settings)
    {
        ModContentPack modContentPack = ModMain.Instance?.Content ?? LoadedModManager.GetMod<ModMain>()?.Content;
        if (modContentPack == null)
            return false;

        string filePath = Path.Combine(modContentPack.RootDir, "Config", "FaceHeadBlacklist.xml");
        if (!File.Exists(filePath))
            return false;

        XmlSerializer serializer = new(typeof(HeadBlacklistData));
        try
        {
            using XmlReader reader = XmlReader.Create(filePath);
            HeadBlacklistData loadedData = (HeadBlacklistData)serializer.Deserialize(reader);
            if (loadedData == null)
                return false;

            blacklistDef.blacklistedHeads.Clear();
            AllowedDefaultDisabledHeadDefNames.Clear();

            if (loadedData.blacklistedHeadNames != null)
            {
                foreach (string headName in loadedData.blacklistedHeadNames)
                {
                    HeadTypeDef headDef = DefDatabase<HeadTypeDef>.GetNamedSilentFail(headName);
                    if (headDef != null)
                    {
                        if (!IsSystemBlacklisted(headDef) && !IsDefaultDisabledHead(headDef) && !blacklistDef.blacklistedHeads.Contains(headDef))
                            blacklistDef.blacklistedHeads.Add(headDef);
                    }
                }
            }

            if (loadedData.allowedHeadNames != null)
            {
                foreach (string headName in loadedData.allowedHeadNames)
                {
                    if (headName.NullOrEmpty())
                        continue;

                    HeadTypeDef headDef = DefDatabase<HeadTypeDef>.GetNamedSilentFail(headName);
                    if (headDef != null && IsDefaultDisabledHead(headDef))
                        AllowedDefaultDisabledHeadDefNames.Add(headName);
                }
            }

            if (settings != null)
            {
                settings.headTypeBlacklistDefNames = blacklistDef.blacklistedHeads
                    .Where(h => h != null && !h.defName.NullOrEmpty())
                    .Select(h => h.defName)
                    .Distinct()
                    .ToList();

                settings.allowedDefaultDisabledHeadDefNames = AllowedDefaultDisabledHeadDefNames
                    .Where(name => !name.NullOrEmpty())
                    .Distinct()
                    .ToList();

                try
                {
                    (ModMain.Instance ?? LoadedModManager.GetMod<ModMain>())?.WriteSettings();
                }
                catch (System.Exception e)
                {
                    Log.Warning($"[Despicable] Failed migrating legacy head blacklist settings: {e.Message}");
                }
            }

            Log.Message($"Successfully migrated legacy head blacklist from: {filePath}");
            return true;
        }
        catch (System.Exception e)
        {
            Log.Error($"Failed to load legacy head blacklist file. Error: {e}");
            return false;
        }
    }

    public static int GetBlacklistedCount()
    {
        return GetBlacklistedHeads().Count;
    }

    public static IReadOnlyList<HeadTypeDef> GetBlacklistedHeads()
    {
        HeadBlacklistDef blacklistDef = DefDatabase<HeadBlacklistDef>.GetNamedSilentFail("Despicable_HeadBlacklist");
        IEnumerable<HeadTypeDef> userBlacklisted = (blacklistDef?.blacklistedHeads ?? new List<HeadTypeDef>())
            .Where(h => h != null);

        IEnumerable<HeadTypeDef> systemBlacklisted = DefDatabase<HeadTypeDef>.AllDefsListForReading.Where(IsSystemBlacklisted);
        IEnumerable<HeadTypeDef> defaultDisabled = DefDatabase<HeadTypeDef>.AllDefsListForReading
            .Where(h => IsDefaultDisabledHead(h) && !IsDefaultDisabledHeadExplicitlyAllowed(h));

        return userBlacklisted
            .Concat(systemBlacklisted)
            .Concat(defaultDisabled)
            .Distinct()
            .ToList();
    }

    private static void EnsureSystemBlacklist(HeadBlacklistDef blacklistDef)
    {
        if (blacklistDef?.blacklistedHeads == null)
            return;

        foreach (HeadTypeDef head in DefDatabase<HeadTypeDef>.AllDefsListForReading)
        {
            if (IsSystemBlacklisted(head) && !blacklistDef.blacklistedHeads.Contains(head))
                blacklistDef.blacklistedHeads.Add(head);
        }
    }

    private static void AllowDefaultDisabledHead(HeadTypeDef headType)
    {
        string defName = headType?.defName;
        if (defName.NullOrEmpty())
            return;

        if (AllowedDefaultDisabledHeadDefNames.Add(defName))
            Log.Message($"Allowed default-disabled head '{defName}' for face parts.");
    }

    private static void RemoveDefaultDisabledHeadAllowance(HeadTypeDef headType)
    {
        string defName = headType?.defName;
        if (defName.NullOrEmpty())
            return;

        AllowedDefaultDisabledHeadDefNames.Remove(defName);
    }

    private static bool DependsOnHumanoidAlienRaces(ModContentPack contentPack)
    {
        if (contentPack == null)
            return false;

        string packageId = contentPack.PackageId;
        if (!packageId.NullOrEmpty() && HarPackageIds.Any(id => packageId.Equals(id, System.StringComparison.OrdinalIgnoreCase)))
            return true;

        ModMetaData metaData = contentPack.ModMetaData;
        if (metaData == null)
            return false;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        System.Type metaType = metaData.GetType();

        object dependenciesObject = null;

        PropertyInfo dependenciesProperty = metaType.GetProperty("modDependencies", flags);
        if (dependenciesProperty != null)
            dependenciesObject = dependenciesProperty.GetValue(metaData, null);

        if (dependenciesObject == null)
        {
            FieldInfo dependenciesField = metaType.GetField("modDependencies", flags);
            if (dependenciesField != null)
                dependenciesObject = dependenciesField.GetValue(metaData);
        }

        IEnumerable dependencies = dependenciesObject as IEnumerable;
        if (dependencies == null)
            return false;

        foreach (object dependency in dependencies)
        {
            if (dependency == null)
                continue;

            System.Type dependencyType = dependency.GetType();
            string dependencyPackageId = null;

            PropertyInfo packageIdProperty = dependencyType.GetProperty("packageId", flags);
            if (packageIdProperty != null)
                dependencyPackageId = packageIdProperty.GetValue(dependency, null) as string;

            if (dependencyPackageId.NullOrEmpty())
            {
                FieldInfo packageIdField = dependencyType.GetField("packageId", flags);
                if (packageIdField != null)
                    dependencyPackageId = packageIdField.GetValue(dependency) as string;
            }

            if (dependencyPackageId.NullOrEmpty())
                continue;

            if (HarPackageIds.Any(id => dependencyPackageId.Equals(id, System.StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }
}
