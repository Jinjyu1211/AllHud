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

    private static StylePreset[]? _builtInPresetsCache;
    public static StylePreset[] BuiltInPresets => _builtInPresetsCache ??= LoadAllBuiltInPresets();

    private static readonly string[] EmbeddedStyleCodes = [
        // 暗金香草主题
        "DS1H4sIAAAAAAAACqVX25LaOBD9lZSeXZRky9e3zLCb2aoklcqQnV3eBGiMF2MTY5hcin9PyVLrZpgdMrxgyX1Oq49arfZPxFBBJjhAC1T8RP+gIhODf4f/U4CWqCChmFmhIhL/XJlhZYYn8SlAj6ggASrBeK04K5hgC4X4T8GpgkeDlw3Y1cps6y2GDmaNB5azrTcbDrO70TrF7FdUhMPKOlSQRDzswXWPCjJEeICZo+J8golviu675o4tDX6c9ciYmo6ciJdtLeZn/FuvYXlI4iRLIoXO4iwLo5gGaC6GaTT84gA9DOqeggE9rfZsUfPVeEkKZwMeqmbVPt2U2phEaRjTnCgMkT7ApXmrGW7XVb16DcGndnfYWQQ4A7EyBRIPD4Me9BSgm7Zb8c5EJzVKFIiGURzmNFTQMBo0zC1/En+/Zqv2abxB4FJ5FIA/O7bldoRY74MMEZM8wWmWQIzmvXaqOO7aI++srbnklhrI22VfHc0Zo2FKY5yDRhElJE0oKBXSdHBup0TV1/w1+6MIvHWMaWiWEpBdvBUyjGlu27pmu72lwdVMH3hzuGGdHZPeCh2HLf79smvreuFAnttwbf+uE2UKlJe7DKfJDOfucOxW0Pg7T2Oai7wBNj2cu8PzbH5SXEt2u+bLzQfWbTSFyF5ZZACWOd7rasVdPXT+64LkZLxBeItNUoUTD3N4MMfz0Pct1HU8SSEvxIMse8Q6JNLaF5eEuTgX4MgM5+7Qc/r7Rw1P0kSM81OA7jhz6lMOMuUgk6xGeBIRbe5HcL5m40mmEb6qICpoam8F37GO9e0riqamGC30t5m8AK4m+sz31Q/+rqvMxS5OII2I5pBbFAGHPKL0LIcfVzLkiM5VsRZC9N7Ll8lZJn9jriSaWUcshJtfPCgdXNNXZ/5sXEz8tMd5FGv/NMNZLtbvZf2MLb40j+3y4JR2WXh0adfDuTu0V6NZRsuChDiXH2aouabtclM15aeOHytubvo8wyKXIEfMcO4OfZ4/trv+u3WBwEJ0htotTd3276uG700WgAY6eiduDXD3c2hMZbiwAU67J2B31b5vy45Bg4wnUHBS52o7g7jkLDkDnImeUlYe++IFX+JhJMOAUe1W37WNwUUgh3gYx2UB31flun/uQIxwn51+9rl73pi/rXtPheF/rv4lIlEN9j2v+bLndtN7fVJ1rJx27W7GupJf8m1WK07YR3a8q8p17QqS+LeLOLjazUd2lB1+1ZQ++HKkqYecVlsrVigMUMOgFoSiNWtXrJa4l4Gi+CQ+49iWowL9zZqqrtmbKes2b778hQK0kh9FbJSirkAyWFM3U+8E2AVv+SKrl3098f+RUlo9GhVSrUOqlRBP2rYcRZpi2Fmbcz1K8OSM52pkBV5V22/Zwne48Cj6GvqM0puLCWtfrrVh9KXOE1tGU7rAKT1D15iqQySff06kHXz9m6BpJCV0xbHaBvFelhWsV+iI83W0LTGGymVzmg4r0w011ZyJ8xljLodMX7awPQO7sjwF6Cj6SXz6BVCG1wGbEQAA",
        // 赛博卫月主题
        "DS1H4sIAAAAAAAACq1YzXLbNhB+F541HoD4JW+x3caHOOOJ3XHNGy3REmNaVChKdpLRuS+Qh+j0xdrH6CyIBUBStuVMdMEutfthF/uDJb9HeZTSIzKJbqP0e/RnlGpgbsy6m0TTKOXwYBalBNbCShnmBlaxm0R3UUon0dw+XljE0vL5rSU+W2WDeBOlzGxxbx9UVuphYAg3UsuBbve0HjyNzdPVyEh4+iVKY2NXE6VUArGO0gTWNkopA2JjbJpEW4v4aNcni/XVAYvA+297t8tz+5j1vM2nVnBaVyBwVTy1VtAYd2PXzK7XZt1NjOBpuc5vq2I2NsMomNUpXJfLWf14PPfWSTRPWnEgroFI2G4SnSzKahbKozhKW2EAv6hXm1Uoq1FYo7RGbL6bRMd1MysaJx4nhCaSWh3HZT3OaAtOqZLCQVwu8ln9eJCNvzf5QxHYGCduP7eV28XLn9XbognPmOMhczxlbtUCrXfTttz66pCoJFFJopJUEM2yrULbSBzLmHPG0B/Dxz5SnjcgWhCptOIeamAAZ0lMhbJwjst6XAfFFRFaMw91UldVvloHZ/BTaOfFcnOcN70IvD3sl9OmrqrbHs5LUXfy7xtoPFaD4aZAZEi4WukpDROAoy4QncvP6g6iIFAViC53QtWTRTG9P8+be6egsUaByJDwe1XlrOh79mx6DjWGGYrRBKLLUBXoHW/atsamC/9BVGPsN57N+qwJAxNxwmnsUEYVRTmXCXeGOzbrs125ADITwqENE11zKmON3lBClBIJ+uRZ24sESTSBsjkr8rAjCWwOQHR2YHNg1ImPPNnbf8mRdhrDU8cDxJMLQ1Ws8iZva28Ux3YARG8DEcoPrWISihAdIkpzRplCGwPeQCkdayI4DxEHVr8NMDjkT8W6/Fa8b0p/JSuEASJDwijGoqcydEvhHQNEp6nDnuo1f5X5V0GdxRgLILqOxTDaspMdZYdmihIXREaY4kRhnnu2C0NnZIc0zHGVSKYS9J8KzVnsCsazY6A+lnf1dBO2ciIdDKFJkgBqB8M1IYSgU4mK+QBjYNXPdPPTenpfLucXTbEti8dfAfXbw6r9Gt4xCILqQYVdVHX7oVwWa1+PuKfbzu40UOiHFie1oDCZGKidleu2njc5jrPkCLoadZeJjhOhnZGUqDjRLgXBY6KHSOOCoElCsAfROGaEAkbWZ22VmyS3Ia2KrjsF50axPoAYnZzRsRNY29TL+Us3q9iv+KGcL9qX6mmk96k/v75w63vxd9Vr8zTUgB2oL4uqmLZFOPXGcngRQIXYew3Srsnnp029usqbefHcVt44qMOP+fasnC+qvv/P7dP5/zHfdgN8uZwPlZ93TA00T8uHwDUsfKx49Atu6vN6lled3mFKTOzgtSx/KKI0+u+vf/798Xc0iWbdWw2++0CaK+6mIGSykLHzLIekB/t9y9WxZFK6AcezWZ8dIkwPeqHydSQZ49zVkeOyHuf0ioPQ77wX7t4yVGe7vbk62fmorSiCSRFiLnzlS821VphCns36rFMtR1Xk2j4zv0D28+v1I5RWSkMzuX+2AsIptPKW46CGE1oiw8MNuiV22D1wfi6lVO0tPNr7POC95izZEyw/nUj4v2tLxFnYO50vo2AJgikdYvopTruhnjtMKcIE8PeRdpc8xsegO0nfP3B3ucefzeg25C6fg1fX7RuSwt/V2Ldlv90TA/l0YCr7Lykwd3V4aCJOIICH31Y83r44u28tJq2622gPmJvmXrMOPtIcYB4cIbxCkN3/5VO/Z0cTAAA=",
        // 默认卫月主题
        "DS1H4sIAAAAAAAACqVYy27bRhT9FYOrFhCEeT+0i+02XsSxEbtI6x1N0RJrSlQoSnYS6AP6df2lYt4zpJRGsjfU0Peeuc8zd/g9y7MJHINR9phNvmd/ZhMI1OqvbCLGYDfKimxC1YupFSutGBhTLQXGdDfKnrIJHGUzKzu3spVd54/2xd9WmdktsN7i2YrVVmrhtzBSREst7Vti3zL9tum9NbKrHgLQb79on0ZZm02gll/bjbtsApH6sckm+rm1/3ixz1dr2de9zn/bu1ue29c4cTYvrGDR1ErgvnztvL4AWCLILIpQC8pH2YNaScoEQ5yOss/amt1I615W6/yxLqceg1EgBSAWg3FBMCQWQy8wJxHG52o5bV7OZ8EHSSDkzDkYlhoBEg0fW3Exr+rpaQAmUrfNarN6iwXnTTstW6/PARUCuQhwL2/CKBlQYbD7UwAlA8KD3M3zafNyEpSy5Pc2X5SRK1BlkAluAaDX0ACI6T8RuWIBrppt2UZJRcYQFxKUZBVTJAlEQ5R3RVdtQ8Nib74Gwb6gNApx1RbKq+rqxBnIGeHAlWdYmrz44u0D9MwAxmsXk7A0MNpRTMkQ56Kp63y1jsJyPNR1udyc5+1bUnRXtE1dPyYgvvGtGohqwsu/bxURuowmbYoY4oRCl1AiVVnGGU1A+tWhmxpRl1iTZlcddFAdCVYvOZAjSiRALho+qQ+JmVHzl8Xzdd4+h5bRZQF9y8S9KzV6bEpdTcs0MCfq9/w4CuV803WNO1/AGMFEGSmfgRQuOSq4WOKBfj8rOGVzbQJxtUlV61A5ZCEN1XOGUAakxB5JAgEgQhaKcSQFjSv0qsxjPoQUYia4r3C9dO4gRS0Y8oH6gIKOjYqB6XMQ4ohB7CiIMMilWhoKIpRIzmKQu3KVt3nXBG8CbRmIhISIJ+whQt+h4wrNofT8kYBwQhyIoFAQ6hIjOaQsNuVTua6+le/banUqDQSEQbVRSDhRZWDCwrCQRDhTbGD3Q/VcophzhB0QJUJgT6kMA4xhOoTEpHZsjdwPyQxJIQWivkQgFxz5mBKIMEIorvb7QfNjzrCEHoIAwgVWh6dpPQIBQ6Jnxh/Lp6bYxIfLKc54lH5MCYIC+zxzCDj3eZaIU8yUfR7qsimeq+Xsti23VRnGEeQoQDGH0TWRAGMetH5brLqv0eHkyNzFMK6C27rpPlTLch3mR+ew+mGSDvcppHnT47GJtutmTHtqV9W6a2ZtHuZ7ngwUSfHLiCD3QPSrRhCGiR8EuGoGpkjNYjEoFH3GqapLQ1BvGTw1jB0cu7ZZzo71LKb+COxDNZt3JzCemv71yEMd3qd0tv/BmBLE39VdL6v6+WCfRoPZ+8ddWZdFV8YXgB9UKVaGXbb57LJtVvd5OysPbRWMk7tR9jHfXlWzeZ3E5eA+Jjkf86253FTLWV/5sGO8p3lZLSLXHCs6FnB+ITVaNtO8Nno/p4TpTt1r80WZTbKLvFutNkVRLc+um2Ken/1y8+8/N2c3bTWrlmeL6vXXbJRNzZ3R3SzVpdAOd5ardYV6sra1EMUkULUuQiEO3BPNEByfXUXgsrS0kwHRn6leL7SorXRnqzIcupAwc4eJN4xmOT2hSxfFtJfCDcyoPQU1PderA9hygg2VyYHz1yuGfHlQ42DSvESzvpq9vOI8YvmYPtKQ7rm+Vwfu7EJN4FD6UdFc2mNN9x0FjCXWfz6NLp79Of35kIYwd4egqDMRR7Q+ja4Xpx+ky5MD05yai9VpTn45tWjaA4q9MdS1jNcL53RwwsbG3XVNfQ9u893gFGAJ9RmpzWBqIM4Yc36CMdnpD2RODElMkZ+iIRcEqeIyuYWAM54cmWGY8bFK5wWgN3gdzBUcOJaP7Q1f5HwkfPTchxmF577RBbz0mDFo/psdGLtRwk8zEZifeP/POvWx7yfMUwFV9zWw+w9KzaUojxUAAA==",
    ];

    private static readonly string[] EmbeddedStyleNames = ["暗金香草 (Vanilla Saffron)", "赛博卫月 (Cyber Moon)", "默认卫月 (Default Moon)"];

    private static StylePreset[] LoadAllBuiltInPresets() {
        var list = new List<StylePreset> {
            CreateDeepBlue(),
            CreatePureBlack(),
            CreateSoftLight(),
            CreateOfficialDark(),
        };
        for (var i = 0; i < EmbeddedStyleCodes.Length; i++) {
            var preset = Decode(EmbeddedStyleCodes[i]);
            if (preset != null && preset.Colors.Count > 0) {
                preset.Name = EmbeddedStyleNames[i];
                list.Add(preset);
            }
        }
        return list.ToArray();
    }

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
