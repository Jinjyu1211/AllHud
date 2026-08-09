using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Utility.Signatures;
using System.Numerics;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Component.GUI;

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

    // Command queue for rate limiting
    private readonly Queue<string> commandQueue = [];
    private long lastCommandTimeMs;
    private const double CommandCooldownMs = 100.0; // 100ms between commands
    private bool commandReady = true;

    // Macro mode for //m multi-line macro support
    private bool macroMode;
    private readonly List<string> macroLines = [];

    // Chat rate limiting (QoLBar style: 1/6s per chat command)
    private float chatQueueTimer;
    private readonly Queue<string> chatQueue = [];
    private const float ChatSendCooldown = 1.0f / 6.0f;

    // Retry item tracking
    private uint retryItem;

    // Key repeat prevention
    private readonly Dictionary<int, bool> previousKeyStates = [];
    private const long KeyRepeatDelayMs = 200;

    // --- Game Window Detection (QoLBar style) ---
    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowThreadProcessId(nint handle, out int processId);

    public static bool IsGameFocused {
        get {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == nint.Zero) return false;
            var procId = Environment.ProcessId;
            _ = GetWindowThreadProcessId(activatedHandle, out var activeProcId);
            return activeProcId == procId;
        }
    }

    public static bool IsGameTextInputActive {
        get {
            unsafe {
                try {
                    var uiModule = UIModule.Instance();
                    if (uiModule == null) return false;
                    return uiModule->GetRaptureAtkModule()->AtkModule.IsTextInputActive();
                } catch { return false; }
            }
        }
    }

    public static bool IsMacroRunning {
        get {
            unsafe {
                try {
                    var uiModule = Framework.Instance()->GetUIModule();
                    if (uiModule == null) return false;
                    return uiModule->GetRaptureShellModule()->MacroCurrentLine >= 0;
                } catch { return false; }
            }
        }
    }

    // QoLBar-style ProcessChatBox delegate for direct command injection
    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 48 8B F2 48 8B F9 45 84 C9")]
    public static unsafe delegate* unmanaged<UIModule*, nint, nint, byte, void> ProcessChatBox;

    // QoLBar-style GetCommandHandler for chat command detection
    [Signature("E8 ?? ?? ?? ?? 66 89 06 66 85 C0")]
    public static unsafe delegate* unmanaged<RaptureShellModule*, nint, nint, int> GetCommandHandler;

    public QCManager(Configuration config, IPluginLog log, ICommandManager commandManager,
        ICondition condition, IClientState clientState, IObjectTable objectTable,
        IFramework framework, IKeyState keyState, IGameInteropProvider gameInterop) {
        this.config = config;
        this.log = log;
        this.commandManager = commandManager;
        this.condition = condition;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.keyState = keyState;

        // Initialize signature-scanned function pointers (QoLBar style)
        gameInterop.InitializeFromAttributes(this);
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
        ProcessCommandQueue();
        ProcessKeybinds();

        // Handle retry items
        if (this.retryItem > 0) {
            TryUseItem(this.retryItem);
            this.retryItem = 0;
        }
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

    // --- Command Queue System (QoLBar-style) ---

    public void EnqueueCommand(string command) {
        if (string.IsNullOrWhiteSpace(command)) return;
        // QoLBar 兼容: 游戏未聚焦时不排队命令
        if (!IsGameFocused) return;

        foreach (var c in command.Split('\n')) {
            var trimmed = c.Trim();
            if (trimmed.Length > 0) {
                this.commandQueue.Enqueue(trimmed[..Math.Min(trimmed.Length, 180)]);
            }
        }
    }

    public void ProcessCommandQueue() {
        if (this.commandQueue.Count == 0) return;

        // Handle chat queue (rate-limited chat messages)
        if (this.chatQueueTimer > 0 && this.chatQueue.Count > 0) {
            this.chatQueueTimer -= (float)this.framework.UpdateDelta.TotalSeconds;
            if (this.chatQueueTimer <= 0) {
                var queued = this.chatQueue.Dequeue();
                ExecuteCommand(queued, true);
            }
        }

        // Process command queue
        if (!this.commandReady) return;
        RunCommandQueue();
    }

    private void RunCommandQueue() {
        // QoLBar 兼容: 宏运行时跳过命令队列，避免冲突
        if (IsMacroRunning) return;

        while (this.commandQueue.Count > 0 && this.commandReady) {
            this.commandReady = false;
            var command = this.commandQueue.Dequeue();

            if (command.StartsWith("//")) {
                // Internal command handling (QoLBar style)
                var internalCmd = command[2..];
                RunInternalCommand(internalCmd);
            } else if (command.StartsWith("/")) {
                // Game command: check if it's a chat send command
                var isChat = IsChatSendCommand(command);
                ExecuteCommand(command, isChat);
            } else if (command.StartsWith("!")) {
                // Alternate prefix
                ExecuteCommand(command[1..], false);
            } else {
                // Plain text: send as chat
                ExecuteCommand(command, true);
            }
        }
    }

    private void RunInternalCommand(string cmd) {
        if (cmd.Length == 0) {
            this.commandReady = true;
            return;
        }

        var cmdChar = cmd[0];
        var args = cmd.Length > 1 ? cmd[1..].Trim() : string.Empty;

        switch (cmdChar) {
            case 'm': // //m<index> - Execute macro, or //m to enter/exit macro mode
                HandleMacroCommand(args);
                break;
            case 'i': // //i<id_or_name> - Use item
                HandleItemCommand(args);
                break;
            case 'g': // //g<gearset#> - Equip gearset
                if (args.Length > 0) {
                    ExecuteGameCommand($"/gearset change {args}");
                }
                break;
            case 't': // //t<place> - Teleport
                if (args.Length > 0) {
                    ExecuteGameCommand($"/tp {args}");
                }
                break;
            case 'e': // //e<text> - Echo/debug output
                if (args.Length > 0) {
                    ExecuteGameCommand($"/echo {args}");
                }
                break;
            case 'w': // //w<marker> <id> - Waymark
                if (args.Length > 0) {
                    ExecuteGameCommand($"/waymark {args}");
                }
                break;
            case 'f': // //f<name> - Follow target
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
                this.commandReady = true;
                break;
            default:
                // QoLBar 兼容: 未识别的内部命令, 补上 / 前缀作为游戏命令执行
                ExecuteGameCommand($"/{cmd}");
                break;
        }
    }

    // QoLBar-style chat command detection using GetCommandHandler signature
    private unsafe bool IsChatSendCommand(string command) {
        var split = command.IndexOf(' ');
        if (split < 1) return split == 0 || !command.StartsWith("/");

        // Use signature-scanned GetCommandHandler if available
        if (GetCommandHandler != null) {
            var handler = 0;
            var stringPtr = nint.Zero;
            try {
                var prefix = command[..split];
                var uiModule = Framework.Instance()->GetUIModule();
                if (uiModule == null) return false;
                var shellModule = uiModule->GetRaptureShellModule();
                if (shellModule == null) return false;

                stringPtr = Marshal.AllocHGlobal(QCUtf8String.Size);
                using var str = new QCUtf8String(stringPtr, prefix);
                Marshal.StructureToPtr(str, stringPtr, false);
                handler = GetCommandHandler(shellModule, stringPtr, nint.Zero);
            } catch { }
            Marshal.FreeHGlobal(stringPtr);

            // Chat send commands have specific handler IDs
            return handler switch {
                8 or (>= 13 and <= 20) or (>= 91 and <= 119 and not 116) => true,
                _ => false,
            };
        }

        // Fallback: simple heuristic
        return false;
    }

    private unsafe void ExecuteCommand(string command, bool isChat) {
        if (string.IsNullOrWhiteSpace(command)) {
            this.commandReady = true;
            return;
        }

        // QoLBar 兼容: 文本输入激活时跳过命令注入，避免干扰用户输入
        if (IsGameTextInputActive) {
            this.commandReady = true;
            return;
        }

        // Use QoLBar-style ProcessChatBox if available, otherwise fallback
        if (ProcessChatBox != null) {
            var stringPtr = nint.Zero;
            try {
                var uiModule = UIModule.Instance();
                if (uiModule == null) {
                    // Fallback
                    ExecuteGameCommand(command);
                    return;
                }

                stringPtr = Marshal.AllocHGlobal(QCUtf8String.Size);
                using var str = new QCUtf8String(stringPtr, command);
                Marshal.StructureToPtr(str, stringPtr, false);

                if (isChat) {
                    if (this.chatQueueTimer <= 0) {
                        this.chatQueueTimer = ChatSendCooldown;
                        ProcessChatBox(uiModule, stringPtr, nint.Zero, 0);
                    } else {
                        this.chatQueue.Enqueue(command);
                    }
                } else {
                    ProcessChatBox(uiModule, stringPtr, nint.Zero, 0);
                }
            } catch {
                // Fallback
                ExecuteGameCommand(command);
            } finally {
                Marshal.FreeHGlobal(stringPtr);
            }
        } else {
            // Fallback to ICommandManager
            if (isChat && this.chatQueueTimer > 0) {
                this.chatQueue.Enqueue(command);
            } else {
                if (isChat) this.chatQueueTimer = ChatSendCooldown;
                this.commandManager.ProcessCommand(command);
            }
        }

        // Allow next command to be processed (after cooldown)
        this.lastCommandTimeMs = Environment.TickCount64;
        this.commandReady = true;
    }

    private void HandleMacroCommand(string arg) {
        if (string.IsNullOrEmpty(arg)) {
            // Toggle macro mode (QoLBar style: //m without args enters/exits macro mode)
            if (this.macroMode) {
                // Exit macro mode and execute accumulated lines
                this.macroMode = false;
                ExecuteMacroLines();
            } else {
                // Enter macro mode
                this.macroMode = true;
                this.macroLines.Clear();
            }
            this.commandReady = true;
            return;
        }

        if (int.TryParse(arg, out var macroIndex) && macroIndex is >= 0 and < 200) {
            // Execute a specific macro by index (QoLBar style: //m0 = individual #0, //m100 = shared #0)
            ExecuteMacroByIndex(macroIndex);
        } else {
            // Unknown macro command
            EnqueueCommand($"/{arg}");
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
        this.commandReady = true;
    }

    private void ExecuteMacroLines() {
        if (this.macroLines.Count == 0) {
            this.commandReady = true;
            return;
        }

        // Execute each line as a command
        foreach (var line in this.macroLines) {
            EnqueueCommand(line);
        }
        this.macroLines.Clear();
        this.commandReady = true;
    }

    private void HandleItemCommand(string arg) {
        if (string.IsNullOrEmpty(arg)) return;

        if (uint.TryParse(arg, out var itemId)) {
            TryUseItem(itemId);
        } else {
            // Try to use item by name (simplified - raw use)
            EnqueueCommand($"/itemsearch {arg}");
        }
    }

    private unsafe void TryUseItem(uint itemId) {
        if (itemId == 0) return;

        try {
            var actionManager = ActionManager.Instance();
            if (actionManager is null) {
                EnqueueCommand($"/itemsearch {itemId}");
                return;
            }

            // Check if the action ID is valid for this item (QoLBar style retry)
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
            EnqueueCommand($"/itemsearch {itemId}");
        }
    }

    private void ExecuteGameCommand(string command) {
        var containsNonAscii = false;
        foreach (var c in command) {
            if (c > 127) { containsNonAscii = true; break; }
        }

        if (containsNonAscii) {
            ExecuteNativeCommand(command);
        } else {
            this.commandManager.ProcessCommand(command);
        }
    }

    private unsafe void ExecuteNativeCommand(string command) {
        try {
            // Use QoLBar-style ProcessChatBox delegate if available
            if (ProcessChatBox != null) {
                var uiModule = UIModule.Instance();
                if (uiModule == null) {
                    this.commandManager.ProcessCommand(command);
                    return;
                }

                var stringPtr = nint.Zero;
                try {
                    stringPtr = Marshal.AllocHGlobal(QCUtf8String.Size);
                    using var str = new QCUtf8String(stringPtr, command);
                    Marshal.StructureToPtr(str, stringPtr, false);
                    ProcessChatBox(uiModule, stringPtr, nint.Zero, 0);
                } finally {
                    Marshal.FreeHGlobal(stringPtr);
                }
                return;
            }

            // Fallback: use ProcessChatBoxEntry
            var uiModule2 = UIModule.Instance();
            if (uiModule2 == null) {
                this.commandManager.ProcessCommand(command);
                return;
            }

            var utf8Str = new Utf8String(command);
            uiModule2->ProcessChatBoxEntry(&utf8Str, 0, true);
            utf8Str.Dtor();
        } catch {
            try {
                this.commandManager.ProcessCommand(command);
            } catch {
                // 忽略最终错误
            }
        }
    }

    public void ExecuteShortcut(QCShortcutDefinition shortcut) {
        if (shortcut.Type == QCShortcutType.Spacer) return;

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
        var lines = shortcut.Command
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToList();

        // 处理 QoLBar 风格的 //m 多行宏标记：移除首尾的 //m 标记行
        if (lines.Count >= 2 && lines[0] == "//m" && lines[^1] == "//m") {
            lines = lines[1..^1];
        } else if (lines.Count >= 1 && lines[0] == "//m") {
            // 只有开头的 //m 标记，没有结尾标记
            lines = lines[1..];
        }

        return lines;
    }

    // --- Keybind Processing (using IKeyState for Dalamud compliance) ---

    public void ProcessKeybinds() {
        // QoLBar 兼容: 游戏未聚焦或文本输入时跳过热键处理
        if (!IsGameFocused || IsGameTextInputActive) return;

        foreach (var shortcut in this.config.QCShortcuts.Values) {
            if (shortcut.Hotkey == 0 || shortcut.Type == QCShortcutType.Spacer || shortcut.IsCategory) continue;
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