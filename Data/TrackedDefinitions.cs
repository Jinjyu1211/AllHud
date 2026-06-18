namespace AllHud.Data;

public enum CooldownGroup {
    Common,
    Personal,
    Burst,
    PartyMitigation,
    TargetMitigation,
    PersonalMitigation,
    RaidBuff,
    Mitigation,
}

public sealed class TrackedStatusDefinition {
    public TrackedStatusDefinition(
        uint statusId,
        string name,
        CooldownGroup group,
        float cooldownSeconds,
        float durationSeconds,
        bool isTargetDebuff = false,
        IReadOnlyList<uint>? actionIds = null,
        IReadOnlyList<uint>? sourceClassJobIds = null) {
        StatusId = statusId;
        Name = name;
        Group = group;
        CooldownSeconds = cooldownSeconds;
        DurationSeconds = durationSeconds;
        IsTargetDebuff = isTargetDebuff;
        ActionIds = actionIds ?? Array.Empty<uint>();
        SourceClassJobIds = sourceClassJobIds ?? Array.Empty<uint>();
    }

    public uint StatusId { get; }
    public string Name { get; }
    public CooldownGroup Group { get; }
    public float CooldownSeconds { get; }
    public float DurationSeconds { get; }
    public bool IsTargetDebuff { get; }
    public IReadOnlyList<uint> ActionIds { get; }
    public IReadOnlyList<uint> SourceClassJobIds { get; }
}

public static class TrackedDefinitions {
    // 初始定义。进游戏打开“显示状态 ID”后，可以继续校准和补全 ID。
    public static readonly IReadOnlyList<TrackedStatusDefinition> RaidBuffs = [
        new(141, "战斗之声", CooldownGroup.RaidBuff, 120, 20, actionIds: new uint[] { 118 }),
        new(786, "战斗连祷", CooldownGroup.RaidBuff, 120, 20, actionIds: new uint[] { 3557 }),
        new(1185, "义结金兰", CooldownGroup.RaidBuff, 120, 20, actionIds: new uint[] { 7396 }),
        new(1221, "连环计", CooldownGroup.RaidBuff, 120, 15, true, new uint[] { 7436 }),
        new(1239, "鼓励", CooldownGroup.RaidBuff, 120, 20, actionIds: new uint[] { 7520 }),
        new(1825, "进攻之探戈", CooldownGroup.Burst, 120, 20, actionIds: new uint[] { 16011 }),
        new(1822, "技术舞步结束", CooldownGroup.RaidBuff, 120, 20, actionIds: new uint[] { 15998 }),
        new(1878, "占卜", CooldownGroup.RaidBuff, 120, 20, actionIds: new uint[] { 16552 }),
        new(2599, "神秘环", CooldownGroup.RaidBuff, 120, 20, actionIds: new uint[] { 24405 }),
        new(2703, "灼热之光", CooldownGroup.RaidBuff, 120, 30, actionIds: new uint[] { 25801 }),
        new(3849, "介毒之术", CooldownGroup.RaidBuff, 120, 20, true, new uint[] { 36957 }),
    ];

    public static readonly IReadOnlyList<TrackedStatusDefinition> Mitigations = [
        new(1193, "雪仇", CooldownGroup.PartyMitigation, 60, 15, true, new uint[] { 7535 }),
        new(1195, "牵制", CooldownGroup.PartyMitigation, 90, 10, true, new uint[] { 7549 }),
        new(1203, "昏乱", CooldownGroup.PartyMitigation, 90, 10, true, new uint[] { 7560 }),
        new(1191, "铁壁", CooldownGroup.PersonalMitigation, 90, 20, actionIds: new uint[] { 7531 }),
        new(82, "神圣领域", CooldownGroup.PersonalMitigation, 420, 10, actionIds: new uint[] { 30 }),
        new(2674, "圣盾阵", CooldownGroup.PersonalMitigation, 5, 8, actionIds: new uint[] { 25746 }),
        new(3829, "极致防御", CooldownGroup.PersonalMitigation, 120, 15, actionIds: new uint[] { 36920, 17 }),
        new(1174, "干预", CooldownGroup.PersonalMitigation, 10, 8, actionIds: new uint[] { 7382 }),
        new(3832, "戮罪", CooldownGroup.PersonalMitigation, 120, 15, actionIds: new uint[] { 36923, 44 }),
        new(87, "战栗", CooldownGroup.Personal, 90, 10, actionIds: new uint[] { 40 }),
        new(409, "死斗", CooldownGroup.PersonalMitigation, 240, 10, actionIds: new uint[] { 43 }),
        new(2678, "原初的血气", CooldownGroup.PersonalMitigation, 25, 8, actionIds: new uint[] { 25751, 16464 }),
        new(3835, "暗影守夜", CooldownGroup.PersonalMitigation, 120, 15, actionIds: new uint[] { 36927, 3636 }),
        new(746, "弃明投暗", CooldownGroup.PersonalMitigation, 60, 10, actionIds: new uint[] { 3634 }),
        new(810, "行尸走肉", CooldownGroup.PersonalMitigation, 300, 10, actionIds: new uint[] { 3638 }),
        new(1178, "至黑之夜", CooldownGroup.PersonalMitigation, 15, 7, actionIds: new uint[] { 7393 }),
        new(2682, "献奉", CooldownGroup.PersonalMitigation, 60, 10, actionIds: new uint[] { 25754 }),
        new(1832, "伪装", CooldownGroup.PersonalMitigation, 90, 20, actionIds: new uint[] { 16140 }),
        new(3838, "大星云", CooldownGroup.PersonalMitigation, 120, 15, actionIds: new uint[] { 36935, 16148 }),
        new(1836, "超火流星", CooldownGroup.PersonalMitigation, 360, 10, actionIds: new uint[] { 16152 }),
        new(1835, "极光", CooldownGroup.PersonalMitigation, 60, 18, actionIds: new uint[] { 16151 }),
        new(2683, "刚玉之心", CooldownGroup.PersonalMitigation, 25, 8, actionIds: new uint[] { 25758 }),
        new(168, "魔罩", CooldownGroup.PersonalMitigation, 120, 20, actionIds: new uint[] { 157 }),
        new(2708, "水流幕", CooldownGroup.PersonalMitigation, 60, 8, actionIds: new uint[] { 25861 }),
        new(1218, "神祝祷", CooldownGroup.PersonalMitigation, 30, 15, actionIds: new uint[] { 7432 }),
        new(1220, "深谋远虑之策", CooldownGroup.PersonalMitigation, 45, 45, actionIds: new uint[] { 7434 }),
        new(2710, "生命回生法", CooldownGroup.PersonalMitigation, 60, 10, actionIds: new uint[] { 25867 }),
        new(1889, "天星交错", CooldownGroup.PersonalMitigation, 30, 30, actionIds: new uint[] { 16556 }),
        new(2717, "擢升", CooldownGroup.PersonalMitigation, 60, 8, actionIds: new uint[] { 25873 }),
        new(2619, "白牛清汁", CooldownGroup.PersonalMitigation, 45, 15, actionIds: new uint[] { 24303 }),
        new(2612, "输血", CooldownGroup.PersonalMitigation, 120, 15, actionIds: new uint[] { 24305 }),
        new(2622, "混合", CooldownGroup.PersonalMitigation, 60, 10, actionIds: new uint[] { 24317 }),
        new(2702, "灿烂之盾", CooldownGroup.PersonalMitigation, 60, 30, actionIds: new uint[] { 25799 }),
        new(726, "圣光幕帘", CooldownGroup.PartyMitigation, 90, 30, actionIds: new uint[] { 3540 }),
        new(1175, "武装戍卫", CooldownGroup.PartyMitigation, 120, 18, actionIds: new uint[] { 7385 }),
        new(1457, "摆脱", CooldownGroup.PartyMitigation, 90, 30, actionIds: new uint[] { 7388 }),
        new(1826, "防守之桑巴", CooldownGroup.PartyMitigation, 120, 15, actionIds: new uint[] { 16012 }),
        new(1839, "光之心", CooldownGroup.PartyMitigation, 90, 15, actionIds: new uint[] { 16160 }),
        new(1894, "暗黑布道", CooldownGroup.PartyMitigation, 90, 15, actionIds: new uint[] { 16471 }),
        new(1934, "行吟", CooldownGroup.PartyMitigation, 120, 15, actionIds: new uint[] { 7405 }),
        new(1951, "策动", CooldownGroup.PartyMitigation, 120, 15, actionIds: new uint[] { 16889 }),
        new(860, "武装解除", CooldownGroup.PartyMitigation, 120, 10, true, new uint[] { 2887 }),
        new(1872, "节制", CooldownGroup.PartyMitigation, 120, 20, actionIds: new uint[] { 16536 }),
        new(299, "野战治疗阵", CooldownGroup.PartyMitigation, 30, 15, actionIds: new uint[] { 188 }),
        new(2711, "疾风怒涛", CooldownGroup.PartyMitigation, 120, 20, actionIds: new uint[] { 25868 }),
        new(2618, "坚角清汁", CooldownGroup.PartyMitigation, 30, 15, actionIds: new uint[] { 24298 }),
        new(2613, "泛输血", CooldownGroup.PartyMitigation, 120, 15, actionIds: new uint[] { 24311 }),
        new(3003, "整体论", CooldownGroup.PartyMitigation, 120, 20, actionIds: new uint[] { 24310 }),
        new(849, "命运之轮", CooldownGroup.PartyMitigation, 60, 18, actionIds: new uint[] { 3613 }),
        new(1892, "中间学派", CooldownGroup.PartyMitigation, 120, 20, actionIds: new uint[] { 16559 }),
        new(2707, "魔法屏障", CooldownGroup.PartyMitigation, 120, 10, actionIds: new uint[] { 25857 }),
        new(102, "真言", CooldownGroup.PartyMitigation, 120, 15, actionIds: new uint[] { 65 }),
    ];

    public static readonly IReadOnlyList<TrackedStatusDefinition> All = RaidBuffs
        .Concat(Mitigations)
        .ToList();

    public static readonly IReadOnlyDictionary<uint, TrackedStatusDefinition> ByStatusId = All
        .Where(definition => definition.StatusId != 0)
        .GroupBy(definition => definition.StatusId)
        .ToDictionary(group => group.Key, group => group.First());

    public static readonly IReadOnlyDictionary<uint, TrackedStatusDefinition> ByActionId = All
        .SelectMany(definition => definition.ActionIds.Select(actionId => new { actionId, definition }))
        .GroupBy(pair => pair.actionId)
        .ToDictionary(group => group.Key, group => group.First().definition);
}
