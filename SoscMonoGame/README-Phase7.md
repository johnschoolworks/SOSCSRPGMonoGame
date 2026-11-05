# Phase 7 — SOSCSRPG Save/Load and Engine Notifications

This phase builds on **Phase 6 Fixed**, including the corrected `LootDrop.cs`.

## New features

- `Engine/GameSaveData.cs`: versioned, DTO-based save format (no UI objects in JSON).
- `Engine/GameSaveService.cs`: JSON save/load, validation, temporary-file then replace, default Linux user-data path.
- Saves player stats, inventory, equipped weapon, location, live monster HP/type, completed quests, merchant inventory and combat log.
- `GameSession.StateChanged`: UI can refresh in response to Engine events, rather than manually calling `RefreshView()` after each command.
- Myra `Save Game` and `Load Game` buttons.
- EngineChecks for save/load roundtrip, active monster, merchant stock, corrupted JSON.

## Linux

```bash
cd ~/Projects/SoscMonoGame-Phase7
env -u DOTNET_ROOT /usr/bin/dotnet restore SoscMonoGame/SoscMonoGame.csproj
env -u DOTNET_ROOT /usr/bin/dotnet build SoscMonoGame/SoscMonoGame.csproj --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run --project SoscMonoGame.EngineChecks/SoscMonoGame.EngineChecks.csproj
env -u DOTNET_ROOT SDL_VIDEODRIVER=x11 /usr/bin/dotnet run --project SoscMonoGame/SoscMonoGame.csproj --no-build
```

Default save location is `~/.local/share/SoscMonoGame/save-v1.json` on typical Linux .NET setups. The exact location is shown in the game's combat log when saved.

## Scope and caveats

- Save format version is **1**. Unsupported versions are rejected; migration is not yet implemented.
- Load validates IDs, quantities, HP, level/XP, quest prerequisites and location/monster combinations.
- Random generator state is **not** persisted; random results after loading may differ.
- `StateChanged` is a lightweight event emitted when a log entry is added; it is not a comprehensive observable-property system.
- The Myra UI is still implemented in `Game1.cs`; UI extraction into its own class remains future work.
- Do not claim verified on Ubuntu until it has compiled and passed EngineChecks on the target machine.
