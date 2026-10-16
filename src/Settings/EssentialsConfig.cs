namespace Mc.Scripting.Essentials.Settings;

/// <summary>The configuration file (<c>config.json</c>). Property names are written kebab-case by the JSON context; defaults are written on first
/// run. Money is text (<c>"12.50"</c>), never a floating-point number.</summary>
public sealed class EssentialsConfig
{
    /// <summary>The currency and the balance limits.</summary>
    public EconomySettings Economy { get; set; } = new();

    /// <summary>Home limits.</summary>
    public HomeSettings Homes { get; set; } = new();

    /// <summary>Teleport warm-up, cooldown, safety and request expiry.</summary>
    public TeleportSettings Teleport { get; set; } = new();

    /// <summary>Spawn behaviour on join.</summary>
    public SpawnSettings Spawn { get; set; } = new();

    /// <summary>The kits, by name.</summary>
    public Dictionary<string, KitConfig> Kits { get; set; } = new()
    {
        ["starter"] = new KitConfig { DelaySeconds = 3600, Items = ["minecraft:bread 8", "minecraft:stone_sword"] },
    };
}

/// <summary>The currency and the balance rules.</summary>
public sealed class EconomySettings
{
    /// <summary>The symbol written before the amount.</summary>
    public string CurrencySymbol { get; set; } = "$";

    /// <summary>The currency name, singular.</summary>
    public string CurrencyName { get; set; } = "dollar";

    /// <summary>The currency name, plural.</summary>
    public string CurrencyPluralName { get; set; } = "dollars";

    /// <summary>Digits after the point (2 = cents). Changing it applies after a restart.</summary>
    public int FractionDigits { get; set; } = 2;

    /// <summary>The balance of a new account.</summary>
    public string StartingBalance { get; set; } = "0.00";

    /// <summary>The largest balance an account may hold. Changing it applies after a restart.</summary>
    public string MaxBalance { get; set; } = "1000000000.00";

    /// <summary>Log every balance change to the server log.</summary>
    public bool LogTransactions { get; set; }
}

/// <summary>Home limits. A player's limit is the highest tier whose node (<c>essentials.sethome.multiple.&lt;tier&gt;</c>) they hold.</summary>
public sealed class HomeSettings
{
    /// <summary>Homes for a player without a tier node.</summary>
    public int DefaultLimit { get; set; } = 3;

    /// <summary>Tier name to home count.</summary>
    public Dictionary<string, int> Tiers { get; set; } = new() { ["pro"] = 5, ["elite"] = 10 };
}

/// <summary>Teleport warm-up, cooldown and safety.</summary>
public sealed class TeleportSettings
{
    /// <summary>Seconds a player waits before a teleport happens. Moving further than <see cref="MoveTolerance"/> cancels it.</summary>
    public int DelaySeconds { get; set; }

    /// <summary>Seconds between two teleports of one player.</summary>
    public int CooldownSeconds { get; set; }

    /// <summary>Blocks a player may move during a warm-up before it is cancelled.</summary>
    public double MoveTolerance { get; set; } = 0.2;

    /// <summary>Move a destination to a standing height near it when the saved height is inside blocks.</summary>
    public bool Safety { get; set; } = true;

    /// <summary>Seconds a teleport request waits for an answer.</summary>
    public int RequestExpirySeconds { get; set; } = 120;
}

/// <summary>Spawn behaviour.</summary>
public sealed class SpawnSettings
{
    /// <summary>Send a player to the spawn when they join.</summary>
    public bool OnJoin { get; set; }
}

/// <summary>One kit: the cooldown between claims and the stacks it gives. Items are <c>&lt;item&gt; [count]</c>.</summary>
public sealed class KitConfig
{
    /// <summary>Seconds between two claims by one player.</summary>
    public int DelaySeconds { get; set; }

    /// <summary>The stacks, for example <c>"minecraft:bread 8"</c>.</summary>
    public List<string> Items { get; set; } = new();
}
