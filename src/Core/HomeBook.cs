namespace Mc.Scripting.Essentials.Core;

/// <summary>The outcome of a change to homes, warps or kits.</summary>
public enum StoreResult
{
    /// <summary>Done.</summary>
    Ok,
    /// <summary>The name breaks the name rules.</summary>
    Invalid,
    /// <summary>No entry with that name.</summary>
    NotFound,
    /// <summary>An entry with the new name already exists.</summary>
    Exists,
    /// <summary>The owner has reached the home limit.</summary>
    Limit,
}

/// <summary>Homes of the players, by owner id (the uuid string). Names are case-insensitive. Pure: no engine types. Thread: the caller's
/// (the tick thread in the plugin).</summary>
public sealed class HomeBook
{
    private readonly Dictionary<string, Dictionary<string, StoredLocation>> _homes = new();

    /// <summary>How many homes the owner has.</summary>
    public int Count(string owner) => _homes.TryGetValue(owner, out var homes) ? homes.Count : 0;

    /// <summary>The home with that name.</summary>
    public bool TryGet(string owner, string name, out StoredLocation location)
    {
        location = null!;
        return _homes.TryGetValue(owner, out var homes) && homes.TryGetValue(Core.Names.Key(name), out location!);
    }

    /// <summary>The owner's home names, sorted.</summary>
    public IReadOnlyList<string> Names(string owner) =>
        _homes.TryGetValue(owner, out var homes) ? homes.Keys.Order(StringComparer.Ordinal).ToList() : [];

    /// <summary>Sets a home. Overwriting an existing home does not count against the limit.</summary>
    /// <param name="owner">The owner id.</param>
    /// <param name="name">The home name.</param>
    /// <param name="location">Where the home is.</param>
    /// <param name="limit">The number of homes the owner may have.</param>
    /// <returns>The result.</returns>
    public StoreResult Set(string owner, string name, StoredLocation location, int limit)
    {
        if (!Core.Names.IsValid(name)) return StoreResult.Invalid;
        if (!_homes.TryGetValue(owner, out var homes)) _homes[owner] = homes = new Dictionary<string, StoredLocation>();
        string key = Core.Names.Key(name);
        if (!homes.ContainsKey(key) && homes.Count >= limit) return StoreResult.Limit;
        homes[key] = location;
        return StoreResult.Ok;
    }

    /// <summary>Deletes a home.</summary>
    public StoreResult Remove(string owner, string name)
    {
        if (!_homes.TryGetValue(owner, out var homes) || !homes.Remove(Core.Names.Key(name))) return StoreResult.NotFound;
        if (homes.Count == 0) _homes.Remove(owner);
        return StoreResult.Ok;
    }

    /// <summary>Renames a home, keeping its place.</summary>
    public StoreResult Rename(string owner, string from, string to)
    {
        if (!Core.Names.IsValid(to)) return StoreResult.Invalid;
        if (!_homes.TryGetValue(owner, out var homes) || !homes.TryGetValue(Core.Names.Key(from), out var location)) return StoreResult.NotFound;
        string fromKey = Core.Names.Key(from), toKey = Core.Names.Key(to);
        if (fromKey == toKey) return StoreResult.Ok;
        if (homes.ContainsKey(toKey)) return StoreResult.Exists;
        homes.Remove(fromKey);
        homes[toKey] = location;
        return StoreResult.Ok;
    }

    /// <summary>The home limit a player gets: the highest tier whose permission node the player has, else the fallback.</summary>
    /// <param name="tiers">Tier name to home count.</param>
    /// <param name="hasTier">Whether the player holds the tier's node.</param>
    /// <param name="fallback">The count without any tier.</param>
    public static int ResolveLimit(IReadOnlyDictionary<string, int> tiers, Func<string, bool> hasTier, int fallback)
    {
        int limit = fallback;
        foreach (var (tier, count) in tiers)
        {
            if (count > limit && hasTier(tier)) limit = count;
        }
        return limit;
    }

    /// <summary>A copy for saving: owner to home name to location. Owners without homes are left out.</summary>
    public Dictionary<string, Dictionary<string, StoredLocation>> Export()
    {
        var copy = new Dictionary<string, Dictionary<string, StoredLocation>>();
        foreach (var (owner, homes) in _homes)
        {
            if (homes.Count > 0) copy[owner] = new Dictionary<string, StoredLocation>(homes);
        }
        return copy;
    }

    /// <summary>Replaces every home with the loaded ones.</summary>
    public void Load(IReadOnlyDictionary<string, Dictionary<string, StoredLocation>> loaded)
    {
        _homes.Clear();
        foreach (var (owner, homes) in loaded)
        {
            if (homes.Count > 0) _homes[owner] = new Dictionary<string, StoredLocation>(homes);
        }
    }
}
