using System.Text;

namespace AllHud;

public static class QCImportExport {
    private const string ExportHeader = "=== QC Bar Export v1 ===";

    public static string ExportBars(QCManager manager) {
        var sb = new StringBuilder();
        sb.AppendLine(ExportHeader);
        sb.AppendLine();

        // Export all shortcuts first
        sb.AppendLine("[Shortcuts]");
        foreach (var (id, shortcut) in manager.Shortcuts) {
            sb.AppendLine($"Id={id}");
            sb.AppendLine($"Name={EscapeValue(shortcut.Name)}");
            sb.AppendLine($"IconId={shortcut.IconId}");
            sb.AppendLine($"Command={EscapeValue(shortcut.Command)}");
            sb.AppendLine($"Tooltip={EscapeValue(shortcut.Tooltip)}");
            sb.AppendLine($"Mode={(int)shortcut.Mode}");
            sb.AppendLine($"IsCategory={shortcut.IsCategory}");
            sb.AppendLine($"Hotkey={shortcut.Hotkey}");
            sb.AppendLine($"Color={shortcut.Color}");
            sb.AppendLine($"IconZoom={shortcut.IconZoom}");
            sb.AppendLine($"IconOffsetX={shortcut.IconOffset.X}");
            sb.AppendLine($"IconOffsetY={shortcut.IconOffset.Y}");
            sb.AppendLine($"CooldownActionId={shortcut.CooldownActionId}");
            sb.AppendLine($"CooldownStyle={shortcut.CooldownStyle}");
            if (shortcut.ChildShortcutIds.Count > 0) {
                sb.AppendLine($"Children={string.Join(",", shortcut.ChildShortcutIds)}");
            }
            sb.AppendLine("---");
        }

        // Export bars
        sb.AppendLine("[Bars]");
        foreach (var bar in manager.Bars) {
            sb.AppendLine($"Id={bar.Id}");
            sb.AppendLine($"Name={EscapeValue(bar.Name)}");
            sb.AppendLine($"Enabled={bar.Enabled}");
            sb.AppendLine($"Horizontal={bar.Horizontal}");
            sb.AppendLine($"PositionMode={bar.PositionMode}");
            sb.AppendLine($"CustomX={bar.CustomPosition.X}");
            sb.AppendLine($"CustomY={bar.CustomPosition.Y}");
            sb.AppendLine($"Scale={bar.Scale}");
            sb.AppendLine($"Opacity={bar.Opacity}");
            sb.AppendLine($"FontScale={bar.FontScale}");
            sb.AppendLine($"ButtonWidth={bar.ButtonWidth}");
            sb.AppendLine($"Columns={bar.Columns}");
            sb.AppendLine($"SpacingX={bar.Spacing.X}");
            sb.AppendLine($"SpacingY={bar.Spacing.Y}");
            sb.AppendLine($"DockSide={bar.DockSide}");
            sb.AppendLine($"Alignment={bar.Alignment}");
            sb.AppendLine($"VisibilityMode={bar.VisibilityMode}");
            sb.AppendLine($"Hint={bar.Hint}");
            sb.AppendLine($"RevealAreaScale={bar.RevealAreaScale}");
            sb.AppendLine($"ShortcutIds={string.Join(",", bar.ShortcutIds)}");
            if (!string.IsNullOrEmpty(bar.Hotkey)) sb.AppendLine($"Hotkey={bar.Hotkey}");
            if (!string.IsNullOrEmpty(bar.ConditionSetId)) sb.AppendLine($"ConditionSetId={bar.ConditionSetId}");
            sb.AppendLine($"IsPieMenu={bar.IsPieMenu}");
            sb.AppendLine($"PieRadius={bar.PieRadius}");
            sb.AppendLine($"ClickThrough={bar.ClickThrough}");
            sb.AppendLine($"LockedPosition={bar.LockedPosition}");
            sb.AppendLine($"NoBackground={bar.NoBackground}");
            sb.AppendLine($"HideWhenEmpty={bar.HideWhenEmpty}");
            sb.AppendLine("===");
        }

        // Export condition sets
        sb.AppendLine("[ConditionSets]");
        foreach (var cs in manager.ConditionSets) {
            sb.AppendLine($"Id={cs.Id}");
            sb.AppendLine($"Name={EscapeValue(cs.Name)}");
            foreach (var cond in cs.Conditions) {
                sb.AppendLine($"CondType={(int)cond.ConditionType}");
                sb.AppendLine($"CondTargetIds={string.Join(",", cond.TargetIds)}");
                sb.AppendLine($"CondNegate={cond.Negate}");
                sb.AppendLine($"CondOp={(int)cond.Operator}");
                sb.AppendLine("---");
            }
        }

        return sb.ToString();
    }

    public static void ImportBars(QCManager manager, string text) {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || lines[0] != ExportHeader) return;

        var currentSection = string.Empty;
        var currentShortcut = new Dictionary<string, string>();
        var currentBar = new Dictionary<string, string>();
        var currentConditionSet = new Dictionary<string, string>();
        var importedShortcuts = new List<Dictionary<string, string>>();
        var importedBars = new List<Dictionary<string, string>>();
        var importedConditionSets = new List<Dictionary<string, string>>();

        for (var i = 1; i < lines.Length; i++) {
            var line = lines[i].Trim();
            if (line.StartsWith('[') && line.EndsWith(']')) {
                currentSection = line[1..^1];
                continue;
            }

            if (line == "---" || line == "===") {
                if (currentSection == "Shortcuts" && currentShortcut.Count > 0) {
                    importedShortcuts.Add(new Dictionary<string, string>(currentShortcut));
                    currentShortcut.Clear();
                }
                if (currentSection == "Bars" && currentBar.Count > 0) {
                    importedBars.Add(new Dictionary<string, string>(currentBar));
                    currentBar.Clear();
                }
                if (currentSection == "ConditionSets" && currentConditionSet.Count > 0) {
                    importedConditionSets.Add(new Dictionary<string, string>(currentConditionSet));
                    currentConditionSet.Clear();
                }
                continue;
            }

            var eqIndex = line.IndexOf('=');
            if (eqIndex < 0) continue;

            var key = line[..eqIndex].Trim();
            var value = line[(eqIndex + 1)..].Trim();

            switch (currentSection) {
                case "Shortcuts": currentShortcut[key] = value; break;
                case "Bars": currentBar[key] = value; break;
                case "ConditionSets": currentConditionSet[key] = value; break;
            }
        }

        // Flush remaining
        if (currentShortcut.Count > 0) importedShortcuts.Add(currentShortcut);
        if (currentBar.Count > 0) importedBars.Add(currentBar);
        if (currentConditionSet.Count > 0) importedConditionSets.Add(currentConditionSet);

        // Import condition sets
        var idMap = new Dictionary<string, string>();
        foreach (var data in importedConditionSets) {
            var newCs = manager.AddConditionSet(GetValue(data, "Name"));
            idMap[GetValue(data, "Id")] = newCs.Id;
        }

        // Import shortcuts
        var shortcutIdMap = new Dictionary<string, string>();
        foreach (var data in importedShortcuts) {
            var newSc = manager.AddShortcut(GetValue(data, "Name"));
            shortcutIdMap[GetValue(data, "Id")] = newSc.Id;
            newSc.IconId = uint.Parse(GetValue(data, "IconId", "0"));
            newSc.Command = UnescapeValue(GetValue(data, "Command"));
            newSc.Tooltip = UnescapeValue(GetValue(data, "Tooltip"));
            newSc.Mode = (QCShortcutMode)int.Parse(GetValue(data, "Mode", "0"));
            newSc.IsCategory = bool.Parse(GetValue(data, "IsCategory", "False"));
            newSc.Hotkey = int.Parse(GetValue(data, "Hotkey", "0"));
            newSc.Color = uint.Parse(GetValue(data, "Color", "4294967295")); // 0xFFFFFFFF
            newSc.IconZoom = float.Parse(GetValue(data, "IconZoom", "1"));
            newSc.IconOffset = new System.Numerics.Vector2(
                float.Parse(GetValue(data, "IconOffsetX", "0")),
                float.Parse(GetValue(data, "IconOffsetY", "0")));
            newSc.CooldownActionId = uint.Parse(GetValue(data, "CooldownActionId", "0"));
            newSc.CooldownStyle = int.Parse(GetValue(data, "CooldownStyle", "0"));
            if (data.TryGetValue("Children", out var children) && !string.IsNullOrWhiteSpace(children)) {
                newSc.ChildShortcutIds = children.Split(',').ToList();
            }
        }

        // Import bars
        foreach (var data in importedBars) {
            var bar = manager.AddBar(GetValue(data, "Name"));
            bar.Enabled = bool.Parse(GetValue(data, "Enabled", "True"));
            bar.Horizontal = bool.Parse(GetValue(data, "Horizontal", "True"));
            bar.PositionMode = int.Parse(GetValue(data, "PositionMode", "0"));
            bar.CustomPosition = new System.Numerics.Vector2(
                float.Parse(GetValue(data, "CustomX", "500")),
                float.Parse(GetValue(data, "CustomY", "400")));
            bar.Scale = float.Parse(GetValue(data, "Scale", "1"));
            bar.Opacity = float.Parse(GetValue(data, "Opacity", "1"));
            bar.FontScale = float.Parse(GetValue(data, "FontScale", "1"));
            bar.ButtonWidth = int.Parse(GetValue(data, "ButtonWidth", "100"));
            bar.Columns = int.Parse(GetValue(data, "Columns", "0"));
            bar.Spacing = new System.Numerics.Vector2(
                float.Parse(GetValue(data, "SpacingX", "4")),
                float.Parse(GetValue(data, "SpacingY", "4")));
            bar.DockSide = int.Parse(GetValue(data, "DockSide", "4"));
            bar.Alignment = int.Parse(GetValue(data, "Alignment", "1"));
            bar.VisibilityMode = int.Parse(GetValue(data, "VisibilityMode", "2"));
            bar.Hint = bool.Parse(GetValue(data, "Hint", "False"));
            bar.RevealAreaScale = float.Parse(GetValue(data, "RevealAreaScale", "1"));
            bar.IsPieMenu = bool.Parse(GetValue(data, "IsPieMenu", "False"));
            bar.PieRadius = float.Parse(GetValue(data, "PieRadius", "120"));
            bar.ClickThrough = bool.Parse(GetValue(data, "ClickThrough", "False"));
            bar.LockedPosition = bool.Parse(GetValue(data, "LockedPosition", "False"));
            bar.NoBackground = bool.Parse(GetValue(data, "NoBackground", "False"));
            bar.HideWhenEmpty = bool.Parse(GetValue(data, "HideWhenEmpty", "False"));

            if (data.TryGetValue("ShortcutIds", out var shortcutIds) && !string.IsNullOrWhiteSpace(shortcutIds)) {
                bar.ShortcutIds = shortcutIds.Split(',').ToList();
            }
            if (data.TryGetValue("Hotkey", out var hotkey) && !string.IsNullOrWhiteSpace(hotkey)) {
                bar.Hotkey = hotkey;
            }
            if (data.TryGetValue("ConditionSetId", out var condId) && !string.IsNullOrWhiteSpace(condId)) {
                bar.ConditionSetId = idMap.TryGetValue(condId, out var newId) ? newId : condId;
            }
        }
    }

    private static string EscapeValue(string value) {
        return value.Replace("\n", "\\n").Replace("\r", "").Replace("=", "\\=");
    }

    private static string UnescapeValue(string value) {
        return value.Replace("\\=", "=").Replace("\\n", "\n");
    }

    private static string GetValue(Dictionary<string, string> data, string key, string defaultValue = "") {
        return data.TryGetValue(key, out var value) ? value : defaultValue;
    }
}