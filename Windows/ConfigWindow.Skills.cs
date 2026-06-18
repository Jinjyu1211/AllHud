using AllHud.Data;
using AllHud.Models;
using AllHud.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace AllHud.Windows;

public sealed partial class ConfigWindow {
    private readonly record struct JobSkillRoleGroup(string Label, IReadOnlyList<uint> ClassJobIds);

    private static readonly JobSkillRoleGroup[] JobSkillRoleGroups = [
        new("防护职业", [TrackedActionCatalog.Paladin, TrackedActionCatalog.Warrior, TrackedActionCatalog.DarkKnight, TrackedActionCatalog.Gunbreaker]),
        new("治疗职业", [TrackedActionCatalog.WhiteMage, TrackedActionCatalog.Scholar, TrackedActionCatalog.Astrologian, TrackedActionCatalog.Sage]),
        new("近战职业", [TrackedActionCatalog.Monk, TrackedActionCatalog.Dragoon, TrackedActionCatalog.Ninja, TrackedActionCatalog.Samurai, TrackedActionCatalog.Reaper, TrackedActionCatalog.Viper]),
        new("远程物理职业", [TrackedActionCatalog.Bard, TrackedActionCatalog.Machinist, TrackedActionCatalog.Dancer]),
        new("远程魔法职业", [TrackedActionCatalog.BlackMage, TrackedActionCatalog.Summoner, TrackedActionCatalog.RedMage, TrackedActionCatalog.Pictomancer]),
    ];

    private static readonly IReadOnlySet<string> HiddenActionSelectionNames = new HashSet<string>(StringComparer.Ordinal) {
        "狂暴",
        "战嚎",
        "Berserk",
        "安魂祈祷",
        "Requiescat",
        "能量抽取",
        "Energy Siphon",
    };

    private void DrawSkillsPage() {
        DrawSectionCard("独立监控", () => {
            DrawSelfCooldownBarSettings();
            DrawJobSkillSelector();
        });
    }

    private void DrawSelfCooldownBarSettings() {
        DrawTargetInfoSubsection("显示设置");
        DrawCheckbox("启动独立监控", nameof(this.config.ShowSelfCooldownBar), this.config.ShowSelfCooldownBar, value => this.config.ShowSelfCooldownBar = value);
        if (!this.config.ShowSelfCooldownBar) {
            ImGui.Spacing();
            return;
        }

        ImGui.SameLine(0.0f, 10.0f);
        DrawCheckbox("锁定窗口", nameof(this.config.SelfCooldownBarLocked), this.config.SelfCooldownBarLocked, value => this.config.SelfCooldownBarLocked = value);
        DrawSelfCooldownBarSelfVisibilityOptions();
        DrawInlineSegmentedSelector("方向", "self_cooldown_bar_layout_direction", Math.Clamp(this.config.SelfCooldownBarLayoutDirection, 0, 1), value => this.config.SelfCooldownBarLayoutDirection = value, ("竖向", 0), ("横向", 1));
        DrawSelfCooldownBarScaleOpacityRow();
        DrawCheckbox("隐藏已就绪技能", nameof(this.config.SelfCooldownBarHideWhenReady), this.config.SelfCooldownBarHideWhenReady, value => this.config.SelfCooldownBarHideWhenReady = value);
        ImGui.Spacing();
    }

    private void DrawSelfCooldownBarSelfVisibilityOptions() {
        var visibility = this.config.SelfCooldownBarSelfOnly ? 1 : this.config.SelfCooldownBarHideSelf ? 2 : 0;
        DrawInlineSegmentedSelector("显示", "self_cooldown_bar_visibility", visibility, value => {
            this.config.SelfCooldownBarSelfOnly = value == 1;
            this.config.SelfCooldownBarHideSelf = value == 2;
        }, ("全部", 0), ("仅自己", 1), ("隐藏自己", 2));
    }

    private void DrawSelfCooldownBarScaleOpacityRow() {
        var opacity = Math.Clamp(this.config.SelfCooldownBarOpacity, 0.15f, 1.0f);
        var labelColor = new Vector4(0.48f, 0.30f, 0.36f, 1.0f);

        DrawInlineHudScaleCombo("缩放", "SelfCooldownBarScale", this.config.SelfCooldownBarScale, value => this.config.SelfCooldownBarScale = Math.Clamp(value, 0.6f, 2.0f), labelColor);
        ImGui.SameLine(0.0f, 12.0f);

        if (DrawInlineOpacitySlider("透明度", "SelfCooldownBarOpacity", ref opacity)) {
            this.config.SelfCooldownBarOpacity = opacity;
            this.saveConfig();
        }
    }

    private void DrawDebugPage() {
        DrawSectionCard("预览", () => {
            DrawCheckbox("状态栏预览", nameof(this.config.ShowStatusPreview), this.config.ShowStatusPreview, value => this.config.ShowStatusPreview = value);
            DrawCheckbox("目标情报预览", nameof(this.config.ShowTargetInfoPreview), this.config.ShowTargetInfoPreview, value => this.config.ShowTargetInfoPreview = value);
            DrawCheckbox("独立监控冷却栏预览", nameof(this.config.ShowSelfCooldownBarPreview), this.config.ShowSelfCooldownBarPreview, value => this.config.ShowSelfCooldownBarPreview = value);
            DrawCheckbox("队伍信息预览", nameof(this.config.ShowPartyInfoPreview), this.config.ShowPartyInfoPreview, value => this.config.ShowPartyInfoPreview = value);
            ImGui.TextDisabled("队伍信息贴在原生队伍栏上，预览只显示在当前可见的队伍行（单人时即自己那一行）");
        });
    }

    private void DrawJobSkillSelector() {
        TrackedActionCatalog.EnsureActionSelectionInitialized(this.config);
        this.config.EnabledJobActionKeys ??= [];

        DrawJobActionSelector();
    }

    private void DrawSectionCard(string title, Action content) {
        var drawList = ImGui.GetWindowDrawList();
        var cardMin = ImGui.GetCursorScreenPos();
        var availWidth = Math.Max(120.0f, ImGui.GetContentRegionAvail().X);
        var titleColor = GetSectionTitleColor(title);

        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        // 标题和内容留出内边距，避免文字贴到卡片边框。
        ImGui.Dummy(new Vector2(1.0f, 6.0f));
        ImGui.Indent(10.0f);
        ImGui.PushStyleColor(ImGuiCol.Text, titleColor);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();
        ImGui.Spacing();
        content();
        ImGui.Unindent(10.0f);
        ImGui.Dummy(new Vector2(1.0f, 4.0f));

        // 卡片背景
        var cardMax = new Vector2(cardMin.X + availWidth, ImGui.GetCursorScreenPos().Y);
        var cardHeight = cardMax.Y - cardMin.Y;
        if (cardHeight > 0.0f) {
            drawList.ChannelsSetCurrent(0);
            drawList.AddRectFilled(
                cardMin + new Vector2(0.0f, 5.0f),
                cardMax + new Vector2(0.0f, 8.0f),
                ImGui.GetColorU32(new Vector4(0.35f, 0.16f, 0.22f, 0.11f)),
                7.0f);
            drawList.AddRectFilled(
                cardMin + new Vector2(0.0f, 2.0f),
                cardMax + new Vector2(0.0f, 4.0f),
                ImGui.GetColorU32(new Vector4(0.45f, 0.22f, 0.28f, 0.045f)),
                7.0f);
            drawList.AddRectFilled(
                cardMin,
                cardMax + new Vector2(0.0f, 2.0f),
                ImGui.GetColorU32(new Vector4(1.0f, 0.978f, 0.984f, 0.92f)),
                7.0f);
            drawList.AddRect(
                cardMin,
                cardMax + new Vector2(0.0f, 2.0f),
                ImGui.GetColorU32(new Vector4(0.955f, 0.700f, 0.760f, 0.42f)),
                7.0f,
                (ImDrawFlags)0,
                1.0f);
            drawList.AddLine(
                cardMin + new Vector2(10.0f, 1.0f),
                new Vector2(cardMax.X - 10.0f, cardMin.Y + 1.0f),
                ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.85f)),
                1.0f);
            drawList.AddRectFilled(
                cardMin + new Vector2(0.0f, 12.0f),
                cardMin + new Vector2(3.0f, Math.Min(cardHeight - 8.0f, 40.0f)),
                ImGui.GetColorU32(WithAlpha(titleColor, 0.82f)),
                1.5f);
        }

        drawList.ChannelsMerge();

        ImGui.Spacing();
    }

    private void DrawJobActionSelector() {
        DrawJobClassSelectorIcons();
        var selectedIndex = GetSelectedJobCatalogIndex();

        var selectedJob = TrackedActionCatalog.Jobs[selectedIndex];
        DrawJobSkillSelectorDivider();
        var jobActions = this.combatState.GetJobActionCatalog(selectedJob.ClassJobId);
        if (jobActions.Count == 0) {
            DrawSkillHintText("这个职业暂时没有读取到技能。");
            return;
        }

        var enabledActionKeys = GetEnabledActionKeySet();
        var visibleActions = FilterJobActions(jobActions, enabledActionKeys);
        DrawJobActionGroupsByCategory(visibleActions, 42.0f, "job", enabledActionKeys);
    }

    private static void DrawJobSkillSelectorDivider() {
        DrawFullContentWidthDivider(4.0f, new Vector4(0.93f, 0.58f, 0.74f, 0.22f), height: 9.0f);
    }

    private int GetSelectedJobCatalogIndex() {
        for (var index = 0; index < TrackedActionCatalog.Jobs.Count; index++) {
            if (TrackedActionCatalog.Jobs[index].ClassJobId == this.config.SelectedJobSkillConfigClassJobId) {
                return index;
            }
        }

        return 0;
    }

    private void DrawJobClassSelectorIcons() {
        DrawTargetInfoSubsection("职业");
        var selectedGroup = GetSelectedJobSkillRoleGroup();
        DrawJobSkillRoleTabs(selectedGroup);
        ImGui.Spacing();

        const float iconSize = 42.0f;
        const float spacing = 9.0f;
        var cellSize = new Vector2(iconSize, iconSize);
        var availWidth = Math.Max(120.0f, ImGui.GetContentRegionAvail().X);
        var columns = Math.Max(1, (int)((availWidth + spacing) / (iconSize + spacing)));
        var visibleJobs = TrackedActionCatalog.Jobs
            .Where(job => selectedGroup.ClassJobIds.Contains(job.ClassJobId))
            .ToList();

        for (var index = 0; index < visibleJobs.Count; index++) {
            if (index > 0 && index % columns != 0) {
                ImGui.SameLine(0.0f, spacing);
            }

            var job = visibleJobs[index];
            var selected = this.config.SelectedJobSkillConfigClassJobId == job.ClassJobId;

            ImGui.PushID($"job_class_{job.ClassJobId}");
            try {
                if (ImGui.InvisibleButton("##job_icon", cellSize) && !selected) {
                    this.config.SelectedJobSkillConfigClassJobId = job.ClassJobId;
                    selected = true;
                }

                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                DrawActionIconImage(this.combatState.GetClassJobIconId(job.ClassJobId), min, max, selected, 0.75f);
                if (selected) {
                    DrawSelectedJobIconBorder(min, max);
                }

                if (ImGui.IsItemHovered()) {
                    DrawStyledTooltip(job.Name);
                }
            }
            finally {
                ImGui.PopID();
            }
        }
    }

    private JobSkillRoleGroup GetSelectedJobSkillRoleGroup() {
        var selectedClassJobId = this.config.SelectedJobSkillConfigClassJobId;
        foreach (var group in JobSkillRoleGroups) {
            if (group.ClassJobIds.Contains(selectedClassJobId)) {
                return group;
            }
        }

        var fallback = JobSkillRoleGroups[0];
        this.config.SelectedJobSkillConfigClassJobId = fallback.ClassJobIds[0];
        return fallback;
    }

    private void DrawJobSkillRoleTabs(JobSkillRoleGroup selectedGroup) {
        var start = ImGui.GetCursorScreenPos();
        for (var index = 0; index < JobSkillRoleGroups.Length; index++) {
            if (index > 0) {
                ImGui.SameLine(0.0f, 2.0f);
            }

            DrawJobSkillRoleTab(JobSkillRoleGroups[index], selectedGroup);
        }

        var lineY = start.Y + 27.0f;
        var lineBounds = GetSectionCardLineBounds();
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(
            new Vector2(lineBounds.Left, lineY),
            new Vector2(lineBounds.Right, lineY),
            ImGui.GetColorU32(new Vector4(0.92f, 0.68f, 0.76f, 0.34f)),
            1.0f);
    }

    private void DrawJobSkillRoleTab(JobSkillRoleGroup group, JobSkillRoleGroup selectedGroup) {
        var selected = string.Equals(group.Label, selectedGroup.Label, StringComparison.Ordinal);
        var accentColor = GetPageAccentColor(ConfigPage.技能);
        var textSize = ImGui.CalcTextSize(group.Label);
        var buttonSize = new Vector2(Math.Clamp(textSize.X + 28.0f, 72.0f, 138.0f), 28.0f);
        var buttonMin = ImGui.GetCursorScreenPos();
        var buttonMax = buttonMin + buttonSize;
        var drawList = ImGui.GetWindowDrawList();

        ImGui.InvisibleButton($"job_skill_role_tab_{group.Label}", buttonSize);
        var hovered = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked() && !selected) {
            this.config.SelectedJobSkillConfigClassJobId = group.ClassJobIds[0];
            selected = true;
        }

        var fillColor = selected
            ? new Vector4(1.0f, 0.975f, 0.980f, 0.96f)
            : hovered
                ? new Vector4(1.0f, 0.945f, 0.960f, 0.82f)
                : new Vector4(1.0f, 0.955f, 0.965f, 0.58f);
        var borderColor = selected ? WithAlpha(accentColor, 0.62f) : WithAlpha(accentColor, hovered ? 0.36f : 0.22f);
        var textColor = selected ? WithAlpha(accentColor, 1.0f) : new Vector4(0.46f, 0.30f, 0.36f, 0.92f);

        drawList.AddRectFilled(buttonMin, buttonMax, ImGui.GetColorU32(fillColor), 5.0f, ImDrawFlags.RoundCornersTop);
        drawList.AddRect(buttonMin, buttonMax, ImGui.GetColorU32(borderColor), 5.0f, ImDrawFlags.RoundCornersTop, selected ? 1.2f : 1.0f);
        if (selected) {
            drawList.AddLine(
                new Vector2(buttonMin.X + 1.0f, buttonMax.Y),
                new Vector2(buttonMax.X - 1.0f, buttonMax.Y),
                ImGui.GetColorU32(new Vector4(1.0f, 0.975f, 0.980f, 1.0f)),
                2.0f);
        }

        drawList.AddText(buttonMin + (buttonSize - textSize) * 0.5f, ImGui.GetColorU32(textColor), group.Label);
    }

    private static void DrawSelectedJobIconBorder(Vector2 min, Vector2 max) {
        ImGui.GetWindowDrawList().AddRect(
            min,
            max,
            ImGui.GetColorU32(new Vector4(0.960f, 0.500f, 0.600f, 1.0f)),
            4.0f,
            (ImDrawFlags)0,
            2.0f);
    }

    private void DrawActionGroup(string title, IReadOnlyList<JobActionCatalogEntry> actions, string idPrefix, string description) {
        DrawTargetInfoSubsection(title);
        DrawSkillHintText(description);

        if (actions.Count == 0) {
            DrawSkillHintText("暂无可选技能。");
            return;
        }

        var enabledActionKeys = GetEnabledActionKeySet();
        DrawJobActionGroupsByCategory(actions, 42.0f, idPrefix, enabledActionKeys);
    }

    private void DrawJobActionGroupsByCategory(
        IReadOnlyList<JobActionCatalogEntry> actions,
        float iconSize,
        string idPrefix,
        IReadOnlySet<string> enabledActionKeys) {
        if (actions.Count == 0) {
            DrawSkillHintText("当前筛选条件下没有技能。");
            return;
        }

        var displayActions = actions
            .GroupBy(GetActionSelectionIdentity, StringComparer.Ordinal)
            .Select(GetPreferredActionEntry)
            .GroupBy(GetActionDisplayIdentity, StringComparer.Ordinal)
            .Select(GetPreferredActionEntry)
            .ToList();

        foreach (var group in GetActionCategoryOrder()) {
            var groupActions = displayActions
                .Where(action => GetActionDisplayGroup(action.Group) == group)
                .OrderBy(action => action.Level)
                .ThenBy(action => action.Name)
                .ToList();
            if (groupActions.Count == 0) {
                continue;
            }

            DrawJobActionCategoryRow(group, groupActions, iconSize, $"{idPrefix}_{group}", enabledActionKeys);
        }

    }

    private static IReadOnlyList<CooldownGroup> GetActionCategoryOrder() {
        return [
            CooldownGroup.RaidBuff,
            CooldownGroup.Burst,
            CooldownGroup.PersonalMitigation,
            CooldownGroup.Personal,
            CooldownGroup.Common,
        ];
    }

    private static void DrawActionCategoryHeader(CooldownGroup group, int count, string? labelOverride = null) {
        DrawTargetInfoSubsection($"{labelOverride ?? GetCooldownGroupLabel(group)} ({count})");
    }

    private void DrawJobActionCategoryRow(
        CooldownGroup group,
        IReadOnlyList<JobActionCatalogEntry> actions,
        float iconSize,
        string idPrefix,
        IReadOnlySet<string> enabledActionKeys) {
        const float labelWidth = 72.0f;
        const float labelGap = 8.0f;
        const float iconSpacing = 7.0f;
        const float rowGap = 7.0f;

        var start = ImGui.GetCursorScreenPos();
        var availWidth = Math.Max(labelWidth + iconSize, ImGui.GetContentRegionAvail().X);
        var iconAreaWidth = Math.Max(iconSize, availWidth - labelWidth - labelGap);
        var columns = Math.Max(1, (int)((iconAreaWidth + iconSpacing) / (iconSize + iconSpacing)));
        var rows = (int)Math.Ceiling(actions.Count / (float)columns);
        var rowHeight = rows * iconSize + Math.Max(0, rows - 1) * iconSpacing;

        DrawJobActionCategoryMarker(group, start, new Vector2(labelWidth, iconSize));

        for (var index = 0; index < actions.Count; index++) {
            var row = index / columns;
            var column = index % columns;
            var pos = start + new Vector2(labelWidth + labelGap + column * (iconSize + iconSpacing), row * (iconSize + iconSpacing));
            ImGui.SetCursorScreenPos(pos);
            DrawJobActionIcon(actions[index], new Vector2(iconSize, iconSize), idPrefix, enabledActionKeys);
        }

        ImGui.SetCursorScreenPos(start + new Vector2(0.0f, rowHeight + rowGap));
    }

    private static void DrawJobActionCategoryMarker(CooldownGroup group, Vector2 min, Vector2 size) {
        var drawList = ImGui.GetWindowDrawList();
        var max = min + size;
        var accent = GetActionCategoryColor(group);
        var label = GetCooldownGroupLabel(group);
        var textSize = ImGui.CalcTextSize(label);
        var textPos = min + (size - textSize) * 0.5f;

        drawList.AddRectFilled(min + new Vector2(1.0f, 2.0f), max + new Vector2(1.0f, 2.0f), ImGui.GetColorU32(new Vector4(0.20f, 0.08f, 0.14f, 0.10f)), 7.0f);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1.0f, 0.94f, 0.97f, 0.68f)), 7.0f);
        drawList.AddRect(min, max, ImGui.GetColorU32(WithAlpha(accent, 0.58f)), 7.0f, (ImDrawFlags)0, 1.2f);
        drawList.AddRect(min + new Vector2(2.0f), max - new Vector2(2.0f), ImGui.GetColorU32(WithAlpha(accent, 0.16f)), 5.0f, (ImDrawFlags)0, 1.0f);
        drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(0.38f, 0.20f, 0.30f, 0.98f)), label);
    }

    private static Vector4 GetActionCategoryColor(CooldownGroup group) {
        return GetActionDisplayGroup(group) switch {
            CooldownGroup.RaidBuff => new Vector4(0.58f, 0.40f, 0.95f, 1.0f),
            CooldownGroup.Burst => new Vector4(0.96f, 0.42f, 0.52f, 1.0f),
            CooldownGroup.PersonalMitigation => new Vector4(0.28f, 0.58f, 0.96f, 1.0f),
            CooldownGroup.Common => new Vector4(0.42f, 0.68f, 0.52f, 1.0f),
            _ => new Vector4(0.82f, 0.50f, 0.62f, 1.0f),
        };
    }

    private void DrawJobActionIconGrid(
        IReadOnlyList<JobActionCatalogEntry> actions,
        float iconSize,
        string idPrefix,
        IReadOnlySet<string> enabledActionKeys) {
        if (actions.Count == 0) {
            DrawSkillHintText("当前筛选条件下没有技能。");
            return;
        }

        const float spacing = 8.0f;
        var cellSize = new Vector2(iconSize, iconSize);
        var availWidth = Math.Max(120.0f, ImGui.GetContentRegionAvail().X);
        var columns = Math.Max(1, (int)((availWidth + spacing) / (iconSize + spacing)));

        var index = 0;
        foreach (var action in actions) {
            if (index > 0 && index % columns != 0) {
                ImGui.SameLine(0.0f, spacing);
            }

            DrawJobActionIcon(action, cellSize, idPrefix, enabledActionKeys);
            index++;
        }
    }

    private void DrawJobActionIcon(
        JobActionCatalogEntry action,
        Vector2 cellSize,
        string idPrefix,
        IReadOnlySet<string> enabledActionKeys) {
        var enabled = IsJobActionEnabled(action, enabledActionKeys);

        ImGui.PushID($"job_action_icon_{idPrefix}_{action.Key}");

        try {
            if (ImGui.InvisibleButton("##bg", cellSize)) {
                SetJobActionEnabled(action, !enabled);
                this.saveConfig();
                enabled = !enabled;
            }

            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var drawList = ImGui.GetWindowDrawList();
            var hovered = ImGui.IsItemHovered();

            DrawActionIconBackground(drawList, min, max, enabled, hovered);
            DrawActionIconImage(action.IconId, min, max, enabled);
            DrawActionIconFrame(drawList, min, max, enabled, hovered, cellSize.X);

            if (hovered) {
                DrawStyledTooltip(() => {
                    ImGui.TextUnformatted(action.Name);
                    ImGui.Separator();
                    ImGui.TextUnformatted($"归类：{(action.IsSharedAction ? "通用 / 职能" : "职业")}");
                    ImGui.TextUnformatted($"等级：{(action.Level == 0 ? "-" : action.Level.ToString())}");
                    ImGui.TextUnformatted($"类型：{GetCooldownGroupLabel(action.Group)}");
                    ImGui.TextUnformatted($"冷却：{FormatCooldownSeconds(action.CooldownSeconds)}");
                    ImGui.TextUnformatted($"ActionId：{action.ActionId}");
                    ImGui.TextUnformatted($"状态追踪：{(action.HasKnownStatus ? "可追状态" : "仅 CD")}");
                    ImGui.TextUnformatted($"当前：{(enabled ? "已启用" : "未启用")}");
                });
            }
        }
        finally {
            ImGui.PopID();
        }
    }

    private static void DrawActionIconBackground(ImDrawListPtr drawList, Vector2 min, Vector2 max, bool enabled, bool hovered) {
        var fill = enabled
            ? new Vector4(1.0f, 0.86f, 0.92f, hovered ? 0.88f : 0.72f)
            : new Vector4(0.30f, 0.24f, 0.30f, hovered ? 0.56f : 0.42f);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(fill), 4.0f);
    }

    private static void DrawActionIconFrame(ImDrawListPtr drawList, Vector2 min, Vector2 max, bool enabled, bool hovered, float size) {
        var thickness = Math.Max(1.0f, size * 0.05f);
        var inset = thickness * 0.5f;
        var frameMin = min + new Vector2(inset);
        var frameMax = max - new Vector2(inset);
        var border = enabled
            ? new Vector4(0.96f, 0.48f, 0.62f, 0.96f)
            : hovered
                ? new Vector4(0.96f, 0.78f, 0.86f, 0.88f)
                : new Vector4(0.78f, 0.82f, 0.90f, 0.70f);

        drawList.AddRect(frameMin, frameMax, ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.85f)), 3.0f, (ImDrawFlags)0, thickness + 1.0f);
        drawList.AddRect(frameMin, frameMax, ImGui.GetColorU32(border), 3.0f, (ImDrawFlags)0, thickness);
    }

    private void DrawActionIconImage(uint iconId, Vector2 min, Vector2 max, bool enabled, float disabledAlpha = 0.45f) {
        var drawList = ImGui.GetWindowDrawList();
        var padding = 3.0f;
        var iconMin = min + new Vector2(padding, padding);
        var iconMax = max - new Vector2(padding, padding);

        if (iconId == 0) {
            drawList.AddRectFilled(iconMin, iconMax, ImGui.GetColorU32(new Vector4(0.12f, 0.12f, 0.12f, 0.9f)), 3.0f);
            drawList.AddText(iconMin + new Vector2(7.0f, 11.0f), ImGui.GetColorU32(new Vector4(0.65f, 0.65f, 0.65f, 1.0f)), "?");
            return;
        }

        var lookup = new GameIconLookup {
            IconId = iconId,
            HiRes = true,
        };

        if (!this.textureProvider.TryGetFromGameIcon(lookup, out var texture)
            || texture is null
            || !texture.TryGetWrap(out var wrap, out _)
            || wrap is null) {
            drawList.AddRectFilled(iconMin, iconMax, ImGui.GetColorU32(new Vector4(0.12f, 0.12f, 0.12f, 0.9f)), 3.0f);
            drawList.AddText(iconMin + new Vector2(5.0f, 11.0f), ImGui.GetColorU32(new Vector4(0.75f, 0.75f, 0.75f, 1.0f)), "N/A");
            return;
        }

        var iconBounds = iconMax - iconMin;
        var drawSize = GetAspectFitSize(wrap.Size, iconBounds);
        var drawMin = iconMin + (iconBounds - drawSize) * 0.5f;
        var tint = enabled ? Vector4.One : new Vector4(1.0f, 1.0f, 1.0f, disabledAlpha);
        drawList.AddImage(wrap.Handle, drawMin, drawMin + drawSize, Vector2.Zero, Vector2.One, ImGui.GetColorU32(tint));
    }

    private static Vector2 GetAspectFitSize(Vector2 sourceSize, Vector2 bounds) {
        if (sourceSize.X <= 0.0f || sourceSize.Y <= 0.0f) {
            return bounds;
        }

        var scale = Math.Min(bounds.X / sourceSize.X, bounds.Y / sourceSize.Y);
        return new Vector2(sourceSize.X * scale, sourceSize.Y * scale);
    }

    private List<JobActionCatalogEntry> FilterJobActions(
        IEnumerable<JobActionCatalogEntry> actions,
        IReadOnlySet<string> enabledActionKeys) {
        var result = new List<JobActionCatalogEntry>();
        foreach (var action in actions) {
            if (HiddenActionSelectionNames.Contains(action.Name.Trim())) {
                continue;
            }

            if (TrackedActionCatalog.PartyMitigationActionIds.Contains(action.ActionId)) {
                continue;
            }

            if (!IsVisibleAction(action)) {
                continue;
            }

            result.Add(action);
        }

        return result;
    }

    private static string GetActionSelectionIdentity(JobActionCatalogEntry action) {
        var knownSkill = TrackedActionCatalog.FindKnownSkill(action.ClassJobId, action.ActionId)
            ?? (action.ClassJobId == 0 ? TrackedActionCatalog.FindCommonSkill(action.ActionId) : null);
        var canonicalActionId = knownSkill?.Definition.ActionIds.FirstOrDefault(actionId => actionId != 0) ?? 0;
        return canonicalActionId == 0
            ? action.Key
            : TrackedActionCatalog.GetActionKey(action.ClassJobId, canonicalActionId);
    }

    private static string GetActionDisplayIdentity(JobActionCatalogEntry action) {
        return action.CooldownGroupId != 0 && GetActionDisplayGroup(action.Group) == CooldownGroup.Personal
            ? $"{action.ClassJobId}:display:{action.CooldownGroupId}"
            : action.Key;
    }

    private static JobActionCatalogEntry GetPreferredActionEntry(IGrouping<string, JobActionCatalogEntry> group) {
        return group
            .OrderByDescending(action => action.Level)
            .ThenByDescending(action => string.Equals(action.Key, group.Key, StringComparison.Ordinal))
            .First();
    }

    private static bool IsVisibleCooldownGroup(CooldownGroup group) {
        // 剔除团减（PartyMitigation/TargetMitigation），保留团辅、单体减伤、爆发、其他长 CD。
        return GetActionDisplayGroup(group) is CooldownGroup.RaidBuff
            or CooldownGroup.Burst
            or CooldownGroup.PersonalMitigation
            or CooldownGroup.Personal;
    }

    private static bool IsVisibleAction(JobActionCatalogEntry action) {
        return IsVisibleCooldownGroup(action.Group)
               || (action.Group == CooldownGroup.Common
                   && TrackedActionCatalog.IndependentMonitorCommonActionIds.Contains(action.ActionId));
    }

    private bool IsMergedMitigationCooldownsEnabled() {
        return this.config.ShowPartyMitigationCooldowns
               || this.config.ShowTargetMitigationCooldowns
               || this.config.ShowPersonalMitigationCooldowns
               || this.config.ShowMitigationCooldowns;
    }

    private IReadOnlySet<string> GetEnabledActionKeySet() {
        return (this.config.EnabledJobActionKeys ?? []).ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsJobActionEnabled(JobActionCatalogEntry action, IReadOnlySet<string> enabledActionKeys) {
        return GetSameSkillActionKeys(action).Any(enabledActionKeys.Contains);
    }

    private void SetJobActionEnabled(JobActionCatalogEntry action, bool enabled) {
        this.config.EnabledJobActionKeys ??= [];
        var keys = GetSameSkillActionKeys(action).ToList();
        this.config.EnabledJobActionKeys.RemoveAll(existing => keys.Contains(existing, StringComparer.Ordinal));
        if (enabled) {
            this.config.EnabledJobActionKeys.Add(keys[0]);
        }
    }

    private static IEnumerable<string> GetSameSkillActionKeys(JobActionCatalogEntry action) {
        var knownSkill = TrackedActionCatalog.FindKnownSkill(action.ClassJobId, action.ActionId)
            ?? (action.ClassJobId == 0 ? TrackedActionCatalog.FindCommonSkill(action.ActionId) : null);
        if (knownSkill is not null) {
            return knownSkill.Definition.ActionIds
                .Where(actionId => actionId != 0)
                .Distinct()
                .Select(actionId => TrackedActionCatalog.GetActionKey(action.ClassJobId, actionId));
        }

        return [action.Key];
    }

    private static string FormatCooldownSeconds(float seconds) {
        return seconds <= 0.05f ? "-" : $"{seconds:0.#}s";
    }

    private void SetJobSkillEnabled(string key, bool enabled) {
        this.config.EnabledJobSkillKeys ??= [];
        this.config.EnabledJobSkillKeys.RemoveAll(existing => string.Equals(existing, key, StringComparison.Ordinal));
        if (enabled) {
            this.config.EnabledJobSkillKeys.Add(key);
        }
    }

    private static string GetCooldownGroupLabel(CooldownGroup group) {
        return NormalizeCooldownGroup(group) switch {
            CooldownGroup.Common => "通用",
            CooldownGroup.Personal => "技能",
            CooldownGroup.Burst => "爆发",
            CooldownGroup.PartyMitigation => "团队减伤",
            CooldownGroup.PersonalMitigation => "单体减伤",
            CooldownGroup.RaidBuff => "团辅",
            CooldownGroup.Mitigation => "减伤",
            _ => "其他",
        };
    }

    private static CooldownGroup NormalizeCooldownGroup(CooldownGroup group) {
        return group == CooldownGroup.TargetMitigation ? CooldownGroup.PartyMitigation : group;
    }

    private static CooldownGroup GetActionDisplayGroup(CooldownGroup group) {
        var normalized = NormalizeCooldownGroup(group);
        return normalized switch {
            // 团减保持独立（后续会被剔除）；单体减伤与泛减伤合并到单体减伤分类。
            CooldownGroup.PartyMitigation => CooldownGroup.PartyMitigation,
            CooldownGroup.PersonalMitigation or CooldownGroup.Mitigation => CooldownGroup.PersonalMitigation,
            _ => normalized,
        };
    }

    private static void DrawSkillHintText(string text) {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.48f, 0.36f, 0.54f, 0.82f));
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }
}
