using System.Numerics;

namespace AllHud;

public sealed class CustomMapMarkerDefinition {
    public string Id { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? SubLabel { get; set; }
    public uint IconId { get; set; }
    public int IconSize { get; set; } = 28;
    public bool UseMapCoordinates { get; set; } = true;

    public Vector3 Position => new(X, Y, Z);
}

/// <summary>
/// 预设的自定义地图标记数据。
/// 坐标来源：NGA玩家社区、新月岛史官(xyd.zzmelon.com)
/// </summary>
public static class CustomMapMarkerDefaults {
    private const uint IconChest = 60561;
    private const uint IconNote = 60561;
    private const uint IconBoss = 60561;
    private const uint IconPot = 60561;

    public static Dictionary<uint, List<CustomMapMarkerDefinition>> GetDefaults() {
        var dict = new Dictionary<uint, List<CustomMapMarkerDefinition>>();

        // 蜃景幻界新月岛北征之章 (MapId: 1346)
        dict[1346] =
        [
            // ===== CE (遭遇战) 标记 =====
            new() { Id = "CE_01", X = 31.4f, Y = 0, Z = 15.2f, Label = "拟态使魔", SubLabel = "变形法师", IconId = IconBoss },
            new() { Id = "CE_02", X = 25.9f, Y = 0, Z = 4.2f, Label = "天道好轮回", SubLabel = "亡灵法师", IconId = IconBoss },
            new() { Id = "CE_03", X = 34.6f, Y = 0, Z = 34.6f, Label = "禁书化形", SubLabel = "古术魔典", IconId = IconBoss },
            new() { Id = "CE_04", X = 36.7f, Y = 0, Z = 21.4f, Label = "暴食咒鬼", SubLabel = "阿尔戈尔", IconId = IconBoss },
            new() { Id = "CE_05", X = 18.4f, Y = 0, Z = 4.2f, Label = "孤岛的绑架犯", SubLabel = "诱拐魔", IconId = IconBoss },
            new() { Id = "CE_06", X = 11.1f, Y = 0, Z = 8.6f, Label = "纯白守护者", SubLabel = "雪石膏之剑", IconId = IconBoss },
            new() { Id = "CE_07", X = 24.5f, Y = 0, Z = 35.7f, Label = "魔法军团", SubLabel = "小小法师", IconId = IconBoss },
            new() { Id = "CE_08", X = 13.6f, Y = 0, Z = 35.4f, Label = "求道的人造人", SubLabel = "神木巨人", IconId = IconBoss },
            new() { Id = "CE_09", X = 19.8f, Y = 0, Z = 31.1f, Label = "苏醒的多头龙", SubLabel = "魔许德拉", IconId = IconBoss },
            new() { Id = "CE_10", X = 26.2f, Y = 0, Z = 28.5f, Label = "叛逆使魔", SubLabel = "负隅宝石兽", IconId = IconBoss },
            new() { Id = "CE_11", X = 37.6f, Y = 0, Z = 10.2f, Label = "诅咒的继承者", SubLabel = "惨白魔人", IconId = IconBoss },
            new() { Id = "CE_12", X = 17.1f, Y = 0, Z = 20.1f, Label = "魔女复制体", SubLabel = "卡洛菲斯提莉二重身", IconId = IconBoss },
            new() { Id = "CE_13", X = 4.0f, Y = 0, Z = 10.2f, Label = "四颚斧花", SubLabel = "提蔛", IconId = IconBoss },
            new() { Id = "CE_14", X = 7.7f, Y = 0, Z = 24.4f, Label = "暗红尸骸", SubLabel = "赤龙", IconId = IconBoss },
            new() { Id = "CE_15", X = 24.8f, Y = 0, Z = 18.6f, Label = "残暴的母蜘蛛", SubLabel = "新月阿剌克涅", IconId = IconBoss },

            // ===== 魔法罐 标记 =====
            new() { Id = "POT_01", X = 11.8f, Y = 0, Z = 32.0f, Label = "瑟瑟发抖的魔法罐", SubLabel = "左下罐", IconId = IconPot },
            new() { Id = "POT_02", X = 26.0f, Y = 0, Z = 17.0f, Label = "幸福的魔法罐", SubLabel = "右上罐", IconId = IconPot },

            // ===== 调查记录 标记 =====
            new() { Id = "NOTE_37", X = 39.6f, Y = 0, Z = 23.2f, Label = "调查笔记 37", IconId = IconNote },
            new() { Id = "NOTE_40", X = 40.2f, Y = 0, Z = 3.5f, Label = "调查笔记 40", IconId = IconNote },
            new() { Id = "NOTE_52", X = 21.3f, Y = 0, Z = 19.7f, Label = "调查笔记 52", IconId = IconNote },
            new() { Id = "NOTE_54", X = 22.7f, Y = 0, Z = 23.9f, Label = "调查笔记 54", SubLabel = "地下", IconId = IconNote },
            new() { Id = "NOTE_63", X = 17.6f, Y = 0, Z = 5.1f, Label = "调查笔记 63", IconId = IconNote },
            new() { Id = "NOTE_YY", X = 11.2f, Y = 0, Z = 38.9f, Label = "调查笔记", SubLabel = "浮游遗迹", IconId = IconNote },
            new() { Id = "NOTE_UN1", X = 4.5f, Y = 0, Z = 36.0f, Label = "调查地点", IconId = IconNote },

            // ===== 宝箱 标记 =====
            new() { Id = "CHEST_01", X = 24.6f, Y = 0, Z = 21.7f, Label = "银箱子", IconId = IconChest },
            new() { Id = "CHEST_02", X = 25.9f, Y = 0, Z = 20.8f, Label = "银箱子", IconId = IconChest },
            new() { Id = "CHEST_03", X = 34.4f, Y = 0, Z = 4.0f, Label = "银宝箱", SubLabel = "胡萝卜", IconId = IconChest },
            new() { Id = "CHEST_04", X = 38.7f, Y = 0, Z = 4.2f, Label = "铜箱子", SubLabel = "新月岛北部", IconId = IconChest },
            new() { Id = "CHEST_05", X = 26.7f, Y = 0, Z = 21.6f, Label = "银箱子", SubLabel = "地下空洞", IconId = IconChest },
            new() { Id = "CHEST_06", X = 22.3f, Y = 0, Z = 24.8f, Label = "宝箱", SubLabel = "地下凉亭", IconId = IconChest },
            new() { Id = "CHEST_07", X = 3.9f, Y = 0, Z = 15.2f, Label = "铜箱子", SubLabel = "左边洞里", IconId = IconChest },

            // ===== 胡萝卜/特殊 标记 =====
            new() { Id = "CARROT_01", X = 36.5f, Y = 0, Z = 19.0f, Label = "胡萝卜", IconId = IconChest },
            new() { Id = "CARROT_02", X = 16.2f, Y = 0, Z = 22.5f, Label = "好运胡萝卜", IconId = IconChest },
            new() { Id = "CARROT_03", X = 38.5f, Y = 0, Z = 14.6f, Label = "胡萝卜", IconId = IconChest },
        ];

        return dict;
    }
}
