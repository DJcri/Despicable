namespace Despicable.AnimGroupStudio.Preview;

/// <summary>
/// Implement on comps that need an explicit best-effort initialization pass for detached
/// preview pawns used by editors/workshops. This is only for synthetic preview pawns and
/// should not replace normal gameplay lifecycle/bootstrap behavior.
/// </summary>
public interface IDetachedPreviewPawnInitializer
{
    void InitializeForDetachedPreview();
}
