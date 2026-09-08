using System.Text.Json;
using BuildingRotation.ContentAudit;

if (args.Length is not (2 or 4) || args[0] is not ("facts" or "zip") ||
    (args.Length == 4 && args[2] != "--templates"))
{
    Console.Error.WriteLine("Usage: ContentAudit <facts|zip> <input-path> [--templates <output-directory>]");
    return 2;
}
try
{
    var catalog = args[0] == "facts" ? ContentReader.ReadFacts(File.ReadAllBytes(args[1])) : ContentReader.ReadZip(args[1]);
    var results = catalog.Buildings.Values.Select(entry =>
    {
        string? limitation = null;
        try { _ = entry.Data.ToLayoutDefinition(); }
        catch (NotSupportedException ex) { limitation = ex.Message; }
        return new { id = entry.Data.Id, layoutMapped = limitation == null, limitation };
    }).ToArray();
    if (args.Length == 4)
        TemplateExporter.Export(catalog, args[3]);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        catalog.InputKind, catalog.BuildingsJsonSha256, buildingCount = results.Length,
        mappedLayoutCount = results.Count(r => r.layoutMapped), gameRuntimeTested = false, results
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0; // A declared unsupported layout is an audit result, not a claim of runtime compatibility.
}
catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or JsonException or FormatException or OverflowException or NotSupportedException or KeyNotFoundException)
{
    Console.Error.WriteLine($"Content audit failed: {ex.Message}");
    return 1;
}
