using System.Text.Json.Serialization;

namespace SKUUpdater;


record Asset(string Name, string BrowserDownloadUrl);
record ReleaseInfo(string Name, string TagName, List<Asset> Assets);


[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ReleaseInfo))]
partial class SourceGenerationContext : JsonSerializerContext { }
