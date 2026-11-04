# SOSCSRPG — MonoGame + Myra Edition

An educational C# role-playing game project built with **MonoGame DesktopGL** and **Myra**. This repository tracks the SOSCSRPG learning project through **Phase 8 (Audit)**. It is **not** the separate SuperAdventure MonoGame adaptation.

> **Release status:** Phase 8 source-level audit and regression baseline. Full upstream feature parity and Linux runtime acceptance have **not** been certified.

## Technology

- C# / .NET 10
- MonoGame Framework DesktopGL (`3.8.*`)
- Myra UI (`1.6.8`)
- JSON persistence with `System.Text.Json`
- Linux DesktopGL / X11 launch path

## Features implemented

- World locations and directional movement
- Monsters, weapon-based combat and loot
- Inventory, consumable healing, trading and merchant stock
- Experience, leveling and two sequential quests
- Versioned JSON save/load, including active encounters and merchant inventory
- Engine-only automated checks without a MonoGame dependency

These are features verified from the provided source, **not** a statement of exact upstream equivalence.

## Repository layout

```text
.
├── SoscMonoGame/
│   ├── Engine/
│   │   ├── Actions/
│   │   ├── Factories/
│   │   ├── Models/
│   │   ├── GameSession.cs
│   │   ├── GameSaveData.cs
│   │   └── GameSaveService.cs
│   ├── Game1.cs
│   ├── Program.cs
│   ├── SoscMonoGame.csproj
│   └── README-Phase1.md ... README-Phase8.md
├── SoscMonoGame.EngineChecks/
├── AUDIT-Phase8.md
├── run-phase7.sh
└── run-phase8.sh
```

## Requirements

Install the .NET 10 SDK and the Linux dependencies required by MonoGame DesktopGL, SDL2 and OpenGL. An X11 session is expected by the provided launch script. NuGet access is required on the first restore.

## Run on Linux

From the repository root:

```bash
chmod +x run-phase8.sh
./run-phase8.sh
```

Run the automated audit without opening the graphical game:

```bash
./run-phase8.sh --no-gui
```

The script uses `/usr/bin/dotnet`, clears `DOTNET_ROOT` for its invocations, restores both projects, builds with warnings treated as errors, executes engine checks, and optionally launches with `SDL_VIDEODRIVER=x11`. If your .NET installation is elsewhere, adjust `DOTNET` in the script.

Direct launch:

```bash
env -u DOTNET_ROOT SDL_VIDEODRIVER=x11 /usr/bin/dotnet run --project SoscMonoGame/SoscMonoGame.csproj
```

## Save files

The default save path is resolved from .NET's `Environment.SpecialFolder.LocalApplicationData` and uses `SoscMonoGame/save-v1.json`. The save format is versioned (`Version = 1`); invalid or incompatible data is rejected. The save operation writes a temporary file and replaces the destination, but does not provide multi-process locking, orphan recovery or deterministic PRNG replay.

## Development history

| Phase | Scope |
| --- | --- |
| 1–6 | Progressive gameplay and engine construction; see the individual phase notes |
| 7 | JSON Save/Load and snapshot regression coverage |
| 8 | Source audit, edge-case checks and documented acceptance criteria |
| 8 maintenance | Checked inventory arithmetic and regression tests for overflow rejection |

For detailed findings and outstanding work, see [AUDIT-Phase8.md](AUDIT-Phase8.md).

## Known limitations and acceptance criteria

- `Game1.cs` still owns much of the Myra UI tree and event handling.
- Domain models are publicly mutable; not all direct changes notify `GameSession.StateChanged`.
- PRNG state is not saved; combat sequences are not replay-deterministic after loading.
- Multi-process save locking and temporary-file recovery are not implemented.
- Exact upstream SOSCSRPG commit and lesson-by-lesson parity matrix are not pinned.
- Compilation, automated checks and graphical smoke testing must be run on a Linux system with .NET installed.

A release signoff requires passing `./run-phase8.sh --no-gui` and manually verifying movement, combat, healing, trading, both quests, save and load in the graphical client.

## Attribution

This project is an independent educational **MonoGame + Myra** implementation in the Scott Lilly RPG tutorial lineage. See Scott Lilly's [SuperAdventure](https://github.com/ScottLilly/SuperAdventure) for a related upstream RPG project. This README does **not** claim that SuperAdventure and SOSCSRPG are identical or that this repository is an official port. Verify original source licensing and preserve required notices before redistribution.

## License

No license file is included in the supplied Phase 8 archive. Confirm licensing for the original materials and this adaptation before assigning a license or publishing third-party code.
# SOSCSRPGMonoGame

## License

This project is licensed under the MIT License.
See [LICENSE](LICENSE).

## Acknowledgments

This educational project is based on concepts from
[Scott Lilly's SOSCSRPG](https://github.com/ScottLilly/SOSCSRPG).

Original SOSCSRPG copyright:
Copyright (C) 2012-2021 Scott Lilly.

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
for attribution and third-party licensing information.
