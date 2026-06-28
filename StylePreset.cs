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
}
