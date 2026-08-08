using System.Numerics;
using System.Text.Json.Serialization;

namespace AllHud;

public enum QCShortcutMode {
    Normal,
    Incremental,
    Random,
}

public enum QCShortcutType {
    Command,
    Category,
    Spacer,
}

public sealed class QCShortcutDefinition {
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "快捷方式";
    public uint IconId { get; set; }
    public string Command { get; set; } = string.Empty;
    public string Tooltip { get; set; } = string.Empty;
    public QCShortcutMode Mode { get; set; } = QCShortcutMode.Normal;
    public QCShortcutType Type { get; set; } = QCShortcutType.Command;
    public bool IsCategory { get; set; }
    public List<string> ChildShortcutIds { get; set; } = [];

    [JsonIgnore]
    public int IncrementalIndex { get; set; }

    // Per-shortcut hotkey
    public int Hotkey { get; set; } // Windows VK code, 0 = none
    public bool KeyPassthrough { get; set; } // Pass key through to game even when hotkey is set

    // Color customization
    public uint Color { get; set; } = 0xFFFFFFFF; // ABGR format

    // Icon customization
    public float IconZoom { get; set; } = 1.0f;
    public float IconRotation { get; set; } // Rotation in radians
    public Vector2 IconOffset { get; set; } = Vector2.Zero;

    // Cooldown display
    public uint CooldownActionId { get; set; }
    public int CooldownStyle { get; set; } // 0=icon overlay, 1=text only

    // Category settings
    public int CategoryWidth { get; set; } = 140;
    public int CategoryColumns { get; set; } = 1;
    public bool CategoryOnHover { get; set; }
    public bool CategoryStaysOpen { get; set; }
    public bool CategoryHoverClose { get; set; }
    public Vector2 CategorySpacing { get; set; } = new(8.0f, 4.0f);
    public float CategoryScale { get; set; } = 1.0f;
    public float CategoryFontScale { get; set; } = 1.0f;
    public bool CategoryNoBackground { get; set; }
}