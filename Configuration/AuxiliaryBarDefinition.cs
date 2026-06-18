using System.Numerics;

namespace AllHud;

public sealed class AuxiliaryBarDefinition {
    public bool Enabled { get; set; }
    public string Name { get; set; } = "辅助栏";
    public int PositionMode { get; set; }
    public bool StretchToEdges { get; set; }
    public int LayoutDirection { get; set; }
    public float VerticalOffset { get; set; } = 0.5f;
    public Vector2 CustomPosition { get; set; } = new(120.0f, 240.0f);
    public float Scale { get; set; } = 1.0f;
    public float Opacity { get; set; } = 1.0f;
    public List<string> ComponentOrder { get; set; } = [];
    public List<string> SectionStartComponentOrder { get; set; } = [];
    public List<string> SectionCenterComponentOrder { get; set; } = [];
    public List<string> SectionEndComponentOrder { get; set; } = [];
}
