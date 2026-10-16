namespace Mc.Scripting.Essentials.Commands;

/// <summary>Registers every command tree of the plugin.</summary>
internal static class CommandTrees
{
    internal static void Register(EssentialsPlugin plugin)
    {
        TeleportCommands.Register(plugin);
        PlayerCommands.Register(plugin);
        EconomyCommands.Register(plugin);
    }

    /// <summary>Online player names, for suggestions.</summary>
    internal static IEnumerable<string> OnlineNames() => Server.Players.Select(p => p.Name);

    /// <summary>The online player with the id, or null.</summary>
    internal static Player? Online(string id)
    {
        foreach (var player in Server.Players)
        {
            if (EssentialsPlugin.KeyOf(player) == id) return player;
        }
        return null;
    }

    /// <summary>A short duration: <c>1h 2m 3s</c>, leaving out the larger units when they are zero.</summary>
    internal static string Duration(long seconds)
    {
        long hours = seconds / 3600, minutes = seconds % 3600 / 60, rest = seconds % 60;
        if (hours > 0) return $"{hours}h {minutes}m {rest}s";
        if (minutes > 0) return $"{minutes}m {rest}s";
        return $"{rest}s";
    }

    /// <summary>The text for a store result.</summary>
    internal static string StoreMessage(Core.StoreResult result, string name, string done) => result switch
    {
        Core.StoreResult.Ok => done,
        Core.StoreResult.Invalid => "Names use letters, digits, '_' and '-' (up to 32 characters).",
        Core.StoreResult.NotFound => $"There is no '{name}'.",
        Core.StoreResult.Exists => $"'{name}' already exists.",
        Core.StoreResult.Limit => "You have reached your home limit.",
        _ => "Nothing changed.",
    };
}
