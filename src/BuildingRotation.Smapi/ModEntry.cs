using BuildingRotation.Runtime;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace BuildingRotation.Smapi;

// Source for the first load/diagnostic gate, not a gameplay adapter. Compile against the
// user's assemblies before claiming this entry loads. No hooks, game writes or hotkeys yet.
public sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.ConsoleCommands.Add("br_status", "Report Building Rotation dependencies and integration status.",
            (_, _) => Report());
        Report();
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) => Report();

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        => Monitor.Log("Returned to title. No rotation sessions or save-specific caches are retained by this diagnostic entry.", LogLevel.Trace);

    private void Report()
    {
        Monitor.Log($"Game assembly: {typeof(Game1).Assembly.GetName().Version}; SMAPI assembly: {typeof(Mod).Assembly.GetName().Version}; runtime: {typeof(FacingData).Assembly.GetName().Version}.", LogLevel.Info);
        Monitor.Log("Diagnostic loader only: move-provider, rendering, collision, warp and live-save bindings are not installed.", LogLevel.Info);
    }
}
