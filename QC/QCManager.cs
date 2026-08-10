using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace AllHud;

public sealed class QCManager : IDisposable {
    private readonly Configuration config;
    private readonly IPluginLog log;
    private readonly ICommandManager commandManager;
    private readonly ICondition condition;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IKeyState keyState;
    private readonly Dictionary<string, bool> conditionCache = [];
    private long lastCacheUpdateMs;

    // Command queue for rate limiting (instance-level, not static)
    private readonly Queue<string> commandQueue = [];
    private long lastCommandTimeMs;
    private const double CommandCooldownMs = 100.0; // 100ms between commands

    // Macro mode for //m multi-line macro support
    private bool macroMode;
    private readonly List<string> macroLines = [];

    // Chat rate limiting
    private float chatQueueTimer;
    private const float ChatSendCooldown = 1.0f / 6.0f;

    // Retry item tracking
    private uint retryItem;

    // Key repeat prevention
    private readonly Dictionary<int, bool> previousKeyStates = [];
    private const long KeyRepeatDelayMs = 200;

    public QCManager(Configuration config, IPluginLog log, ICommandManager commandManager,
        ICondition condition, IClientState clientState, IObjectTable objectTable,
        IFramework framework, IKeyState keyState) {
        this.config = config;
        this.log = log;
        this.commandManager = commandManager;
        this.condition = condition;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.keyState = keyState;
    }

    public void Dispose() {
        this.commandQueue.Clear();
        this.macroLines.Clear();
        this.conditionCache.Clear();
        this.previousKeyStates.Clear();
    }

    public List<QCBarDefinition> Bars => this.config.QCBars;
    public Dictionary<string, QCShortcutDefinition> Shortcuts => this.config.QCShortcuts;
    public List<QCConditionSetDefinition> ConditionSets => this.config.QCConditionSets;

    // Framework update callback
    public void OnFrameworkUpdate() {
        this.chatQueueTimer = Math.Max(0, this.chatQueueTimer - (float)this.framework.UpdateDelta.TotalSeconds);
        ProcessKeybinds();
    }

    public QCBarDefinition AddBar(string name) {
        var bar = new QCBarDefinition {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
        };
        this.config.QCBars.Add(bar);
        return bar;
    }

    public void RemoveBar(string id) {
        this.config.QCBars.RemoveAll(b => b.Id == id);
    }

    public QCShortcutDefinition AddShortcut(string name) {
        var shortcut = new QCShortcutDefinition {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
        };
        this.config.QCShortcuts[shortcut.Id] = shortcut;
        return shortcut;
    }

    public void RemoveShortcut(string id) {
        this.config.QCShortcuts.Remove(id);
        foreach (var bar in this.config.QCBars) {
            bar.ShortcutIds.RemoveAll(sid => sid == id);
        }
        foreach (var shortcut in this.config.QCShortcuts.Values) {
            shortcut.ChildShortcutIds.RemoveAll(cid => cid == id);
        }
    }

    public QCConditionSetDefinition AddConditionSet(string name) {
        var set = new QCConditionSetDefinition {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
        };
        this.config.QCConditionSets.Add(set);
        return set;
    }

    public void RemoveConditionSet(string id) {
        this.config.QCConditionSets.RemoveAll(cs => cs.Id == id);
        foreach (var bar in this.config.QCBars) {
            if (bar.ConditionSetId == id) bar.ConditionSetId = null;
        }
    }

    public void ClearAllData() {
        this.config.QCBars.Clear();
        this.config.QCShortcuts.Clear();
        this.config.QCConditionSets.Clear();
    }

    public bool IsBarVisible(QCBarDefinition bar) {
        if (string.IsNullOrEmpty(bar.ConditionSetId)) return true;
        var conditionSet = this.config.QCConditionSets.FirstOrDefault(cs => cs.Id == bar.ConditionSetId);
        if (conditionSet is null) return true;
        return EvaluateConditionSet(conditionSet);
    }

    private void UpdateCache() {
        var now = Environment.TickCount64;
        if (now < this.lastCacheUpdateMs + 100) return;
        this.conditionCache.Clear();
        this.lastCacheUpdateMs = now;
    }

    public bool EvaluateConditionSet(QCConditionSetDefinition set) {
        if (set.Conditions.Count == 0 || set.Conditions[0].ConditionType == QCConditionType.None) return true;

        UpdateCache();
        var first = true;
        var prev = true;

        foreach (var entry in set.Conditions) {
            if (entry.ConditionType == QCConditionType.None) continue;

            var result = EvaluateSingleCondition(entry);
            if (first) {
                prev = result;
                first = false;
            } else {
                prev = entry.Operator switch {
                    QCBinaryOperator.AND => prev && result,
                    QCBinaryOperator.OR => prev || result,
                    QCBinaryOperator.EQUALS => prev == result,
                    QCBinaryOperator.XOR => prev ^ result,
                    _ => prev && result,
                };
            }
        }

        return prev;
    }

    private bool EvaluateSingleCondition(QCConditionEntry entry) {
        var cacheKey = $"{entry.ConditionType}_{string.Join(",", entry.TargetIds)}_{entry.Negate}";
        if (this.conditionCache.TryGetValue(cacheKey, out var cached)) return cached;

        var raw = entry.ConditionType switch {
            QCConditionType.InCombat => this.condition[ConditionFlag.InCombat],
            QCConditionType.OutOfCombat => !this.condition[ConditionFlag.InCombat],
            QCConditionType.InInstance => this.condition[ConditionFlag.BoundByDuty],
            QCConditionType.OutOfInstance => !this.condition[ConditionFlag.BoundByDuty],
            QCConditionType.ClassJobId => entry.TargetIds.Count == 0 || entry.TargetIds.Contains(this.objectTable.LocalPlayer?.ClassJob.RowId ?? 0),
            QCConditionType.TerritoryId => entry.TargetIds.Count == 0 || entry.TargetIds.Contains(this.clientState.TerritoryType),
            QCConditionType.Mounted => this.condition[ConditionFlag.Mounted],
            QCConditionType.Swimming => this.condition[ConditionFlag.Swimming],
            QCConditionType.Crafting => this.condition[ConditionFlag.Crafting],
            QCConditionType.Gathering => this.condition[ConditionFlag.Gathering],
            QCConditionType.BetweenAreas => this.condition[ConditionFlag.BetweenAreas],
            QCConditionType.BoundByDuty56 => this.condition[ConditionFlag.BoundByDuty56],
            QCConditionType.WaitingForDuty => this.condition[ConditionFlag.WaitingForDuty],
            QCConditionType.ConditionSet => EvaluateConditionSetByIndex(entry.TargetIds),
            _ => true,
        };

        var result = entry.Negate ? !raw : raw;
        this.conditionCache[cacheKey] = result;
        return result;
    }

    private bool EvaluateConditionSetByIndex(List<uint> targetIds) {
        if (targetIds.Count == 0) return true;
        var index = (int)targetIds[0];
        if (index < 0 || index >= this.config.QCConditionSets.Count) return true;
        return EvaluateConditionSet(this.config.QCConditionSets[index]);
    }

    // --- Command Queue System ---

    public void EnqueueCommand(string command) {
        if (string.IsNullOrWhiteSpace(command)) return;
        foreach (var c in command.Split('\n')) {
            var trimmed = c.Trim();
            if (trimmed.Length > 0) {
                this.commandQueue.Enqueue(trimmed[..Math.Min(trimmed.Length, 180)]);
            }
        }
    }

    public void ProcessCommandQueue() {
        if (this.commandQueue.Count == 0) return;

        // Handle retry items
        if (this.retryItem > 0) {
            UseItem(this.retryItem);
            this.retryItem = 0;
            return;
        }

        var now = Environment.TickCount64;
        if (now < this.lastCommandTimeMs + CommandCooldownMs) return;

        var command = this.commandQueue.Dequeue();
        this.lastCommandTimeMs = now;

        // Support for "//" prefix: internal commands
        if (command.StartsWith("//")) {
            var internalCmd = command[2..].Trim();
            if (internalCmd.Length > 0) {
                ProcessInternalCommand(internalCmd);
            }
            return;
        }

        // Support for "/" prefix: execute as game command
        if (command.StartsWith("/")) {
            ExecuteGameCommand(command);
            return;
        }

        // Support for "!" prefix: alternate command prefix
        if (command.StartsWith("!")) {
            ExecuteGameCommand(command[1..]);
            return;
        }

        // Fallback: send as chat text
        SendChatMessage(command);
    }

    private void ProcessInternalCommand(string command) {
        if (command.Length == 0) return;

        // Parse: command letter + optional space + arguments
        var cmdChar = command[0];
        var args = command.Length > 1 ? command[1..].Trim() : string.Empty;

        switch (cmdChar) {
            case 'm': // //m<index> - Execute macro, or //m to enter/exit macro mode
                HandleMacroCommand(args);
                break;
            case 'i': // //i<id_or_name> - Use item
                HandleItemCommand(args);
                break;
            case 'g': // //g<gearset#> - Equip gearset, or //gearset <#> 
                if (args.Length > 0) {
                    ExecuteGameCommand($"/gearset change {args}");
                }
                break;
            case 't': // //t<place> - Teleport, or //tp <place>
                if (args.Length > 0) {
                    ExecuteGameCommand($"/tp {args}");
                }
                break;
            case 'e': // //e<text> - Echo/debug output
                if (args.Length > 0) {
                    ExecuteGameCommand($"/echo {args}");
                }
                break;
            case 'w': // //w<marker> <id> - Waymark, or //waymark <id>
                if (args.Length > 0) {
                    ExecuteGameCommand($"/waymark {args}");
                }
                break;
            case 'f': // //f<name> - Follow target, or //follow
                if (args.Length > 0) {
                    ExecuteGameCommand($"/follow {args}");
                } else {
                    ExecuteGameCommand("/follow");
                }
                break;
            case 's': // //s or //sound <n> - Play sound effect
                if (args.Length > 0) {
                    ExecuteGameCommand($"/sound {(int.TryParse(args, out var se) ? se : 1)}");
                } else {
                    ExecuteGameCommand("/sound 1");
                }
                break;
            case 'l': // //l or //lock - Lock/unlock UI
                ExecuteGameCommand("/lock");
                break;
            case ' ': // //  - comment, do nothing
                break;
            default:
                // Unrecognized internal command, send as-is
                ExecuteGameCommand(command);
                break;
        }
    }

    private void HandleMacroCommand(string arg) {
        if (string.IsNullOrEmpty(arg)) {
            // Toggle macro mode
            if (this.macroMode) {
                // Exit macro mode and execute accumulated lines
                this.macroMode = false;
                ExecuteMacroLines();
            } else {
                // Enter macro mode
                this.macroMode = true;
                this.macroLines.Clear();
            }
            return;
        }

        if (int.TryParse(arg, out var macroIndex) && macroIndex is >= 0 and < 200) {
            // Execute a specific macro by index
            ExecuteMacroByIndex(macroIndex);
        } else {
            // Unknown macro command
            SendChatMessage(arg);
        }
    }

    private unsafe void ExecuteMacroByIndex(int index) {
        var uiModule = Framework.Instance()->GetUIModule();
        if (uiModule == null) return;

        var macroModule = uiModule->GetRaptureMacroModule();
        if (macroModule == null) return;

        try {
            if (index < 100) {
                // Individual macro (0-99)
                fixed (void* ptr = &macroModule->Individual[index]) {
                    var shellModule = uiModule->GetRaptureShellModule();
                    if (shellModule != null) {
                        shellModule->ExecuteMacro((RaptureMacroModule.Macro*)ptr);
                    }
                }
            } else {
                // Shared macro (100-199)
                fixed (void* ptr = &macroModule->Shared[index - 100]) {
                    var shellModule = uiModule->GetRaptureShellModule();
                    if (shellModule != null) {
                        shellModule->ExecuteMacro((RaptureMacroModule.Macro*)ptr);
                    }
                }
            }
        } catch {
            // Fallback
        }
    }

    private void ExecuteMacroLines() {
        if (this.macroLines.Count == 0) return;

        // Execute each line as a command
        foreach (var line in this.macroLines) {
            this.commandQueue.Enqueue(line);
        }
        this.macroLines.Clear();
    }

    private void HandleItemCommand(string arg) {
        if (string.IsNullOrEmpty(arg)) return;

        if (uint.TryParse(arg, out var itemId)) {
            UseItem(itemId);
        } else {
            // Try to use item by name (simplified - raw use)
            SendChatMessage($"/item \"{arg}\"");
        }
    }

    private unsafe void UseItem(uint itemId) {
        if (itemId == 0) return;

        // Use ActionManager.UseAction - the safe Dalamud-compatible way
        // This avoids raw AgentInventoryContext calls that could crash the game
        try {
            var actionManager = ActionManager.Instance();
            if (actionManager is null) {
                SendChatMessage($"/itemsearch {itemId}");
                return;
            }

            // Check if the action ID is valid for this item
            var actionId = ActionManager.GetSpellIdForAction(ActionType.Item, itemId);
            if (actionId == 0) {
                // Retry once (item may not be loaded yet)
                this.retryItem = itemId;
                return;
            }

            // UseAction is the safe Dalamud API for using items
            actionManager->UseAction(ActionType.Item, itemId);
        } catch {
            // Fallback to command
            SendChatMessage($"/itemsearch {itemId}");
        }
    }

    private void ExecuteGameCommand(string command) {
        this.commandManager.ProcessCommand(command);
    }

    private void SendChatMessage(string message) {
        if (this.chatQueueTimer > 0) {
            // Queue the message for later
            this.commandQueue.Enqueue(message);
            return;
        }

        this.chatQueueTimer = ChatSendCooldown;
        this.commandManager.ProcessCommand(message);
    }

    public void ExecuteShortcut(QCShortcutDefinition shortcut) {
        if (shortcut.Type == QCShortcutType.Spacer) return;

        if (shortcut.IsCategory) return; // Categories are handled by UI interaction

        var commands = GetCommandLines(shortcut);
        if (commands.Count == 0) return;

        // If in macro mode, accumulate commands instead of executing
        if (this.macroMode) {
            this.macroLines.AddRange(commands);
            return;
        }

        switch (shortcut.Mode) {
            case QCShortcutMode.Normal:
                foreach (var cmd in commands) {
                    EnqueueCommand(cmd);
                }
                break;
            case QCShortcutMode.Incremental:
                if (shortcut.IncrementalIndex >= commands.Count) shortcut.IncrementalIndex = 0;
                EnqueueCommand(commands[shortcut.IncrementalIndex]);
                shortcut.IncrementalIndex++;
                break;
            case QCShortcutMode.Random:
                var randomIndex = Random.Shared.Next(commands.Count);
                EnqueueCommand(commands[randomIndex]);
                break;
        }
    }

    private static List<string> GetCommandLines(QCShortcutDefinition shortcut) {
        if (string.IsNullOrWhiteSpace(shortcut.Command)) return [];
        return shortcut.Command
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToList();
    }

    // --- Keybind Processing (using IKeyState for Dalamud compliance) ---

    public void ProcessKeybinds() {
        foreach (var shortcut in this.config.QCShortcuts.Values) {
            if (shortcut.Hotkey == 0 || shortcut.Type == QCShortcutType.Spacer) continue;
            if (IsVkKeyPressed(shortcut.Hotkey)) {
                if (!shortcut.KeyPassthrough) {
                    ExecuteShortcut(shortcut);
                }
            }
        }
    }

    private bool IsVkKeyPressed(int vkCode) {
        if (vkCode <= 0 || vkCode > 255) return false;

        // Use IKeyState from Dalamud instead of GetAsyncKeyState
        var rawState = this.keyState[vkCode];

        // Prevent repeat firing: only trigger on transition from not-pressed to pressed
        var wasPressed = this.previousKeyStates.GetValueOrDefault(vkCode, false);
        this.previousKeyStates[vkCode] = rawState;

        return rawState && !wasPressed;
    }

    // --- Cooldown Tracking ---

    public unsafe float GetCooldownRemaining(uint actionId) {
        try {
            var actionManager = ActionManager.Instance();
            if (actionManager is null) return 0;
            return actionManager->GetRecastTime(ActionType.Action, actionId);
        } catch {
            return 0;
        }
    }

    public unsafe float GetCooldownMax(uint actionId) {
        try {
            var actionManager = ActionManager.Instance();
            if (actionManager is null) return 1;
            var remaining = actionManager->GetRecastTime(ActionType.Action, actionId);
            var elapsed = actionManager->GetRecastTimeElapsed(ActionType.Action, actionId);
            var total = remaining + elapsed;
            return total > 0 ? total : 1;
        } catch {
            return 1;
        }
    }

    // --- Helper Methods ---

    // Check if a VK key is currently held down (for pie menus, continuous hold)
    public bool IsVkKeyHeld(int vkCode) {
        if (vkCode <= 0 || vkCode > 255) return false;
        return this.keyState[vkCode];
    }

    public static string GetVkKeyName(int vkCode) {
        return vkCode switch {
            >= 0x30 and <= 0x39 => $"{(char)vkCode}",
            >= 0x41 and <= 0x5A => $"{(char)vkCode}",
            0x01 => "鼠标左键",
            0x02 => "鼠标右键",
            0x04 => "鼠标中键",
            0x05 => "鼠标X1",
            0x06 => "鼠标X2",
            0x09 => "Tab",
            0x10 => "Shift",
            0x11 => "Ctrl",
            0x12 => "Alt",
            0x1B => "Esc",
            0x20 => "Space",
            0x25 => "←",
            0x26 => "↑",
            0x27 => "→",
            0x28 => "↓",
            >= 0x70 and <= 0x7B => $"F{vkCode - 0x6F}",
            _ => $"VK {vkCode}",
        };
    }

    }
