using System.Text.Json;
using Mc.Scripting.Essentials.Core;
using Mc.Scripting.Essentials.Settings;

namespace Mc.Scripting.Essentials.Tests;

/// <summary>The configuration and the data documents through the JSON context.</summary>
internal static class SettingsTests
{
    internal static void Register(Action<string, Action> add)
    {
        add("config: defaults round trip, keys are kebab-case", () =>
        {
            string json = JsonSerializer.Serialize(new EssentialsConfig(), EssentialsJson.Default.EssentialsConfig);
            Check.True(json.Contains("\"currency-symbol\"", StringComparison.Ordinal), "kebab-case key written");
            var back = JsonSerializer.Deserialize(json, EssentialsJson.Default.EssentialsConfig);
            Check.True(back is not null, "read back");
            Check.Equal(3, back!.Homes.DefaultLimit, "default home limit");
            Check.Equal(2, back.Kits["starter"].Items.Count, "starter kit items");
            Check.Equal("$", back.Economy.CurrencySymbol, "currency symbol");
        });

        add("config: comments and trailing commas are read", () =>
        {
            const string text = "{ // written by hand\n \"economy\": { \"currency-symbol\": \"E\", \"fraction-digits\": 0, }, }";
            var config = JsonSerializer.Deserialize(text, EssentialsJson.Default.EssentialsConfig);
            Check.True(config is not null, "parsed");
            Check.Equal("E", config!.Economy.CurrencySymbol, "symbol read");
            Check.Equal(0, config.Economy.FractionDigits, "digits read");
            Check.Equal(120, config.Teleport.RequestExpirySeconds, "unset keys keep their defaults");
        });

        add("config: an item entry the parser refuses is reported, not guessed", () =>
        {
            var config = new EssentialsConfig();
            config.Kits["vip"] = new KitConfig { Items = ["minecraft:gold_ingot 3", "gold ingot"] };
            var book = new KitBook();
            var errors = new List<string>();
            book.Load(config.Kits, errors);
            Check.Equal(1, errors.Count, "one error for the spaced name");
        });

        add("data docs: a homes file keeps its locations through JSON", () =>
        {
            var doc = new HomesDoc();
            doc.Players["u"] = new Dictionary<string, StoredLocation> { ["a"] = new("minecraft:overworld", 1.5, 64, -3, 90f, 10f) };
            string text = JsonSerializer.Serialize(doc, EssentialsJson.Default.HomesDoc);
            var back = JsonSerializer.Deserialize(text, EssentialsJson.Default.HomesDoc);
            Check.True(back is not null && back.Players["u"]["a"] == new StoredLocation("minecraft:overworld", 1.5, 64, -3, 90f, 10f), "location kept");
        });
    }
}
