using System;
using System.Collections.Generic;
using UnityEngine;

namespace Despicable.AnimModule.AnimGroupStudio.UI;

internal enum AgsAuthorPreviewGizmoVisualKind
{
    Disc,
    Ring
}

internal sealed class AgsAuthorPreviewGizmoEntry
{
    public string NodeTag;
    public Vector2 ViewportUv;
    public Rect ScreenRect;
    public float Radius;
    public int Depth;
    public int SourceCount;
    public float CameraDepth;
    public Vector2 ViewportBasisX;
    public Vector2 ViewportBasisZ;
    public Color FillColor;
    public Color OutlineColor;
    public bool IsSelected;
    public bool IsSynthetic;
    public string DragBasisNodeTag;
    public AgsAuthorPreviewGizmoVisualKind VisualKind;
}

internal enum AgsAuthorPreviewGizmoDragMode
{
    None,
    Translate,
    Rotate
}

internal sealed class AgsAuthorPreviewRuntimeState
{
    public readonly List<Dialog_AnimGroupStudio.AuthorPreviewSlot> Slots = new();
    public readonly Dictionary<string, Dialog_AnimGroupStudio.AuthorPreviewSlot> SlotsByKey = new();
    public readonly HashSet<int> DirtyStageIndices = new();
    public readonly List<int> CompiledStageHashes = new();

    public bool IsPlaying;
    public float Speed = 1f;
    public float TickAccumulator;
    public int CurrentTick;
    public int SourceHash = int.MinValue;
    public bool StructureDirty = true;
    public bool SelectionDirty = true;
    public bool SavePending;
    public readonly List<AgsAuthorPreviewGizmoEntry> Gizmos = new();
    public readonly Dictionary<string, AgsAuthorPreviewGizmoEntry> GizmosByTag = new(StringComparer.Ordinal);

    public string LastOverlapSignature;
    public int LastOverlapCycleIndex = -1;
    public Vector2 LastOverlapMousePosition;
    public float LastOverlapClickTime = -100f;

    public bool GizmoPressActive;
    public bool GizmoDragging;
    public string ActiveGizmoNodeTag;
    public AgsAuthorPreviewGizmoDragMode GizmoDragMode;
    public Vector2 GizmoPressMousePosition;
    public Vector2 GizmoDragStartViewportUv;
    public Vector2 GizmoTranslateGrabViewportOffset;
    public Vector3 GizmoDragStartOffset;
    public Vector2 GizmoDragStartBasisX;  // frozen at drag-begin, used for absolute displacement solve
    public Vector2 GizmoDragStartBasisZ;
    public Vector2 GizmoDragPivotScreenPosition;
    public float GizmoRotateDragLastMouseAngle;
    public float GizmoRotateDragAccumulatedAngle;
    public bool GizmoDragChangedData;
    public bool GizmoDragIsPropNode;  // true when the active drag target is a prop node (wider clamp range)
}
