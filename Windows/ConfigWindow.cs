using AllHud.Data;
using AllHud.Models;
using AllHud.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using LuminaItem = Lumina.Excel.Sheets.Item;

namespace AllHud.Windows;

public sealed partial class ConfigWindow {
    private const int InstalledPluginSelectionCacheTtlMs = 1000;
    private int pushedColorCount;
    private int pushedVarCount;

    private enum ConfigPage {
        状态栏,
        目标情报,
        队伍信息,
        任务栏,
        技能,
        外观,
        调试,
    }

    private enum TaskBarPage {
        任务栏,
        辅助栏,
        插件收纳,
        高级,
    }

    private readonly record struct PickedImGuiWindow(string Name, string Key);
    private readonly record struct TaskBarComponentDefinition(string Id, string Icon, string Name, string Description);
    private readonly record struct CustomShortcutPreset(string Name, string DisplayName, uint IconId, string Command, string Description);
    private readonly record struct PendingCustomShortcutCommandRemoval(string RowUiId, bool IsPendingLine);

    private static readonly uint[] CommonCustomShortcutIconIds = [
        1, 2, 5, 7, 14, 17, 20, 21, 27, 40, 45, 60,
        74, 111, 112, 60073, 60074, 60411, 60412, 60413, 60414, 60415, 60453,
    ];

    private static readonly CustomShortcutPreset[] DailyRoutinesCustomShortcutPresets = [
        new("DR 木人：重置仇恨", "木人重置", 60413, "/pdr resetallsd", "Daily Routines 木人模块指令：重置全部木人仇恨。"),
        new("DR 传送：快捷传送面板", "快捷传送面板", 60453, "/pdrtp", "Daily Routines 快捷传送面板。"),
        new("DR 特殊场景：幻象群岛", "幻象群岛", 60415, "/pdrfe ocs", "Daily Routines 特殊场景进入指令：幻象群岛。"),
        new("DR 特殊场景：云冠群岛", "云冠群岛", 60415, "/pdrfe diadem", "Daily Routines 特殊场景进入指令：云冠群岛。"),
        new("DR 特殊场景：开拓无人岛", "开拓无人岛", 60415, "/pdrfe island", "Daily Routines 特殊场景进入指令：开拓无人岛。"),
        new("DR 特殊场景：博兹雅", "博兹雅", 60415, "/pdrfe bozja", "Daily Routines 特殊场景进入指令：博兹雅。"),
        new("DR 特殊场景：扎杜诺尔", "扎杜诺尔", 60415, "/pdrfe zadnor", "Daily Routines 特殊场景进入指令：扎杜诺尔。"),
        new("DR 特殊场景：常风之地", "常风之地", 60415, "/pdrfe anemos", "Daily Routines 特殊场景进入指令：常风之地。"),
        new("DR 特殊场景：恒冰之地", "恒冰之地", 60415, "/pdrfe pagos", "Daily Routines 特殊场景进入指令：恒冰之地。"),
        new("DR 特殊场景：涌火之地", "涌火之地", 60415, "/pdrfe pyros", "Daily Routines 特殊场景进入指令：涌火之地。"),
        new("DR 特殊场景：丰水之地", "丰水之地", 60415, "/pdrfe hydatos", "Daily Routines 特殊场景进入指令：丰水之地。"),
        new("DR 特殊场景：憧憬湾", "憧憬湾", 60415, "/pdrfe ardorum", "Daily Routines 特殊场景进入指令：憧憬湾。"),
        new("DR 特殊场景：法恩娜", "法恩娜", 60415, "/pdrfe phaenna", "Daily Routines 特殊场景进入指令：法恩娜。"),
        new("DR 特殊场景：俄匊斯", "俄匊斯", 60415, "/pdrfe oizys", "Daily Routines 特殊场景进入指令：俄匊斯。"),
        new("DR 特殊场景：奥克塞西亚", "奥克塞西亚", 60415, "/pdrfe auxesia", "Daily Routines 特殊场景进入指令：奥克塞西亚。"),
    ];

    private readonly record struct CurrencyDisplayOption(uint ItemId, string Name);

    private static readonly TaskBarComponentDefinition[] TaskBarComponentDefinitions = [
        new(Configuration.TaskBarComponentTime, "◷", "时间", "显示本地时间 / 艾欧泽亚时间"),
        new(Configuration.TaskBarComponentFps, "▦", "FPS", "显示当前帧率"),
        new(Configuration.TaskBarComponentMainMenu, "☰", "主菜单", "打开 AllHud 主菜单"),
        new(Configuration.TaskBarComponentVolume, "♪", "音量控制", "调整音量并打开音量面板"),
        new(Configuration.TaskBarComponentPluginList, "◇", "插件列表", "自己添加常用插件并快速打开"),
        new(Configuration.TaskBarComponentPluginShortcut, "◇", "插件快捷方式", "选择一个插件，点击图标直接打开"),
        new(Configuration.TaskBarComponentCustomShortcut, "✦", "自定义快捷方式", "自定义名称、图标和执行命令"),
        new(Configuration.TaskBarComponentQuickMenu, "▣", "快捷菜单", "把常用项目放进一个弹出菜单"),
        new(Configuration.TaskBarComponentServerInfo, "◎", "服务器信息栏", "显示服务器信息栏条目"),
        new(Configuration.TaskBarComponentInventory, "□", "背包", "显示背包已用 / 总格数"),
        new(Configuration.TaskBarComponentSaddlebag, "□", "陆行鸟鞍囊", "显示鞍囊已用 / 总格数"),
        new(Configuration.TaskBarComponentTeleport, "✈", "传送", "快速访问已共鸣的以太之光。"),
        new(Configuration.TaskBarComponentCoordinates, "⌖", "坐标", "显示你在游戏世界中的当前位置。"),
        new(Configuration.TaskBarComponentGearsetSwitcher, "⚒", "套装切换", "显示当前套装，并可快速切换。"),
        new(Configuration.TaskBarComponentCurrency, "¤", "货币", "显示当前的金币和军票。"),
    ];

    private static readonly Dictionary<string, TaskBarComponentDefinition> TaskBarComponentDefinitionLookup = TaskBarComponentDefinitions.ToDictionary(component => component.Id, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> pendingCustomShortcutCommandLineCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> customShortcutCommandLineUiIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PendingCustomShortcutCommandRemoval> pendingCustomShortcutCommandRemovals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> pendingQuickMenuItemRemovals = new(StringComparer.OrdinalIgnoreCase);

    private List<CurrencyDisplayOption>? scannedCurrencyOptions;

    private List<CurrencyDisplayOption> GetScannedCurrencyOptions() {
        if (scannedCurrencyOptions is not null) return scannedCurrencyOptions;

        var result = new List<CurrencyDisplayOption>();
        try {
            var sheet = this.dataManager.GetExcelSheet<LuminaItem>();
            if (sheet is not null) {
                foreach (var item in sheet) {
                    if (item.ItemUICategory.RowId != 59) continue;
                    var name = item.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    result.Add(new CurrencyDisplayOption(item.RowId, name));
                }
            }
        } catch {
        }

        if (!result.Any(c => c.ItemId == 1)) {
            result.Insert(0, new(1, "金币"));
        }

        scannedCurrencyOptions = result;
        return result;
    }

    private List<CurrencyDisplayOption> GetAllCurrencyOptionsForConfig() {
        var result = new List<CurrencyDisplayOption>(GetScannedCurrencyOptions());
        foreach (var custom in this.config.CustomCurrencies) {
            if (custom.Enabled && custom.ItemId != 0 && !result.Any(c => c.ItemId == custom.ItemId)) {
                result.Add(new(custom.ItemId, custom.Name));
            }
        }
        return result;
    }
    private readonly Configuration config;
    private readonly CombatStateTracker combatState;
    private readonly ITextureProvider textureProvider;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IDataManager dataManager;
    private readonly Action saveConfig;
    private readonly List<Dalamud.Plugin.IExposedPlugin> installedPluginSelectionCache = [];
    private readonly Dictionary<string, Dalamud.Plugin.IExposedPlugin> installedPluginSelectionByInternalName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> pluginListTileTextCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> pluginListSelectedInternalNameCache = new(StringComparer.OrdinalIgnoreCase);
    private long installedPluginSelectionCacheUpdatedAtMs = long.MinValue;
    private float pluginListTileTextCacheWidth = -1.0f;
    private float expandedSettingsWidth;
    private bool isPickingHiddenImGuiWindow;
    private string hiddenImGuiWindowPickerStatus = string.Empty;
    private string draggingTaskBarComponentId = string.Empty;
    private string selectedTaskBarComponentSettingsId = string.Empty;
    private string selectedTaskBarComponentSettingsScope = string.Empty;
    private string selectedAuxiliaryComponentSettingsId = string.Empty;
    private string selectedAuxiliaryComponentSettingsScope = string.Empty;
    private int selectedAuxiliaryComponentSettingsBarIndex = -1;
    private ConfigPage selectedPage = ConfigPage.状态栏;
    private TaskBarPage selectedTaskBarPage = TaskBarPage.任务栏;
    private int selectedAuxiliaryBarIndex;

    public ConfigWindow(Configuration config, CombatStateTracker combatState, ITextureProvider textureProvider, IDalamudPluginInterface pluginInterface, IDataManager dataManager, Action saveConfig) {
        this.config = config;
        this.combatState = combatState;
        this.textureProvider = textureProvider;
        this.pluginInterface = pluginInterface;
        this.dataManager = dataManager;
        this.saveConfig = saveConfig;
    }

    public bool IsOpen { get; set; }

    private static void DrawStyledTooltip(string text) {
        DrawStyledTooltip(() => ImGui.TextUnformatted(text));
    }

    private static void DrawStyledTooltip(Action drawContent) {
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(0.16f, 0.10f, 0.14f, 0.94f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.92f, 0.96f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1.0f, 0.58f, 0.76f, 0.58f));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(1.0f, 0.58f, 0.76f, 0.34f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(11.0f, 8.0f));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 7.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6.0f, 4.0f));
        ImGui.BeginTooltip();
        drawContent();
        ImGui.EndTooltip();
        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor(4);
    }

    public void Draw() {
        if (!IsOpen) {
            return;
        }

        // ── 定制主题 ──
        PushCustomStyle();

        var useImported = this.config.ActiveThemePreset == ThemePreset.Imported
                          && this.config.ImportedStyleColors is { Count: > 0 };
        ImGui.SetNextWindowSize(new Vector2(760.0f, 620.0f), ImGuiCond.FirstUseEver);
        var isOpen = this.IsOpen;
        var windowFlags = useImported ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse;
        if (!ImGui.Begin("AllHud 设置", ref isOpen, windowFlags)) {
            this.IsOpen = isOpen;
            PopCustomStyle();
            ImGui.End();
            return;
        }

        this.IsOpen = isOpen;

        if (!useImported) {
            // ── 定制标题栏 ──
            DrawCustomTitleBar();
        }

        // ── 左侧导航 + 内容区 ──
        var navWidth = 156.0f;

        // 自适应导航/内容区颜色
        Vector4 navBg, navBorder, contentBg, contentBorder;
        if (useImported) {
            // 导入样式：深色兜底，调色板有则覆盖
            contentBg = new Vector4(0.11f, 0.11f, 0.12f, 1.0f);
            contentBorder = new Vector4(0.30f, 0.30f, 0.36f, 0.50f);
            var imported = this.config.ImportedStyleColors!;
            if (imported.TryGetValue("ChildBg", out var cb)) contentBg = cb;
            if (imported.TryGetValue("Border", out var cbo)) contentBorder = WithAlpha(cbo, 0.5f);
            navBg = WithAlpha(contentBg, 0.85f);
            navBorder = WithAlpha(contentBorder, 0.35f);
        } else if (this.config.ActiveThemePreset == ThemePreset.Custom) {
            // 自定义主题：用户配置优先，否则由强调色派生
            var accent = this.config.CustomThemeAccentColor;
            var bg = this.config.CustomThemeBackgroundColor;
            if (this.config.CustomThemeNavBgColor.HasValue) navBg = this.config.CustomThemeNavBgColor.Value;
            else navBg = WithAlpha(bg, 0.82f);
            if (this.config.CustomThemeNavBorderColor.HasValue) navBorder = this.config.CustomThemeNavBorderColor.Value;
            else navBorder = WithAlpha(accent, 0.45f);
            contentBg = WithAlpha(bg, 0.96f);
            contentBorder = WithAlpha(accent, 0.35f);
        } else {
            // 默认粉白
            navBg = new Vector4(0.982f, 0.900f, 0.918f, 1.0f);
            navBorder = new Vector4(0.925f, 0.660f, 0.725f, 0.50f);
            contentBg = new Vector4(1.0f, 0.972f, 0.978f, 1.0f);
            contentBorder = new Vector4(0.940f, 0.720f, 0.780f, 0.42f);
        }

        ImGui.PushStyleColor(ImGuiCol.ChildBg, navBg);
        ImGui.PushStyleColor(ImGuiCol.Border, navBorder);
        ImGui.BeginChild("config_nav", new Vector2(navWidth, -1.0f), true);
        DrawNavSection();
        ImGui.EndChild();
        ImGui.PopStyleColor(2);

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, contentBg);
        ImGui.PushStyleColor(ImGuiCol.Border, contentBorder);
        ImGui.BeginChild("config_content", new Vector2(0.0f, -1.0f), true);
        ImGui.Dummy(new Vector2(1.0f, 4.0f));
        DrawSelectedPage();
        ImGui.EndChild();
        ImGui.PopStyleColor(2);

        ImGui.End();
        PopCustomStyle();

        DrawHiddenImGuiWindowPickerOverlay();
    }

    private void DrawHiddenImGuiWindowPickerOverlay() {
        if (!this.isPickingHiddenImGuiWindow) {
            return;
        }

        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.Pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(viewport.Size, ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.0f);
        var flags = ImGuiWindowFlags.NoTitleBar
                    | ImGuiWindowFlags.NoDecoration
                    | ImGuiWindowFlags.NoMove
                    | ImGuiWindowFlags.NoResize
                    | ImGuiWindowFlags.NoSavedSettings
                    | ImGuiWindowFlags.NoScrollbar;

        if (!ImGui.Begin("AllHud 隐藏窗口拾取遮罩", flags)) {
            ImGui.End();
            return;
        }

        var hoveredWindow = TryGetHoveredImGuiWindow();
        var drawList = ImGui.GetWindowDrawList();
        var mousePos = ImGui.GetMousePos();
        var tip = hoveredWindow is { } window
            ? $"左键隐藏: {window.Name}"
            : "移动到要隐藏的悬浮窗上，左键确认，右键或 Esc 取消";
        var tipSize = ImGui.CalcTextSize(tip) + new Vector2(16.0f, 10.0f);
        var tipMin = mousePos + new Vector2(16.0f, 18.0f);
        var tipMax = tipMin + tipSize;
        drawList.AddRectFilled(tipMin, tipMax, ImGui.GetColorU32(new Vector4(0.18f, 0.10f, 0.15f, 0.92f)), 6.0f);
        drawList.AddRect(tipMin, tipMax, ImGui.GetColorU32(new Vector4(1.0f, 0.58f, 0.76f, 0.72f)), 6.0f);
        drawList.AddText(tipMin + new Vector2(8.0f, 5.0f), ImGui.GetColorU32(new Vector4(1.0f, 0.92f, 0.96f, 1.0f)), tip);

        if (ImGui.IsKeyPressed(ImGuiKey.Escape) || ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
            this.isPickingHiddenImGuiWindow = false;
            this.hiddenImGuiWindowPickerStatus = "已取消添加。";
        } else if (hoveredWindow is not null && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
            AddHiddenImGuiWindow(hoveredWindow.Value.Name);
        }

        ImGui.End();
    }

    private void PushCustomStyle() {
        this.pushedColorCount = 0;
        this.pushedVarCount = 0;

        var useImported = this.config.ActiveThemePreset == ThemePreset.Imported
                          && this.config.ImportedStyleColors is { Count: > 0 };

        // 根据主题方案选择基色：导入样式用深色兜底，避免浅色文字在深色背景下不可见
        Vector4 windowBg, childBg, text, border, separator;
        Vector4 frameBg, frameBgHovered, frameBgActive, popupBg;
        Vector4 button, buttonHovered, buttonActive;
        Vector4 header, headerHovered, headerActive;
        Vector4 tab, tabHovered, tabActive;
        Vector4 scrollbarBg, scrollbarGrab, scrollbarGrabHovered, scrollbarGrabActive;
        Vector4 sliderGrab, sliderGrabActive;

        if (useImported) {
            // 深色基色 — 大多数 DS1 样式基于深色主题，未覆盖的部分也能保证可读性
            windowBg = new Vector4(0.08f, 0.08f, 0.09f, 1.0f);
            childBg = new Vector4(0.11f, 0.11f, 0.12f, 1.0f);
            text = new Vector4(0.92f, 0.92f, 0.92f, 1.0f);
            border = new Vector4(0.30f, 0.30f, 0.36f, 0.50f);
            separator = new Vector4(0.28f, 0.28f, 0.34f, 0.60f);
            frameBg = new Vector4(0.16f, 0.16f, 0.19f, 1.0f);
            frameBgHovered = new Vector4(0.22f, 0.22f, 0.26f, 1.0f);
            frameBgActive = new Vector4(0.28f, 0.28f, 0.32f, 1.0f);
            popupBg = new Vector4(0.12f, 0.12f, 0.14f, 1.0f);
            button = new Vector4(0.20f, 0.20f, 0.24f, 1.0f);
            buttonHovered = new Vector4(0.28f, 0.28f, 0.33f, 1.0f);
            buttonActive = new Vector4(0.36f, 0.36f, 0.42f, 1.0f);
            header = new Vector4(0.18f, 0.18f, 0.22f, 1.0f);
            headerHovered = new Vector4(0.26f, 0.26f, 0.31f, 1.0f);
            headerActive = new Vector4(0.34f, 0.34f, 0.40f, 1.0f);
            tab = new Vector4(0.16f, 0.16f, 0.19f, 1.0f);
            tabHovered = new Vector4(0.26f, 0.26f, 0.31f, 1.0f);
            tabActive = new Vector4(0.30f, 0.30f, 0.36f, 1.0f);
            scrollbarBg = new Vector4(0.06f, 0.06f, 0.07f, 0.50f);
            scrollbarGrab = new Vector4(0.31f, 0.31f, 0.36f, 1.0f);
            scrollbarGrabHovered = new Vector4(0.41f, 0.41f, 0.47f, 1.0f);
            scrollbarGrabActive = new Vector4(0.51f, 0.51f, 0.58f, 1.0f);
            sliderGrab = new Vector4(0.41f, 0.41f, 0.51f, 1.0f);
            sliderGrabActive = new Vector4(0.51f, 0.51f, 0.61f, 1.0f);
        } else {
            // 粉白基色
            windowBg = new Vector4(0.992f, 0.940f, 0.948f, 1.0f);
            childBg = new Vector4(1.0f, 0.970f, 0.976f, 1.0f);
            text = new Vector4(0.42f, 0.28f, 0.35f, 1.0f);
            border = new Vector4(0.910f, 0.700f, 0.750f, 0.65f);
            separator = new Vector4(0.925f, 0.670f, 0.730f, 0.70f);
            frameBg = new Vector4(1.0f, 0.985f, 0.980f, 1.0f);
            frameBgHovered = new Vector4(0.985f, 0.905f, 0.920f, 1.0f);
            frameBgActive = new Vector4(0.955f, 0.795f, 0.835f, 1.0f);
            popupBg = new Vector4(1.0f, 0.960f, 0.965f, 1.0f);
            button = new Vector4(1.0f, 0.985f, 0.980f, 1.0f);
            buttonHovered = new Vector4(0.975f, 0.850f, 0.875f, 1.0f);
            buttonActive = new Vector4(0.925f, 0.680f, 0.735f, 1.0f);
            header = new Vector4(0.965f, 0.840f, 0.870f, 1.0f);
            headerHovered = new Vector4(0.940f, 0.760f, 0.810f, 1.0f);
            headerActive = new Vector4(0.900f, 0.660f, 0.720f, 1.0f);
            tab = new Vector4(0.990f, 0.950f, 0.955f, 1.0f);
            tabHovered = new Vector4(0.950f, 0.820f, 0.850f, 1.0f);
            tabActive = new Vector4(0.975f, 0.890f, 0.905f, 1.0f);
            scrollbarBg = new Vector4(0.940f, 0.850f, 0.860f, 0.35f);
            scrollbarGrab = new Vector4(0.900f, 0.560f, 0.640f, 0.68f);
            scrollbarGrabHovered = new Vector4(0.870f, 0.460f, 0.560f, 0.82f);
            scrollbarGrabActive = new Vector4(0.820f, 0.380f, 0.500f, 1.0f);
            sliderGrab = new Vector4(0.74f, 0.34f, 0.52f, 1.0f);
            sliderGrabActive = new Vector4(0.66f, 0.26f, 0.46f, 1.0f);
        }

        if (this.config.ActiveThemePreset == ThemePreset.Custom) {
            var accent = this.config.CustomThemeAccentColor;
            windowBg = this.config.CustomThemeBackgroundColor;
            childBg = this.config.CustomThemeBackgroundColor;
            text = this.config.CustomThemeTextColor;
            border = WithAlpha(accent, 0.55f);
            separator = WithAlpha(accent, 0.55f);
            buttonActive = WithAlpha(accent, 0.92f);
            headerActive = WithAlpha(accent, 0.90f);
            tabActive = WithAlpha(accent, 0.85f);
            scrollbarGrab = WithAlpha(accent, 0.68f);
            scrollbarGrabHovered = WithAlpha(accent, 0.82f);
            scrollbarGrabActive = WithAlpha(accent, 1.0f);
            sliderGrab = accent;
            sliderGrabActive = accent;
        }

        void PushColor(ImGuiCol col, Vector4 color) {
            ImGui.PushStyleColor(col, color);
            this.pushedColorCount++;
        }

        PushColor(ImGuiCol.WindowBg, windowBg);
        PushColor(ImGuiCol.ChildBg, childBg);
        PushColor(ImGuiCol.FrameBg, frameBg);
        PushColor(ImGuiCol.FrameBgHovered, frameBgHovered);
        PushColor(ImGuiCol.FrameBgActive, frameBgActive);
        PushColor(ImGuiCol.PopupBg, popupBg);
        PushColor(ImGuiCol.Button, button);
        PushColor(ImGuiCol.ButtonHovered, buttonHovered);
        PushColor(ImGuiCol.ButtonActive, buttonActive);
        PushColor(ImGuiCol.Header, header);
        PushColor(ImGuiCol.HeaderHovered, headerHovered);
        PushColor(ImGuiCol.HeaderActive, headerActive);
        PushColor(ImGuiCol.Tab, tab);
        PushColor(ImGuiCol.TabHovered, tabHovered);
        PushColor(ImGuiCol.TabActive, tabActive);
        PushColor(ImGuiCol.Separator, separator);
        PushColor(ImGuiCol.Border, border);
        PushColor(ImGuiCol.Text, text);
        PushColor(ImGuiCol.ScrollbarBg, scrollbarBg);
        PushColor(ImGuiCol.ScrollbarGrab, scrollbarGrab);
        PushColor(ImGuiCol.ScrollbarGrabHovered, scrollbarGrabHovered);
        PushColor(ImGuiCol.ScrollbarGrabActive, scrollbarGrabActive);
        PushColor(ImGuiCol.SliderGrab, sliderGrab);
        PushColor(ImGuiCol.SliderGrabActive, sliderGrabActive);
        PushColor(ImGuiCol.ResizeGrip, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
        PushColor(ImGuiCol.ResizeGripHovered, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
        PushColor(ImGuiCol.ResizeGripActive, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));

        // 样式变量：导入样式用调色板变量，否则用默认
        if (useImported) {
            this.PushImportedStyleVars();
        } else {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 6.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 10.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 4.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 4.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.GrabMinSize, 10.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1.0f);
            this.pushedVarCount = 10;
        }

        // 导入样式：调色板覆盖基色，确保完全接管
        if (useImported) {
            foreach (var (name, color) in this.config.ImportedStyleColors!) {
                if (Enum.TryParse<ImGuiCol>(name, out var col)) {
                    ImGui.PushStyleColor(col, color);
                    this.pushedColorCount++;
                }
            }
        }
    }

    private void PushImportedStyleVars() {
        var sv = this.config.ImportedStyleVars;
        void TryPushVar(ImGuiStyleVar v, string key, float fallback) {
            var val = sv != null && sv.TryGetValue(key, out var f) ? f : fallback;
            ImGui.PushStyleVar(v, val);
            this.pushedVarCount++;
        }
        TryPushVar(ImGuiStyleVar.FrameRounding, "FrameRounding", 4.0f);
        TryPushVar(ImGuiStyleVar.TabRounding, "TabRounding", 6.0f);
        TryPushVar(ImGuiStyleVar.WindowRounding, "WindowRounding", 8.0f);
        TryPushVar(ImGuiStyleVar.ScrollbarSize, "ScrollbarSize", 10.0f);
        TryPushVar(ImGuiStyleVar.ScrollbarRounding, "ScrollbarRounding", 4.0f);
        TryPushVar(ImGuiStyleVar.GrabRounding, "GrabRounding", 4.0f);
        TryPushVar(ImGuiStyleVar.GrabMinSize, "GrabMinSize", 10.0f);
        TryPushVar(ImGuiStyleVar.FrameBorderSize, "FrameBorderSize", 1.0f);
        TryPushVar(ImGuiStyleVar.ChildRounding, "ChildRounding", 8.0f);
        TryPushVar(ImGuiStyleVar.ChildBorderSize, "ChildBorderSize", 1.0f);
    }

    private void PopCustomStyle() {
        if (this.pushedVarCount > 0) ImGui.PopStyleVar(this.pushedVarCount);
        if (this.pushedColorCount > 0) ImGui.PopStyleColor(this.pushedColorCount);
    }

    private void DrawCustomTitleBar() {
        var drawList = ImGui.GetWindowDrawList();
        var availWidth = ImGui.GetContentRegionAvail().X;
        var titleBarHeight = ImGui.GetTextLineHeight() + 18.0f;

        var titleBarMin = ImGui.GetCursorScreenPos();
        var titleBarMax = titleBarMin + new Vector2(availWidth, titleBarHeight);
        var textY = titleBarMin.Y + 9.0f;

        // 标题栏背景/边框取自 style，便于自定义主题接管
        var styleWindowBg = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
        var styleBorder = ImGui.GetStyle().Colors[(int)ImGuiCol.Border];
        var styleButton = ImGui.GetStyle().Colors[(int)ImGuiCol.Button];
        var styleButtonHovered = ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered];
        var styleText = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];

        drawList.AddRectFilled(
            titleBarMin,
            titleBarMax,
            ImGui.GetColorU32(WithAlpha(styleWindowBg, 0.96f)),
            8.0f);
        drawList.AddRect(
            titleBarMin,
            titleBarMax,
            ImGui.GetColorU32(WithAlpha(styleBorder, 0.42f)),
            8.0f,
            (ImDrawFlags)0,
            1.0f);
        drawList.AddLine(
            new Vector2(titleBarMin.X + 10.0f, titleBarMax.Y - 1.0f),
            new Vector2(titleBarMax.X - 10.0f, titleBarMax.Y - 1.0f),
            ImGui.GetColorU32(WithAlpha(styleBorder, 0.26f)),
            1.0f);

        // 居中标题：插件名使用静态 RGB 字色，标题栏更有识别度但不做动画。
        ReadOnlySpan<string> titleLetters = ["A", "l", "l", "H", "u", "d"];
        ReadOnlySpan<Vector4> titleLetterColors = [
            new(0.96f, 0.24f, 0.30f, 0.98f),
            new(0.98f, 0.55f, 0.18f, 0.98f),
            new(0.94f, 0.78f, 0.20f, 0.98f),
            new(0.24f, 0.66f, 0.36f, 0.98f),
            new(0.20f, 0.62f, 0.92f, 0.98f),
            new(0.58f, 0.42f, 0.90f, 0.98f),
        ];

        var titleMainWidth = 0.0f;
        foreach (var letter in titleLetters) {
            titleMainWidth += ImGui.CalcTextSize(letter).X;
        }

        var titleSub = " 设置";
        var titleSubSize = ImGui.CalcTextSize(titleSub);
        var titleWidth = titleMainWidth + titleSubSize.X;
        var titleCenterX = (titleBarMin.X + titleBarMax.X) * 0.5f;
        var titleTextPos = new Vector2(titleCenterX - titleWidth * 0.5f, textY);
        var titlePillMin = titleTextPos - new Vector2(12.0f, 5.0f);
        var titlePillMax = titleTextPos + new Vector2(titleWidth + 12.0f, ImGui.GetTextLineHeight() + 5.0f);
        drawList.AddRectFilled(titlePillMin, titlePillMax, ImGui.GetColorU32(WithAlpha(styleButton, 0.56f)), 999.0f);
        drawList.AddRectFilled(titlePillMin + new Vector2(8.0f, titlePillMax.Y - titlePillMin.Y - 3.0f), titlePillMax - new Vector2(8.0f, 1.5f), ImGui.GetColorU32(WithAlpha(styleBorder, 0.18f)), 999.0f);
        drawList.AddRect(titlePillMin, titlePillMax, ImGui.GetColorU32(WithAlpha(styleBorder, 0.34f)), 999.0f, (ImDrawFlags)0, 1.0f);

        var letterX = titleTextPos.X;
        for (var index = 0; index < titleLetters.Length; index++) {
            var letter = titleLetters[index];
            var letterPos = new Vector2(letterX, titleTextPos.Y);
            drawList.AddText(letterPos + new Vector2(0.8f, 0.8f), ImGui.GetColorU32(WithAlpha(styleText, 0.34f)), letter);
            drawList.AddText(letterPos, ImGui.GetColorU32(titleLetterColors[index]), letter);
            letterX += ImGui.CalcTextSize(letter).X;
        }

        drawList.AddText(new Vector2(letterX, titleTextPos.Y), ImGui.GetColorU32(WithAlpha(styleText, 0.92f)), titleSub);

        const float closeBtnSize = 26.0f;

        // ── 关闭按钮 ──
        var closeMin = new Vector2(titleBarMax.X - closeBtnSize - 8.0f, titleBarMin.Y + (titleBarHeight - closeBtnSize) * 0.5f);
        var closeMax = closeMin + new Vector2(closeBtnSize, closeBtnSize);
        var closeCenter = (closeMin + closeMax) * 0.5f;

        var closeHovered = ImGui.IsMouseHoveringRect(closeMin, closeMax);

        // 小型圆角按钮 + 细线 X，避免厚重红叉。
        var closeBgColor = closeHovered
            ? ImGui.GetColorU32(WithAlpha(styleButtonHovered, 0.88f))
            : ImGui.GetColorU32(WithAlpha(styleButton, 0.86f));
        drawList.AddRectFilled(closeMin, closeMax, closeBgColor, 6.0f);
        drawList.AddRect(
            closeMin,
            closeMax,
            ImGui.GetColorU32(closeHovered
                ? WithAlpha(styleBorder, 0.85f)
                : WithAlpha(styleBorder, 0.55f)),
            6.0f,
            (ImDrawFlags)0,
            1.0f);

        var xColor = ImGui.GetColorU32(closeHovered
            ? WithAlpha(styleText, 1.0f)
            : WithAlpha(styleText, 0.82f));
        const float xHalf = 5.0f;
        drawList.AddLine(closeCenter - new Vector2(xHalf, xHalf), closeCenter + new Vector2(xHalf, xHalf), xColor, 1.55f);
        drawList.AddLine(closeCenter + new Vector2(xHalf, -xHalf), closeCenter - new Vector2(xHalf, -xHalf), xColor, 1.55f);

        // 关闭点击
        if (closeHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
            this.IsOpen = false;
        }

        ImGui.SetCursorScreenPos(new Vector2(titleBarMin.X, titleBarMax.Y));
        ImGui.Dummy(new Vector2(1.0f, 6.0f));
    }

    private void DrawNavSection() {
        var areaWidth = ImGui.GetContentRegionAvail().X;

        ImGui.Dummy(new Vector2(areaWidth, 8.0f));

        ImGui.Indent(4.0f);

        DrawNavButton(ConfigPage.状态栏, "状态栏");
        DrawNavButton(ConfigPage.目标情报, "目标情报");
        DrawNavButton(ConfigPage.队伍信息, "队伍信息");
        DrawNavButton(ConfigPage.任务栏, "任务栏");
        DrawNavButton(ConfigPage.技能, "独立监控");
        DrawNavButton(ConfigPage.外观, "外观主题");
        DrawNavButton(ConfigPage.调试, "调试");

        ImGui.Unindent(4.0f);
    }

    private void DrawNavButton(ConfigPage page, string label) {
        var selected = this.selectedPage == page;
        var accentColor = GetPageAccentColor(page);
        var areaWidth = ImGui.GetContentRegionAvail().X - 4.0f;
        const float height = 38.0f;

        var cursor = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var itemMin = cursor;
        var itemMax = cursor + new Vector2(areaWidth, height);

        // 大卡片 — 选中背景配圆角边框（颜色取自 style，便于导入样式接管）
        if (selected) {
            var selBg = WithAlpha(ImGui.GetStyle().Colors[(int)ImGuiCol.Header], 0.92f);
            drawList.AddRectFilled(itemMin, itemMax, ImGui.GetColorU32(selBg), 7.0f);
            drawList.AddRect(itemMin, itemMax, ImGui.GetColorU32(WithAlpha(accentColor, 0.42f)), 7.0f);

            // 左侧指示条
            drawList.AddRectFilled(
                itemMin + new Vector2(0.0f, 8.0f),
                itemMin + new Vector2(3.0f, height - 8.0f),
                ImGui.GetColorU32(WithAlpha(accentColor, 0.95f)),
                1.5f);
        }

        // 小卡片 — 悬浮
        if (ImGui.IsMouseHoveringRect(itemMin, itemMax) && !selected) {
            var hoverBg = WithAlpha(ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderHovered], 0.55f);
            drawList.AddRectFilled(itemMin, itemMax, ImGui.GetColorU32(hoverBg), 6.0f);
        }

        // 文字
        var textColor = selected
            ? WithAlpha(accentColor, 1.0f)
            : WithAlpha(ImGui.GetStyle().Colors[(int)ImGuiCol.Text], 0.78f);
        drawList.AddText(itemMin + new Vector2(14.0f, (height - ImGui.GetTextLineHeight()) * 0.5f), ImGui.GetColorU32(textColor), label);

        // 点击区域
        ImGui.SetCursorPos(ImGui.GetCursorPos());
        ImGui.InvisibleButton($"nav_{page}", new Vector2(areaWidth, height));
        if (ImGui.IsItemClicked()) {
            this.selectedPage = page;
        }

        ImGui.Dummy(new Vector2(areaWidth, 0.0f));
    }

    private Vector4 GetPageAccentColor(ConfigPage page) {
        // 导入样式：从调色板取主色调
        if (this.config.ActiveThemePreset == ThemePreset.Imported
            && this.config.ImportedStyleColors is { Count: > 0 }) {
            var imported = this.config.ImportedStyleColors;
            // 依次尝试 Header / TitleBgActive / Button / Accent
            if (imported.TryGetValue("Header", out var c)) return c;
            if (imported.TryGetValue("TitleBgActive", out c)) return c;
            if (imported.TryGetValue("Button", out c)) return c;
            if (imported.TryGetValue("Accent", out c)) return c;
            // 兜底取任意第一个颜色
            return imported.Values.FirstOrDefault();
        }

        // 自定义主题：使用用户强调色
        if (this.config.ActiveThemePreset == ThemePreset.Custom) {
            return this.config.CustomThemeAccentColor;
        }

        // 默认主题：硬编码
        return page switch {
            ConfigPage.目标情报 => new Vector4(0.78f, 0.34f, 0.18f, 1.0f),
            ConfigPage.状态栏 => new Vector4(0.24f, 0.54f, 0.32f, 1.0f),
            ConfigPage.队伍信息 => new Vector4(0.68f, 0.48f, 0.14f, 1.0f),
            ConfigPage.任务栏 => new Vector4(0.20f, 0.58f, 0.72f, 1.0f),
            ConfigPage.技能 => new Vector4(0.42f, 0.36f, 0.72f, 1.0f),
            ConfigPage.外观 => new Vector4(0.55f, 0.40f, 0.75f, 1.0f),
            ConfigPage.调试 => new Vector4(0.58f, 0.36f, 0.66f, 1.0f),
            _ => new Vector4(0.42f, 0.17f, 0.28f, 1.0f),
        };
    }

    private Vector4 GetSectionTitleColor(string title) {
        // 导入样式：统一用调色板 Header 色
        if (this.config.ActiveThemePreset == ThemePreset.Imported
            && this.config.ImportedStyleColors is { Count: > 0 }) {
            return this.GetPageAccentColor(ConfigPage.目标情报);
        }

        if (title.Contains("目标", StringComparison.Ordinal)) {
            return new Vector4(0.74f, 0.31f, 0.16f, 1.0f);
        }

        if (title.Contains("状态", StringComparison.Ordinal)) {
            return new Vector4(0.22f, 0.50f, 0.30f, 1.0f);
        }

        if (title.Contains("队伍", StringComparison.Ordinal)) {
            return new Vector4(0.64f, 0.45f, 0.12f, 1.0f);
        }

        if (title.Contains("任务", StringComparison.Ordinal)) {
            return new Vector4(0.20f, 0.58f, 0.72f, 1.0f);
        }

        if (title.Contains("技能", StringComparison.Ordinal) || title.Contains("独立监控", StringComparison.Ordinal)) {
            return new Vector4(0.42f, 0.36f, 0.72f, 1.0f);
        }

        return new Vector4(0.40f, 0.17f, 0.28f, 1.0f);
    }

    private Vector4 GetSubsectionTitleColor(string title) {
        // 导入样式：统一用调色板 Header 色
        if (this.config.ActiveThemePreset == ThemePreset.Imported
            && this.config.ImportedStyleColors is { Count: > 0 }) {
            return this.GetPageAccentColor(ConfigPage.目标情报);
        }

        if (title.Contains("技能", StringComparison.Ordinal)
            || title.Contains("独立监控", StringComparison.Ordinal)
            || title.Contains("一键添加", StringComparison.Ordinal)
            || title.Contains("团辅", StringComparison.Ordinal)
            || title.Contains("减伤", StringComparison.Ordinal)) {
            return new Vector4(0.42f, 0.36f, 0.72f, 1.0f);
        }

        if (title.Contains("血", StringComparison.Ordinal) || title.Contains("咏唱", StringComparison.Ordinal)) {
            return new Vector4(0.75f, 0.36f, 0.16f, 1.0f);
        }

        if (title.Contains("状态", StringComparison.Ordinal)) {
            return new Vector4(0.24f, 0.52f, 0.31f, 1.0f);
        }

        return new Vector4(0.62f, 0.42f, 0.14f, 1.0f);
    }

    private static Vector4 WithAlpha(Vector4 color, float alpha) {
        return new Vector4(color.X, color.Y, color.Z, alpha);
    }

    private void DrawSelectedPage() {
        switch (this.selectedPage) {
            case ConfigPage.目标情报:
                DrawTargetInfoPage();
                break;
            case ConfigPage.状态栏:
                DrawStatusBarPage();
                break;
            case ConfigPage.队伍信息:
                DrawPartyInfoPage();
                break;
            case ConfigPage.任务栏:
                DrawTaskBarPage();
                break;
            case ConfigPage.技能:
                DrawSkillsPage();
                break;
            case ConfigPage.外观:
                DrawAppearancePage();
                break;
            case ConfigPage.调试:
                DrawDebugPage();
                break;
        }
    }
    private void DrawLimitBreakPositionSelector() {
        var position = Math.Clamp(this.config.PartyLimitBreakBarPosition, 0, 1);
        DrawSegmentedSelector("LB 位置", "limit_break_position", position, value => this.config.PartyLimitBreakBarPosition = value, ("队伍顶部", 0), ("队伍底部", 1));
    }

    private static byte[] CreateUtf8Buffer(string value, int length) {
        var buffer = new byte[length];
        var bytes = Encoding.UTF8.GetBytes(value);
        Array.Copy(bytes, buffer, Math.Min(bytes.Length, buffer.Length - 1));
        return buffer;
    }

    private static string ReadUtf8Buffer(byte[] buffer) {
        var length = Array.IndexOf(buffer, (byte)0);
        if (length < 0) {
            length = buffer.Length;
        }

        return Encoding.UTF8.GetString(buffer, 0, length).Trim();
    }

    private void DrawCheckbox(string label, string id, bool value, Action<bool> setter, float? rowHeightOverride = null) {
        const float gap = 3.0f;
        const float textVisualOffsetY = 1.0f;
        var frameHeight = ImGui.GetFrameHeight();
        var boxSize = MathF.Round(ImGui.GetFontSize());
        var textSize = ImGui.CalcTextSize(label);
        var rowHeight = rowHeightOverride ?? MathF.Max(frameHeight, textSize.Y);
        var rowSize = new Vector2(boxSize + gap + textSize.X, rowHeight);
        var rowMin = ImGui.GetCursorScreenPos();
        var boxMin = rowMin + new Vector2(0.0f, (rowHeight - boxSize) * 0.5f);
        var boxMax = boxMin + new Vector2(boxSize);
        var textPos = rowMin + new Vector2(boxSize + gap, (rowHeight - textSize.Y) * 0.5f + textVisualOffsetY);
        var drawList = ImGui.GetWindowDrawList();

        ImGui.InvisibleButton($"{label}##{id}", rowSize);
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        if (ImGui.IsItemClicked()) {
            setter(!value);
            this.saveConfig();
        }

        var styleBorder = ImGui.GetStyle().Colors[(int)ImGuiCol.Border];
        var styleFrameBg = ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg];
        var styleSliderGrab = ImGui.GetStyle().Colors[(int)ImGuiCol.SliderGrab];
        var styleText = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        var borderColor = hovered
            ? WithAlpha(styleSliderGrab, 0.92f)
            : WithAlpha(styleBorder, 0.82f);
        var fillColor = value
            ? WithAlpha(styleSliderGrab, active ? 0.92f : 0.82f)
            : hovered
                ? WithAlpha(styleFrameBg, 0.92f)
                : WithAlpha(styleFrameBg, 0.72f);

        drawList.AddRectFilled(boxMin, boxMax, ImGui.GetColorU32(fillColor), 4.0f);
        drawList.AddRect(boxMin, boxMax, ImGui.GetColorU32(borderColor), 4.0f, (ImDrawFlags)0, 1.2f);
        if (value) {
            var checkColor = ImGui.GetColorU32(WithAlpha(styleFrameBg, 1.0f));
            drawList.AddLine(boxMin + new Vector2(4.0f, 8.5f), boxMin + new Vector2(7.0f, 11.5f), checkColor, 1.8f);
            drawList.AddLine(boxMin + new Vector2(7.0f, 11.5f), boxMin + new Vector2(12.5f, 5.0f), checkColor, 1.8f);
        }

        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), textPos, ImGui.GetColorU32(styleText), label);
    }

    private void DrawSegmentedSelector(string label, string id, int currentValue, Action<int> setter, params (string Label, int Value)[] options) {
        var accentColor = GetSelectorAccentColor(label);
        const float rowIndent = 28.0f;
        const float labelButtonGap = 12.0f;
        var labelTextWidth = ImGui.CalcTextSize(label).X;
        var labelWidth = labelTextWidth + labelButtonGap;
        var buttonWidth = Math.Clamp(options.Max(option => ImGui.CalcTextSize(option.Label).X + 24.0f), 58.0f, 92.0f);
        var buttonSpacing = 5.0f;
        var buttonsWidth = buttonWidth * options.Length + buttonSpacing * Math.Max(0, options.Length - 1);
        var availableWidth = Math.Max(80.0f, ImGui.GetContentRegionAvail().X - rowIndent);
        var rowStartX = ImGui.GetCursorPosX() + rowIndent;
        var keepOnOneLine = labelWidth + buttonsWidth <= availableWidth;

        ImGui.SetCursorPosX(rowStartX);
        ImGui.AlignTextToFramePadding();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();

        if (keepOnOneLine) {
            ImGui.SameLine(rowStartX + labelWidth);
        } else {
            ImGui.SetCursorPosX(rowStartX);
        }

        var styleText = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        var styleButton = ImGui.GetStyle().Colors[(int)ImGuiCol.Button];
        var styleFrameBg = ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg];
        for (var index = 0; index < options.Length; index++) {
            var option = options[index];
            var selected = currentValue == option.Value;
            var buttonTextColor = selected
                ? WithAlpha(styleFrameBg, 1.0f)
                : WithAlpha(styleText, 0.86f);
            var buttonColor = selected
                ? WithAlpha(accentColor, 0.72f)
                : WithAlpha(styleButton, 0.88f);
            var buttonHoveredColor = selected
                ? WithAlpha(accentColor, 0.88f)
                : WithAlpha(accentColor, 0.24f);
            var buttonActiveColor = selected
                ? WithAlpha(accentColor, 0.95f)
                : WithAlpha(accentColor, 0.34f);

            var buttonSize = new Vector2(buttonWidth, 24.0f);
            var buttonMin = ImGui.GetCursorScreenPos();
            var buttonMax = buttonMin + buttonSize;
            var drawList = ImGui.GetWindowDrawList();

            ImGui.InvisibleButton($"{option.Label}##{id}_{option.Value}", buttonSize);
            var hovered = ImGui.IsItemHovered();
            var active = ImGui.IsItemActive();
            if (ImGui.IsItemClicked() && !selected) {
                setter(option.Value);
                this.saveConfig();
            }

            var fillColor = active ? buttonActiveColor : hovered ? buttonHoveredColor : buttonColor;
            var borderColor = WithAlpha(accentColor, selected ? 0.86f : hovered ? 0.56f : 0.34f);
            const float rounding = 5.0f;

            // 填充向内缩 1px，边框最后绘制，避免彩色背景看起来溢出边框。
            drawList.AddRectFilled(
                buttonMin + new Vector2(1.0f, 1.0f),
                buttonMax - new Vector2(1.0f, 1.0f),
                ImGui.GetColorU32(fillColor),
                rounding - 1.0f);
            drawList.AddRect(
                buttonMin,
                buttonMax,
                ImGui.GetColorU32(borderColor),
                rounding,
                (ImDrawFlags)0,
                selected ? 1.4f : 1.0f);

            if (selected) {
                drawList.AddRect(
                    buttonMin + new Vector2(2.0f, 2.0f),
                    buttonMax - new Vector2(2.0f, 2.0f),
                    ImGui.GetColorU32(WithAlpha(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), 0.16f)),
                    rounding - 2.0f,
                    (ImDrawFlags)0,
                    1.0f);
            }

            var textSize = ImGui.CalcTextSize(option.Label);
            var textPos = buttonMin + (buttonSize - textSize) * 0.5f;
            drawList.AddText(textPos, ImGui.GetColorU32(buttonTextColor), option.Label);

            if (index < options.Length - 1) {
                ImGui.SameLine(0.0f, buttonSpacing);
            }
        }
    }

    private void DrawInlineSegmentedSelector(string label, string id, int currentValue, Action<int> setter, params (string Label, int Value)[] options) {
        var accentColor = GetSelectorAccentColor(label);
        var buttonWidth = Math.Clamp(options.Max(option => ImGui.CalcTextSize(option.Label).X + 20.0f), 50.0f, 78.0f);

        if (!string.IsNullOrWhiteSpace(label)) {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(label);
            ImGui.SameLine(0.0f, 6.0f);
        }

        for (var index = 0; index < options.Length; index++) {
            if (index > 0) {
                ImGui.SameLine(0.0f, 5.0f);
            }

            var option = options[index];
            var selected = currentValue == option.Value;
            var styleText = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
            var styleButton = ImGui.GetStyle().Colors[(int)ImGuiCol.Button];
            var styleFrameBg = ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg];
            var buttonTextColor = selected
                ? WithAlpha(styleFrameBg, 1.0f)
                : WithAlpha(styleText, 0.86f);
            var buttonColor = selected
                ? WithAlpha(accentColor, 0.72f)
                : WithAlpha(styleButton, 0.88f);
            var buttonHoveredColor = selected
                ? WithAlpha(accentColor, 0.88f)
                : WithAlpha(accentColor, 0.24f);
            var buttonActiveColor = selected
                ? WithAlpha(accentColor, 0.95f)
                : WithAlpha(accentColor, 0.34f);
            var buttonSize = new Vector2(buttonWidth, 24.0f);
            var buttonMin = ImGui.GetCursorScreenPos();
            var buttonMax = buttonMin + buttonSize;
            var drawList = ImGui.GetWindowDrawList();

            ImGui.InvisibleButton($"{option.Label}##{id}_{option.Value}", buttonSize);
            var hovered = ImGui.IsItemHovered();
            var active = ImGui.IsItemActive();
            if (ImGui.IsItemClicked() && !selected) {
                setter(option.Value);
                this.saveConfig();
            }

            var fillColor = active ? buttonActiveColor : hovered ? buttonHoveredColor : buttonColor;
            var borderColor = WithAlpha(accentColor, selected ? 0.86f : hovered ? 0.56f : 0.34f);
            drawList.AddRectFilled(buttonMin + new Vector2(1.0f, 1.0f), buttonMax - new Vector2(1.0f, 1.0f), ImGui.GetColorU32(fillColor), 5.0f);
            drawList.AddRect(buttonMin, buttonMax, ImGui.GetColorU32(borderColor), 5.0f, (ImDrawFlags)0, selected ? 1.4f : 1.0f);
            var textSize = ImGui.CalcTextSize(option.Label);
            drawList.AddText(buttonMin + (buttonSize - textSize) * 0.5f, ImGui.GetColorU32(buttonTextColor), option.Label);
        }
    }

    private static Vector4 GetSelectorAccentColor(string label) {
        if (label.Contains("技能", StringComparison.Ordinal) || label.Contains("独立监控", StringComparison.Ordinal)) {
            return new Vector4(0.42f, 0.36f, 0.72f, 1.0f);
        }

        if (label.Contains("状态", StringComparison.Ordinal)) {
            return new Vector4(0.25f, 0.56f, 0.34f, 1.0f);
        }

        if (label.Contains("咏唱", StringComparison.Ordinal)) {
            return new Vector4(0.78f, 0.38f, 0.16f, 1.0f);
        }

        if (label.Contains("LB", StringComparison.Ordinal)) {
            return new Vector4(0.68f, 0.48f, 0.13f, 1.0f);
        }

        return new Vector4(0.70f, 0.34f, 0.18f, 1.0f);
    }

    private void DrawTargetInfoSubsection(string title) {
        ImGui.Spacing();
        var titleColor = GetSubsectionTitleColor(title);
        var drawList = ImGui.GetWindowDrawList();
        var lineMin = ImGui.GetCursorScreenPos();
        var lineBounds = GetContentLineBounds();
        var lineWidth = Math.Max(24.0f, lineBounds.Right - lineBounds.Left);
        var lineY = lineMin.Y + 3.0f;
        drawList.AddLine(
            new Vector2(lineBounds.Left, lineY),
            new Vector2(lineBounds.Right, lineY),
            ImGui.GetColorU32(WithAlpha(titleColor, 0.32f)),
            1.0f);
        ImGui.Dummy(new Vector2(lineWidth, 7.0f));
        ImGui.PushStyleColor(ImGuiCol.Text, titleColor);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();
        ImGui.Spacing();
    }

    private static (float Left, float Right) GetContentLineBounds() {
        var windowPos = ImGui.GetWindowPos();
        return (
            windowPos.X + ImGui.GetWindowContentRegionMin().X,
            windowPos.X + ImGui.GetWindowContentRegionMax().X);
    }

    private static void DrawFullContentWidthDivider(float yOffset, Vector4 color, float thickness = 1.0f, float height = 1.0f) {
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var lineBounds = GetContentLineBounds();
        var y = min.Y + yOffset;
        drawList.AddLine(
            new Vector2(lineBounds.Left, y),
            new Vector2(lineBounds.Right, y),
            ImGui.GetColorU32(color),
            thickness);
        ImGui.Dummy(new Vector2(1.0f, height));
    }

    private static (float Left, float Right) GetSectionCardLineBounds() {
        var lineBounds = GetContentLineBounds();
        return (lineBounds.Left + 1.0f, lineBounds.Right - 1.0f);
    }

    private bool DrawAddComponentCard(TaskBarComponentDefinition component) {
        var drawList = ImGui.GetWindowDrawList();
        var rowMin = ImGui.GetCursorScreenPos();
        var rowWidth = Math.Max(280.0f, ImGui.GetContentRegionAvail().X);
        const float rowHeight = 48.0f;
        var rowMax = rowMin + new Vector2(rowWidth, rowHeight);

        ImGui.InvisibleButton($"add_component_{component.Id}", new Vector2(rowWidth, rowHeight));
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        var clicked = ImGui.IsItemClicked();

        var styleFrameBg = ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg];
        var styleFrameBgHovered = ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBgHovered];
        var styleFrameBgActive = ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBgActive];
        var styleBorder = ImGui.GetStyle().Colors[(int)ImGuiCol.Border];
        var styleText = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        var styleHeader = ImGui.GetStyle().Colors[(int)ImGuiCol.Header];

        var fill = active
            ? WithAlpha(styleFrameBgActive, 0.96f)
            : hovered
                ? WithAlpha(styleFrameBgHovered, 0.96f)
                : WithAlpha(styleFrameBg, 0.88f);
        var border = hovered
            ? WithAlpha(styleHeader, 0.78f)
            : WithAlpha(styleBorder, 0.44f);

        drawList.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(fill), 8.0f);
        drawList.AddRect(rowMin, rowMax, ImGui.GetColorU32(border), 8.0f, (ImDrawFlags)0, active ? 1.4f : 1.0f);
        DrawTaskBarComponentIcon(drawList, component.Id, rowMin + new Vector2(25.0f, rowHeight * 0.5f), hovered ? WithAlpha(styleText, 1.0f) : WithAlpha(styleText, 0.82f));

        var namePos = rowMin + new Vector2(50.0f, 7.0f);
        var descPos = rowMin + new Vector2(50.0f, 27.0f);
        var textMaxWidth = Math.Max(40.0f, rowMax.X - descPos.X - 12.0f);
        drawList.AddText(namePos, ImGui.GetColorU32(WithAlpha(styleText, 1.0f)), component.Name);
        drawList.AddText(descPos, ImGui.GetColorU32(WithAlpha(styleText, 0.76f)), TrimTextToWidth(component.Description, textMaxWidth));

        ImGui.SetCursorScreenPos(rowMin + new Vector2(0.0f, rowHeight + 6.0f));
        return clicked;
    }

    private static string TrimTextToWidth(string text, float maxWidth) {
        if (string.IsNullOrWhiteSpace(text) || ImGui.CalcTextSize(text).X <= maxWidth) {
            return text;
        }

        const string ellipsis = "…";
        var trimmed = text.Trim();
        while (trimmed.Length > 0 && ImGui.CalcTextSize(trimmed + ellipsis).X > maxWidth) {
            trimmed = trimmed[..^1];
        }

        return trimmed.Length == 0 ? ellipsis : trimmed + ellipsis;
    }

    private void DrawHudScaleCombo(string label, float currentScale, Action<float> setter) {
        DrawInlineHudScaleCombo(label, label, currentScale, setter, null);
    }

    private void DrawInlineFrameLabel(string label, Vector4? labelColor = null) {
        ImGui.AlignTextToFramePadding();
        if (labelColor is { } color) {
            ImGui.PushStyleColor(ImGuiCol.Text, color);
        }
        ImGui.TextUnformatted(label);
        if (labelColor is not null) {
            ImGui.PopStyleColor();
        }
    }

    private void DrawInlineHudScaleCombo(string label, string id, float currentScale, Action<float> setter, Vector4? labelColor = null) {
        ReadOnlySpan<float> scales = [0.60f, 0.80f, 1.00f, 1.20f, 1.40f, 1.60f, 1.80f, 2.00f];
        var currentLabel = $"{MathF.Round(currentScale * 100.0f):0}%";
        var previewLabel = $"{label} {currentLabel}";
        var previewWidth = Math.Clamp(ImGui.CalcTextSize(previewLabel).X + 48.0f, 110.0f, 220.0f);

        ImGui.SetNextItemWidth(previewWidth);
        if (!ImGui.BeginCombo($"##{id}", previewLabel)) {
            return;
        }

        foreach (var scale in scales) {
            var scaleLabel = $"{MathF.Round(scale * 100.0f):0}%";
            var selected = Math.Abs(currentScale - scale) < 0.001f;
            if (ImGui.Selectable(scaleLabel, selected)) {
                setter(scale);
                this.saveConfig();
            }

            if (selected) {
                ImGui.SetItemDefaultFocus();
            }
        }

        ImGui.EndCombo();
    }

    private static bool DrawInlineOpacitySlider(string label, string id, ref float value, float minValue = 0.15f, float maxValue = 1.0f) {
        var percentValue = value * 100.0f;
        var previewLabel = $"{label} {percentValue:0}%";
        var previewWidth = Math.Clamp(ImGui.CalcTextSize(previewLabel).X + 42.0f, 82.0f, 150.0f);

        ImGui.SetNextItemWidth(previewWidth);
        if (!ImGui.SliderFloat($"##{id}", ref percentValue, minValue * 100.0f, maxValue * 100.0f, $"{label} %.0f%%")) {
            return false;
        }

        value = Math.Clamp(percentValue / 100.0f, minValue, maxValue);
        return true;
    }

    private static bool DrawInlinePercentSlider(string label, string id, ref float value, float minValue = 0.0f, float maxValue = 100.0f) {
        var previewLabel = $"{label} {value:0}%";
        var previewWidth = Math.Clamp(ImGui.CalcTextSize(previewLabel).X + 42.0f, 82.0f, 150.0f);

        ImGui.SetNextItemWidth(previewWidth);
        return ImGui.SliderFloat($"##{id}", ref value, minValue, maxValue, $"{label} %.0f%%");
    }
}
