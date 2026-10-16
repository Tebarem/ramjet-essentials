namespace Mc.Scripting.Essentials.Core;

/// <summary>The time each key last ran, in the caller's unit (ticks for teleports, epoch seconds for kits, which must survive a restart). A key
/// with no entry is ready. Pure: the caller passes the current time.</summary>
public sealed class Cooldowns
{
    private readonly Dictionary<string, long> _last = new();

    /// <summary>Whether the cooldown has passed for the key.</summary>
    public bool IsReady(string key, long now, long cooldown) => !_last.TryGetValue(key, out long last) || now - last >= cooldown;

    /// <summary>How long is left on the cooldown, 0 when it has passed.</summary>
    public long Remaining(string key, long now, long cooldown) =>
        _last.TryGetValue(key, out long last) ? Math.Max(0, cooldown - (now - last)) : 0;

    /// <summary>Records that the key ran now.</summary>
    public void Stamp(string key, long now) => _last[key] = now;

    /// <summary>Forgets a key (an administrator reset).</summary>
    public void Forget(string key) => _last.Remove(key);

    /// <summary>A copy for saving.</summary>
    public Dictionary<string, long> Export() => new(_last);

    /// <summary>Replaces every entry with the loaded ones.</summary>
    public void Load(IReadOnlyDictionary<string, long> loaded)
    {
        _last.Clear();
        foreach (var (key, last) in loaded) _last[key] = last;
    }
}
