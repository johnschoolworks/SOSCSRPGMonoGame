# SoscMonoGame — Phase 3: Quests and World Factory

Built on Phase 2, targeting .NET 10 with MonoGame DesktopGL and Myra.

## New systems

- `Core/Quest.cs`: immutable quest definition and per-session completion state.
- `Core/QuestFactory.cs`: defines the village silk collection quest.
- `Core/WorldFactory.cs`: constructs the map and links locations; `World` now delegates map creation.
- `GameSession.TurnInQuest()`: validates location and inventory, consumes required items, grants rewards once.
- `Game1.cs`: quest progress display and turn-in button.

## Quest: Silk for the Village

1. Start at Home. Travel **North** to Town Square, then **North** to Forest.
2. Defeat a spider; receive **1 Spider Silk**.
3. Travel **East** to Forest Clearing for another spider; return and re-enter a monster location to collect 3 silk total.
4. Return to **Town Square**, click **Turn In Quest**.
5. Receive **20 Gold, 25 XP and 1 Iron Sword**. Click **Equip Iron**.
6. The quest can be completed only once. The 3 silk are consumed.

## Linux

```bash
cd SoscMonoGame
env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT SDL_VIDEODRIVER=x11 /usr/bin/dotnet run
```

## Scope

This phase provides one demonstrable quest, not a full Scott Lilly course-equivalent quest engine. Future phases can add multiple quests, prerequisites, persistent save data, and trader interactions.
