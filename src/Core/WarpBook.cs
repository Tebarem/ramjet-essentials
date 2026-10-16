namespace Mc.Scripting.Essentials.Core;

/// <summary>A named public destination.</summary>
/// <param name="Name">The warp name as it was set.</param>
/// <param name="Location">Where the warp is.</param>
/// <param name="Owner">The id of the player who set it (empty for a warp made by the console).</param>
public sealed record Warp(string Name, StoredLocation Location, string Owner);

/// <summary>Warps and the spawn point. Pure: no engine types. Names are case-insensitive.</summary>
public sealed class WarpBook
{
    private readonly Dictionary<string, Warp> _warps = new();

    /// <summary>The spawn point set with <c>/setspawn</c>; null when none is set (the world spawn is used then).</summary>
    public StoredLocation? Spawn { get; private set; }

    /// <summary>The warp names, sorted.</summary>
    public IReadOnlyList<string> Names => _warps.Values.Select(w => w.Name).Order(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>The warp with that name.</summary>
    public bool TryGet(string name, out Warp warp) => _warps.TryGetValue(Core.Names.Key(name), out warp!);

    /// <summary>Sets a warp, replacing one with the same name.</summary>
    public StoreResult Set(string name, StoredLocation location, string owner)
    {
        if (!Core.Names.IsValid(name)) return StoreResult.Invalid;
        _warps[Core.Names.Key(name)] = new Warp(name, location, owner);
        return StoreResult.Ok;
    }

    /// <summary>Deletes a warp.</summary>
    public StoreResult Remove(string name) => _warps.Remove(Core.Names.Key(name)) ? StoreResult.Ok : StoreResult.NotFound;

    /// <summary>Sets the spawn point.</summary>
    public void SetSpawn(StoredLocation location) => Spawn = location;

    /// <summary>A copy of every warp, for saving.</summary>
    public IReadOnlyList<Warp> Export() => _warps.Values.ToList();

    /// <summary>Replaces every warp and the spawn with the loaded ones.</summary>
    public void Load(IEnumerable<Warp> warps, StoredLocation? spawn)
    {
        _warps.Clear();
        foreach (var warp in warps)
        {
            if (Core.Names.IsValid(warp.Name)) _warps[Core.Names.Key(warp.Name)] = warp;
        }
        Spawn = spawn;
    }
}
