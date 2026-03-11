using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using Despicable;

namespace Despicable.AnimGroupStudio.Preview;

/// <summary>
/// Thread-local bridge that lets the AGS preview render path expose real per-node positions
/// back to the authoring UI without inventing a second transform system.
/// </summary>
public static class AgsPreviewNodeCapture
{
    internal sealed class RawNodeSample
    {
        public string SlotKey;
        public string NodeTag;
        public Vector2 ViewportUv;
        public int Depth;
        public float CameraDepth;
        public Vector2 ViewportBasisX;
        public Vector2 ViewportBasisZ;
    }

    internal sealed class SlotDebugStats
    {
        public int CaptureCalls;
        public int Tagged;
        public int CameraReady;
        public int Projected;
        public int InvalidProjection;
        public int Recorded;

        public SlotDebugStats Clone()
        {
            return (SlotDebugStats)MemberwiseClone();
        }
    }

    [ThreadStatic]
    private static RuntimeState state;

    private static RuntimeState State
    {
        get
        {
            if (state == null)
                state = new RuntimeState();
            return state;
        }
    }

    public static bool IsCapturing
        => State.Active && !State.CurrentSlotKey.NullOrEmpty() && State.CurrentPawn != null;

    public static void ResetRuntimeState()
    {
        state = null;
        propsTagGetterByType.Clear();
    }

    public static void BeginFrame()
    {
        var runtime = State;
        runtime.Active = true;
        runtime.CurrentSlotKey = null;
        runtime.CurrentPawn = null;
        runtime.CurrentCamera = null;
        runtime.SamplesBySlot.Clear();
        runtime.StatsBySlot.Clear();
    }

    public static void EndFrame()
    {
        var runtime = State;
        runtime.Active = false;
        runtime.CurrentSlotKey = null;
        runtime.CurrentPawn = null;
        runtime.CurrentCamera = null;
    }

    public static void BeginSlot(string slotKey, Pawn pawn)
    {
        if (!State.Active)
            return;

        State.CurrentSlotKey = slotKey;
        State.CurrentPawn = pawn;
        State.CurrentCamera = null;
    }

    public static void TryAttachActiveCamera(Pawn pawn, Camera camera)
    {
        if (!State.Active || camera == null || pawn == null)
            return;

        if (!ReferenceEquals(State.CurrentPawn, pawn))
            return;

        State.CurrentCamera = camera;
    }

    public static void EndSlot()
    {
        State.CurrentSlotKey = null;
        State.CurrentPawn = null;
        State.CurrentCamera = null;
    }

    public static void CaptureNode(PawnRenderNode node, Pawn pawn, Matrix4x4 matrix)
    {
        if (!IsCapturing || node == null || pawn == null || !ReferenceEquals(State.CurrentPawn, pawn))
            return;

        SlotDebugStats stats = GetOrCreateStats(State.CurrentSlotKey);
        stats.CaptureCalls++;

        string nodeTag = TryGetNodeTag(node);
        if (nodeTag.NullOrEmpty())
            return;

        stats.Tagged++;

        Camera cam = State.CurrentCamera ?? Despicable.UIUtil.GetPawnCacheCameraForPreview();
        if (cam == null)
            return;

        stats.CameraReady++;

        Vector3 worldPos = matrix.MultiplyPoint3x4(Vector3.zero);
        Vector3 viewport = cam.WorldToViewportPoint(worldPos);
        if (float.IsNaN(viewport.x) || float.IsNaN(viewport.y) || float.IsNaN(viewport.z) || viewport.z <= 0f)
        {
            stats.InvalidProjection++;
            return;
        }

        const float basisEpsilon = 0.01f;

        // For the root node, offset is in world/parent space — the matrix already
        // has the root's own angle baked in, so probing local axes gives a rotated
        // basis that doesn't match what offset.x/z actually control.
        // Probe world-space axes instead so the drag solver works in the same space
        // as the offset values. For all other nodes the local-space probe is correct
        // because their offset is expressed in the parent's (root's) local space.
        Vector3 viewportX, viewportZ;
        bool isRootNode = string.Equals(nodeTag, "Root", StringComparison.Ordinal);
        if (isRootNode)
        {
            viewportX = cam.WorldToViewportPoint(worldPos + new Vector3(basisEpsilon, 0f, 0f));
            viewportZ = cam.WorldToViewportPoint(worldPos + new Vector3(0f, 0f, basisEpsilon));
        }
        else
        {
            viewportX = cam.WorldToViewportPoint(matrix.MultiplyPoint3x4(new Vector3(basisEpsilon, 0f, 0f)));
            viewportZ = cam.WorldToViewportPoint(matrix.MultiplyPoint3x4(new Vector3(0f, 0f, basisEpsilon)));
        }
        Vector2 basisX = Vector2.zero;
        Vector2 basisZ = Vector2.zero;

        if (!(float.IsNaN(viewportX.x) || float.IsNaN(viewportX.y) || float.IsNaN(viewportX.z) || viewportX.z <= 0f))
            basisX = new Vector2((viewportX.x - viewport.x) / basisEpsilon, (viewportX.y - viewport.y) / basisEpsilon);

        if (!(float.IsNaN(viewportZ.x) || float.IsNaN(viewportZ.y) || float.IsNaN(viewportZ.z) || viewportZ.z <= 0f))
            basisZ = new Vector2((viewportZ.x - viewport.x) / basisEpsilon, (viewportZ.y - viewport.y) / basisEpsilon);

        stats.Projected++;

        if (!State.SamplesBySlot.TryGetValue(State.CurrentSlotKey, out List<RawNodeSample> samples))
        {
            samples = new List<RawNodeSample>(32);
            State.SamplesBySlot[State.CurrentSlotKey] = samples;
        }

        samples.Add(new RawNodeSample
        {
            SlotKey = State.CurrentSlotKey,
            NodeTag = nodeTag,
            ViewportUv = new Vector2(viewport.x, viewport.y),
            Depth = CountDepth(node),
            CameraDepth = viewport.z,
            ViewportBasisX = basisX,
            ViewportBasisZ = basisZ
        });

        stats.Recorded++;
    }

    internal static void CopyFrameSamplesTo(Dictionary<string, List<RawNodeSample>> destination)
    {
        destination.Clear();

        foreach (KeyValuePair<string, List<RawNodeSample>> kvp in State.SamplesBySlot)
        {
            if (kvp.Key.NullOrEmpty() || kvp.Value == null)
                continue;

            destination[kvp.Key] = new List<RawNodeSample>(kvp.Value);
        }
    }

    internal static void CopyFrameStatsTo(Dictionary<string, SlotDebugStats> destination)
    {
        destination.Clear();

        foreach (KeyValuePair<string, SlotDebugStats> kvp in State.StatsBySlot)
        {
            if (kvp.Key.NullOrEmpty() || kvp.Value == null)
                continue;

            destination[kvp.Key] = kvp.Value.Clone();
        }
    }

    private static int CountDepth(PawnRenderNode node)
    {
        int depth = 0;
        PawnRenderNode current = node?.parent;
        while (current != null)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }

    private static string TryGetNodeTag(PawnRenderNode node)
    {
        object props = node?.Props;
        if (props == null)
            return null;

        Type propsType = props.GetType();
        if (!propsTagGetterByType.TryGetValue(propsType, out Func<object, string> getter))
        {
            getter = ResolveTagGetter(propsType);
            propsTagGetterByType[propsType] = getter;
        }

        try
        {
            return getter != null ? getter(props) : null;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "AgsPreviewNodeCapture.ResolveTag",
                "AGS preview node capture failed to inspect a render-node tag.",
                ex);
            return null;
        }
    }

    private static Func<object, string> ResolveTagGetter(Type propsType)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        Func<object, string> bestGetter = null;
        int bestScore = int.MinValue;

        for (Type current = propsType; current != null; current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(Flags))
            {
                int score = ScoreTagMember(field.Name, field.FieldType);
                if (score <= bestScore)
                    continue;

                bestGetter = (object props) => ExtractDefName(field.GetValue(props));
                bestScore = score;
            }

            foreach (PropertyInfo property in current.GetProperties(Flags))
            {
                if (property.GetIndexParameters().Length != 0)
                    continue;

                int score = ScoreTagMember(property.Name, property.PropertyType);
                if (score <= bestScore)
                    continue;

                bestGetter = (object props) => ExtractDefName(property.GetValue(props, null));
                bestScore = score;
            }
        }

        return bestGetter;
    }

    private static int ScoreTagMember(string memberName, Type memberType)
    {
        if (memberType == null)
            return int.MinValue;

        int score = int.MinValue;
        bool isTagDef = typeof(PawnRenderNodeTagDef).IsAssignableFrom(memberType);
        bool isAnyDef = typeof(Def).IsAssignableFrom(memberType);
        if (!isTagDef && !isAnyDef)
            return score;

        score = isTagDef ? 100 : 25;
        if (!memberName.NullOrEmpty())
        {
            if (string.Equals(memberName, "tagDef", StringComparison.OrdinalIgnoreCase))
                score += 1000;
            else if (string.Equals(memberName, "renderNodeTag", StringComparison.OrdinalIgnoreCase))
                score += 900;
            else if (memberName.IndexOf("tag", StringComparison.OrdinalIgnoreCase) >= 0)
                score += 200;
            else if (memberName.IndexOf("node", StringComparison.OrdinalIgnoreCase) >= 0)
                score += 25;
        }

        return score;
    }

    private static string ExtractDefName(object value)
    {
        return (value as Def)?.defName;
    }

    private static SlotDebugStats GetOrCreateStats(string slotKey)
    {
        if (!State.StatsBySlot.TryGetValue(slotKey, out SlotDebugStats stats) || stats == null)
        {
            stats = new SlotDebugStats();
            State.StatsBySlot[slotKey] = stats;
        }

        return stats;
    }

    private sealed class RuntimeState
    {
        public bool Active;
        public string CurrentSlotKey;
        public Pawn CurrentPawn;
        public Camera CurrentCamera;
        public readonly Dictionary<string, List<RawNodeSample>> SamplesBySlot =
            new Dictionary<string, List<RawNodeSample>>(StringComparer.Ordinal);
        public readonly Dictionary<string, SlotDebugStats> StatsBySlot =
            new Dictionary<string, SlotDebugStats>(StringComparer.Ordinal);
    }

    private static readonly Dictionary<Type, Func<object, string>> propsTagGetterByType =
        new Dictionary<Type, Func<object, string>>(8);
}
