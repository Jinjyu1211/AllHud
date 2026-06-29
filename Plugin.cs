using AllHud.Services;
using AllHud.Windows;
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
        IPluginLog log) {
        this.pluginInterface = pluginInterface;
        this.log = log;

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

            this.log.Information("AllHud initializing: creating config window.");
            this.configWindow = new ConfigWindow(this.config, this.combatState, textureProvider, this.pluginInterface, dataManager, SaveConfig);

            this.log.Information("AllHud initializing: registering UI callbacks.");
            this.pluginInterface.UiBuilder.Draw += Draw;
            this.pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
            this.pluginInterface.UiBuilder.OpenMainUi += OpenConfigUi;

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
        this.pluginInterface.UiBuilder.OverrideGameCursor = true;
        this.overlayRenderer.Dispose();
        this.combatState.Dispose();
        this.log.Information("AllHud disposed.");
    }

    private void Draw() {
        this.overlayRenderer.Draw();
        this.configWindow.Draw();
    }

    private void OpenConfigUi() {
        this.configWindow.IsOpen = !this.configWindow.IsOpen;
    }

    private void SaveConfig() {
        this.pluginInterface.SavePluginConfig(this.config);
    }

}
