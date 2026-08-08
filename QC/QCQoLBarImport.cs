using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AllHud;

/// <summary>
/// QoLBar 导出格式兼容导入。
/// 支持解析 QoLBar 的 GZip+Base64 编码 JSON 格式，映射到 QC 数据模型。
/// </summary>
public static class QCQoLBarImport {
    private const string QcExportPrefix = "=== QC Bar Export v1 ===";

    // --- QoLBar 数据模型（用于反序列化） ---

    #pragma warning disable CS0649 // Fields are only set via JSON deserialization

    // ReSharper disable InconsistentNaming
    // ReSharper disable UnusedMember.Local
    // ReSharper disable NotAccessedField.Local

    private sealed class QoLBarCfg {
        [JsonProperty("n")] public string? Name;
        [JsonProperty("k")] public int Hotkey;
        [JsonProperty("sL")] public List<QoLShCfg>? ShortcutList;
        [JsonProperty("h")] public bool Hidden;
        [JsonProperty("d")] public int DockSide = 4; // default: Undocked
        [JsonProperty("a")] public int Alignment = 1; // default: Center
        [JsonProperty("v")] public int Visibility = 2; // default: Always
        [JsonProperty("ht")] public bool Hint;
        [JsonProperty("bW")] public int ButtonWidth = 100;
        [JsonProperty("cT")] public bool ClickThrough;
        [JsonProperty("p")] public float[]? Position;
        [JsonProperty("l")] public bool LockedPosition;
        [JsonProperty("co")] public int Columns;
        [JsonProperty("s")] public float Scale = 1.0f;
        [JsonProperty("rA")] public float RevealAreaScale = 1.0f;
        [JsonProperty("fS")] public float FontScale = 1.0f;
        [JsonProperty("sp")] public int[]? Spacing;
        [JsonProperty("nB")] public bool NoBackground;
        [JsonProperty("c")] public int ConditionSet = -1; // -1 = none
    }

    private sealed class QoLShCfg {
        [JsonProperty("n")] public string? Name;
        [JsonProperty("t")] public int Type; // 0=Command, 1=Category, 2=Spacer
        [JsonProperty("c")] public string? Command;
        [JsonProperty("k")] public int Hotkey;
        [JsonProperty("kP")] public bool KeyPassthrough;
        [JsonProperty("sL")] public List<QoLShCfg>? SubList;
        [JsonProperty("m")] public int Mode; // 0=Default, 1=Incremental, 2=Random
        [JsonProperty("cl")] public uint Color = 0xFFFFFFFF;
        [JsonProperty("iZ")] public float IconZoom = 1.0f;
        [JsonProperty("iO")] public float[]? IconOffset;
        [JsonProperty("iR")] public float IconRotation;
        [JsonProperty("cdA")] public uint CooldownAction;
        [JsonProperty("cdS")] public int CooldownStyle;
        [JsonProperty("cW")] public int CategoryWidth = 140;
        [JsonProperty("cSO")] public bool CategoryStaysOpen;
        [JsonProperty("cC")] public int CategoryColumns = 1;
        [JsonProperty("cSp")] public int[]? CategorySpacing;
        [JsonProperty("cS")] public float CategoryScale = 1.0f;
        [JsonProperty("cF")] public float CategoryFontScale = 1.0f;
        [JsonProperty("cNB")] public bool CategoryNoBackground;
        [JsonProperty("cH")] public bool CategoryOnHover;
        [JsonProperty("cHC")] public bool CategoryHoverClose;
    }

    private sealed class QoLCndSetCfg {
        [JsonProperty("n")] public string? Name;
        [JsonProperty("c")] public List<QoLCndCfg>? Conditions;
    }

    private sealed class QoLCndCfg {
        [JsonProperty("i")] public string? Id; // condition type ID string
        [JsonProperty("a")] public object? Arg; // dynamic arg
        [JsonProperty("n")] public bool Negate;
        [JsonProperty("o")] public int Operator; // 0=AND, 1=OR, 2=EQUALS, 3=XOR
    }

    private sealed class QoLExportInfo {
        [JsonProperty("b1")] public QoLBarCfg? Bar1;
        [JsonProperty("b2")] public QoLBarCfg? Bar2;
        [JsonProperty("s1")] public QoLShCfg? Shortcut1;
        [JsonProperty("s2")] public QoLShCfg? Shortcut2;
        [JsonProperty("cs")] public QoLCndSetCfg? ConditionSet;
        [JsonProperty("v")] public string? Version;
    }

    private sealed class QoLImportInfo {
        [JsonProperty("bar")] public QoLBarCfg? Bar;
        [JsonProperty("shortcut")] public QoLShCfg? Shortcut;
        [JsonProperty("conditionSet")] public QoLCndSetCfg? ConditionSet;
    }

    // ReSharper restore InconsistentNaming
    // ReSharper restore UnusedMember.Local
    // ReSharper restore NotAccessedField.Local

    #pragma warning restore CS0649

    // --- 主入口 ---

    /// <summary>
    /// 尝试从文本中导入 QoLBar 格式。
    /// 自动检测是 QC 格式还是 QoLBar 格式。
    /// </summary>
    public static bool TryImportFromText(QCManager manager, string text) {
        // 跳过 QC 自己的格式
        if (text.StartsWith(QcExportPrefix)) return false;

        // 尝试 QoLBar 格式：Base64(GZip(JSON))
        try {
            return ImportQoLBar(manager, text);
        } catch {
            // 不是有效的 QoLBar 格式
        }

        // 尝试纯 JSON 格式（可能是未压缩的 QoLBar 导出）
        try {
            return ImportQoLBarJson(manager, text);
        } catch {
            // 不是有效的 JSON 格式
        }

        return false;
    }

    /// <summary>
    /// 从 Base64 编码的 QoLBar 导出数据导入
    /// </summary>
    public static bool ImportQoLBar(QCManager manager, string base64Data) {
        if (string.IsNullOrWhiteSpace(base64Data)) return false;

        // 清理字符串（移除可能的空白和换行）
        var clean = base64Data.Trim();
        if (clean.Length < 10) return false;

        // 解码 Base64 → GZip → JSON
        var json = DecompressBase64(clean);
        if (json is null) return false;

        return ImportQoLBarJson(manager, json);
    }

    /// <summary>
    /// 从 JSON 字符串导入 QoLBar 数据
    /// </summary>
    private static bool ImportQoLBarJson(QCManager manager, string json) {
        if (string.IsNullOrWhiteSpace(json)) return false;

        var obj = JObject.Parse(json);

        // 尝试检测是 ExportInfo 还是 ImportInfo 还是直接数据
        var hasB1 = obj["b1"] is JObject;
        var hasB2 = obj["b2"] is JObject;
        var hasS1 = obj["s1"] is JObject;
        var hasS2 = obj["s2"] is JObject;
        var hasCs = obj["cs"] is JObject;
        var hasBar = obj["bar"] is JObject;
        var hasShortcut = obj["shortcut"] is JObject;
        var hasConditionSet = obj["conditionSet"] is JObject;

        // 判断是否有 ExportInfo 特征
        var isExportInfo = hasB1 || hasB2 || hasS1 || hasS2 || hasCs;
        // 判断是否有 ImportInfo 特征
        var isImportInfo = hasBar || hasShortcut || hasConditionSet;

        if (isExportInfo) {
            // 解析 ExportInfo
            var export = obj.ToObject<QoLExportInfo>();
            if (export is null) return false;

            var imported = false;

            // 优先使用新版 (b2), 回退到旧版 (b1)
            if (export.Bar2 is not null) {
                ImportBar(manager, export.Bar2);
                imported = true;
            } else if (export.Bar1 is not null) {
                ImportBar(manager, export.Bar1);
                imported = true;
            }

            // 优先使用新版 (s2), 回退到旧版 (s1)
            if (export.Shortcut2 is not null) {
                ImportShortcut(manager, export.Shortcut2);
                imported = true;
            } else if (export.Shortcut1 is not null) {
                ImportShortcut(manager, export.Shortcut1);
                imported = true;
            }

            if (export.ConditionSet is not null) {
                ImportConditionSet(manager, export.ConditionSet);
                imported = true;
            }

            return imported;
        }

        if (isImportInfo) {
            // 解析 ImportInfo
            var import = obj.ToObject<QoLImportInfo>();
            if (import is null) return false;

            var imported = false;
            if (import.Bar is not null) { ImportBar(manager, import.Bar); imported = true; }
            if (import.Shortcut is not null) { ImportShortcut(manager, import.Shortcut); imported = true; }
            if (import.ConditionSet is not null) { ImportConditionSet(manager, import.ConditionSet); imported = true; }
            return imported;
        }

        // 尝试直接解析为 BarCfg
        if (obj["sL"] is JObject || obj["d"] is JToken) {
            var bar = obj.ToObject<QoLBarCfg>();
            if (bar is not null) { ImportBar(manager, bar); return true; }
        }

        // 尝试直接解析为 ShCfg
        if (obj["t"] is JToken || obj["c"] is JToken) {
            var sh = obj.ToObject<QoLShCfg>();
            if (sh is not null) { ImportShortcut(manager, sh); return true; }
        }

        // 尝试直接解析为 CndSetCfg
        if (obj["c"] is JArray) {
            var cs = obj.ToObject<QoLCndSetCfg>();
            if (cs is not null) { ImportConditionSet(manager, cs); return true; }
        }

        return false;
    }

    // --- 映射方法 ---

    private static void ImportBar(QCManager manager, QoLBarCfg qolBar) {
        var bar = manager.AddBar(qolBar.Name ?? "导入的快捷栏");
        bar.Enabled = !qolBar.Hidden;
        bar.Horizontal = qolBar.DockSide is 1 or 3; // Right/Left = vertical, Top/Bottom = horizontal
        bar.PositionMode = qolBar.DockSide switch {
            0 => 3, // Top
            1 => 1, // Right
            2 => 4, // Bottom
            3 => 0, // Left
            _ => 2, // Custom / Undocked
        };
        bar.DockSide = qolBar.DockSide;
        bar.Alignment = qolBar.Alignment;
        bar.VisibilityMode = qolBar.Visibility;
        bar.Hint = qolBar.Hint;
        bar.ButtonWidth = qolBar.ButtonWidth;
        bar.ClickThrough = qolBar.ClickThrough;
        bar.LockedPosition = qolBar.LockedPosition;
        bar.Columns = qolBar.Columns;
        bar.Scale = qolBar.Scale;
        bar.RevealAreaScale = qolBar.RevealAreaScale;
        bar.FontScale = qolBar.FontScale;
        bar.NoBackground = qolBar.NoBackground;

        if (qolBar.Position is { Length: >= 2 }) {
            bar.CustomPosition = new System.Numerics.Vector2(qolBar.Position[0], qolBar.Position[1]);
        }

        if (qolBar.Spacing is { Length: >= 2 }) {
            bar.Spacing = new System.Numerics.Vector2(qolBar.Spacing[0], qolBar.Spacing[1]);
        }

        // 导入快捷方式列表
        if (qolBar.ShortcutList is { Count: > 0 }) {
            foreach (var qolSh in qolBar.ShortcutList) {
                var shortcut = ImportShortcutData(manager, qolSh);
                if (shortcut is not null) {
                    bar.ShortcutIds.Add(shortcut.Id);
                }
            }
        }

        // 条件集索引映射（导入时略过，由用户手动绑定）
        // QoLBar 使用索引引用条件集，QC 使用 ID 引用，需要手动绑定
    }

    private static void ImportShortcut(QCManager manager, QoLShCfg qolSh) {
        ImportShortcutData(manager, qolSh);
    }

    private static QCShortcutDefinition? ImportShortcutData(QCManager manager, QoLShCfg qolSh) {
        if (string.IsNullOrWhiteSpace(qolSh.Name)) return null;

        var shortcut = manager.AddShortcut(qolSh.Name.Trim());
        shortcut.Type = (QCShortcutType)qolSh.Type;
        shortcut.IsCategory = qolSh.Type == 1;
        shortcut.Command = qolSh.Command ?? string.Empty;
        shortcut.Hotkey = qolSh.Hotkey;
        shortcut.KeyPassthrough = qolSh.KeyPassthrough;
        shortcut.Mode = qolSh.Mode switch {
            0 => QCShortcutMode.Normal,
            1 => QCShortcutMode.Incremental,
            2 => QCShortcutMode.Random,
            _ => QCShortcutMode.Normal,
        };
        shortcut.Color = qolSh.Color;
        shortcut.IconZoom = qolSh.IconZoom;
        shortcut.IconRotation = qolSh.IconRotation;
        shortcut.CooldownActionId = qolSh.CooldownAction;
        shortcut.CooldownStyle = qolSh.CooldownStyle;

        if (qolSh.IconOffset is { Length: >= 2 }) {
            shortcut.IconOffset = new System.Numerics.Vector2(qolSh.IconOffset[0], qolSh.IconOffset[1]);
        }

        // Category settings
        shortcut.CategoryWidth = qolSh.CategoryWidth;
        shortcut.CategoryColumns = qolSh.CategoryColumns;
        shortcut.CategoryOnHover = qolSh.CategoryOnHover;
        shortcut.CategoryStaysOpen = qolSh.CategoryStaysOpen;
        shortcut.CategoryHoverClose = qolSh.CategoryHoverClose;
        shortcut.CategoryScale = qolSh.CategoryScale;
        shortcut.CategoryFontScale = qolSh.CategoryFontScale;
        shortcut.CategoryNoBackground = qolSh.CategoryNoBackground;

        if (qolSh.CategorySpacing is { Length: >= 2 }) {
            shortcut.CategorySpacing = new System.Numerics.Vector2(qolSh.CategorySpacing[0], qolSh.CategorySpacing[1]);
        }

        // 导入子快捷方式（分类菜单）
        if (qolSh.SubList is { Count: > 0 }) {
            foreach (var qolSub in qolSh.SubList) {
                var sub = ImportShortcutData(manager, qolSub);
                if (sub is not null) {
                    shortcut.ChildShortcutIds.Add(sub.Id);
                }
            }
        }

        return shortcut;
    }

    private static void ImportConditionSet(QCManager manager, QoLCndSetCfg qolCs) {
        if (string.IsNullOrWhiteSpace(qolCs.Name)) return;

        var cs = manager.AddConditionSet(qolCs.Name.Trim());

        if (qolCs.Conditions is null) return;

        foreach (var qolCnd in qolCs.Conditions) {
            if (string.IsNullOrWhiteSpace(qolCnd.Id)) continue;

            var entry = MapCondition(qolCnd);
            if (entry.ConditionType != QCConditionType.None) {
                cs.Conditions.Add(entry);
            }
        }
    }

    private static QCConditionEntry MapCondition(QoLCndCfg qolCnd) {
        var entry = new QCConditionEntry {
            Negate = qolCnd.Negate,
            Operator = (QCBinaryOperator)Math.Clamp(qolCnd.Operator, 0, 3),
        };

        var arg = qolCnd.Arg;

        switch (qolCnd.Id) {
            case "cf": // ConditionFlag
                MapConditionFlag(GetIntArg(arg), entry);
                break;
            case "j": // ClassJob
            case "c": // ClassJob (legacy)
                entry.ConditionType = QCConditionType.ClassJobId;
                AddIntArg(arg, entry);
                break;
            case "z": // Zone/Territory
            case "t": // Territory (legacy)
                entry.ConditionType = QCConditionType.TerritoryId;
                AddIntArg(arg, entry);
                break;
            case "cs": // ConditionSet reference
                entry.ConditionType = QCConditionType.ConditionSet;
                AddIntArg(arg, entry);
                break;
            case "r": // Role
            case "l": // Level
            case "wd": // WeaponDrawn (already handled by cf)
            case "et": // EnmityTarget
            case "lt": // LeadTarget
            case "hl": // HPLevel
            case "k": // KeyItem
            case "pt": // PartyType
            case "pe": // PartyCount
            case "ce": // CriticalEngagement
            case "is": // InSession
            case "em": // Emote
            case "ae": // AreaEvent
            case "av": // AuraVariant
            case "p": // Performance
            default:
                // 不支持的条件类型，跳过
                break;
        }

        return entry;
    }

    private static void MapConditionFlag(int flagValue, QCConditionEntry entry) {
        // 映射常见的 ConditionFlag 值到 QCConditionType
        // 这些值来自游戏客户端的 ConditionFlag 枚举
        entry.ConditionType = flagValue switch {
            1 => QCConditionType.InCombat,       // InCombat
            2 => QCConditionType.Mounted,         // Mounted
            4 => QCConditionType.Swimming,        // Swimming
            5 => QCConditionType.Crafting,        // Crafting
            6 => QCConditionType.Gathering,       // Gathering
            8 => QCConditionType.BetweenAreas,    // BetweenAreas
            12 => QCConditionType.BoundByDuty56,  // BoundByDuty56
            17 => QCConditionType.WaitingForDuty, // WaitingForDuty
            27 => QCConditionType.InInstance,     // BoundByDuty
            28 => QCConditionType.WeaponDrawn,    // WeaponDrawn
            33 => QCConditionType.InGpose,        // InGpose
            34 => QCConditionType.NoviceNetwork,  // NoviceNetwork
            38 => QCConditionType.DutyReady,      // DutyFinderQueue
            39 => QCConditionType.DutyReadyConfirm, // DutyFinderConfirm
            _ => QCConditionType.None,
        };
    }

    private static int GetIntArg(object? arg) {
        if (arg is null) return 0;
        if (arg is long l) return (int)l;
        if (arg is int i) return i;
        if (arg is double d) return (int)d;
        if (arg is string s && int.TryParse(s, out var parsed)) return parsed;
        return 0;
    }

    private static void AddIntArg(object? arg, QCConditionEntry entry) {
        var val = GetIntArg(arg);
        if (val > 0) {
            entry.TargetIds.Add((uint)val);
        }
    }

    // --- 压缩/解压工具 ---

    public static string? DecompressBase64(string base64Data) {
        try {
            var bytes = Convert.FromBase64String(base64Data);
            using var ms = new MemoryStream(bytes);
            using var gs = new GZipStream(ms, CompressionMode.Decompress);
            using var reader = new StreamReader(gs, Encoding.UTF8);
            return reader.ReadToEnd();
        } catch {
            return null;
        }
    }

    /// <summary>
    /// 检测字符串是否为 QoLBar 导出格式
    /// </summary>
    public static bool IsQoLBarFormat(string text) {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.StartsWith(QcExportPrefix)) return false;

        // 尝试 Base64 解码 + GZip 检测
        try {
            var bytes = Convert.FromBase64String(text.Trim());
            // GZip 文件头是 0x1F 0x8B
            if (bytes.Length > 2 && bytes[0] == 0x1F && bytes[1] == 0x8B) {
                return true;
            }
            // 也检查纯文本 JSON 特征
            var textStr = Encoding.UTF8.GetString(bytes);
            return textStr.TrimStart().StartsWith('{');
        } catch {
            // 检查是否为纯 JSON
            var trimmed = text.Trim();
            return trimmed.StartsWith('{') || trimmed.StartsWith('[');
        }
    }
}