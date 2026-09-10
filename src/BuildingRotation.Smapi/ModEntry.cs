using BuildingRotation.Runtime;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace BuildingRotation.Smapi;

// Diagnostic package for the supplied game 1.6.15 / SMAPI 4.5.2 files.
// Observe selection only: no Harmony patches, game writes or hotkeys.
public sealed class ModEntry : Mod
{
    private LetsMoveItProbe? moveProbe;
    private string probeStatus = "waiting for GameLaunched";
    private string? lastSelection;

    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.ConsoleCommands.Add("br_status", "Report Building Rotation dependencies and integration status.",
            (_, _) => Report());
        Report();
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        try
        {
            moveProbe = new LetsMoveItProbe(Helper);
            probeStatus = "read-only selection probe attached";
        }
        catch (Exception ex)
        {
            probeStatus = $"selection probe unavailable: {ex.Message}";
            Monitor.Log(probeStatus, LogLevel.Warn);
        }
        Report();
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) => Report();

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        lastSelection = null;
        Monitor.Log("Returned to title; selection report cleared.", LogLevel.Trace);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
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
        Monitor.Log("Diagnostic loader only: move-provider, rendering, collision, warp and live-save bindings are not installed.", LogLevel.Info);
        Monitor.Log(probeStatus, LogLevel.Info);
        ReportSelection(true);
    }
}
