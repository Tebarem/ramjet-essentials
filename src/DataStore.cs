using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Mc.Scripting.Essentials.Core;
using Mc.Scripting.Essentials.Settings;

namespace Mc.Scripting.Essentials;

/// <summary>Reads the data files once on enable and writes the changed ones, batched to at most one write per second; a stop flushes at once. Writes
/// are atomic (the plugin config layer's WriteText). A file that does not parse is reported and never written, so a damaged file is not replaced
/// by defaults.</summary>
internal sealed class DataStore
{
    internal const string FileHomes = "homes.json";
    internal const string FileWarps = "warps.json";
    internal const string FileEconomy = "economy.json";
    internal const string FilePlayers = "players.json";

    private readonly EssentialsPlugin _owner;
    private readonly HashSet<string> _dirty = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unreadable = new(StringComparer.Ordinal);
    private bool _flushScheduled;

    internal DataStore(EssentialsPlugin owner) => _owner = owner;

    /// <summary>Loads every data file that exists. A missing file starts empty.</summary>
    internal void Load()
    {
        if (Read(FileHomes, EssentialsJson.Default.HomesDoc) is { } homes) _owner.Homes.Load(homes.Players);

        if (Read(FileWarps, EssentialsJson.Default.WarpsDoc) is { } warps)
        {
            var list = warps.Warps.Where(kv => kv.Value.Location is not null)
                .Select(kv => new Warp(kv.Key, kv.Value.Location, kv.Value.Owner)).ToList();
            _owner.Warps.Load(list, warps.Spawn);
        }

        if (Read(FileEconomy, EssentialsJson.Default.EconomyDoc) is { } economy) _owner.Accounts.Load(economy.Accounts);

        if (Read(FilePlayers, EssentialsJson.Default.PlayersDoc) is { } players)
        {
            _owner.Nicks.Clear();
            foreach (var (id, nick) in players.Nicks)
            {
                if (Names.IsValidNick(nick)) _owner.Nicks[id] = nick;
            }
            _owner.KitCooldowns.Load(players.KitCooldowns);
        }
    }

    /// <summary>Marks a file as changed. The write happens within a second.</summary>
    internal void MarkDirty(string file)
    {
        _dirty.Add(file);
        if (_flushScheduled) return;
        _flushScheduled = true;
        _owner.Scheduler.RunLater(EssentialsPlugin.TicksPerSecond, () =>
        {
            _flushScheduled = false;
            WriteDirty();
        });
    }

    /// <summary>Writes every changed file now (server stop, unload).</summary>
    internal void FlushNow() => WriteDirty();

    private T? Read<T>(string file, JsonTypeInfo<T> info) where T : class
    {
        string? text = _owner.Config.ReadText(file);
        if (text is null) return null;
        try
        {
            return JsonSerializer.Deserialize(text, info);
        }
        catch (JsonException e)
        {
            _unreadable.Add(file);
            _owner.Log.Error($"{file} does not parse and will not be written until it is fixed: {e.Message}");
            return null;
        }
    }

    private void WriteDirty()
    {
        foreach (string file in _dirty)
        {
            if (_unreadable.Contains(file))
            {
                _owner.Log.Error($"{file} was not written: it did not parse on load");
                continue;
            }
            _owner.Config.WriteText(file, Serialize(file));
        }
        _dirty.Clear();
    }

    private string Serialize(string file) => file switch
    {
        FileHomes => JsonSerializer.Serialize(new HomesDoc { Players = _owner.Homes.Export() }, EssentialsJson.Default.HomesDoc),
        FileWarps => JsonSerializer.Serialize(new WarpsDoc
        {
            Warps = _owner.Warps.Export().ToDictionary(w => w.Name, w => new WarpEntry { Location = w.Location, Owner = w.Owner }),
            Spawn = _owner.Warps.Spawn,
        }, EssentialsJson.Default.WarpsDoc),
        FileEconomy => JsonSerializer.Serialize(new EconomyDoc { Accounts = _owner.Accounts.Export() }, EssentialsJson.Default.EconomyDoc),
        FilePlayers => JsonSerializer.Serialize(new PlayersDoc
        {
            Nicks = new Dictionary<string, string>(_owner.Nicks),
            KitCooldowns = _owner.KitCooldowns.Export(),
        }, EssentialsJson.Default.PlayersDoc),
        _ => throw new InvalidOperationException($"no writer for {file}"),
    };
}
