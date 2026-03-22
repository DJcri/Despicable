using System;
// Guardrail-Reason: Mod settings rendering stays together while tab visibility, section measurement, and cross-module refresh hooks remain one settings surface.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;
using Despicable.AnimModule.AnimGroupStudio.UI;
using Despicable.FacePartsModule.UI;
using Despicable.UIFramework;
using Despicable.UIFramework.Controls;
using Despicable.UIFramework.Layout;

namespace Despicable;

/// <summary>
/// Framework-native mod settings page renderer.
/// This surface uses attached vanilla tabs with sectioned bodies so global settings stay compact and organized.
/// </summary>
public sealed class D2ModSettingsRenderer
{
    private enum SettingsTab
    {
        Core,
        FaceParts,
        Content,
        Developer
    }

    private static readonly D2UIStyle FaceUiStyle = D2UIStyle.Default.With(s =>
    {
        s.HeaderHeight = 58f;
        s.FooterHeight = 0f;
        s.BodyTopPadY = 6f;
        s.BodyBottomPadY = 6f;
        s.RowHeight = 28f;
        s.ButtonHeight = 28f;
    });

    private const float SectionHeaderHeight = 28f;
    private static SettingsTab _activeTab = SettingsTab.Core;

    private Vector2 _coreScroll;
    private Vector2 _facePartsScroll;
    private Vector2 _contentScroll;
    private Vector2 _developerScroll;

    private float _coreHeight;
    private float _facePartsHeight;
    private float _contentHeight;
    private float _developerHeight;

    public void Draw(Rect inRect, Settings settings)
    {
        if (settings == null)
            return;

        bool facialBefore = settings.facialPartsExtensionEnabled;
        bool portraitDynamicsBefore = settings.facialDynamicsInPortraits;
        bool runtimeZoomGateBefore = settings.runtimeFacialDynamicsZoomGateEnabled;
        float runtimeZoomCutoffBefore = settings.runtimeFacialDynamicsMaxZoomRootSize;
        bool autoEyeBefore = settings.experimentalAutoEyePatchEnabled;
        bool nudityBefore = settings.nudityEnabled;
        bool renderGenitalsBefore = settings.renderGenitalsEnabled;

        var ctx = new UIContext(FaceUiStyle, null, "D2ModSettings", UIPass.Draw);
        List<SettingsTab> visibleTabs = BuildVisibleTabs();
        CoerceActiveTab(visibleTabs);

        using (var shell = ctx.GroupPanel("SettingsShell", inRect, out var v, soft: false, pad: true))
        {
            DrawHeader(ctx, ref v);

            Rect tabsShellRect = v.NextFill(UIRectTag.Panel, "Settings/TabsShell");
            string[] labels = BuildTabLabels(visibleTabs);
            int selectedIndex = GetSelectedTabIndex(visibleTabs, _activeTab);

            var attached = D2Tabs.VanillaAttachedTabBody(
                ctx,
                tabsShellRect,
                ref selectedIndex,
                labels,
                "Settings/Tabs",
                innerPad: 6f,
                forcedRows: 1);

            _activeTab = visibleTabs[selectedIndex];

            using var body = ctx.GroupPanel("Settings/Body", attached.InnerRect, soft: true, pad: true, padOverride: ctx.Style.Pad);
            DrawActiveTabBody(ctx, settings, body.Inner);
        }

        bool canRefreshLiveState = Current.ProgramState == ProgramState.Playing && Current.Game != null;

        if (canRefreshLiveState && (facialBefore != settings.facialPartsExtensionEnabled || portraitDynamicsBefore != settings.facialDynamicsInPortraits || runtimeZoomGateBefore != settings.runtimeFacialDynamicsZoomGateEnabled || runtimeZoomCutoffBefore != settings.runtimeFacialDynamicsMaxZoomRootSize || autoEyeBefore != settings.experimentalAutoEyePatchEnabled))
            FacePartsUtil.RefreshAllFacePartsForSettingsChange();

        if (canRefreshLiveState && (nudityBefore != settings.nudityEnabled || renderGenitalsBefore != settings.renderGenitalsEnabled))
            TryRefreshAllLovinPartsForSettingsChange();
    }

    private static void DrawHeader(UIContext ctx, ref D2VStack v)
    {
        Rect titleRect = v.Next(28f, UIRectTag.Label, "Header/Title");
        Rect subtitleRect = v.Next(24f, UIRectTag.Label, "Header/Subtitle");

        D2Text.DrawWrappedLabel(ctx, titleRect, "Despicable Settings", GameFont.Medium, UIRectTag.Label, "Header/TitleText");
        D2Text.DrawWrappedLabel(ctx, subtitleRect, "Global settings", GameFont.Small, UIRectTag.Label, "Header/SubtitleText");
    }

    private static List<SettingsTab> BuildVisibleTabs()
    {
        var tabs = new List<SettingsTab>(4) { SettingsTab.Core, SettingsTab.FaceParts };
        if (Despicable.Core.ContentAvailability.NSFWActive)
            tabs.Add(SettingsTab.Content);
        if (Prefs.DevMode)
            tabs.Add(SettingsTab.Developer);
        return tabs;
    }

    private static void CoerceActiveTab(List<SettingsTab> visibleTabs)
    {
        if (visibleTabs == null || visibleTabs.Count == 0)
        {
            _activeTab = SettingsTab.Core;
            return;
        }

        for (int i = 0; i < visibleTabs.Count; i++)
        {
            if (visibleTabs[i] == _activeTab)
                return;
        }

        _activeTab = visibleTabs[0];
    }

    private static int GetSelectedTabIndex(List<SettingsTab> visibleTabs, SettingsTab activeTab)
    {
        if (visibleTabs == null || visibleTabs.Count == 0)
            return 0;

        for (int i = 0; i < visibleTabs.Count; i++)
        {
            if (visibleTabs[i] == activeTab)
                return i;
        }

        return 0;
    }

    private static string[] BuildTabLabels(List<SettingsTab> visibleTabs)
    {
        if (visibleTabs == null || visibleTabs.Count == 0)
            return new[] { "Core" };

        string[] labels = new string[visibleTabs.Count];
        for (int i = 0; i < visibleTabs.Count; i++)
            labels[i] = GetTabLabel(visibleTabs[i]);
        return labels;
    }

    private static string GetTabLabel(SettingsTab tab)
    {
        switch (tab)
        {
            case SettingsTab.Core:
                return "Core";
            case SettingsTab.FaceParts:
                return "Face Parts";
            case SettingsTab.Content:
                return "Content";
            case SettingsTab.Developer:
                return "Developer";
            default:
                return "Core";
        }
    }

    private void DrawActiveTabBody(UIContext ctx, Settings settings, Rect bodyRect)
    {
        switch (_activeTab)
        {
            case SettingsTab.FaceParts:
                D2ScrollView.Draw(ctx, bodyRect, ref _facePartsScroll, ref _facePartsHeight,
                    delegate (UIContext c, ref D2VStack v) { DrawFacePartsTab(c, settings, ref v); },
                    "Settings/FacePartsScroll");
                break;
            case SettingsTab.Content:
                D2ScrollView.Draw(ctx, bodyRect, ref _contentScroll, ref _contentHeight,
                    delegate (UIContext c, ref D2VStack v) { DrawContentTab(c, settings, ref v); },
                    "Settings/ContentScroll");
                break;
            case SettingsTab.Developer:
                D2ScrollView.Draw(ctx, bodyRect, ref _developerScroll, ref _developerHeight,
                    delegate (UIContext c, ref D2VStack v) { DrawDeveloperTab(c, settings, ref v); },
                    "Settings/DeveloperScroll");
                break;
            case SettingsTab.Core:
            default:
                D2ScrollView.Draw(ctx, bodyRect, ref _coreScroll, ref _coreHeight,
                    delegate (UIContext c, ref D2VStack v) { DrawCoreTab(c, settings, ref v); },
                    "Settings/CoreScroll");
                break;
        }
    }

    private static void DrawCoreTab(UIContext ctx, Settings settings, ref D2VStack v)
    {
        DrawSystemsSection(ctx, settings, ref v);
        if (Despicable.Core.ContentAvailability.NSFWActive)
        {
            v.NextSpace(ctx.Style.GapM);
            DrawIntegrationsSection(ctx, settings, ref v);
        }
        v.NextSpace(ctx.Style.GapM);
        DrawHeroKarmaSection(ctx, settings, ref v);
        v.NextSpace(ctx.Style.GapM);
        DrawToolsSection(ctx, ref v);
    }

    private static void DrawFacePartsTab(UIContext ctx, Settings settings, ref D2VStack v)
    {
        DrawFacePartsOptionsSection(ctx, settings, ref v);
        v.NextSpace(ctx.Style.GapM);
        DrawFacePartsManagementSection(ctx, ref v);
    }

    private static void DrawContentTab(UIContext ctx, Settings settings, ref D2VStack v)
    {
        if (!Despicable.Core.ContentAvailability.NSFWActive)
            return;

        DrawLovinSection(ctx, settings, ref v);
        v.NextSpace(ctx.Style.GapM);
        DrawNuditySection(ctx, settings, ref v);
        v.NextSpace(ctx.Style.GapM);
        DrawAudioSection(ctx, settings, ref v);
    }

    private static void DrawDeveloperTab(UIContext ctx, Settings settings, ref D2VStack v)
    {
        if (!Prefs.DevMode)
            return;

        DrawDeveloperGeneralSection(ctx, settings, ref v);
        v.NextSpace(ctx.Style.GapM);
        DrawDeveloperHeroKarmaSection(ctx, settings, ref v);
    }

    private static void DrawSystemsSection(UIContext ctx, Settings settings, ref D2VStack outer)
    {
        float height = MeasureSectionHeight(ctx, 3);
        Rect rect = outer.Next(height, UIRectTag.PanelSoft, "Core/Systems/Outer");
        using var panel = ctx.GroupPanel("Core/Systems", rect, soft: true, pad: true, padOverride: ctx.Style.Pad);
        var v = ctx.D2VStack(panel.Inner, label: "Core/Systems/Stack");
        DrawSectionHeader(ctx, ref v, "Systems", "Core/Systems/Header");

        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Core/Systems/Animation"), "Animation Extension", ref settings.animationExtensionEnabled, id: "Core/Systems/AnimationToggle");

        bool facialEnabled = !ModMain.IsNlFacialInstalled;
        string facialDisabledReason = ModMain.IsNlFacialInstalled ? "Blocked by NL Facial Animation." : null;
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Core/Systems/FacialParts"), "Facial Parts", ref settings.facialPartsExtensionEnabled, enabled: facialEnabled, disabledReason: facialDisabledReason, id: "Core/Systems/FacialPartsToggle");
        if (ModMain.IsNlFacialInstalled)
            settings.facialPartsExtensionEnabled = false;

        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Core/Systems/HeroModule"), "Hero Module", ref settings.heroModuleEnabled, id: "Core/Systems/HeroModuleToggle");
    }

    private static void DrawIntegrationsSection(UIContext ctx, Settings settings, ref D2VStack outer)
    {
        float height = MeasureSectionHeight(ctx, 1);
        Rect rect = outer.Next(height, UIRectTag.PanelSoft, "Core/Integrations/Outer");
        using var panel = ctx.GroupPanel("Core/Integrations", rect, soft: true, pad: true, padOverride: ctx.Style.Pad);
        var v = ctx.D2VStack(panel.Inner, label: "Core/Integrations/Stack");
        DrawSectionHeader(ctx, ref v, "Integrations", "Core/Integrations/Header");

        bool intimacyLoaded = false;
        try
        {
            Type guardsType = AccessTools.TypeByName("Despicable.NSFW.Integrations.IntegrationGuards");
            if (guardsType != null)
            {
                intimacyLoaded = (bool?)(AccessTools.Method(guardsType, "IsIntimacyLoaded")?.Invoke(null, null)) ?? false;
            }
        }
        catch
        {
            intimacyLoaded = false;
        }

        DrawCheckboxRow(
            ctx,
            v.NextRow(UIRectTag.Checkbox, "Core/Integrations/HideLovinOption"),
            "Hide lovin' manual interaction when Intimacy is installed",
            ref settings.hideManualLovinOptionWhenIntimacyInstalled,
            enabled: intimacyLoaded,
            disabledReason: "Intimacy must be installed first.",
            id: "Core/Integrations/HideLovinOptionToggle");

    }

    private static void DrawHeroKarmaSection(UIContext ctx, Settings settings, ref D2VStack outer)
    {
        float height = MeasureSectionHeight(ctx, 1);
        Rect rect = outer.Next(height, UIRectTag.PanelSoft, "Core/HeroKarma/Outer");
        using var panel = ctx.GroupPanel("Core/HeroKarma", rect, soft: true, pad: true, padOverride: ctx.Style.Pad);
        var v = ctx.D2VStack(panel.Inner, label: "Core/HeroKarma/Stack");
        DrawSectionHeader(ctx, ref v, "Hero Karma", "Core/HeroKarma/Header");

        bool enabled = settings.heroModuleEnabled && (!Prefs.DevMode || settings.heroKarmaEnableLocalRep);
        string disabledReason = !settings.heroModuleEnabled
            ? "Hero Module must be enabled first."
            : "Local Reputation must be enabled first.";

        DrawCheckboxRow(
            ctx,
            v.NextRow(UIRectTag.Checkbox, "Core/HeroKarma/OffMapPlayerWordOfMouth"),
            "D2C_Settings_AllowOffMapPlayerFactionSettlementWordOfMouth".Translate(),
            ref settings.heroKarmaAllowOffMapPlayerFactionSettlementWordOfMouth,
            enabled: enabled,
            disabledReason: disabledReason,
            id: "Core/HeroKarma/OffMapPlayerWordOfMouthToggle");
    }

    private static void DrawToolsSection(UIContext ctx, ref D2VStack outer)
    {
        float height = MeasureSectionHeight(ctx, 1);
        Rect rect = outer.Next(height, UIRectTag.PanelSoft, "Core/Tools/Outer");
        using var panel = ctx.GroupPanel("Core/Tools", rect, soft: true, pad: true, padOverride: ctx.Style.Pad);
        var v = ctx.D2VStack(panel.Inner, label: "Core/Tools/Stack");
        DrawSectionHeader(ctx, ref v, "Tools", "Core/Tools/Header");

        Rect buttonRect = v.NextButton(UIRectTag.Button, "Core/Tools/OpenAnimationStudioRow");
        if (D2Widgets.ButtonText(ctx, buttonRect, "Open Animation Studio", "Core/Tools/OpenAnimationStudioButton"))
            Find.WindowStack.Add(new Dialog_AnimGroupStudio());
    }

    private static void DrawFacePartsOptionsSection(UIContext ctx, Settings settings, ref D2VStack outer)
    {
        float height = MeasureSectionHeight(ctx, 6);
        Rect rect = outer.Next(height, UIRectTag.PanelSoft, "FaceParts/Options/Outer");
        using var panel = ctx.GroupPanel("FaceParts/Options", rect, soft: true, pad: true, padOverride: ctx.Style.Pad);
        var v = ctx.D2VStack(panel.Inner, label: "FaceParts/Options/Stack");
        DrawSectionHeader(ctx, ref v, "Options", "FaceParts/Options/Header");

        bool facePartsEnabled = settings.facialPartsExtensionEnabled && !ModMain.IsNlFacialInstalled;
        string facePartsDisabledReason = facePartsEnabled ? null : "Facial Parts must be enabled first.";
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "FaceParts/Options/PortraitDynamics"), "Animate Faces In Portraits", ref settings.facialDynamicsInPortraits, enabled: facePartsEnabled, disabledReason: facePartsDisabledReason, id: "FaceParts/Options/PortraitDynamicsToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "FaceParts/Options/ZoomGate"), "Gate Runtime Face Dynamics By Zoom", ref settings.runtimeFacialDynamicsZoomGateEnabled, enabled: facePartsEnabled, disabledReason: facePartsDisabledReason, id: "FaceParts/Options/ZoomGateToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "FaceParts/Options/HostileGate"), "Gate Runtime Face Dynamics For Hostiles", ref settings.runtimeFacialDynamicsGateHostilePawns, enabled: facePartsEnabled, disabledReason: facePartsDisabledReason, id: "FaceParts/Options/HostileGateToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "FaceParts/Options/VisitorGate"), "Gate Runtime Face Dynamics For Visitors/Traders", ref settings.runtimeFacialDynamicsGateVisitorsAndTraders, enabled: facePartsEnabled, disabledReason: facePartsDisabledReason, id: "FaceParts/Options/VisitorGateToggle");

        using (new TextStateScope(GameFont.Small, TextAnchor.MiddleLeft, false))
            D2Widgets.LabelClipped(ctx, v.NextRow(UIRectTag.Label, "FaceParts/Options/ZoomCutoffLabel"), "Runtime Face Dynamics Zoom Cutoff", "FaceParts/Options/ZoomCutoffLabelText");
        settings.runtimeFacialDynamicsMaxZoomRootSize = D2Widgets.HorizontalSlider(ctx, v.NextRow(UIRectTag.Slider, "FaceParts/Options/ZoomCutoffSliderRow"), settings.runtimeFacialDynamicsMaxZoomRootSize, 4f, 20f, showValueLabel: true, label: "FaceParts/Options/ZoomCutoffSlider");
    }

    private static void DrawFacePartsManagementSection(UIContext ctx, ref D2VStack outer)
    {
        float height = MeasureSectionHeight(ctx, 1);
        Rect rect = outer.Next(height, UIRectTag.PanelSoft, "FaceParts/Management/Outer");
        using var panel = ctx.GroupPanel("FaceParts/Management", rect, soft: true, pad: true, padOverride: ctx.Style.Pad);
        var v = ctx.D2VStack(panel.Inner, label: "FaceParts/Management/Stack");
        DrawSectionHeader(ctx, ref v, "Management", "FaceParts/Management/Header");

        Rect buttonRect = v.NextButton(UIRectTag.Button, "FaceParts/Management/OpenBlacklistRow");
        if (D2Widgets.ButtonText(ctx, buttonRect, "Open Headtype Blacklist", "FaceParts/Management/OpenBlacklistButton"))
            Find.WindowStack.Add(new Dialog_D2HeadtypeBlacklist());
    }

    private static void DrawLovinSection(UIContext ctx, Settings settings, ref D2VStack outer)
    {
        float height = MeasureSectionHeight(ctx, 3);
        Rect rect = outer.Next(height, UIRectTag.PanelSoft, "Content/Lovin/Outer");
        using var panel = ctx.GroupPanel("Content/Lovin", rect, soft: true, pad: true, padOverride: ctx.Style.Pad);
        var v = ctx.D2VStack(panel.Inner, label: "Content/Lovin/Stack");
        DrawSectionHeader(ctx, ref v, "Lovin", "Content/Lovin/Header");

        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Content/Lovin/Enabled"), "Lovin Extension", ref settings.lovinExtensionEnabled, id: "Content/Lovin/EnabledToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Content/Lovin/MutualConsent"), "Require Mutual Consent", ref settings.lovinMutualConsent, enabled: settings.lovinExtensionEnabled, disabledReason: "Lovin Extension must be enabled first.", id: "Content/Lovin/MutualConsentToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Content/Lovin/RespectIdeology"), "Respect Ideology Restrictions", ref settings.lovinRespectIdeology, enabled: settings.lovinExtensionEnabled, disabledReason: "Lovin Extension must be enabled first.", id: "Content/Lovin/RespectIdeologyToggle");
    }

    private static void DrawNuditySection(UIContext ctx, Settings settings, ref D2VStack outer)
    {
        float height = MeasureSectionHeight(ctx, 2);
        Rect rect = outer.Next(height, UIRectTag.PanelSoft, "Content/Nudity/Outer");
        using var panel = ctx.GroupPanel("Content/Nudity", rect, soft: true, pad: true, padOverride: ctx.Style.Pad);
        var v = ctx.D2VStack(panel.Inner, label: "Content/Nudity/Stack");
        DrawSectionHeader(ctx, ref v, "Nudity", "Content/Nudity/Header");

        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Content/Nudity/Enabled"), "Nudity Enabled", ref settings.nudityEnabled, id: "Content/Nudity/EnabledToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Content/Nudity/RenderGenitals"), "Render Genitals", ref settings.renderGenitalsEnabled, enabled: settings.nudityEnabled, disabledReason: "Nudity Enabled must be on first.", id: "Content/Nudity/RenderGenitalsToggle");
    }

    private static void DrawAudioSection(UIContext ctx, Settings settings, ref D2VStack outer)
    {
        float height = MeasureSectionHeight(ctx, 2);
        Rect rect = outer.Next(height, UIRectTag.PanelSoft, "Content/Audio/Outer");
        using var panel = ctx.GroupPanel("Content/Audio", rect, soft: true, pad: true, padOverride: ctx.Style.Pad);
        var v = ctx.D2VStack(panel.Inner, label: "Content/Audio/Stack");
        DrawSectionHeader(ctx, ref v, "Audio", "Content/Audio/Header");

        using (new TextStateScope(GameFont.Small, TextAnchor.MiddleLeft, false))
            D2Widgets.LabelClipped(ctx, v.NextRow(UIRectTag.Label, "Content/Audio/VolumeLabel"), "Animation Sound Volume", "Content/Audio/VolumeLabelText");
        settings.soundVolume = D2Widgets.HorizontalSlider(ctx, v.NextRow(UIRectTag.Slider, "Content/Audio/VolumeSliderRow"), settings.soundVolume, 0f, 1f, showValueLabel: true, label: "Content/Audio/VolumeSlider");
    }

    private static void DrawDeveloperGeneralSection(UIContext ctx, Settings settings, ref D2VStack outer)
    {
        float height = MeasureSectionHeight(ctx, 2);
        Rect rect = outer.Next(height, UIRectTag.PanelSoft, "Developer/General/Outer");
        using var panel = ctx.GroupPanel("Developer/General", rect, soft: true, pad: true, padOverride: ctx.Style.Pad);
        var v = ctx.D2VStack(panel.Inner, label: "Developer/General/Stack");
        DrawSectionHeader(ctx, ref v, "General", "Developer/General/Header");

        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/General/DebugUi"), "Hero Karma Debug UI", ref settings.heroKarmaDebugUI, id: "Developer/General/DebugUiToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/General/Diagnostics"), "Echo Diagnostics To Log", ref settings.heroKarmaEchoDiagnosticsToLog, id: "Developer/General/DiagnosticsToggle");
    }

    private static void DrawDeveloperHeroKarmaSection(UIContext ctx, Settings settings, ref D2VStack outer)
    {
        float height = MeasureSectionHeight(ctx, 23);
        Rect rect = outer.Next(height, UIRectTag.PanelSoft, "Developer/HeroKarma/Outer");
        using var panel = ctx.GroupPanel("Developer/HeroKarma", rect, soft: true, pad: true, padOverride: ctx.Style.Pad);
        var v = ctx.D2VStack(panel.Inner, label: "Developer/HeroKarma/Stack");
        DrawSectionHeader(ctx, ref v, "Hero Karma", "Developer/HeroKarma/Header");

        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/GlobalKarma"), "Enable Global Karma", ref settings.heroKarmaEnableGlobalKarma, id: "Developer/HeroKarma/GlobalKarmaToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/LocalRep"), "Enable Local Reputation", ref settings.heroKarmaEnableLocalRep, id: "Developer/HeroKarma/LocalRepToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/IdeologyApproval"), "Enable Ideology Approval", ref settings.heroKarmaEnableIdeologyApproval, id: "Developer/HeroKarma/IdeologyApprovalToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/StandingEffects"), "Enable Standing Effects", ref settings.heroKarmaStandingEnableEffects, id: "Developer/HeroKarma/StandingEffectsToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/Prisoners"), "Local Rep Influences Prisoners", ref settings.heroKarmaLocalRepInfluencePrisoners, id: "Developer/HeroKarma/PrisonersToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/Arrest"), "Local Rep Arrest Compliance", ref settings.heroKarmaLocalRepArrestCompliance, id: "Developer/HeroKarma/ArrestToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/Goodwill"), "Local Rep Goodwill Bias", ref settings.heroKarmaLocalRepGoodwillBias, id: "Developer/HeroKarma/GoodwillToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/Trade"), "Local Rep Trade Pricing", ref settings.heroKarmaLocalRepTradePricing, id: "Developer/HeroKarma/TradeToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/ExecutePrisoner"), "Hook Execute Prisoner", ref settings.hkDevHookExecutePrisoner, id: "Developer/HeroKarma/ExecutePrisonerToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/TendOutsider"), "Hook Tend Outsider", ref settings.hkDevHookTendOutsider, id: "Developer/HeroKarma/TendOutsiderToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/ReleasePrisoner"), "Hook Release Prisoner", ref settings.hkDevHookReleasePrisoner, id: "Developer/HeroKarma/ReleasePrisonerToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/EnslaveAttempt"), "Hook Enslave Attempt", ref settings.hkDevHookEnslaveAttempt, id: "Developer/HeroKarma/EnslaveAttemptToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/OrganHarvest"), "Hook Organ Harvest", ref settings.hkDevHookOrganHarvest, id: "Developer/HeroKarma/OrganHarvestToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/CharityGift"), "Hook Charity Gift", ref settings.hkDevHookCharityGift, id: "Developer/HeroKarma/CharityGiftToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/AttackNeutral"), "Hook Attack Neutral", ref settings.hkDevHookAttackNeutral, id: "Developer/HeroKarma/AttackNeutralToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/ArrestNeutral"), "Hook Arrest Neutral", ref settings.hkDevHookArrestNeutral, id: "Developer/HeroKarma/ArrestNeutralToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/RescueOutsider"), "Hook Rescue Outsider", ref settings.hkDevHookRescueOutsider, id: "Developer/HeroKarma/RescueOutsiderToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/StabilizeOutsider"), "Hook Stabilize Outsider", ref settings.hkDevHookStabilizeOutsider, id: "Developer/HeroKarma/StabilizeOutsiderToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/KillDownedNeutral"), "Hook Kill Downed Neutral", ref settings.hkDevHookKillDownedNeutral, id: "Developer/HeroKarma/KillDownedNeutralToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/HarmGuest"), "Hook Harm Guest", ref settings.hkDevHookHarmGuest, id: "Developer/HeroKarma/HarmGuestToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/FreeSlave"), "Hook Free Slave", ref settings.hkDevHookFreeSlave, id: "Developer/HeroKarma/FreeSlaveToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/DonateToBeggars"), "Hook Donate To Beggars", ref settings.hkDevHookDonateToBeggars, id: "Developer/HeroKarma/DonateToBeggarsToggle");
        DrawCheckboxRow(ctx, v.NextRow(UIRectTag.Checkbox, "Developer/HeroKarma/SellCaptive"), "Hook Sell Captive", ref settings.hkDevHookSellCaptive, id: "Developer/HeroKarma/SellCaptiveToggle");
    }

    private static float MeasureSectionHeight(UIContext ctx, int rowCount)
    {
        float pad = ctx.Style.Pad * 2f;
        float title = SectionHeaderHeight;
        float gap = ctx.Style.Gap;
        float rowsHeight = rowCount * ctx.Style.RowHeight;
        float rowGaps = Mathf.Max(0, rowCount - 1) * gap;
        return pad + title + gap + rowsHeight + rowGaps + (ctx.Style.Gap * 2f);
    }

    private static void DrawSectionHeader(UIContext ctx, ref D2VStack v, string title, string id)
    {
        D2Section.DrawCaptionStrip(ctx, v.Next(SectionHeaderHeight, UIRectTag.Label, id + "/Rect"), title, id, GameFont.Medium);
    }

    private static void DrawCheckboxRow(UIContext ctx, Rect rect, string label, ref bool value, bool enabled = true, string disabledReason = null, string id = null)
    {
        ctx.RecordRect(rect, UIRectTag.Checkbox, id ?? label, null);
        if (ctx.Pass == UIPass.Measure)
            return;

        if (!enabled && !disabledReason.NullOrEmpty())
            TooltipHandler.TipRegion(rect, disabledReason);

        using (new GUIEnabledScope(enabled))
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
        {
            Widgets.CheckboxLabeled(rect, label, ref value);
        }
    }
    private static void TryRefreshAllLovinPartsForSettingsChange()
    {
        try
        {
            Type compType = AccessTools.TypeByName("Despicable.CompLovinParts");
            AccessTools.Method(compType, "RefreshAllLovinPartsForSettingsChange")?.Invoke(null, null);
        }
        catch (System.Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("D2ModSettingsRenderer.TryRefreshLovinParts", "Mod settings refresh could not notify Lovin visuals about a settings change; continuing without the best-effort refresh.", ex);
        }
    }
}
