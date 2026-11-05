# SoscMonoGame — Phase 5: Engine Refactor

This phase reorganizes the Phase 4 prototype into a headless RPG engine and a MonoGame/Myra presentation layer. It preserves the existing gameplay loop and adds isolated factories and action classes.

## Structure

```text
SoscMonoGame/
  Engine/
    Models/        Player, Monster, Item, Inventory, Quest, Location, World, Trader
    Factories/     ItemFactory, ItemCatalog, MonsterFactory, QuestFactory, WorldFactory
    Actions/       AttackWithWeapon, Heal
    GameSession.cs  RPG rules and orchestration
  Game1.cs         MonoGame and Myra presentation only
  Program.cs
SoscMonoGame.EngineChecks/
  Program.cs       Headless regression checks (no graphics or NuGet packages)
```

The namespace `SoscMonoGame.Engine` is intentionally shared across engine files in this incremental phase, avoiding breaking public types while reorganizing source files. ItemCatalog now delegates to ItemFactory. MonsterFactory creates new monster instances. The combat and healing operations have been extracted from GameSession into Actions.

**Scope:** Phase 5 is a structural refactor, not the full SOSCSRPG feature set. Player leveling, save/load, multi-quest support, loot tables, and events are deferred to subsequent phases. In particular, the current UI still refreshes explicitly after user actions.

## Run on Ubuntu 26.04

```bash
env -u DOTNET_ROOT /usr/bin/dotnet restore SoscMonoGame/SoscMonoGame.csproj
env -u DOTNET_ROOT SDL_VIDEODRIVER=x11 /usr/bin/dotnet run --project SoscMonoGame/SoscMonoGame.csproj
```

## Run headless regression checks

```bash
env -u DOTNET_ROOT /usr/bin/dotnet run --project SoscMonoGame.EngineChecks/SoscMonoGame.EngineChecks.csproj
```

The source and ZIP were inspected in the packaging environment, but the .NET 10 SDK is not installed here. Compile and runtime validation must be performed on your Linux host.
