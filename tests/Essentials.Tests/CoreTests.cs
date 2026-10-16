using Mc.Scripting.Essentials.Core;

namespace Mc.Scripting.Essentials.Tests;

/// <summary>Homes, warps, names, the reply book and the safe-spot search.</summary>
internal static class CoreTests
{
    private static readonly StoredLocation Spot = new("minecraft:overworld", 1, 64, 2, 0, 0);

    internal static void Register(Action<string, Action> add)
    {
        add("homes: the limit counts new names, not overwrites", () =>
        {
            var homes = new HomeBook();
            Check.Equal(StoreResult.Ok, homes.Set("u1", "a", Spot, 2), "first home");
            Check.Equal(StoreResult.Ok, homes.Set("u1", "b", Spot, 2), "second home");
            Check.Equal(StoreResult.Limit, homes.Set("u1", "c", Spot, 2), "third home is over the limit");
            Check.Equal(StoreResult.Ok, homes.Set("u1", "A", Spot with { X = 9 }, 2), "overwrite does not count");
            Check.True(homes.TryGet("u1", "a", out var place) && place.X == 9, "names are case-insensitive and the overwrite stored");
            Check.Equal(2, homes.Count("u1"), "count");
        });

        add("homes: invalid names are refused", () =>
        {
            var homes = new HomeBook();
            Check.Equal(StoreResult.Invalid, homes.Set("u", "bad name", Spot, 5), "space");
            Check.Equal(StoreResult.Invalid, homes.Set("u", "", Spot, 5), "empty");
            Check.Equal(StoreResult.Invalid, homes.Set("u", new string('x', 33), Spot, 5), "33 characters");
        });

        add("homes: rename keeps the place and refuses an existing name", () =>
        {
            var homes = new HomeBook();
            homes.Set("u", "a", Spot, 5);
            homes.Set("u", "b", Spot, 5);
            Check.Equal(StoreResult.Exists, homes.Rename("u", "a", "b"), "target exists");
            Check.Equal(StoreResult.Ok, homes.Rename("u", "a", "c"), "rename");
            Check.True(!homes.TryGet("u", "a", out _) && homes.TryGet("u", "c", out _), "old name gone, new name there");
            Check.Equal(StoreResult.NotFound, homes.Rename("u", "zz", "y"), "missing home");
        });

        add("homes: remove, and export/load keeps the owners with homes only", () =>
        {
            var homes = new HomeBook();
            homes.Set("u", "a", Spot, 5);
            homes.Set("v", "x", Spot, 5);
            Check.Equal(StoreResult.Ok, homes.Remove("v", "x"), "remove");
            Check.Equal(StoreResult.NotFound, homes.Remove("v", "x"), "already gone");
            var copy = new HomeBook();
            copy.Load(homes.Export());
            Check.Equal(1, copy.Count("u"), "loaded home");
            Check.Equal(0, copy.Count("v"), "owner without homes is not exported");
        });

        add("homes: the highest tier held sets the limit", () =>
        {
            var tiers = new Dictionary<string, int> { ["pro"] = 5, ["elite"] = 10 };
            Check.Equal(10, HomeBook.ResolveLimit(tiers, t => t is "pro" or "elite", 3), "elite wins");
            Check.Equal(5, HomeBook.ResolveLimit(tiers, t => t == "pro", 3), "pro alone");
            Check.Equal(3, HomeBook.ResolveLimit(tiers, _ => false, 3), "no tier: default");
        });

        add("warps: case-insensitive, spawn optional, export/load", () =>
        {
            var warps = new WarpBook();
            Check.True(warps.Spawn is null, "no spawn by default");
            Check.Equal(StoreResult.Ok, warps.Set("Market", Spot, "u"), "set");
            Check.True(warps.TryGet("market", out var warp) && warp.Name == "Market", "found by lower case, name kept");
            Check.Equal(StoreResult.Invalid, warps.Set("no way", Spot, "u"), "invalid name");
            warps.SetSpawn(Spot);

            var copy = new WarpBook();
            copy.Load(warps.Export(), warps.Spawn);
            Check.Equal(1, copy.Names.Count, "warp loaded");
            Check.True(copy.Spawn == Spot, "spawn loaded");
            Check.Equal(StoreResult.Ok, copy.Remove("MARKET"), "remove ignores case");
            Check.Equal(0, copy.Names.Count, "empty after remove");
        });

        add("names: the rules for names and nicks", () =>
        {
            Check.True(Names.IsValid("my_home-1"), "letters, digits, underscore, hyphen");
            Check.True(!Names.IsValid("a.b"), "dot refused");
            Check.True(!Names.IsValid(null), "null refused");
            Check.True(Names.IsValidNick("Steve"), "short nick");
            Check.True(!Names.IsValidNick(new string('n', 17)), "nick longer than 16 refused");
        });

        add("reply book: both ends point at each other", () =>
        {
            var replies = new ReplyBook();
            replies.Record("a", "b");
            Check.Equal<string?>("b", replies.PartnerOf("a"), "a replies to b");
            Check.Equal<string?>("a", replies.PartnerOf("b"), "b replies to a");
            replies.Forget("a");
            Check.Equal<string?>(null, replies.PartnerOf("a"), "forgotten");
        });

        add("safe spot: the nearest standing height, none when nothing stands", () =>
        {
            Check.Equal<int?>(5, SafeSpot.FindY(5, 0, 50, y => y == 5), "start is standable");
            Check.Equal<int?>(11, SafeSpot.FindY(20, 0, 50, y => y == 11), "found below the start");
            Check.Equal<int?>(null, SafeSpot.FindY(20, 0, 50, _ => false), "nothing stands");
            Check.Equal<int?>(null, SafeSpot.FindY(20, 0, 50, y => y == 70), "outside the search radius");
        });
    }
}
