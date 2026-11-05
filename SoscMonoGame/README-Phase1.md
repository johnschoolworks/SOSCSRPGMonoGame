# SoscMonoGame — Phase 1: World and Location

This extends the supplied MonoGame + Myra prototype without replacing its UI framework.

## Added
- `Core/Location.cs`: location metadata, links in four directions, monster/rest flags.
- `Core/World.cs`: five connected locations.
- `Core/GameSession.cs`: current location, directional movement, location-triggered encounters, healing on return home.
- `Game1.cs`: location display and North/South/East/West buttons.

## Map
```
                  [Forest] --east--> [Forest Clearing]
                     |
                  [Town Square] --east--> [Old Bridge]
                     |
                   [Home]
```
Connections are bidirectional. Start at Home; move North to Town Square, then North to Forest to fight a spider. Win to leave; return Home to heal.

## Build (native .NET 10, Linux)
```bash
cd SoscMonoGame
env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT SDL_VIDEODRIVER=x11 /usr/bin/dotnet run
```

## Scope
This is Phase 1 only. Potion inventory, item drops, quests, traders, save/load and level-up rules are not implemented. The existing potion action remains unlimited. Player death currently ends progression until a future restart/respawn feature is added.
