using Mc.Scripting.Services;

namespace Mc.Scripting.Essentials.Core;

/// <summary>Balances in minor units (cents for a two-digit currency). Pure arithmetic: accounts are keyed by the owner id (the uuid string) and
/// every rule (no negative balance, no balance over the maximum, both sides of a transfer or neither) is checked here, where tests can reach it.
/// Thread: the tick thread.</summary>
public sealed class Ledger
{
    private readonly Dictionary<string, Account> _accounts = new();

    /// <summary>An account: the last known name (for lists) and the balance.</summary>
    public sealed record Account(string Name, long Balance);

    /// <param name="maxBalance">The largest balance an account may hold, in minor units.</param>
    public Ledger(long maxBalance)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBalance);
        MaxBalance = maxBalance;
    }

    /// <summary>The largest balance an account may hold, in minor units.</summary>
    public long MaxBalance { get; }

    /// <summary>Whether the owner has an account.</summary>
    public bool HasAccount(string id) => _accounts.ContainsKey(id);

    /// <summary>The balance; 0 for an unknown account.</summary>
    public long Balance(string id) => _accounts.TryGetValue(id, out var account) ? account.Balance : 0;

    /// <summary>The last known name of the account, or null.</summary>
    public string? NameOf(string id) => _accounts.TryGetValue(id, out var account) ? account.Name : null;

    /// <summary>Whether the account holds at least the amount.</summary>
    public bool Has(string id, long minor) => Balance(id) >= minor;

    /// <summary>Creates an account with a starting balance. Returns false when it already exists.</summary>
    public bool Create(string id, string name, long start)
    {
        if (_accounts.ContainsKey(id)) return false;
        _accounts[id] = new Account(name, Math.Clamp(start, 0, MaxBalance));
        return true;
    }

    /// <summary>Updates the last known name. Returns true when the name changed.</summary>
    public bool Rename(string id, string name)
    {
        if (!_accounts.TryGetValue(id, out var account) || account.Name == name) return false;
        _accounts[id] = account with { Name = name };
        return true;
    }

    /// <summary>Adds money. Zero or negative amounts are refused.</summary>
    public EconomyResult Deposit(string id, long minor)
    {
        if (minor <= 0) return EconomyResult.Refused;
        if (!_accounts.TryGetValue(id, out var account)) return EconomyResult.Unknown;
        if (minor > MaxBalance - account.Balance) return EconomyResult.OverMax;
        _accounts[id] = account with { Balance = account.Balance + minor };
        return EconomyResult.Ok;
    }

    /// <summary>Takes money. Refuses to go below zero.</summary>
    public EconomyResult Withdraw(string id, long minor)
    {
        if (minor <= 0) return EconomyResult.Refused;
        if (!_accounts.TryGetValue(id, out var account)) return EconomyResult.Unknown;
        if (account.Balance < minor) return EconomyResult.NotEnough;
        _accounts[id] = account with { Balance = account.Balance - minor };
        return EconomyResult.Ok;
    }

    /// <summary>Moves money between two accounts: both change or neither does.</summary>
    public EconomyResult Transfer(string from, string to, long minor)
    {
        if (minor <= 0 || from == to) return EconomyResult.Refused;
        if (!_accounts.TryGetValue(from, out var payer) || !_accounts.TryGetValue(to, out var payee)) return EconomyResult.Unknown;
        if (payer.Balance < minor) return EconomyResult.NotEnough;
        if (minor > MaxBalance - payee.Balance) return EconomyResult.OverMax;
        _accounts[from] = payer with { Balance = payer.Balance - minor };
        _accounts[to] = payee with { Balance = payee.Balance + minor };
        return EconomyResult.Ok;
    }

    /// <summary>Sets a balance outright (administrators). Negative values and values over the maximum are refused.</summary>
    public EconomyResult Set(string id, long minor)
    {
        if (minor < 0) return EconomyResult.Refused;
        if (!_accounts.TryGetValue(id, out var account)) return EconomyResult.Unknown;
        if (minor > MaxBalance) return EconomyResult.OverMax;
        _accounts[id] = account with { Balance = minor };
        return EconomyResult.Ok;
    }

    /// <summary>The richest accounts, richest first; ties sort by id so the order is stable.</summary>
    public IReadOnlyList<(string Id, string Name, long Balance)> Top(int count, int offset) =>
        _accounts.OrderByDescending(kv => kv.Value.Balance).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Skip(offset).Take(count).Select(kv => (kv.Key, kv.Value.Name, kv.Value.Balance)).ToList();

    /// <summary>A copy of every account, for saving.</summary>
    public Dictionary<string, Account> Export() => new(_accounts);

    /// <summary>Replaces every account with the loaded ones. Balances over the maximum are clamped.</summary>
    public void Load(IReadOnlyDictionary<string, Account> loaded)
    {
        _accounts.Clear();
        foreach (var (id, account) in loaded)
        {
            _accounts[id] = account with { Balance = Math.Clamp(account.Balance, 0, MaxBalance) };
        }
    }
}
