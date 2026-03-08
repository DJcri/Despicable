using System.Collections.Generic;
using UnityEngine;
using Despicable.AnimGroupStudio;
using RimWorld;
using Despicable.AnimModule.AnimGroupStudio.Model;

namespace Despicable.AnimModule.AnimGroupStudio.UI;
internal sealed class AgsEditorSessionState
{
    public List<AgsModel.Project> Projects;
    public AgsModel.Project Project;

    public string AuthorRoleKey = "male_1";
    public int AuthorStageIndex;
    public int AuthorTrackIndex = -1;
    public int AuthorKeyIndex = -1;

    public Vector2 AuthorStageScroll;
    public Vector2 AuthorTrackScroll;
    public Vector2 AuthorKeyScroll;
    public Vector2 AuthorInspectorScroll;
    public Vector2 AuthorLeftScroll;
    public float AuthorLeftContentHeight;
    public float AuthorInspectorContentHeight;
    public int AuthorRightPaneTab;

    public bool AuthorScaleLock = true;
    public readonly Dictionary<string, bool> PropTagCache = new();

    public string LastExportFolder;
    public int LastExportWritten;
    public int LastExportOverwritten;

    public Dictionary<string, AgsModel.ExistingFamily> Families;
    public List<string> FamilyKeysSorted;
    public string SelectedFamilyKey;
    public AnimGroupDef SelectedGroup;
    public int SelectedStageIndex;
    public bool LoopCurrentStage;
}
