using Mc.Scripting.Essentials.Core;

namespace Mc.Scripting.Essentials.Commands;

/// <summary>Homes, warps, spawn, teleport requests, back and the administrator teleport.</summary>
internal static class TeleportCommands
{
    internal static void Register(EssentialsPlugin p)
    {
        p.Commands.Register(Command.Literal("sethome").Permission(Nodes.SetHome)
            .Executes(ctx => SetHome(p, ctx, "home"))
            .Then(Command.Argument("name", Arg.Word()).Executes(ctx => SetHome(p, ctx, ctx.GetString("name")))),
            description: "Set a home (Essentials)");

        p.Commands.Register(Command.Literal("home").Permission(Nodes.Home)
            .Executes(ctx => GoHome(p, ctx, "home"))
            .Then(Command.Argument("name", Arg.Word()).Suggests(ctx => HomeNames(p, ctx))
                .Executes(ctx => GoHome(p, ctx, ctx.GetString("name")))),
            description: "Teleport to a home (Essentials)");

        p.Commands.Register(Command.Literal("delhome").Permission(Nodes.DelHome)
            .Then(Command.Argument("name", Arg.Word()).Suggests(ctx => HomeNames(p, ctx))
                .Executes(ctx => DelHome(p, ctx, ctx.GetString("name")))),
            description: "Delete a home (Essentials)");

        p.Commands.Register(Command.Literal("renamehome").Permission(Nodes.RenameHome)
            .Then(Command.Argument("old", Arg.Word()).Suggests(ctx => HomeNames(p, ctx))
                .Then(Command.Argument("new", Arg.Word())
                    .Executes(ctx => RenameHome(p, ctx, ctx.GetString("old"), ctx.GetString("new"))))),
            description: "Rename a home (Essentials)");

        p.Commands.Register(Command.Literal("setwarp").Permission(Nodes.SetWarp)
            .Then(Command.Argument("name", Arg.Word()).Executes(ctx => SetWarp(p, ctx, ctx.GetString("name")))),
            description: "Set a warp (Essentials)");

        p.Commands.Register(Command.Literal("warp").Permission(Nodes.Warp)
            .Executes(ctx => ListWarps(p, ctx))
            .Then(Command.Argument("name", Arg.Word()).Suggests(_ => p.Warps.Names)
                .Executes(ctx => GoWarp(p, ctx, ctx.GetString("name")))),
            description: "Teleport to a warp, or list the warps (Essentials)");

        p.Commands.Register(Command.Literal("delwarp").Permission(Nodes.DelWarp)
            .Then(Command.Argument("name", Arg.Word()).Suggests(_ => p.Warps.Names)
                .Executes(ctx => DelWarp(p, ctx, ctx.GetString("name")))),
            description: "Delete a warp (Essentials)");

        p.Commands.Register(Command.Literal("spawn").Permission(Nodes.Spawn)
            .Executes(ctx => Go(p, ctx, ctx.RequirePlayer(), p.SpawnTarget(), "the spawn")),
            description: "Teleport to the spawn (Essentials)");

        p.Commands.Register(Command.Literal("setspawn").Permission(Nodes.SetSpawn)
            .Executes(ctx => SetSpawn(p, ctx)),
            description: "Set the spawn to where you stand (Essentials)");

        p.Commands.Register(Command.Literal("tpa").Permission(Nodes.Tpa)
            .Then(Command.Argument("player", Arg.Word()).Suggests(_ => CommandTrees.OnlineNames())
                .Executes(ctx => Request(p, ctx, ctx.GetString("player"), TpaKind.GoTo))),
            description: "Ask to teleport to a player (Essentials)");

        p.Commands.Register(Command.Literal("tpahere").Permission(Nodes.TpaHere)
            .Then(Command.Argument("player", Arg.Word()).Suggests(_ => CommandTrees.OnlineNames())
                .Executes(ctx => Request(p, ctx, ctx.GetString("player"), TpaKind.Here))),
            description: "Ask a player to teleport to you (Essentials)");

        p.Commands.Register(Command.Literal("tpaccept").Permission(Nodes.TpAccept)
            .Executes(ctx => Answer(p, ctx, null, accept: true))
            .Then(Command.Argument("player", Arg.Word()).Suggests(_ => CommandTrees.OnlineNames())
                .Executes(ctx => Answer(p, ctx, ctx.GetString("player"), accept: true))),
            description: "Accept a teleport request (Essentials)");

        p.Commands.Register(Command.Literal("tpdeny").Permission(Nodes.TpDeny)
            .Executes(ctx => Answer(p, ctx, null, accept: false))
            .Then(Command.Argument("player", Arg.Word()).Suggests(_ => CommandTrees.OnlineNames())
                .Executes(ctx => Answer(p, ctx, ctx.GetString("player"), accept: false))),
            description: "Deny a teleport request (Essentials)");

        p.Commands.Register(Command.Literal("tpacancel").Permission(Nodes.TpaCancel)
            .Executes(ctx => CancelRequest(p, ctx)),
            description: "Cancel your teleport request (Essentials)");

        p.Commands.Register(Command.Literal("back").Permission(Nodes.Back)
            .Executes(ctx => GoBack(p, ctx)),
            description: "Return to where you were before the last teleport or death (Essentials)");

        p.Commands.Register(Command.Literal("etp").Permission(Nodes.TpAdmin)
            .Then(Command.Argument("player", Arg.Word()).Suggests(_ => CommandTrees.OnlineNames())
                .Executes(ctx => TeleportToPlayer(ctx, ctx.GetString("player"))))
            .Then(Command.Argument("x", Arg.Double())
                .Then(Command.Argument("y", Arg.Double())
                    .Then(Command.Argument("z", Arg.Double())
                        .Executes(ctx => TeleportToCoordinates(ctx))))),
            description: "Teleport without asking: to a player or to coordinates (Essentials; /tp is vanilla)");
    }

    private static void SetHome(EssentialsPlugin p, CommandContext ctx, string name)
    {
        Player player = ctx.RequirePlayer();
        var homes = p.Options.Homes;
        int limit = HomeBook.ResolveLimit(homes.Tiers, tier => player.HasPermission(Nodes.HomeTierPrefix + tier), homes.DefaultLimit);
        var result = p.Homes.Set(EssentialsPlugin.KeyOf(player), name, EssentialsPlugin.Here(player), limit);
        if (result == StoreResult.Ok) p.Data.MarkDirty(DataStore.FileHomes);
        ctx.Reply(result == StoreResult.Limit
            ? $"You have reached your limit of {limit} homes. Delete one with /delhome."
            : CommandTrees.StoreMessage(result, name, $"Home '{name}' set."));
    }

    private static void GoHome(EssentialsPlugin p, CommandContext ctx, string name)
    {
        Player player = ctx.RequirePlayer();
        if (!p.Homes.TryGet(EssentialsPlugin.KeyOf(player), name, out var place))
        {
            ctx.Reply($"You have no home named '{name}'. Set one with /sethome {name}.");
            return;
        }
        Go(p, ctx, player, place, $"home '{name}'");
    }

    private static void DelHome(EssentialsPlugin p, CommandContext ctx, string name)
    {
        var result = p.Homes.Remove(EssentialsPlugin.KeyOf(ctx.RequirePlayer()), name);
        if (result == StoreResult.Ok) p.Data.MarkDirty(DataStore.FileHomes);
        ctx.Reply(CommandTrees.StoreMessage(result, name, $"Home '{name}' deleted."));
    }

    private static void RenameHome(EssentialsPlugin p, CommandContext ctx, string from, string to)
    {
        var result = p.Homes.Rename(EssentialsPlugin.KeyOf(ctx.RequirePlayer()), from, to);
        if (result == StoreResult.Ok) p.Data.MarkDirty(DataStore.FileHomes);
        ctx.Reply(CommandTrees.StoreMessage(result, from, $"Home '{from}' is now '{to}'."));
    }

    private static void SetWarp(EssentialsPlugin p, CommandContext ctx, string name)
    {
        Player player = ctx.RequirePlayer();
        var result = p.Warps.Set(name, EssentialsPlugin.Here(player), EssentialsPlugin.KeyOf(player));
        if (result == StoreResult.Ok) p.Data.MarkDirty(DataStore.FileWarps);
        ctx.Reply(CommandTrees.StoreMessage(result, name, $"Warp '{name}' set."));
    }

    private static void ListWarps(EssentialsPlugin p, CommandContext ctx)
    {
        var names = p.Warps.Names;
        ctx.Reply(names.Count == 0 ? "There are no warps yet." : "Warps: " + string.Join(", ", names));
    }

    private static void GoWarp(EssentialsPlugin p, CommandContext ctx, string name)
    {
        if (!p.Warps.TryGet(name, out var warp))
        {
            ctx.Reply($"There is no warp named '{name}'.");
            return;
        }
        Go(p, ctx, ctx.RequirePlayer(), warp.Location, $"warp '{warp.Name}'");
    }

    private static void DelWarp(EssentialsPlugin p, CommandContext ctx, string name)
    {
        var result = p.Warps.Remove(name);
        if (result == StoreResult.Ok) p.Data.MarkDirty(DataStore.FileWarps);
        ctx.Reply(CommandTrees.StoreMessage(result, name, $"Warp '{name}' deleted."));
    }

    private static void SetSpawn(EssentialsPlugin p, CommandContext ctx)
    {
        p.Warps.SetSpawn(EssentialsPlugin.Here(ctx.RequirePlayer()));
        p.Data.MarkDirty(DataStore.FileWarps);
        ctx.Reply("Spawn set to where you stand.");
    }

    private static void Request(EssentialsPlugin p, CommandContext ctx, string name, TpaKind kind)
    {
        Player me = ctx.RequirePlayer();
        if (Server.GetPlayer(name) is not { } target)
        {
            ctx.Reply($"{name} is not online.");
            return;
        }
        if (target == me)
        {
            ctx.Reply("You cannot send a teleport request to yourself.");
            return;
        }
        if (p.Tpa.Request(EssentialsPlugin.KeyOf(me), EssentialsPlugin.KeyOf(target), kind, EssentialsPlugin.NowTick) != StoreResult.Ok)
        {
            ctx.Reply("That request could not be sent.");
            return;
        }
        string ask = kind == TpaKind.GoTo ? "wants to teleport to you" : "wants you to teleport to them";
        target.SendMessage(Component.Text($"{me.Name} {ask}. /tpaccept to accept, /tpdeny to deny."));
        ctx.Reply($"Request sent to {target.Name}. It expires in {p.Options.Teleport.RequestExpirySeconds} seconds.");
    }

    private static void Answer(EssentialsPlugin p, CommandContext ctx, string? name, bool accept)
    {
        Player me = ctx.RequirePlayer();
        string? from = null;
        if (name is not null)
        {
            if (Server.GetPlayer(name) is not { } named)
            {
                ctx.Reply($"{name} is not online.");
                return;
            }
            from = EssentialsPlugin.KeyOf(named);
        }

        long now = EssentialsPlugin.NowTick;
        var request = accept
            ? p.Tpa.Accept(EssentialsPlugin.KeyOf(me), from, now)
            : p.Tpa.Deny(EssentialsPlugin.KeyOf(me), from, now);
        if (request is null)
        {
            ctx.Reply(name is null ? "You have no teleport request." : $"{name} has no teleport request for you.");
            return;
        }
        if (CommandTrees.Online(request.From) is not { } asker)
        {
            ctx.Reply("That player went offline.");
            return;
        }
        if (!accept)
        {
            asker.SendMessage(Component.Text($"{me.Name} denied your teleport request."));
            ctx.Reply($"Denied the request from {asker.Name}.");
            return;
        }

        // A /tpa request moves the requester to the accepter; a /tpahere request moves the accepter to the requester.
        Player mover = request.Kind == TpaKind.GoTo ? asker : me;
        StoredLocation destination = EssentialsPlugin.Here(request.Kind == TpaKind.GoTo ? me : asker);
        var outcome = p.Teleport(mover, destination, out long wait);
        ctx.Reply(outcome switch
        {
            TeleportOutcome.Done or TeleportOutcome.Started => $"Accepted the request from {asker.Name}.",
            TeleportOutcome.Cooldown => $"{mover.Name} must wait {CommandTrees.Duration(wait)} before teleporting again.",
            _ => "The teleport could not start.",
        });
        asker.SendMessage(Component.Text($"{me.Name} accepted your teleport request."));
    }

    private static void CancelRequest(EssentialsPlugin p, CommandContext ctx)
    {
        bool cancelled = p.Tpa.CancelOutgoing(EssentialsPlugin.KeyOf(ctx.RequirePlayer()));
        ctx.Reply(cancelled ? "Your teleport request is cancelled." : "You have no teleport request to cancel.");
    }

    private static void GoBack(EssentialsPlugin p, CommandContext ctx)
    {
        Player me = ctx.RequirePlayer();
        if (!p.Backs.TryGetValue(EssentialsPlugin.KeyOf(me), out var place))
        {
            ctx.Reply("There is no previous location to return to.");
            return;
        }
        Go(p, ctx, me, place, "your previous location");
    }

    private static void TeleportToPlayer(CommandContext ctx, string name)
    {
        Player me = ctx.RequirePlayer();
        if (Server.GetPlayer(name) is not { } target)
        {
            ctx.Reply($"{name} is not online.");
            return;
        }
        ctx.Reply(me.Teleport(target.Location) ? $"Teleported to {target.Name}." : "The teleport failed.");
    }

    private static void TeleportToCoordinates(CommandContext ctx)
    {
        Player me = ctx.RequirePlayer();
        var at = new Vec3d(ctx.GetDouble("x"), ctx.GetDouble("y"), ctx.GetDouble("z"));
        ctx.Reply(me.Teleport(at) ? $"Teleported to {at.X:0.##}, {at.Y:0.##}, {at.Z:0.##}." : "The teleport failed.");
    }

    /// <summary>Runs a teleport through the pipeline and says what happened.</summary>
    private static void Go(EssentialsPlugin p, CommandContext ctx, Player player, StoredLocation place, string label)
    {
        switch (p.Teleport(player, place, out long wait))
        {
            case TeleportOutcome.Done:
                ctx.Reply($"Teleported to {label}.");
                break;
            case TeleportOutcome.Started:
                ctx.Reply($"Teleporting to {label} in {p.Options.Teleport.DelaySeconds} seconds. Do not move.");
                break;
            case TeleportOutcome.AlreadyWarming:
                ctx.Reply("You are already about to teleport.");
                break;
            case TeleportOutcome.Cooldown:
                ctx.Reply($"You must wait {CommandTrees.Duration(wait)} before teleporting again.");
                break;
            default:
                ctx.Reply("The teleport failed.");
                break;
        }
    }

    private static IEnumerable<string> HomeNames(EssentialsPlugin p, CommandContext ctx) =>
        ctx.Player is { } player ? p.Homes.Names(EssentialsPlugin.KeyOf(player)) : [];
}
