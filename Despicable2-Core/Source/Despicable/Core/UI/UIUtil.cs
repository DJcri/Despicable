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
        float scale = 1f,
        bool isolateCameraState = false)
    {
        if (pawn == null || target == null) return;

        try
        {
            pawn.Drawer?.renderer?.EnsureGraphicsInitialized();

            if (isolateCameraState)
                ClearRenderTexture(target);

            // Let the pawn-cache renderer manage its own camera state for the supplied
            // target texture. Manually forcing targetTexture/pixelRect/aspect here can
            // leak non-square preview settings into later vanilla portraits when the
            // underlying renderer swaps cameras or keeps extra internal state.
            Camera cam = GetPawnCacheCameraForPreview(forceRefresh: true);
            if (cam != null)
                AgsPreviewNodeCapture.TryAttachActiveCamera(pawn, cam);

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
        catch (Exception e)
        {
            Log.Error(e.ToString());
        }
    }

    // Guardrail-Allow-Static: Best-effort reflection cache for PawnCache camera handle, reused across portrait renders.
    private static Camera cachedPawnCacheCamera;

    public static void ResetRuntimeState()
    {
        cachedPawnCacheCamera = null;
    }

    public static Camera GetPawnCacheCameraForPreview(bool forceRefresh = false)
    {
        return TryGetPawnCacheCamera(forceRefresh);
    }

    private static Camera TryGetPawnCacheCamera(bool forceRefresh = false)
    {
        if (!forceRefresh && IsUsableCachedCamera(cachedPawnCacheCamera))
            return cachedPawnCacheCamera;

        if (!forceRefresh)
            cachedPawnCacheCamera = null;

        try
        {
            object renderer = PawnCacheCameraManager.PawnCacheRenderer;
            if (renderer == null) return null;

            // Face-parts preview renders use forceRefresh so they can resolve the
            // live pawn-cache camera without poisoning the shared cache for other
            // preview UIs. Prefer direct Camera members on the renderer before any
            // broad recursive scan so compat layers do not accidentally hand us an
            // unrelated nested camera from another editor surface.
            Camera resolved = FindPreferredRendererCamera(renderer);
            if (!IsUsableCachedCamera(resolved))
                resolved = FindCameraRecursive(renderer, 3, new HashSet<object>(ReferenceEqualityComparer.Instance));

            if (!IsUsableCachedCamera(resolved))
                return null;

            if (!forceRefresh)
                cachedPawnCacheCamera = resolved;

            return resolved;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("UIUtil.TryGetPawnCacheCamera", "UIUtil failed to inspect the pawn cache renderer camera.", ex);
        }

        return null;
    }

    private static bool IsUsableCachedCamera(Camera cam)
    {
        return cam != null && cam.gameObject != null;
    }


    private static Camera FindPreferredRendererCamera(object renderer)
    {
        if (renderer == null)
            return null;

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type rendererType = renderer.GetType();

        foreach (FieldInfo field in rendererType.GetFields(Flags))
        {
            if (field.FieldType != typeof(Camera))
                continue;

            try
            {
                Camera camera = field.GetValue(renderer) as Camera;
                if (IsUsableCachedCamera(camera))
                    return camera;
            }
            catch
            {
            }
        }

        foreach (PropertyInfo property in rendererType.GetProperties(Flags))
        {
            if (property.PropertyType != typeof(Camera) || !property.CanRead || property.GetIndexParameters().Length != 0)
                continue;

            try
            {
                Camera camera = property.GetValue(renderer, null) as Camera;
                if (IsUsableCachedCamera(camera))
                    return camera;
            }
            catch
            {
            }
        }

        if (renderer is Component component)
        {
            Camera componentCamera = component.GetComponent<Camera>();
            if (IsUsableCachedCamera(componentCamera))
                return componentCamera;

            Camera childCamera = component.GetComponentInChildren<Camera>(true);
            if (IsUsableCachedCamera(childCamera))
                return childCamera;
        }

        if (renderer is GameObject gameObject)
        {
            Camera objectCamera = gameObject.GetComponent<Camera>();
            if (IsUsableCachedCamera(objectCamera))
                return objectCamera;

            Camera childCamera = gameObject.GetComponentInChildren<Camera>(true);
            if (IsUsableCachedCamera(childCamera))
                return childCamera;
        }

        return null;
    }

    private static void ClearRenderTexture(RenderTexture target)
    {
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
        }
        finally
        {
            RenderTexture.active = previous;
        }
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
