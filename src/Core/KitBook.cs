using System.Globalization;
using Mc.Scripting.Essentials.Settings;

namespace Mc.Scripting.Essentials.Core;

/// <summary>One stack in a kit: the item id (with its namespace) and the count.</summary>
public sealed record KitItem(string Type, int Count);

/// <summary>A kit: its name, the cooldown between two claims and the stacks it gives.</summary>
public sealed record Kit(string Name, long DelaySeconds, IReadOnlyList<KitItem> Items);

/// <summary>The configured kits, parsed. Pure: an item entry is <c>&lt;item&gt; [count]</c>, the item with or without <c>minecraft:</c>; the
/// count is 1 when omitted. Entries that do not parse are reported and skipped, never guessed.</summary>
public sealed class KitBook
{
    private const int MaxCount = 6400;

    private readonly Dictionary<string, Kit> _kits = new();

    /// <summary>The kit names, sorted.</summary>
    public IReadOnlyList<string> Names => _kits.Keys.Order(StringComparer.Ordinal).ToList();

    /// <summary>The kit with that name.</summary>
    public bool TryGet(string name, out Kit kit) => _kits.TryGetValue(Core.Names.Key(name), out kit!);

    /// <summary>Replaces the kits with the configured ones.</summary>
    /// <param name="config">The kit sections from the config file, by name.</param>
    /// <param name="errors">Receives one line per entry that was skipped.</param>
    public void Load(IReadOnlyDictionary<string, KitConfig> config, List<string> errors)
    {
        _kits.Clear();
        foreach (var (name, section) in config)
        {
            if (!Core.Names.IsValid(name))
            {
                errors.Add($"kit '{name}': the name breaks the name rules, kit skipped");
                continue;
            }
            var items = new List<KitItem>();
            foreach (string line in section.Items)
            {
                if (TryParseItem(line, out var item)) items.Add(item);
                else errors.Add($"kit '{name}': item '{line}' does not parse, entry skipped");
            }
            _kits[Core.Names.Key(name)] = new Kit(name, Math.Max(0, section.DelaySeconds), items);
        }
    }

    /// <summary>Parses <c>&lt;item&gt; [count]</c>.</summary>
    public static bool TryParseItem(string? line, out KitItem item)
    {
        item = null!;
        if (string.IsNullOrWhiteSpace(line)) return false;
        string[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 2) return false;

        string type = parts[0].Contains(':', StringComparison.Ordinal) ? parts[0] : "minecraft:" + parts[0];
        if (!IsItemId(type)) return false;

        int count = 1;
        if (parts.Length == 2 && (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out count) || count is < 1 or > MaxCount))
        {
            return false;
        }
        item = new KitItem(type, count);
        return true;
    }

    private static bool IsItemId(string id)
    {
        int colon = id.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0 || colon == id.Length - 1 || id.IndexOf(':', colon + 1) >= 0) return false;
        foreach (char c in id)
        {
            if (!(c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or ':' or '/' or '.' or '-')) return false;
        }
        return true;
    }
}
