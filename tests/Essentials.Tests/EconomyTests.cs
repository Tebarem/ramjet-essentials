using Mc.Scripting.Essentials.Core;
using Mc.Scripting.Services;

namespace Mc.Scripting.Essentials.Tests;

/// <summary>Money text and the ledger.</summary>
internal static class EconomyTests
{
    internal static void Register(Action<string, Action> add)
    {
        add("money text: whole and fraction forms, no floating point", () =>
        {
            Check.True(MoneyText.TryParse("12", 2, out var whole) && whole == 1200, "whole");
            Check.True(MoneyText.TryParse("12.5", 2, out var half) && half == 1250, "one fraction digit");
            Check.True(MoneyText.TryParse("0.05", 2, out var cents) && cents == 5, "leading zero");
            Check.True(MoneyText.TryParse("7", 0, out var plain) && plain == 7, "zero-digit currency");
        });

        add("money text: refuses signs, extra digits, dangling points and overflow", () =>
        {
            Check.True(!MoneyText.TryParse("1.234", 2, out _), "too many fraction digits");
            Check.True(!MoneyText.TryParse("-1", 2, out _), "sign");
            Check.True(!MoneyText.TryParse("1.", 2, out _), "dangling point");
            Check.True(!MoneyText.TryParse(".5", 2, out _), "no whole part");
            Check.True(!MoneyText.TryParse("7.5", 0, out _), "point in a zero-digit currency");
            Check.True(!MoneyText.TryParse("9223372036854775807", 2, out _), "overflow");
            Check.True(!MoneyText.TryParse("1e3", 2, out _), "exponent");
            Check.True(!MoneyText.TryParse(null, 2, out _), "null");
        });

        add("ledger: deposit and withdraw with their refusals", () =>
        {
            var ledger = new Ledger(1000);
            Check.True(ledger.Create("a", "Ann", 100), "create");
            Check.True(!ledger.Create("a", "Ann", 5), "second create refused");
            Check.Equal(EconomyResult.Ok, ledger.Deposit("a", 50), "deposit");
            Check.Equal(150L, ledger.Balance("a"), "balance after deposit");
            Check.Equal(EconomyResult.NotEnough, ledger.Withdraw("a", 151), "no overdraft");
            Check.Equal(EconomyResult.Refused, ledger.Withdraw("a", 0), "zero refused");
            Check.Equal(EconomyResult.Refused, ledger.Deposit("a", -5), "negative refused");
            Check.Equal(EconomyResult.Unknown, ledger.Deposit("zz", 5), "unknown account");
            Check.Equal(EconomyResult.OverMax, ledger.Deposit("a", 851), "over the maximum");
            Check.Equal(EconomyResult.Ok, ledger.Deposit("a", 850), "exactly the maximum is fine");
            Check.Equal(1000L, ledger.Balance("a"), "maximum reached");
        });

        add("ledger: a transfer changes both accounts or neither", () =>
        {
            var ledger = new Ledger(1000);
            ledger.Create("a", "A", 100);
            ledger.Create("b", "B", 0);
            Check.Equal(EconomyResult.NotEnough, ledger.Transfer("a", "b", 101), "payer short");
            Check.Equal(EconomyResult.Unknown, ledger.Transfer("a", "zz", 10), "unknown payee");
            Check.Equal(100L, ledger.Balance("a"), "payer unchanged after refusals");
            Check.Equal(EconomyResult.Ok, ledger.Transfer("a", "b", 40), "transfer");
            Check.Equal(60L, ledger.Balance("a"), "payer after");
            Check.Equal(40L, ledger.Balance("b"), "payee after");
            Check.Equal(EconomyResult.Refused, ledger.Transfer("a", "a", 1), "paying yourself");
        });

        add("ledger: top is richest first, ties by id; set refuses negatives", () =>
        {
            var ledger = new Ledger(1000);
            ledger.Create("b", "Bo", 50);
            ledger.Create("a", "Al", 50);
            ledger.Create("c", "Cy", 90);
            var top = ledger.Top(10, 0);
            Check.Equal("c", top[0].Id, "richest first");
            Check.Equal("a", top[1].Id, "tie broken by id");
            Check.Equal("b", top[2].Id, "last tie");
            Check.Equal(1, ledger.Top(1, 1).Count, "offset and count");
            Check.Equal(EconomyResult.Refused, ledger.Set("a", -1), "negative set refused");
            Check.Equal(EconomyResult.Ok, ledger.Set("a", 7), "set");
            Check.Equal(7L, ledger.Balance("a"), "set value");
        });

        add("ledger: export and load clamp to the maximum", () =>
        {
            var ledger = new Ledger(100);
            ledger.Create("a", "Al", 10);
            var copy = new Ledger(100);
            copy.Load(new Dictionary<string, Ledger.Account> { ["b"] = new("Bo", 500) });
            Check.Equal(100L, copy.Balance("b"), "clamped on load");
            Check.True(!copy.HasAccount("a"), "load replaces the accounts");
        });

        add("currency: formats with its symbol and digits", () =>
        {
            var dollars = new Currency("dollar", "$", 2, "dollars");
            Check.Equal("$12.05", dollars.Format(1205), "cents kept");
            Check.Equal("-$0.50", dollars.Format(-50), "negative");
            Check.Equal("$7", new Currency("coin", "$", 0, "coins").Format(7), "zero digits");
        });
    }
}
