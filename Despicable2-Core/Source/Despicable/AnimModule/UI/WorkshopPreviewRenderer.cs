using UnityEngine;
using RimWorld;
using Verse;
using System.Reflection;

namespace Despicable;
/// <summary>
/// Renders a pawn portrait into a persistent RenderTexture so we can update per-frame
/// while scrubbing or playing a timeline.
/// </summary>
public sealed class WorkshopPreviewRenderer
{
    private RenderTexture rt;
    private int width;
    private int height;

    // Cached camera used by PawnCacheRenderer (best-effort via reflection).
    // Guardrail-Allow-Static: Best-effort reflection cache for PawnCache camera handle, reused across preview renders.
    private static Camera cachedPawnCacheCamera;

    public WorkshopPreviewRenderer(int width = 700, int height = 980)
    {
        this.width = Mathf.Max(32, width);
        this.height = Mathf.Max(32, height);
    }

    /// <summary>
    /// Ensures the internal RenderTexture matches the requested size.
    /// Recreates the RT only when the dimensions change.
    /// </summary>
    public bool EnsureSize(int width, int height)
    {
        width = Mathf.Clamp(width, 128, 2048);
        height = Mathf.Clamp(height, 128, 2048);

        if (this.width == width && this.height == height && rt != null && rt.IsCreated())
            return false;

        this.width = width;
        this.height = height;

        // Recreate
        try
        {
            if (rt != null)
            {
                rt.Release();
                Object.Destroy(rt);
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("WorkshopPreviewRenderer.EmptyCatch:1", "Workshop preview renderer best-effort cleanup failed.", e); }
        finally
        {
            rt = null;
        }

        Ensure();
        return true;
    }

    public RenderTexture Texture
    {
        get
        {
            Ensure();
            return rt;
        }
    }

    /// <summary>
    /// Legacy-friendly helper used by preview call sites.
    /// Renders using sensible defaults (south-facing, no extra angle/offset).
    /// </summary>
    public void RenderPawn(Pawn pawn)
    {
        Render(pawn, Rot4.South, 0f, default(Vector3), renderHeadgear: true, portrait: true, scale: 1f);
    }

    public void RenderPawn(Pawn pawn, Rot4 rot, float angle, Vector3 positionOffset, bool renderHeadgear = true, bool portrait = true, float scale = 1f)
    {
        Render(pawn, rot, angle, positionOffset, renderHeadgear, portrait, scale);
    }

    /// <summary>
    /// Legacy-friendly helper used by preview call sites.
    /// </summary>
    public RenderTexture GetTexture() => Texture;

    public void Render(Pawn pawn, Rot4 rot, float angle, Vector3 positionOffset, bool renderHeadgear, bool portrait, float scale)
    {
        if (pawn == null) return;
        Ensure();
        UIUtil.RenderPawnToTexture(pawn, rt, rot, angle, positionOffset, renderHeadgear, portrait: portrait, scale: scale);
    }

    /// <summary>
    /// Render two pawns into the SAME RenderTexture without a "split" barrier.
    /// Best effort: we clear once, then render both with the PawnCache camera set to Depth-only clears.
    /// If we can't access the underlying camera, we fall back to separate renders (may overwrite).
    /// </summary>
    public void RenderPawnsShared(Pawn a, Pawn b, Rot4 rot, float angle, bool renderHeadgear = true, bool portrait = true, float scale = 1f)
    {
        if (a == null && b == null) return;
        Ensure();

        // Clear once.
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
        RenderTexture.active = prev;

        // Try to ensure the cache camera doesn't clear color between draws.
        Camera cam = GetPawnCacheCamera();
        bool hadCam = cam != null;
        CameraClearFlags oldFlags = CameraClearFlags.SolidColor;
        Color oldBg = Color.clear;
        if (hadCam)
        {
            oldFlags = cam.clearFlags;
            oldBg = cam.backgroundColor;
            cam.clearFlags = CameraClearFlags.Depth;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }

        try
        {
            // Render in the active workshop context if already set; otherwise create a scope.
            if (WorkshopRenderContext.Active)
            {
                if (a != null) UIUtil.RenderPawnToTexture(a, rt, rot, angle, default(Vector3), renderHeadgear, portrait: portrait, scale: scale);
                if (b != null) UIUtil.RenderPawnToTexture(b, rt, rot, angle, default(Vector3), renderHeadgear, portrait: portrait, scale: scale);
            }
            else
            {
                using (new WorkshopRenderContext.Scope(active: true))
                {
                    if (a != null) UIUtil.RenderPawnToTexture(a, rt, rot, angle, default(Vector3), renderHeadgear, portrait: portrait, scale: scale);
                    if (b != null) UIUtil.RenderPawnToTexture(b, rt, angle: angle, rot: rot, positionOffset: default(Vector3), renderHeadgear: renderHeadgear, portrait: portrait, scale: scale);
                }
            }
        }
        catch
        {
            // swallow (preview only)
        }
        finally
        {
            if (hadCam)
            {
                cam.clearFlags = oldFlags;
                cam.backgroundColor = oldBg;
            }
        }
    }

    private static Camera GetPawnCacheCamera()
    {
        if (cachedPawnCacheCamera != null) return cachedPawnCacheCamera;
        try
        {
            object renderer = PawnCacheCameraManager.PawnCacheRenderer;
            if (renderer == null) return null;
            var t = renderer.GetType();

            // Fields first.
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (f.FieldType == typeof(Camera))
                {
                    cachedPawnCacheCamera = (Camera)f.GetValue(renderer);
                    return cachedPawnCacheCamera;
                }
            }

            // Then properties.
            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (p.PropertyType == typeof(Camera) && p.GetIndexParameters().Length == 0)
                {
                    cachedPawnCacheCamera = (Camera)p.GetValue(renderer, null);
                    return cachedPawnCacheCamera;
                }
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("WorkshopPreviewRenderer.EmptyCatch:2", "Workshop preview renderer best-effort cleanup failed.", e); }

        return null;
    }

    private void Ensure()
    {
        if (rt != null && rt.IsCreated()) return;
        rt = new RenderTexture(width, height, 32, RenderTextureFormat.ARGB32);
        rt.Create();
    }

    public void Dispose()
    {
        try
        {
            if (rt != null)
            {
                rt.Release();
                Object.Destroy(rt);
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("WorkshopPreviewRenderer.EmptyCatch:3", "Workshop preview renderer best-effort cleanup failed.", e); }
        finally
        {
            rt = null;
        }
    }
}
