# SOSCSRPG C# Tutorial 010 — Game State, Locations, and Navigation

**Series:** SOSCSRPG C# Tutorials  
**Platform:** Ubuntu 26.04 · .NET 10 · Visual Studio Code  
**Prerequisite:** Tutorial 009 — JSON Save/Load and Game State Persistence  
**Language:** English  
**Project:** `Tutorial010`, a complete standalone console application

> An original educational adaptation inspired by Scott Lilly's SOSCSRPG. The console engine is deliberately separate from the MonoGame + Myra port.

## 1. Learning objectives

In this chapter you will:

1. Model locations as immutable definitions with unique IDs.
2. Build a connected world using a graph of adjacent locations.
3. Implement safe travel with validation.
4. Create fresh monster encounters when entering dangerous locations.
5. Publish `LocationChanged` events for future UI integration.
6. Extend Tutorial 009's JSON save/load to preserve the current location.
7. Restore quests and inventory alongside world state.
8. Test invalid navigation, encounter isolation, and corrupted save locations.

## 2. Architecture

```text
WorldFactory (static location definitions)
  ├── Town Square (1) <--> Forest Trail (2)
  │                          ├── Spider Clearing (3)
  │                          └── Wolf Den (4)
  |
GameWorld (mutable navigation state)
  ├── CurrentLocationId
  ├── CurrentEncounter
  ├── TravelTo(destinationId)
  └── LocationChanged event

MonsterFactory -> new Monster for each visit
SaveGameService -> JSON { Player, Inventory, Quests, CurrentLocationId }
```

A **location** is a reusable immutable definition. A **GameWorld** represents one running adventure, including where the player currently stands. A **Monster** is a mutable encounter, created fresh on entry.

### The world graph

```text
           Town Square
                |
           Forest Trail
            /       \
 Spider Clearing   Wolf Den
```

The graph is **bidirectional** here because every destination lists the route back. The engine does not assume all connections are symmetric; the factory defines the allowed moves.

## 3. Set up the project

In the VS Code terminal:

```bash
cat /etc/os-release | grep PRETTY_NAME
env -u DOTNET_ROOT /usr/bin/dotnet --version
code --version | head -n 1
```

Install the C# extension if needed:

```bash
code --install-extension ms-dotnettools.csharp
```

Create the standalone project:

```bash
mkdir -p ~/Projects/SOSCSRPG-Tutorials
cd ~/Projects/SOSCSRPG-Tutorials

env -u DOTNET_ROOT /usr/bin/dotnet new console \
  --name Tutorial010 --framework net10.0

cd Tutorial010
mkdir -p Models Factories Persistence World
code .
```

You can instead extract the accompanying ZIP, which already includes all source files.

## 4. Source tree

```text
Tutorial010/
├── Factories/
│   ├── ItemFactory.cs
│   └── QuestFactory.cs
├── Models/
│   ├── GameItem.cs
│   ├── Inventory.cs
│   ├── Player.cs
│   └── Quest.cs
├── Persistence/
│   ├── SaveData.cs
│   └── SaveGameService.cs
├── World/
│   ├── Location.cs
│   ├── MonsterFactory.cs
│   ├── WorldFactory.cs
│   └── GameWorld.cs
├── Program.cs
└── Tutorial010.csproj
```

## 5. Complete source code

This is a complete chapter, including the Tutorial 009 persistence classes updated for world locations. Copy each block into its matching path.

### 5.1 `Tutorial010.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

### 5.2 `Models/GameItem.cs`

```csharp
namespace Tutorial010.Models;

public sealed record GameItem(int Id, string Name);
```

### 5.3 `Factories/ItemFactory.cs`

```csharp
using Tutorial010.Models;

namespace Tutorial010.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public static GameItem SpiderSilk { get; } = new(SpiderSilkId, "Spider Silk");
}
```

### 5.4 `Models/Inventory.cs`

```csharp
namespace Tutorial010.Models;

public sealed class Inventory
{
    private readonly Dictionary<int, int> _items = new();

    public event EventHandler<InventoryChangedEventArgs>? InventoryChanged;

    public int Count(int itemId) => _items.GetValueOrDefault(itemId);

    public IReadOnlyList<(int ItemId, int Quantity)> GetEntries() =>
        _items.Select(e => (ItemId: e.Key, Quantity: e.Value))
            .OrderBy(e => e.ItemId).ToList().AsReadOnly();

    public void Add(GameItem item, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Id <= 0) throw new ArgumentOutOfRangeException(nameof(item));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));

        int updated = checked(Count(item.Id) + quantity);
        _items[item.Id] = updated;
        OnInventoryChanged(item.Id, updated);
    }

    public bool Remove(int itemId, int quantity = 1)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        int owned = Count(itemId);
        if (owned < quantity) return false;

        int remaining = owned - quantity;
        if (remaining == 0) _items.Remove(itemId);
        else _items[itemId] = remaining;

        OnInventoryChanged(itemId, remaining);
        return true;
    }

    private void OnInventoryChanged(int itemId, int newQuantity) =>
        InventoryChanged?.Invoke(this, new InventoryChangedEventArgs(itemId, newQuantity));
}

public sealed class InventoryChangedEventArgs : EventArgs
{
    public int ItemId { get; }
    public int NewQuantity { get; }

    public InventoryChangedEventArgs(int itemId, int newQuantity)
    {
        ItemId = itemId;
        NewQuantity = newQuantity;
    }
}
```

### 5.5 `Models/Player.cs`

```csharp
namespace Tutorial010.Models;

public sealed class Player
{
    public string Name { get; }
    public Inventory Inventory { get; } = new();
    public int Gold { get; private set; }
    public int ExperiencePoints { get; private set; }

    public event EventHandler<PlayerStatsChangedEventArgs>? StatsChanged;

    public Player(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name;
    }

    public bool CanReceiveRewards(int gold, int experience)
    {
        if (gold < 0) throw new ArgumentOutOfRangeException(nameof(gold));
        if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
        return (long)Gold + gold <= int.MaxValue &&
               (long)ExperiencePoints + experience <= int.MaxValue;
    }

    // Called while reconstructing a saved game, before UI subscriptions.
    public void RestoreRewards(int gold, int experience)
    {
        if (gold < 0) throw new ArgumentOutOfRangeException(nameof(gold));
        if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
        Gold = gold;
        ExperiencePoints = experience;
    }

    public void ReceiveRewards(int gold, int experience)
    {
        if (!CanReceiveRewards(gold, experience))
            throw new OverflowException("Player reward limit exceeded.");

        if (gold == 0 && experience == 0) return;

        Gold += gold;
        ExperiencePoints += experience;
        StatsChanged?.Invoke(this, new PlayerStatsChangedEventArgs(Gold, ExperiencePoints));
    }
}

public sealed class PlayerStatsChangedEventArgs : EventArgs
{
    public int Gold { get; }
    public int ExperiencePoints { get; }

    public PlayerStatsChangedEventArgs(int gold, int experience)
    {
        Gold = gold;
        ExperiencePoints = experience;
    }
}
```

### 5.6 `Models/Quest.cs`

```csharp
namespace Tutorial010.Models;

public enum QuestStatus { NotStarted, Active, Completed }

public sealed class Quest
{
    public int Id { get; }
    public string Title { get; }
    public int RequiredItemId { get; }
    public int RequiredQuantity { get; }
    public int RewardGold { get; }
    public int RewardExperience { get; }
    public QuestStatus Status { get; private set; }

    public event EventHandler<QuestStatusChangedEventArgs>? StatusChanged;

    public Quest(int id, string title, int requiredItemId, int requiredQuantity,
                 int rewardGold, int rewardExperience)
    {
        if (id <= 0 || requiredItemId <= 0 || requiredQuantity <= 0 ||
            rewardGold < 0 || rewardExperience < 0 ||
            string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Invalid quest definition.");

        Id = id;
        Title = title;
        RequiredItemId = requiredItemId;
        RequiredQuantity = requiredQuantity;
        RewardGold = rewardGold;
        RewardExperience = rewardExperience;
    }

    public void Accept()
    {
        if (Status != QuestStatus.NotStarted)
            throw new InvalidOperationException("Quest cannot be accepted again.");

        SetStatus(QuestStatus.Active);
    }

    public int Progress(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return Math.Min(RequiredQuantity, player.Inventory.Count(RequiredItemId));
    }

    public bool TurnIn(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (Status != QuestStatus.Active ||
            player.Inventory.Count(RequiredItemId) < RequiredQuantity ||
            !player.CanReceiveRewards(RewardGold, RewardExperience))
            return false;

        if (!player.Inventory.Remove(RequiredItemId, RequiredQuantity))
            return false;

        player.ReceiveRewards(RewardGold, RewardExperience);
        SetStatus(QuestStatus.Completed);
        return true;
    }

    // Restoration is an initialization operation, not a gameplay transition.
    // It deliberately does not raise StatusChanged.
    public void RestoreStatus(QuestStatus status)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
    }

    private void SetStatus(QuestStatus next)
    {
        QuestStatus previous = Status;
        Status = next;
        StatusChanged?.Invoke(this,
            new QuestStatusChangedEventArgs(previous, next));
    }
}

public sealed class QuestStatusChangedEventArgs : EventArgs
{
    public QuestStatus Previous { get; }
    public QuestStatus Current { get; }

    public QuestStatusChangedEventArgs(QuestStatus previous, QuestStatus current)
    {
        Previous = previous;
        Current = current;
    }
}
```

### 5.7 `Factories/QuestFactory.cs`

```csharp
using Tutorial010.Models;

namespace Tutorial010.Factories;

public static class QuestFactory
{
    public static Quest CreateSpiderSilkQuest() =>
        new(id: 1, title: "Collect Spider Silk",
            requiredItemId: ItemFactory.SpiderSilkId,
            requiredQuantity: 3, rewardGold: 10, rewardExperience: 20);
}
```

### 5.8 `World/Location.cs`

```csharp
namespace Tutorial010.World;

public sealed record Location(int Id, string Name, string Description,
    IReadOnlyList<int> NeighborIds, int? MonsterId = null);
```

### 5.9 `World/MonsterFactory.cs`

```csharp
namespace Tutorial010.World;

public sealed class Monster
{
    public int Id { get; }
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }
    public bool IsAlive => HitPoints > 0;

    public Monster(int id, string name, int hitPoints)
    {
        Id = id;
        Name = name;
        if (hitPoints <= 0) throw new ArgumentOutOfRangeException(nameof(hitPoints));
        MaximumHitPoints = hitPoints;
        HitPoints = hitPoints;
    }

    public void TakeDamage(int damage)
    {
        if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
        HitPoints = (int)Math.Max(0L, (long)HitPoints - damage);
    }
}

public static class MonsterFactory
{
    public const int SpiderId = 1;
    public const int WolfId = 2;

    public static Monster Create(int id) => id switch
    {
        SpiderId => new Monster(id, "Venomous Spider", 10),
        WolfId => new Monster(id, "Forest Wolf", 18),
        _ => throw new KeyNotFoundException($"Unknown monster: {id}")
    };
}
```

### 5.10 `World/WorldFactory.cs`

```csharp
namespace Tutorial010.World;

public static class WorldFactory
{
    public const int TownSquareId = 1;
    public const int ForestTrailId = 2;
    public const int SpiderClearingId = 3;
    public const int WolfDenId = 4;

    private static readonly Dictionary<int, Location> Locations = new()
    {
        [TownSquareId] = new(TownSquareId, "Town Square",
            "A peaceful place to begin your journey.", new[] { ForestTrailId }),
        [ForestTrailId] = new(ForestTrailId, "Forest Trail",
            "The road splits among ancient trees.",
            new[] { TownSquareId, SpiderClearingId, WolfDenId }),
        [SpiderClearingId] = new(SpiderClearingId, "Spider Clearing",
            "Silvery webs cover the branches.", new[] { ForestTrailId }, MonsterFactory.SpiderId),
        [WolfDenId] = new(WolfDenId, "Wolf Den",
            "Tracks disappear into a shadowy hollow.", new[] { ForestTrailId }, MonsterFactory.WolfId)
    };

    public static Location Get(int id) =>
        Locations.TryGetValue(id, out var location)
            ? location : throw new KeyNotFoundException($"Unknown location: {id}");

    public static IReadOnlyList<Location> All() =>
        Locations.Values.OrderBy(l => l.Id).ToList().AsReadOnly();
}
```

### 5.11 `World/GameWorld.cs`

```csharp
namespace Tutorial010.World;

public sealed class GameWorld
{
    public int CurrentLocationId { get; private set; }
    public Location CurrentLocation => WorldFactory.Get(CurrentLocationId);
    public Monster? CurrentEncounter { get; private set; }

    public event EventHandler<LocationChangedEventArgs>? LocationChanged;

    public GameWorld(int startLocationId = WorldFactory.TownSquareId)
    {
        _ = WorldFactory.Get(startLocationId);
        CurrentLocationId = startLocationId;
    }

    public IReadOnlyList<Location> AvailableDestinations() =>
        CurrentLocation.NeighborIds.Select(WorldFactory.Get).ToList().AsReadOnly();

    public bool TravelTo(int destinationId)
    {
        if (!CurrentLocation.NeighborIds.Contains(destinationId))
            return false;

        Location previous = CurrentLocation;
        CurrentLocationId = destinationId;
        CurrentEncounter = CurrentLocation.MonsterId is int monsterId
            ? MonsterFactory.Create(monsterId) : null;
        LocationChanged?.Invoke(this,
            new LocationChangedEventArgs(previous, CurrentLocation));
        return true;
    }
}

public sealed class LocationChangedEventArgs : EventArgs
{
    public Location Previous { get; }
    public Location Current { get; }
    public LocationChangedEventArgs(Location previous, Location current)
    {
        Previous = previous;
        Current = current;
    }
}
```

### 5.12 `Persistence/SaveData.cs`

```csharp
namespace Tutorial010.Persistence;

public sealed class SaveData
{
    public int Version { get; set; } = 2;
    public int CurrentLocationId { get; set; } = 1;
    public string PlayerName { get; set; } = "";
    public int Gold { get; set; }
    public int ExperiencePoints { get; set; }
    public List<InventoryEntryData> Inventory { get; set; } = new();
    public List<QuestEntryData> Quests { get; set; } = new();
}

public sealed class InventoryEntryData
{
    public int ItemId { get; set; }
    public int Quantity { get; set; }
}

public sealed class QuestEntryData
{
    public int QuestId { get; set; }
    public string Status { get; set; } = "";
}
```

### 5.13 `Persistence/SaveGameService.cs`

```csharp
using System.Text.Json;
using Tutorial010.Factories;
using Tutorial010.Models;
using Tutorial010.World;

namespace Tutorial010.Persistence;

public sealed class SaveGameService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false
    };

    public SaveData Capture(Player player, IEnumerable<Quest> quests, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(world);
        return new SaveData
        {
            PlayerName = player.Name,
            CurrentLocationId = world.CurrentLocationId,
            Gold = player.Gold,
            ExperiencePoints = player.ExperiencePoints,
            Inventory = player.Inventory.GetEntries()
                .Select(e => new InventoryEntryData { ItemId = e.ItemId, Quantity = e.Quantity })
                .ToList(),
            Quests = quests.Select(q => new QuestEntryData
            {
                QuestId = q.Id,
                Status = q.Status.ToString()
            }).ToList()
        };
    }

    public string Serialize(SaveData save) => JsonSerializer.Serialize(save, Options);

    public SaveData Deserialize(string json) =>
        JsonSerializer.Deserialize<SaveData>(json, Options)
        ?? throw new InvalidDataException("Save file contains JSON null.");

    public void Save(string path, Player player, IEnumerable<Quest> quests, GameWorld world)
    {
        string json = Serialize(Capture(player, quests, world));
        // Temporary file plus rename avoids leaving a truncated target file.
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, fullPath, overwrite: true);
    }

    public (Player Player, List<Quest> Quests, GameWorld World) Load(string path) =>
        Restore(Deserialize(File.ReadAllText(path)));

    public (Player Player, List<Quest> Quests, GameWorld World) Restore(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Version != 2)
            throw new InvalidDataException($"Unsupported save version: {data.Version}");
        if (string.IsNullOrWhiteSpace(data.PlayerName))
            throw new InvalidDataException("Missing player name.");
        if (data.Gold < 0 || data.ExperiencePoints < 0)
            throw new InvalidDataException("Negative player rewards.");
        if (data.Inventory is null || data.Quests is null)
            throw new InvalidDataException("Missing inventory or quest list.");

        // A save stores location identity, not mutable encounter objects.
        try { _ = WorldFactory.Get(data.CurrentLocationId); }
        catch (KeyNotFoundException ex)
        {
            throw new InvalidDataException("Unknown saved location.", ex);
        }

        // Validate everything before publishing the restored game state.
        var inventoryIds = new HashSet<int>();
        foreach (var entry in data.Inventory)
        {
            if (entry is null || entry.Quantity <= 0 || !inventoryIds.Add(entry.ItemId))
                throw new InvalidDataException("Invalid or duplicate inventory entry.");
            _ = ResolveItem(entry.ItemId);
        }
        var questIds = new HashSet<int>();
        var parsedQuests = new List<(Quest Quest, QuestStatus Status)>();
        foreach (var entry in data.Quests)
        {
            if (entry is null || !questIds.Add(entry.QuestId) ||
                !Enum.TryParse<QuestStatus>(entry.Status, ignoreCase: false, out var status) ||
                !Enum.IsDefined(status))
                throw new InvalidDataException("Invalid or duplicate quest entry.");
            Quest quest = entry.QuestId switch
            {
                1 => QuestFactory.CreateSpiderSilkQuest(),
                _ => throw new InvalidDataException($"Unknown quest ID: {entry.QuestId}")
            };
            parsedQuests.Add((quest, status));
        }

        var player = new Player(data.PlayerName);
        player.RestoreRewards(data.Gold, data.ExperiencePoints);
        foreach (var entry in data.Inventory)
            player.Inventory.Add(ResolveItem(entry.ItemId), entry.Quantity);
        foreach (var (quest, status) in parsedQuests)
            quest.RestoreStatus(status);
        return (player, parsedQuests.Select(x => x.Quest).ToList(),
            new GameWorld(data.CurrentLocationId));
    }

    private static GameItem ResolveItem(int id) => id switch
    {
        ItemFactory.SpiderSilkId => ItemFactory.SpiderSilk,
        _ => throw new InvalidDataException($"Unknown item ID: {id}")
    };
}
```

### 5.14 `Program.cs`

```csharp
using Tutorial010.Factories;
using Tutorial010.Models;
using Tutorial010.Persistence;
using Tutorial010.World;

Console.WriteLine("=== SOSCSRPG Tutorial 010 ===");
Console.WriteLine("Locations, Navigation, and Encounters");

var player = new Player("Adventurer");
var quest = QuestFactory.CreateSpiderSilkQuest();
quest.Accept();
var world = new GameWorld();
world.LocationChanged += (_, e) =>
    Console.WriteLine($"[World] {e.Previous.Name} -> {e.Current.Name}");

PrintLocation(world);
Check(!world.TravelTo(WorldFactory.SpiderClearingId),
    "Cannot skip an unconnected location");
Check(world.TravelTo(WorldFactory.ForestTrailId), "Town -> Forest Trail");
PrintLocation(world);
Check(world.TravelTo(WorldFactory.SpiderClearingId), "Trail -> Spider Clearing");
PrintLocation(world);
Check(world.CurrentEncounter is not null, "Spider encounter generated");
Check(world.CurrentEncounter!.Name == "Venomous Spider", "Correct encounter");

var firstSpider = world.CurrentEncounter;
firstSpider.TakeDamage(4);
Check(firstSpider.HitPoints == 6, "Encounter takes damage");
Check(world.TravelTo(WorldFactory.ForestTrailId), "Return to trail");
Check(world.TravelTo(WorldFactory.SpiderClearingId), "Re-enter clearing");
Check(world.CurrentEncounter is not null && world.CurrentEncounter.HitPoints == 10,
    "New visit creates a fresh encounter");

player.Inventory.Add(ItemFactory.SpiderSilk, 3);
var saveService = new SaveGameService();
string path = Path.Combine(Environment.CurrentDirectory, "savegame.json");
saveService.Save(path, player, new[] { quest }, world);
Console.WriteLine($"Saved game to {path}");

var (loadedPlayer, loadedQuests, loadedWorld) = saveService.Load(path);
Check(loadedWorld.CurrentLocationId == WorldFactory.SpiderClearingId,
    "Location restored from JSON");
Check(loadedWorld.CurrentEncounter is null,
    "Transient encounter is not restored");
Check(loadedPlayer.Inventory.Count(ItemFactory.SpiderSilkId) == 3,
    "Inventory restored");
Check(loadedQuests.Single().Status == QuestStatus.Active,
    "Quest status restored");

Check(loadedWorld.TravelTo(WorldFactory.ForestTrailId),
    "Restored world can navigate");
Check(!loadedWorld.TravelTo(999), "Invalid destination rejected");

try
{
    saveService.Restore(new SaveData
    {
        PlayerName = "Invalid",
        CurrentLocationId = 999
    });
    throw new Exception("FAIL: invalid location accepted");
}
catch (InvalidDataException)
{
    Console.WriteLine("PASS: Invalid saved location rejected");
}

Console.WriteLine("ALL WORLD CHECKS PASSED");

static void PrintLocation(GameWorld world)
{
    Console.WriteLine($"\nLocation: {world.CurrentLocation.Name}");
    Console.WriteLine(world.CurrentLocation.Description);
    Console.WriteLine("Travel options: " +
        string.Join(", ", world.AvailableDestinations().Select(x => x.Name)));
    if (world.CurrentEncounter is Monster monster)
        Console.WriteLine($"Encounter: {monster.Name} ({monster.HitPoints} HP)");
}

static void Check(bool result, string description)
{
    if (!result) throw new InvalidOperationException($"FAIL: {description}");
    Console.WriteLine($"PASS: {description}");
}
```

## 6. Build and run

From the VS Code integrated terminal:

```bash
cd ~/Projects/SOSCSRPG-Tutorials/Tutorial010

env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run --no-build
```

Optional stricter check:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet build -warnaserror
```

The demo walks from Town Square to Forest Trail and Spider Clearing, tests blocked moves, re-enters the clearing to generate a new spider, saves the game, and reloads the location.

Expected verification lines include:

```text
PASS: Cannot skip an unconnected location
PASS: Town -> Forest Trail
PASS: Trail -> Spider Clearing
PASS: Spider encounter generated
PASS: New visit creates a fresh encounter
PASS: Location restored from JSON
PASS: Transient encounter is not restored
PASS: Inventory restored
PASS: Quest status restored
PASS: Invalid destination rejected
PASS: Invalid saved location rejected
ALL WORLD CHECKS PASSED
```

The application also creates `savegame.json` in its current working directory.

## 7. How navigation works

`GameWorld.TravelTo()` checks whether the destination is in `CurrentLocation.NeighborIds`.

```csharp
if (!CurrentLocation.NeighborIds.Contains(destinationId))
    return false;
```

If the destination is not connected, the method returns `false` and leaves world state unchanged. Otherwise it updates `CurrentLocationId`, creates an encounter if the destination has a monster ID, and raises `LocationChanged`.

**Design note:** This tutorial allows leaving a location while a monster is alive. Combat locks and escape rules will be added later.

## 8. Why encounters are not saved yet

The save file records `CurrentLocationId`, not `CurrentEncounter`. After loading, the player remains in the same location but no active monster is restored.

This is a deliberate simplification, **not** a complete combat save system. To support saving during combat, we will eventually persist encounter identity, current HP, status effects, and reward-claim state.

Also, revisiting a dangerous location currently creates a fresh monster. Persistent cleared locations would require additional world-state data.

## 9. Save version 2

Tutorial 009 used save format version 1. Tutorial 010 adds:

```json
{
  "Version": 2,
  "CurrentLocationId": 3
}
```

The complete save contains the existing player, inventory, and quest fields as well. The updated service explicitly accepts **version 2 only**; it does not automatically migrate version 1 files. A production engine should add migration logic if older saves need to remain usable.

The loader validates that the location ID exists before constructing the restored game state.

## 10. Events and the future Myra UI

`GameWorld` raises a strongly typed event:

```csharp
public event EventHandler<LocationChangedEventArgs>? LocationChanged;
```

A future UI presenter can subscribe:

```csharp
world.LocationChanged += (_, args) =>
{
    Console.WriteLine($"Moved to {args.Current.Name}");
};
```

In MonoGame + Myra, the handler would update the location label, description, navigation buttons, and encounter panel instead of printing to the console.

As with Tutorial 008, synchronous event handlers should remain lightweight. A throwing subscriber can interrupt the caller after state has changed.

## 11. Debugging in VS Code

1. Open the `Tutorial010` project folder.
2. Set a breakpoint in `GameWorld.TravelTo()`.
3. Press **F5** to start debugging.
4. Inspect `CurrentLocation.NeighborIds` and `destinationId`.
5. Step into `MonsterFactory.Create()` when entering Spider Clearing.
6. Set a breakpoint in `SaveGameService.Restore()` and inspect `CurrentLocationId`.
7. Verify that invalid location ID `999` throws `InvalidDataException`.

| Shortcut | Action |
|---|---|
| `F5` | Start / continue |
| `F9` | Toggle breakpoint |
| `F10` | Step over |
| `F11` | Step into |
| `Shift+F5` | Stop |

## 12. C# versus C++

| Concept | C# | C++ |
|---|---|---|
| Immutable location data | `record Location(...)` | `struct`/class with const accessors |
| Graph adjacency | `IReadOnlyList<int>` | `std::vector<int>` / `std::span` |
| Location lookup | `Dictionary<int, Location>` | `std::unordered_map<int, Location>` |
| Navigation events | `EventHandler<T>` | Observer pattern / signals |
| Optional encounter | `Monster?` | `std::optional` or smart pointer |
| JSON serialization | `System.Text.Json` | Third-party JSON library |
| State isolation | Separate `GameWorld` instances | Separate world objects |

## 13. Exercises

- [ ] **A — New location:** Add `Old Bridge` connected to Forest Trail.
- [ ] **B — One-way route:** Make a path from Old Bridge to Wolf Den without a return connection.
- [ ] **C — Encounter lock:** Prevent traveling while `CurrentEncounter.IsAlive`.
- [ ] **D — Fight command:** Add a console command to attack the current monster.
- [ ] **E — Save migration:** Add a migration path from Tutorial 009's version 1 save file to version 2, defaulting to Town Square.
- [ ] **F — Navigation menu:** Print numbered destination choices and read input with `Console.ReadLine()`.
- [ ] **G — Cleared areas:** Track cleared monster locations and prevent automatic respawning.
- [ ] **H — Event presenter:** Create a `WorldPresenter : IDisposable` that subscribes to `LocationChanged` and unsubscribes when disposed.

### Engineering challenge

Should a game store a whole `Location` object in its save file, or only a location ID? Consider what happens when the game is patched and a location description changes. IDs let the loader resolve the current content definition, but removing or renumbering IDs requires migration.

## 14. GitHub workflow

```bash
cat > .gitignore <<'EOF'
bin/
obj/
.vs/
savegame.json
savegame.json.tmp
EOF

git init
git add Program.cs Models Factories Persistence World Tutorial010.csproj .gitignore
git commit -m "Tutorial 010: world locations navigation and save state"
```

## 15. Knowledge check

1. What is a graph edge in the location system?
2. Why does `TravelTo()` validate adjacency?
3. Why should each monster encounter be a fresh instance?
4. What state is intentionally not restored from JSON?
5. Why was the save format version incremented?
6. What is the difference between immutable location definitions and mutable world state?
7. How would a Myra navigation panel use `LocationChanged`?
8. What changes are needed to support combat locks and persistent cleared locations?

## 16. Chapter summary

You now have a navigable world layered on top of the earlier SOSCSRPG systems:

```text
Player + Inventory + Quests
             |
             v
        GameWorld
          |    |
          |    +--> Monster encounters
          v
   WorldFactory locations
             |
             v
     SaveGameService
             |
             v
         JSON v2
```

**Implemented:** location catalog, navigation graph, travel validation, encounter spawning, location events, versioned location persistence, and engine checks.

**Not yet implemented:** a playable combat loop, travel restrictions during combat, persistent monster-cleared state, NPC dialogue, or Myra widgets.

### Next: Tutorial 011 — Turn-Based Combat and Encounter State

We'll connect the player, weapons, monsters, and navigation into an interactive turn-based combat loop, including rewards and safeguards against claiming loot twice.
