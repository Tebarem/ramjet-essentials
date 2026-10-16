using Mc.Scripting.Essentials.Commands;
using Mc.Scripting.Essentials.Core;
using Mc.Scripting.Essentials.Settings;

namespace Mc.Scripting.Essentials;

/// <summary>Essentials: homes, warps, spawn, teleport requests, back, kits, nicks, private messages (handed to the chat plugin when it is installed) and
/// the economy, which is the default provider of <see cref="IEconomy"/> at the lowest service priority. Written from the public command reference;
/// the design is in <c>DESIGN.md</c>. No code of the original plugin was read or copied.</summary>
public sealed partial class EssentialsPlugin : Plugin
{
    /// <summary>Ticks per second of game time.</summary>
    internal const long TicksPerSecond = 20;

    private Config<EssentialsConfig> _file = null!;

    /// <summary>The current settings (replaced on <c>/essentials reload</c>).</summary>
    internal EssentialsConfig Options => _file.Value;

    internal HomeBook Homes { get; } = new();
    internal WarpBook Warps { get; } = new();
    internal KitBook Kits { get; } = new();
    internal Cooldowns KitCooldowns { get; } = new();
    internal Cooldowns TeleportCooldowns { get; } = new();
    internal ReplyBook Replies { get; } = new();
    internal Dictionary<string, string> Nicks { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, StoredLocation> Backs { get; } = new(StringComparer.Ordinal);
    internal DataStore Data { get; private set; } = null!;
    internal Ledger Accounts { get; private set; } = null!;
    internal Currency CurrencyInfo { get; private set; } = null!;
    internal long StartingBalance { get; private set; }
    internal TpaBook Tpa { get; private set; } = null!;
    internal Warmups<TeleportJob> Warmups { get; private set; } = null!;

    /// <summary>A teleport waiting for its warm-up: who is moved and where to.</summary>
    internal sealed record TeleportJob(Player Who, StoredLocation Target);

    /// <summary>The player's id as the data files store it (the 32-digit uuid).</summary>
    internal static string KeyOf(Player player) => player.Uuid.ToString();

    /// <summary>The id of a uuid, as the data files store it.</summary>
    internal static string KeyOf(Uuid uuid) => uuid.ToString();

    /// <summary>The current tick (game time).</summary>
    internal static long NowTick => (long)Server.CurrentTick;

    /// <summary>The current time in Unix seconds, for the cooldowns that survive a restart.</summary>
    internal static long NowSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    protected override void OnEnable()
    {
        _file = Config.Load(EssentialsJson.Default.EssentialsConfig);
        ApplySettings();
        Data = new DataStore(this);
        Data.Load();

        Services.Register<IEconomy>(new EssentialsEconomy(this), ServicePriority.Lowest);
        RegisterNodes();
        RegisterEvents();
        CommandTrees.Register(this);
    }

    protected override void OnDisable()
    {
        Data?.FlushNow();
    }

    /// <summary>Re-reads the configuration file. Returns false, and keeps the settings in use, when the file does not parse.</summary>
    internal bool ReloadConfig()
    {
        if (!_file.Reload()) return false;
        ApplySettings();
        return true;
    }

    /// <summary>Reads the settings that the running state depends on. The currency and the balance limits apply on a restart, because a ledger
    /// keeps its maximum; the rest applies at once.</summary>
    private void ApplySettings()
    {
        var economy = Options.Economy;
        int digits = Math.Clamp(economy.FractionDigits, 0, 9);
        CurrencyInfo = new Currency(economy.CurrencyName, economy.CurrencySymbol, digits, economy.CurrencyPluralName);
        if (Accounts is null)
        {
            Accounts = new Ledger(ParseMoney(economy.MaxBalance, digits, "economy.max-balance", 1_000_000_000L * 100));
        }
        StartingBalance = ParseMoney(economy.StartingBalance, digits, "economy.starting-balance", 0);
        Tpa = new TpaBook(Options.Teleport.RequestExpirySeconds * TicksPerSecond);
        Warmups = new Warmups<TeleportJob>(Options.Teleport.MoveTolerance);

        var errors = new List<string>();
        Kits.Load(Options.Kits, errors);
        foreach (string error in errors) Log.Warn(error);
    }

    private long ParseMoney(string text, int digits, string key, long fallback)
    {
        if (Core.MoneyText.TryParse(text, digits, out long minor)) return minor;
        Log.Warn($"config {key} '{text}' is not an amount with {digits} digits, using {fallback}");
        return fallback;
    }

    private void RegisterNodes()
    {
        Permissions.Register(Nodes.SetHome, PermissionDefault.True, "Set a home (/sethome)");
        Permissions.Register(Nodes.Home, PermissionDefault.True, "Teleport to a home (/home)");
        Permissions.Register(Nodes.DelHome, PermissionDefault.True, "Delete a home (/delhome)");
        Permissions.Register(Nodes.RenameHome, PermissionDefault.True, "Rename a home (/renamehome)");
        foreach (string tier in Options.Homes.Tiers.Keys)
        {
            Permissions.Register(Nodes.HomeTierPrefix + tier, PermissionDefault.False, $"Home limit tier '{tier}' (see config homes.tiers)");
        }
        Permissions.Register(Nodes.SetWarp, PermissionDefault.OpLevel(2), "Set a warp (/setwarp)");
        Permissions.Register(Nodes.Warp, PermissionDefault.True, "Teleport to a warp, or list warps (/warp)");
        Permissions.Register(Nodes.DelWarp, PermissionDefault.OpLevel(2), "Delete a warp (/delwarp)");
        Permissions.Register(Nodes.Spawn, PermissionDefault.True, "Teleport to the spawn (/spawn)");
        Permissions.Register(Nodes.SetSpawn, PermissionDefault.OpLevel(2), "Set the spawn (/setspawn)");
        Permissions.Register(Nodes.Tpa, PermissionDefault.True, "Ask to teleport to a player (/tpa)");
        Permissions.Register(Nodes.TpaHere, PermissionDefault.True, "Ask a player to teleport to you (/tpahere)");
        Permissions.Register(Nodes.TpAccept, PermissionDefault.True, "Accept a teleport request (/tpaccept)");
        Permissions.Register(Nodes.TpDeny, PermissionDefault.True, "Deny a teleport request (/tpdeny)");
        Permissions.Register(Nodes.TpaCancel, PermissionDefault.True, "Cancel your teleport request (/tpacancel)");
        Permissions.Register(Nodes.Back, PermissionDefault.True, "Return to the place before the last teleport or death (/back)");
        Permissions.Register(Nodes.TpAdmin, PermissionDefault.OpLevel(2), "Teleport to a player or coordinates without asking (/etp)");
        Permissions.Register(Nodes.CooldownBypass, PermissionDefault.OpLevel(2), "Skip the teleport cooldown");
        Permissions.Register(Nodes.WarmupBypass, PermissionDefault.OpLevel(2), "Skip the teleport warm-up");
        Permissions.Register(Nodes.Kit, PermissionDefault.True, "Claim a kit (/kit)");
        Permissions.Register(Nodes.ShowKit, PermissionDefault.OpLevel(2), "List the items of a kit (/showkit)");
        Permissions.Register(Nodes.KitExemptDelay, PermissionDefault.OpLevel(2), "Claim kits without the delay");
        foreach (string kit in Kits.Names)
        {
            Permissions.Register(Nodes.KitPrefix + kit, PermissionDefault.True, $"Claim the kit '{kit}'");
        }
        Permissions.Register(Nodes.Nick, PermissionDefault.True, "Set your nick (/nick)");
        Permissions.Register(Nodes.NickOthers, PermissionDefault.OpLevel(2), "Set the nick of another player (/nick)");
        Permissions.Register(Nodes.RealName, PermissionDefault.True, "Find the player behind a nick (/realname)");
        Permissions.Register(Nodes.Msg, PermissionDefault.True, "Send and reply to private messages (/emsg, /r)");
        Permissions.Register(Nodes.Balance, PermissionDefault.True, "See your balance (/balance)");
        Permissions.Register(Nodes.BalanceOthers, PermissionDefault.OpLevel(2), "See the balance of another player (/balance)");
        Permissions.Register(Nodes.BalanceTop, PermissionDefault.True, "See the richest players (/balancetop)");
        Permissions.Register(Nodes.Pay, PermissionDefault.True, "Pay another player (/pay)");
        Permissions.Register(Nodes.EcoPrefix + "give", PermissionDefault.OpLevel(3), "Give money (/eco give)");
        Permissions.Register(Nodes.EcoPrefix + "take", PermissionDefault.OpLevel(3), "Take money (/eco take)");
        Permissions.Register(Nodes.EcoPrefix + "set", PermissionDefault.OpLevel(3), "Set a balance (/eco set)");
        Permissions.Register(Nodes.EcoPrefix + "reset", PermissionDefault.OpLevel(3), "Reset a balance to the starting balance (/eco reset)");
        Permissions.Register(Nodes.Reload, PermissionDefault.OpLevel(3), "Reload the configuration (/essentials reload)");
    }

    private void RegisterEvents()
    {
        Events.On((ref PlayerJoinEvent e) => OnJoin(e.Player, e.Uuid, e.Username.ToString()));
        Events.On((ref PlayerQuitEvent e) => OnQuit(e.Player));
        Events.On((ref PlayerDeathEvent e) => RememberBack(e.Player));
        Events.On((ref PlayerTeleportEvent e) => OnTeleported(e.Player, e.FromWorld.Key, e.From.X, e.From.Y, e.From.Z));
        Events.On((ref PlayerMoveEvent e) => OnMoved(e.Player));
        Events.On((ref ServerStopEvent e) => Data.FlushNow());
    }

    private void OnJoin(Player player, Uuid uuid, string name)
    {
        string id = KeyOf(uuid);
        if (!Accounts.HasAccount(id))
        {
            Accounts.Create(id, name, StartingBalance);
            Data.MarkDirty(DataStore.FileEconomy);
        }
        else if (Accounts.Rename(id, name))
        {
            Data.MarkDirty(DataStore.FileEconomy);
        }

        if (Nicks.TryGetValue(id, out string? nick)) player.DisplayName = Component.Text(nick);
        if (Options.Spawn.OnJoin)
        {
            Scheduler.RunLater(1, () =>
            {
                if (player.IsOnline) Teleport(player, SpawnTarget(), out _);
            });
        }
    }

    private void OnQuit(Player player)
    {
        string id = KeyOf(player);
        Warmups.Cancel(id);
        Tpa.Forget(id);
        Replies.Forget(id);
        Backs.Remove(id);
    }

    private void RememberBack(Player player)
    {
        Backs[KeyOf(player)] = Here(player);
    }

    private void OnTeleported(Player player, string fromWorld, double x, double y, double z)
    {
        Backs[KeyOf(player)] = new StoredLocation(fromWorld, x, y, z, 0f, 0f);
    }

    private void OnMoved(Player player)
    {
        if (Warmups.Count == 0) return;
        if (Warmups.Moved(KeyOf(player), PosOf(player))) player.SendMessage(Component.Text("Teleport cancelled: you moved."));
    }
}
