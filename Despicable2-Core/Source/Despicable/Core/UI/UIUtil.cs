using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Despicable.AnimGroupStudio.Preview;
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
        // AGS non-portrait preview capture needs a handle to that camera for node projection,
        // but forcing the camera viewport there can skew the workshop framing. Keep explicit
        // camera handoff for capture, and only normalize rect/pixelRect for portrait renders.
        Camera cam = null;
        Rect oldRect = default;
        Rect oldPixelRect = default;
        bool hadCam = false;
        bool adjustedViewport = false;

        try
        {
            pawn.Drawer?.renderer?.EnsureGraphicsInitialized();

            cam = TryGetPawnCacheCamera();
            hadCam = cam != null;
            if (hadCam)
            {
                if (portrait)
                {
                    oldRect = cam.rect;
                    oldPixelRect = cam.pixelRect;
                    cam.rect = new Rect(0f, 0f, 1f, 1f);
                    cam.pixelRect = new Rect(0f, 0f, target.width, target.height);
                    adjustedViewport = true;
                }

                AgsPreviewNodeCapture.TryAttachActiveCamera(pawn, cam);
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
            if (hadCam && adjustedViewport)
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

    public static Camera GetPawnCacheCameraForPreview()
    {
        return TryGetPawnCacheCamera();
    }

    private static Camera TryGetPawnCacheCamera()
    {
        if (cachedPawnCacheCamera != null) return cachedPawnCacheCamera;

        try
        {
            object renderer = PawnCacheCameraManager.PawnCacheRenderer;
            if (renderer == null) return null;

            cachedPawnCacheCamera = FindCameraRecursive(renderer, 3, new HashSet<object>(ReferenceEqualityComparer.Instance));
            return cachedPawnCacheCamera;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("UIUtil.TryGetPawnCacheCamera", "UIUtil failed to inspect the pawn cache renderer camera.", ex);
        }

        return null;
    }

    private static Camera FindCameraRecursive(object value, int depthRemaining, HashSet<object> visited)
    {
        if (value == null || depthRemaining < 0)
            return null;

        if (value is Camera directCamera)
            return directCamera;

        if (value is GameObject go)
            return go.GetComponentInChildren<Camera>(true);

        if (value is Component component)
        {
            Camera componentCamera = component.GetComponent<Camera>();
            if (componentCamera != null)
                return componentCamera;

            return component.GetComponentInChildren<Camera>(true);
        }

        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Type) || typeof(Delegate).IsAssignableFrom(type))
            return null;

        if (!visited.Add(value))
            return null;

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (FieldInfo field in type.GetFields(Flags))
        {
            object fieldValue;
            try
            {
                fieldValue = field.GetValue(value);
            }
            catch
            {
                continue;
            }

            Camera found = FindCameraRecursive(fieldValue, depthRemaining - 1, visited);
            if (found != null)
                return found;
        }

        foreach (PropertyInfo property in type.GetProperties(Flags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
                continue;

            object propertyValue;
            try
            {
                propertyValue = property.GetValue(value, null);
            }
            catch
            {
                continue;
            }

            Camera found = FindCameraRecursive(propertyValue, depthRemaining - 1, visited);
            if (found != null)
                return found;
        }

        return null;
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        public new bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }

}
