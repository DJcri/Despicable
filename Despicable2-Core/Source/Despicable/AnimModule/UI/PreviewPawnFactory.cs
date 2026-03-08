using RimWorld;
using RimWorld.Planet;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace Despicable;
// Guardrail-Reason: Preview pawn creation paths stay co-located while appearance normalization remains tightly coupled.
public static class PreviewPawnFactory
{
    /// <summary>
    /// Creates a safe "preview" pawn for UI rendering.
    /// Best-effort: if anything fails, callers can fall back to the source pawn.
    /// </summary>
    public static Pawn MakePreviewPawn(Pawn source)
    {
        if (source == null)
            return null;

        Pawn preview = null;

        // Pick a safe kind/faction. Using the source pawn directly can fail (or loop forever)
        // if the source kind doesn't have story/body types (animals/mechs) but the workshop
        // expects humanlike pawns.
        PawnKindDef kind = null;
        Faction faction = null;
        try { kind = source.kindDef; } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:1", "Preview pawn factory best-effort step failed.", e); }
        try { faction = source.Faction; } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:2", "Preview pawn factory best-effort step failed.", e); }

        bool needsHumanlike = false;
        try { needsHumanlike = source.RaceProps?.Humanlike != true; } catch { needsHumanlike = true; }

        if (kind == null || needsHumanlike)
        {
            try { kind = PawnKindDefOf.Colonist; } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:3", "Preview pawn factory best-effort step failed.", e); }
        }

        // 1) Generate a fresh pawn.
        // Use the safest overloads and guard against null faction.
        try
        {
            if (kind != null && faction != null)
                preview = PawnGenerator.GeneratePawn(kind, faction);
            else if (kind != null)
                preview = PawnGenerator.GeneratePawn(kind);
        }
        catch
        {
            try
            {
                if (kind != null)
                    preview = PawnGenerator.GeneratePawn(kind);
            }
            catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:4", "Preview pawn factory best-effort step failed.", e); }
        }

        if (preview == null)
            return null;

        // Keep it out of the world.
        try
        {
            if (preview.Spawned)
                preview.DeSpawn();
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:5", "Preview pawn factory best-effort step failed.", e); }

        // 2) Copy story/appearance (best effort).
        try
        {
            if (source.story != null && preview.story != null)
            {
                preview.story.bodyType = source.story.bodyType;
                preview.story.headType = source.story.headType;
                preview.story.hairDef = source.story.hairDef;
                preview.story.HairColor = source.story.HairColor;

                try { preview.story.skinColorOverride = source.story.skinColorOverride; } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:6", "Preview pawn factory best-effort step failed.", e); }
                try { preview.story.favoriteColor = source.story.favoriteColor; } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:7", "Preview pawn factory best-effort step failed.", e); }
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:8", "Preview pawn factory best-effort step failed.", e); }

        // 3) Copy apparel (best effort).
        try
        {
            if (preview.apparel != null)
            {
                foreach (var a in preview.apparel.WornApparel.ToList())
                    a.Destroy();

                if (source.apparel != null)
                {
                    foreach (var worn in source.apparel.WornApparel)
                    {
                        if (worn == null) continue;

                        Thing newApparel;
                        try { newApparel = ThingMaker.MakeThing(worn.def, worn.Stuff); }
                        catch { newApparel = ThingMaker.MakeThing(worn.def); }

                        if (newApparel is Apparel apparel)
                        {
                            try { apparel.DrawColor = worn.DrawColor; } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:9", "Preview pawn factory best-effort step failed.", e); }

                            try
                            {
                                var qc = apparel.TryGetComp<CompQuality>();
                                var srcQc = worn.TryGetComp<CompQuality>();
                                if (qc != null && srcQc != null)
                                    qc.SetQuality(srcQc.Quality, ArtGenerationContext.Colony);
                            }
                            catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:10", "Preview pawn factory best-effort step failed.", e); }

                            preview.apparel.Wear(apparel, dropReplacedApparel: false);
                        }
                        else
                        {
                            try { newApparel.Destroy(); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:11", "Preview pawn factory best-effort step failed.", e); }
                        }
                    }
                }
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:12", "Preview pawn factory best-effort step failed.", e); }

        // 4) Copy relevant custom comps (face parts etc.) best effort.
        try
        {
            var srcFace = source.TryGetComp<CompFaceParts>();
            var dstFace = preview.TryGetComp<CompFaceParts>();
            if (srcFace != null && dstFace != null)
            {
                dstFace.eyeStyleDef = srcFace.eyeStyleDef;
                dstFace.mouthStyleDef = srcFace.mouthStyleDef;
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:13", "Preview pawn factory best-effort step failed.", e); }

        // Give it a harmless name label in dev tools, just in case.
        try { preview.Name = new NameSingle($"{source.LabelShort}_Preview"); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:14", "Preview pawn factory best-effort step failed.", e); }

        // Default facial hair: no beard (best effort).
        try { SetNoBeard(preview); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:15", "Preview pawn factory best-effort step failed.", e); }

        // 5) Make it visually consistent.
        try
        {
            preview.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(preview);
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:16", "Preview pawn factory best-effort step failed.", e); }

        return preview;
    }

    /// <summary>
    /// Creates a preview pawn that matches requested gender/body type when possible.
    /// gender: -1 any, 0 male, 1 female.
    /// bodyTypeDefName: BodyTypeDef.defName or null/empty for default (Male/Female based on gender).
    /// </summary>
    public static Pawn MakePreviewPawn(Pawn source, int gender, string bodyTypeDefName)
    {
        var preview = MakePreviewPawn(source);
        if (preview == null) return null;

        // Gender: -1 means any/unset (WorkshopRole convention).
        try
        {
            if (gender == 0) preview.gender = Gender.Male;
            else if (gender == 1) preview.gender = Gender.Female;
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:17", "Preview pawn factory best-effort step failed.", e); }

        // Body type: optional override. If missing but gender fixed, default accordingly.
        try
        {
            if (preview.story != null)
            {
                string bt = bodyTypeDefName;
                if (string.IsNullOrEmpty(bt))
                {
                    if (gender == 0) bt = "Male";
                    else if (gender == 1) bt = "Female";
                }

                if (!string.IsNullOrEmpty(bt))
                {
                    var def = DefDatabase<BodyTypeDef>.GetNamedSilentFail(bt);
                    if (def != null) preview.story.bodyType = def;
                }
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:18", "Preview pawn factory best-effort step failed.", e); }

        // Refresh graphics after constraint changes.
        try
        {
            preview.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(preview);
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:19", "Preview pawn factory best-effort step failed.", e); }

        return preview;
    }

    /// <summary>
    /// Creates a minimal, clean preview pawn (no apparel, bald, no beard) for dev preview casting.
    /// Unlike MakePreviewPawn(source,...), this does NOT copy story/apparel from the source pawn,
    /// so role-gender visuals won't inherit unexpected appearance from the template.
    ///
    /// If registerAsWorldPawn is true and we're in a live game, the pawn is added to WorldPawns
    /// (so it behaves like a "real" pawn for dev quicktest saves). Callers should remove/Destroy
    /// the pawn when done.
    /// </summary>
    public static Pawn MakeBaselinePreviewPawn(Pawn template, int gender, string bodyTypeDefName, string labelSuffix = null, bool registerAsWorldPawn = true)
    {
        Pawn pawn = null;

        try
        {
            var kind = PawnKindDefOf.Colonist;
            Faction faction = null;
            try { faction = template?.Faction; } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:20", "Preview pawn factory best-effort step failed.", e); }
            if (faction == null)
            {
                try { faction = Faction.OfPlayerSilentFail; } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:21", "Preview pawn factory best-effort step failed.", e); }
            }

            if (faction != null)
                pawn = PawnGenerator.GeneratePawn(kind, faction);
            else
                pawn = PawnGenerator.GeneratePawn(kind);
        }
        catch
        {
            try { pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:22", "Preview pawn factory best-effort step failed.", e); }
        }

        if (pawn == null) return null;

        // Keep it out of the map.
        try { if (pawn.Spawned) pawn.DeSpawn(); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:23", "Preview pawn factory best-effort step failed.", e); }

        // Apply requested gender.
        // Def convention: 0=Any/Unisex, 1=Male, 2=Female. For unisex, keep the generated pawn gender.
        try
        {
            if (gender == 1) pawn.gender = Gender.Male;
            else if (gender == 2) pawn.gender = Gender.Female;
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:24", "Preview pawn factory best-effort step failed.", e); }

        // Apply requested body type.
        try
        {
            if (pawn.story != null)
            {
                string bt = bodyTypeDefName;
                if (string.IsNullOrEmpty(bt))
                {
                    if (gender == 1) bt = "Male";
                    else if (gender == 2) bt = "Female";
                }

                if (!string.IsNullOrEmpty(bt))
                {
                    var def = DefDatabase<BodyTypeDef>.GetNamedSilentFail(bt);
                    if (def != null) pawn.story.bodyType = def;
                }
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:25", "Preview pawn factory best-effort step failed.", e); }

        // Strip apparel for clean silhouettes.
        try
        {
            if (pawn.apparel != null)
            {
                foreach (var a in pawn.apparel.WornApparel.ToList())
                    a.Destroy();
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:26", "Preview pawn factory best-effort step failed.", e); }

        // Bald hair (best effort).
        try
        {
            if (pawn.story != null)
            {
                HairDef bald = null;
                try { bald = DefDatabase<HairDef>.GetNamedSilentFail("Bald"); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:27", "Preview pawn factory best-effort step failed.", e); }
                if (bald == null) { try { bald = DefDatabase<HairDef>.GetNamedSilentFail("Shaved"); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:28", "Preview pawn factory best-effort step failed.", e); } }
                if (bald != null) pawn.story.hairDef = bald;
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:29", "Preview pawn factory best-effort step failed.", e); }

        // No beard.
        try { SetNoBeard(pawn); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:30", "Preview pawn factory best-effort step failed.", e); }

        // Give it a clear label.
        try
        {
            int id = PreviewPawnIdAllocator.NextId();
            string suffix = string.IsNullOrEmpty(labelSuffix) ? "Role" : labelSuffix;
            pawn.Name = new NameSingle($"AGS_{suffix}_Preview_{id:0000}");
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:31", "Preview pawn factory best-effort step failed.", e); }

        // Optionally register as a world pawn in dev play sessions.
        if (registerAsWorldPawn)
        {
            try
            {
                if (Current.ProgramState == ProgramState.Playing && Find.WorldPawns != null)
                    Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Decide);
            }
            catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:32", "Preview pawn factory best-effort step failed.", e); }
        }

        // Refresh graphics.
        try
        {
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:33", "Preview pawn factory best-effort step failed.", e); }

        return pawn;
    }

    private static void SetNoBeard(Pawn pawn)
    {
        if (pawn == null) return;

        try
        {
            if (pawn.style != null)
            {
                pawn.style.beardDef = BeardDefOf.NoBeard;
                return;
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:34", "Preview pawn factory best-effort step failed.", e); }

        try
        {
            var story = pawn.story;
            if (story != null)
            {
                var f = story.GetType().GetField("beardDef");
                if (f != null) { f.SetValue(story, BeardDefOf.NoBeard); return; }

                var p = story.GetType().GetProperty("beardDef");
                if (p != null && p.CanWrite) { p.SetValue(story, BeardDefOf.NoBeard); return; }
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("PreviewPawnFactory.EmptyCatch:35", "Preview pawn factory best-effort step failed.", e); }
    }
}
