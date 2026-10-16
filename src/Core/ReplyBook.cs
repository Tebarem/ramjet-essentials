namespace Mc.Scripting.Essentials.Core;

/// <summary>Who each player last talked to privately, for <c>/r</c>. Both ends of a conversation point at each other. Pure.</summary>
public sealed class ReplyBook
{
    private readonly Dictionary<string, string> _partner = new();

    /// <summary>Records a conversation between two players: each is the other's reply target.</summary>
    public void Record(string a, string b)
    {
        _partner[a] = b;
        _partner[b] = a;
    }

    /// <summary>The player's last correspondent, or null.</summary>
    public string? PartnerOf(string player) => _partner.TryGetValue(player, out var partner) ? partner : null;

    /// <summary>Forgets a player (on quit).</summary>
    public void Forget(string player) => _partner.Remove(player);
}
