using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length != 2) throw new ArgumentException("Usage: GameAudit <game-directory> <LetsMoveIt.dll>");
using var game = new AssemblyFile(Path.Combine(args[0], "Stardew Valley.dll"));
using var mover = new AssemblyFile(args[1]);
var targets = new (AssemblyFile File, string Type, string Name, string[] Parameters)[]
{
    (game, "StardewValley.Buildings.Building", "occupiesTile", new[] { "Int32", "Int32", "Boolean" }),
    (game, "StardewValley.Buildings.Building", "isTilePassable", new[] { "Microsoft.Xna.Framework.Vector2" }),
    (game, "StardewValley.Buildings.Building", "intersects", new[] { "Microsoft.Xna.Framework.Rectangle" }),
    (game, "StardewValley.Buildings.Building", "draw", new[] { "Microsoft.Xna.Framework.Graphics.SpriteBatch" }),
    (game, "StardewValley.Buildings.Building", "doAction", new[] { "Microsoft.Xna.Framework.Vector2", "StardewValley.Farmer" }),
    (game, "StardewValley.Buildings.Building", "LoadFromBuildingData", new[] { "StardewValley.GameData.Buildings.BuildingData", "Boolean", "Boolean" }),
    (game, "StardewValley.Buildings.Building", "load", Array.Empty<string>()),
    (game, "StardewValley.Buildings.Building", "updateInteriorWarps", new[] { "StardewValley.GameLocation" }),
    (game, "StardewValley.Game1", "performWarpFarmer", new[] { "StardewValley.LocationRequest", "Int32", "Int32", "Int32" }),
    (mover, "LetsMoveIt.ModEntry", "SelectTargetAction", new[] { "StardewModdingAPI.Events.ButtonPressedEventArgs" }),
    (mover, "LetsMoveIt.ModEntry", "SingleTargetAction", new[] { "StardewModdingAPI.Events.ButtonPressedEventArgs" }),
    (mover, "LetsMoveIt.ModEntry", "ClearSelection", Array.Empty<string>()),
    (mover, "LetsMoveIt.TargetData.Target", "Render", new[] { "Microsoft.Xna.Framework.Graphics.SpriteBatch", "StardewValley.GameLocation", "Microsoft.Xna.Framework.Vector2" })
};
foreach (var target in targets)
{
    target.File.Require(target.Type, target.Name, target.Parameters);
    Console.WriteLine($"OK {target.Type}.{target.Name}");
}
Console.WriteLine($"{targets.Length}/{targets.Length} actual binary hook signatures found. No game code executed; Harmony installation and gameplay remain live tests.");

sealed class AssemblyFile : IDisposable
{
    private readonly Stream stream;
    private readonly PEReader pe;
    private readonly MetadataReader metadata;
    public AssemblyFile(string path)
    { stream = File.OpenRead(path); pe = new PEReader(stream); metadata = pe.GetMetadataReader(); }
    public void Require(string typeName, string name, string[] parameters)
    {
        var names = new TypeNames();
        TypeDefinition type = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition)
            .Single(t => metadata.GetString(t.Namespace) + "." + metadata.GetString(t.Name) == typeName);
        var matches = type.GetMethods().Select(metadata.GetMethodDefinition)
            .Where(m => metadata.GetString(m.Name) == name)
            .Where(m => m.DecodeSignature(names, (object?)null).ParameterTypes.SequenceEqual(parameters)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException($"Expected one exact target for {typeName}.{name}, found {matches.Length}.");
        if (matches[0].RelativeVirtualAddress == 0) throw new InvalidOperationException("Target has no IL body.");
    }
    public void Dispose() { pe.Dispose(); stream.Dispose(); }
}

sealed class TypeNames : ISignatureTypeProvider<string, object?>
{
    public string GetPrimitiveType(PrimitiveTypeCode code) => code.ToString();
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    { var t = reader.GetTypeDefinition(handle); return reader.GetString(t.Namespace) + "." + reader.GetString(t.Name); }
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    { var t = reader.GetTypeReference(handle); return reader.GetString(t.Namespace) + "." + reader.GetString(t.Name); }
    public string GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle, byte rawTypeKind)
        => reader.GetTypeSpecification(handle).DecodeSignature(this, context);
    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[,]";
    public string GetSZArrayType(string elementType) => elementType + "[]";
    public string GetByReferenceType(string elementType) => elementType + "&";
    public string GetPointerType(string elementType) => elementType + "*";
    public string GetPinnedType(string elementType) => elementType;
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> arguments) => genericType + "<" + string.Join(",", arguments) + ">";
    public string GetGenericMethodParameter(object? context, int index) => "!!" + index;
    public string GetGenericTypeParameter(object? context, int index) => "!" + index;
    public string GetFunctionPointerType(MethodSignature<string> signature) => "function pointer";
}
