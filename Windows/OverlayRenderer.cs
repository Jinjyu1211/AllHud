using AllHud.Data;
using AllHud.Models;
using AllHud.Services;
using Dalamud.Game.Addon.Events;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Config;
using Dalamud.Game.Inventory;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using LuminaGeneralAction = Lumina.Excel.Sheets.GeneralAction;
using LuminaItem = Lumina.Excel.Sheets.Item;
using LuminaClassJob = Lumina.Excel.Sheets.ClassJob;
using LuminaMainCommand = Lumina.Excel.Sheets.MainCommand;
using LuminaMainCommandCategory = Lumina.Excel.Sheets.MainCommandCategory;
using LuminaTerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace AllHud.Windows;

public sealed partial class OverlayRenderer {
    private const float MinimumIconSize = 12.0f;
    private const float MaximumIconSize = 64.0f;
    private const float NativeStatusIconSize = 38.0f;
    private const float LongStatusThresholdSeconds = 300.0f;
    private const float TaskBarPluginShortcutIconSize = 36.0f;
    private const float TaskBarGameOnlyIconSize = 32.0f;
    private const uint FoodStatusId = 48;
    private const int MaxLiveStatusIcons = 24;
    private const int MaxPreviewStatusIconsPerSection = 8;
    private const int MaxSelfStatusEnhancements = 20;
    private const int MaxSelfStatusEnfeeblements = 20;
    private const int MaxSelfStatusOthers = 20;
    private const int NativeStatusIconsPerRow = 10;
    private const ulong PreviewSourceObjectIdBase = 0xCB7000;
    private const float TaskBarBaseFontSize = 20.0f;
    private const float MaxCooldownIconTimeSeconds = 1800.0f;
    private const string CoordinatesPopupId = "AllHud 坐标";
    private const string GearsetPopupId = "AllHud 套装";
    private const string CurrencyPopupId = "AllHud 货币";
    private const uint CurrencyItemGil = 1;
    private const uint CurrencyItemMgp = 29;
    private const uint CurrencyItemVenture = 21072;
    private const uint CurrencyItemWolfMarks = 25;
    private const uint CurrencyItemPoetics = 28;
    private const uint CurrencyItemBiColorGemstones = 26807;
    private const uint CurrencyItemAlliedSeals = 27;
    private const uint CurrencyItemCenturioSeals = 10307;
    private const uint BlueMageClassJobId = 36;
    private const string EmbeddedDalamudIconPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAQsSURBVFhHxZbfaxNZFMezLkXdZRUqUkV3NTLVJkMSrIUkahIZ6kNbC6KyXasi9kGIixalv2wSFbfrg/+A4IM+iA+++OSD4pMiqOCDCCIi6kxSY42m02lB2W3JLJ+bpkyvWWstjQcCmTP33HPu9/s9Z67LVWH71OxaaGqe3bJ/3uxTs+tHEi7x2D9YUSWW2+3rltfMm1la3VYrqjSMRX7+5dXxmvFcm3JGXjMvZm1b8durnpqCtXmt24rVanrXylw2rlzj3WioapEZWV0tx8zJFt8sLBjdXrMUfgfjyvVsXLly/491VUbnytfDjWoHp4eCUa16uRlS/HL8nMwMBTaaMXWnOGnPmsLIFk8TkOs9a8aHG9WDIEGBw0H3BtbK8d9sJDGSXsvU/Pv1/vU2/ykCeD/s8p6YLGic5GhhJKqGcm3ePnmfWRtQomoSGinfRPbI5qt6yl/I7/AcgfPcvvozJMx2rL0g1sZqd5AcOnh27kVxzuf/NU4Lp4hLT0XsTHf43tt4/UUKMBIBe6ij4Xym0/0Ajmk7hEYcFIhi4so14p17mjF1Lyg5fZ8ZLfTuQMMAHAOrntTecFLjVNAmsZEMp9+3eftJxDM8p7u8zymU9dBDLAdw7sv6GYfRh13eY4Lfsy02P6DVz7aMcaJ0IvpUJD0VtM2wqpBEFJQI2NAx1L7u3OCf7hsoHlqc0Jsx9fd8s+8wiMiUCBN9Ggps1Af22Jx+tGnZTxQA7HBGMLxChQwra9Pd9Y/hn+TATEtCC7E884MqEHLGFjcIVS3S/2639YE9/5Z4JJhirMiqVYiJ0xvJcB6/M5b3xknfR7QhEEn5JugOvc+dh2s6gDaFVg5Isc744iaxWo0+lv34JqkYA3L5PcMGOrLx0GWRPOm1QErE9a+3M0eVW2JAHfr1EgcrKz6EQ3Wy/0tWgjLdG3zBL3MscAehkoAPDyihi3Sn+4l+srZAUcyGz7gXLabVbZ3mnMHMsLqCk2e6wg/RAx2hpyITiBAqSIKQRTfE1L0gIdRfbhSnE82PZN+XDBjRAqcd7AzcBmrRMcyIrvDDkaCnjnUUg25QPu1ZpGP13embadXLZTXPZPrp1mH9dOu7Un9DH8KkAFBBkMIfVRpoQwoGDeig0GkCnu0HgmFDVzg34RCvB5oEx0zF6REul9HreWkkAv+IEZ7yTbDH1MuvnseTZiRanplb3KrTB8cMKoqQBSYGV2/wBaiANLSw3rlmVsac4NtfeiaB/lfrR5LLswHoxcTs2/S+5Ct9P+RCv9ooANFyIniFDp7LIYn4RjT/digq+WhR8b2YZctPGaIS07I4MW0EKK8pmbiQStcvihFinOu17Fsh5NpGAbK/Yla8pvv3l6OsIkYn0K7frYDiXTKcl/0VM74Fzq6ouM14FZtvk6doRY0J+t3Eh5W7hv0Hk1mQnvHVoeMAAAAASUVORK5CYII=";

    private static readonly int[] HudFontSizes = [8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 22, 24, 26, 28, 30, 32];
    private static readonly TimeSpan OverlayPositionSaveDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DtrTaskBarCacheDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan InventoryUsageCacheDuration = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan GearsetPopupCacheDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan CoordinatesTextCacheDuration = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan CurrencyCountCacheDuration = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan GearsetDisplayInfoCacheDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan PluginLookupCacheDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PluginIconPathMissRetryDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PluginIconTextureRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RemotePluginIconRetryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PartyInfoCacheDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan NativePartyAnchorCacheDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan HiddenWindowResolvedDiscoveryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HiddenWindowUnresolvedDiscoveryInterval = TimeSpan.FromMilliseconds(75);
    private static readonly TimeSpan MissingGameIconRetryDelay = TimeSpan.FromMilliseconds(150);
    private static readonly Vector4 MitigationColor = new(0.45f, 0.90f, 1.0f, 1.0f);
    private static readonly Vector4 RaidBuffColor = new(0.86f, 0.62f, 1.0f, 1.0f);
    private static readonly Vector4 SelfAppliedBuffColor = new(0.55f, 1.0f, 0.55f, 1.0f);
    private static readonly Vector4 SelfBuffColor = new(0.65f, 0.90f, 1.0f, 1.0f);
    private static readonly Vector4 SelfDebuffColor = new(1.0f, 0.75f, 0.35f, 1.0f);
    private static readonly Vector4 OtherDebuffColor = new(1.0f, 0.45f, 0.45f, 1.0f);
    private static readonly Vector4 HeaderColor = new(1.0f, 1.0f, 1.0f, 1.0f);
    private static readonly Vector4 PersonalSkillColor = new(0.95f, 0.86f, 0.45f, 1.0f);
    private readonly record struct NativePartyMemberAnchor(Vector2 RowMin, float RowRight, float RowH, bool HasJobIconAnchor);
    private sealed record PartyInfoLookupMaps(
        IReadOnlyDictionary<uint, PartyCooldownGroupEntry> GroupsByEntityId,
        IReadOnlyDictionary<ulong, PartyCooldownGroupEntry> GroupsByObjectId,
        IReadOnlyDictionary<int, PartyCooldownGroupEntry> GroupsByPartySlot,
        IReadOnlyDictionary<uint, PartyFoodStatusEntry> FoodByEntityId,
        IReadOnlyDictionary<ulong, PartyFoodStatusEntry> FoodByObjectId,
        IReadOnlyDictionary<int, PartyFoodStatusEntry> FoodByPartySlot);
    private sealed record PartyInfoRenderSnapshot(bool ShowMitigations, bool ShowFoodCheck, bool ShowLimitBreak, IReadOnlyList<PartyCooldownGroupEntry> Groups, IReadOnlyList<PartyFoodStatusEntry> FoodStatuses, PartyInfoLookupMaps Lookups);
    private sealed record NativePartyAnchorSnapshot(NativePartyMemberAnchor?[] Anchors, float? FallbackJobIconLeft, float FallbackJobIconHeight);
    private readonly record struct CachedHiddenImGuiWindow(string Key, ImGuiWindowPtr Window);
    private readonly record struct StatusIconSection(string Label, Vector4 Color, IReadOnlyList<StatusEntry> Statuses);
    private readonly record struct TargetStatusIconLayout(StatusEntry Status, Vector2 Offset, float Size, int Index);
    private readonly record struct LimitBreakGaugeSnapshot(float Current, float Max, int Segments);
    private readonly record struct PluginListEntry(string InternalName, Dalamud.Plugin.IExposedPlugin? Plugin);
    private readonly record struct PluginIconPathCacheEntry(string? Path, DateTime RetryAt);
    private readonly record struct DtrTaskBarSnapshot(string Title, string Text, string Tooltip, bool HasClickAction, Action<DtrInteractionEvent>? OnClick);
    private readonly record struct GearsetPopupEntry(byte Id, string JobName, uint ClassJobId, short JobLevel, string Group, int GroupSort, int JobSort, uint IconId, bool Selected);
    private readonly record struct GearsetPopupGroup(string Group, List<GearsetPopupEntry> Entries);
    private readonly record struct GearsetPopupCache(int Fingerprint, int CurrentGearsetIndex, DateTime RefreshAt, List<GearsetPopupEntry> Entries, List<GearsetPopupGroup> LeftGroups, List<GearsetPopupGroup> RightGroups);
    private readonly record struct GearsetJobMetadata(string Name, int ExpArrayIndex, string Group, int GroupSort, int JobSort, uint IconId);
    private readonly record struct GearsetTaskBarCache(int GearsetId, uint ClassJobId, short JobLevel, bool ShowNumber, bool ShowName, bool ShowLevel, string Text, string MeasureText, uint IconId);
    private readonly record struct StatusSectionLayout(StatusIconSection Section, int Columns, Vector2 GridSize, Vector2 Size);
    private readonly record struct GameIconCacheKey(uint IconId, bool HiRes);
    private enum TaskBarDrawIcon {
        None,
        MainMenu,
        Volume,
        PluginList,
        QuickMenu,
    }

    private readonly record struct TaskBarItem(string Text, string Tooltip, Action<DtrInteractionEvent>? OnClick, Action<DtrInteractionEvent>? OnRightClick = null, string PopupId = "", bool AdjustVolumeOnWheel = false, bool IsIcon = false, string MeasureText = "", bool IsDalamudIcon = false, uint GameIconId = 0, float TextScale = 1.0f, string PluginShortcutInternalName = "", Dalamud.Plugin.IExposedPlugin? PluginShortcutPlugin = null, TaskBarDrawIcon DrawIcon = TaskBarDrawIcon.None, string QuickMenuComponentId = "");
    private readonly record struct GearsetDisplayInfo(byte GearsetId, string Name, uint ClassJobId);
    private readonly record struct TaskBarPopupAnchor(Vector2 Min, Vector2 Max);
    private readonly record struct TaskBarRowMetrics(float Width, float Height);
    private readonly record struct TaskBarMainMenuCategory(uint RowId, string Name, uint IconId, List<TaskBarMainMenuEntry> Entries);
    private readonly record struct TaskBarMainMenuEntry(string Name, uint? CommandId, string ChatCommand, uint IconId, bool IsSeparator = false);
    private sealed class EmptyDisposable : IDisposable {
        public static readonly EmptyDisposable Instance = new();
        public void Dispose() { }
    }

    private sealed class StyleScope : IDisposable {
        private readonly int colorCount;
        private readonly int varCount;

        public StyleScope(int colorCount, int varCount = 0) {
            this.colorCount = colorCount;
            this.varCount = varCount;
        }

        public void Dispose() {
            if (this.varCount > 0) {
                ImGui.PopStyleVar(this.varCount);
            }

            if (this.colorCount > 0) {
                ImGui.PopStyleColor(this.colorCount);
            }
        }
    }

    private readonly Configuration config;
    private readonly CombatStateTracker combatState;
    private readonly IDataManager dataManager;
    private readonly ITextureProvider textureProvider;
    private readonly IGameGui gameGui;
    private readonly IAddonEventManager addonEventManager;
    private readonly ICommandManager commandManager;
    private readonly IGameConfig gameConfig;
    private readonly IGameInventory gameInventory;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDtrBar dtrBar;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Action saveConfig;
    private readonly string pluginIconCacheDirectory;
    private readonly Dictionary<int, IFontHandle> hudFonts = [];
    private readonly Dictionary<int, IFontHandle> iconFonts = [];
    private readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly Dictionary<string, PluginIconPathCacheEntry> pluginIconPathCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISharedImmediateTexture> pluginIconTextureCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> pluginIconTextureRetryAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> remotePluginIconCachePathCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> missingCachedRemotePluginIconRetryAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> remotePluginIconRetryAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, IDalamudTextureWrap?> frameGameIconWrapCache = [];
    private readonly Dictionary<GameIconCacheKey, ISharedImmediateTexture> gameIconTextureCache = [];
    private readonly Dictionary<uint, DateTime> missingGameIconRetryAt = [];
    private readonly Dictionary<uint, GearsetJobMetadata> gearsetJobMetadataCache = [];
    private GearsetTaskBarCache? gearsetTaskBarCache;
    private GearsetPopupCache? gearsetPopupCache;
    private readonly Dictionary<int, string> cooldownIconTimeTextCache = [];
    private readonly Dictionary<int, string> targetStatusTimeTextCache = [];
    private readonly Dictionary<int, Vector2> lastSavedAuxiliaryBarPositions = [];
    private readonly Dictionary<int, DateTime> auxiliaryBarPositionSaveDueAt = [];
    private readonly Dictionary<string, Dalamud.Plugin.IExposedPlugin> installedPluginLookupCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<IReadOnlyList<CooldownEntry>, IReadOnlyList<CooldownEntry>> nativeMitigationCooldownCache = [];
    private readonly HashSet<string> hiddenImGuiWindowKeyCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<CachedHiddenImGuiWindow>> cachedHiddenImGuiWindows = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap?> pluginRemoteIconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap?>> pluginRemoteIconTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TaskBarMainMenuCategory> mainMenuCategories = [];
    private Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? embeddedDalamudIconTexture;
    private Task<Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap?>? embeddedDalamudIconTextureTask;
    private string pluginListFilter = string.Empty;
    private string taskBarFpsText = "FPS 000";
    private List<TaskBarItem> cachedDtrTaskBarItems = [];
    private List<DtrTaskBarSnapshot> cachedDtrTaskBarSnapshots = [];
    private readonly List<DtrTaskBarSnapshot> dtrTaskBarSnapshotBuffer = [];
    private (int Used, int Total) cachedInventoryUsage;
    private (int Used, int Total) cachedSaddlebagUsage;
    private PartyInfoRenderSnapshot? cachedPartyInfoSnapshot;
    private NativePartyAnchorSnapshot? cachedNativePartyAnchorSnapshot;
    // 自身状态分段缓存：selfStatuses 由 100ms TTL 缓存返回稳定引用，section 构建（Where/OrderBy/ToList）
    // 仅依赖该引用与三个开关，因此按引用 + 开关位记忆，避免每帧重复 LINQ 过滤排序。
    private IReadOnlyList<StatusEntry>? cachedSelfStatusSectionSource;
    private int cachedSelfStatusSectionToggles = -1;
    private IReadOnlyList<StatusIconSection>? cachedSelfStatusSections;
    // 目标状态自/他拆分缓存：targetInfo.Statuses 为 100ms TTL 缓存返回的稳定引用，
    // self/other/concat 三次 Where+ToList 仅依赖该引用，按引用记忆避免每帧重复分配。
    private IReadOnlyList<StatusEntry>? cachedTargetStatusSplitSource;
    private IReadOnlyList<StatusEntry>? cachedTargetOrderedStatuses;
    private int hiddenImGuiWindowKeyCacheSourceCount = -1;
    private ulong hiddenImGuiWindowKeyCacheFingerprint;
    private nint hiddenImGuiContextPtr;
    private int hiddenImGuiWindowCount = -1;
    private int selectedMainMenuCategoryIndex;
    private bool mainMenuLoaded;
    private uint? saddlebagMainCommandId;
    private uint? mapMainCommandId;
    private TaskBarPopupAnchor mainMenuPopupAnchor;
    private TaskBarPopupAnchor volumePopupAnchor;
    private TaskBarPopupAnchor pluginPopupAnchor;
    private TaskBarPopupAnchor quickMenuPopupAnchor;
    private TaskBarPopupAnchor simplePopupAnchor;
    private string activeQuickMenuComponentId = string.Empty;
    private int pendingPluginListPopupOpenFrames;
    private Vector2 lastSavedStatusOverlayPosition;
    private Vector2 lastSavedSelfCooldownBarPosition;
    private Vector2 lastSavedTargetInfoPosition;
    private Vector2 lastSavedTargetInfoCastBarPosition;
    private Vector2 lastSavedTargetInfoStatusBarPosition;
    private DateTime? statusOverlayPositionSaveDueAt;
    private DateTime? selfCooldownBarPositionSaveDueAt;
    private DateTime? targetInfoPositionSaveDueAt;
    private DateTime? targetInfoCastBarPositionSaveDueAt;
    private DateTime? targetInfoStatusBarPositionSaveDueAt;
    private bool nativeCursorForced;
    private bool nativeCursorRequestedThisFrame;
    private bool pluginListPopupDrawnThisFrame;
    private DateTime nextTaskBarFpsUpdateAt;
    private DateTime nextDtrTaskBarRefreshAt;
    private DateTime nextInventoryUsageRefreshAt;
    private DateTime nextPluginLookupRefreshAt;
    private DateTime nextPartyInfoRefreshAt;
    private DateTime nextNativePartyAnchorRefreshAt;
    private DateTime nextHiddenWindowDiscoveryAt;
    private uint cachedTerritoryNameId = uint.MaxValue;
    private string cachedTerritoryName = string.Empty;
    private string? cachedCoordinatesText;
    private DateTime nextCoordinatesTextRefreshAt;
    private long cachedCurrencyCount = -1;
    private uint cachedCurrencyCountItemId;
    private DateTime nextCurrencyCountRefreshAt;
    private GearsetDisplayInfo? cachedGearsetDisplayInfo;
    private DateTime nextGearsetDisplayInfoRefreshAt;
    private int overlayStartupWarmupFrames;

    private static readonly string[] GearsetPopupLeftGroups = ["防护职业", "近战职业", "生产职业"];
    private static readonly string[] GearsetPopupRightGroups = ["治疗职业", "远程物理职业", "远程魔法职业", "采集职业", "其他"];

    public OverlayRenderer(Configuration config, CombatStateTracker combatState, IDataManager dataManager, ITextureProvider textureProvider, IGameGui gameGui, IAddonEventManager addonEventManager, ICommandManager commandManager, IGameConfig gameConfig, IGameInventory gameInventory, IClientState clientState, IObjectTable objectTable, IDtrBar dtrBar, IDalamudPluginInterface pluginInterface, Action saveConfig) {
        this.config = config;
        this.combatState = combatState;
        this.dataManager = dataManager;
        this.textureProvider = textureProvider;
        this.gameGui = gameGui;
        this.addonEventManager = addonEventManager;
        this.commandManager = commandManager;
        this.gameConfig = gameConfig;
        this.gameInventory = gameInventory;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dtrBar = dtrBar;
        this.pluginInterface = pluginInterface;
        this.saveConfig = saveConfig;
        this.pluginIconCacheDirectory = Path.Combine(this.pluginInterface.ConfigDirectory.FullName, "plugin-icon-cache");
        this.lastSavedStatusOverlayPosition = config.StatusOverlayPosition;
        this.lastSavedSelfCooldownBarPosition = config.SelfCooldownBarPosition;
        this.lastSavedTargetInfoPosition = config.CustomTargetInfoPosition;
        this.lastSavedTargetInfoCastBarPosition = config.CustomTargetInfoCastBarPosition;
        this.lastSavedTargetInfoStatusBarPosition = config.CustomTargetInfoStatusBarPosition;
        this.overlayStartupWarmupFrames = 2;
    }

    public void Dispose() {
        ResetNativeCursorIfNeeded();
        foreach (var fontHandle in this.hudFonts.Values) {
            fontHandle.Dispose();
        }

        foreach (var fontHandle in this.iconFonts.Values) {
            fontHandle.Dispose();
        }

        foreach (var texture in this.pluginRemoteIconCache.Values) {
            texture?.Dispose();
        }

        this.embeddedDalamudIconTexture?.Dispose();

        this.httpClient.Dispose();
    }

    public void Draw() {
        this.nativeCursorRequestedThisFrame = false;
        this.pluginListPopupDrawnThisFrame = false;
        this.frameGameIconWrapCache.Clear();
        ApplyKnownFloatingShortcutWindowHides();

        var flags = ImGuiWindowFlags.NoTitleBar
                    | ImGuiWindowFlags.NoSavedSettings
                    | ImGuiWindowFlags.NoFocusOnAppearing;

        DrawCustomTargetInfoWindow(flags | ImGuiWindowFlags.AlwaysAutoResize);
        var drawTaskBarsThisFrame = this.overlayStartupWarmupFrames <= 0;
        if (drawTaskBarsThisFrame) {
            DrawTaskBarWindow(flags);
            DrawAuxiliaryBars(flags);
        } else {
            this.overlayStartupWarmupFrames--;
        }
        DrawSkillsWindow();
        DrawSelfCooldownBarWindow(flags | ImGuiWindowFlags.AlwaysAutoResize);
        if (this.config.ShowStatusOverlay) {
            DrawStatusWindow(flags | ImGuiWindowFlags.AlwaysAutoResize);
        }

        if (!this.nativeCursorRequestedThisFrame) {
            ResetNativeCursorIfNeeded();
        }
    }

    private void ApplyKnownFloatingShortcutWindowHides() {
        if (this.config.HiddenImGuiWindowNames.Count == 0) {
            ClearHiddenImGuiWindowDiscoveryCache();
            return;
        }

        TryApplyKnownFloatingShortcutWindowHides();
    }

    private unsafe void TryApplyKnownFloatingShortcutWindowHides() {
        try {
            var ctxPtr = ImGuiNative.GetCurrentContext();
            if (ctxPtr == null) {
                ClearHiddenImGuiWindowDiscoveryCache();
                return;
            }

            var ctx = new ImGuiContextPtr(ctxPtr);
            if (ctx.IsNull) {
                ClearHiddenImGuiWindowDiscoveryCache();
                return;
            }

            var contextPtr = (nint)ctxPtr;
            if (this.hiddenImGuiContextPtr != contextPtr) {
                ClearHiddenImGuiWindowDiscoveryCache();
                this.hiddenImGuiContextPtr = contextPtr;
                this.nextHiddenWindowDiscoveryAt = DateTime.MinValue;
            }

            var keysToHide = GetHiddenImGuiWindowKeyCache();
            if (keysToHide.Count == 0) {
                ClearHiddenImGuiWindowDiscoveryCache();
                return;
            }

            ref var windows = ref ctx.Windows;
            var now = DateTime.UtcNow;
            var windowCountChanged = this.hiddenImGuiWindowCount != windows.Size;
            if (windowCountChanged || now >= this.nextHiddenWindowDiscoveryAt) {
                DiscoverHiddenImGuiWindows(windows, keysToHide);
                this.hiddenImGuiWindowCount = windows.Size;
                this.nextHiddenWindowDiscoveryAt = now + (HasUnresolvedHiddenImGuiWindows(keysToHide)
                    ? HiddenWindowUnresolvedDiscoveryInterval
                    : HiddenWindowResolvedDiscoveryInterval);
            }

            ApplyCachedHiddenImGuiWindows();
        } catch {
        }
    }

    private void ClearHiddenImGuiWindowDiscoveryCache() {
        if (this.cachedHiddenImGuiWindows.Count == 0
            && this.hiddenImGuiContextPtr == 0
            && this.hiddenImGuiWindowCount == -1
            && this.nextHiddenWindowDiscoveryAt == DateTime.MinValue) {
            return;
        }

        this.cachedHiddenImGuiWindows.Clear();
        this.hiddenImGuiContextPtr = 0;
        this.hiddenImGuiWindowCount = -1;
        this.nextHiddenWindowDiscoveryAt = DateTime.MinValue;
    }

    private HashSet<string> GetHiddenImGuiWindowKeyCache() {
        var fingerprint = GetHiddenImGuiWindowNameFingerprint();
        if (this.hiddenImGuiWindowKeyCacheSourceCount == this.config.HiddenImGuiWindowNames.Count
            && this.hiddenImGuiWindowKeyCacheFingerprint == fingerprint) {
            return this.hiddenImGuiWindowKeyCache;
        }

        this.hiddenImGuiWindowKeyCache.Clear();
        foreach (var windowName in this.config.HiddenImGuiWindowNames) {
            var key = NormalizeImGuiWindowNameKey(windowName);
            if (key.Length > 0) {
                this.hiddenImGuiWindowKeyCache.Add(key);
            }
        }

        this.hiddenImGuiWindowKeyCacheSourceCount = this.config.HiddenImGuiWindowNames.Count;
        this.hiddenImGuiWindowKeyCacheFingerprint = fingerprint;
        this.cachedHiddenImGuiWindows.Clear();
        this.hiddenImGuiWindowCount = -1;
        this.nextHiddenWindowDiscoveryAt = DateTime.MinValue;
        return this.hiddenImGuiWindowKeyCache;
    }

    private ulong GetHiddenImGuiWindowNameFingerprint() {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var windowName in this.config.HiddenImGuiWindowNames) {
            AppendTrimmedStringHash(windowName, ref hash, prime);
            hash ^= 31;
            hash *= prime;
        }

        return hash;
    }

    private static void AppendTrimmedStringHash(string? value, ref ulong hash, ulong prime) {
        if (string.IsNullOrEmpty(value)) {
            return;
        }

        var start = 0;
        var end = value.Length - 1;
        while (start <= end && char.IsWhiteSpace(value[start])) {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(value[end])) {
            end--;
        }

        for (var index = start; index <= end; index++) {
            hash ^= value[index];
            hash *= prime;
        }
    }

    private static string NormalizeImGuiWindowNameKey(string windowName) {
        var trimmed = (windowName ?? string.Empty).Trim();
        if (trimmed.Length == 0) {
            return string.Empty;
        }

        var idx = trimmed.IndexOf("###", StringComparison.Ordinal);
        return idx >= 0 ? trimmed[idx..] : trimmed;
    }

    private unsafe void DiscoverHiddenImGuiWindows(ImVector<ImGuiWindowPtr> windows, IReadOnlySet<string> keysToHide) {
        this.cachedHiddenImGuiWindows.Clear();
        for (var i = 0; i < windows.Size; i++) {
            var window = windows[i];
            if (window.Handle == null || window.Name == null) {
                continue;
            }

            var name = Marshal.PtrToStringUTF8((nint)window.Name) ?? string.Empty;
            var key = NormalizeImGuiWindowNameKey(name);
            if (key.Length == 0 || !keysToHide.Contains(key)) {
                continue;
            }

            if (!this.cachedHiddenImGuiWindows.TryGetValue(key, out var cachedWindows)) {
                cachedWindows = [];
                this.cachedHiddenImGuiWindows[key] = cachedWindows;
            }

            cachedWindows.Add(new CachedHiddenImGuiWindow(key, window));
        }
    }

    private bool HasUnresolvedHiddenImGuiWindows(IReadOnlySet<string> keysToHide) {
        foreach (var key in keysToHide) {
            if (!this.cachedHiddenImGuiWindows.TryGetValue(key, out var cachedWindows) || cachedWindows.Count == 0) {
                return true;
            }
        }

        return false;
    }

    private unsafe void ApplyCachedHiddenImGuiWindows() {
        foreach (var cachedWindows in this.cachedHiddenImGuiWindows.Values) {
            foreach (var cachedWindow in cachedWindows) {
                ApplyHardHideToImGuiWindow(cachedWindow.Window);
            }
        }
    }

    private static unsafe void ApplyHardHideToImGuiWindow(ImGuiWindowPtr window) {
        if (window.Handle == null) {
            return;
        }

        const sbyte frameCount = 2;
        window.Hidden = true;
        window.SkipItems = true;
        window.HiddenFramesCanSkipItems = frameCount;
        window.HiddenFramesCannotSkipItems = frameCount;
        window.HiddenFramesForRenderOnly = frameCount;

        try {
            ref var drawList = ref window.DrawList;
            drawList.CmdBuffer.Clear();
            drawList.IdxBuffer.Clear();
            drawList.VtxBuffer.Clear();
        } catch {
        }
    }

    private IDisposable PushTaskBarFont(float scale) {
        return PushHudFont(TaskBarBaseFontSize * Math.Clamp(scale, 0.6f, 2.0f));
    }

    private IDisposable PushTaskBarIconFont(float scale) {
        return PushIconFont(TaskBarBaseFontSize * Math.Clamp(scale, 0.6f, 2.0f));
    }

    private IDisposable PushHudFont(float fontSize) {
        var font = GetHudFont(GetNearestHudFontSize(fontSize));
        return font.Available ? font.Push() : EmptyDisposable.Instance;
    }

    private IDisposable PushIconFont(float fontSize) {
        var font = GetIconFont(GetNearestHudFontSize(fontSize));
        return font.Available ? font.Push() : EmptyDisposable.Instance;
    }

    private IDisposable PushTaskBarItemFont(TaskBarItem item, float scale) {
        if (item.IsIcon) {
            return PushTaskBarIconFont(scale);
        }

        return Math.Abs(item.TextScale - 1.0f) > 0.001f
            ? PushTaskBarFont(scale * item.TextScale)
            : EmptyDisposable.Instance;
    }

    private Vector2 CalcTaskBarItemTextSize(TaskBarItem item, float scale) {
        if (item.DrawIcon != TaskBarDrawIcon.None) {
            var size = MathF.Round(TaskBarPluginShortcutIconSize * scale);
            return new Vector2(size, size);
        }

        if (!string.IsNullOrWhiteSpace(item.PluginShortcutInternalName)) {
            var size = MathF.Round(TaskBarPluginShortcutIconSize * scale);
            return new Vector2(size, size);
        }

        if (item.IsDalamudIcon) {
            var size = MathF.Round(TaskBarPluginShortcutIconSize * scale);
            return new Vector2(size, size);
        }

        if (item.GameIconId != 0) {
            var iconSize = MathF.Round(GetTaskBarGameIconSize(item, scale));
            using var itemFont = PushTaskBarItemFont(item, scale);
            var textSize = CalcMultilineTextSize(string.IsNullOrEmpty(item.MeasureText) ? item.Text : item.MeasureText);
            return string.IsNullOrWhiteSpace(item.Text) && string.IsNullOrWhiteSpace(item.MeasureText)
                ? new Vector2(iconSize, iconSize)
                : new Vector2(iconSize + 6.0f * scale + textSize.X, Math.Max(iconSize, textSize.Y));
        }

        using var font = PushTaskBarItemFont(item, scale);
        return CalcMultilineTextSize(string.IsNullOrEmpty(item.MeasureText) ? item.Text : item.MeasureText);
    }

    private Vector2 CalcTaskBarItemDrawTextSize(TaskBarItem item, float scale) {
        if (item.DrawIcon != TaskBarDrawIcon.None) {
            var size = MathF.Round(TaskBarPluginShortcutIconSize * scale);
            return new Vector2(size, size);
        }

        if (!string.IsNullOrWhiteSpace(item.PluginShortcutInternalName)) {
            var size = MathF.Round(TaskBarPluginShortcutIconSize * scale);
            return new Vector2(size, size);
        }

        if (item.IsDalamudIcon) {
            var size = MathF.Round(TaskBarPluginShortcutIconSize * scale);
            return new Vector2(size, size);
        }

        if (item.GameIconId != 0) {
            var iconSize = MathF.Round(GetTaskBarGameIconSize(item, scale));
            using var itemFont = PushTaskBarItemFont(item, scale);
            var textSize = CalcMultilineTextSize(item.Text);
            return string.IsNullOrWhiteSpace(item.Text)
                ? new Vector2(iconSize, iconSize)
                : new Vector2(iconSize + 6.0f * scale + textSize.X, Math.Max(iconSize, textSize.Y));
        }

        using var font = PushTaskBarItemFont(item, scale);
        return CalcMultilineTextSize(item.Text);
    }

    private static bool IsTaskBarIconLikeItem(TaskBarItem item) {
        return item.DrawIcon != TaskBarDrawIcon.None
               || item.IsDalamudIcon
               || !string.IsNullOrWhiteSpace(item.PluginShortcutInternalName)
               || (item.GameIconId != 0 && string.IsNullOrWhiteSpace(item.Text));
    }

    private Vector2 CalcTaskBarItemLayoutSize(TaskBarItem item, float scale) {
        var size = CalcTaskBarItemTextSize(item, scale);
        if (!IsTaskBarIconLikeItem(item)) {
            return size;
        }

        var square = MathF.Round(Math.Max(36.0f * scale, Math.Max(size.X, size.Y)));
        return new Vector2(square, square);
    }

    private static Vector2 CalcMultilineTextSize(string text) {
        var lineHeight = ImGui.GetTextLineHeight();
        if (text.IndexOf('\n', StringComparison.Ordinal) < 0) {
            return new Vector2(ImGui.CalcTextSize(text).X, lineHeight);
        }

        var width = 0.0f;
        var lineCount = 0;
        var start = 0;
        while (start <= text.Length) {
            var newlineIndex = text.IndexOf('\n', start);
            var end = newlineIndex < 0 ? text.Length : newlineIndex;
            var line = text[start..end];
            width = Math.Max(width, ImGui.CalcTextSize(line).X);
            lineCount++;
            if (newlineIndex < 0) {
                break;
            }

            start = newlineIndex + 1;
        }

        return new Vector2(width, lineHeight * lineCount);
    }

    private static float GetTaskBarGameIconSize(TaskBarItem item, float scale) {
        return (string.IsNullOrWhiteSpace(item.Text) && string.IsNullOrWhiteSpace(item.MeasureText)
            ? TaskBarGameOnlyIconSize
            : 22.0f) * scale;
    }

    private IFontHandle GetHudFont(int fontSize) {
        if (this.hudFonts.TryGetValue(fontSize, out var font)) {
            return font;
        }

        font = this.pluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(fontSize)));
        this.hudFonts.Add(fontSize, font);
        return font;
    }

    private IFontHandle GetIconFont(int fontSize) {
        if (this.iconFonts.TryGetValue(fontSize, out var font)) {
            return font;
        }

        font = this.pluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddFontAwesomeIconFont(new SafeFontConfig { SizePx = fontSize })));
        this.iconFonts.Add(fontSize, font);
        return font;
    }

    private static int GetNearestHudFontSize(float fontSize) {
        var targetSize = Math.Clamp((int)MathF.Round(fontSize), HudFontSizes[0], HudFontSizes[^1]);
        return HudFontSizes.MinBy(size => Math.Abs(size - targetSize));
    }

    private bool IsHudFontReady(float fontSize) {
        return GetHudFont(GetNearestHudFontSize(fontSize)).Available;
    }

    private bool IsIconFontReady(float fontSize) {
        return GetIconFont(GetNearestHudFontSize(fontSize)).Available;
    }

    private static Vector2 SnapToPixel(Vector2 value) {
        return new Vector2(MathF.Round(value.X), MathF.Round(value.Y));
    }

    private static float SnapToPixel(float value) {
        return MathF.Round(value);
    }








    private static string FormatNumber(uint value) {
        return value.ToString("N0");
    }

    private void DrawSkillsWindow() {
        if (!this.config.ShowPartyInfo) {
            return;
        }

        if (!ShouldDrawPartyInfoNow()) {
            return;
        }

        var snapshot = GetPartyInfoRenderSnapshot();
        if (!snapshot.ShowMitigations && !snapshot.ShowFoodCheck && !snapshot.ShowLimitBreak) {
            return;
        }

        if (this.config.ShowPartyInfoPreview) {
            DrawPartyInfoPreview(snapshot);
            return;
        }

        DrawNativeAttachedPartyInfo(
            snapshot.Groups,
            snapshot.FoodStatuses,
            snapshot.Lookups,
            snapshot.ShowLimitBreak);
    }

    private bool ShouldDrawPartyInfoNow() {
        return this.config.ShowPartyInfoPreview || this.combatState.IsInDutyActive;
    }

    private void DrawPartyInfoPreview(PartyInfoRenderSnapshot snapshot) {
        var drawList = ImGui.GetBackgroundDrawList();
        var viewport = ImGui.GetMainViewport();
        const int columnCount = 2;
        const float panelPadding = 16.0f;
        const float columnGap = 12.0f;
        const float rowHeight = 46.0f;
        const float rowGap = 5.0f;
        const float titleHeight = 22.0f;
        const float limitBreakHeight = 26.0f;

        var rowCount = Math.Max(snapshot.Groups.Count, snapshot.FoodStatuses.Count);
        if (rowCount <= 0 && !snapshot.ShowLimitBreak) {
            return;
        }

        var rowsPerColumn = Math.Max(1, (int)MathF.Ceiling(rowCount / (float)columnCount));
        var contentWidth = Math.Min(760.0f, Math.Max(520.0f, viewport.WorkSize.X - 48.0f));
        var columnWidth = (contentWidth - panelPadding * 2.0f - columnGap) / columnCount;
        var contentHeight = panelPadding + titleHeight + (snapshot.ShowLimitBreak ? limitBreakHeight : 0.0f) + rowsPerColumn * rowHeight + Math.Max(0, rowsPerColumn - 1) * rowGap + panelPadding;
        var panelMin = SnapToPixel(new Vector2(
            viewport.WorkPos.X + viewport.WorkSize.X - contentWidth - 24.0f,
            viewport.WorkPos.Y + Math.Max(24.0f, (viewport.WorkSize.Y - contentHeight) * 0.5f)));
        var panelMax = panelMin + new Vector2(contentWidth, contentHeight);

        drawList.AddRectFilled(panelMin, panelMax, ImGui.GetColorU32(new Vector4(0.08f, 0.06f, 0.09f, 0.82f)), 10.0f);
        drawList.AddRect(panelMin, panelMax, ImGui.GetColorU32(new Vector4(0.62f, 0.36f, 0.48f, 0.78f)), 10.0f, (ImDrawFlags)0, 1.4f);
        drawList.AddText(panelMin + new Vector2(panelPadding, 10.0f), ImGui.GetColorU32(new Vector4(0.98f, 0.94f, 0.96f, 1.0f)), "队伍信息预览（全职业）");

        var rowsTop = panelMin.Y + panelPadding + titleHeight;
        if (snapshot.ShowLimitBreak) {
            DrawLimitBreakGauge(drawList, panelMin + new Vector2(panelPadding, rowsTop - panelMin.Y), contentWidth - panelPadding * 2.0f, 18.0f, new LimitBreakGaugeSnapshot(2.0f, 3.0f, 3));
            rowsTop += limitBreakHeight;
        }

        for (var index = 0; index < rowCount; index++) {
            var column = index / rowsPerColumn;
            var row = index % rowsPerColumn;
            var rowMin = new Vector2(
                panelMin.X + panelPadding + column * (columnWidth + columnGap),
                rowsTop + row * (rowHeight + rowGap));
            var group = index < snapshot.Groups.Count ? snapshot.Groups[index] : null;
            var foodStatus = snapshot.ShowFoodCheck
                ? FindPartyInfoPreviewFoodStatus(snapshot.FoodStatuses, snapshot.Lookups, group, index)
                : null;
            DrawPartyInfoPreviewRow(drawList, rowMin, columnWidth, rowHeight, group, foodStatus);
        }
    }

    private static PartyFoodStatusEntry? FindPartyInfoPreviewFoodStatus(IReadOnlyList<PartyFoodStatusEntry> foodStatuses, PartyInfoLookupMaps lookups, PartyCooldownGroupEntry? group, int index) {
        if (group is not null) {
            if (group.SourceEntityId != 0 && lookups.FoodByEntityId.TryGetValue(group.SourceEntityId, out var foodStatus)) {
                return foodStatus;
            }

            if (group.SourceObjectId != 0 && lookups.FoodByObjectId.TryGetValue(group.SourceObjectId, out foodStatus)) {
                return foodStatus;
            }

            if (group.PartySlot >= 0 && lookups.FoodByPartySlot.TryGetValue(group.PartySlot, out foodStatus)) {
                return foodStatus;
            }

            return null;
        }

        return index < foodStatuses.Count ? foodStatuses[index] : null;
    }

    private void DrawPartyInfoPreviewRow(ImDrawListPtr drawList, Vector2 rowMin, float rowWidth, float rowHeight, PartyCooldownGroupEntry? group, PartyFoodStatusEntry? foodStatus) {
        var rowMax = rowMin + new Vector2(rowWidth, rowHeight);
        drawList.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(new Vector4(0.14f, 0.11f, 0.16f, 0.78f)), 8.0f);
        drawList.AddRect(rowMin, rowMax, ImGui.GetColorU32(new Vector4(0.35f, 0.22f, 0.28f, 0.72f)), 8.0f, (ImDrawFlags)0, 1.0f);

        var sourceJobIconId = group?.SourceJobIconId ?? 0;
        var sourceName = group?.SourceName ?? foodStatus?.SourceName ?? "预览队员";
        var sourceJobName = group?.SourceJobName ?? foodStatus?.SourceJobName ?? string.Empty;
        IReadOnlyList<CooldownEntry> cooldowns = group is not null
            ? GetNativeAttachedMitigationCooldowns(group.Cooldowns)
            : Array.Empty<CooldownEntry>();

        var jobIconMin = rowMin + new Vector2(6.0f, 6.0f);
        var jobIconMax = jobIconMin + new Vector2(34.0f, 34.0f);
        DrawGameIconImage(drawList, sourceJobIconId, jobIconMin, jobIconMax, true, true);

        var textX = jobIconMax.X + 8.0f;
        drawList.AddText(new Vector2(textX, rowMin.Y + 6.0f), ImGui.GetColorU32(new Vector4(0.98f, 0.95f, 0.96f, 1.0f)), sourceName);
        drawList.AddText(new Vector2(textX, rowMin.Y + 23.0f), ImGui.GetColorU32(new Vector4(0.72f, 0.56f, 0.62f, 0.90f)), sourceJobName);

        const float iconSize = 32.0f;
        const float iconGap = 4.0f;
        var iconCount = cooldowns.Count + (foodStatus is not null ? 1 : 0);
        if (iconCount <= 0) {
            return;
        }

        var iconsWidth = iconCount * iconSize + Math.Max(0, iconCount - 1) * iconGap;
        var iconX = Math.Max(textX + 72.0f, rowMax.X - iconsWidth - 8.0f);
        var iconY = rowMin.Y + (rowHeight - iconSize) * 0.5f;
        for (var index = 0; index < cooldowns.Count; index++) {
            DrawNativeAttachedCooldownIcon(drawList, new Vector2(iconX + index * (iconSize + iconGap), iconY), cooldowns[index], iconSize);
        }

        if (foodStatus is not null) {
            DrawNativeAttachedFoodIcon(drawList, new Vector2(iconX + cooldowns.Count * (iconSize + iconGap), iconY), foodStatus, iconSize);
        }
    }

    private PartyInfoRenderSnapshot GetPartyInfoRenderSnapshot() {
        var now = DateTime.UtcNow;
        if (this.cachedPartyInfoSnapshot is not null && now < this.nextPartyInfoRefreshAt) {
            return this.cachedPartyInfoSnapshot;
        }

        if (this.config.ShowPartyInfoPreview) {
            var previewGroups = this.combatState.GetPartyCooldownGroupsPreview();
            var previewFood = this.config.ShowPartyFoodCheck ? this.combatState.GetPartyFoodStatusesPreview() : [];
            var previewLookups = CreatePartyInfoLookupMaps(previewGroups, previewFood);
            this.nativeMitigationCooldownCache.Clear();
            this.cachedPartyInfoSnapshot = new PartyInfoRenderSnapshot(true, this.config.ShowPartyFoodCheck, this.config.ShowPartyLimitBreakBar, previewGroups, previewFood, previewLookups);
            this.nextPartyInfoRefreshAt = now + PartyInfoCacheDuration;
            return this.cachedPartyInfoSnapshot;
        }

        var showMitigations = IsMitigationCooldownsVisible() && this.combatState.IsPartyCooldownTrackingActive;
        var showFoodCheck = this.config.ShowPartyFoodCheck && this.combatState.IsPartyStatusTrackingActive;
        var showLimitBreak = this.config.ShowPartyLimitBreakBar;
        var groups = showMitigations ? this.combatState.GetPartyCooldownGroups(this.config) : [];
        var foodStatuses = showFoodCheck ? this.combatState.GetPartyFoodStatuses(this.config) : [];
        var lookups = CreatePartyInfoLookupMaps(groups, foodStatuses);

        this.nativeMitigationCooldownCache.Clear();
        this.cachedPartyInfoSnapshot = new PartyInfoRenderSnapshot(showMitigations, showFoodCheck, showLimitBreak, groups, foodStatuses, lookups);
        this.nextPartyInfoRefreshAt = now + PartyInfoCacheDuration;
        return this.cachedPartyInfoSnapshot;
    }

    private static PartyInfoLookupMaps CreatePartyInfoLookupMaps(IReadOnlyList<PartyCooldownGroupEntry> groups, IReadOnlyList<PartyFoodStatusEntry> foodStatuses) {
        var groupsByEntityId = new Dictionary<uint, PartyCooldownGroupEntry>();
        var groupsByObjectId = new Dictionary<ulong, PartyCooldownGroupEntry>();
        var groupsByPartySlot = new Dictionary<int, PartyCooldownGroupEntry>();
        foreach (var group in groups) {
            if (group.SourceEntityId != 0) {
                groupsByEntityId.TryAdd(group.SourceEntityId, group);
            }

            if (group.SourceObjectId != 0) {
                groupsByObjectId.TryAdd(group.SourceObjectId, group);
            }

            if (group.PartySlot >= 0) {
                groupsByPartySlot.TryAdd(group.PartySlot, group);
            }
        }

        var foodByEntityId = new Dictionary<uint, PartyFoodStatusEntry>();
        var foodByObjectId = new Dictionary<ulong, PartyFoodStatusEntry>();
        var foodByPartySlot = new Dictionary<int, PartyFoodStatusEntry>();
        foreach (var status in foodStatuses) {
            if (status.SourceEntityId != 0) {
                foodByEntityId.TryAdd(status.SourceEntityId, status);
            }

            if (status.SourceObjectId != 0) {
                foodByObjectId.TryAdd(status.SourceObjectId, status);
            }

            if (status.PartySlot >= 0) {
                foodByPartySlot.TryAdd(status.PartySlot, status);
            }
        }

        return new PartyInfoLookupMaps(groupsByEntityId, groupsByObjectId, groupsByPartySlot, foodByEntityId, foodByObjectId, foodByPartySlot);
    }

    private unsafe void DrawNativeAttachedPartyInfo(IReadOnlyList<PartyCooldownGroupEntry> groups, IReadOnlyList<PartyFoodStatusEntry> foodStatuses, PartyInfoLookupMaps lookups, bool showLimitBreak) {
        if (groups.Count == 0 && foodStatuses.Count == 0 && !showLimitBreak) {
            return;
        }

        var addonPtr = this.gameGui.GetAddonByName("_PartyList");
        if (addonPtr.IsNull) {
            return;
        }

        var addon = (AddonPartyList*)addonPtr.Address;
        if (!IsNativePartyListVisible(addon)) {
            return;
        }

        var memberCount = Math.Clamp(addon->MemberCount, 0, 8);
        if (memberCount <= 0) {
            return;
        }

        var partyArray = PartyListNumberArray.Instance();
        if (partyArray is null) {
            return;
        }

        var drawList = ImGui.GetBackgroundDrawList();
        const float iconSize = 36.0f;
        const float iconGap = 5.0f;
        const float attachGap = 16.0f;

        var anchorSnapshot = GetNativePartyAnchorSnapshot(addon, memberCount, iconSize);
        var anchors = anchorSnapshot.Anchors;
        var fallbackJobIconLeft = anchorSnapshot.FallbackJobIconLeft;
        var fallbackJobIconHeight = anchorSnapshot.FallbackJobIconHeight;

        if (showLimitBreak) {
            DrawNativeAttachedLimitBreakBar(drawList, anchors.Where(anchor => anchor is not null).Select(anchor => anchor!.Value).ToList());
        }

        for (var nativeIndex = 0; nativeIndex < memberCount; nativeIndex++) {
            ref var nativeMember = ref addon->PartyMembers[nativeIndex];
            ref var nativeData = ref partyArray->PartyMembers[nativeIndex];
            // 掉线/不在场的成员原生队伍栏读不到血量（显示"??级"），MaxHealth 为 0。
            // 用原生数据统一判定，避免依赖缓存导致"有的掉线隐藏、有的不隐藏"。
            if (nativeData.MaxHealth <= 0) {
                continue;
            }

            var entityId = nativeData.EntityId;
            var hasGroup = lookups.GroupsByEntityId.TryGetValue(entityId, out var group)
                           || lookups.GroupsByObjectId.TryGetValue(entityId, out group)
                           || lookups.GroupsByPartySlot.TryGetValue(nativeIndex, out group);
            var hasFood = lookups.FoodByEntityId.TryGetValue(entityId, out var foodStatus)
                          || lookups.FoodByObjectId.TryGetValue(entityId, out foodStatus)
                          || lookups.FoodByPartySlot.TryGetValue(nativeIndex, out foodStatus);
            if (!hasGroup && !hasFood) {
                continue;
            }

            if (anchors[nativeIndex] is not { } anchor) {
                continue;
            }

            var rowMin = anchor.RowMin;
            var rowH = anchor.RowH;
            if (!anchor.HasJobIconAnchor && fallbackJobIconLeft is { } jobIconLeft) {
                var rowCenterY = rowMin.Y + rowH * 0.5f;
                rowMin = new Vector2(jobIconLeft, rowCenterY - fallbackJobIconHeight * 0.5f);
                rowH = fallbackJobIconHeight;
            }

            var cooldowns = hasGroup && group is not null
                ? GetNativeAttachedMitigationCooldowns(group.Cooldowns)
                : [];
            var iconCount = cooldowns.Count + (hasFood && foodStatus is not null ? 1 : 0);
            if (iconCount == 0) {
                continue;
            }

            var iconGroupW = iconCount * iconSize + Math.Max(0, iconCount - 1) * iconGap;
            var x = rowMin.X - attachGap - iconGroupW;
            var y = rowMin.Y + (rowH - iconSize) * 0.5f;
            for (var i = 0; i < cooldowns.Count; i++) {
                DrawNativeAttachedCooldownIcon(drawList, new Vector2(x + i * (iconSize + iconGap), y), cooldowns[i], iconSize);
            }

            if (hasFood && foodStatus is not null) {
                DrawNativeAttachedFoodIcon(drawList, new Vector2(x + cooldowns.Count * (iconSize + iconGap), y), foodStatus, iconSize);
            }
        }
    }

    private unsafe NativePartyAnchorSnapshot GetNativePartyAnchorSnapshot(AddonPartyList* addon, int memberCount, float fallbackIconSize) {
        var now = DateTime.UtcNow;
        if (this.cachedNativePartyAnchorSnapshot is not null
            && now < this.nextNativePartyAnchorRefreshAt
            && this.cachedNativePartyAnchorSnapshot.Anchors.Length == memberCount) {
            return this.cachedNativePartyAnchorSnapshot;
        }

        var anchors = new NativePartyMemberAnchor?[memberCount];
        var jobIconLefts = new List<float>(memberCount);
        var jobIconHeightTotal = 0.0f;
        var jobIconAnchorCount = 0;
        for (var nativeIndex = 0; nativeIndex < memberCount; nativeIndex++) {
            ref var nativeMember = ref addon->PartyMembers[nativeIndex];
            if (!TryGetNativePartyMemberAnchor(nativeMember, out var rowMin, out var rowRight, out var rowH, out var hasJobIconAnchor)) {
                continue;
            }

            var anchor = new NativePartyMemberAnchor(rowMin, rowRight, rowH, hasJobIconAnchor);
            anchors[nativeIndex] = anchor;
            if (hasJobIconAnchor) {
                jobIconLefts.Add(anchor.RowMin.X);
                jobIconHeightTotal += anchor.RowH;
                jobIconAnchorCount++;
            }
        }

        float? fallbackJobIconLeft = null;
        var fallbackJobIconHeight = fallbackIconSize;
        if (jobIconAnchorCount > 0) {
            jobIconLefts.Sort();
            fallbackJobIconLeft = jobIconLefts[jobIconLefts.Count / 2];
            fallbackJobIconHeight = jobIconHeightTotal / jobIconAnchorCount;
        }

        this.cachedNativePartyAnchorSnapshot = new NativePartyAnchorSnapshot(anchors, fallbackJobIconLeft, fallbackJobIconHeight);
        this.nextNativePartyAnchorRefreshAt = now + NativePartyAnchorCacheDuration;
        return this.cachedNativePartyAnchorSnapshot;
    }

    private static unsafe bool IsNativePartyListVisible(AddonPartyList* addon) {
        if (addon is null || !addon->IsVisible || addon->Alpha == 0) {
            return false;
        }

        return addon->RootNode is not null && addon->RootNode->IsVisible();
    }

    private void DrawNativeAttachedLimitBreakBar(ImDrawListPtr drawList, IReadOnlyList<NativePartyMemberAnchor> anchors) {
        if (anchors.Count == 0 || !TryReadLimitBreakGauge(out var gauge)) {
            return;
        }

        var partyLeft = anchors.Min(anchor => anchor.RowMin.X);
        var partyRight = anchors.Max(anchor => anchor.RowRight);
        var partyTop = anchors.Min(anchor => anchor.RowMin.Y);
        var partyBottom = anchors.Max(anchor => anchor.RowMin.Y + anchor.RowH);
        var partyWidth = Math.Max(1.0f, partyRight - partyLeft);
        var barWidth = Math.Clamp(partyWidth, 250.0f, 400.0f);
        const float barHeight = 18.0f;
        const float topAttachGap = 20.0f;
        const float bottomAttachGap = 20.0f;
        var x = partyLeft + (partyWidth - barWidth) * 0.5f + 6.0f;
        var y = this.config.PartyLimitBreakBarPosition == 0
            ? partyTop - barHeight - topAttachGap
            : partyBottom + bottomAttachGap;

        DrawLimitBreakGauge(drawList, new Vector2(x, y), barWidth, barHeight, gauge);
    }

    private unsafe bool TryReadLimitBreakGauge(out LimitBreakGaugeSnapshot gauge) {
        gauge = default;
        var controller = LimitBreakController.Instance();
        if (controller is null) {
            return false;
        }

        var segments = Math.Clamp((int)controller->BarCount, 1, 3);
        var unitsPerBar = controller->BarUnits;
        var max = unitsPerBar * segments;
        if (unitsPerBar <= 0 || max <= 0) {
            return false;
        }

        var current = Math.Clamp((float)controller->CurrentUnits, 0.0f, max);
        gauge = new LimitBreakGaugeSnapshot(current, max, segments);
        return true;
    }

    private static unsafe float ReadGaugeBarVisualValue(AtkComponentGaugeBar* gaugeBar, int segmentMax) {
        if (gaugeBar is null || segmentMax <= 0) {
            return 0.0f;
        }

        var fillNode = gaugeBar->PrimaryFill.MainFillNode;
        if (fillNode is null || !fillNode->IsVisible()) {
            return 0.0f;
        }

        var fillWidth = Math.Max(0.0f, fillNode->Width * Math.Max(0.0f, fillNode->ScaleX));
        var maxWidth = Math.Max(0.0f, gaugeBar->MaxFillPositionX);
        if (maxWidth <= 1.0f && gaugeBar->BackdropImageNode is not null) {
            maxWidth = Math.Max(0.0f, gaugeBar->BackdropImageNode->Width * Math.Max(0.0f, gaugeBar->BackdropImageNode->ScaleX));
        }

        if (maxWidth <= 1.0f) {
            return 0.0f;
        }

        return segmentMax * Math.Clamp(fillWidth / maxWidth, 0.0f, 1.0f);
    }

    private static void DrawLimitBreakGauge(ImDrawListPtr drawList, Vector2 pos, float width, float height, LimitBreakGaugeSnapshot gauge) {
        const ImDrawFlags roundCornersAll = (ImDrawFlags)240;
        const ImDrawFlags roundCornersLeft = (ImDrawFlags)80;

        var min = pos;
        var max = pos + new Vector2(width, height);
        var rounding = height * 0.48f;
        var progress = gauge.Max > 0.0f ? Math.Clamp(gauge.Current / gauge.Max, 0.0f, 1.0f) : 0.0f;
        var full = progress >= 0.995f;
        var segmentCount = Math.Clamp(gauge.Segments, 1, 3);

        var trimGold = ImGui.GetColorU32(full ? new Vector4(1.0f, 0.86f, 0.34f, 1.0f) : new Vector4(0.86f, 0.64f, 0.28f, 0.96f));
        var trimShadow = ImGui.GetColorU32(new Vector4(0.36f, 0.24f, 0.08f, 0.78f));
        var backing = ImGui.GetColorU32(new Vector4(0.025f, 0.030f, 0.055f, 0.58f));
        var backingVignette = ImGui.GetColorU32(new Vector4(0.07f, 0.075f, 0.12f, 0.42f));
        var activeFillColor = full ? new Vector4(1.0f, 0.58f, 0.14f, 0.96f) : new Vector4(0.25f, 0.55f, 1.0f, 0.94f);
        var activeCoreColor = full ? new Vector4(1.0f, 0.86f, 0.34f, 0.74f) : new Vector4(0.52f, 0.92f, 1.0f, 0.70f);
        var completedFillColor = new Vector4(1.0f, 0.58f, 0.14f, 0.96f);
        var completedCoreColor = new Vector4(1.0f, 0.86f, 0.34f, 0.74f);
        var energyFill = ImGui.GetColorU32(activeFillColor);
        var energyCore = ImGui.GetColorU32(activeCoreColor);
        var completedFill = ImGui.GetColorU32(completedFillColor);
        var completedCore = ImGui.GetColorU32(completedCoreColor);

        var innerMin = min + new Vector2(3.0f, 3.0f);
        var innerMax = max - new Vector2(3.0f, 3.0f);
        var innerWidth = Math.Max(1.0f, innerMax.X - innerMin.X);
        var innerHeight = Math.Max(1.0f, innerMax.Y - innerMin.Y);
        var innerRounding = Math.Max(1.0f, rounding - 3.0f);
        var fillRight = innerMin.X + innerWidth * progress;
        var unitsPerSegment = gauge.Max / segmentCount;
        var completedSegments = unitsPerSegment > 0.0f
            ? Math.Clamp((int)MathF.Floor(gauge.Current / unitsPerSegment + 0.0001f), 0, segmentCount)
            : 0;
        var completedRight = innerMin.X + innerWidth * completedSegments / segmentCount;

        // A light, translucent housing replaces the previous heavy black block while keeping contrast over the game UI.
        drawList.AddRectFilled(min + new Vector2(0.0f, 2.0f), max + new Vector2(0.0f, 3.0f), ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.24f)), rounding);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.20f, 0.145f, 0.055f, 0.68f)), rounding);
        drawList.AddRect(min + new Vector2(1.0f, 1.0f), max - new Vector2(1.0f, 1.0f), trimShadow, rounding - 1.0f, (ImDrawFlags)0, 1.0f);
        drawList.AddRectFilled(innerMin, innerMax, backing, innerRounding);
        drawList.AddRectFilled(innerMin + new Vector2(0.0f, 1.0f), innerMax - new Vector2(0.0f, innerHeight * 0.45f), backingVignette, innerRounding);

        if (progress > 0.001f) {
            var fillMax = new Vector2(fillRight, innerMax.Y);
            var activeRounding = innerRounding;

            drawList.AddRectFilled(innerMin, fillMax, energyFill, activeRounding, roundCornersAll);
            drawList.AddRectFilled(innerMin + new Vector2(1.0f, 2.0f), fillMax - new Vector2(1.0f, innerHeight * 0.46f), energyCore, Math.Max(1.0f, innerRounding - 1.0f), roundCornersAll);

            if (completedRight > innerMin.X + 1.0f) {
                var completedMax = new Vector2(Math.Min(completedRight, fillRight), innerMax.Y);
                var completedCoversWholeFill = completedMax.X >= fillRight - 0.5f;
                var completedFlags = completedCoversWholeFill ? roundCornersAll : roundCornersLeft;
                drawList.AddRectFilled(innerMin, completedMax, completedFill, activeRounding, completedFlags);
                drawList.AddRectFilled(innerMin + new Vector2(1.0f, 2.0f), completedMax - new Vector2(1.0f, innerHeight * 0.46f), completedCore, Math.Max(1.0f, innerRounding - 1.0f), completedFlags);

                if (!full && fillRight > completedRight + 1.0f) {
                    var blendWidth = Math.Min(22.0f, fillRight - completedRight);
                    var blendMin = new Vector2(Math.Max(innerMin.X, completedRight - 6.0f), innerMin.Y);
                    var blendMax = new Vector2(Math.Min(fillRight, completedRight + blendWidth), innerMax.Y);
                    var blendCoreMin = blendMin + new Vector2(0.0f, 2.0f);
                    var blendCoreMax = blendMax - new Vector2(0.0f, innerHeight * 0.46f);
                    drawList.AddRectFilledMultiColor(blendMin, blendMax, completedFill, energyFill, energyFill, completedFill);
                    drawList.AddRectFilledMultiColor(blendCoreMin, blendCoreMax, completedCore, energyCore, energyCore, completedCore);
                }
            }

            drawList.AddLine(new Vector2(innerMin.X + 4.0f, innerMin.Y + 2.0f), new Vector2(Math.Max(innerMin.X + 4.0f, fillRight - 4.0f), innerMin.Y + 2.0f), ImGui.GetColorU32(new Vector4(1.0f, 0.95f, 0.72f, 0.42f)), 1.0f);
        }

        // Segment markers are small inlaid gold glints instead of opaque dividers, so the bar reads as one continuous reservoir.
        for (var index = 1; index < segmentCount; index++) {
            var splitX = MathF.Round(innerMin.X + innerWidth * index / segmentCount);
            var markerTop = innerMin.Y + 1.0f;
            var markerBottom = innerMax.Y - 1.0f;
            var markerCenterY = min.Y + height * 0.5f;
            var darkLine = ImGui.GetColorU32(new Vector4(0.18f, 0.08f, 0.02f, 0.58f));
            var goldLine = ImGui.GetColorU32(new Vector4(1.0f, 0.86f, 0.34f, 0.72f));
            var markerCore = ImGui.GetColorU32(new Vector4(1.0f, 0.96f, 0.62f, 0.80f));

            drawList.AddLine(
                new Vector2(splitX - 1.0f, markerTop),
                new Vector2(splitX - 1.0f, markerBottom),
                darkLine,
                1.0f);
            drawList.AddLine(
                new Vector2(splitX, markerTop),
                new Vector2(splitX, markerBottom),
                goldLine,
                1.5f);
            drawList.AddLine(
                new Vector2(splitX + 1.0f, markerTop + 2.0f),
                new Vector2(splitX + 1.0f, markerBottom - 2.0f),
                markerCore,
                1.0f);

            var diamondHalf = Math.Max(2.0f, height * 0.14f);
            drawList.AddQuadFilled(
                new Vector2(splitX, markerCenterY - diamondHalf),
                new Vector2(splitX + diamondHalf, markerCenterY),
                new Vector2(splitX, markerCenterY + diamondHalf),
                new Vector2(splitX - diamondHalf, markerCenterY),
                ImGui.GetColorU32(new Vector4(1.0f, 0.78f, 0.22f, 0.62f)));
        }

        drawList.AddRect(innerMin, innerMax, ImGui.GetColorU32(new Vector4(0.82f, 0.93f, 1.0f, 0.18f)), innerRounding, (ImDrawFlags)0, 1.0f);
        drawList.AddRect(min, max, trimGold, rounding, (ImDrawFlags)0, full ? 2.0f : 1.45f);
        if (full) {
            drawList.AddRect(min - new Vector2(2.0f, 2.0f), max + new Vector2(2.0f, 2.0f), ImGui.GetColorU32(new Vector4(1.0f, 0.66f, 0.16f, 0.26f)), rounding + 2.0f, (ImDrawFlags)0, 3.0f);
        }
    }

    private static unsafe bool TryGetNativePartyMemberAnchor(
        AddonPartyList.PartyListMemberStruct nativeMember,
        out Vector2 rowMin,
        out float rowRight,
        out float rowH,
        out bool hasJobIconAnchor) {
        rowMin = Vector2.Zero;
        rowRight = 0.0f;
        rowH = 0.0f;
        hasJobIconAnchor = false;

        var hasBounds = false;
        var boundsMin = Vector2.Zero;
        var boundsRight = 0.0f;
        var boundsH = 0.0f;
        var rowVisualCenterY = 0.0f;
        var rowVisualCenterWeight = 0;

        void IncludeNode(float x, float y, float width, float height, float scaleX, float scaleY) {
            var nodeW = Math.Max(1.0f, width * Math.Max(0.1f, scaleX));
            var nodeH = Math.Max(1.0f, height * Math.Max(0.1f, scaleY));
            if (!hasBounds) {
                boundsMin = new Vector2(x, y);
                boundsRight = x + nodeW;
                boundsH = nodeH;
                hasBounds = true;
                return;
            }

            boundsMin = new Vector2(Math.Min(boundsMin.X, x), Math.Min(boundsMin.Y, y));
            boundsRight = Math.Max(boundsRight, x + nodeW);
            boundsH = Math.Max(boundsH, y + nodeH - boundsMin.Y);
        }

        void IncludeRowCenter(float y, float height) {
            rowVisualCenterY += y + height * 0.5f;
            rowVisualCenterWeight++;
        }

        if (nativeMember.PartyMemberComponent is not null
            && nativeMember.PartyMemberComponent->AtkResNode is not null
            && nativeMember.PartyMemberComponent->AtkResNode->IsVisible()) {
            var component = nativeMember.PartyMemberComponent->AtkResNode;
            IncludeNode(component->ScreenX, component->ScreenY, component->Width, component->Height, component->ScaleX, component->ScaleY);
        }

        var jobIconLeft = 0.0f;
        var jobIconTop = 0.0f;
        var jobIconHeight = 0.0f;
        if (nativeMember.ClassJobIcon is not null && nativeMember.ClassJobIcon->IsVisible()) {
            var icon = nativeMember.ClassJobIcon;
            jobIconLeft = icon->ScreenX;
            jobIconTop = icon->ScreenY;
            jobIconHeight = Math.Max(1.0f, icon->Height * Math.Max(0.1f, icon->ScaleY));
            hasJobIconAnchor = true;
            IncludeNode(icon->ScreenX, icon->ScreenY, icon->Width, icon->Height, icon->ScaleX, icon->ScaleY);
        }

        if (nativeMember.Name is not null && nativeMember.Name->IsVisible()) {
            var name = nativeMember.Name;
            IncludeNode(name->ScreenX, name->ScreenY, name->Width, name->Height, name->ScaleX, name->ScaleY);
        }

        if (nativeMember.HPGaugeBar is not null && nativeMember.HPGaugeBar->AtkResNode is not null && nativeMember.HPGaugeBar->AtkResNode->IsVisible()) {
            var hp = nativeMember.HPGaugeBar->AtkResNode;
            var hpH = Math.Max(1.0f, hp->Height * Math.Max(0.1f, hp->ScaleY));
            IncludeNode(hp->ScreenX, hp->ScreenY, hp->Width, hp->Height, hp->ScaleX, hp->ScaleY);
            IncludeRowCenter(hp->ScreenY, hpH);
        }

        if (nativeMember.MPGaugeBar is not null && nativeMember.MPGaugeBar->AtkResNode is not null && nativeMember.MPGaugeBar->AtkResNode->IsVisible()) {
            var mp = nativeMember.MPGaugeBar->AtkResNode;
            var mpH = Math.Max(1.0f, mp->Height * Math.Max(0.1f, mp->ScaleY));
            IncludeNode(mp->ScreenX, mp->ScreenY, mp->Width, mp->Height, mp->ScaleX, mp->ScaleY);
            IncludeRowCenter(mp->ScreenY, mpH);
        }

        if (!hasBounds || boundsRight <= boundsMin.X || boundsH <= 1.0f) {
            return false;
        }

        var rowCenterY = rowVisualCenterWeight > 0
            ? rowVisualCenterY / rowVisualCenterWeight
            : hasJobIconAnchor
                ? jobIconTop + jobIconHeight * 0.5f
                : boundsMin.Y + boundsH * 0.5f;

        rowMin = hasJobIconAnchor ? new Vector2(jobIconLeft, rowCenterY - jobIconHeight * 0.5f) : boundsMin;
        rowRight = boundsRight;
        rowH = hasJobIconAnchor ? jobIconHeight : boundsH;
        return true;
    }

    private IReadOnlyList<CooldownEntry> GetNativeAttachedMitigationCooldowns(IReadOnlyList<CooldownEntry> cooldowns) {
        if (this.nativeMitigationCooldownCache.TryGetValue(cooldowns, out var cachedCooldowns)) {
            return cachedCooldowns;
        }

        cachedCooldowns = cooldowns
            .Where(cooldown => NormalizeCooldownGroup(cooldown.Group) is CooldownGroup.PartyMitigation or CooldownGroup.Mitigation
                               || TrackedActionCatalog.PartyInfoExtraMitigationActionIds.Contains(cooldown.ActionId))
            .OrderBy(cooldown => cooldown.IsReady)
            .ThenBy(cooldown => cooldown.RemainingCooldownSeconds)
            .ThenBy(cooldown => cooldown.Name, StringComparer.Ordinal)
            .ToList();
        this.nativeMitigationCooldownCache[cooldowns] = cachedCooldowns;
        return cachedCooldowns;
    }

    private void DrawNativeAttachedCooldownIcon(ImDrawListPtr drawList, Vector2 pos, CooldownEntry cooldown, float size) {
        var min = pos;
        var max = pos + new Vector2(size, size);
        DrawGameIconImage(drawList, cooldown.IconId, min, max);

        if (!cooldown.IsReady) {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.34f)), 3.0f);
            var remainingSeconds = cooldown.RemainingCooldownSeconds;
            if (ShouldDrawCooldownIconTime(remainingSeconds, cooldown.CooldownSeconds)) {
                DrawCenteredIconText(drawList, FormatCooldownIconTime(remainingSeconds), min, max, ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.96f)), size, true);
            }
        }

        // 贴合边框：线条向内缩半个线宽，完全落在图标范围内不外扩。
        var thickness = Math.Max(1.0f, size * 0.05f);
        var inset = thickness * 0.5f;
        drawList.AddRect(min + new Vector2(inset), max - new Vector2(inset), ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.85f)), 3.0f, (ImDrawFlags)0, thickness + 1.0f);
        drawList.AddRect(min + new Vector2(inset), max - new Vector2(inset), ImGui.GetColorU32(new Vector4(0.78f, 0.82f, 0.90f, 0.95f)), 3.0f, (ImDrawFlags)0, thickness);
    }

    private static bool ShouldDrawCooldownIconTime(float seconds, float expectedMaxSeconds) {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0.05f) {
            return false;
        }

        var maxSeconds = Math.Clamp(expectedMaxSeconds + 30.0f, 30.0f, MaxCooldownIconTimeSeconds);
        return seconds <= maxSeconds;
    }

    private void DrawNativeAttachedFoodIcon(ImDrawListPtr drawList, Vector2 pos, PartyFoodStatusEntry foodStatus, float size) {
        var min = pos;
        var max = pos + new Vector2(size, size);
        var rounding = 4.0f;
        DrawGameIconImage(drawList, foodStatus.IconId, min, max, true);

        if (!foodStatus.HasFood) {
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.18f, 0.03f, 0.06f, 0.24f)), rounding);
            drawList.AddRect(min + new Vector2(0.5f), max - new Vector2(0.5f), ImGui.GetColorU32(new Vector4(1.0f, 0.50f, 0.58f, 0.72f)), rounding, (ImDrawFlags)0, 1.0f);
            DrawCenteredIconText(drawList, "!", min, max, ImGui.GetColorU32(new Vector4(1.0f, 0.94f, 0.96f, 1.0f)), size * 1.08f);
        } else if (foodStatus.RemainingSeconds > 0.05f) {
            var text = FormatCooldownIconTime(foodStatus.RemainingSeconds);
            DrawBottomIconTextPill(drawList, text, min, max, ImGui.GetColorU32(new Vector4(0.82f, 1.0f, 0.76f, 0.98f)), size);
        }
    }

    private void DrawBottomIconTextPill(ImDrawListPtr drawList, string text, Vector2 min, Vector2 max, uint color, float iconSize) {
        if (string.IsNullOrWhiteSpace(text)) {
            return;
        }

        var baseFontSize = ImGui.GetFontSize();
        var fontSize = Math.Clamp(iconSize * 0.30f, baseFontSize * 0.64f, baseFontSize * 0.86f);
        var fontHandle = GetHudFont(GetNearestHudFontSize(fontSize));
        if (!fontHandle.Available) {
            return;
        }

        using var font = fontHandle.Push();
        var textSize = ImGui.CalcTextSize(text);
        var padding = new Vector2(3.0f, 1.0f);
        var pillSize = textSize + padding * 2.0f;
        var pillMin = SnapToPixel(new Vector2(min.X + Math.Max(1.0f, (max.X - min.X - pillSize.X) * 0.5f), max.Y - pillSize.Y - 1.0f));
        var pillMax = SnapToPixel(pillMin + pillSize);
        drawList.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(new Vector4(0.03f, 0.05f, 0.03f, 0.62f)), Math.Max(2.0f, iconSize * 0.11f));
        drawList.AddText(SnapToPixel(pillMin + padding + new Vector2(1.0f, 1.0f)), ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.82f)), text);
        drawList.AddText(SnapToPixel(pillMin + padding), color, text);
    }
}
