using AllHud.QC;
using AllHud.Windows;
using AllHud.Markers;
using AllHud.Data;
using AllHud.Models;
using AllHud.Services;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Config;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Inventory;
using Dalamud.Game.ClientState;
using Dalamud.Game;
using System.Reflection;

namespace AllHud;

public sealed class AllHud : IDalamudPlugin {
    private readonly Configuration config;
    private readonly CombatStateTracker combatState;
    private readonly OverlayRenderer overlayRenderer;
    private readonly WorldMarkerSystem worldMarkerSystem;
    private readonly ConfigWindow configWindow;

    private readonly QCManager qcManager;
    private readonly QCRenderer qcRenderer;
    private readonly QCConfigPage qcConfigPage;

    private readonly IPluginLog log;

    public AllHud(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IDataManager dataManager,
        ITextureProvider textureProvider,
        IAddonEventManager addonEventManager,
        IGameGui gameGui,
        ICondition condition,
        IClientState clientState,
        IObjectTable objectTable,
        IPartyList partyList,
        IFramework framework,
        ITargetManager targetManager,
        IGameConfig gameConfig,
        IGameInventory gameInventory,
        IDtrBar dtrBar,
        IKeyState keyState,
        IGameInteropProvider gameInteropProvider,
        IPluginLog log) {
        this.log = log;

        this.log.Information("AllHud initializing: loading configuration.");
        this.config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        this.log.Information("AllHud initializing: creating combat tracker.");
        this.combatState = new CombatStateTracker(dataManager, clientState, condition, framework, objectTable, partyList, targetManager, gameGui, gameInteropProvider, log);

        this.log.Information("AllHud initializing: creating overlay renderer.");
        this.overlayRenderer = new OverlayRenderer(this.config, this.combatState, dataManager, textureProvider, gameGui, addonEventManager, commandManager, gameConfig, gameInventory, clientState, objectTable, dtrBar, this.pluginInterface, SaveConfig);

        this.log.Information("AllHud initializing: creating QC module.");
        this.qcManager = new QCManager(this.config, log, commandManager, condition, clientState, objectTable, framework, keyState, gameInteropProvider);
        this.qcRenderer = new QCRenderer(this.qcManager, this.config, textureProvider, this.pluginInterface);
        this.qcConfigPage = new QCConfigPage(this.qcManager, this.config, SaveConfig);

        this.log.Information("AllHud initializing: registering /qc command.");
        commandManager.AddHandler("/qc", new CommandInfo(OnQcCommand) {
            HelpMessage = "打开QC快捷栏配置界面。",
        });

        this.log.Information("AllHud initializing: creating config window.");
        this.configWindow = new ConfigWindow(this.config, this.combatState, textureProvider, this.pluginInterface, dataManager, SaveConfig, this.qcConfigPage);

        this.log.Information("AllHud initializing: creating world marker system.");
        this.worldMarkerSystem = new WorldMarkerSystem(dataManager, this.config, this.pluginInterface, textureProvider, gameGui, log);

        this.log.Information("AllHud initializing: registering main commands.");
        commandManager.AddHandler("/allhud", new CommandInfo(OnAllHudCommand) {
            HelpMessage = "\u6253\u5F00AllHud\u914D\u7F6E\u754C\u9762\u3002",
        });
        commandManager.AddHandler("/ah", new CommandInfo(OnAllHudCommand) {
            HelpMessage = "\u6253\u5F00AllHud\u914D\u7F6E\u754C\u9762\u3002",
        });

        this.log.Information("AllHud initializing: done.");
    }

    public void Dispose() {
        this.log.Information("AllHud disposing: cleaning up.");
        this.configWindow?.Dispose();
        this.overlayRenderer?.Dispose();
        this.worldMarkerSystem?.Dispose();
        this.qcRenderer?.Dispose();
        this.qcManager?.Dispose();
        this.combatState?.Dispose();
    }

    private void SaveConfig() {
        this.pluginInterface.SavePluginConfig(this.config);
    }

    private void OnAllHudCommand(string command, string arguments) {
        this.configWindow.IsOpen = !this.configWindow.IsOpen;
    }

    private void OnQcCommand(string command, string arguments) {
        this.qcConfigPage.IsOpen = !this.qcConfigPage.IsOpen;
    }
}
