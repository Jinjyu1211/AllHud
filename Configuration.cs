using Dalamud.Configuration;
using System.Numerics;

namespace AllHud;

public enum ThemePreset {
    Default,
    Custom,
    Imported,
}

public sealed class Configuration : IPluginConfiguration {
    private const int CurrentVersion = 76;

    public const string TaskBarComponentTime = "time";
    public const string TaskBarComponentLocalTime = "local_time";
    public const string TaskBarComponentEorzeaTime = "eorzea_time";
    public const string TaskBarComponentFps = "fps";
    public const string TaskBarComponentVolume = "volume";
    public const string TaskBarComponentMainMenu = "main_menu";
    public const string TaskBarComponentPluginList = "plugin_list";
    public const string TaskBarComponentPluginShortcut = "plugin_shortcut";
    public const string TaskBarComponentCustomShortcut = "custom_shortcut";
    public const string TaskBarComponentQuickMenu = "quick_menu";
    public const string TaskBarComponentServerInfo = "server_info";
    public const string TaskBarComponentInventory = "inventory";
    public const string TaskBarComponentSaddlebag = "saddlebag";
    public const string TaskBarComponentTeleport = "teleport";
    public const string TaskBarComponentCoordinates = "coordinates";
    public const string TaskBarComponentGearsetSwitcher = "gearset_switcher";
    public const string TaskBarComponentCurrency = "currency";

    public static readonly string[] DefaultTaskBarComponentOrder = [
        TaskBarComponentTime,
        TaskBarComponentFps,
        TaskBarComponentVolume,
        TaskBarComponentMainMenu,
        TaskBarComponentPluginList,
        TaskBarComponentPluginShortcut,
        TaskBarComponentServerInfo,
        TaskBarComponentInventory,
        TaskBarComponentSaddlebag,
    ];

    private static readonly string[] KnownTaskBarComponentIds = [
        TaskBarComponentTime,
        TaskBarComponentFps,
        TaskBarComponentVolume,
        TaskBarComponentMainMenu,
        TaskBarComponentPluginList,
        TaskBarComponentPluginShortcut,
        TaskBarComponentCustomShortcut,
        TaskBarComponentQuickMenu,
        TaskBarComponentServerInfo,
        TaskBarComponentInventory,
        TaskBarComponentSaddlebag,
        TaskBarComponentTeleport,
        TaskBarComponentCoordinates,
        TaskBarComponentGearsetSwitcher,
        TaskBarComponentCurrency,
    ];

    private static readonly HashSet<string> KnownTaskBarComponentIdSet = KnownTaskBarComponentIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static readonly Vector4 DefaultSelfAppliedTimerColor = new(0.45f, 1.0f, 0.55f, 1.0f);
    public static readonly Vector4 DefaultOtherAppliedTimerColor = new(1.0f, 1.0f, 1.0f, 1.0f);

    public int Version { get; set; } = 1;

    public bool Locked { get; set; }
    public bool TargetInfoLocked { get; set; }
    public bool StatusBarLocked { get; set; }
    public bool StatusBarMousePassthrough { get; set; }
    public bool PartyInfoMousePassthrough { get; set; }
    public bool ShowStatusOverlay { get; set; }
    public bool ShowCustomTargetInfo { get; set; } = true;
    public Vector2 CustomTargetInfoPosition { get; set; } = new(560.0f, 160.0f);
    public float CustomTargetInfoWidth { get; set; } = 500.0f;
    public float CustomTargetInfoBackgroundOpacity { get; set; }
    public int CustomTargetInfoStatusRows { get; set; } = 2;
    public bool CustomTargetInfoStatusesAboveHp { get; set; }
    public bool CustomTargetInfoHideHpNumbers { get; set; }
    public bool CustomTargetInfoHideMaxHp { get; set; }
    public bool CustomTargetInfoSplitCastBar { get; set; }
    public bool CustomTargetInfoSplitStatusBar { get; set; }
    public Vector2 CustomTargetInfoCastBarPosition { get; set; } = new(760.0f, 220.0f);
    public Vector2 CustomTargetInfoStatusBarPosition { get; set; } = new(560.0f, 220.0f);
    public float CustomTargetInfoScale { get; set; } = 1.0f;
    public float CustomTargetInfoCastBarScale { get; set; } = 1.0f;
    public float CustomTargetInfoStatusBarScale { get; set; } = 1.0f;
    public int CustomTargetInfoCastBarPlacement { get; set; }
    public bool ShowSelfBuffs { get; set; } = true;
    public bool ShowSelfEnfeeblements { get; set; } = true;
    public bool ShowSelfOtherStatuses { get; set; } = true;
    public bool ShowSelfConditionalEnhancements { get; set; } = true;
    public bool ShowTargetDots { get; set; } = true;
    public int StatusBarLayoutMode { get; set; }
    public bool ShowPartyInfo { get; set; } = true;
    public bool ShowPartyMitigationCooldowns { get; set; } = true;
    public bool ShowPartyFoodCheck { get; set; }
    public bool ShowPartyLimitBreakBar { get; set; }
    public int PartyLimitBreakBarPosition { get; set; }
    public bool ShowTaskBar { get; set; }
    public int TaskBarEdge { get; set; }
    public bool TaskBarStretchToEdges { get; set; }
    public float TaskBarHorizontalOffset { get; set; } = 0.5f;
    public float TaskBarScale { get; set; } = 1.0f;
    public float TaskBarOpacity { get; set; } = 1.0f;
    public bool ShowAuxiliaryBar { get; set; }
    public int AuxiliaryBarPositionMode { get; set; }
    public float AuxiliaryBarScale { get; set; } = 1.0f;
    public float AuxiliaryBarOpacity { get; set; } = 1.0f;
    public List<AuxiliaryBarDefinition> AuxiliaryBars { get; set; } = [];
    public bool TaskBarShowLocalTime { get; set; } = true;
    public bool TaskBarShowEorzeaTime { get; set; } = true;
    public bool TaskBarShowJob { get; set; } = true;
    public bool TaskBarShowHpMp { get; set; } = true;
    public bool TaskBarShowTerritory { get; set; } = true;
    public bool TaskBarShowFps { get; set; } = true;
    public bool TaskBarShowMainMenu { get; set; } = true;
    public bool TaskBarShowVolume { get; set; } = true;
    public bool TaskBarShowPluginList { get; set; } = true;
    public bool TaskBarShowPluginShortcut { get; set; }
    public bool TaskBarShowServerInfoBar { get; set; } = true;
    public bool TaskBarShowTeleport { get; set; }
    public bool TaskBarShowCoordinates { get; set; }
    public bool TaskBarShowCoordinatesTerritory { get; set; } = true;
    public bool TaskBarShowCoordinatesPosition { get; set; } = true;
    public int TaskBarCoordinatesDisplayMode { get; set; }
    public bool TaskBarShowGearsetSwitcher { get; set; }
    public bool TaskBarGearsetShowNumber { get; set; } = true;
    public bool TaskBarGearsetShowName { get; set; }
    public bool TaskBarGearsetShowLevel { get; set; } = true;
    public bool TaskBarGearsetClosePopupOnSwitch { get; set; } = true;
    public bool TaskBarShowCurrency { get; set; }
    public uint TaskBarCurrencyItemId { get; set; } = 1;
    public bool TaskBarCurrencyShowName { get; set; }
    public List<uint> VisibleCurrencyItemIds { get; set; } = [];
    public string TaskBarPluginShortcutInternalName { get; set; } = string.Empty;
    public List<string> PluginListInternalNames { get; set; } = [];
    public Dictionary<string, string> PluginShortcutInternalNames { get; set; } = [];
    public Dictionary<string, CustomShortcutDefinition> CustomShortcuts { get; set; } = [];
    public Dictionary<string, QuickMenuDefinition> QuickMenus { get; set; } = [];
    public bool TaskBarShowInventory { get; set; }
    public bool TaskBarShowSaddlebag { get; set; }
    public bool TaskBarDownloadPluginIcons { get; set; }
    public int TaskBarServerInfoBarMode { get; set; }
    public bool ShowTargetMitigationCooldowns { get; set; } = true;
    public bool ShowPersonalMitigationCooldowns { get; set; } = true;
    public bool ShowMitigationCooldowns { get; set; } = true;
    public bool ShowRawStatusIds { get; set; }
    public bool OnlyShowSelfAppliedTargetStatuses { get; set; } = true;
    public bool HideExpiredCooldowns { get; set; }
    public bool ShowSourceJobNames { get; set; }
    public bool ShowStatusPreview { get; set; }
    public bool ShowTargetInfoPreview { get; set; }
    // 队伍冷却面板（副本内按队友分行的团辅/爆发/减伤/长CD 冷却总览）
    public bool ShowSelfCooldownBar { get; set; }
    public float SelfCooldownBarScale { get; set; } = 1.0f;
    public float SelfCooldownBarOpacity { get; set; } = 1.0f;
    public bool SelfCooldownBarLocked { get; set; }
    public bool SelfCooldownBarHideWhenReady { get; set; }
    public bool SelfCooldownBarSelfOnly { get; set; }
    public bool SelfCooldownBarHideSelf { get; set; }
    public int SelfCooldownBarLayoutDirection { get; set; }
    public bool ShowSelfCooldownBarPreview { get; set; }
    public bool ShowPartyInfoPreview { get; set; }
    public Vector2 SelfCooldownBarPosition { get; set; } = new(760.0f, 520.0f);
    public bool CustomThemeEnabled { get; set; }
    public Vector4 CustomThemeAccentColor { get; set; } = new(0.84f, 0.34f, 0.52f, 1.0f);
    public Vector4 CustomThemeBackgroundColor { get; set; } = new(0.992f, 0.940f, 0.948f, 1.0f);
    public Vector4 CustomThemeTextColor { get; set; } = new(0.42f, 0.28f, 0.35f, 1.0f);
    public Dictionary<string, Vector4>? ImportedStyleColors { get; set; }
    public Dictionary<string, float>? ImportedStyleVars { get; set; }
    public string? ImportedStyleName { get; set; }
    public Vector4? CustomThemeNavBgColor { get; set; }
    public Vector4? CustomThemeNavBorderColor { get; set; }
    public ThemePreset ActiveThemePreset { get; set; } = ThemePreset.Default;

    // 状态栏（自身 Buff/Debuff）面板外观
    public float StatusPanelBackgroundOpacity { get; set; } = 0.80f;
    public float StatusPanelBorderOpacity { get; set; } = 0.82f;
    public float StatusPanelShadowOpacity { get; set; } = 0.12f;
    public float StatusSectionLabelBackgroundOpacity { get; set; } = 0.88f;
    public float StatusSectionLabelBorderOpacity { get; set; } = 0.55f;
    public float StatusSectionDividerOpacity { get; set; } = 0.20f;
    public bool StatusPanelUseAccentColor { get; set; } = false;
    public Vector4 StatusPanelCustomBackground { get; set; } = new(0.10f, 0.10f, 0.12f, 0.80f);
    public Vector4 StatusPanelCustomBorder { get; set; } = new(0.50f, 0.50f, 0.55f, 0.82f);
    public Vector4 StatusPanelCustomShadow { get; set; } = new(0.00f, 0.00f, 0.00f, 0.12f);
    public Vector4 StatusSectionCustomLabelBackground { get; set; } = new(0.12f, 0.12f, 0.14f, 0.88f);
    public Vector4 StatusSectionCustomLabelBorder { get; set; } = new(0.50f, 0.50f, 0.55f, 0.55f);
    public Vector4 StatusSectionCustomDivider { get; set; } = new(0.50f, 0.50f, 0.55f, 0.20f);
    public List<string> HiddenImGuiWindowNames { get; set; } = [];
    public List<string> TaskBarComponentOrder { get; set; } = [.. DefaultTaskBarComponentOrder];
    public List<string> TaskBarLeftComponentOrder { get; set; } = [];
    public List<string> TaskBarCenterComponentOrder { get; set; } = [];
    public List<string> TaskBarRightComponentOrder { get; set; } = [];
    public float Scale { get; set; } = 1.0f;
    public float IconSize { get; set; } = 20.0f;
    public int StatusIconsPerRow { get; set; } = 8;
    public Vector4 SelfAppliedTimerColor { get; set; } = DefaultSelfAppliedTimerColor;
    public Vector4 OtherAppliedTimerColor { get; set; } = DefaultOtherAppliedTimerColor;
    public Vector2 StatusOverlayPosition { get; set; } = new(760.0f, 360.0f);
    public float MaxTargetStatusDurationSeconds { get; set; } = 120.0f;
    public float ExpiredCooldownGraceSeconds { get; set; } = 5.0f;
    public uint SelectedJobSkillConfigClassJobId { get; set; } = 19;
    public bool JobSkillSelectionInitialized { get; set; }
    public List<string> EnabledJobSkillKeys { get; set; } = [];
    public bool JobActionSelectionInitialized { get; set; }
    public List<string> EnabledJobActionKeys { get; set; } = [];
    public List<CustomTrackedDefinition> CustomTrackedDefinitions { get; set; } = [];
    public List<CustomCurrencyDefinition> CustomCurrencies { get; set; } = [];

    public bool ApplyMigrations() {
        var changed = false;

        if (Version < 2) {
            ShowPartyMitigationCooldowns = true;
            ShowTargetMitigationCooldowns = true;
            ShowPersonalMitigationCooldowns = true;
            changed = true;
        }

        if (Version < 3) {
            var mergedMitigationVisibility = ShowPartyMitigationCooldowns || ShowTargetMitigationCooldowns;
            if (ShowPartyMitigationCooldowns != mergedMitigationVisibility) {
                ShowPartyMitigationCooldowns = mergedMitigationVisibility;
                changed = true;
            }

            if (ShowTargetMitigationCooldowns != mergedMitigationVisibility) {
                ShowTargetMitigationCooldowns = mergedMitigationVisibility;
                changed = true;
            }

            changed = true;
        }

        if (Version < 4) {
            if (StatusIconsPerRow <= 0) {
                StatusIconsPerRow = 8;
                changed = true;
            }

            if (SelfAppliedTimerColor == default) {
                SelfAppliedTimerColor = DefaultSelfAppliedTimerColor;
                changed = true;
            }

            if (OtherAppliedTimerColor == default) {
                OtherAppliedTimerColor = DefaultOtherAppliedTimerColor;
                changed = true;
            }

            changed = true;
        }

        if (Version < 5) {
            if (StatusOverlayPosition == default) {
                StatusOverlayPosition = new Vector2(760.0f, 360.0f);
                changed = true;
            }

            changed = true;
        }

        if (Version < 6) {
            EnabledJobActionKeys ??= [];
            AddMissingEnabledJobActionKey("31:2887", ref changed); // 机工：武装解除
            AddMissingEnabledJobActionKey("25:157", ref changed);  // 黑魔：魔罩
            changed = true;
        }

        if (Version < 8) {
            EnabledJobActionKeys ??= [];
            AddMissingEnabledJobActionKey("19:36920", ref changed); // 骑士：极致防御（预警升级）
            AddMissingEnabledJobActionKey("21:36923", ref changed); // 战士：戮罪（复仇升级）
            AddMissingEnabledJobActionKey("21:16464", ref changed); // 战士：原初的勇猛
            AddMissingEnabledJobActionKey("32:36927", ref changed); // 暗骑：暗影守夜（暗影墙升级）
            AddMissingEnabledJobActionKey("37:36935", ref changed); // 枪刃：大星云（星云升级）
            AddMissingEnabledJobActionKey("24:25861", ref changed); // 白魔：水流幕
            AddMissingEnabledJobActionKey("24:7432", ref changed);  // 白魔：神祝祷
            AddMissingEnabledJobActionKey("28:7434", ref changed);  // 学者：深谋远虑之策
            AddMissingEnabledJobActionKey("28:25867", ref changed); // 学者：生命回生法
            AddMissingEnabledJobActionKey("33:16556", ref changed); // 占星：天星交错
            AddMissingEnabledJobActionKey("33:25873", ref changed); // 占星：擢升
            AddMissingEnabledJobActionKey("40:24303", ref changed); // 贤者：白牛清汁
            AddMissingEnabledJobActionKey("40:24305", ref changed); // 贤者：输血
            AddMissingEnabledJobActionKey("40:24317", ref changed); // 贤者：混合
            changed = true;
        }

        if (Version < 9) {
            EnabledJobActionKeys ??= [];
            AddMissingEnabledJobActionKey("27:25799", ref changed); // 召唤：灿烂之盾
            changed = true;
        }

        if (Version < 15) {
            ShowStatusPreview = false;
            changed = true;
        }

        if (Version < 20) {
            changed = true;
        }

        if (Version < 21) {
            ShowCustomTargetInfo = true;
            CustomTargetInfoPosition = new Vector2(560.0f, 160.0f);
            CustomTargetInfoWidth = 320.0f;
            ShowStatusOverlay = false;
            changed = true;
        }

        if (Version < 22) {
            CustomTargetInfoBackgroundOpacity = 0.22f;
            changed = true;
        }

        if (Version < 23) {
            CustomTargetInfoWidth = 480.0f;
            CustomTargetInfoBackgroundOpacity = 0.0f;
            changed = true;
        }

        if (Version < 24) {
            changed = true;
        }

        if (Version < 25) {
            CustomTargetInfoWidth = 640.0f;
            changed = true;
        }

        if (Version < 26) {
            CustomTargetInfoWidth = 560.0f;
            changed = true;
        }

        if (Version < 27) {
            CustomTargetInfoWidth = 500.0f;
            changed = true;
        }

        if (Version < 28) {
            CustomTargetInfoStatusRows = 2;
            CustomTargetInfoStatusesAboveHp = false;
            changed = true;
        }

        if (Version < 29) {
            CustomTargetInfoSplitCastBar = false;
            CustomTargetInfoSplitStatusBar = false;
            CustomTargetInfoCastBarPosition = new Vector2(760.0f, 220.0f);
            CustomTargetInfoStatusBarPosition = new Vector2(560.0f, 220.0f);
            CustomTargetInfoScale = 1.0f;
            CustomTargetInfoCastBarScale = 1.0f;
            CustomTargetInfoStatusBarScale = 1.0f;
            changed = true;
        }

        if (Version < 30) {
            CustomTargetInfoCastBarPlacement = 0;
            changed = true;
        }

        if (Version < 31) {
            ShowSelfEnfeeblements = true;
            ShowSelfOtherStatuses = true;
            ShowSelfConditionalEnhancements = true;
            changed = true;
        }

        if (Version < 32) {
            ShowPartyFoodCheck = false;
            changed = true;
        }

        if (Version < 33) {
            ShowPartyLimitBreakBar = false;
            PartyLimitBreakBarPosition = 0;
            changed = true;
        }

        if (Version < 34) {
            TargetInfoLocked = Locked;
            StatusBarLocked = Locked;
            changed = true;
        }

        if (Version < 35) {
            ShowTaskBar = false;
            TaskBarEdge = 0;
            TaskBarScale = 1.0f;
            TaskBarOpacity = 0.72f;
            TaskBarShowLocalTime = true;
            TaskBarShowEorzeaTime = true;
            TaskBarShowJob = true;
            TaskBarShowHpMp = true;
            TaskBarShowTerritory = true;
            TaskBarShowFps = true;
            TaskBarShowMainMenu = true;
            TaskBarShowVolume = true;
            TaskBarShowPluginList = true;
            TaskBarShowServerInfoBar = true;
            changed = true;
        }

        if (Version < 36) {
            if (TaskBarEdge is < 0 or > 1) {
                TaskBarEdge = 0;
                changed = true;
            }
        }

        if (Version < 37) {
            TaskBarDownloadPluginIcons = false;
            changed = true;
        }

        if (Version < 38) {
            TaskBarDownloadPluginIcons = true;
            changed = true;
        }

        if (Version < 39) {
            TaskBarStretchToEdges = false;
            changed = true;
        }

        if (Version < 40) {
            TaskBarServerInfoBarMode = 0;
            changed = true;
        }

        if (Version < 41) {
            changed = true;
        }

        if (Version < 43) {
            HiddenImGuiWindowNames ??= [];
            changed = true;
        }

        if (Version < 44) {
            TaskBarComponentOrder = [.. DefaultTaskBarComponentOrder];
            changed = true;
        }

        if (Version < 45) {
            TaskBarComponentOrder = MergeLegacyTimeComponents(TaskBarComponentOrder).ToList();
            TaskBarShowLocalTime = TaskBarShowLocalTime || TaskBarShowEorzeaTime;
            changed = true;
        }

        if (Version < 46) {
            ShowAuxiliaryBar = false;
            AuxiliaryBarPositionMode = 0;
            AuxiliaryBarScale = 1.0f;
            AuxiliaryBarOpacity = 1.0f;
            changed = true;
        }

        if (Version < 47) {
            AuxiliaryBars = [new AuxiliaryBarDefinition {
                Enabled = ShowAuxiliaryBar,
                Name = "辅助栏 1",
                PositionMode = AuxiliaryBarPositionMode,
                Scale = AuxiliaryBarScale,
                Opacity = AuxiliaryBarOpacity,
            }];
            changed = true;
        }

        if (Version < 48) {
            if (Math.Abs(TaskBarOpacity - 0.72f) < 0.001f) {
                TaskBarOpacity = 1.0f;
            }

            changed = true;
        }

        if (Version < 49) {
            AuxiliaryBars ??= [];
            foreach (var bar in AuxiliaryBars) {
                if (IsGeneratedAuxiliaryBarName(bar.Name)) {
                    bar.Name = "辅助栏";
                }
            }

            changed = true;
        }

        if (Version < 50) {
            if (Math.Abs(AuxiliaryBarOpacity - 0.72f) < 0.001f) {
                AuxiliaryBarOpacity = 1.0f;
            }

            AuxiliaryBars ??= [];
            foreach (var bar in AuxiliaryBars) {
                if (Math.Abs(bar.Opacity - 0.72f) < 0.001f) {
                    bar.Opacity = 1.0f;
                }
            }

            changed = true;
        }

        if (Version < 51) {
            AuxiliaryBars = CollapseDuplicateDefaultAuxiliaryBars(AuxiliaryBars);
            changed = true;
        }

        if (Version < 57) {
            TaskBarCenterComponentOrder = TaskBarComponentOrder is { Count: > 0 }
                ? [.. TaskBarComponentOrder]
                : [.. DefaultTaskBarComponentOrder];
            changed = true;
        }

        if (Version < 58) {
            TaskBarComponentOrder = RemoveUnconfiguredPluginShortcutComponents(TaskBarComponentOrder);
            TaskBarLeftComponentOrder = RemoveUnconfiguredPluginShortcutComponents(TaskBarLeftComponentOrder);
            TaskBarCenterComponentOrder = RemoveUnconfiguredPluginShortcutComponents(TaskBarCenterComponentOrder);
            TaskBarRightComponentOrder = RemoveUnconfiguredPluginShortcutComponents(TaskBarRightComponentOrder);
            foreach (var bar in AuxiliaryBars) {
                bar.ComponentOrder = RemoveUnconfiguredPluginShortcutComponents(bar.ComponentOrder);
            }

            changed = true;
        }

        if (Version < 59) {
            foreach (var bar in AuxiliaryBars) {
                if (bar.SectionCenterComponentOrder.Count == 0 && bar.ComponentOrder.Count > 0) {
                    bar.SectionCenterComponentOrder = [.. bar.ComponentOrder];
                }
            }

            changed = true;
        }

        if (Version < 60) {
            TaskBarComponentOrder = RemoveBaseQuickMenuComponents(TaskBarComponentOrder);
            TaskBarLeftComponentOrder = RemoveBaseQuickMenuComponents(TaskBarLeftComponentOrder);
            TaskBarCenterComponentOrder = RemoveBaseQuickMenuComponents(TaskBarCenterComponentOrder);
            TaskBarRightComponentOrder = RemoveBaseQuickMenuComponents(TaskBarRightComponentOrder);
            foreach (var bar in AuxiliaryBars) {
                bar.ComponentOrder = RemoveBaseQuickMenuComponents(bar.ComponentOrder);
                bar.SectionStartComponentOrder = RemoveBaseQuickMenuComponents(bar.SectionStartComponentOrder);
                bar.SectionCenterComponentOrder = RemoveBaseQuickMenuComponents(bar.SectionCenterComponentOrder);
                bar.SectionEndComponentOrder = RemoveBaseQuickMenuComponents(bar.SectionEndComponentOrder);
            }

            changed = true;
        }

        if (Version < 61) {
            changed = true;
        }

        if (Version < 62) {
            TaskBarComponentOrder = RemoveOptionalTaskBarComponents(TaskBarComponentOrder);
            TaskBarLeftComponentOrder = RemoveOptionalTaskBarComponents(TaskBarLeftComponentOrder);
            TaskBarCenterComponentOrder = RemoveOptionalTaskBarComponents(TaskBarCenterComponentOrder);
            TaskBarRightComponentOrder = RemoveOptionalTaskBarComponents(TaskBarRightComponentOrder);
            foreach (var bar in AuxiliaryBars) {
                bar.ComponentOrder = RemoveOptionalTaskBarComponents(bar.ComponentOrder);
                bar.SectionStartComponentOrder = RemoveOptionalTaskBarComponents(bar.SectionStartComponentOrder);
                bar.SectionCenterComponentOrder = RemoveOptionalTaskBarComponents(bar.SectionCenterComponentOrder);
                bar.SectionEndComponentOrder = RemoveOptionalTaskBarComponents(bar.SectionEndComponentOrder);
            }

            TaskBarShowTeleport = false;
            TaskBarShowCoordinates = false;
            TaskBarShowGearsetSwitcher = false;
            TaskBarShowCurrency = false;
            changed = true;
        }

        if (Version < 63) {
            if (TaskBarCoordinatesDisplayMode == 1) {
                TaskBarShowCoordinatesTerritory = true;
                TaskBarShowCoordinatesPosition = false;
            } else if (TaskBarCoordinatesDisplayMode == 2) {
                TaskBarShowCoordinatesTerritory = false;
                TaskBarShowCoordinatesPosition = true;
            } else {
                TaskBarShowCoordinatesTerritory = true;
                TaskBarShowCoordinatesPosition = true;
            }

            changed = true;
        }

        if (Version < 64) {
            TaskBarGearsetShowNumber = true;
            TaskBarGearsetShowName = false;
            TaskBarGearsetClosePopupOnSwitch = true;
            changed = true;
        }

        if (Version < 65) {
            TaskBarGearsetShowLevel = true;
            changed = true;
        }

        if (Version < 66) {
            StatusBarLayoutMode = 0;
            changed = true;
        }

        if (Version < 67) {
            MigrateQuickTeleportCustomShortcutIcon();
            changed = true;
        }

        if (Version < 68) {
            // 团减固定在队伍信息显示，清理独立监控里历史勾选的团减残留键。
            EnabledJobActionKeys ??= [];
            var removed = EnabledJobActionKeys.RemoveAll(IsPartyInfoActionKey);
            if (removed > 0) {
                changed = true;
            }

            changed = true;
        }

        if (Version < 69) {
            // 队伍信息已固定显示的技能不再留在独立监控里，避免同一个技能重复出现在两块 UI。
            EnabledJobActionKeys ??= [];
            var removed = EnabledJobActionKeys.RemoveAll(IsPartyInfoActionKey);
            if (removed > 0) {
                changed = true;
            }

            changed = true;
        }

        if (Version < 70) {
            SelfCooldownBarLayoutDirection = Math.Clamp(SelfCooldownBarLayoutDirection, 0, 1);
            changed = true;
        }

        if (Version < 71) {
            StatusBarMousePassthrough = false;
            PartyInfoMousePassthrough = false;
            changed = true;
        }

        if (Version < 72) {
            CustomThemeEnabled = false;
            CustomThemeAccentColor = new Vector4(0.84f, 0.34f, 0.52f, 1.0f);
            CustomThemeBackgroundColor = new Vector4(0.992f, 0.940f, 0.948f, 1.0f);
            CustomThemeTextColor = new Vector4(0.42f, 0.28f, 0.35f, 1.0f);
            changed = true;
        }

        if (Version < 73) {
            CustomCurrencies = [];
            changed = true;
        }

        if (Version < 74) {
            CustomThemeNavBgColor = null;
            CustomThemeNavBorderColor = null;
            ImportedStyleColors = null;
            ImportedStyleVars = null;
            ImportedStyleName = null;
            changed = true;
        }

        if (Version < 75) {
            ActiveThemePreset = ImportedStyleColors is { Count: > 0 }
                ? ThemePreset.Imported
                : CustomThemeEnabled
                    ? ThemePreset.Custom
                    : ThemePreset.Default;
            changed = true;
        }

        if (Version < 76) {
            VisibleCurrencyItemIds ??= [];
            changed = true;
        }

        StatusIconsPerRow = Math.Clamp(StatusIconsPerRow, 1, 12);
        StatusBarLayoutMode = Math.Clamp(StatusBarLayoutMode, 0, 1);
        IconSize = Math.Clamp(IconSize, 12.0f, 64.0f);
        CustomTargetInfoWidth = 500.0f;
        CustomTargetInfoBackgroundOpacity = Math.Clamp(CustomTargetInfoBackgroundOpacity, 0.0f, 0.80f);
        CustomTargetInfoStatusRows = Math.Clamp(CustomTargetInfoStatusRows, 1, 2);
        CustomTargetInfoScale = ClampHudScale(CustomTargetInfoScale);
        CustomTargetInfoCastBarScale = ClampHudScale(CustomTargetInfoCastBarScale);
        CustomTargetInfoStatusBarScale = ClampHudScale(CustomTargetInfoStatusBarScale);
        CustomTargetInfoCastBarPlacement = Math.Clamp(CustomTargetInfoCastBarPlacement, 0, 2);
        PartyLimitBreakBarPosition = Math.Clamp(PartyLimitBreakBarPosition, 0, 1);
        SelfCooldownBarLayoutDirection = Math.Clamp(SelfCooldownBarLayoutDirection, 0, 1);
        TaskBarEdge = TaskBarEdge == 1 ? 1 : 0;
        TaskBarServerInfoBarMode = Math.Clamp(TaskBarServerInfoBarMode, 0, 1);
        TaskBarScale = ClampHudScale(TaskBarScale);
        TaskBarOpacity = Math.Clamp(TaskBarOpacity, 0.15f, 1.0f);
        AuxiliaryBarPositionMode = Math.Clamp(AuxiliaryBarPositionMode, 0, 2);
        AuxiliaryBarScale = ClampHudScale(AuxiliaryBarScale);
        AuxiliaryBarOpacity = Math.Clamp(AuxiliaryBarOpacity, 0.15f, 1.0f);
        AuxiliaryBars = NormalizeAuxiliaryBars(AuxiliaryBars);
        var firstAuxiliaryBar = AuxiliaryBars.FirstOrDefault();
        ShowAuxiliaryBar = firstAuxiliaryBar?.Enabled ?? false;
        AuxiliaryBarPositionMode = firstAuxiliaryBar?.PositionMode ?? 0;
        AuxiliaryBarScale = firstAuxiliaryBar?.Scale ?? 1.0f;
        AuxiliaryBarOpacity = firstAuxiliaryBar?.Opacity ?? 1.0f;
        HiddenImGuiWindowNames = HiddenImGuiWindowNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        TaskBarComponentOrder = NormalizeTaskBarComponentOrder(TaskBarComponentOrder);
        TaskBarLeftComponentOrder = NormalizeTaskBarSectionComponentOrder(TaskBarLeftComponentOrder);
        TaskBarCenterComponentOrder = NormalizeTaskBarSectionComponentOrder(TaskBarCenterComponentOrder);
        TaskBarRightComponentOrder = NormalizeTaskBarSectionComponentOrder(TaskBarRightComponentOrder);
        TaskBarPluginShortcutInternalName = TaskBarPluginShortcutInternalName.Trim();
        TaskBarGearsetShowNumber = TaskBarGearsetShowNumber || (!TaskBarGearsetShowName && !TaskBarGearsetShowLevel);
        PluginListInternalNames = NormalizePluginListInternalNames(PluginListInternalNames);
        PluginShortcutInternalNames = NormalizePluginShortcutInternalNames(PluginShortcutInternalNames);
        CustomShortcuts = NormalizeCustomShortcuts(CustomShortcuts);
        QuickMenus = NormalizeQuickMenus(QuickMenus);

        if (changed) {
            Version = CurrentVersion;
        }

        return changed;
    }

    private void AddMissingEnabledJobActionKey(string key, ref bool changed) {
        if (EnabledJobActionKeys.Any(existing => string.Equals(existing, key, StringComparison.Ordinal))) {
            return;
        }

        EnabledJobActionKeys.Add(key);
        changed = true;
    }

    // 独立监控键格式为 "{classJobId}:{actionId}"，命中队伍信息 actionId 即视为残留。
    private static bool IsPartyInfoActionKey(string key) {
        var separatorIndex = key.LastIndexOf(':');
        if (separatorIndex < 0 || separatorIndex >= key.Length - 1) {
            return false;
        }

        return uint.TryParse(key.AsSpan(separatorIndex + 1), out var actionId)
               && AllHud.Data.TrackedActionCatalog.PartyMitigationActionIds.Contains(actionId);
    }

    private static void AddMissingComponent(List<string> componentOrder, string componentId, ref bool changed) {
        if (componentOrder.Any(id => id.Equals(componentId, StringComparison.OrdinalIgnoreCase))) {
            return;
        }

        componentOrder.Add(componentId);
        changed = true;
    }

    private static float ClampHudScale(float scale) {
        return Math.Clamp(scale <= 0.0f ? 1.0f : scale, 0.6f, 2.0f);
    }

    private List<AuxiliaryBarDefinition> NormalizeAuxiliaryBars(IEnumerable<AuxiliaryBarDefinition>? bars) {
        var result = (bars ?? [])
            .Where(bar => bar is not null)
            .Select((bar, index) => new AuxiliaryBarDefinition {
                Enabled = bar.Enabled,
                Name = string.IsNullOrWhiteSpace(bar.Name) || IsGeneratedAuxiliaryBarName(bar.Name) ? "辅助栏" : bar.Name.Trim(),
                PositionMode = Math.Clamp(bar.PositionMode, 0, 2),
                StretchToEdges = bar.StretchToEdges,
                CustomPosition = bar.CustomPosition == default ? new Vector2(120.0f, 240.0f) : bar.CustomPosition,
                Scale = ClampHudScale(bar.Scale),
                Opacity = Math.Clamp(bar.Opacity <= 0.0f ? 1.0f : bar.Opacity, 0.15f, 1.0f),
                LayoutDirection = Math.Clamp(bar.LayoutDirection, 0, 1),
                ComponentOrder = NormalizeAuxiliaryComponentOrder(RemoveUnconfiguredPluginShortcutComponents(bar.ComponentOrder)),
                SectionStartComponentOrder = NormalizeAuxiliaryComponentOrder(RemoveUnconfiguredPluginShortcutComponents(bar.SectionStartComponentOrder)),
                SectionCenterComponentOrder = NormalizeAuxiliaryComponentOrder(RemoveUnconfiguredPluginShortcutComponents(bar.SectionCenterComponentOrder)),
                SectionEndComponentOrder = NormalizeAuxiliaryComponentOrder(RemoveUnconfiguredPluginShortcutComponents(bar.SectionEndComponentOrder)),
            })
            .ToList();

        result = CollapseDuplicateDefaultAuxiliaryBars(result);

        if (result.Count == 0) {
            result.Add(new AuxiliaryBarDefinition());
        }

        return result;
    }

    private static List<AuxiliaryBarDefinition> CollapseDuplicateDefaultAuxiliaryBars(IEnumerable<AuxiliaryBarDefinition>? bars) {
        var result = new List<AuxiliaryBarDefinition>();
        var hasDefaultBar = false;
        foreach (var bar in bars ?? []) {
            if (IsDefaultAuxiliaryBar(bar)) {
                if (hasDefaultBar) {
                    continue;
                }

                hasDefaultBar = true;
            }

            result.Add(bar);
        }

        return result;
    }

    private static bool IsDefaultAuxiliaryBar(AuxiliaryBarDefinition bar) {
        return !bar.Enabled
               && string.Equals(bar.Name?.Trim(), "辅助栏", StringComparison.Ordinal)
               && bar.PositionMode == 0
               && !bar.StretchToEdges
               && bar.ComponentOrder.Count == 0
               && Math.Abs(bar.Scale - 1.0f) < 0.001f
               && Math.Abs(bar.Opacity - 1.0f) < 0.001f;
    }

    public static bool IsPluginShortcutComponentId(string componentId) {
        return componentId.Equals(TaskBarComponentPluginShortcut, StringComparison.OrdinalIgnoreCase)
               || componentId.StartsWith(TaskBarComponentPluginShortcut + ":", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCustomShortcutComponentId(string componentId) {
        return componentId.Equals(TaskBarComponentCustomShortcut, StringComparison.OrdinalIgnoreCase)
               || componentId.StartsWith(TaskBarComponentCustomShortcut + ":", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsQuickMenuComponentId(string componentId) {
        return componentId.Equals(TaskBarComponentQuickMenu, StringComparison.OrdinalIgnoreCase)
               || componentId.StartsWith(TaskBarComponentQuickMenu + ":", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRepeatableComponentId(string componentId) {
        return IsPluginShortcutComponentId(componentId) || IsCustomShortcutComponentId(componentId) || IsQuickMenuComponentId(componentId);
    }

    public static string GetComponentBaseId(string componentId) {
        if (IsPluginShortcutComponentId(componentId)) {
            return TaskBarComponentPluginShortcut;
        }

        if (IsCustomShortcutComponentId(componentId)) {
            return TaskBarComponentCustomShortcut;
        }

        return IsQuickMenuComponentId(componentId) ? TaskBarComponentQuickMenu : componentId;
    }

    public static string CreatePluginShortcutComponentId() {
        return $"{TaskBarComponentPluginShortcut}:{Guid.NewGuid():N}";
    }

    public static string CreateCustomShortcutComponentId() {
        return $"{TaskBarComponentCustomShortcut}:{Guid.NewGuid():N}";
    }

    public static string CreateQuickMenuComponentId() {
        return $"{TaskBarComponentQuickMenu}:{Guid.NewGuid():N}";
    }

    public static List<string> NormalizeAuxiliaryComponentOrder(IEnumerable<string>? componentOrder) {
        return NormalizeTaskBarSectionComponentOrder(componentOrder);
    }

    public static List<string> NormalizeTaskBarSectionComponentOrder(IEnumerable<string>? componentOrder) {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in MergeLegacyTimeComponents(componentOrder)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Where(id => !id.Equals(TaskBarComponentQuickMenu, StringComparison.OrdinalIgnoreCase))
            .Where(id => KnownTaskBarComponentIdSet.Contains(GetComponentBaseId(id)))) {
            if (IsRepeatableComponentId(id) || seen.Add(id)) {
                result.Add(id);
            }
        }

        return result;
    }

    private List<string> RemoveUnconfiguredPluginShortcutComponents(IEnumerable<string>? componentOrder) {
        return (componentOrder ?? [])
            .Where(id => !IsPluginShortcutComponentId(id) || HasConfiguredPluginShortcut(id))
            .ToList();
    }

    private static List<string> RemoveBaseQuickMenuComponents(IEnumerable<string>? componentOrder) {
        return (componentOrder ?? [])
            .Where(id => !string.Equals(id?.Trim(), TaskBarComponentQuickMenu, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static List<string> RemoveOptionalTaskBarComponents(IEnumerable<string>? componentOrder) {
        return (componentOrder ?? [])
            .Where(id => !IsOptionalTaskBarComponentId(id?.Trim() ?? string.Empty))
            .ToList();
    }

    private static bool IsOptionalTaskBarComponentId(string componentId) {
        return componentId.Equals(TaskBarComponentTeleport, StringComparison.OrdinalIgnoreCase)
               || componentId.Equals(TaskBarComponentCoordinates, StringComparison.OrdinalIgnoreCase)
               || componentId.Equals(TaskBarComponentGearsetSwitcher, StringComparison.OrdinalIgnoreCase)
               || componentId.Equals(TaskBarComponentCurrency, StringComparison.OrdinalIgnoreCase);
    }

    private bool HasConfiguredPluginShortcut(string componentId) {
        if (componentId.Equals(TaskBarComponentPluginShortcut, StringComparison.OrdinalIgnoreCase)) {
            return !string.IsNullOrWhiteSpace(TaskBarPluginShortcutInternalName);
        }

        return PluginShortcutInternalNames.TryGetValue(componentId, out var internalName) && !string.IsNullOrWhiteSpace(internalName);
    }

    public static List<string> NormalizeTaskBarComponentOrder(IEnumerable<string>? componentOrder) {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in MergeLegacyTimeComponents(componentOrder)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Where(id => !id.Equals(TaskBarComponentQuickMenu, StringComparison.OrdinalIgnoreCase))
            .Where(id => KnownTaskBarComponentIdSet.Contains(GetComponentBaseId(id)))) {
            if (IsRepeatableComponentId(id) || seen.Add(id)) {
                result.Add(id);
            }
        }

        foreach (var id in DefaultTaskBarComponentOrder) {
            if (!result.Contains(id, StringComparer.OrdinalIgnoreCase)) {
                result.Add(id);
            }
        }

        return result;
    }

    private static Dictionary<string, string> NormalizePluginShortcutInternalNames(Dictionary<string, string>? shortcuts) {
        return (shortcuts ?? [])
            .Where(pair => IsPluginShortcutComponentId(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, CustomShortcutDefinition> NormalizeCustomShortcuts(Dictionary<string, CustomShortcutDefinition>? shortcuts) {
        var result = new Dictionary<string, CustomShortcutDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in shortcuts ?? []) {
            var key = pair.Key?.Trim() ?? string.Empty;
            if (!IsCustomShortcutComponentId(key) || key.Equals(TaskBarComponentCustomShortcut, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var value = pair.Value ?? new CustomShortcutDefinition();
            result[key] = new CustomShortcutDefinition {
                Name = string.IsNullOrWhiteSpace(value.Name) ? "快捷方式" : value.Name.Trim(),
                IconId = value.IconId,
                Command = (value.Command ?? string.Empty).Trim(),
            };
        }

        return result;
    }

    private void MigrateQuickTeleportCustomShortcutIcon() {
        foreach (var shortcut in CustomShortcuts.Values) {
            if (shortcut.IconId == 60314
                && string.Equals((shortcut.Command ?? string.Empty).Trim(), "/pdrtp", StringComparison.OrdinalIgnoreCase)) {
                shortcut.IconId = 60453;
            }
        }
    }

    private static Dictionary<string, QuickMenuDefinition> NormalizeQuickMenus(Dictionary<string, QuickMenuDefinition>? menus) {
        var result = new Dictionary<string, QuickMenuDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in menus ?? []) {
            var key = pair.Key?.Trim() ?? string.Empty;
            if (!IsQuickMenuComponentId(key) || key.Equals(TaskBarComponentQuickMenu, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var value = pair.Value ?? new QuickMenuDefinition();
            result[key] = new QuickMenuDefinition {
                Name = string.IsNullOrWhiteSpace(value.Name) ? "快捷菜单" : value.Name.Trim(),
                IconId = value.IconId,
                ComponentOrder = NormalizeTaskBarSectionComponentOrder(value.ComponentOrder)
                    .Where(id => !IsQuickMenuComponentId(id))
                    .ToList(),
            };
        }

        return result;
    }

    private static List<string> NormalizePluginListInternalNames(IEnumerable<string>? internalNames) {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var internalName in internalNames ?? []) {
            var trimmed = internalName?.Trim() ?? string.Empty;
            if (trimmed.Length > 0 && seen.Add(trimmed)) {
                result.Add(trimmed);
            }
        }

        return result;
    }

    private static IEnumerable<string> MergeLegacyTimeComponents(IEnumerable<string>? componentOrder) {
        var emittedTime = false;
        foreach (var rawId in componentOrder ?? []) {
            var id = rawId?.Trim() ?? string.Empty;
            if (id.Equals(TaskBarComponentLocalTime, StringComparison.OrdinalIgnoreCase)
                || id.Equals(TaskBarComponentEorzeaTime, StringComparison.OrdinalIgnoreCase)) {
                if (!emittedTime) {
                    emittedTime = true;
                    yield return TaskBarComponentTime;
                }

                continue;
            }

            yield return id;
        }
    }

    private static bool IsGeneratedAuxiliaryBarName(string? name) {
        const string prefix = "辅助栏 ";
        return !string.IsNullOrWhiteSpace(name)
               && name.Trim().StartsWith(prefix, StringComparison.Ordinal)
               && int.TryParse(name.Trim()[prefix.Length..], out _);
    }
}
