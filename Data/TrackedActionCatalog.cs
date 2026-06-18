namespace AllHud.Data;

public sealed record JobCatalogEntry(uint ClassJobId, string Name);

public sealed class TrackedActionDefinition {
    public TrackedActionDefinition(string key, uint classJobId, string jobName, TrackedStatusDefinition definition, bool enabledByDefault = true, bool isSharedSkill = false) {
        Key = key;
        ClassJobId = classJobId;
        JobName = jobName;
        Definition = definition;
        EnabledByDefault = enabledByDefault;
        IsSharedSkill = isSharedSkill;
    }

    public string Key { get; }
    public uint ClassJobId { get; }
    public string JobName { get; }
    public TrackedStatusDefinition Definition { get; }
    public bool EnabledByDefault { get; }
    public bool IsSharedSkill { get; }
}

public static class TrackedActionCatalog {
    public const uint Paladin = 19;
    public const uint Monk = 20;
    public const uint Warrior = 21;
    public const uint Dragoon = 22;
    public const uint Bard = 23;
    public const uint WhiteMage = 24;
    public const uint BlackMage = 25;
    public const uint Summoner = 27;
    public const uint Scholar = 28;
    public const uint Ninja = 30;
    public const uint Machinist = 31;
    public const uint DarkKnight = 32;
    public const uint Astrologian = 33;
    public const uint Samurai = 34;
    public const uint RedMage = 35;
    public const uint Gunbreaker = 37;
    public const uint Dancer = 38;
    public const uint Reaper = 39;
    public const uint Sage = 40;
    public const uint Viper = 41;
    public const uint Pictomancer = 42;

    public static readonly IReadOnlyList<JobCatalogEntry> Jobs = [
        new(Paladin, "骑士"),
        new(Warrior, "战士"),
        new(DarkKnight, "暗骑"),
        new(Gunbreaker, "枪刃"),
        new(Monk, "武僧"),
        new(Dragoon, "龙骑"),
        new(Ninja, "忍者"),
        new(Samurai, "武士"),
        new(Reaper, "钐镰"),
        new(Viper, "蝰蛇"),
        new(Bard, "诗人"),
        new(Machinist, "机工"),
        new(Dancer, "舞者"),
        new(BlackMage, "黑魔"),
        new(Summoner, "召唤"),
        new(RedMage, "赤魔"),
        new(Pictomancer, "绘灵"),
        new(WhiteMage, "白魔"),
        new(Scholar, "学者"),
        new(Astrologian, "占星"),
        new(Sage, "贤者"),
    ];

    private static readonly uint[] TankJobs = [Paladin, Warrior, DarkKnight, Gunbreaker];
    private static readonly uint[] MeleeJobs = [Monk, Dragoon, Ninja, Samurai, Reaper, Viper];
    private static readonly uint[] PhysicalRangedJobs = [Bard, Machinist, Dancer];
    private static readonly uint[] CasterJobs = [BlackMage, Summoner, RedMage, Pictomancer];
    private static readonly uint[] PhysicalDpsJobs = [Monk, Dragoon, Ninja, Samurai, Reaper, Viper, Bard, Machinist, Dancer];
    private static readonly uint[] HealerJobs = [WhiteMage, Scholar, Astrologian, Sage];
    private static readonly uint[] MagicJobs = [BlackMage, Summoner, RedMage, Pictomancer, WhiteMage, Scholar, Astrologian, Sage];
    private static readonly uint[] ArmLengthJobs = [Paladin, Warrior, DarkKnight, Gunbreaker, Monk, Dragoon, Ninja, Samurai, Reaper, Viper, Bard, Machinist, Dancer];
    private static readonly uint[] InterruptJobs = [Paladin, Warrior, DarkKnight, Gunbreaker, Bard, Machinist, Dancer];

    public static readonly IReadOnlySet<uint> IndependentMonitorCommonActionIds = new HashSet<uint> {
        3,
        7533,
        7537,
        7548,
        7559,
        7541,
        7542,
    };

    public static readonly IReadOnlySet<uint> PartyInfoExtraMitigationActionIds = new HashSet<uint> {
        157,
        2241,
        7394,
        7432,
        7408,
        7498,
        16015,
        24404,
        25799,
        25861,
        34685,
        34686,
        36962,
    };

    private static readonly IReadOnlySet<string> CuratedRaidBuffActionNames = new HashSet<string>(StringComparer.Ordinal) {
        "战斗之声",
        "光明神的最终乐章",
        "战斗连祷",
        "义结金兰",
        "介毒之术",
        "夺取",
        "神秘环",
        "技巧舞步",
        "技术舞步结束",
        "灼热之光",
        "鼓励",
        "星空构想",
        "连环计",
        "占卜",
        "Battle Voice",
        "Radiant Finale",
        "Battle Litany",
        "Brotherhood",
        "Dokumori",
        "Mug",
        "Arcane Circle",
        "Technical Finish",
        "Searing Light",
        "Embolden",
        "Starry Muse",
        "Chain Stratagem",
        "Divination",
    };

    private static readonly IReadOnlySet<string> CuratedBurstActionNames = new HashSet<string>(StringComparer.Ordinal) {
        "进攻之探戈",
        "安魂祈祷",
        "绝对统治",
        "战逃反应",
        "原初的解放",
        "血乱",
        "掠影示现",
        "血壤",
        "倾泻弹雨",
        "连祷终结",
        "整备",
        "野火",
        "超荷",
        "枪管加热",
        "Devilment",
        "Requiescat",
        "Imperator",
        "Fight or Flight",
        "Inner Release",
        "Delirium",
        "Living Shadow",
        "Bloodfest",
        "Reassemble",
        "Wildfire",
        "Hypercharge",
        "Barrel Stabilizer",
    };

    private static readonly IReadOnlySet<string> CuratedTargetMitigationActionNames = new HashSet<string>(StringComparer.Ordinal) {
        "雪仇",
        "牵制",
        "昏乱",
        "Reprisal",
        "Feint",
        "Addle",
    };

    private static readonly IReadOnlySet<string> CuratedPersonalMitigationActionNames = new HashSet<string>(StringComparer.Ordinal) {
        "铁壁",
        "预警",
        "极致防御",
        "干预",
        "原初的勇猛",
        "原初的血气",
        "戮罪",
        "暗影墙",
        "暗影守夜",
        "至黑之夜",
        "星云",
        "大星云",
        "刚玉之心",
        "水流幕",
        "神祝祷",
        "深谋远虑之策",
        "生命回生法",
        "擢升",
        "天星交错",
        "白牛清汁",
        "输血",
        "混合",
        "四液混合",
        "坦培拉涂层",
        "坦培拉油彩",
        "灿烂之盾",
        "神秘纹",
        "残影",
        "金刚极意",
        "第三眼",
        "天诛势",
        "大地神的抒情恋歌",
        "治疗之华尔兹",
        "魔罩",
        "Rampart",
        "Sentinel",
        "Guardian",
        "Intervention",
        "Nascent Flash",
        "Bloodwhetting",
        "Damnation",
        "Shadow Wall",
        "Shadowed Vigil",
        "The Blackest Night",
        "Nebula",
        "Great Nebula",
        "Heart of Corundum",
        "Aquaveil",
        "Divine Benison",
        "Excogitation",
        "Protraction",
        "Exaltation",
        "Celestial Intersection",
        "Taurochole",
        "Haima",
        "Krasis",
        "Tempera Coat",
        "Tempera Grassa",
        "Radiant Aegis",
        "Arcane Crest",
        "Shade Shift",
        "Riddle of Earth",
        "Third Eye",
        "Tengentsu",
        "Nature's Minne",
        "Curing Waltz",
        "Manaward",
    };

    private static readonly IReadOnlySet<string> CuratedPartyMitigationActionNames = new HashSet<string>(StringComparer.Ordinal) {
        "真言",
        "圣光幕帘",
        "武装戍卫",
        "摆脱",
        "暗黑布道",
        "光之心",
        "行吟",
        "策动",
        "防守之桑巴",
        "魔法屏障",
        "节制",
        "野战治疗阵",
        "异想的幻光",
        "展开战术",
        "慰藉",
        "生命回生法",
        "疾风怒涛之计",
        "疾风怒涛",
        "命运之轮",
        "中间学派",
        "擢升",
        "太阳星座",
        "坚角清汁",
        "整体论",
        "泛输血",
        "魂灵风息",
        "武装解除",
        "Mantra",
        "Divine Veil",
        "Passage of Arms",
        "Shake It Off",
        "Dark Missionary",
        "Heart of Light",
        "Troubadour",
        "Tactician",
        "Shield Samba",
        "Magick Barrier",
        "Temperance",
        "Sacred Soil",
        "Fey Illumination",
        "Deployment Tactics",
        "Consolation",
        "Protraction",
        "Expedient",
        "Collective Unconscious",
        "Neutral Sect",
        "Sun Sign",
        "Kerachole",
        "Holos",
        "Panhaima",
        "Pneuma",
        "Dismantle",
    };

    private static readonly IReadOnlySet<string> CuratedCommonActionNames = new HashSet<string>(StringComparer.Ordinal) {
        "疾跑",
        "挑衅",
        "退避",
        "沉稳咏唱",
        "醒梦",
        "即刻咏唱",
        "插言",
        "下踢",
        "亲疏自行",
        "内丹",
        "浴血",
        "Sprint",
        "Provoke",
        "Shirk",
        "Surecast",
        "Lucid Dreaming",
        "Swiftcast",
        "Interject",
        "Low Blow",
        "Arm's Length",
        "Second Wind",
        "Bloodbath",
    };

    public static readonly IReadOnlyList<TrackedActionDefinition> CommonSkills = [
        CommonSkill("common-sprint", 50, "疾跑", CooldownGroup.Common, 60, 10, false, 3),
        CommonSkillForJobs("common-rampart", 1191, "铁壁", CooldownGroup.PersonalMitigation, 90, 20, false, TankJobs, 7531),
        CommonSkillForJobs("common-provoke", 0, "挑衅", CooldownGroup.Common, 30, 0, false, TankJobs, 7533),
        CommonSkillForJobs("common-reprisal", 1193, "雪仇", CooldownGroup.PartyMitigation, 60, 15, true, TankJobs, 7535),
        CommonSkillForJobs("common-shirk", 0, "退避", CooldownGroup.Common, 120, 0, false, TankJobs, 7537),
        CommonSkillForJobs("common-feint", 1195, "牵制", CooldownGroup.PartyMitigation, 90, 10, true, MeleeJobs, 7549),
        CommonSkillForJobs("common-addle", 1203, "昏乱", CooldownGroup.PartyMitigation, 90, 10, true, CasterJobs, 7560),
        CommonSkillForJobs("common-surecast", 0, "沉稳咏唱", CooldownGroup.Common, 120, 6, false, MagicJobs, 7559),
        CommonSkillForJobs("common-lucid-dreaming", 1204, "醒梦", CooldownGroup.Common, 60, 21, false, MagicJobs, 7562),
        CommonSkillForJobs("common-swiftcast", 167, "即刻咏唱", CooldownGroup.Common, 40, 10, false, MagicJobs, 7561),
        CommonSkillForJobs("common-interject", 0, "插言", CooldownGroup.Common, 30, 0, false, InterruptJobs, 7538),
        CommonSkillForJobs("common-low-blow", 0, "下踢", CooldownGroup.Common, 25, 0, false, TankJobs, 7540),
        CommonSkillForJobs("common-arms-length", 0, "亲疏自行", CooldownGroup.Common, 120, 6, false, ArmLengthJobs, 7548),
        CommonSkillForJobs("common-second-wind", 0, "内丹", CooldownGroup.Common, 120, 0, false, PhysicalDpsJobs, 7541),
        CommonSkillForJobs("common-bloodbath", 84, "浴血", CooldownGroup.Common, 90, 20, false, MeleeJobs, 7542),
    ];

    public static readonly IReadOnlyList<TrackedActionDefinition> Skills = [
        Skill(Paladin, "骑士", "pld-divine-veil", 726, "圣光幕帘", CooldownGroup.PartyMitigation, 90, 30, false, 3540),
        Skill(Paladin, "骑士", "pld-passage-of-arms", 1175, "武装戍卫", CooldownGroup.PartyMitigation, 120, 18, false, 7385),
        Skill(Paladin, "骑士", "pld-imperator", 0, "绝对统治", CooldownGroup.Burst, 60, 0, false, 36921, 7383),
        Skill(Paladin, "骑士", "pld-hallowed-ground", 82, "神圣领域", CooldownGroup.PersonalMitigation, 420, 10, false, 30),
        Skill(Paladin, "骑士", "pld-intervention", 1174, "干预", CooldownGroup.PersonalMitigation, 10, 8, false, 7382),
        Skill(Paladin, "骑士", "pld-holy-sheltron", 2674, "圣盾阵", CooldownGroup.PersonalMitigation, 5, 8, false, 25746),
        Skill(Paladin, "骑士", "pld-guardian", 3829, "极致防御", CooldownGroup.PersonalMitigation, 120, 15, false, 36920, 17),

        Skill(Warrior, "战士", "war-shake-it-off", 1457, "摆脱", CooldownGroup.PartyMitigation, 90, 30, false, 7388),
        Skill(Warrior, "战士", "war-inner-release", 0, "原初的解放", CooldownGroup.Burst, 60, 0, false, 7389, 38),
        Skill(Warrior, "战士", "war-thrill-of-battle", 87, "战栗", CooldownGroup.Personal, 90, 10, false, 40),
        Skill(Warrior, "战士", "war-holmgang", 409, "死斗", CooldownGroup.PersonalMitigation, 240, 10, false, 43),
        Skill(Warrior, "战士", "war-damnation", 3832, "戮罪", CooldownGroup.PersonalMitigation, 120, 15, false, 36923, 44),
        Skill(Warrior, "战士", "war-bloodwhetting", 2678, "原初的血气", CooldownGroup.PersonalMitigation, 25, 8, false, 25751, 16464),

        Skill(DarkKnight, "暗骑", "drk-dark-missionary", 1894, "暗黑布道", CooldownGroup.PartyMitigation, 90, 15, false, 16471),
        Skill(DarkKnight, "暗骑", "drk-delirium", 0, "血乱", CooldownGroup.Burst, 60, 0, false, 7390),
        Skill(DarkKnight, "暗骑", "drk-living-shadow", 0, "掠影示现", CooldownGroup.Burst, 120, 0, false, 16472),
        Skill(DarkKnight, "暗骑", "drk-dark-mind", 746, "弃明投暗", CooldownGroup.PersonalMitigation, 60, 10, false, 3634),
        Skill(DarkKnight, "暗骑", "drk-shadowed-vigil", 3835, "暗影守夜", CooldownGroup.PersonalMitigation, 120, 15, false, 36927, 3636),
        Skill(DarkKnight, "暗骑", "drk-living-dead", 810, "行尸走肉", CooldownGroup.PersonalMitigation, 300, 10, false, 3638),
        Skill(DarkKnight, "暗骑", "drk-the-blackest-night", 1178, "至黑之夜", CooldownGroup.PersonalMitigation, 15, 7, false, 7393),
        Skill(DarkKnight, "暗骑", "drk-oblation", 2682, "献奉", CooldownGroup.PersonalMitigation, 60, 10, false, 25754),

        Skill(Gunbreaker, "枪刃", "gnb-heart-of-light", 1839, "光之心", CooldownGroup.PartyMitigation, 90, 15, false, 16160),
        Skill(Gunbreaker, "枪刃", "gnb-bloodfest", 0, "血壤", CooldownGroup.Burst, 120, 0, false, 16164),
        Skill(Gunbreaker, "枪刃", "gnb-camouflage", 1832, "伪装", CooldownGroup.PersonalMitigation, 90, 20, false, 16140),
        Skill(Gunbreaker, "枪刃", "gnb-great-nebula", 3838, "大星云", CooldownGroup.PersonalMitigation, 120, 15, false, 36935, 16148),
        Skill(Gunbreaker, "枪刃", "gnb-aurora", 1835, "极光", CooldownGroup.PersonalMitigation, 60, 18, false, 16151),
        Skill(Gunbreaker, "枪刃", "gnb-superbolide", 1836, "超火流星", CooldownGroup.PersonalMitigation, 360, 10, false, 16152),
        Skill(Gunbreaker, "枪刃", "gnb-heart-of-corundum", 2683, "刚玉之心", CooldownGroup.PersonalMitigation, 25, 8, false, 25758),

        Skill(Monk, "武僧", "mnk-brotherhood", 1185, "义结金兰", CooldownGroup.RaidBuff, 120, 20, false, 7396),
        Skill(Monk, "武僧", "mnk-mantra", 102, "真言", CooldownGroup.PartyMitigation, 120, 15, false, 65),
        Skill(Monk, "武僧", "mnk-riddle-of-earth", 1179, "金刚极意", CooldownGroup.PersonalMitigation, 120, 10, false, 7394),

        Skill(Dragoon, "龙骑", "drg-battle-litany", 786, "战斗连祷", CooldownGroup.RaidBuff, 120, 20, false, 3557),

        Skill(Ninja, "忍者", "nin-dokumori", 3849, "介毒之术", CooldownGroup.RaidBuff, 120, 20, true, 36957),
        Skill(Ninja, "忍者", "nin-shade-shift", 488, "残影", CooldownGroup.PersonalMitigation, 120, 20, false, 2241),

        Skill(Samurai, "武士", "sam-third-eye", 1232, "第三眼", CooldownGroup.PersonalMitigation, 15, 4, false, 7498),
        Skill(Samurai, "武士", "sam-tengentsu", 3853, "天诛势", CooldownGroup.PersonalMitigation, 15, 4, false, 36962),

        Skill(Reaper, "钐镰", "rpr-arcane-circle", 2599, "神秘环", CooldownGroup.RaidBuff, 120, 20, false, 24405),
        Skill(Reaper, "钐镰", "rpr-arcane-crest", 2598, "神秘纹", CooldownGroup.PersonalMitigation, 30, 5, false, 24404),

        Skill(Bard, "诗人", "brd-battle-voice", 141, "战斗之声", CooldownGroup.RaidBuff, 120, 20, false, 118),
        Skill(Bard, "诗人", "brd-radiant-finale", 2964, "光明神的最终乐章", CooldownGroup.RaidBuff, 110, 15, false, 25785),
        Skill(Bard, "诗人", "brd-troubadour", 1934, "行吟", CooldownGroup.PartyMitigation, 120, 15, false, 7405),
        Skill(Bard, "诗人", "brd-natures-minne", 1202, "大地神的抒情恋歌", CooldownGroup.PersonalMitigation, 120, 15, false, 7408),

        Skill(Machinist, "机工", "mch-tactician", 1951, "策动", CooldownGroup.PartyMitigation, 120, 15, false, 16889),
        Skill(Machinist, "机工", "mch-dismantle", 860, "武装解除", CooldownGroup.PartyMitigation, 120, 10, true, 2887),
        Skill(Machinist, "机工", "mch-reassemble", 0, "整备", CooldownGroup.Burst, 55, 0, false, 2876),
        Skill(Machinist, "机工", "mch-wildfire", 0, "野火", CooldownGroup.Burst, 120, 0, false, 2878),
        Skill(Machinist, "机工", "mch-barrel-stabilizer", 0, "枪管加热", CooldownGroup.Burst, 120, 0, false, 7414),

        Skill(Dancer, "舞者", "dnc-technical-finish", 1822, "技巧舞步", CooldownGroup.RaidBuff, 120, 20, false, 15998),
        Skill(Dancer, "舞者", "dnc-devilment", 1825, "进攻之探戈", CooldownGroup.Burst, 120, 20, false, 16011),
        Skill(Dancer, "舞者", "dnc-shield-samba", 1826, "防守之桑巴", CooldownGroup.PartyMitigation, 120, 15, false, 16012),
        Skill(Dancer, "舞者", "dnc-curing-waltz", 0, "治疗之华尔兹", CooldownGroup.PersonalMitigation, 60, 0, false, 16015),

        Skill(BlackMage, "黑魔", "blm-manaward", 168, "魔罩", CooldownGroup.PersonalMitigation, 120, 20, false, 157),

        Skill(Summoner, "召唤", "smn-searing-light", 2703, "灼热之光", CooldownGroup.RaidBuff, 120, 30, false, 25801),
        Skill(Summoner, "召唤", "smn-radiant-aegis", 2702, "灿烂之盾", CooldownGroup.PersonalMitigation, 60, 30, false, 25799),

        Skill(RedMage, "赤魔", "rdm-embolden", 1239, "鼓励", CooldownGroup.RaidBuff, 120, 20, false, 7520),
        Skill(RedMage, "赤魔", "rdm-magick-barrier", 2707, "魔法屏障", CooldownGroup.PartyMitigation, 120, 10, false, 25857),

        Skill(Pictomancer, "绘灵", "pct-starry-muse", 3685, "星空构想", CooldownGroup.RaidBuff, 120, 20, false, 34675),
        Skill(Pictomancer, "绘灵", "pct-tempera-coat", 3686, "坦培拉涂层", CooldownGroup.PersonalMitigation, 120, 10, false, 34685),
        Skill(Pictomancer, "绘灵", "pct-tempera-grassa", 3687, "坦培拉油彩", CooldownGroup.PersonalMitigation, 1, 10, false, 34686),

        Skill(WhiteMage, "白魔", "whm-temperance", 1872, "节制", CooldownGroup.PartyMitigation, 120, 20, false, 16536),
        Skill(WhiteMage, "白魔", "whm-aquaveil", 2708, "水流幕", CooldownGroup.PersonalMitigation, 60, 8, false, 25861),
        Skill(WhiteMage, "白魔", "whm-divine-benison", 1218, "神祝祷", CooldownGroup.PersonalMitigation, 30, 15, false, 7432),

        Skill(Scholar, "学者", "sch-chain-stratagem", 1221, "连环计", CooldownGroup.RaidBuff, 120, 15, true, 7436),
        Skill(Scholar, "学者", "sch-sacred-soil", 299, "野战治疗阵", CooldownGroup.PartyMitigation, 30, 15, false, 188),
        Skill(Scholar, "学者", "sch-expedient", 2711, "疾风怒涛", CooldownGroup.PartyMitigation, 120, 20, false, 25868),
        Skill(Scholar, "学者", "sch-deployment-tactics", 0, "展开战术", CooldownGroup.PartyMitigation, 120, 30, false, 3585),
        Skill(Scholar, "学者", "sch-excogitation", 1220, "深谋远虑之策", CooldownGroup.PersonalMitigation, 45, 45, false, 7434),
        Skill(Scholar, "学者", "sch-protraction", 2710, "生命回生法", CooldownGroup.PersonalMitigation, 60, 10, false, 25867),

        Skill(Astrologian, "占星", "ast-divination", 1878, "占卜", CooldownGroup.RaidBuff, 120, 20, false, 16552),
        Skill(Astrologian, "占星", "ast-collective-unconscious", 849, "命运之轮", CooldownGroup.PartyMitigation, 60, 18, false, 3613),
        Skill(Astrologian, "占星", "ast-neutral-sect", 1892, "中间学派", CooldownGroup.PartyMitigation, 120, 20, false, 16559),
        Skill(Astrologian, "占星", "ast-celestial-intersection", 1889, "天星交错", CooldownGroup.PersonalMitigation, 30, 30, false, 16556),
        Skill(Astrologian, "占星", "ast-exaltation", 2717, "擢升", CooldownGroup.PersonalMitigation, 60, 8, false, 25873),

        Skill(Sage, "贤者", "sge-kerachole", 2618, "坚角清汁", CooldownGroup.PartyMitigation, 30, 15, false, 24298),
        Skill(Sage, "贤者", "sge-panhaima", 2613, "泛输血", CooldownGroup.PartyMitigation, 120, 15, false, 24311),
        Skill(Sage, "贤者", "sge-holos", 3003, "整体论", CooldownGroup.PartyMitigation, 120, 20, false, 24310),
        Skill(Sage, "贤者", "sge-taurochole", 2619, "白牛清汁", CooldownGroup.PersonalMitigation, 45, 15, false, 24303),
        Skill(Sage, "贤者", "sge-haima", 2612, "输血", CooldownGroup.PersonalMitigation, 120, 15, false, 24305),
        Skill(Sage, "贤者", "sge-krasis", 2622, "混合", CooldownGroup.PersonalMitigation, 60, 10, false, 24317),
    ];

    private static readonly IReadOnlyDictionary<uint, IReadOnlyList<TrackedActionDefinition>> SharedSkillsByJob = Jobs
        .ToDictionary(
            job => job.ClassJobId,
            job => (IReadOnlyList<TrackedActionDefinition>)CommonSkills
                .Where(skill => IsCommonSkillAllowedForJob(skill, job.ClassJobId))
                .Select(skill => CloneCommonSkillForJob(skill, job.ClassJobId, job.Name))
                .ToList());

    // 队伍信息的团减是固定全量来源，不再受“独立监控”勾选影响。
    // CommonSkills（雪仇/牵制/昏乱，按 TankJobs/MeleeJobs/CasterJobs 归属）与 Skills 里各职业独有的团减互不重复，
    // 且每条定义都带 SourceClassJobIds，因此队伍栏可按队员职业正确过滤。
    // 队伍信息显示的减伤来源：团减 + 少量 DPS 自保/护盾。
    public static readonly IReadOnlyList<TrackedStatusDefinition> PartyMitigationDefinitions = CommonSkills
        .Concat(Skills)
        .Where(skill => skill.Definition.Group == CooldownGroup.PartyMitigation
            || skill.Definition.ActionIds.Any(PartyInfoExtraMitigationActionIds.Contains))
        .Select(skill => skill.Definition)
        .ToList();

    public static readonly IReadOnlySet<uint> PartyMitigationActionIds = PartyMitigationDefinitions
        .SelectMany(definition => definition.ActionIds)
        .Where(actionId => actionId != 0)
        .ToHashSet();

    // 队伍冷却面板追踪的技能：独立监控那四类（团辅 / 爆发 / 单体减伤 / 其他长CD），全职业固定来源。
    // 每条定义都带 SourceClassJobIds，队伍面板按队员职业自动过滤；通用技能（共享）由 CommonSkills 提供多职业归属。
    public static readonly IReadOnlyList<TrackedStatusDefinition> PartyTrackedDefinitions = CommonSkills
        .Concat(Skills)
        .Where(skill => skill.Definition.Group is CooldownGroup.RaidBuff
            or CooldownGroup.Burst
            or CooldownGroup.PersonalMitigation
            or CooldownGroup.Personal)
        .Where(skill => !skill.Definition.ActionIds.Any(PartyMitigationActionIds.Contains))
        .Select(skill => skill.Definition)
        .ToList();

    public static readonly IReadOnlySet<uint> PartyTrackedActionIds = PartyTrackedDefinitions
        .SelectMany(definition => definition.ActionIds)
        .Where(actionId => actionId != 0)
        .ToHashSet();

    public static void EnsureSelectionInitialized(Configuration config) {
        config.EnabledJobSkillKeys ??= [];
        if (config.JobSkillSelectionInitialized) {
            return;
        }

        config.EnabledJobSkillKeys = Skills
            .Where(skill => skill.EnabledByDefault)
            .Select(skill => skill.Key)
            .ToList();
        config.JobSkillSelectionInitialized = true;
    }

    public static void EnsureActionSelectionInitialized(Configuration config) {
        config.EnabledJobActionKeys ??= [];
        MigrateCommonActionKeys(config);
        RemovePartyInfoActionKeys(config.EnabledJobActionKeys);

        if (config.JobActionSelectionInitialized) {
            return;
        }

        var enabledSkillKeys = (config.EnabledJobSkillKeys ?? [])
            .ToHashSet(StringComparer.Ordinal);
        var sourceSkills = config.JobSkillSelectionInitialized && enabledSkillKeys.Count > 0
            ? Skills.Where(skill => enabledSkillKeys.Contains(skill.Key))
            : Skills.Where(skill => skill.EnabledByDefault);

        config.EnabledJobActionKeys = sourceSkills
            .Where(skill => skill.Definition.ActionIds.FirstOrDefault() != 0)
            .Where(skill => !skill.Definition.ActionIds.Any(PartyMitigationActionIds.Contains))
            .Select(skill => GetActionKey(skill.ClassJobId, skill.Definition.ActionIds.First()))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        config.JobActionSelectionInitialized = true;
    }

    public static string GetActionKey(uint classJobId, uint actionId) => $"{classJobId}:{actionId}";

    public static string GetCommonActionKey(uint actionId) => GetActionKey(0, actionId);

    public static IReadOnlyList<uint> GetClassJobFamilyIds(uint classJobId) {
        var baseClassJobId = GetBaseClassJobId(classJobId);
        return baseClassJobId == 0 || baseClassJobId == classJobId
            ? [classJobId]
            : [classJobId, baseClassJobId];
    }

    public static uint GetBaseClassJobId(uint classJobId) {
        return classJobId switch {
            Paladin => 1,
            Monk => 2,
            Warrior => 3,
            Dragoon => 4,
            Bard => 5,
            WhiteMage => 6,
            BlackMage => 7,
            Summoner or Scholar => 26,
            Ninja => 29,
            _ => 0,
        };
    }

    public static TrackedActionDefinition? FindKnownSkill(uint classJobId, uint actionId) {
        return GetSkillsForJob(classJobId).FirstOrDefault(skill =>
            skill.ClassJobId == classJobId && skill.Definition.ActionIds.Contains(actionId));
    }

    public static TrackedActionDefinition? FindCommonSkill(uint actionId) {
        return CommonSkills.FirstOrDefault(skill => skill.Definition.ActionIds.Contains(actionId));
    }

    public static CooldownGroup? FindCuratedActionGroup(string actionName) {
        if (string.IsNullOrWhiteSpace(actionName)) {
            return null;
        }

        var normalizedName = actionName.Trim();
        if (CuratedRaidBuffActionNames.Contains(normalizedName)) {
            return CooldownGroup.RaidBuff;
        }

        if (CuratedBurstActionNames.Contains(normalizedName)) {
            return CooldownGroup.Burst;
        }

        if (CuratedTargetMitigationActionNames.Contains(normalizedName)) {
            return CooldownGroup.PartyMitigation;
        }

        if (CuratedPersonalMitigationActionNames.Contains(normalizedName)) {
            return CooldownGroup.PersonalMitigation;
        }

        if (CuratedPartyMitigationActionNames.Contains(normalizedName)) {
            return CooldownGroup.PartyMitigation;
        }

        if (CuratedCommonActionNames.Contains(normalizedName)) {
            return CooldownGroup.Common;
        }

        return null;
    }

    public static IReadOnlySet<string> GetEnabledSkillKeys(Configuration config) {
        EnsureSelectionInitialized(config);
        return config.EnabledJobSkillKeys.ToHashSet(StringComparer.Ordinal);
    }

    public static IReadOnlyList<TrackedStatusDefinition> GetEnabledDefinitions(Configuration config) {
        var enabledKeys = GetEnabledSkillKeys(config);
        EnsureActionSelectionInitialized(config);
        var enabledActionKeys = (config.EnabledJobActionKeys ?? []).ToHashSet(StringComparer.Ordinal);
        return GetAllActionSkills()
            .Where(skill => enabledKeys.Contains(skill.Key)
                            || enabledActionKeys.Contains(GetActionKey(skill.ClassJobId, skill.Definition.ActionIds.FirstOrDefault())))
            .Select(skill => skill.Definition)
            .ToList();
    }

    public static IReadOnlyList<TrackedActionDefinition> GetSkillsForJob(uint classJobId) {
        return Skills
            .Where(skill => skill.ClassJobId == classJobId)
            .Concat(GetSharedSkillsForJob(classJobId))
            .OrderBy(skill => skill.Definition.Group)
            .ThenBy(skill => skill.Definition.Name)
            .ToList();
    }

    public static IReadOnlyList<TrackedActionDefinition> GetSharedSkillsForJob(uint classJobId) {
        return SharedSkillsByJob.TryGetValue(classJobId, out var skills)
            ? skills
            : Array.Empty<TrackedActionDefinition>();
    }

    private static IEnumerable<TrackedActionDefinition> GetAllActionSkills() {
        return Skills.Concat(GetAllSharedSkills());
    }

    private static IEnumerable<TrackedActionDefinition> GetAllSharedSkills() {
        return SharedSkillsByJob.Values.SelectMany(skills => skills);
    }

    private static bool IsCommonSkillAllowedForJob(TrackedActionDefinition skill, uint classJobId) {
        return skill.Definition.SourceClassJobIds.Count == 0
               || skill.Definition.SourceClassJobIds.Contains(classJobId);
    }

    private static TrackedActionDefinition CloneCommonSkillForJob(TrackedActionDefinition skill, uint classJobId, string jobName) {
        var definition = skill.Definition;
        return new TrackedActionDefinition(
            $"{skill.Key}-{classJobId}",
            classJobId,
            jobName,
            new TrackedStatusDefinition(
                definition.StatusId,
                definition.Name,
                definition.Group,
                definition.CooldownSeconds,
                definition.DurationSeconds,
                definition.IsTargetDebuff,
                definition.ActionIds,
                new[] { classJobId }),
            skill.EnabledByDefault,
            true);
    }

    private static void MigrateCommonActionKeys(Configuration config) {
        if (config.EnabledJobActionKeys.Count == 0) {
            return;
        }

        var changed = false;
        var migratedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in config.EnabledJobActionKeys) {
            if (!TryParseActionKey(key, out var classJobId, out var actionId) || classJobId != 0) {
                migratedKeys.Add(key);
                continue;
            }

            var commonSkill = FindCommonSkill(actionId);
            if (commonSkill is null) {
                migratedKeys.Add(key);
                continue;
            }

            foreach (var job in Jobs.Where(job => IsCommonSkillAllowedForJob(commonSkill, job.ClassJobId))) {
                migratedKeys.Add(GetActionKey(job.ClassJobId, actionId));
            }

            changed = true;
        }

        if (changed) {
            config.EnabledJobActionKeys = migratedKeys.ToList();
        }
    }

    private static void RemovePartyInfoActionKeys(List<string> keys) {
        keys.RemoveAll(key => TryParseActionKey(key, out _, out var actionId)
                              && PartyMitigationActionIds.Contains(actionId));
    }

    private static bool TryParseActionKey(string key, out uint classJobId, out uint actionId) {
        classJobId = 0;
        actionId = 0;

        if (string.IsNullOrWhiteSpace(key)) {
            return false;
        }

        var parts = key.Split(':', 2);
        return parts.Length == 2
               && uint.TryParse(parts[0], out classJobId)
               && uint.TryParse(parts[1], out actionId)
               && actionId != 0;
    }

    private static TrackedActionDefinition TankRole(uint classJobId, string jobName, string key) {
        return SharedSkill(classJobId, jobName, key, 1193, "雪仇", CooldownGroup.PartyMitigation, 60, 15, true, 7535);
    }

    private static TrackedActionDefinition Sprint(uint classJobId, string jobName) {
        return SharedSkill(classJobId, jobName, $"common-sprint-{classJobId}", 50, "疾跑", CooldownGroup.Common, 60, 10, false, 3);
    }

    private static TrackedActionDefinition MeleeRole(uint classJobId, string jobName, string key) {
        return SharedSkill(classJobId, jobName, key, 1195, "牵制", CooldownGroup.PartyMitigation, 90, 10, true, 7549);
    }

    private static TrackedActionDefinition MagicRole(uint classJobId, string jobName, string key) {
        return SharedSkill(classJobId, jobName, key, 1203, "昏乱", CooldownGroup.PartyMitigation, 90, 10, true, 7560);
    }

    private static TrackedActionDefinition SurecastRole(uint classJobId, string jobName, string key) {
        return SharedSkill(classJobId, jobName, key, 0, "沉稳咏唱", CooldownGroup.Common, 120, 6, false, 7559);
    }

    private static TrackedActionDefinition CommonSkill(
        string key,
        uint statusId,
        string name,
        CooldownGroup group,
        float cooldownSeconds,
        float durationSeconds,
        bool isTargetDebuff,
        params uint[] actionIds) {
        return new TrackedActionDefinition(
            key,
            0,
            "通用",
            new TrackedStatusDefinition(
                statusId,
                name,
                group,
                cooldownSeconds,
                durationSeconds,
                isTargetDebuff,
                actionIds,
                Array.Empty<uint>()),
            isSharedSkill: true);
    }

    private static TrackedActionDefinition CommonSkillForJobs(
        string key,
        uint statusId,
        string name,
        CooldownGroup group,
        float cooldownSeconds,
        float durationSeconds,
        bool isTargetDebuff,
        IReadOnlyList<uint> sourceClassJobIds,
        params uint[] actionIds) {
        return new TrackedActionDefinition(
            key,
            0,
            "通用",
            new TrackedStatusDefinition(
                statusId,
                name,
                group,
                cooldownSeconds,
                durationSeconds,
                isTargetDebuff,
                actionIds,
                sourceClassJobIds),
            isSharedSkill: true);
    }

    private static TrackedActionDefinition Skill(
        uint classJobId,
        string jobName,
        string key,
        uint statusId,
        string name,
        CooldownGroup group,
        float cooldownSeconds,
        float durationSeconds,
        bool isTargetDebuff,
        params uint[] actionIds) {
        return CreateSkill(classJobId, jobName, key, statusId, name, group, cooldownSeconds, durationSeconds, isTargetDebuff, false, actionIds);
    }

    private static TrackedActionDefinition SharedSkill(
        uint classJobId,
        string jobName,
        string key,
        uint statusId,
        string name,
        CooldownGroup group,
        float cooldownSeconds,
        float durationSeconds,
        bool isTargetDebuff,
        params uint[] actionIds) {
        return CreateSkill(classJobId, jobName, key, statusId, name, group, cooldownSeconds, durationSeconds, isTargetDebuff, true, actionIds);
    }

    private static TrackedActionDefinition CreateSkill(
        uint classJobId,
        string jobName,
        string key,
        uint statusId,
        string name,
        CooldownGroup group,
        float cooldownSeconds,
        float durationSeconds,
        bool isTargetDebuff,
        bool isSharedSkill,
        IReadOnlyList<uint> actionIds) {
        return new TrackedActionDefinition(
            key,
            classJobId,
            jobName,
            new TrackedStatusDefinition(
                statusId,
                name,
                group,
                cooldownSeconds,
                durationSeconds,
                isTargetDebuff,
                actionIds,
                new[] { classJobId }),
            isSharedSkill: isSharedSkill);
    }
}
