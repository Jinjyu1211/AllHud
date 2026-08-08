using AllHud.Data;
using AllHud.Models;
using AllHud.Services;
using Dalamud.Game.Addon.Events;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Config;
using Dalamud.Game.Inventory;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using LuminaGeneralAction = Lumina.Excel.Sheets.GeneralAction;
using LuminaItem = Lumina.Excel.Sheets.Item;
using LuminaClassJob = Lumina.Excel.Sheets.ClassJob;
using LuminaMainCommand = Lumina.Excel.Sheets.MainCommand;
using LuminaMainCommandCategory = Lumina.Excel.Sheets.MainCommandCategory;
using LuminaTerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace AllHud.Windows;

public sealed partial class OverlayRenderer {
    private readonly record struct SelfCooldownVisibleGroup(PartyCooldownGroupEntry Entry, IReadOnlyList<CooldownEntry> Cooldowns);
    private sealed record SelfCooldownLayoutCacheKey(
        IReadOnlyList<PartyCooldownGroupEntry> Groups,
        bool HideWhenReady,
        bool Preview,
        int LayoutDirection,
        float IconSize,
        float Spacing,
        float RowGap,
        float Pad,
        float ViewportWidth);

    private sealed record SelfCooldownLayoutCache(IReadOnlyList<SelfCooldownVisibleGroup> VisibleGroups, IReadOnlyList<SelfCooldownHorizontalRow> HorizontalRows);

    private SelfCooldownLayoutCacheKey? cachedSelfCooldownLayoutKey;
    private SelfCooldownLayoutCache? cachedSelfCooldownLayout;

    private void DrawSelfCooldownBarWindow(ImGuiWindowFlags flags) {
        if (!this.config.ShowSelfCooldownBar) {
            return;
        }

        if (this.config.ShowSelfCooldownBarPreview) {
            DrawSelfCooldownBarPreviewWindow(flags);
            return;
        }

        DrawNativeAttachedSelfCooldown();
    }

    private unsafe void DrawNativeAttachedSelfCooldown() {
        var groups = this.combatState.GetPartyCooldownTracking(this.config);
        if (groups.Count == 0) {
            return;
        }

        // 当队伍栏减伤已显示时，独立监控栏需过滤掉"纯减伤"技能，避免在同一位置（职业图标左侧）重复绘制。
        // 保留用户勾选的 RaidBuff/Burst/PersonalMitigation 等非纯减伤内容。
        var suppressMitigation = IsMitigationCooldownsVisible();
        if (suppressMitigation) {
            groups = groups
                .Select(group => new PartyCooldownGroupEntry(
                    group.SourceName,
                    group.SourceJobName,
                    group.SourceClassJobId,
                    group.SourceJobIconId,
                    group.SourceObjectId,
                    group.SourceEntityId,
                    group.IsLocalPlayer,
                    group.PartySlot,
                    group.Cooldowns
                        .Where(cooldown => !IsMitigationCooldownEntry(cooldown))
                        .ToList()))
                .Where(group => group.Cooldowns.Count > 0)
                .ToList();
            if (groups.Count == 0) {
                return;
            }
        }

        var addonPtr = this.gameGui.GetAddonByName("_PartyList");
        if (addonPtr.IsNull) {
            return;
        }

        var addon = (AddonPartyList*)addonPtr.Address;
        if (!IsNativePartyListVisible(addon)) {
            return;
        }

        var memberCount = Math.Clamp(addon->MemberCount, 0, 8);
        if (memberCount <= 0) {
            return;
        }

        var partyArray = PartyListNumberArray.Instance();
        if (partyArray is null) {
            return;
        }

        const float iconSize = 36.0f;
        const float iconGap = 5.0f;
        const float attachGap = 16.0f;

        var anchorSnapshot = GetNativePartyAnchorSnapshot(addon, memberCount, iconSize);
        var anchors = anchorSnapshot.Anchors;
        var fallbackJobIconLeft = anchorSnapshot.FallbackJobIconLeft;
        var fallbackJobIconHeight = anchorSnapshot.FallbackJobIconHeight;

        // 仅使用 entityId / objectId 匹配，partySlot 因 GetPartyMembers 过滤无CD成员导致索引不对应原生列表，不可靠
        var groupsByEntityId = new Dictionary<uint, PartyCooldownGroupEntry>();
        var groupsByObjectId = new Dictionary<ulong, PartyCooldownGroupEntry>();
        foreach (var group in groups) {
            if (group.SourceEntityId != 0) {
                groupsByEntityId.TryAdd(group.SourceEntityId, group);
            }

            if (group.SourceObjectId != 0) {
                groupsByObjectId.TryAdd(group.SourceObjectId, group);
            }
        }

        var drawList = ImGui.GetBackgroundDrawList();

        for (var nativeIndex = 0; nativeIndex < memberCount; nativeIndex++) {
            ref var nativeData = ref partyArray->PartyMembers[nativeIndex];
            if (nativeData.MaxHealth <= 0) {
                continue;
            }

            var entityId = nativeData.EntityId;
            var hasGroup = groupsByEntityId.TryGetValue(entityId, out var group)
                || groupsByObjectId.TryGetValue(entityId, out group);

            if (!hasGroup || group is null) {
                continue;
            }

            if (anchors[nativeIndex] is not { } anchor) {
                continue;
            }

            var cooldowns = this.config.SelfCooldownBarHideWhenReady
                ? group.Cooldowns.Where(cd => !cd.IsReady).ToList()
                : group.Cooldowns;
            if (cooldowns.Count == 0) {
                continue;
            }

            var rowMin = anchor.RowMin;
            var rowH = anchor.RowH;
            if (!anchor.HasJobIconAnchor && fallbackJobIconLeft is { } jobIconLeft) {
                var rowCenterY = rowMin.Y + rowH * 0.5f;
                rowMin = new Vector2(jobIconLeft, rowCenterY - fallbackJobIconHeight * 0.5f);
                rowH = fallbackJobIconHeight;
            }

            var iconGroupW = cooldowns.Count * iconSize + Math.Max(0, cooldowns.Count - 1) * iconGap;
            var x = rowMin.X - attachGap - iconGroupW;
            var y = rowMin.Y + (rowH - iconSize) * 0.5f;
            for (var i = 0; i < cooldowns.Count; i++) {
                DrawNativeAttachedCooldownIcon(drawList, new Vector2(x + i * (iconSize + iconGap), y), cooldowns[i], iconSize);
            }
        }
    }

    private void DrawSelfCooldownBarPreviewWindow(ImGuiWindowFlags flags) {
        var groups = this.combatState.GetPartyCooldownTrackingPreview(this.config);
        if (groups.Count == 0) {
            return;
        }

        var horizontalLayout = this.config.SelfCooldownBarLayoutDirection == 1;
        const float iconSize = 36.0f;
        var iconGap = MathF.Round(5.0f * this.config.SelfCooldownBarScale);
        var rowGap = MathF.Round(5.0f * this.config.SelfCooldownBarScale);
        var pad = MathF.Round(8.0f * this.config.SelfCooldownBarScale);
        var rowHeight = MathF.Round(46.0f * this.config.SelfCooldownBarScale);
        var scale = this.config.SelfCooldownBarScale;

        var layout = GetSelfCooldownLayoutCache(groups, horizontalLayout, iconSize, iconGap, rowGap, pad);
        var visibleGroups = layout.VisibleGroups;
        if (visibleGroups.Count == 0) {
            return;
        }

        var windowWidth = horizontalLayout
            ? Math.Min(ImGui.GetMainViewport().WorkSize.X - pad * 2.0f, layout.HorizontalRows.Max(row => row.Width) + pad * 2.0f + 32.0f)
            : ImGui.GetMainViewport().WorkSize.X * 0.26f;
        var rowCount = horizontalLayout ? layout.HorizontalRows.Count : visibleGroups.Count;
        var windowHeight = pad + rowCount * rowHeight + Math.Max(0, rowCount - 1) * rowGap + pad;

        ImGui.SetNextWindowSize(new Vector2(windowWidth, windowHeight));
        if (this.config.SelfCooldownBarLocked) {
            ImGui.SetNextWindowPos(this.config.SelfCooldownBarPosition);
        }

        if (!ImGui.Begin("###AllHudSelfCooldownBar", flags)) {
            ImGui.End();
            return;
        }

        TrackSelfCooldownBarPosition();

        var drawList = ImGui.GetWindowDrawList();

        if (horizontalLayout) {
            DrawSelfCooldownBarHorizontalLayout(drawList, layout.HorizontalRows, pad, scale, rowHeight, iconSize, iconGap);
        } else {
            DrawSelfCooldownBarVerticalLayout(drawList, visibleGroups, pad, scale, rowHeight, iconSize, iconGap);
        }

        ImGui.End();
    }

    private readonly record struct SelfCooldownHorizontalRow(IReadOnlyList<SelfCooldownVisibleGroup> Groups, float Width);

    private SelfCooldownLayoutCache GetSelfCooldownLayoutCache(IReadOnlyList<PartyCooldownGroupEntry> groups, bool horizontalLayout, float iconSize, float spacing, float rowGap, float pad) {
        var viewportWidth = ImGui.GetMainViewport().WorkSize.X;
        var key = new SelfCooldownLayoutCacheKey(
            groups,
            this.config.SelfCooldownBarHideWhenReady,
            this.config.ShowSelfCooldownBarPreview,
            horizontalLayout ? 1 : 0,
            iconSize,
            spacing,
            rowGap,
            pad,
            MathF.Round(viewportWidth));
        if (this.cachedSelfCooldownLayoutKey == key && this.cachedSelfCooldownLayout is not null) {
            return this.cachedSelfCooldownLayout;
        }

        var visibleGroups = new List<SelfCooldownVisibleGroup>(groups.Count);
        foreach (var group in groups) {
            if (this.config.SelfCooldownBarHideWhenReady) {
                List<CooldownEntry>? visibleCooldowns = null;
                foreach (var cooldown in group.Cooldowns) {
                    if (cooldown.IsReady) {
                        continue;
                    }

                    visibleCooldowns ??= new List<CooldownEntry>(group.Cooldowns.Count);
                    visibleCooldowns.Add(cooldown);
                }

                if (visibleCooldowns is not null && visibleCooldowns.Count > 0) {
                    visibleGroups.Add(new SelfCooldownVisibleGroup(group, visibleCooldowns));
                }
            } else if (group.Cooldowns.Count > 0) {
                visibleGroups.Add(new SelfCooldownVisibleGroup(group, group.Cooldowns));
            }
        }

        if (!this.config.ShowSelfCooldownBarPreview) {
            SortSelfCooldownGroupsByNativePartyList(visibleGroups);
        }

        var horizontalRows = horizontalLayout
            ? BuildSelfCooldownHorizontalRows(visibleGroups, iconSize, spacing, rowGap, pad)
            : Array.Empty<SelfCooldownHorizontalRow>();
        this.cachedSelfCooldownLayoutKey = key;
        this.cachedSelfCooldownLayout = new SelfCooldownLayoutCache(visibleGroups, horizontalRows);
        return this.cachedSelfCooldownLayout;
    }

    private static float GetSelfCooldownHorizontalGroupWidth(SelfCooldownVisibleGroup group, float iconSize, float iconGap) {
        var nameWidth = ImGui.CalcTextSize(group.Entry.SourceName).X;
        var jobNameWidth = ImGui.CalcTextSize(group.Entry.SourceJobName).X;
        return 48.0f + Math.Max(nameWidth, jobNameWidth) + 8.0f
               + group.Cooldowns.Count * iconSize + Math.Max(0, group.Cooldowns.Count - 1) * iconGap;
    }

    private static IReadOnlyList<SelfCooldownHorizontalRow> BuildSelfCooldownHorizontalRows(IReadOnlyList<SelfCooldownVisibleGroup> visibleGroups, float iconSize, float spacing, float groupGap, float pad) {
        var viewport = ImGui.GetMainViewport();
        var maxContentWidth = Math.Max(iconSize, viewport.WorkSize.X - pad * 2.0f - 32.0f);
        var rows = new List<SelfCooldownHorizontalRow>();
        var currentGroups = new List<SelfCooldownVisibleGroup>();
        var currentWidth = 0.0f;

        foreach (var group in visibleGroups) {
            var groupWidth = GetSelfCooldownHorizontalGroupWidth(group, iconSize, spacing);
            var nextWidth = currentGroups.Count == 0 ? groupWidth : currentWidth + groupGap + groupWidth;
            if (currentGroups.Count > 0 && nextWidth > maxContentWidth) {
                rows.Add(new SelfCooldownHorizontalRow(currentGroups, currentWidth));
                currentGroups = new List<SelfCooldownVisibleGroup>();
                currentWidth = 0.0f;
                nextWidth = groupWidth;
            }

            currentGroups.Add(group);
            currentWidth = nextWidth;
        }

        if (currentGroups.Count > 0) {
            rows.Add(new SelfCooldownHorizontalRow(currentGroups, currentWidth));
        }

        return rows;
    }

    private void DrawSelfCooldownBarVerticalLayout(ImDrawListPtr drawList, IReadOnlyList<SelfCooldownVisibleGroup> visibleGroups, float pad, float scale, float rowHeight, float iconSize, float iconGap) {
        for (var rowIndex = 0; rowIndex < visibleGroups.Count; rowIndex++) {
            var group = visibleGroups[rowIndex];
            var rowMin = ImGui.GetWindowPos() + new Vector2(pad, pad + rowIndex * (rowHeight + MathF.Round(5.0f * scale)));
            var rowMax = rowMin + new Vector2(ImGui.GetWindowWidth() - pad * 2.0f, rowHeight);
            DrawSelfCooldownBarRowBackground(drawList, rowMin, rowMax, scale);

            // 职业图标
            var jobIconMin = rowMin + new Vector2(6.0f, 6.0f);
            var jobIconMax = jobIconMin + new Vector2(34.0f, 34.0f);
            DrawGameIconImage(drawList, group.Entry.SourceJobIconId, jobIconMin, jobIconMax, true, true);

            // 玩家名 + 职业名
            var textX = jobIconMax.X + 8.0f;
            drawList.AddText(new Vector2(textX, rowMin.Y + 6.0f), ImGui.GetColorU32(new Vector4(0.98f, 0.95f, 0.96f, 1.0f)), group.Entry.SourceName);
            drawList.AddText(new Vector2(textX, rowMin.Y + 23.0f), ImGui.GetColorU32(new Vector4(0.72f, 0.56f, 0.62f, 0.90f)), group.Entry.SourceJobName);

            // CD 图标
            var cooldowns = group.Cooldowns;
            if (cooldowns.Count == 0) {
                continue;
            }

            var iconsWidth = cooldowns.Count * iconSize + Math.Max(0, cooldowns.Count - 1) * iconGap;
            var iconX = Math.Max(textX + 72.0f, rowMax.X - iconsWidth - 8.0f);
            var iconY = rowMin.Y + (rowHeight - iconSize) * 0.5f;
            for (var cooldownIndex = 0; cooldownIndex < cooldowns.Count; cooldownIndex++) {
                DrawNativeAttachedCooldownIcon(drawList, new Vector2(iconX + cooldownIndex * (iconSize + iconGap), iconY), cooldowns[cooldownIndex], iconSize);
            }
        }
    }

    private void DrawSelfCooldownBarHorizontalLayout(ImDrawListPtr drawList, IReadOnlyList<SelfCooldownHorizontalRow> rows, float pad, float scale, float rowHeight, float iconSize, float iconGap) {
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
            var row = rows[rowIndex];
            var groupX = pad;
            var rowY = pad + rowIndex * (rowHeight + MathF.Round(5.0f * scale));
            foreach (var group in row.Groups) {
                var rowMin = ImGui.GetWindowPos() + new Vector2(groupX, rowY);
                var rowMax = rowMin + new Vector2(GetSelfCooldownHorizontalGroupWidth(group, iconSize, iconGap), rowHeight);
                DrawSelfCooldownBarRowBackground(drawList, rowMin, rowMax, scale);

                // 职业图标
                var jobIconMin = rowMin + new Vector2(6.0f, 6.0f);
                var jobIconMax = jobIconMin + new Vector2(34.0f, 34.0f);
                DrawGameIconImage(drawList, group.Entry.SourceJobIconId, jobIconMin, jobIconMax, true, true);

                // 玩家名 + 职业名
                var textX = jobIconMax.X + 8.0f;
                drawList.AddText(new Vector2(textX, rowMin.Y + 6.0f), ImGui.GetColorU32(new Vector4(0.98f, 0.95f, 0.96f, 1.0f)), group.Entry.SourceName);
                drawList.AddText(new Vector2(textX, rowMin.Y + 23.0f), ImGui.GetColorU32(new Vector4(0.72f, 0.56f, 0.62f, 0.90f)), group.Entry.SourceJobName);

                // CD 图标
                var cooldowns = group.Cooldowns;
                if (cooldowns.Count == 0) {
                    groupX = rowMax.X - ImGui.GetWindowPos().X + MathF.Round(5.0f * scale);
                    continue;
                }

                var iconsWidth = cooldowns.Count * iconSize + Math.Max(0, cooldowns.Count - 1) * iconGap;
                var iconX = Math.Max(textX + 72.0f, rowMax.X - iconsWidth - 8.0f);
                var iconY = rowMin.Y + (rowHeight - iconSize) * 0.5f;
                for (var cooldownIndex = 0; cooldownIndex < cooldowns.Count; cooldownIndex++) {
                    DrawNativeAttachedCooldownIcon(drawList, new Vector2(iconX + cooldownIndex * (iconSize + iconGap), iconY), cooldowns[cooldownIndex], iconSize);
                }

                groupX = rowMax.X - ImGui.GetWindowPos().X + MathF.Round(5.0f * scale);
            }
        }
    }

    private static void DrawSelfCooldownBarRowBackground(ImDrawListPtr drawList, Vector2 rowMin, Vector2 rowMax, float scale) {
        var rounding = MathF.Round(4.0f * scale);
        drawList.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(new Vector4(0.14f, 0.11f, 0.16f, 0.78f)), rounding);
        drawList.AddRect(rowMin, rowMax, ImGui.GetColorU32(new Vector4(0.35f, 0.22f, 0.28f, 0.72f)), rounding, (ImDrawFlags)0, 1.0f);
    }

    private unsafe void SortSelfCooldownGroupsByNativePartyList(List<SelfCooldownVisibleGroup> groups) {
        var memberOrder = GetNativePartyMemberOrder();
        if (memberOrder.Count == 0) {
            return;
        }

        var indexByEntityId = new Dictionary<uint, int>(memberOrder.Count);
        for (var index = 0; index < memberOrder.Count; index++) {
            indexByEntityId[memberOrder[index]] = index;
        }

        groups.Sort((left, right) => {
            var order = GetNativePartyMemberOrderIndex(left.Entry, indexByEntityId)
                .CompareTo(GetNativePartyMemberOrderIndex(right.Entry, indexByEntityId));
            return order != 0 ? order : left.Entry.PartySlot.CompareTo(right.Entry.PartySlot);
        });
    }

    private static int GetNativePartyMemberOrderIndex(PartyCooldownGroupEntry entry, IReadOnlyDictionary<uint, int> indexByEntityId) {
        if (entry.SourceEntityId != 0 && indexByEntityId.TryGetValue(entry.SourceEntityId, out var entityIndex)) {
            return entityIndex;
        }

        if (entry.SourceObjectId <= uint.MaxValue && indexByEntityId.TryGetValue((uint)entry.SourceObjectId, out var objectIndex)) {
            return objectIndex;
        }

        return int.MaxValue;
    }

    private unsafe IReadOnlyList<uint> GetNativePartyMemberOrder() {
        var addonPtr = this.gameGui.GetAddonByName("_PartyList");
        if (addonPtr.IsNull) {
            return [];
        }

        var addon = (AddonPartyList*)addonPtr.Address;
        if (!IsNativePartyListVisible(addon)) {
            return [];
        }

        var memberCount = Math.Clamp(addon->MemberCount, 0, 8);
        if (memberCount <= 0) {
            return [];
        }

        var partyArray = PartyListNumberArray.Instance();
        if (partyArray is null) {
            return [];
        }

        var entityIds = new List<uint>(memberCount);
        for (var nativeIndex = 0; nativeIndex < memberCount; nativeIndex++) {
            ref var nativeData = ref partyArray->PartyMembers[nativeIndex];
            if (nativeData.MaxHealth > 0 && nativeData.EntityId != 0) {
                entityIds.Add(nativeData.EntityId);
            }
        }

        return entityIds;
    }

    private static void DrawSelfCooldownBarCard(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale, float opacity) {
        var rounding = MathF.Round(8.0f * scale);
        drawList.AddRectFilled(min + new Vector2(1.0f, 2.0f), max + new Vector2(1.0f, 2.0f), ImGui.GetColorU32(new Vector4(0.20f, 0.08f, 0.14f, 0.10f * opacity)), rounding);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1.0f, 0.94f, 0.97f, 0.28f * opacity)), rounding);
        drawList.AddRect(min, max, ImGui.GetColorU32(new Vector4(0.96f, 0.58f, 0.78f, 0.36f * opacity)), rounding, (ImDrawFlags)0, Math.Max(1.0f, 1.0f * scale));
    }

    private void TrackSelfCooldownBarPosition() {
        if (this.config.SelfCooldownBarLocked) {
            return;
        }

        var currentPosition = ImGui.GetWindowPos();
        if (currentPosition != this.config.SelfCooldownBarPosition) {
            this.config.SelfCooldownBarPosition = currentPosition;
            this.selfCooldownBarPositionSaveDueAt = DateTime.UtcNow.Add(OverlayPositionSaveDelay);
        }

        if (this.selfCooldownBarPositionSaveDueAt is not { } saveDueAt
            || DateTime.UtcNow < saveDueAt
            || this.config.SelfCooldownBarPosition == this.lastSavedSelfCooldownBarPosition) {
            return;
        }

        this.saveConfig();
        this.lastSavedSelfCooldownBarPosition = this.config.SelfCooldownBarPosition;
        this.selfCooldownBarPositionSaveDueAt = null;
    }

    // 与 OverlayRenderer.GetNativeAttachedMitigationCooldowns 的筛选标准保持一致：
    // 归为 PartyMitigation / Mitigation 的分组，或 ActionId 在队伍额外减伤白名单中的条目，
    // 会在"队伍信息减伤栏"展示，因此在"独立监控栏"中需剔除以避免同位置重复绘制。
    private static bool IsMitigationCooldownEntry(CooldownEntry cooldown) {
        var normalized = cooldown.Group == CooldownGroup.TargetMitigation
            ? CooldownGroup.PartyMitigation
            : cooldown.Group;
        return normalized is CooldownGroup.PartyMitigation or CooldownGroup.Mitigation
               || TrackedActionCatalog.PartyInfoExtraMitigationActionIds.Contains(cooldown.ActionId);
    }
}
