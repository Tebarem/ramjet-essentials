using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mc.Scripting.Essentials.Settings;

/// <summary>JSON source generation for the configuration and the data files: no reflection at load, kebab-case property names.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.KebabCaseLower,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(EssentialsConfig))]
[JsonSerializable(typeof(HomesDoc))]
[JsonSerializable(typeof(WarpsDoc))]
[JsonSerializable(typeof(EconomyDoc))]
[JsonSerializable(typeof(PlayersDoc))]
internal sealed partial class EssentialsJson : JsonSerializerContext;
