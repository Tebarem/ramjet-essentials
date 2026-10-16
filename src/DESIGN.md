# Essentials: design

Status: MVP. Homes, warps, spawn, teleport requests, back, kits, nicks, private-message commands, money and the
balance commands. The data importer, the sign triggers and the later command families are not in this version.

## Layout

- `Core/`: pure rules with no engine types, so the unit tests run without a server. Homes and warps (`HomeBook`,
  `WarpBook`), balances (`Ledger`, `MoneyText`), teleport requests (`TpaBook`), warm-ups and cooldowns (`Warmups<T>`,
  `Cooldowns`), kits (`KitBook`), the standing-height search (`SafeSpot`), reply targets (`ReplyBook`), name rules (`Names`).
- `Settings/`: the configuration classes, the data-file documents and the JSON source-generation context (kebab-case names).
- Plugin layer (`EssentialsPlugin.cs`, `Teleports.cs`, `DataStore.cs`, `Economy.cs`, `Commands/`): the engine calls. The
  plugin keeps the state the core classes hold, reads the events, persists the files and builds the command trees.
- `Nodes.cs`: the permission node names. They follow the public reference of the original plugin, so an existing
  permission setup keeps working.

## Economy

Balances are `long` minor units; a text amount is parsed without floating point (`MoneyText`). `Ledger` checks every
rule: no overdraft, no balance over the maximum, a transfer changes both accounts or neither. The plugin registers
`IEconomy` at `ServicePriority.Lowest`, so an economy plugin registered higher takes over every consumer. Accounts are
created on join with the starting balance. `/pay` to an offline player works when that player has an account.

`/eco give` and `/eco take` go through the `IEconomy` service; `/eco set` and `/eco reset` write the ledger directly
(the service has no set operation), so listeners of `Changed` do not hear administrator sets.

## Teleports

Every teleport goes through one pipeline (`Teleport` in `Teleports.cs`):

1. Cooldown (`teleport.cooldown-seconds`), unless the player holds `essentials.teleport.cooldown.bypass`.
2. Warm-up (`teleport.delay-seconds`), unless the player holds `essentials.teleport.warmup.bypass`. A move beyond
   `teleport.move-tolerance` cancels it. The timer checks the due tick, so a stale timer does nothing.
3. Safe destination (`teleport.safety`): the saved height moves to the nearest standing place in the same column,
   searching 24 blocks each way. A chunk that is not loaded counts as not standable, and the saved height is kept then.
4. The back location is recorded before the move. The engine's teleport event also records it, which gives the same place.

Request expiry (`teleport.request-expiry-seconds`) is checked on every request and answer. A request is replaced by the
same sender's next request. `/tpaccept` without a name takes the most recent request.

## Persistence

Four JSON files in the plugin's data folder: `homes.json`, `warps.json` (warps and the spawn), `economy.json` (accounts),
`players.json` (nicks and kit cooldowns). Each file is read once on enable. Changes are batched to one write per second,
and every changed file is written on server stop. A file that does not parse is reported and never written, so defaults
cannot replace it. Writes use the atomic write of the plugin config layer.

Deviation from the original plan: one file per kind instead of one file per player. The whole file is read at boot, which the
memory budget accepts at this size; per-player files are the next step if the files grow.

## Chat hand-off

`IPrivateMessages` (in `Mc.Scripting.Services`, engine managed API) is the contract. The chat plugin registers it when it
is installed and owns formatting, ignore and mute. `/emsg` and `/r` call it when present and send a plain line to both
players otherwise. Essentials keeps the reply targets either way.

## Command roots

`/tp` and `/msg` are vanilla roots, so Essentials registers `/etp` and `/emsg` for now. `/r` is free and used as is.
Overriding `/tp` and `/msg` needs the opt-in command override of the command layer; once it is there, the same trees can
be registered under the vanilla names.

## Known gaps (MVP)

- Teleport invulnerability is not implemented, and a warm-up is not cancelled by damage (no damage listener yet).
- Per-warp permission nodes (`essentials.warp.<name>`) are not checked; `/warp` checks `essentials.warp` only.
- The death location is the player's position when the death event fires. This is expected to be the place of death; it
  is not yet verified on a server.
- The height limits used by the safety search are the overworld's (-64 to 320). Other dimensions use the same range.
- Passable and solid blocks are classified by name (air, grass, flowers, torches and similar; liquids are never solid).
  A block that the name test gets wrong can place a player inside it or next to it.
- Economy transaction logging is a plain server log line, not a ledger file.
- Not done: the importer, `/whois`, mail, AFK, jail, the sign triggers, random teleport, the text pages.
- Config changes to the currency and the balance limits apply after a restart; the other settings apply on reload.
