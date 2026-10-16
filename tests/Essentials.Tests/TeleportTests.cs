using Mc.Scripting.Essentials.Core;

namespace Mc.Scripting.Essentials.Tests;

/// <summary>Teleport requests, warm-ups, cooldowns and kits.</summary>
internal static class TeleportTests
{
    internal static void Register(Action<string, Action> add)
    {
        add("tpa: accept picks the most recent request, a name picks that one", () =>
        {
            var tpa = new TpaBook(100);
            tpa.Request("a", "t", TpaKind.GoTo, 0);
            tpa.Request("b", "t", TpaKind.Here, 5);
            var latest = tpa.Accept("t", null, 10);
            Check.True(latest is { From: "b", Kind: TpaKind.Here }, "most recent first");
            Check.True(tpa.Accept("t", "a", 10) is { From: "a" }, "named request accepted");
            Check.True(tpa.Accept("t", null, 10) is null, "nothing left");
        });

        add("tpa: requests expire, self-requests are refused, a new request replaces the old", () =>
        {
            var tpa = new TpaBook(100);
            Check.Equal(StoreResult.Invalid, tpa.Request("a", "a", TpaKind.GoTo, 0), "self request");
            tpa.Request("a", "t", TpaKind.GoTo, 0);
            Check.True(tpa.Accept("t", null, 101) is null, "expired after 100 ticks");

            tpa.Request("a", "t", TpaKind.GoTo, 200);
            tpa.Request("a", "u", TpaKind.Here, 201);
            Check.Equal(0, tpa.Incoming("t", 202).Count, "the old request was replaced");
            Check.Equal(1, tpa.Incoming("u", 202).Count, "the new request is there");
        });

        add("tpa: deny, cancel and forget", () =>
        {
            var tpa = new TpaBook(100);
            tpa.Request("a", "t", TpaKind.GoTo, 0);
            Check.True(tpa.Deny("t", "a", 1) is { From: "a" }, "denied");
            tpa.Request("a", "t", TpaKind.GoTo, 2);
            Check.True(tpa.CancelOutgoing("a"), "cancelled");
            Check.True(!tpa.CancelOutgoing("a"), "nothing to cancel the second time");
            tpa.Request("a", "t", TpaKind.GoTo, 3);
            tpa.Forget("t");
            Check.Equal(0, tpa.Incoming("t", 4).Count, "forgotten on quit");
        });

        add("warm-up: completes when due, refuses a second start", () =>
        {
            var warmups = new Warmups<string>(0.2);
            Check.True(warmups.Begin("p", 0, 60, new Pos(0, 64, 0), "job"), "begin");
            Check.True(!warmups.Begin("p", 0, 60, new Pos(0, 64, 0), "again"), "second start refused");
            Check.True(!warmups.TryComplete("p", 59, out _), "not due yet");
            Check.True(warmups.TryComplete("p", 60, out var job) && job == "job", "due at the delay");
            Check.True(!warmups.TryComplete("p", 61, out _), "gone after completion");
        });

        add("warm-up: moving beyond the tolerance cancels it", () =>
        {
            var warmups = new Warmups<string>(0.2);
            warmups.Begin("p", 0, 60, new Pos(0, 64, 0), "job");
            Check.True(!warmups.Moved("p", new Pos(0.1, 64, 0)), "a small move stays");
            Check.True(warmups.Moved("p", new Pos(1, 64, 0)), "a move beyond 0.2 cancels");
            Check.True(!warmups.Has("p"), "removed");
            Check.True(!warmups.Moved("p", new Pos(5, 64, 0)), "no warm-up, nothing to cancel");
        });

        add("cooldowns: ready after the delay, remaining counts down, survives a copy", () =>
        {
            var cooldowns = new Cooldowns();
            Check.True(cooldowns.IsReady("k", 0, 100), "unknown key is ready");
            cooldowns.Stamp("k", 50);
            Check.True(!cooldowns.IsReady("k", 120, 100), "70 of 100 passed");
            Check.Equal(30L, cooldowns.Remaining("k", 120, 100), "remaining");
            Check.True(cooldowns.IsReady("k", 150, 100), "ready at exactly 100");

            var copy = new Cooldowns();
            copy.Load(cooldowns.Export());
            Check.True(!copy.IsReady("k", 120, 100), "copy keeps the stamp");
        });

        add("kits: item lines parse with the default namespace and count", () =>
        {
            Check.True(KitBook.TryParseItem("minecraft:bread 8", out var explicitItem) && explicitItem == new KitItem("minecraft:bread", 8), "explicit");
            Check.True(KitBook.TryParseItem("stone_sword", out var bare) && bare == new KitItem("minecraft:stone_sword", 1), "bare id, count 1");
            Check.True(!KitBook.TryParseItem("bread 0", out _), "zero refused");
            Check.True(!KitBook.TryParseItem("bread 6401", out _), "over 6400 refused");
            Check.True(!KitBook.TryParseItem("Bread", out _), "upper case refused");
            Check.True(!KitBook.TryParseItem("a:b:c", out _), "two colons refused");
            Check.True(!KitBook.TryParseItem("bread x", out _), "count must be a number");
        });

        add("kits: bad entries are reported and skipped, the kit still loads", () =>
        {
            var book = new KitBook();
            var errors = new List<string>();
            var config = new Dictionary<string, Settings.KitConfig>
            {
                ["starter"] = new() { DelaySeconds = 60, Items = ["minecraft:bread 2", "BAD ITEM"] },
            };
            book.Load(config, errors);
            Check.True(book.TryGet("starter", out var kit) && kit.Items.Count == 1 && kit.DelaySeconds == 60, "one item left, delay kept");
            Check.Equal(1, errors.Count, "one error reported");
        });
    }
}
