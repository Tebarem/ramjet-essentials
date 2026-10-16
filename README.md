# Essentials

Ramjet's first-party Essentials plugin (`id: essentials`): homes, warps, spawn, teleport requests, back, kits, nicks,
private messages and the default economy. Clean-room: written from the public command reference and its own design
(`src/DESIGN.md`). No code of the original plugin was read or copied.

- `src/`: the plugin, a Ramjet source plugin. Deploy it as `plugins/essentials/` (the server compiles it;
  `src/essentials.csproj` only exists for an out-of-server compile check). Design notes: `src/DESIGN.md`.
- `tests/Essentials.Tests/`: unit tests for the storage, teleport, kit, economy and config rules (console app, no packages).

Build and test against an engine build: set `RAMJET_HOME` to the engine's `build/bin` (the folder with `dotnet/Mc.Scripting.Api.dll`).

```bash
dotnet build src/essentials.csproj
dotnet run --project tests/Essentials.Tests
```

## Commands (MVP)

| Command | Purpose |
|---|---|
| `/sethome [name]`, `/home [name]`, `/delhome <name>`, `/renamehome <old> <new>` | Homes; the limit depends on the `essentials.sethome.multiple.<tier>` nodes |
| `/setwarp <name>`, `/warp [name]`, `/delwarp <name>` | Warps; `/warp` alone lists them |
| `/spawn`, `/setspawn` | The spawn point |
| `/tpa <player>`, `/tpahere <player>`, `/tpaccept [player]`, `/tpdeny [player]`, `/tpacancel` | Teleport requests |
| `/back` | Return to the place before the last teleport or death |
| `/etp <player>`, `/etp <x> <y> <z>` | Administrator teleport without warm-up (`/tp` is vanilla; see below) |
| `/kit [name]`, `/showkit <name>` | Kits, with a per-player cooldown |
| `/nick <name or off>`, `/realname <nick>` | Display names |
| `/emsg <player> <text>`, `/r <text>` | Private messages (`/msg` is vanilla; see below) |
| `/balance [player]`, `/balancetop [page]`, `/pay <player> <amount>` | Money |
| `/eco give, take, set or reset <player> [amount]` | Administrator money commands |
| `/essentials [reload]` | Plugin information and configuration reload |

Configuration is `config.json`, written with defaults on the first run. Data is kept in JSON files in the plugin's data folder.

## Notes for admins

- `/tp` and `/msg` belong to vanilla. Essentials uses `e`-prefixed names (`/etp`, `/emsg`) until the
  server's opt-in command override is available for it.
- When the chat plugin is installed it handles the delivery of `/emsg` and `/r`, including ignore and mute rules. Without it,
  Essentials sends a plain message to both players.
- Economy: Essentials is the default money provider. Any economy plugin registered with a higher service priority replaces it.

## License

MIT, see `LICENSE`.
