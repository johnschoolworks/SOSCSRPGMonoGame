# Phase 6 — Advanced SOSCSRPG Mechanics

Built on Phase 5, targeting **.NET 10**, MonoGame DesktopGL and Myra.

## Features
- Two sequential quests: **Silk for the Village** (3 Spider Silk), then **The Wolf Threat** (2 Wolf Fang). Quest rewards are granted only once.
- Forest spawns Venomous Spider; Forest Clearing spawns Forest Wolf. Location encounter definitions live in `WorldFactory` / `Location`.
- `LootDrop` and `LootFactory` roll independent per-item drop chances. Spider Silk and Wolf Fang have guaranteed drops; potions have 15% and 20% chances respectively.
- `Player.GainExperience` implements cumulative XP thresholds: level 2 at 25 XP, level 3 at 50 XP; +5 maximum HP and full healing per level.
- UI displays active quest and next XP threshold. Phase 4 merchant and inventory remain.

## Run (Ubuntu)
```sh
env -u DOTNET_ROOT /usr/bin/dotnet restore SoscMonoGame/SoscMonoGame.csproj
env -u DOTNET_ROOT SDL_VIDEODRIVER=x11 /usr/bin/dotnet run --project SoscMonoGame/SoscMonoGame.csproj
env -u DOTNET_ROOT /usr/bin/dotnet run --project SoscMonoGame.EngineChecks/SoscMonoGame.EngineChecks.csproj
```

## Scope
Phase 6 does **not** add Save/Load, persistent quests, or a generic event bus (planned Phase 7). The engine uses an injected `Random` for deterministic tests.
