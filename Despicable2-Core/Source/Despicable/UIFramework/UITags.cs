namespace Despicable.UIFramework;
/// <summary>
/// Lightweight tags for rect validation and debug overlay.
/// Keep this generic; modules can encode detail in the optional label string.
/// </summary>
public enum UIRectTag
{
    None = 0,

    // Window sections
    Header,
    Body,
    Footer,

    // Containers / surfaces
    Panel,
    PanelSoft,
    Background,
    Group,
    ScrollView,
    Tab,

    // Common elements
    Label,
    Button,
    Icon,
    Input,
    Checkbox,
    Slider,
    TextField,
    TextArea,
    Divider,

    // List semantics
    ListItem,
    ListRow,
    ListRowSelected,

    // Special-purpose overlays / hitboxes
    TooltipHotspot,
    Highlight,
    DebugOverlay,

    // Blueprint / surfaces beyond windows
    Blueprint_ITab,
    Blueprint_FloatMenu,
    Blueprint_Gizmo,

    // Controls (semantic)
    Control_Selector,
    Control_Search,
    Control_Field,
    Control_MenuRow,

    // Text semantics
    Text_Wrapped,
    Text_Bullet,
    Text_Header,

    Custom
}

public enum UIValidationMode
{
    Off = 0,
    ErrorsOnly = 1,
    Strict = 2
}

public enum UIIssueSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

