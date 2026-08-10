using AllHud.Services;
using AllHud.Windows;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AllHud;

public sealed class Plugin : IDalamudPlugin {
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly CombatStateTracker combatState;
    private readonly OverlayRenderer overlayRenderer;
    private readonly ConfigWindow configWindow;
    private readonly QCManager qcManager;
    private readonly QCRenderer qcRenderer;
    private readonly QCConfigPage qcConfigPage;
    private readonly IFramework framework;
    private readonly ICommandManager commandManager;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IDataManager dataManager,
        IClientState clientState,
        ICondition condition,
        IFramework framework,
        IObjectTable objectTable,
        IPartyList partyList,
        ITargetManager targetManager,
        IGameGui gameGui,
        IAddonEventManager addonEventManager,
        IGameInteropProvider gameInteropProvider,
        ITextureProvider textureProvider,
        ICommandManager commandManager,
        IGameConfig gameConfig,
        IGameInventory gameInventory,
        IDtrBar dtrBar,
        IPluginLog log,
        IKeyState keyState) {
        this.pluginInterface = pluginInterface;
        this.log = log;
        this.framework = framework;
        this.commandManager = commandManager;

        try {
            this.log.Information("AllHud initializing: loading configuration.");
            this.config = this.pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            if (this.config.ApplyMigrations()) {
                this.log.Information("AllHud initializing: saving migrated configuration.");
                this.pluginInterface.SavePluginConfig(this.config);
            }

            this.pluginInterface.UiBuilder.OverrideGameCursor = false;

            this.log.Information("AllHud initializing: creating combat tracker.");
            this.combatState = new CombatStateTracker(dataManager, clientState, condition, framework, objectTable, partyList, targetManager, gameGui, gameInteropProvider, log);

            this.log.Information("AllHud initializing: creating overlay renderer.");
            this.overlayRenderer = new OverlayRenderer(this.config, this.combatState, dataManager, textureProvider, gameGui, addonEventManager, commandManager, gameConfig, gameInventory, clientState, objectTable, dtrBar, this.pluginInterface, SaveConfig);

            this.log.Information("AllHud initializing: creating QC module.");
            this.qcManager = new QCManager(this.config, log, commandManager, condition, clientState, objectTable, framework, keyState);
            this.qcRenderer = new QCRenderer(this.qcManager, this.config, textureProvider, this.pluginInterface, log);
            this.qcConfigPage = new QCConfigPage(this.qcManager, this.config, SaveConfig);

            this.log.Information("AllHud initializing: registering /qc command.");
            commandManager.AddHandler("/qc", new CommandInfo(OnQcCommand) {
                HelpMessage = "打开QC快捷栏配置界面。",
            });

            this.log.Information("AllHud initializing: creating config window.");
            this.configWindow = new ConfigWindow(this.config, this.combatState, textureProvider, this.pluginInterface, dataManager, SaveConfig, this.qcConfigPage);

            this.log.Information("AllHud initializing: registering UI callbacks.");
            this.pluginInterface.UiBuilder.Draw += Draw;
            this.pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
            this.pluginInterface.UiBuilder.OpenMainUi += OpenConfigUi;
            framework.Update += OnFrameworkUpdate;

            this.log.Information("AllHud loaded.");
        } catch (Exception ex) {
            this.log.Error(ex, "AllHud failed during initialization.");
            throw;
        }
    }

    public void Dispose() {
        this.pluginInterface.UiBuilder.Draw -= Draw;
        this.pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi;
        this.pluginInterface.UiBuilder.OpenMainUi -= OpenConfigUi;
        this.framework.Update -= OnFrameworkUpdate;
        this.pluginInterface.UiBuilder.OverrideGameCursor = true;
        this.commandManager.RemoveHandler("/qc");
        this.overlayRenderer.Dispose();
        this.qcRenderer.Dispose();
        this.qcManager.Dispose();
        this.combatState.Dispose();
        this.log.Information("AllHud disposed.");
    }

    private void OnFrameworkUpdate(IFramework framework) {
        this.qcManager.OnFrameworkUpdate();
        this.qcManager.ProcessCommandQueue();
    }

    private void Draw() {
        this.overlayRenderer.Draw();
        this.qcRenderer.DrawAllBars();
        this.configWindow.Draw();
    }

    private void OpenConfigUi() {
        this.configWindow.IsOpen = !this.configWindow.IsOpen;
    }

    private void OnQcCommand(string command, string arguments) {
        this.configWindow.IsOpen = true;
        this.configWindow.SelectQcTab();
    }

    private void SaveConfig() {
        this.pluginInterface.SavePluginConfig(this.config);
    }

}