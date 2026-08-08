using System.Numerics;

namespace AllHud;

public sealed class QCBarDefinition {
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "QC 快捷栏";
    public bool Enabled { get; set; } = true;
    public bool Horizontal { get; set; } = true;
    public int PositionMode { get; set; } // 0=left, 1=right, 2=custom, 3=top, 4=bottom
    public Vector2 CustomPosition { get; set; } = new(500.0f, 400.0f);
    public float Scale { get; set; } = 1.0f;
    public float Opacity { get; set; } = 1.0f;
    public List<string> ShortcutIds { get; set; } = [];
    public string? Hotkey { get; set; }
    public string? ConditionSetId { get; set; }
    public bool IsPieMenu { get; set; }
    public float PieRadius { get; set; } = 120.0f;

    // Grid layout
    public int Columns { get; set; } // 0 = single row/column, >0 = grid
    public Vector2 Spacing { get; set; } = new(4.0f, 4.0f);

    // Visibility
    public bool ClickThrough { get; set; }
    public bool LockedPosition { get; set; }
    public bool NoBackground { get; set; }
    public bool HideWhenEmpty { get; set; }

    // QoLBar-style features
    public int DockSide { get; set; } = 4; // 0=top, 1=right, 2=bottom, 3=left, 4=undocked
    public int Alignment { get; set; } = 1; // 0=left/top, 1=center, 2=right/bottom
    public int VisibilityMode { get; set; } = 2; // 0=slide, 1=immediate, 2=always
    public bool Hint { get; set; } // Show hint when hidden
    public float RevealAreaScale { get; set; } = 1.0f;
    public float FontScale { get; set; } = 1.0f;
    public int ButtonWidth { get; set; } = 100; // Percentage of button area
    public bool Editing { get; set; }
}