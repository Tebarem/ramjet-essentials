using Mc.Scripting.Essentials.Core;
using Mc.Scripting.Services;

namespace Mc.Scripting.Essentials.Commands;

/// <summary>Kits, nicks, private messages and the plugin's own command.</summary>
internal static class PlayerCommands
{
    internal static void Register(EssentialsPlugin p)
    {
        p.Commands.Register(Command.Literal("kit").Permission(Nodes.Kit)
            .Executes(ctx => ListKits(p, ctx))
            .Then(Command.Argument("name", Arg.Word()).Suggests(_ => p.Kits.Names)
                .Executes(ctx => Claim(p, ctx, ctx.GetString("name")))),
            aliases: ["kits"], description: "Claim a kit (Essentials)");

        p.Commands.Register(Command.Literal("showkit").Permission(Nodes.ShowKit)
            .Then(Command.Argument("name", Arg.Word()).Suggests(_ => p.Kits.Names)
                .Executes(ctx => ShowKit(p, ctx, ctx.GetString("name")))),
            description: "List the items of a kit (Essentials)");

        p.Commands.Register(Command.Literal("nick").Permission(Nodes.Nick)
            .Then(Command.Argument("value", Arg.Word())
                .Executes(ctx => SetNick(p, ctx, ctx.GetString("value"), null))
                .Then(Command.Argument("player", Arg.Word()).Permission(Nodes.NickOthers).Suggests(_ => CommandTrees.OnlineNames())
                    .Executes(ctx => SetNick(p, ctx, ctx.GetString("value"), Server.GetPlayer(ctx.GetString("player")))))),
            description: "Set your nick, or 'off' to remove it (Essentials)");

        p.Commands.Register(Command.Literal("realname").Permission(Nodes.RealName)
            .Then(Command.Argument("nick", Arg.Word()).Executes(ctx => RealName(p, ctx, ctx.GetString("nick")))),
            description: "Find the player behind a nick (Essentials)");

        p.Commands.Register(Command.Literal("emsg").Permission(Nodes.Msg)
            .Then(Command.Argument("player", Arg.Word()).Suggests(_ => CommandTrees.OnlineNames())
                .Then(Command.Argument("message", Arg.GreedyString())
                    .Executes(ctx => SendPrivately(p, ctx, ctx.GetString("player"), ctx.GetString("message"))))),
            description: "Send a private message (Essentials; /msg is vanilla)");

        p.Commands.Register(Command.Literal("r").Permission(Nodes.Msg)
            .Then(Command.Argument("message", Arg.GreedyString())
                .Executes(ctx => ReplyPrivately(p, ctx, ctx.GetString("message")))),
            description: "Reply to the last private message (Essentials)");

        p.Commands.Register(Command.Literal("essentials")
            .Executes(ctx => Info(ctx))
            .Then(Command.Literal("reload").Permission(Nodes.Reload).Executes(ctx => Reload(p, ctx))),
            description: "Plugin information and reload (Essentials)");
    }

    private static void ListKits(EssentialsPlugin p, CommandContext ctx)
    {
        var names = p.Kits.Names;
        ctx.Reply(names.Count == 0 ? "There are no kits." : "Kits: " + string.Join(", ", names));
    }

    private static void Claim(EssentialsPlugin p, CommandContext ctx, string name)
    {
        Player player = ctx.RequirePlayer();
        if (!p.Kits.TryGet(name, out Kit kit))
        {
            ctx.Reply($"There is no kit named '{name}'.");
            return;
        }
        string kitKey = Names.Key(kit.Name);
        if (!player.HasPermission(Nodes.KitPrefix + kitKey))
        {
            ctx.Reply($"You may not claim the kit '{kit.Name}'.");
            return;
        }

        string key = $"kit:{kitKey}:{EssentialsPlugin.KeyOf(player)}";
        long now = EssentialsPlugin.NowSeconds;
        if (!player.HasPermission(Nodes.KitExemptDelay) && !p.KitCooldowns.IsReady(key, now, kit.DelaySeconds))
        {
            long wait = p.KitCooldowns.Remaining(key, now, kit.DelaySeconds);
            ctx.Reply($"You can claim '{kit.Name}' again in {CommandTrees.Duration(wait)}.");
            return;
        }

        int total = kit.Items.Sum(item => item.Count), refused = 0;
        foreach (var item in kit.Items)
        {
            try
            {
                refused += player.Inventory.AddItem(new ItemStack(item.Type, item.Count));
            }
            catch (ArgumentException)
            {
                refused += item.Count;  // the engine refused the id: the stack is skipped, the rest of the kit is given
            }
        }
        if (refused >= total)
        {
            ctx.Reply($"Nothing from '{kit.Name}' fit in your inventory or the kit's items are invalid. Nothing was taken from the cooldown.");
            return;
        }

        p.KitCooldowns.Stamp(key, now);
        p.Data.MarkDirty(DataStore.FilePlayers);
        ctx.Reply(refused > 0
            ? $"Kit '{kit.Name}' claimed. {refused} items did not fit or are invalid and were not given."
            : $"Kit '{kit.Name}' claimed.");
    }

    private static void ShowKit(EssentialsPlugin p, CommandContext ctx, string name)
    {
        if (!p.Kits.TryGet(name, out Kit kit))
        {
            ctx.Reply($"There is no kit named '{name}'.");
            return;
        }
        string items = kit.Items.Count == 0 ? "no items" : string.Join(", ", kit.Items.Select(i => $"{i.Count}x {i.Type}"));
        ctx.Reply($"Kit '{kit.Name}' (delay {CommandTrees.Duration(kit.DelaySeconds)}): {items}");
    }

    private static void SetNick(EssentialsPlugin p, CommandContext ctx, string value, Player? other)
    {
        Player me = ctx.RequirePlayer();
        Player subject = other ?? me;
        string id = EssentialsPlugin.KeyOf(subject);
        if (value.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            p.Nicks.Remove(id);
            subject.DisplayName = null;
        }
        else if (!Names.IsValidNick(value))
        {
            ctx.Reply($"Nicks use letters, digits, '_' and '-' (up to {Names.MaxNickLength} characters).");
            return;
        }
        else
        {
            p.Nicks[id] = value;
            subject.DisplayName = Component.Text(value);
        }
        p.Data.MarkDirty(DataStore.FilePlayers);
        ctx.Reply(subject == me ? "Your nick is updated." : $"The nick of {subject.Name} is updated.");
    }

    private static void RealName(EssentialsPlugin p, CommandContext ctx, string nick)
    {
        foreach (var (id, value) in p.Nicks)
        {
            if (!value.Equals(nick, StringComparison.OrdinalIgnoreCase)) continue;
            string name = p.Accounts.NameOf(id) ?? "an unknown player";
            ctx.Reply($"{value} is {name}.");
            return;
        }
        ctx.Reply($"No player uses the nick '{nick}'.");
    }

    private static void SendPrivately(EssentialsPlugin p, CommandContext ctx, string name, string text)
    {
        Player me = ctx.RequirePlayer();
        Deliver(p, ctx, me, Server.GetPlayer(name), text);
    }

    private static void ReplyPrivately(EssentialsPlugin p, CommandContext ctx, string text)
    {
        Player me = ctx.RequirePlayer();
        string? partner = p.Replies.PartnerOf(EssentialsPlugin.KeyOf(me));
        if (partner is null)
        {
            ctx.Reply("You have no one to reply to.");
            return;
        }
        Deliver(p, ctx, me, CommandTrees.Online(partner), text);
    }

    /// <summary>Hands the message to the chat plugin's service when one is registered. Without it, both ends get a plain line.</summary>
    private static void Deliver(EssentialsPlugin p, CommandContext ctx, Player me, Player? to, string text)
    {
        if (to is not { } recipient)
        {
            ctx.Reply("That player is not online.");
            return;
        }
        if (recipient == me)
        {
            ctx.Reply("You cannot message yourself.");
            return;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            ctx.Reply("Write a message after the name.");
            return;
        }
        string message = text.Trim();

        if (p.Services.TryGet<IPrivateMessages>() is { } chat)
        {
            switch (chat.Send(me, recipient, message))
            {
                case PrivateMessageResult.Ok:
                    break;
                case PrivateMessageResult.Offline:
                    ctx.Reply("That player is not online.");
                    return;
                case PrivateMessageResult.Ignored:
                    ctx.Reply($"{recipient.Name} is not accepting messages from you.");
                    return;
                case PrivateMessageResult.Muted:
                    ctx.Reply("You are muted.");
                    return;
                default:
                    ctx.Reply("The message was not sent.");
                    return;
            }
        }
        else
        {
            recipient.SendMessage(Component.Text($"[{me.Name} -> you] {message}"));
            me.SendMessage(Component.Text($"[you -> {recipient.Name}] {message}"));
        }
        p.Replies.Record(EssentialsPlugin.KeyOf(me), EssentialsPlugin.KeyOf(recipient));
    }

    private static void Info(CommandContext ctx) =>
        ctx.Reply("Essentials for Ramjet: homes, warps, teleports, kits, nicks, private messages and the economy. Settings: config.json in the plugin's folder.");

    private static void Reload(EssentialsPlugin p, CommandContext ctx) =>
        ctx.Reply(p.ReloadConfig()
            ? "Configuration reloaded. The currency and the balance limits apply after a restart."
            : "The configuration did not reload: the file does not parse. The previous settings stay.");
}
