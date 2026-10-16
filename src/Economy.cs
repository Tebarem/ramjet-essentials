using Mc.Scripting.Essentials.Core;
using Mc.Scripting.Services;

namespace Mc.Scripting.Essentials;

/// <summary>The economy Essentials provides: <see cref="IEconomy"/> over the ledger. Registered at the lowest priority, so an economy plugin with a
/// higher priority takes over the service and every consumer. Thread: the tick thread.</summary>
internal sealed class EssentialsEconomy(EssentialsPlugin owner) : IEconomy
{
    /// <inheritdoc/>
    public event Action<Uuid?>? Changed;

    /// <inheritdoc/>
    public Currency Currency => owner.CurrencyInfo;

    /// <inheritdoc/>
    public bool HasAccount(Uuid player) => owner.Accounts.HasAccount(EssentialsPlugin.KeyOf(player));

    /// <inheritdoc/>
    public long Balance(Uuid player) => owner.Accounts.Balance(EssentialsPlugin.KeyOf(player));

    /// <inheritdoc/>
    public bool Has(Uuid player, long minor) => owner.Accounts.Has(EssentialsPlugin.KeyOf(player), minor);

    /// <inheritdoc/>
    public EconomyResult Deposit(Uuid player, long minor, string reason)
    {
        var result = owner.Accounts.Deposit(EssentialsPlugin.KeyOf(player), minor);
        if (result == EconomyResult.Ok) Settled(player, "deposit", minor, reason);
        return result;
    }

    /// <inheritdoc/>
    public EconomyResult Withdraw(Uuid player, long minor, string reason)
    {
        var result = owner.Accounts.Withdraw(EssentialsPlugin.KeyOf(player), minor);
        if (result == EconomyResult.Ok) Settled(player, "withdraw", minor, reason);
        return result;
    }

    /// <inheritdoc/>
    public EconomyResult Transfer(Uuid from, Uuid to, long minor, string reason)
    {
        var result = owner.Accounts.Transfer(EssentialsPlugin.KeyOf(from), EssentialsPlugin.KeyOf(to), minor);
        if (result != EconomyResult.Ok) return result;
        Settled(from, "transfer out", minor, reason);
        Settled(to, "transfer in", minor, reason);
        return result;
    }

    /// <summary>Marks the file for saving, tells the listeners and logs when transactions are logged.</summary>
    internal void Settled(Uuid player, string verb, long minor, string reason)
    {
        owner.Data.MarkDirty(DataStore.FileEconomy);
        Changed?.Invoke(player);
        if (owner.Options.Economy.LogTransactions) owner.Log.Info($"{verb} {minor} for {EssentialsPlugin.KeyOf(player)}: {reason}");
    }
}
