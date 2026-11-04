# SOSCSRPG C# Tutorial 019 — Testing, Refactoring, and Stabilization

**Series:** SOSCSRPG C# Tutorials (19/20)  
**Platform:** Ubuntu 26.04 · .NET 10 · Visual Studio Code  
**Prerequisite:** Tutorial 018 — Complete World and Quest Integration  
**Language:** English

> An original educational adaptation inspired by Scott Lilly's SOSCSRPG. This is a standalone terminal RPG with a UI-independent game engine; it is **not** a compiled MonoGame + Myra application.

## 1. Learning objectives

Tutorial 018 integrated exploration, quests, combat, inventory, and merchants. Tutorial 019 makes those systems easier to test and maintain:

1. Separate gameplay rules from terminal input/output.
2. Inject an `IRandomSource` so combat outcomes can be deterministic in tests.
3. Create a zero-dependency regression runner (no xUnit/NUnit package downloads).
4. Test world navigation, quest acceptance/completion, guaranteed loot, combat validation, and merchant rules.
5. Keep the gameplay engine reusable by a future MonoGame + Myra UI.

## 2. Requirements

- Ubuntu 26.04 (or another .NET 10-supported Linux distribution)
- .NET 10 SDK (`dotnet --version`)
- VS Code with the C# extension (optional; CLI is sufficient)

Verify your environment:

```bash
dotnet --info
code --version
```

## 3. Project structure

```text
Tutorial019/
├── RpgEngine/
│   ├── RpgEngine.csproj
│   └── GameEngine.cs        # World, quest, inventory, combat and RNG abstraction
├── TerminalRpg/
│   ├── TerminalRpg.csproj
│   └── Program.cs           # CLI adapter, no game rules
├── RegressionTests/
│   ├── RegressionTests.csproj
│   └── Program.cs           # Standalone deterministic regression tests
└── README.md
```

## 4. Build and run

From the extracted `Tutorial019` directory:

```bash
dotnet restore TerminalRpg/TerminalRpg.csproj
dotnet build TerminalRpg/TerminalRpg.csproj
dotnet run --project TerminalRpg/TerminalRpg.csproj
```

Use `code .` to open the whole directory in VS Code. The `TerminalRpg` project references `RpgEngine` directly.

### CLI commands

```text
help
look
go west
talk
quest
inventory
status
fight
buy potion
use potion
quit
```

During combat, use `attack`, `defend`, `potion`, or `flee`.

## 5. Automated regression tests

```bash
dotnet run --project RegressionTests/RegressionTests.csproj
```

The runner prints one `PASS` or `FAIL` line per test and returns a nonzero exit status if any test fails. Tests cover:

| Test | Expected behavior |
| --- | --- |
| Initial state | Town Square, 30 HP |
| Invalid navigation | No movement |
| World graph | Hut and cave reachable |
| Quest acceptance | Alchemist activates quest |
| Guaranteed loot | One Spider Silk per cave victory |
| Quest transaction | Three silk removed, 20 gold paid once |
| Merchant restrictions | Requires shop location and funds |
| Invalid combat action | No turn consumed |
| Safe locations | Cannot start combat |
| Flee | Ends combat without loot |

### Deterministic combat

Production uses `SystemRandomSource`, which wraps `System.Random`. Tests use `FixedRandomSource(99)`, a deterministic value generator. The `IRandomSource` interface lets you substitute additional sequences later for edge-case tests. The fixed source is deliberately simple; it is not a realistic probability simulator.

## 6. Architecture and refactoring

`GameEngine` owns game state and rules. `TerminalRpg` owns console input and rendering. `RegressionTests` calls the engine directly and never needs to simulate a keyboard or scrape console text.

The combat state is represented by `GameEngine.CombatSession`. Each `Act` call processes a player action and, when appropriate, the enemy's response. An invalid action does not consume a turn. Victory awards gold and loot; defeat restores the player in town.

The Spider Silk drop is **guaranteed** when a Cave Spider is defeated. A separate random roll can award a bonus potion. This protects the main quest from depending on random chance.

Quest completion is one-time: three silk are consumed and 20 gold are added. Talking again cannot award the reward twice.

## 7. Manual integration walkthrough

Start the CLI and enter:

```text
go west
talk
go east
go east
go south
fight
```

Attack until the Cave Spider is defeated. Repeat `fight` twice more. Then:

```text
go north
go west
go west
talk
quest
status
```

Expected quest output:

```text
Quest complete! You received 20 gold.
Spider Silk: Completed.
```

The quest also earns 9 gold from the three spiders, so the final balance should be 29 gold if no other purchases or battles occurred.

## 8. Limitations and future improvements

- No save/load system in this standalone example.
- No graphical MonoGame + Myra frontend yet.
- No dependency on external unit-test frameworks; the runner is a custom console test harness.
- Encounters can be started repeatedly in dangerous locations.
- Combat randomness can be made more expressive by adding a sequence-backed fake RNG.
- Enemy health and item definitions are hardcoded for clarity; Tutorial 020 can move them to data files.

## 9. Tutorial 020 preview

**Final Integration and GitHub Release:** add release checks, documentation, versioning, and packaging, and prepare a clean repository suitable for publication.

## Attribution

This project is an original educational adaptation inspired by [Scott Lilly's SOSCSRPG](https://github.com/ScottLilly/SOSCSRPG). It is not an official Scott Lilly release.
