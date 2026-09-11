using BuildingRotation.Runtime;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace BuildingRotation.Smapi;

// First ordinary empty Barn prototype; game 1.6.15 / SMAPI 4.5.2 / Let's Move It 0.6.20.
public sealed class ModEntry : Mod
{
    private LetsMoveItProbe? moveProbe;
    private string probeStatus = "waiting for GameLaunched";
    private string? lastSelection;
    private RotationController? rotation;

    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.Saving += (_, _) => rotation?.Cancel(true);
        helper.Events.Display.RenderedWorld += (_, e) => rotation?.DrawPreview(e.SpriteBatch);
        helper.ConsoleCommands.Add("br_status", "Report Building Rotation dependencies and integration status.",
            (_, _) => Report());
        Report();
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        try
        {
            moveProbe = new LetsMoveItProbe(Helper);
            var host = new GameRotationHost();
            rotation = new RotationController(Helper, Monitor, moveProbe, host);
            RotationPatches.Install(rotation, moveProbe, Monitor);
            probeStatus = "ordinary empty Barn prototype attached; placeholder graphics";
        }
        catch (Exception ex)
        {
            rotation = null;
            probeStatus = $"prototype unavailable: {ex.Message}";
            Monitor.Log(probeStatus, LogLevel.Warn);
        }
        Report();
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (rotation != null)
        {
            rotation.Host.Clear();
            foreach (var b in Game1.getFarm().buildings) rotation.Host.Reapply(b);
        }
        Report();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        lastSelection = null;
        rotation?.Cancel(false);
        rotation?.Host.Clear();
        RotationPatches.Clear();
        Monitor.Log("Returned to title; selection report cleared.", LogLevel.Trace);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        rotation?.Tick();
        if (Context.IsWorldReady && e.IsMultipleOf(10)) ReportSelection(false);
    }

    private void ReportSelection(bool force)
    {
        if (moveProbe == null) return;
        try
        {
            string current = moveProbe.Read();
            if (force || current != lastSelection)
                Monitor.Log($"Move observation: {current}", LogLevel.Info);
            lastSelection = current;
        }
        catch (Exception ex)
        {
            // Stop only this observer if private structures differ at runtime.
            moveProbe = null;
            probeStatus = $"selection probe stopped: {ex.Message}";
            Monitor.Log(probeStatus, LogLevel.Warn);
        }
    }

    private void Report()
    {
        Monitor.Log($"Game assembly: {typeof(Game1).Assembly.GetName().Version}; SMAPI: {Constants.ApiVersion}; runtime assembly: {typeof(FacingData).Assembly.GetName().Version}.", LogLevel.Info);
        Monitor.Log($"Save loaded: {Context.IsWorldReady}; Let's Move It loaded: {Helper.ModRegistry.IsLoaded("Exblosis.LetsMoveIt")}. This does not verify its settings or input integration.", LogLevel.Info);
        Monitor.Log("Prototype: ordinary empty Barn only, single player, Let's Move It single selection / MouseLeft / no copy. Hold + sideways drag to turn; click to commit. Gameplay needs testing.", LogLevel.Info);
        Monitor.Log(probeStatus, LogLevel.Info);
        ReportSelection(true);
    }
}
