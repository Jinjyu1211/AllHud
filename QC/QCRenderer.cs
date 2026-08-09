using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace AllHud;

public sealed class QCRenderer : IDisposable {
    private readonly QCManager manager;
    private readonly Configuration config;
    private readonly ITextureProvider textureProvider;
    private readonly Dictionary<uint, IDalamudTextureWrap?> iconCache = [];
    // Cached path buffer for pie wedge drawing to avoid per-frame allocation
    private Vector2[]? pieWedgePath;
    // Cached unpacked colors
    private readonly Dictionary<uint, Vector4> colorCache = [];
    // Slide animation tracking
    private readonly Dictionary<string, SlideAnimation> barAnimations = [];

    // Click animation tracking (QoLBar-style: expanding circle on click)
    private readonly Dictionary<string, ClickAnimation> clickAnimations = [];
    private sealed class ClickAnimation {
        public float Time;
        public bool Active;
    }

    private sealed class SlideAnimation {
        public double Progress; // 0.0 = hidden, 1.0 = fully visible
        public bool IsHovered;
        public double HoverTimer;
    }

    public QCRenderer(QCManager manager, Configuration config, ITextureProvider textureProvider,
        IDalamudPluginInterface pluginInterface) {
        this.manager = manager;
        this.config = config;
        this.textureProvider = textureProvider;
    }

    public void Dispose() {
        ClearIconCache();
        this.colorCache.Clear();
        this.barAnimations.Clear();
        this.clickAnimations.Clear();
    }

    // QoLBar-style: RotateVector helper
    private static Vector2 RotateVector(Vector2 v, float aCos, float aSin) =>
        new(v.X * aCos - v.Y * aSin, v.X * aSin + v.Y * aCos);

    public void DrawAllBars() {
        var displaySize = ImGui.GetIO().DisplaySize;

        for (var index = 0; index < this.manager.Bars.Count; index++) {
            var bar = this.manager.Bars[index];
            if (!bar.Enabled || !this.manager.IsBarVisible(bar)) continue;

            var shortcuts = GetVisibleShortcuts(bar);
            if (shortcuts.Count == 0 && !bar.IsPieMenu) {
                if (bar.HideWhenEmpty) continue;
            }

            if (bar.IsPieMenu) {
                DrawPieMenu(bar, shortcuts, index);
                continue;
            }

            DrawBar(bar, shortcuts, index, displaySize);
        }
    }

    private List<QCShortcutDefinition> GetVisibleShortcuts(QCBarDefinition bar) {
        var result = new List<QCShortcutDefinition>(bar.ShortcutIds.Count);
        foreach (var sid in bar.ShortcutIds) {
            if (this.manager.Shortcuts.TryGetValue(sid, out var shortcut)) {
                result.Add(shortcut);
            }
        }
        return result;
    }

    private void DrawBar(QCBarDefinition bar, List<QCShortcutDefinition> shortcuts, int index, Vector2 displaySize) {
        var scale = Math.Clamp(bar.Scale, 0.4f, 2.0f);
        var opacity = Math.Clamp(bar.Opacity, 0.15f, 1.0f);
        var buttonSize = MathF.Max(1.0f, MathF.Round(44.0f * scale * (bar.ButtonWidth / 100.0f)));
        var spacing = bar.Spacing;

        var columns = bar.Columns > 0 ? bar.Columns : (bar.Horizontal ? shortcuts.Count : 1);
        var rows = bar.Columns > 0 ? (int)MathF.Ceiling((float)shortcuts.Count / bar.Columns) : 1;

        // For horizontal non-grid, override
        if (bar.Columns <= 0 && bar.Horizontal) {
            columns = shortcuts.Count;
            rows = 1;
        } else if (bar.Columns <= 0 && !bar.Horizontal) {
            columns = 1;
            rows = shortcuts.Count;
        }

        var padding = MathF.Round(7.0f * scale);
        var contentWidth = columns * buttonSize + Math.Max(0, columns - 1) * spacing.X + padding * 2.0f;
        var contentHeight = rows * buttonSize + Math.Max(0, rows - 1) * spacing.Y + padding * 2.0f;

        var pos = GetBarPosition(bar, displaySize, contentWidth, contentHeight);
        var size = new Vector2(contentWidth, contentHeight);

        // Handle visibility modes
        var finalOpacity = opacity;
        var finalPos = pos;
        if (bar.VisibilityMode == 0) {
            // Slide mode - time-based animation from edge
            var anim = GetOrCreateAnimation(bar.Id);
            UpdateSlideAnimation(bar, anim, pos, size, displaySize);
            finalPos = GetAnimatedSlidePosition(bar, pos, size, displaySize, anim);
            finalOpacity = opacity * (float)anim.Progress;
        }

        // Handle hint when bar is hidden via visibility mode
        if (bar.VisibilityMode == 0 && bar.Hint) {
            DrawHint(bar, pos, size, displaySize, scale);
        }

        var customPosition = bar.PositionMode == 2;
        var cond = customPosition ? ImGuiCond.Once : ImGuiCond.Always;
        ImGui.SetNextWindowPos(SnapToPixel(finalPos), cond);
        ImGui.SetNextWindowSize(SnapToPixel(size), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        var flags = ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration
                    | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar
                    | ImGuiWindowFlags.NoFocusOnAppearing;
        if (!customPosition && !bar.LockedPosition) flags |= ImGuiWindowFlags.NoMove;
        if (bar.ClickThrough) flags |= ImGuiWindowFlags.NoInputs;

        if (!ImGui.Begin($"QC快捷栏_{index}", flags)) {
            ImGui.End();
            ImGui.PopStyleVar(2);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var windowMin = ImGui.GetWindowPos();
        var windowMax = windowMin + size;
        var rounding = MathF.Round(6.0f * scale);

        if (!bar.NoBackground) {
            DrawBarBackground(drawList, windowMin, windowMax, rounding, finalOpacity, scale);
        }

        // Draw shortcuts in grid
        for (var i = 0; i < shortcuts.Count; i++) {
            var shortcut = shortcuts[i];
            var col = i % columns;
            var row = i / columns;

            var x = windowMin.X + padding + col * (buttonSize + spacing.X);
            var y = windowMin.Y + padding + row * (buttonSize + spacing.Y);

            var buttonMin = SnapToPixel(new Vector2(x, y));
            var buttonMax = SnapToPixel(new Vector2(x + buttonSize, y + buttonSize));

            var id = $"##qc_btn_{index}_{i}";
            ImGui.SetCursorScreenPos(buttonMin);
            ImGui.InvisibleButton(id, new Vector2(buttonSize, buttonSize));
            var hovered = ImGui.IsItemHovered();
            var active = ImGui.IsItemActive();

            DrawShortcutButton(drawList, buttonMin, buttonMax, shortcut, hovered, active, finalOpacity, scale, id);

            if (hovered && !string.IsNullOrWhiteSpace(shortcut.Tooltip)) {
                ImGui.SetTooltip(shortcut.Tooltip);
            }

            // QoLBar 兼容: Spacer 类型的按钮不接受点击
            if (shortcut.Type == QCShortcutType.Spacer) {
                // 空操作 - Spacer 仅用于占位
            } else if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                if (shortcut.IsCategory) {
                    // QoLBar 兼容: 如果有命令的分类,点击时先执行命令再打开弹出菜单
                    if (!string.IsNullOrWhiteSpace(shortcut.Command)) {
                        this.manager.ExecuteShortcut(shortcut);
                    }
                    ImGui.OpenPopup($"QC_cat_popup_{index}_{i}");
                } else {
                    // QoLBar-style: trigger click animation
                    if (this.clickAnimations.TryGetValue(id, out var clickAnim)) {
                        clickAnim.Active = true;
                        clickAnim.Time = 0;
                    } else {
                        this.clickAnimations[id] = new ClickAnimation { Active = true, Time = 0 };
                    }
                    this.manager.ExecuteShortcut(shortcut);
                }
            }

            // Category popup
            if (shortcut.IsCategory) {
                DrawCategoryPopup(shortcut, index, i, scale);
            }
        }

        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    private static void DrawBarBackground(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float opacity, float scale) {
        // Shadow
        drawList.AddRectFilled(min + new Vector2(0.0f, 2.0f * scale), max + new Vector2(0.0f, 2.0f * scale),
            ImGui.GetColorU32(new Vector4(0.36f, 0.14f, 0.25f, opacity * 0.14f)), rounding);
        // Main fill
        drawList.AddRectFilled(min, max,
            ImGui.GetColorU32(new Vector4(1.0f, 0.84f, 0.92f, opacity * 0.88f)), rounding);
        // Highlight gradient
        drawList.AddRectFilledMultiColor(
            min + new Vector2(1.0f * scale, 1.0f * scale),
            max - new Vector2(1.0f * scale, Math.Max(1.0f, (max.Y - min.Y) * 0.46f)),
            ImGui.GetColorU32(new Vector4(1.0f, 0.96f, 0.99f, opacity * 0.40f)),
            ImGui.GetColorU32(new Vector4(0.96f, 0.86f, 1.0f, opacity * 0.26f)),
            ImGui.GetColorU32(new Vector4(0.96f, 0.86f, 1.0f, opacity * 0.06f)),
            ImGui.GetColorU32(new Vector4(1.0f, 0.96f, 0.99f, opacity * 0.08f)));
        // Border
        drawList.AddRect(min, max,
            ImGui.GetColorU32(new Vector4(0.94f, 0.58f, 0.74f, opacity * 0.82f)), rounding, ImDrawFlags.None, 1.0f * scale);
    }

    private void DrawShortcutButton(ImDrawListPtr drawList, Vector2 min, Vector2 max, QCShortcutDefinition shortcut, bool hovered, bool active, float opacity, float scale, string id) {
        var innerPad = 3.0f * scale;
        var innerMin = min + new Vector2(innerPad, innerPad);
        var innerMax = max - new Vector2(innerPad, innerPad);

        // Use shortcut color if not default
        var useCustomColor = shortcut.Color != 0xFFFFFFFF;
        var bgColor = useCustomColor ? UnpackColor(shortcut.Color, opacity) :
            active
                ? new Vector4(0.82f, 0.50f, 0.68f, opacity * 0.95f)
                : hovered
                    ? new Vector4(0.92f, 0.64f, 0.80f, opacity * 0.90f)
                    : new Vector4(0.94f, 0.72f, 0.84f, opacity * 0.72f);
        drawList.AddRectFilled(innerMin, innerMax, ImGui.GetColorU32(bgColor), 4.0f * scale);

        // Border
        var borderColor = useCustomColor
            ? new Vector4(bgColor.X * 0.8f, bgColor.Y * 0.8f, bgColor.Z * 0.8f, opacity * 0.92f)
            : hovered
                ? new Vector4(0.88f, 0.38f, 0.62f, opacity * 0.92f)
                : new Vector4(0.84f, 0.44f, 0.64f, opacity * 0.58f);
        drawList.AddRect(innerMin, innerMax, ImGui.GetColorU32(borderColor), 4.0f * scale, ImDrawFlags.None, 1.0f * scale);

        // Mode indicator
        if (shortcut.Mode != QCShortcutMode.Normal) {
            var modeText = shortcut.Mode == QCShortcutMode.Incremental ? "INC" : "RND";
            var modeSize = ImGui.CalcTextSize(modeText);
            var modePos = new Vector2(innerMax.X - modeSize.X - 2.0f * scale, innerMin.Y + 2.0f * scale);
            drawList.AddText(modePos, ImGui.GetColorU32(new Vector4(0.65f, 0.30f, 0.50f, opacity * 0.80f)), modeText);
        }

        // Icon or text
        if (shortcut.IconId != 0) {
            var iconPad = 4.0f * scale;
            var iconMin = innerMin + new Vector2(iconPad, iconPad);
            var iconMax = innerMax - new Vector2(iconPad, iconPad);
            var iconSize = iconMax - iconMin;

            // QoLBar-style icon rendering with UV transform and cooldown text
            DrawGameIcon(drawList, iconMin, iconSize, shortcut, hovered, active, opacity, scale, id);
        } else {
            var text = string.IsNullOrWhiteSpace(shortcut.Name) ? "?" : shortcut.Name;
            if (text.Length > 2) text = text[..2];
            var textSize = ImGui.CalcTextSize(text);
            var textPos = innerMin + (innerMax - innerMin - textSize) * 0.5f;
            drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(0.25f, 0.12f, 0.18f, opacity)), text);
        }
    }

    // QoLBar-style icon rendering with UV transform
    private void DrawGameIcon(ImDrawListPtr drawList, Vector2 pos, Vector2 size, QCShortcutDefinition shortcut, bool hovered, bool active, float opacity, float scale, string id) {
        var tex = GetCachedIcon(shortcut.IconId);
        if (tex == null) return;

        var zoom = Math.Max(shortcut.IconZoom, 0.01f);
        var offset = shortcut.IconOffset;
        var rotation = shortcut.IconRotation;
        var flipped = shortcut.IconFlipped;

        // Calculate UV coordinates with zoom + offset (QoLBar-style)
        var z = 0.5f / zoom;
        var uv1 = new Vector2(0.5f - z + offset.X, 0.5f - z + offset.Y);
        var uv3 = new Vector2(0.5f + z + offset.X, 0.5f + z + offset.Y);

        var p1 = pos;
        var p2 = pos + new Vector2(size.X, 0);
        var p3 = pos + size;
        var p4 = pos + new Vector2(0, size.Y);

        // QoLBar-style: Apply rotation to UV coordinates (4-corner UV rotation)
        if (Math.Abs(rotation) > 0.001f) {
            var rCos = (float)Math.Cos(rotation);
            var rSin = (float)-Math.Sin(rotation);
            var uvHalfSize = (uv3 - uv1) / 2;
            var uvCenter = uv1 + uvHalfSize;

            var rotatedUv1 = uvCenter + RotateVector(-uvHalfSize, rCos, rSin);
            var rotatedUv2 = uvCenter + RotateVector(new Vector2(uvHalfSize.X, -uvHalfSize.Y), rCos, rSin);
            var rotatedUv3 = uvCenter + RotateVector(uvHalfSize, rCos, rSin);
            var rotatedUv4 = uvCenter + RotateVector(new Vector2(-uvHalfSize.X, uvHalfSize.Y), rCos, rSin);

            if (!flipped)
                drawList.AddImageQuad(tex.Handle, p1, p2, p3, p4, rotatedUv1, rotatedUv2, rotatedUv3, rotatedUv4, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, opacity)));
            else
                drawList.AddImageQuad(tex.Handle, p2, p1, p4, p3, rotatedUv1, rotatedUv2, rotatedUv3, rotatedUv4, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, opacity)));
        } else {
            if (!flipped)
                drawList.AddImage(tex.Handle, p1, p3, uv1, uv3, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, opacity)));
            else
                drawList.AddImageQuad(tex.Handle, p2, p1, p4, p3, uv1, uv1, uv3, uv3, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, opacity)));
        }

        // QoLBar-style: cooldown countdown text
        if (shortcut.CooldownActionId != 0) {
            var cooldownCurrent = this.manager.GetCooldownRemaining(shortcut.CooldownActionId);
            var cooldownMax = this.manager.GetCooldownMax(shortcut.CooldownActionId);
            if (cooldownMax > 0 && cooldownCurrent > 0) {
                var center = pos + size / 2;
                var wantedSize = size.X * 0.75f;
                var cdStr = $"{Math.Ceiling(cooldownMax - cooldownCurrent)}";
                var textSizeHalf = ImGui.CalcTextSize(cdStr) / (2 * ImGuiHelpers.GlobalScale);
                // Outline
                drawList.AddText(ImGui.GetFont(), wantedSize, center - textSizeHalf + new Vector2(0, wantedSize * 0.05f), 0xFF000000, cdStr);
                // Main text
                drawList.AddText(ImGui.GetFont(), wantedSize, center - textSizeHalf - Vector2.UnitY, 0xFFFFFFFF, cdStr);
            }
        }
    }

    private void DrawCategoryPopup(QCShortcutDefinition shortcut, int barIndex, int shortcutIndex, float scale) {
        var popupId = $"QC_cat_popup_{barIndex}_{shortcutIndex}";
        var popupWidth = Math.Max(shortcut.CategoryWidth, 40) * scale * shortcut.CategoryScale;
        ImGui.SetNextWindowSize(new Vector2(popupWidth, 0.0f), ImGuiCond.Appearing);
        ImGui.SetNextWindowSizeConstraints(new Vector2(popupWidth, 0.0f), new Vector2(popupWidth * 2.0f, float.MaxValue));

        var popupFlags = ImGuiWindowFlags.NoScrollbar;
        if (shortcut.CategoryNoBackground) popupFlags |= ImGuiWindowFlags.NoBackground;

        if (shortcut.CategoryOnHover) {
            if (ImGui.IsItemHovered()) {
                ImGui.OpenPopup(popupId);
            }
        }

        if (!ImGui.BeginPopup(popupId, popupFlags)) return;

        var catSpacing = shortcut.CategorySpacing;
        var colCount = Math.Max(shortcut.CategoryColumns, 1);

        // 使用嵌套索引跟踪当前层级，用于生成唯一 ID
        var childIndex = 0;
        foreach (var childId in shortcut.ChildShortcutIds) {
            if (!this.manager.Shortcuts.TryGetValue(childId, out var child)) continue;

            var hasSubItems = child.IsCategory && child.ChildShortcutIds.Count > 0;

            if (colCount > 1 && childIndex % colCount != 0)
                ImGui.SameLine(0.0f, catSpacing.X);

            ImGui.BeginGroup();
            ImGui.PushID($"qc_cat_child_{barIndex}_{shortcutIndex}_{childIndex}");

            // 计算子项宽度（多列时）
            var itemWidth = colCount > 1
                ? (popupWidth - catSpacing.X * (colCount - 1) - ImGui.GetStyle().WindowPadding.X * 2) / colCount
                : 0.0f;

            if (hasSubItems) {
                // 嵌套子分类：点击执行命令，悬停显示子菜单
                if (!string.IsNullOrWhiteSpace(child.Command)) {
                    if (ImGui.Selectable(child.Name ?? "?", false, ImGuiSelectableFlags.None,
                            new Vector2(itemWidth > 0 ? itemWidth : 0, 0))) {
                        this.manager.ExecuteShortcut(child);
                        if (!shortcut.CategoryStaysOpen) ImGui.CloseCurrentPopup();
                    }
                } else {
                    ImGui.TextUnformatted(child.Name ?? "?");
                }

                // 递归渲染子分类的弹出菜单
                var subPopupId = $"QC_cat_sub_{barIndex}_{shortcutIndex}_{childIndex}";
                if (ImGui.IsItemHovered()) {
                    ImGui.OpenPopup(subPopupId);
                }

                if (ImGui.BeginPopup(subPopupId, ImGuiWindowFlags.NoScrollbar | (child.CategoryNoBackground ? ImGuiWindowFlags.NoBackground : 0))) {
                    var subWidth = Math.Max(child.CategoryWidth, 40) * scale * child.CategoryScale;
                    ImGui.SetNextWindowSize(new Vector2(subWidth, 0.0f), ImGuiCond.Appearing);
                    ImGui.SetNextWindowSizeConstraints(new Vector2(subWidth, 0.0f), new Vector2(subWidth * 2.0f, float.MaxValue));

                    var subChildIndex = 0;
                    foreach (var subChildId in child.ChildShortcutIds) {
                        if (!this.manager.Shortcuts.TryGetValue(subChildId, out var subChild)) continue;

                        var subColCount = Math.Max(child.CategoryColumns, 1);
                        if (subColCount > 1 && subChildIndex % subColCount != 0)
                            ImGui.SameLine(0.0f, child.CategorySpacing.X);

                        ImGui.PushID($"qc_cat_sub_child_{barIndex}_{shortcutIndex}_{childIndex}_{subChildIndex}");

                        var subItemWidth = subColCount > 1
                            ? (subWidth - child.CategorySpacing.X * (subColCount - 1) - ImGui.GetStyle().WindowPadding.X * 2) / subColCount
                            : 0.0f;

                        if (ImGui.Selectable(subChild.Name ?? "?", false, ImGuiSelectableFlags.None,
                                new Vector2(subItemWidth > 0 ? subItemWidth : 0, 0))) {
                            this.manager.ExecuteShortcut(subChild);
                            if (!child.CategoryStaysOpen) ImGui.CloseCurrentPopup();
                        }

                        if (ImGui.IsItemHovered() && !string.IsNullOrWhiteSpace(subChild.Tooltip)) {
                            ImGui.SetTooltip(subChild.Tooltip);
                        }
                        ImGui.PopID();
                        subChildIndex++;
                    }

                    // 悬停关闭
                    if (child.CategoryHoverClose && !ImGui.IsWindowHovered() && !ImGui.IsAnyItemHovered()) {
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }
            } else {
                // 普通子项
                if (ImGui.Selectable(child.Name ?? "?", false, ImGuiSelectableFlags.None,
                        new Vector2(itemWidth > 0 ? itemWidth : 0, 0))) {
                    this.manager.ExecuteShortcut(child);
                    if (!shortcut.CategoryStaysOpen) ImGui.CloseCurrentPopup();
                }
            }

            if (ImGui.IsItemHovered() && !string.IsNullOrWhiteSpace(child.Tooltip)) {
                ImGui.SetTooltip(child.Tooltip);
            }
            ImGui.PopID();
            ImGui.EndGroup();
            childIndex++;
        }

        // Hover close
        if (shortcut.CategoryHoverClose && !ImGui.IsWindowHovered() && !ImGui.IsAnyItemHovered()) {
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void DrawPieMenu(QCBarDefinition bar, List<QCShortcutDefinition> shortcuts, int index) {
        if (string.IsNullOrWhiteSpace(bar.Hotkey)) return;

        // Parse VK code from hotkey string (e.g., "VK 160" or "160")
        var vkCode = 0;
        if (bar.Hotkey.StartsWith("VK ", StringComparison.OrdinalIgnoreCase)) {
            int.TryParse(bar.Hotkey[3..], out vkCode);
        } else {
            int.TryParse(bar.Hotkey, out vkCode);
        }

        if (vkCode <= 0 || !this.manager.IsVkKeyHeld(vkCode)) return;

        var center = ImGui.GetIO().DisplaySize * 0.5f;
        var radius = MathF.Round(bar.PieRadius * bar.Scale);
        var innerRadius = MathF.Round(radius * 0.3f);
        var count = shortcuts.Count;
        if (count == 0) return;

        var drawList = ImGui.GetForegroundDrawList();
        var angleStep = MathF.PI * 2.0f / count;
        var startAngle = -MathF.PI * 0.5f;

        var mousePos = ImGui.GetMousePos();
        var mouseDir = mousePos - center;
        var mouseAngle = MathF.Atan2(mouseDir.Y, mouseDir.X);
        var mouseDist = mouseDir.Length();

        // Draw background
        drawList.AddCircleFilled(center, radius + 8.0f, ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.40f)));
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0.96f, 0.78f, 0.88f, 0.95f)));
        drawList.AddCircleFilled(center, innerRadius, ImGui.GetColorU32(new Vector4(1.0f, 0.92f, 0.96f, 0.98f)));

        // Draw wedges
        for (var i = 0; i < count; i++) {
            var shortcut = shortcuts[i];
            var a1 = startAngle + i * angleStep;
            var a2 = a1 + angleStep;

            var isHovered = mouseDist >= innerRadius && mouseDist <= radius
                            && IsAngleBetween(mouseAngle, a1, a2);

            if (isHovered) {
                DrawPieWedge(drawList, center, radius, innerRadius, a1, a2, ImGui.GetColorU32(new Vector4(0.90f, 0.50f, 0.70f, 0.50f)));
            }

            // Draw border
            DrawPieWedge(drawList, center, radius, innerRadius, a1, a1 + 0.02f,
                ImGui.GetColorU32(new Vector4(0.78f, 0.40f, 0.60f, 0.50f)));

            // Draw icon/text
            var midAngle = a1 + angleStep * 0.5f;
            var textRadius = (innerRadius + radius) * 0.5f;
            var textPos = new Vector2(
                center.X + MathF.Cos(midAngle) * textRadius,
                center.Y + MathF.Sin(midAngle) * textRadius);

            var label = string.IsNullOrWhiteSpace(shortcut.Name) ? "?" : shortcut.Name;
            if (label.Length > 4) label = label[..4];
            var textSize = ImGui.CalcTextSize(label);
            drawList.AddText(textPos - textSize * 0.5f,
                ImGui.GetColorU32(new Vector4(0.25f, 0.12f, 0.18f, 1.0f)), label);

            // Click handling
            if (isHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                this.manager.ExecuteShortcut(shortcut);
            }
        }

        // Center text
        drawList.AddText(center - ImGui.CalcTextSize(bar.Name) * 0.5f,
            ImGui.GetColorU32(new Vector4(0.25f, 0.12f, 0.18f, 0.80f)), bar.Name);
    }

    private void DrawPieWedge(ImDrawListPtr drawList, Vector2 center, float radius, float innerRadius, float a1, float a2, uint color) {
        const int segments = 24;
        var segCount = Math.Max(3, (int)(segments * (a2 - a1) / (MathF.PI * 2.0f)));
        var pathLen = segCount * 2 + 2;

        // Use cached path buffer to avoid per-frame allocation
        var path = this.pieWedgePath;
        if (path is null || path.Length < pathLen) {
            path = new Vector2[pathLen];
            this.pieWedgePath = path;
        }

        for (var i = 0; i <= segCount; i++) {
            var angle = a1 + (a2 - a1) * i / segCount;
            path[i] = new Vector2(center.X + MathF.Cos(angle) * radius, center.Y + MathF.Sin(angle) * radius);
            path[segCount * 2 - i] = new Vector2(center.X + MathF.Cos(angle) * innerRadius, center.Y + MathF.Sin(angle) * innerRadius);
        }

        drawList.AddConvexPolyFilled(ref path[0], segCount * 2 + 1, color);
    }

    private static bool IsAngleBetween(float angle, float a1, float a2) {
        while (angle < a1) angle += MathF.PI * 2.0f;
        while (a2 <= a1) a2 += MathF.PI * 2.0f;
        return angle >= a1 && angle < a2;
    }

    private Vector2 GetBarPosition(QCBarDefinition bar, Vector2 displaySize, float width, float height) {
        var margin = 4.0f;

        // Use dock side if applicable (not undocked)
        if (bar.DockSide >= 0 && bar.DockSide <= 3) {
            var dockMargin = 8.0f;
            return bar.DockSide switch {
                0 => new Vector2(GetDockAlign(bar, displaySize.X, width, dockMargin), dockMargin),           // Top
                1 => new Vector2(displaySize.X - width - dockMargin, GetDockAlign(bar, displaySize.Y, height, dockMargin)),  // Right
                2 => new Vector2(GetDockAlign(bar, displaySize.X, width, dockMargin), displaySize.Y - height - dockMargin), // Bottom
                3 => new Vector2(dockMargin, GetDockAlign(bar, displaySize.Y, height, dockMargin)),         // Left
                _ => new Vector2(dockMargin, (displaySize.Y - height) * 0.5f),
            };
        }

        // Legacy position modes
        if (bar.PositionMode == 2 && bar.CustomPosition.X > 0 && bar.CustomPosition.Y > 0) {
            return bar.CustomPosition;
        }

        return bar.PositionMode switch {
            1 => new Vector2(displaySize.X - width - margin, (displaySize.Y - height) * 0.5f),   // Right
            3 => new Vector2((displaySize.X - width) * 0.5f, margin),                             // Top
            4 => new Vector2((displaySize.X - width) * 0.5f, displaySize.Y - height - margin),    // Bottom
            _ => new Vector2(margin, (displaySize.Y - height) * 0.5f),                            // Left (default)
        };
    }

    private float GetDockAlign(QCBarDefinition bar, float containerSize, float elementSize, float margin) {
        return bar.Alignment switch {
            0 => margin,                                    // Left/Top
            1 => (containerSize - elementSize) * 0.5f,      // Center
            2 => containerSize - elementSize - margin,       // Right/Bottom
            _ => (containerSize - elementSize) * 0.5f,
        };
    }

    private SlideAnimation GetOrCreateAnimation(string barId) {
        if (!this.barAnimations.TryGetValue(barId, out var anim)) {
            anim = new SlideAnimation { Progress = 1.0 };
            this.barAnimations[barId] = anim;
        }
        return anim;
    }

    private void UpdateSlideAnimation(QCBarDefinition bar, SlideAnimation anim, Vector2 pos, Vector2 size, Vector2 displaySize) {
        const double revealDuration = 0.25; // seconds to fully reveal
        const double hideDuration = 0.35; // seconds to fully hide
        const double hoverDelay = 0.15; // seconds before hover detection starts

        var dt = ImGui.GetIO().DeltaTime;
        if (dt <= 0 || dt > 0.1) dt = 1.0f / 60.0f; // clamp

        // Check if mouse is near the bar's reveal area
        var mousePos = ImGui.GetMousePos();
        var isNearBar = IsMouseNearBar(bar, pos, size, displaySize, mousePos);

        // Handle hover state with hysteresis
        if (isNearBar) {
            anim.HoverTimer += dt;
        } else {
            anim.HoverTimer -= dt;
        }

        anim.HoverTimer = Math.Clamp(anim.HoverTimer, -hideDuration, revealDuration);
        anim.IsHovered = anim.HoverTimer > hoverDelay;

        // Animate progress toward target
        var targetProgress = anim.IsHovered ? 1.0 : 0.0;
        var speed = targetProgress > anim.Progress ? 1.0 / revealDuration : 1.0 / hideDuration;
        var step = speed * dt;

        if (anim.Progress < targetProgress) {
            anim.Progress = Math.Min(anim.Progress + step, 1.0);
        } else if (anim.Progress > targetProgress) {
            anim.Progress = Math.Max(anim.Progress - step, 0.0);
        }
    }

    private static bool IsMouseNearBar(QCBarDefinition bar, Vector2 pos, Vector2 size, Vector2 displaySize, Vector2 mousePos) {
        // Determine which edge the bar slides from and check if mouse is near that edge
        var slideEdge = bar.DockSide >= 0 && bar.DockSide <= 3 ? bar.DockSide : 0;
        var revealDistance = 30.0f * bar.RevealAreaScale;

        switch (slideEdge) {
            case 0: // Top - check if mouse is near top
                return mousePos.Y <= revealDistance;
            case 1: // Right - check if mouse is near right edge
                return mousePos.X >= displaySize.X - revealDistance;
            case 2: // Bottom - check if mouse is near bottom edge
                return mousePos.Y >= displaySize.Y - revealDistance;
            case 3: // Left - check if mouse is near left edge
                return mousePos.X <= revealDistance;
            default:
                return false;
        }
    }

    private static Vector2 GetAnimatedSlidePosition(QCBarDefinition bar, Vector2 pos, Vector2 size, Vector2 displaySize, SlideAnimation anim) {
        var progress = (float)anim.Progress;
        if (progress >= 1.0f) return pos; // fully visible

        var slideEdge = bar.DockSide >= 0 && bar.DockSide <= 3 ? bar.DockSide : 0;
        var offset = slideEdge switch {
            0 => new Vector2(0, -size.Y * (1.0f - progress)), // Top: slide down from above
            1 => new Vector2(size.X * (1.0f - progress), 0),  // Right: slide left from right
            2 => new Vector2(0, size.Y * (1.0f - progress)),  // Bottom: slide up from below
            3 => new Vector2(-size.X * (1.0f - progress), 0), // Left: slide right from left
            _ => Vector2.Zero,
        };

        return pos + offset;
    }

    private void DrawHint(QCBarDefinition bar, Vector2 pos, Vector2 size, Vector2 displaySize, float scale) {
        // Draw a small hint indicator when the bar is hidden
        var hintSize = 6.0f * scale;
        var hintPos = pos;

        // Position hint at the appropriate edge
        switch (bar.DockSide) {
            case 0: // Top
                hintPos = new Vector2(pos.X + size.X * 0.5f, 0);
                break;
            case 1: // Right
                hintPos = new Vector2(displaySize.X - hintSize, pos.Y + size.Y * 0.5f);
                break;
            case 2: // Bottom
                hintPos = new Vector2(pos.X + size.X * 0.5f, displaySize.Y - hintSize);
                break;
            case 3: // Left
                hintPos = new Vector2(0, pos.Y + size.Y * 0.5f);
                break;
        }

        var drawList = ImGui.GetForegroundDrawList();
        drawList.AddCircleFilled(hintPos, hintSize, ImGui.GetColorU32(new Vector4(0.94f, 0.58f, 0.74f, 0.50f)));
    }

    // QoLBar-style: get cached icon, using ITextureProvider for game icons
    private IDalamudTextureWrap? GetCachedIcon(uint iconId) {
        if (iconId == 0) return null;

        // Try cache first
        if (this.iconCache.TryGetValue(iconId, out var cached)) {
            return cached;
        }

        // Try to load via ITextureProvider (modern Dalamud API)
        try {
            var gameIcon = new GameIconLookup { IconId = iconId, HiRes = false };
            var texture = this.textureProvider.GetFromGameIcon(gameIcon).GetWrapOrDefault();
            this.iconCache[iconId] = texture;
            return texture;
        } catch {
            return null;
        }
    }

    private static Vector2 SnapToPixel(Vector2 v) => new(MathF.Round(v.X), MathF.Round(v.Y));

    private Vector4 UnpackColor(uint color, float opacity) {
        // Check cache first
        if (this.colorCache.TryGetValue(color, out var cached)) {
            return new Vector4(cached.X, cached.Y, cached.Z, cached.W * opacity);
        }

        // ABGR format: A = bits 24-31, B = bits 16-23, G = bits 8-15, R = bits 0-7
        var a = ((color >> 24) & 0xFF) / 255.0f;
        var b = ((color >> 16) & 0xFF) / 255.0f;
        var g = ((color >> 8) & 0xFF) / 255.0f;
        var r = (color & 0xFF) / 255.0f;
        var result = new Vector4(r, g, b, a);
        this.colorCache[color] = result;
        return new Vector4(r, g, b, a * opacity);
    }

    public void ClearIconCache() {
        foreach (var texture in this.iconCache.Values) {
            texture?.Dispose();
        }
        this.iconCache.Clear();
    }
}