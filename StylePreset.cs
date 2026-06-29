using System.IO.Compression;
using System.Numerics;
using Newtonsoft.Json;

namespace AllHud;

public class StylePreset {
    [JsonProperty("name")] public string Name { get; set; } = "";
    [JsonProperty("ver")] public int Version { get; set; }
    [JsonProperty("col")] public Dictionary<string, Vector4> Colors { get; set; } = new();
    [JsonProperty("dol")] public Dictionary<string, Vector4>? CustomColors { get; set; }

    [JsonProperty("c")] public float WindowRounding { get; set; } = 8.0f;
    [JsonProperty("g")] public float ChildRounding { get; set; } = 8.0f;
    [JsonProperty("k")] public float FrameRounding { get; set; } = 4.0f;
    [JsonProperty("l")] public float FrameBorderSize { get; set; } = 1.0f;
    [JsonProperty("h")] public float ChildBorderSize { get; set; } = 1.0f;
    [JsonProperty("s")] public float ScrollbarSize { get; set; } = 10.0f;
    [JsonProperty("t")] public float ScrollbarRounding { get; set; } = 4.0f;
    [JsonProperty("u")] public float GrabMinSize { get; set; } = 10.0f;
    [JsonProperty("v")] public float GrabRounding { get; set; } = 4.0f;
    [JsonProperty("w")] public float TabRounding { get; set; } = 6.0f;

    public Dictionary<string, float> ToStyleVars() => new() {
        ["WindowRounding"] = WindowRounding,
        ["ChildRounding"] = ChildRounding,
        ["FrameRounding"] = FrameRounding,
        ["FrameBorderSize"] = FrameBorderSize,
        ["ChildBorderSize"] = ChildBorderSize,
        ["ScrollbarSize"] = ScrollbarSize,
        ["ScrollbarRounding"] = ScrollbarRounding,
        ["GrabMinSize"] = GrabMinSize,
        ["GrabRounding"] = GrabRounding,
        ["TabRounding"] = TabRounding,
    };

    public static StylePreset? Decode(string encoded) {
        try {
            var b64 = encoded.Trim();
            if (b64.StartsWith("DS1")) b64 = b64[3..];
            var bytes = Convert.FromBase64String(b64);
            using var ms = new MemoryStream(bytes);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var sr = new StreamReader(gz);
            var json = sr.ReadToEnd();
            return JsonConvert.DeserializeObject<StylePreset>(json);
        } catch {
            return null;
        }
    }

    // 内置预设主题，改编自 ImGui 社区分享（issue #707）
    public static readonly StylePreset[] BuiltInPresets = [
        CreateDeepBlue(),
        CreatePureBlack(),
        CreateSoftLight(),
        CreateOfficialDark(),
    ];

    // 深蓝 — 改编自 codz01 主题
    static StylePreset CreateDeepBlue() => new() {
        Name = "深蓝 (Deep Blue)",
        WindowRounding = 5.3f, ChildRounding = 5.3f, FrameRounding = 2.3f, FrameBorderSize = 1.0f,
        ChildBorderSize = 1.0f, ScrollbarSize = 10.0f, ScrollbarRounding = 0.0f,
        GrabMinSize = 10.0f, GrabRounding = 0.0f, TabRounding = 4.0f,
        Colors = new() {
            ["Text"] = new(0.90f, 0.90f, 0.90f, 0.90f),
            ["TextDisabled"] = new(0.60f, 0.60f, 0.60f, 1.00f),
            ["WindowBg"] = new(0.09f, 0.09f, 0.15f, 1.00f),
            ["ChildBg"] = new(0.12f, 0.12f, 0.18f, 1.00f),
            ["PopupBg"] = new(0.05f, 0.05f, 0.10f, 0.85f),
            ["Border"] = new(0.70f, 0.70f, 0.70f, 0.65f),
            ["FrameBg"] = new(0.00f, 0.00f, 0.01f, 1.00f),
            ["FrameBgHovered"] = new(0.90f, 0.80f, 0.80f, 0.40f),
            ["FrameBgActive"] = new(0.90f, 0.65f, 0.65f, 0.45f),
            ["TitleBg"] = new(0.00f, 0.00f, 0.00f, 0.83f),
            ["TitleBgActive"] = new(0.00f, 0.00f, 0.00f, 0.87f),
            ["MenuBarBg"] = new(0.01f, 0.01f, 0.02f, 0.80f),
            ["ScrollbarBg"] = new(0.20f, 0.25f, 0.30f, 0.60f),
            ["ScrollbarGrab"] = new(0.55f, 0.53f, 0.55f, 0.51f),
            ["ScrollbarGrabHovered"] = new(0.56f, 0.56f, 0.56f, 1.00f),
            ["ScrollbarGrabActive"] = new(0.56f, 0.56f, 0.56f, 0.91f),
            ["SliderGrab"] = new(0.70f, 0.70f, 0.70f, 0.62f),
            ["SliderGrabActive"] = new(0.30f, 0.30f, 0.30f, 0.84f),
            ["Button"] = new(0.48f, 0.72f, 0.89f, 0.49f),
            ["ButtonHovered"] = new(0.50f, 0.69f, 0.99f, 0.68f),
            ["ButtonActive"] = new(0.80f, 0.50f, 0.50f, 1.00f),
            ["Header"] = new(0.30f, 0.69f, 1.00f, 0.53f),
            ["HeaderHovered"] = new(0.44f, 0.61f, 0.86f, 1.00f),
            ["HeaderActive"] = new(0.38f, 0.62f, 0.83f, 1.00f),
            ["Separator"] = new(0.50f, 0.50f, 0.50f, 1.00f),
            ["Tab"] = new(0.20f, 0.20f, 0.30f, 1.00f),
            ["TabHovered"] = new(0.44f, 0.61f, 0.86f, 1.00f),
            ["TabActive"] = new(0.30f, 0.69f, 1.00f, 0.53f),
        },
    };

    // 纯黑 — 改编自 f4ruq 主题
    static StylePreset CreatePureBlack() => new() {
        Name = "纯黑 (Pure Black)",
        WindowRounding = 6.0f, ChildRounding = 6.0f, FrameRounding = 4.0f, FrameBorderSize = 0.0f,
        ChildBorderSize = 1.0f, ScrollbarSize = 12.0f, ScrollbarRounding = 6.0f,
        GrabMinSize = 10.0f, GrabRounding = 4.0f, TabRounding = 4.0f,
        Colors = new() {
            ["Text"] = new(0.90f, 0.90f, 0.90f, 1.00f),
            ["TextDisabled"] = new(0.50f, 0.50f, 0.50f, 1.00f),
            ["WindowBg"] = new(0.00f, 0.00f, 0.00f, 1.00f),
            ["ChildBg"] = new(0.06f, 0.06f, 0.06f, 1.00f),
            ["PopupBg"] = new(0.02f, 0.02f, 0.02f, 0.94f),
            ["Border"] = new(0.20f, 0.20f, 0.20f, 0.50f),
            ["FrameBg"] = new(0.10f, 0.125f, 0.15f, 1.00f),
            ["FrameBgHovered"] = new(0.30f, 0.35f, 0.40f, 1.00f),
            ["FrameBgActive"] = new(0.40f, 0.45f, 0.50f, 1.00f),
            ["TitleBg"] = new(0.00f, 0.00f, 0.00f, 0.83f),
            ["TitleBgActive"] = new(0.00f, 0.00f, 0.00f, 0.87f),
            ["MenuBarBg"] = new(0.05f, 0.05f, 0.05f, 0.80f),
            ["ScrollbarBg"] = new(0.02f, 0.02f, 0.02f, 0.40f),
            ["ScrollbarGrab"] = new(0.31f, 0.31f, 0.31f, 0.80f),
            ["ScrollbarGrabHovered"] = new(0.41f, 0.41f, 0.41f, 0.90f),
            ["ScrollbarGrabActive"] = new(0.51f, 0.51f, 0.51f, 1.00f),
            ["SliderGrab"] = new(0.41f, 0.41f, 0.51f, 0.80f),
            ["SliderGrabActive"] = new(0.51f, 0.51f, 0.61f, 1.00f),
            ["Button"] = new(0.10f, 0.125f, 0.15f, 1.00f),
            ["ButtonHovered"] = new(0.30f, 0.35f, 0.40f, 1.00f),
            ["ButtonActive"] = new(0.40f, 0.45f, 0.50f, 1.00f),
            ["Header"] = new(0.18f, 0.18f, 0.22f, 1.00f),
            ["HeaderHovered"] = new(0.26f, 0.26f, 0.31f, 1.00f),
            ["HeaderActive"] = new(0.34f, 0.34f, 0.40f, 1.00f),
            ["Separator"] = new(0.28f, 0.28f, 0.34f, 0.60f),
            ["Tab"] = new(0.16f, 0.16f, 0.19f, 1.00f),
            ["TabHovered"] = new(0.26f, 0.26f, 0.31f, 1.00f),
            ["TabActive"] = new(0.30f, 0.30f, 0.36f, 1.00f),
        },
    };

    // 浅色 — 改编自 dougbinks 主题
    static StylePreset CreateSoftLight() => new() {
        Name = "浅色 (Soft Light)",
        WindowRounding = 6.0f, ChildRounding = 6.0f, FrameRounding = 3.0f, FrameBorderSize = 1.0f,
        ChildBorderSize = 1.0f, ScrollbarSize = 10.0f, ScrollbarRounding = 4.0f,
        GrabMinSize = 10.0f, GrabRounding = 4.0f, TabRounding = 4.0f,
        Colors = new() {
            ["Text"] = new(0.00f, 0.00f, 0.00f, 0.88f),
            ["TextDisabled"] = new(0.60f, 0.60f, 0.60f, 1.00f),
            ["WindowBg"] = new(0.86f, 0.86f, 0.86f, 1.00f),
            ["ChildBg"] = new(0.92f, 0.92f, 0.92f, 1.00f),
            ["PopupBg"] = new(0.96f, 0.96f, 0.96f, 0.97f),
            ["Border"] = new(0.50f, 0.50f, 0.50f, 0.32f),
            ["FrameBg"] = new(0.80f, 0.80f, 0.80f, 0.30f),
            ["FrameBgHovered"] = new(0.60f, 0.60f, 0.60f, 0.40f),
            ["FrameBgActive"] = new(0.50f, 0.50f, 0.50f, 0.60f),
            ["TitleBg"] = new(0.76f, 0.76f, 0.76f, 1.00f),
            ["TitleBgActive"] = new(0.60f, 0.60f, 0.60f, 1.00f),
            ["MenuBarBg"] = new(0.80f, 0.80f, 0.80f, 0.80f),
            ["ScrollbarBg"] = new(0.40f, 0.40f, 0.40f, 0.15f),
            ["ScrollbarGrab"] = new(0.40f, 0.40f, 0.40f, 0.60f),
            ["ScrollbarGrabHovered"] = new(0.40f, 0.40f, 0.40f, 0.90f),
            ["ScrollbarGrabActive"] = new(0.40f, 0.40f, 0.40f, 1.00f),
            ["SliderGrab"] = new(0.35f, 0.35f, 0.35f, 0.70f),
            ["SliderGrabActive"] = new(0.20f, 0.20f, 0.20f, 0.80f),
            ["Button"] = new(0.60f, 0.60f, 0.60f, 0.25f),
            ["ButtonHovered"] = new(0.50f, 0.50f, 0.50f, 0.40f),
            ["ButtonActive"] = new(0.50f, 0.50f, 0.50f, 0.60f),
            ["Header"] = new(0.55f, 0.55f, 0.55f, 0.45f),
            ["HeaderHovered"] = new(0.50f, 0.50f, 0.50f, 0.55f),
            ["HeaderActive"] = new(0.50f, 0.50f, 0.50f, 0.70f),
            ["Separator"] = new(0.50f, 0.50f, 0.50f, 0.25f),
            ["Tab"] = new(0.72f, 0.72f, 0.72f, 0.70f),
            ["TabHovered"] = new(0.60f, 0.60f, 0.60f, 0.80f),
            ["TabActive"] = new(0.65f, 0.65f, 0.65f, 0.85f),
        },
    };

    // 官方深色 — 基于 ImGui::StyleColorsDark
    static StylePreset CreateOfficialDark() => new() {
        Name = "官方深色 (Official Dark)",
        WindowRounding = 7.0f, ChildRounding = 7.0f, FrameRounding = 4.0f, FrameBorderSize = 1.0f,
        ChildBorderSize = 1.0f, ScrollbarSize = 11.0f, ScrollbarRounding = 5.0f,
        GrabMinSize = 10.0f, GrabRounding = 4.0f, TabRounding = 5.0f,
        Colors = new() {
            ["Text"] = new(1.00f, 1.00f, 1.00f, 1.00f),
            ["TextDisabled"] = new(0.50f, 0.50f, 0.50f, 1.00f),
            ["WindowBg"] = new(0.06f, 0.06f, 0.06f, 1.00f),
            ["ChildBg"] = new(0.10f, 0.10f, 0.10f, 1.00f),
            ["PopupBg"] = new(0.08f, 0.08f, 0.08f, 0.94f),
            ["Border"] = new(0.43f, 0.43f, 0.43f, 0.50f),
            ["FrameBg"] = new(0.16f, 0.16f, 0.16f, 1.00f),
            ["FrameBgHovered"] = new(0.27f, 0.27f, 0.27f, 1.00f),
            ["FrameBgActive"] = new(0.39f, 0.39f, 0.39f, 1.00f),
            ["TitleBg"] = new(0.04f, 0.04f, 0.04f, 1.00f),
            ["TitleBgActive"] = new(0.16f, 0.16f, 0.16f, 1.00f),
            ["MenuBarBg"] = new(0.14f, 0.14f, 0.14f, 1.00f),
            ["ScrollbarBg"] = new(0.02f, 0.02f, 0.02f, 0.53f),
            ["ScrollbarGrab"] = new(0.31f, 0.31f, 0.31f, 1.00f),
            ["ScrollbarGrabHovered"] = new(0.41f, 0.41f, 0.41f, 1.00f),
            ["ScrollbarGrabActive"] = new(0.51f, 0.51f, 0.51f, 1.00f),
            ["SliderGrab"] = new(0.24f, 0.52f, 0.88f, 1.00f),
            ["SliderGrabActive"] = new(0.26f, 0.59f, 0.98f, 1.00f),
            ["Button"] = new(0.35f, 0.35f, 0.35f, 0.40f),
            ["ButtonHovered"] = new(0.35f, 0.35f, 0.35f, 0.60f),
            ["ButtonActive"] = new(0.46f, 0.46f, 0.46f, 1.00f),
            ["Header"] = new(0.35f, 0.35f, 0.35f, 0.45f),
            ["HeaderHovered"] = new(0.35f, 0.35f, 0.35f, 0.80f),
            ["HeaderActive"] = new(0.46f, 0.46f, 0.46f, 0.80f),
            ["Separator"] = new(0.28f, 0.28f, 0.28f, 0.62f),
            ["Tab"] = new(0.18f, 0.18f, 0.18f, 0.86f),
            ["TabHovered"] = new(0.39f, 0.39f, 0.39f, 0.80f),
            ["TabActive"] = new(0.28f, 0.28f, 0.28f, 1.00f),
        },
    };
}
