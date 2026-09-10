using BuildingRotation.Runtime;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace BuildingRotation.Smapi;

// Compiled against the supplied game 1.6.15 / SMAPI 4.3.2 assemblies.
// The user's launch screenshot reports SMAPI 4.5.2; matching references and an
// in-game load check are still needed. No hooks, game writes or hotkeys yet.
public sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.ConsoleCommands.Add("br_status", "Report Building Rotation dependencies and integration status.",
            (_, _) => Report());
        Report();
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e) => Report();

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) => Report();

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        => Monitor.Log("Returned to title. No rotation sessions or save-specific caches are retained by this diagnostic entry.", LogLevel.Trace);

    private void Report()
    {
        Monitor.Log($"Game assembly: {typeof(Game1).Assembly.GetName().Version}; SMAPI assembly: {typeof(Mod).Assembly.GetName().Version}; runtime: {typeof(FacingData).Assembly.GetName().Version}.", LogLevel.Info);
        Monitor.Log($"Save loaded: {Context.IsWorldReady}; Let's Move It loaded: {Helper.ModRegistry.IsLoaded("Exblosis.LetsMoveIt")}. This does not verify its settings or input integration.", LogLevel.Info);
        Monitor.Log("Diagnostic loader only: move-provider, rendering, collision, warp and live-save bindings are not installed.", LogLevel.Info);
    }
}
