using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace Despicable;
[StaticConstructorOnStartup]
public static class UIUtil
{
    /// <summary>
    /// Live portrait render into a caller-owned RenderTexture.
    /// Use this when you need per-frame updates (e.g. timeline scrubbing) without fighting PortraitsCache.
    /// </summary>
    public static void RenderPawnToTexture(
        Pawn pawn,
        RenderTexture target,
        Rot4 rot,
        float angle,
        Vector3 positionOffset,
        bool renderHeadgear = true,
        bool portrait = true,
        float scale = 1f)
    {
        if (pawn == null || target == null) return;

        // RimWorld's PawnCacheRenderer uses an internal shared camera.
        // In some setups that camera's rect/pixelRect can be left in a half-viewport state
        // after other UI renders. Force a full target-sized viewport for preview renders.
        Camera cam = null;
        Rect oldRect = default;
        Rect oldPixelRect = default;
        bool hadCam = false;

        try
        {
            pawn.Drawer?.renderer?.EnsureGraphicsInitialized();

            cam = TryGetPawnCacheCamera();
            hadCam = cam != null;
            if (hadCam)
            {
                oldRect = cam.rect;
                oldPixelRect = cam.pixelRect;
                cam.rect = new Rect(0f, 0f, 1f, 1f);
                cam.pixelRect = new Rect(0f, 0f, target.width, target.height);
            }

            if (WorkshopRenderContext.Active)
            {
                PawnCacheCameraManager.PawnCacheRenderer.RenderPawn(
                    pawn,
                    target,
                    Vector3.zero,
                    scale,
                    angle,
                    rot,
                    renderHead: true,
                    renderHeadgear: renderHeadgear,
                    renderClothes: true,
                    portrait: portrait,
                    positionOffset: positionOffset);
            }
            else
            {
                using (new WorkshopRenderContext.Scope(active: true))
                {
                    PawnCacheCameraManager.PawnCacheRenderer.RenderPawn(
                        pawn,
                        target,
                        Vector3.zero,
                        scale,
                        angle,
                        rot,
                        renderHead: true,
                        renderHeadgear: renderHeadgear,
                        renderClothes: true,
                        portrait: portrait,
                        positionOffset: positionOffset);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
        }
        finally
        {
            if (hadCam)
            {
                try
                {
                    cam.rect = oldRect;
                    cam.pixelRect = oldPixelRect;
                }
                catch (Exception ex)
                {
                    Despicable.Core.DebugLogger.WarnExceptionOnce("UIUtil.RenderPawnToTexture.RestoreCamera", "UIUtil failed to restore the pawn cache camera viewport after rendering.", ex);
                }
            }
        }
    }

    // Guardrail-Allow-Static: Best-effort reflection cache for PawnCache camera handle, reused across portrait renders.
    private static Camera cachedPawnCacheCamera;

    private static Camera TryGetPawnCacheCamera()
    {
        if (cachedPawnCacheCamera != null) return cachedPawnCacheCamera;

        try
        {
            object renderer = PawnCacheCameraManager.PawnCacheRenderer;
            if (renderer == null) return null;
            var t = renderer.GetType();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

            foreach (var f in t.GetFields(flags))
            {
                if (f.FieldType == typeof(Camera))
                {
                    cachedPawnCacheCamera = (Camera)f.GetValue(renderer);
                    return cachedPawnCacheCamera;
                }
            }

            foreach (var p in t.GetProperties(flags))
            {
                if (p.PropertyType == typeof(Camera) && p.GetIndexParameters().Length == 0)
                {
                    cachedPawnCacheCamera = (Camera)p.GetValue(renderer, null);
                    return cachedPawnCacheCamera;
                }
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("UIUtil.TryGetPawnCacheCamera", "UIUtil failed to inspect the pawn cache renderer camera.", ex);
        }

        return null;
    }
}
