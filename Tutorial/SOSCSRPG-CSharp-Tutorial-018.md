# SOSCSRPG C# Tutorial 018 — Complete World and Quest Integration

**Platform:** Ubuntu 26.04, .NET 10, VS Code  
**Project:** Tutorial018  
**Prerequisite:** Tutorial 017 — Advanced Combat and Loot Tables

## Learning goals

Integrate connected locations, NPC dialogue, quest acceptance and completion, monster encounters, guaranteed quest drops, inventory, potion usage, and merchant purchases into a playable terminal RPG. This is a standalone educational sample, not a direct source-level patch of Tutorial 017 and not a MonoGame + Myra GUI application.

## Setup

```bash
sudo apt update
sudo apt install dotnet-sdk-10.0
code --install-extension ms-dotnettools.csharp
cd Tutorial018
dotnet restore
dotnet run
```

If .NET 10 is not available from your configured apt sources, install it using Microsoft's official Ubuntu installation instructions. Confirm `dotnet --version` reports a 10.x SDK.

## World

```text
        Town Square
        /         \
Alchemist Hut --- Forest
       |             |
  Trading Post   Spider Cave
```

The diagonal drawing is conceptual; actual travel is by cardinal commands. The hut-to-forest route is an optional shortcut.

## Commands

| Command | Action |
|---|---|
| `help` | Show commands |
| `look` | Describe the current location |
| `go east` etc. | Travel through a valid exit |
| `talk` | Interact with the current NPC |
| `quest` | View quest progress |
| `fight` | Start a local monster encounter |
| `attack`, `defend`, `potion`, `flee` | Combat actions |
| `inventory` | View items |
| `status` | View HP and gold |
| `buy potion` | Buy a potion at the Trading Post for 5 gold |
| `use potion` | Restore up to 12 HP |
| `quit` | Exit |

## Guided quest walkthrough

```text
go west
talk
quest
go east
go east
go south
fight
```

Inside each spider encounter, use `attack` until the spider is defeated. Repeat `fight` until you have three Spider Silk, then:

```text
go north
go west
talk
quest
status
```

Expected quest result: **3 Spider Silk are consumed and 20 gold are awarded**. Cave Spiders drop one Spider Silk per victory, guaranteeing progress without relying on RNG.

## Architecture

- `Location` stores a location's descriptive metadata and exits.
- `Quest` tracks quest acceptance and completion.
- `Game` owns world state, inventory, combat, merchant actions, and command dispatch.
- `Program.cs` provides the terminal entry point.

For a production version, extract `Game` into UI-independent domain services, use injectable random-number generation, implement save/load, and add automated tests. This chapter is a runnable integration baseline, not a full reproduction of the preceding tutorials' project structure.

## Verification checklist

- [ ] `dotnet build` succeeds on a machine with .NET 10.
- [ ] All five locations are reachable.
- [ ] Talking to the alchemist starts the quest once.
- [ ] Every defeated Cave Spider drops one Spider Silk.
- [ ] Quest completion consumes three silk and awards 20 gold exactly once.
- [ ] Potions heal without exceeding 30 HP.
- [ ] Invalid travel and commands are handled without crashing.

## Next chapter

**Tutorial 019 — Testing, Refactoring, and Stabilization** will introduce testable services, deterministic combat tests, and regression checks before the final release.
