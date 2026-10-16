namespace Mc.Scripting.Essentials.Core;

/// <summary>Teleport warm-ups: a player starts one, waits for the delay, and is teleported unless the warm-up is cancelled first. Movement beyond the
/// tolerance cancels it. Keyed by player id; each entry carries a payload the caller uses when it completes. A due time is checked at completion, so a
/// stale timer for an earlier warm-up finds nothing to do.</summary>
public sealed class Warmups<T>
{
    private readonly Dictionary<string, Pending> _pending = new();
    private readonly double _toleranceSquared;

    private sealed record Pending(long DueTick, Pos Start, T Payload);

    /// <param name="tolerance">How far the player may move (in blocks) before the warm-up is cancelled.</param>
    public Warmups(double tolerance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);
        _toleranceSquared = tolerance * tolerance;
    }

    /// <summary>How many warm-ups are running.</summary>
    public int Count => _pending.Count;

    /// <summary>Whether the player has a warm-up running.</summary>
    public bool Has(string player) => _pending.ContainsKey(player);

    /// <summary>Starts a warm-up. Returns false when the player already has one.</summary>
    public bool Begin(string player, long now, long delayTicks, Pos start, T payload)
    {
        if (_pending.ContainsKey(player)) return false;
        _pending[player] = new Pending(now + delayTicks, start, payload);
        return true;
    }

    /// <summary>Cancels a warm-up. Returns false when there is none.</summary>
    public bool Cancel(string player) => _pending.Remove(player);

    /// <summary>Cancels the warm-up when the player is now further than the tolerance from where it started.</summary>
    /// <returns>True when a warm-up was cancelled by this move.</returns>
    public bool Moved(string player, Pos now)
    {
        if (!_pending.TryGetValue(player, out var pending)) return false;
        double dx = now.X - pending.Start.X, dy = now.Y - pending.Start.Y, dz = now.Z - pending.Start.Z;
        if (dx * dx + dy * dy + dz * dz <= _toleranceSquared) return false;
        _pending.Remove(player);
        return true;
    }

    /// <summary>Takes the payload of a warm-up that is due. The warm-up is removed.</summary>
    public bool TryComplete(string player, long now, out T payload)
    {
        payload = default!;
        if (!_pending.TryGetValue(player, out var pending) || pending.DueTick > now) return false;
        _pending.Remove(player);
        payload = pending.Payload;
        return true;
    }
}
