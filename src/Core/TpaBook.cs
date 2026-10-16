namespace Mc.Scripting.Essentials.Core;

/// <summary>Which way a teleport request goes: <see cref="GoTo"/> sends the requester to the target (<c>/tpa</c>), <see cref="Here"/> brings the
/// target to the requester (<c>/tpahere</c>).</summary>
public enum TpaKind
{
    /// <summary>The requester moves to the target.</summary>
    GoTo,
    /// <summary>The target moves to the requester.</summary>
    Here,
}

/// <summary>A pending request.</summary>
/// <param name="From">The requester's id.</param>
/// <param name="To">The target's id.</param>
/// <param name="Kind">The direction.</param>
/// <param name="CreatedTick">The tick it was sent on.</param>
public sealed record TpaRequest(string From, string To, TpaKind Kind, long CreatedTick);

/// <summary>Teleport requests between players. A target can hold several incoming requests; a sender holds at most one outgoing request (a new one
/// replaces the old). Requests expire after <paramref name="expiryTicks"/>. Times are ticks passed in by the caller, so the state machine is
/// testable without a clock.</summary>
public sealed class TpaBook
{
    private readonly long _expiryTicks;
    private readonly List<TpaRequest> _requests = new();

    /// <param name="expiryTicks">How long a request waits for an answer.</param>
    public TpaBook(long expiryTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expiryTicks);
        _expiryTicks = expiryTicks;
    }

    /// <summary>Sends a request. A player cannot request themselves.</summary>
    public StoreResult Request(string from, string to, TpaKind kind, long now)
    {
        if (from == to) return StoreResult.Invalid;
        Expire(now);
        _requests.RemoveAll(r => r.From == from);
        _requests.Add(new TpaRequest(from, to, kind, now));
        return StoreResult.Ok;
    }

    /// <summary>Accepts a request to <paramref name="to"/>: the named sender's, or the most recent one when <paramref name="from"/> is null.
    /// The request is removed.</summary>
    public TpaRequest? Accept(string to, string? from, long now) => Take(to, from, now);

    /// <summary>Denies a request the same way <see cref="Accept"/> picks one.</summary>
    public TpaRequest? Deny(string to, string? from, long now) => Take(to, from, now);

    /// <summary>Withdraws the sender's outgoing request. Returns false when there is none.</summary>
    public bool CancelOutgoing(string from) => _requests.RemoveAll(r => r.From == from) > 0;

    /// <summary>The live incoming requests to a player, most recent last.</summary>
    public IReadOnlyList<TpaRequest> Incoming(string to, long now)
    {
        Expire(now);
        return _requests.Where(r => r.To == to).ToList();
    }

    /// <summary>Drops every request a player sent or received (on quit).</summary>
    public void Forget(string player) => _requests.RemoveAll(r => r.From == player || r.To == player);

    private void Expire(long now) => _requests.RemoveAll(r => now - r.CreatedTick > _expiryTicks);

    private TpaRequest? Take(string to, string? from, long now)
    {
        Expire(now);
        int index = -1;
        for (int i = 0; i < _requests.Count; i++)
        {
            if (_requests[i].To == to && (from is null || _requests[i].From == from)) index = i;
        }
        if (index < 0) return null;
        var request = _requests[index];
        _requests.RemoveAt(index);
        return request;
    }
}
