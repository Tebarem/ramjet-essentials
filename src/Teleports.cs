using Mc.Scripting.Essentials.Core;
using Mc.Scripting.Worlds;

namespace Mc.Scripting.Essentials;

/// <summary>What a teleport request did.</summary>
internal enum TeleportOutcome
{
    /// <summary>The player was moved.</summary>
    Done,
    /// <summary>A warm-up started; the move happens when it ends.</summary>
    Started,
    /// <summary>The player already has a warm-up running.</summary>
    AlreadyWarming,
    /// <summary>The cooldown has not passed.</summary>
    Cooldown,
    /// <summary>The player is offline or the destination's world does not exist.</summary>
    Failed,
}

/// <summary>The teleport pipeline: cooldown, warm-up, safe destination and the back location. Every teleport of the plugin goes through here.</summary>
public sealed partial class EssentialsPlugin
{
    // The overworld build limits. Other dimensions use the same range until the engine reports each one's limits.
    private const int MinBlockY = -64;
    private const int MaxBlockY = 320;

    /// <summary>Teleports a player to a place, through the cooldown and the warm-up unless the player has the bypass nodes.</summary>
    /// <param name="player">The player to move.</param>
    /// <param name="target">The destination.</param>
    /// <param name="waitSeconds">For <see cref="TeleportOutcome.Cooldown"/>: the seconds left.</param>
    internal TeleportOutcome Teleport(Player player, StoredLocation target, out long waitSeconds)
    {
        waitSeconds = 0;
        if (!player.IsOnline) return TeleportOutcome.Failed;
        string id = KeyOf(player);
        long now = NowTick;
        var settings = Options.Teleport;

        if (settings.CooldownSeconds > 0 && !player.HasPermission(Nodes.CooldownBypass))
        {
            long cooldown = settings.CooldownSeconds * TicksPerSecond;
            if (!TeleportCooldowns.IsReady(id, now, cooldown))
            {
                waitSeconds = (TeleportCooldowns.Remaining(id, now, cooldown) + TicksPerSecond - 1) / TicksPerSecond;
                return TeleportOutcome.Cooldown;
            }
        }

        long delay = player.HasPermission(Nodes.WarmupBypass) ? 0 : settings.DelaySeconds * TicksPerSecond;
        if (delay > 0)
        {
            if (Warmups.Has(id)) return TeleportOutcome.AlreadyWarming;
            Warmups.Begin(id, now, delay, PosOf(player), new TeleportJob(player, target));
            Scheduler.RunLater(delay, () => FinishWarmup(id));
            return TeleportOutcome.Started;
        }
        return Execute(player, target) ? TeleportOutcome.Done : TeleportOutcome.Failed;
    }

    /// <summary>The warm-up's timer. A stale timer finds nothing due and does nothing.</summary>
    private void FinishWarmup(string id)
    {
        if (!Warmups.TryComplete(id, NowTick, out var job)) return;
        if (!Execute(job.Who, job.Target) && job.Who.IsOnline) job.Who.SendMessage(Component.Text("Teleport failed."));
    }

    private bool Execute(Player player, StoredLocation target)
    {
        if (!player.IsOnline) return false;
        StoredLocation destination = Options.Teleport.Safety ? SafeDestination(target) : target;
        if (!TryLocation(destination, out Location location)) return false;

        string id = KeyOf(player);
        Backs[id] = Here(player);
        if (!player.Teleport(location)) return false;
        TeleportCooldowns.Stamp(id, NowTick);
        return true;
    }

    /// <summary>Where the spawn is: the spawn set with <c>/setspawn</c>, else the overworld's spawn.</summary>
    internal StoredLocation SpawnTarget()
    {
        if (Warps.Spawn is { } spawn) return spawn;
        Location at = Server.Overworld.SpawnLocation;
        return new StoredLocation(at.World.Key, at.X, at.Y, at.Z, at.Yaw, at.Pitch);
    }

    /// <summary>A player's place as stored.</summary>
    internal static StoredLocation Here(Player player)
    {
        Location at = player.Location;
        return new StoredLocation(at.World.Key, at.X, at.Y, at.Z, at.Yaw, at.Pitch);
    }

    private static Pos PosOf(Player player)
    {
        Vec3d at = player.Position;
        return new Pos(at.X, at.Y, at.Z);
    }

    private static bool TryLocation(StoredLocation place, out Location location)
    {
        if (Server.GetWorld(place.World) is not { } world)
        {
            location = default;
            return false;
        }
        location = new Location(world, new Vec3d(place.X, place.Y, place.Z), (float)place.Yaw, (float)place.Pitch);
        return true;
    }

    /// <summary>Keeps the destination's column and moves its height to the nearest standing place when the saved height is inside blocks. The
    /// destination stays as saved when no standing place is near, or when the chunk is not loaded.</summary>
    private static StoredLocation SafeDestination(StoredLocation target)
    {
        if (Server.GetWorld(target.World) is not { } world) return target;
        int x = (int)Math.Floor(target.X), z = (int)Math.Floor(target.Z);
        int? y = SafeSpot.FindY((int)Math.Floor(target.Y), MinBlockY, MaxBlockY, feet => Standable(world, x, feet, z));
        return y is { } found ? target with { Y = found } : target;
    }

    /// <summary>Solid ground under the feet and two free cells for the feet and the head.</summary>
    private static bool Standable(World world, int x, int feetY, int z) =>
        Block(world, x, feetY - 1, z, out string ground) && IsSolid(ground)
        && Block(world, x, feetY, z, out string feet) && IsPassable(feet)
        && Block(world, x, feetY + 1, z, out string head) && IsPassable(head);

    private static bool Block(World world, int x, int y, int z, out string key)
    {
        if (world.TryGetBlock(new BlockPos(x, y, z), out BlockState state))
        {
            key = state.Key;
            return true;
        }
        key = "";
        return false;
    }

    /// <summary>Blocks a player can stand in or walk through. A name heuristic: air, grass, flowers, saplings and torches.</summary>
    internal static bool IsPassable(string key) =>
        key == "minecraft:air" || key.EndsWith("_air", StringComparison.Ordinal) || key.EndsWith("grass", StringComparison.Ordinal)
        || key.EndsWith("fern", StringComparison.Ordinal) || key.EndsWith("bush", StringComparison.Ordinal)
        || key.EndsWith("flower", StringComparison.Ordinal) || key.EndsWith("tulip", StringComparison.Ordinal)
        || key.EndsWith("sapling", StringComparison.Ordinal) || key.EndsWith("torch", StringComparison.Ordinal);

    /// <summary>Solid enough to stand on: not passable and not a liquid.</summary>
    internal static bool IsSolid(string key) =>
        !IsPassable(key) && !key.Contains("water", StringComparison.Ordinal) && !key.Contains("lava", StringComparison.Ordinal);
}
