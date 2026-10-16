using Mc.Scripting.Essentials.Core;

namespace Mc.Scripting.Essentials.Settings;

/// <summary><c>homes.json</c>: owner id to home name to location.</summary>
public sealed class HomesDoc
{
    /// <summary>The homes, by owner id.</summary>
    public Dictionary<string, Dictionary<string, StoredLocation>> Players { get; set; } = new();
}

/// <summary>One warp in <c>warps.json</c>.</summary>
public sealed class WarpEntry
{
    /// <summary>Where the warp is.</summary>
    public StoredLocation Location { get; set; } = null!;

    /// <summary>The id of the player who set it.</summary>
    public string Owner { get; set; } = "";
}

/// <summary><c>warps.json</c>: the warps by name, and the spawn point when one was set.</summary>
public sealed class WarpsDoc
{
    /// <summary>The warps, by name.</summary>
    public Dictionary<string, WarpEntry> Warps { get; set; } = new();

    /// <summary>The spawn point, or null for the world spawn.</summary>
    public StoredLocation? Spawn { get; set; }
}

/// <summary><c>economy.json</c>: the accounts, by owner id.</summary>
public sealed class EconomyDoc
{
    /// <summary>The accounts.</summary>
    public Dictionary<string, Ledger.Account> Accounts { get; set; } = new();
}

/// <summary><c>players.json</c>: nicks and kit cooldowns (epoch seconds of the last claim), by owner id or by key.</summary>
public sealed class PlayersDoc
{
    /// <summary>The nicks, by owner id.</summary>
    public Dictionary<string, string> Nicks { get; set; } = new();

    /// <summary>The last claim of each kit, keyed <c>kit:&lt;name&gt;:&lt;owner id&gt;</c>.</summary>
    public Dictionary<string, long> KitCooldowns { get; set; } = new();
}
