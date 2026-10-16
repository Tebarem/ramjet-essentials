using Mc.Scripting.Essentials.Core;
using Mc.Scripting.Services;

namespace Mc.Scripting.Essentials.Commands;

/// <summary>Balances, the richest list, payments and the administrator money commands.</summary>
internal static class EconomyCommands
{
    private const int TopPageSize = 10;

    internal static void Register(EssentialsPlugin p)
    {
        p.Commands.Register(Command.Literal("balance").Permission(Nodes.Balance)
            .Executes(ctx => ShowBalance(p, ctx, null))
            .Then(Command.Argument("player", Arg.Word()).Permission(Nodes.BalanceOthers).Suggests(_ => CommandTrees.OnlineNames())
                .Executes(ctx => ShowBalance(p, ctx, ctx.GetString("player")))),
            aliases: ["bal", "money"], description: "Show a balance (Essentials)");

        p.Commands.Register(Command.Literal("balancetop").Permission(Nodes.BalanceTop)
            .Executes(ctx => ShowTop(p, ctx, 1))
            .Then(Command.Argument("page", Arg.Int(1)).Executes(ctx => ShowTop(p, ctx, ctx.GetInt("page")))),
            aliases: ["baltop"], description: "Show the richest players (Essentials)");

        p.Commands.Register(Command.Literal("pay").Permission(Nodes.Pay)
            .Then(Command.Argument("player", Arg.Word()).Suggests(_ => CommandTrees.OnlineNames())
                .Then(Command.Argument("amount", Arg.Word())
                    .Executes(ctx => Pay(p, ctx, ctx.GetString("player"), ctx.GetString("amount"))))),
            description: "Pay another player (Essentials)");

        p.Commands.Register(
            Command.Literal("eco")
                .Then(Adjustment(p, "give"))
                .Then(Adjustment(p, "take"))
                .Then(Adjustment(p, "set"))
                .Then(Reset(p)),
            aliases: ["economy"], description: "Give, take, set or reset money (Essentials)");
    }

    /// <summary>Uuid of an online or known offline player, by name.</summary>
    private static Uuid? ResolveUuid(string name)
    {
        if (Server.GetPlayer(name) is { } online) return online.Uuid;
        return OfflinePlayers.Find(name);
    }

    private static void ShowBalance(EssentialsPlugin p, CommandContext ctx, string? name)
    {
        Uuid? id = name is null ? ctx.RequirePlayer().Uuid : ResolveUuid(name);
        if (id is not { } who)
        {
            ctx.Reply($"There is no player named {name}.");
            return;
        }
        string label = name is null ? "Your balance" : $"{name}'s balance";
        ctx.Reply($"{label}: {p.CurrencyInfo.Format(p.Accounts.Balance(EssentialsPlugin.KeyOf(who)))}");
    }

    private static void ShowTop(EssentialsPlugin p, CommandContext ctx, int page)
    {
        if (page < 1)
        {
            ctx.Reply("Pages start at 1.");
            return;
        }
        var rows = p.Accounts.Top(TopPageSize, (page - 1) * TopPageSize);
        if (rows.Count == 0)
        {
            ctx.Reply("There are no balances on this page.");
            return;
        }
        var lines = new List<string> { $"Richest players, page {page}:" };
        int rank = (page - 1) * TopPageSize;
        foreach (var (_, name, balance) in rows)
        {
            lines.Add($"{++rank}. {name}: {p.CurrencyInfo.Format(balance)}");
        }
        ctx.Reply(string.Join("\n", lines));
    }

    private static void Pay(EssentialsPlugin p, CommandContext ctx, string name, string amount)
    {
        Player me = ctx.RequirePlayer();
        if (!Core.MoneyText.TryParse(amount, p.CurrencyInfo.FractionDigits, out long minor) || minor <= 0)
        {
            ctx.Reply("Enter an amount such as 5 or 5.25.");
            return;
        }
        if (ResolveUuid(name) is not { } to)
        {
            ctx.Reply($"There is no player named {name}.");
            return;
        }
        if (to == me.Uuid)
        {
            ctx.Reply("You cannot pay yourself.");
            return;
        }
        if (p.Services.TryGet<IEconomy>() is not { } economy)
        {
            ctx.Reply("No economy is available.");
            return;
        }

        string recipient = Server.GetPlayer(name)?.Name ?? OfflinePlayers.Get(to)?.Name ?? name;
        switch (economy.Transfer(me.Uuid, to, minor, "pay"))
        {
            case EconomyResult.Ok:
                ctx.Reply($"Paid {economy.Currency.Format(minor)} to {recipient}. Your balance: {economy.Currency.Format(economy.Balance(me.Uuid))}.");
                if (CommandTrees.Online(EssentialsPlugin.KeyOf(to)) is { } online)
                {
                    online.SendMessage(Component.Text($"{me.Name} paid you {economy.Currency.Format(minor)}."));
                }
                break;
            case EconomyResult.Unknown:
                ctx.Reply($"{recipient} has no account yet.");
                break;
            case EconomyResult.NotEnough:
                ctx.Reply("You do not have enough money.");
                break;
            case EconomyResult.OverMax:
                ctx.Reply($"{recipient} cannot hold that much more.");
                break;
            default:
                ctx.Reply("The payment was refused.");
                break;
        }
    }

    private static CommandNode Adjustment(EssentialsPlugin p, string action) =>
        Command.Literal(action).Permission(Nodes.EcoPrefix + action)
            .Then(Command.Argument("player", Arg.Word()).Suggests(_ => CommandTrees.OnlineNames())
                .Then(Command.Argument("amount", Arg.Word())
                    .Executes(ctx => Adjust(p, ctx, action, ctx.GetString("player"), ctx.GetString("amount")))));

    private static CommandNode Reset(EssentialsPlugin p) =>
        Command.Literal("reset").Permission(Nodes.EcoPrefix + "reset")
            .Then(Command.Argument("player", Arg.Word()).Suggests(_ => CommandTrees.OnlineNames())
                .Executes(ctx => Adjust(p, ctx, "reset", ctx.GetString("player"), null)));

    /// <summary>give and take go through the economy service (its listeners hear them); set and reset are the administrator's own writes to the
    /// ledger.</summary>
    private static void Adjust(EssentialsPlugin p, CommandContext ctx, string action, string name, string? amount)
    {
        if (ResolveUuid(name) is not { } who)
        {
            ctx.Reply($"There is no player named {name}.");
            return;
        }
        if (action != "reset" && !Core.MoneyText.TryParse(amount, p.CurrencyInfo.FractionDigits, out _))
        {
            ctx.Reply("Enter an amount such as 5 or 5.25.");
            return;
        }
        Core.MoneyText.TryParse(amount, p.CurrencyInfo.FractionDigits, out long minor);

        string key = EssentialsPlugin.KeyOf(who);
        var economy = p.Services.TryGet<IEconomy>();
        EconomyResult result = action switch
        {
            "give" => economy?.Deposit(who, minor, "eco give") ?? EconomyResult.Unknown,
            "take" => economy?.Withdraw(who, minor, "eco take") ?? EconomyResult.Unknown,
            "set" => p.Accounts.Set(key, minor),
            _ => p.Accounts.Set(key, p.StartingBalance),
        };
        if (result == EconomyResult.Ok && action is "set" or "reset") p.Data.MarkDirty(DataStore.FileEconomy);

        ctx.Reply(result switch
        {
            EconomyResult.Ok => $"{name}'s balance is now {p.CurrencyInfo.Format(p.Accounts.Balance(key))}.",
            EconomyResult.Unknown => $"{name} has no account yet; they need to join once.",
            EconomyResult.NotEnough => $"{name} does not have that much to take.",
            EconomyResult.OverMax => "That would go over the maximum balance.",
            _ => "The change was refused.",
        });
    }
}
