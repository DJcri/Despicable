using System.Collections.Generic;

namespace Despicable.AnimModule.AnimGroupStudio.UI;
internal sealed class AgsAuthorPreviewRuntimeState
{
    public readonly List<Dialog_AnimGroupStudio.AuthorPreviewSlot> Slots = new();
    public readonly Dictionary<string, Dialog_AnimGroupStudio.AuthorPreviewSlot> SlotsByKey = new();

    public bool IsPlaying;
    public float Speed = 1f;
    public float TickAccumulator;
    public int CurrentTick;
    public int StageHash = int.MinValue;
}
