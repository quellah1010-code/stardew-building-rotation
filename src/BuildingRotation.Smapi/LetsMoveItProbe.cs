using System.Collections;
using System.Reflection;
using StardewModdingAPI;
using StardewValley.Buildings;

namespace BuildingRotation.Smapi;

// Temporary read-only diagnostic for the inspected SMAPI 4.5.2 / Let's Move It
// 0.6.20 binaries. These private members aren't a public compatibility API.
// This probe never grants RotationEditor input ownership or writes live state.
internal sealed class LetsMoveItProbe
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly object mod;
    private readonly FieldInfo singleTarget;
    private readonly FieldInfo multipleTargets;
    private readonly FieldInfo config;
    private readonly PropertyInfo targetObject;
    private readonly PropertyInfo modEnabled;
    private readonly PropertyInfo copyMode;
    private readonly PropertyInfo multiSelect;

    public LetsMoveItProbe(IModHelper helper)
    {
        if (Constants.ApiVersion.ToString() != "4.5.2")
            throw new NotSupportedException("Selection diagnostics currently expect SMAPI 4.5.2.");
        IModInfo info = helper.ModRegistry.Get("Exblosis.LetsMoveIt")
            ?? throw new NotSupportedException("Let's Move It is not loaded.");
        if (info.Manifest.Version.ToString() != "0.6.20")
            throw new NotSupportedException("Selection diagnostics currently expect Let's Move It 0.6.20.");

        // ModMetadata.Mod was verified in the supplied SMAPI binary. Retain only
        // the mod instance, not a target, location, building or config snapshot.
        mod = Property(info.GetType(), "Mod").GetValue(info)
            ?? throw new InvalidOperationException("Let's Move It has no active mod instance.");
        Type type = mod.GetType();
        if (type.FullName != "LetsMoveIt.ModEntry")
            throw new NotSupportedException("Unexpected Let's Move It entry type.");
        singleTarget = Field(type, "SingleTarget");
        multipleTargets = Field(type, "MultipleTargets");
        config = Field(type, "Config");
        targetObject = Property(singleTarget.FieldType, "TargetObject");
        modEnabled = Property(config.FieldType, "ModEnabled");
        copyMode = Property(config.FieldType, "CopyMode");
        multiSelect = Property(config.FieldType, "MultiSelect");
    }

    public string Read()
    {
        if (!Context.IsWorldReady) return "not in a save";
        object settings = config.GetValue(mod)
            ?? throw new InvalidOperationException("Let's Move It config is unavailable.");
        string flags = $"enabled={modEnabled.GetValue(settings)}, copy={copyMode.GetValue(settings)}, multi={multiSelect.GetValue(settings)}";
        var targets = multipleTargets.GetValue(mod) as IDictionary
            ?? throw new InvalidOperationException("Unexpected multi-selection collection.");
        if (targets.Count > 0)
            return $"multiple selection ({targets.Count} tile groups); {flags}";

        object? target = singleTarget.GetValue(mod);
        object? held = target == null ? null : targetObject.GetValue(target);
        if (held == null) return $"nothing held; {flags}";
        if (held is Building building)
            return $"building={building.buildingType.Value}; origin=({building.tileX.Value},{building.tileY.Value}); footprint={building.tilesWide.Value}x{building.tilesHigh.Value}; {flags}";
        return $"non-building target={held.GetType().Name}; {flags}";
    }

    private static FieldInfo Field(Type type, string name)
        => type.GetField(name, Members) ?? throw new MissingFieldException(type.FullName, name);

    private static PropertyInfo Property(Type type, string name)
        => type.GetProperty(name, Members) ?? throw new MissingMemberException(type.FullName, name);
}
