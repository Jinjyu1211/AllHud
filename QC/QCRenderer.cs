using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Textures;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace AllHud;

public sealed class QCRenderer : IDisposable {
    private readonly QCManager manager;
    private readonly Configuration config;
    private readonly ITextureProvider textureProvider;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Dictionary<uint, IDalamudTextureWrap?> iconCache = [];
    // Cached path buffer for pie wedge drawing to avoid per-frame allocation
    private Vector2[]? pieWedgePath;
    // Cached unpacked colors
    private readonly Dictionary<uint, Vector4> colorCache = [];
    // Slide animation tracking
    private readonly Dictionary<string, SlideAnimation> barAnimations = [];

    private sealed class SlideAnimation {
        public double Progress; // 0.0 = hidden, 1.0 = fully visible
        public bool IsHovered;
        public double HoverTimer;
    }

    public QCRenderer(QCManager manager, Configuration config, ITextureProvider textureProvider, IDalamudPluginInterface pluginInterface) {
        this.manager = manager;
        this.config = config;
        this.textureProvider = textureProvider;
        this.pluginInterface = pluginInterface;
    }

    public void Dispose() {
        ClearIconCache();
        this.colorCache.Clear();
        this.barAnimations.Clear();
    }

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
        var buttonSize = MathF.Round(44.0f * scale * (bar.ButtonWidth / 100.0f));
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

            DrawShortcutButton(drawList, buttonMin, buttonMax, shortcut, hovered, active, finalOpacity, scale);

            // Cooldown overlay
            if (shortcut.CooldownActionId != 0) {
                DrawCooldownOverlay(drawList, buttonMin, buttonMax, shortcut, scale);
            }

            if (hovered && !string.IsNullOrWhiteSpace(shortcut.Tooltip)) {
                ImGui.SetTooltip(shortcut.Tooltip);
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                if (shortcut.IsCategory) {
                    ImGui.OpenPopup($"QC_cat_popup_{index}_{i}");
                } else {
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

    private void DrawShortcutButton(ImDrawListPtr drawList, Vector2 min, Vector2 max, QCShortcutDefinition shortcut, bool hovered, bool active, float opacity, float scale) {
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
            var iconMin = innerMin + new Vector2(iconPad, iconPad) + shortcut.IconOffset * scale;
            var iconMax = innerMax - new Vector2(iconPad, iconPad) + shortcut.IconOffset * scale;

            // Apply icon zoom
            if (Math.Abs(shortcut.IconZoom - 1.0f) > 0.01f) {
                var center = (iconMin + iconMax) * 0.5f;
                var halfSize = (iconMax - iconMin) * 0.5f * shortcut.IconZoom;
                iconMin = center - halfSize;
                iconMax = center + halfSize;
            }

            DrawGameIcon(shortcut.IconId, iconMin, iconMax);
        } else {
            var text = string.IsNullOrWhiteSpace(shortcut.Name) ? "?" : shortcut.Name;
            if (text.Length > 2) text = text[..2];
            var textSize = ImGui.CalcTextSize(text);
            var textPos = innerMin + (innerMax - innerMin - textSize) * 0.5f;
            drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(0.25f, 0.12f, 0.18f, opacity)), text);
        }
    }

    private void DrawCooldownOverlay(ImDrawListPtr drawList, Vector2 min, Vector2 max, QCShortcutDefinition shortcut, float scale) {
        var remaining = this.manager.GetCooldownRemaining(shortcut.CooldownActionId);
        var maxCd = this.manager.GetCooldownMax(shortcut.CooldownActionId);
        if (remaining <= 0 || maxCd <= 0) return;

        var ratio = Math.Clamp(remaining / maxCd, 0.0f, 1.0f);

        if (shortcut.CooldownStyle == 0) {
            // Icon overlay style - dark overlay from bottom
            var overlayHeight = (max.Y - min.Y) * ratio;
            var overlayColor = new Vector4(0.0f, 0.0f, 0.0f, 0.45f);
            drawList.AddRectFilled(
                new Vector2(min.X, max.Y - overlayHeight),
                max,
                ImGui.GetColorU32(overlayColor), 4.0f * scale);

            // Draw cooldown text
            var cdText = $"{remaining:F1}";
            var textSize = ImGui.CalcTextSize(cdText);
            var textPos = new Vector2(
                min.X + (max.X - min.X - textSize.X) * 0.5f,
                max.Y - overlayHeight + 2.0f * scale);
            drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.90f)), cdText);
        } else {
            // Text only style
            var cdText = $"{remaining:F1}s";
            var textSize = ImGui.CalcTextSize(cdText);
            var textPos = new Vector2(
                min.X + (max.X - min.X - textSize.X) * 0.5f,
                min.Y + (max.Y - min.Y - textSize.Y) * 0.5f);
            drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.90f)), cdText);
        }
    }

    private void DrawCategoryPopup(QCShortcutDefinition shortcut, int barIndex, int shortcutIndex, float scale) {
        var popupId = $"QC_cat_popup_{barIndex}_{shortcutIndex}";
        var popupWidth = shortcut.CategoryWidth * scale * shortcut.CategoryScale;
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

        if (shortcut.CategoryColumns > 1) {
            var colCount = shortcut.CategoryColumns;
            var catSpacing = shortcut.CategorySpacing;
            var childWidth = (popupWidth - catSpacing.X * (colCount - 1) - ImGui.GetStyle().WindowPadding.X * 2) / colCount;

            for (var i = 0; i < shortcut.ChildShortcutIds.Count; i++) {
                var childId = shortcut.ChildShortcutIds[i];
                if (!this.manager.Shortcuts.TryGetValue(childId, out var child)) continue;

                if (i % colCount != 0) ImGui.SameLine(0.0f, catSpacing.X);

                ImGui.BeginGroup();
                ImGui.PushID($"qc_cat_child_{barIndex}_{shortcutIndex}_{i}");
                var label = string.IsNullOrWhiteSpace(child.Name) ? "?" : child.Name;
                var selectableSize = new Vector2(childWidth, 0.0f);
                if (ImGui.Selectable(label, false, ImGuiSelectableFlags.None, selectableSize)) {
                    this.manager.ExecuteShortcut(child);
                    if (!shortcut.CategoryStaysOpen) ImGui.CloseCurrentPopup();
                }

                if (ImGui.IsItemHovered() && !string.IsNullOrWhiteSpace(child.Tooltip)) {
                    ImGui.SetTooltip(child.Tooltip);
                }
                ImGui.PopID();
                ImGui.EndGroup();
            }
        } else {
            foreach (var childId in shortcut.ChildShortcutIds) {
                if (!this.manager.Shortcuts.TryGetValue(childId, out var child)) continue;

                var label = string.IsNullOrWhiteSpace(child.Name) ? "?" : child.Name;
                if (ImGui.Selectable(label)) {
                    this.manager.ExecuteShortcut(child);
                    if (!shortcut.CategoryStaysOpen) ImGui.CloseCurrentPopup();
                }

                if (ImGui.IsItemHovered() && !string.IsNullOrWhiteSpace(child.Tooltip)) {
                    ImGui.SetTooltip(child.Tooltip);
                }
            }
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

    private void DrawGameIcon(uint iconId, Vector2 min, Vector2 max) {
        try {
            var texture = GetCachedIcon(iconId);
            if (texture != null) {
                ImGui.GetWindowDrawList().AddImage(texture.Handle, min, max);
            }
        } catch {
            // Silently fail icon rendering
        }
    }

    private IDalamudTextureWrap? GetCachedIcon(uint iconId) {
        if (iconId == 0) return null;

        if (this.iconCache.TryGetValue(iconId, out var cached)) {
            return cached;
        }

        var gameIcon = new GameIconLookup { IconId = iconId, HiRes = false };
        var texture = this.textureProvider.GetFromGameIcon(gameIcon).GetWrapOrDefault();
        this.iconCache[iconId] = texture;
        return texture;
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