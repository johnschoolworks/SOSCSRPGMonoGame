# SOSCSRPG C# Tutorial 012 — Interactive Game Loop, Commands, and Player Actions

**Environment:** Ubuntu 26.04 · .NET 10 · Visual Studio Code  
**Prerequisite:** Tutorial 011 — Turn-Based Combat and Encounter State  
**Project:** `Tutorial012` · Standalone console RPG  
**Language:** English

> Original C# learning project inspired by Scott Lilly's SOSCSRPG. This is a terminal implementation, independent of the separate MonoGame + Myra port.

## 1. Learning objectives

This chapter transforms the automated combat simulation into a **playable terminal adventure**. You will:

1. Design a command parser with `enum` and `record`.
2. Build an interactive `Console.ReadLine()` game loop.
3. Separate console interaction from the domain engine.
4. Travel between connected locations and fight monsters turn by turn.
5. Display character status, inventory, and quests.
6. Accept and turn in a Spider Silk quest.
7. Save and load JSON v3, including mid-combat encounters.
8. Handle EOF, unknown commands, invalid arguments, and corrupted save files.
9. Run deterministic noninteractive self-tests for the command loop.

## 2. Quick start

Extract the accompanying source ZIP. From the VS Code integrated terminal:

```bash
cd Tutorial012
env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run
```

If you're starting from scratch instead:

```bash
mkdir -p ~/Projects/SOSCSRPG-Tutorials
cd ~/Projects/SOSCSRPG-Tutorials
env -u DOTNET_ROOT /usr/bin/dotnet new console --name Tutorial012 --framework net10.0
cd Tutorial012
mkdir -p Models Factories World Combat Persistence ConsoleUI
code .
```

Install the VS Code C# extension if required:

```bash
code --install-extension ms-dotnettools.csharp
```

## 3. Game commands

| Command | Purpose |
|---|---|
| `help` | Show command list |
| `look` | Describe location, exits, and enemy |
| `map` | Show world locations and connections |
| `go 2` | Travel to location ID 2 |
| `attack` | Attack current monster; it counterattacks if alive |
| `status` | Show HP, Gold, XP, and weapon |
| `inventory` | List inventory item IDs and quantities |
| `quests` | Display quest status and progress |
| `accept` | Accept the Spider Silk quest |
| `turnin` | Turn in three Spider Silk for quest rewards |
| `save` | Write `savegame.json` |
| `load` | Restore the latest saved game |
| `quit` | Exit the game |

Aliases include `h`, `?`, `l`, `travel`, `a`, `stats`, `inv`, `quest`, and `exit`.

### Suggested playthrough

```text
help
accept
map
go 2
go 3
look
attack
attack
attack
attack
status
inventory
save
go 2
go 3
```

You may need additional attacks depending on random damage. Repeat the Spider Clearing encounter until you have three Spider Silk. Then use `turnin`. The game also supports saving and loading **during** combat.

## 4. Game-loop architecture

```text
Console.ReadLine()
       |
       v
CommandParser.Parse()
       |
       v
GameSession.Execute()
       |
       +--> GameWorld.TravelTo()
       +--> CombatService.Attack()
       +--> Player / Inventory / Quest
       +--> SaveGameService.Save() / Load()
       |
       v
TextWriter (Console.Out)
```

`GameSession` accepts `TextReader` and `TextWriter` for its game loop. This allows automated tests to feed commands without a person typing and to capture the resulting transcript.

The domain engine remains independent of `ConsoleUI` and can later be driven by Myra buttons instead of terminal commands. Tutorial 011's `CombatService` still prints detailed combat messages directly to `Console`; migrating these to typed combat events or result objects is an intentional future improvement.

## 5. Complete source code

The ZIP includes every file needed for this standalone tutorial. Below is the full source, including the domain classes carried forward from Tutorial 011.

### 5.1 `Tutorial012.csproj`

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
namespace Tutorial012.Models;

public sealed record GameItem(int Id, string Name);
```

### 5.3 `Models/Weapon.cs`

```csharp
namespace Tutorial012.Models;

public sealed record Weapon(int Id, string Name, int MinimumDamage, int MaximumDamage)
{
    public int RollDamage(Random random) => random.Next(MinimumDamage, MaximumDamage + 1);
}
```

### 5.4 `Models/Inventory.cs`

```csharp
namespace Tutorial012.Models;

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
namespace Tutorial012.Models;

public sealed class Player
{
    public string Name { get; }
    public Inventory Inventory { get; } = new();
    public int MaximumHitPoints { get; } = 20;
    public int HitPoints { get; private set; } = 20;
    public bool IsAlive => HitPoints > 0;
    public Weapon EquippedWeapon { get; } = new(1, "Rusty Sword", 2, 5);
    public int Gold { get; private set; }
    public int ExperiencePoints { get; private set; }

    public event EventHandler<PlayerStatsChangedEventArgs>? StatsChanged;

    public Player(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name;
    }

    public void TakeDamage(int damage)
    {
        if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
        HitPoints = (int)Math.Max(0L, (long)HitPoints - damage);
    }

    public void RestoreHitPoints(int hitPoints)
    {
        if (hitPoints < 0 || hitPoints > MaximumHitPoints)
            throw new ArgumentOutOfRangeException(nameof(hitPoints));
        HitPoints = hitPoints;
    }

    public bool Heal(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (!IsAlive || HitPoints == MaximumHitPoints) return false;
        HitPoints = (int)Math.Min((long)MaximumHitPoints, (long)HitPoints + amount);
        return true;
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
namespace Tutorial012.Models;

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

### 5.7 `Factories/ItemFactory.cs`

```csharp
using Tutorial012.Models;

namespace Tutorial012.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public static GameItem SpiderSilk { get; } = new(SpiderSilkId, "Spider Silk");
}
```

### 5.8 `Factories/QuestFactory.cs`

```csharp
using Tutorial012.Models;

namespace Tutorial012.Factories;

public static class QuestFactory
{
    public static Quest CreateSpiderSilkQuest() =>
        new(id: 1, title: "Collect Spider Silk",
            requiredItemId: ItemFactory.SpiderSilkId,
            requiredQuantity: 3, rewardGold: 10, rewardExperience: 20);
}
```

### 5.9 `World/Location.cs`

```csharp
namespace Tutorial012.World;

public sealed record Location(int Id, string Name, string Description,
    IReadOnlyList<int> NeighborIds, int? MonsterId = null);
```

### 5.10 `World/MonsterFactory.cs`

```csharp
namespace Tutorial012.World;

public sealed class Monster
{
    public int Id { get; }
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }
    public bool IsAlive => HitPoints > 0;
    public int MinimumDamage { get; }
    public int MaximumDamage { get; }
    public int RewardGold { get; }
    public int RewardExperience { get; }
    public int? LootItemId { get; }
    public bool RewardsClaimed { get; private set; }

    public void Restore(int hitPoints, bool claimed)
    {
        if (hitPoints < 0 || hitPoints > MaximumHitPoints || (claimed && hitPoints > 0))
            throw new InvalidDataException("Invalid saved monster state.");
        HitPoints = hitPoints;
        RewardsClaimed = claimed;
    }

    public bool ClaimRewards()
    {
        if (IsAlive || RewardsClaimed) return false;
        RewardsClaimed = true;
        return true;
    }

    public Monster(int id, string name, int hitPoints, int minimumDamage, int maximumDamage, int rewardGold, int rewardExperience, int? lootItemId)
    {
        Id = id;
        Name = name;
        if (hitPoints <= 0) throw new ArgumentOutOfRangeException(nameof(hitPoints));
        if (minimumDamage < 0 || maximumDamage < minimumDamage || rewardGold < 0 || rewardExperience < 0)
            throw new ArgumentException("Invalid monster stats.");
        MinimumDamage = minimumDamage;
        MaximumDamage = maximumDamage;
        RewardGold = rewardGold;
        RewardExperience = rewardExperience;
        LootItemId = lootItemId;
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
        SpiderId => new Monster(id, "Venomous Spider", 10, 1, 3, 5, 10, Factories.ItemFactory.SpiderSilkId),
        WolfId => new Monster(id, "Forest Wolf", 18, 2, 5, 9, 15, null),
        _ => throw new KeyNotFoundException($"Unknown monster: {id}")
    };
}
```

### 5.11 `World/WorldFactory.cs`

```csharp
namespace Tutorial012.World;

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

### 5.12 `World/GameWorld.cs`

```csharp
namespace Tutorial012.World;

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

    public void RestoreEncounter(Monster? encounter)
    {
        if (encounter is not null && CurrentLocation.MonsterId != encounter.Id)
            throw new InvalidDataException("Encounter does not match location.");
        CurrentEncounter = encounter;
    }

    public bool TravelTo(int destinationId)
    {
        if ((CurrentEncounter?.IsAlive ?? false) ||
            !CurrentLocation.NeighborIds.Contains(destinationId))
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

### 5.13 `Combat/CombatService.cs`

```csharp
using Tutorial012.Factories;
using Tutorial012.Models;
using Tutorial012.World;

namespace Tutorial012.Combat;

public enum CombatOutcome { NoEncounter, PlayerDead, PlayerAttacked, MonsterDefeated }

public sealed class CombatService
{
    private readonly Random _random;
    public CombatService(Random random) => _random = random ?? throw new ArgumentNullException(nameof(random));

    public CombatOutcome Attack(Player player, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(world);
        Monster? monster = world.CurrentEncounter;
        if (monster is null || !monster.IsAlive) return CombatOutcome.NoEncounter;
        if (!player.IsAlive) return CombatOutcome.PlayerDead;

        int damage = player.EquippedWeapon.RollDamage(_random);
        monster.TakeDamage(damage);
        Console.WriteLine($"You hit {monster.Name} for {damage}. Monster HP: {monster.HitPoints}");
        if (!monster.IsAlive)
        {
            // Preflight all reward additions before claiming the encounter.
            if (!player.CanReceiveRewards(monster.RewardGold, monster.RewardExperience))
                throw new OverflowException("Combat reward capacity exceeded.");
            if (monster.LootItemId is int itemId && player.Inventory.Count(itemId) == int.MaxValue)
                throw new OverflowException("Loot inventory capacity exceeded.");
            if (!monster.ClaimRewards()) return CombatOutcome.NoEncounter;
            if (monster.LootItemId is int lootId)
            {
                player.Inventory.Add(ItemFactory.SpiderSilk);
                Console.WriteLine("Loot: Spider Silk x1");
            }
            player.ReceiveRewards(monster.RewardGold, monster.RewardExperience);
            Console.WriteLine($"Victory! +{monster.RewardGold} Gold, +{monster.RewardExperience} XP");
            return CombatOutcome.MonsterDefeated;
        }

        int counterDamage = _random.Next(monster.MinimumDamage, monster.MaximumDamage + 1);
        player.TakeDamage(counterDamage);
        Console.WriteLine($"{monster.Name} hits you for {counterDamage}. HP: {player.HitPoints}/{player.MaximumHitPoints}");
        return player.IsAlive ? CombatOutcome.PlayerAttacked : CombatOutcome.PlayerDead;
    }
}
```

### 5.14 `Persistence/SaveData.cs`

```csharp
namespace Tutorial012.Persistence;

public sealed class SaveData
{
    public int Version { get; set; } = 3;
    public int HitPoints { get; set; } = 20;
    public int? EncounterMonsterId { get; set; }
    public int? EncounterHitPoints { get; set; }
    public bool EncounterRewardsClaimed { get; set; }
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

### 5.15 `Persistence/SaveGameService.cs`

```csharp
using System.Text.Json;
using Tutorial012.Factories;
using Tutorial012.Models;
using Tutorial012.World;

namespace Tutorial012.Persistence;

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
            HitPoints = player.HitPoints,
            EncounterMonsterId = world.CurrentEncounter?.Id,
            EncounterHitPoints = world.CurrentEncounter?.HitPoints,
            EncounterRewardsClaimed = world.CurrentEncounter?.RewardsClaimed,
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
        if (data.Version != 3)
            throw new InvalidDataException($"Unsupported save version: {data.Version}");
        if (string.IsNullOrWhiteSpace(data.PlayerName))
            throw new InvalidDataException("Missing player name.");
        if (data.Gold < 0 || data.ExperiencePoints < 0 || data.HitPoints < 0 || data.HitPoints > 20)
            throw new InvalidDataException("Negative player rewards.");
        if (data.Inventory is null || data.Quests is null)
            throw new InvalidDataException("Missing inventory or quest list.");

        // A save stores location identity, not mutable encounter objects.
        try { _ = WorldFactory.Get(data.CurrentLocationId); }
        catch (KeyNotFoundException ex)
        {
            throw new InvalidDataException("Unknown saved location.", ex);
        }

        if (data.EncounterMonsterId is int savedMonsterId)
        {
            if (WorldFactory.Get(data.CurrentLocationId).MonsterId != savedMonsterId ||
                data.EncounterHitPoints is null)
                throw new InvalidDataException("Encounter/location mismatch.");
            try
            {
                var probe = MonsterFactory.Create(savedMonsterId);
                probe.Restore(data.EncounterHitPoints.Value, data.EncounterRewardsClaimed);
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidDataException)
            {
                throw new InvalidDataException("Invalid saved encounter.", ex);
            }
        }
        else if (data.EncounterHitPoints is not null || data.EncounterRewardsClaimed)
            throw new InvalidDataException("Orphaned encounter fields.");

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
        player.RestoreHitPoints(data.HitPoints);
        foreach (var entry in data.Inventory)
            player.Inventory.Add(ResolveItem(entry.ItemId), entry.Quantity);
        foreach (var (quest, status) in parsedQuests)
            quest.RestoreStatus(status);
        var world = new GameWorld(data.CurrentLocationId);
        if (data.EncounterMonsterId is int encounterId)
        {
            var encounter = MonsterFactory.Create(encounterId);
            encounter.Restore(data.EncounterHitPoints!.Value, data.EncounterRewardsClaimed);
            world.RestoreEncounter(encounter);
        }
        return (player, parsedQuests.Select(x => x.Quest).ToList(), world);
    }

    private static GameItem ResolveItem(int id) => id switch
    {
        ItemFactory.SpiderSilkId => ItemFactory.SpiderSilk,
        _ => throw new InvalidDataException($"Unknown item ID: {id}")
    };
}
```

### 5.16 `ConsoleUI/CommandParser.cs`

```csharp
namespace Tutorial012.ConsoleUI;

public enum GameCommandKind
{
    Help, Look, Map, Travel, Attack, Status, Inventory, Quests,
    Accept, TurnIn, Save, Load, Quit, Invalid
}

public sealed record GameCommand(GameCommandKind Kind, int? Argument = null);

public static class CommandParser
{
    public static GameCommand Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new(GameCommandKind.Invalid);

        string[] tokens = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string verb = tokens[0].ToLowerInvariant();

        GameCommandKind kind = verb switch
        {
            "help" or "h" or "?" => GameCommandKind.Help,
            "look" or "l" => GameCommandKind.Look,
            "map" => GameCommandKind.Map,
            "go" or "travel" => GameCommandKind.Travel,
            "attack" or "a" => GameCommandKind.Attack,
            "status" or "stats" => GameCommandKind.Status,
            "inventory" or "inv" => GameCommandKind.Inventory,
            "quests" or "quest" => GameCommandKind.Quests,
            "accept" => GameCommandKind.Accept,
            "turnin" => GameCommandKind.TurnIn,
            "save" => GameCommandKind.Save,
            "load" => GameCommandKind.Load,
            "quit" or "exit" => GameCommandKind.Quit,
            _ => GameCommandKind.Invalid
        };

        bool needsArgument = kind == GameCommandKind.Travel;
        if (needsArgument)
            return tokens.Length == 2 &&
                   int.TryParse(tokens[1], out int id) && id > 0
                ? new(kind, id) : new(GameCommandKind.Invalid);

        return tokens.Length == 1 ? new(kind) : new(GameCommandKind.Invalid);
    }
}
```

### 5.17 `ConsoleUI/GameSession.cs`

```csharp
using Tutorial012.Combat;
using Tutorial012.Factories;
using Tutorial012.Models;
using Tutorial012.Persistence;
using Tutorial012.World;

namespace Tutorial012.ConsoleUI;

public sealed class GameSession
{
    private readonly SaveGameService _saves = new();
    private readonly CombatService _combat;
    private readonly string _savePath;
    private Player _player;
    private List<Quest> _quests;
    private GameWorld _world;

    public bool IsRunning { get; private set; } = true;
    public Player Player => _player;
    public GameWorld World => _world;
    public IReadOnlyList<Quest> Quests => _quests;

    public GameSession(string savePath, Random? random = null)
    {
        _savePath = Path.GetFullPath(savePath);
        _combat = new CombatService(random ?? new Random());
        _player = new Player("Adventurer");
        _quests = new List<Quest> { QuestFactory.CreateSpiderSilkQuest() };
        _world = new GameWorld();
    }

    public void Run(TextReader input, TextWriter output)
    {
        output.WriteLine("SOSCSRPG Tutorial 012 — Interactive Adventure");
        output.WriteLine("Type 'help' for commands.");
        Describe(output);
        while (IsRunning)
        {
            output.Write("\n> ");
            output.Flush();
            string? line = input.ReadLine();
            if (line is null)
            {
                output.WriteLine("Input closed. Goodbye.");
                IsRunning = false;
                break;
            }
            Execute(CommandParser.Parse(line), output);
        }
    }

    public void Execute(GameCommand command, TextWriter output)
    {
        switch (command.Kind)
        {
            case GameCommandKind.Help:
                output.WriteLine("help | look | map | go <id> | attack | status | inventory");
                output.WriteLine("quests | accept | turnin | save | load | quit");
                break;
            case GameCommandKind.Look:
                Describe(output);
                break;
            case GameCommandKind.Map:
                foreach (Location location in WorldFactory.All())
                    output.WriteLine($"{location.Id}: {location.Name} -> [{string.Join(", ", location.NeighborIds)}]");
                break;
            case GameCommandKind.Travel:
                if (!_player.IsAlive)
                    output.WriteLine("You cannot travel while defeated.");
                else if (_world.TravelTo(command.Argument!.Value))
                    Describe(output);
                else
                    output.WriteLine("Travel blocked: invalid route or living enemy.");
                break;
            case GameCommandKind.Attack:
                if (!_player.IsAlive)
                {
                    output.WriteLine("You have fallen. Load a save or quit.");
                    break;
                }
                if (_world.CurrentEncounter is null || !_world.CurrentEncounter.IsAlive)
                {
                    output.WriteLine("There is no living enemy here.");
                    break;
                }
                CombatOutcome outcome = _combat.Attack(_player, _world);
                output.WriteLine($"Combat outcome: {outcome}");
                if (outcome == CombatOutcome.PlayerDead)
                    output.WriteLine("Game over. Use 'load' or 'quit'.");
                break;
            case GameCommandKind.Status:
                output.WriteLine($"{_player.Name} | HP {_player.HitPoints}/{_player.MaximumHitPoints} | Gold {_player.Gold} | XP {_player.ExperiencePoints}");
                output.WriteLine($"Weapon: {_player.EquippedWeapon.Name}");
                break;
            case GameCommandKind.Inventory:
                var entries = _player.Inventory.GetEntries();
                if (entries.Count == 0) output.WriteLine("Inventory is empty.");
                foreach (var entry in entries)
                    output.WriteLine($"Item #{entry.ItemId}: {entry.Quantity}");
                break;
            case GameCommandKind.Quests:
                foreach (Quest quest in _quests)
                    output.WriteLine($"{quest.Title}: {quest.Status} ({quest.Progress(_player)}/{quest.RequiredQuantity})");
                break;
            case GameCommandKind.Accept:
                Quest first = _quests[0];
                if (first.Status != QuestStatus.NotStarted)
                    output.WriteLine("Quest already accepted or completed.");
                else
                {
                    first.Accept();
                    output.WriteLine($"Accepted: {first.Title}");
                }
                break;
            case GameCommandKind.TurnIn:
                Quest current = _quests[0];
                output.WriteLine(current.TurnIn(_player)
                    ? $"Quest completed! +{current.RewardGold} Gold, +{current.RewardExperience} XP"
                    : "Cannot turn in: quest inactive, completed, or missing required items.");
                break;
            case GameCommandKind.Save:
                _saves.Save(_savePath, _player, _quests, _world);
                output.WriteLine($"Saved: {_savePath}");
                break;
            case GameCommandKind.Load:
                if (!File.Exists(_savePath))
                {
                    output.WriteLine("No save file exists yet.");
                    break;
                }
                try
                {
                    var restored = _saves.Load(_savePath);
                    // Publish the fully restored state only after validation.
                    _player = restored.Player;
                    _quests = restored.Quests;
                    _world = restored.World;
                    output.WriteLine("Game loaded.");
                    Describe(output);
                }
                catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException
                    or UnauthorizedAccessException or InvalidDataException
                    or ArgumentException or OverflowException)
                {
                    output.WriteLine($"Load failed; current game preserved: {ex.Message}");
                }
                break;
            case GameCommandKind.Quit:
                IsRunning = false;
                output.WriteLine("Goodbye, adventurer.");
                break;
            default:
                output.WriteLine("Unknown command. Type 'help'.");
                break;
        }
    }

    private void Describe(TextWriter output)
    {
        output.WriteLine($"\n{_world.CurrentLocation.Name}");
        output.WriteLine(_world.CurrentLocation.Description);
        output.WriteLine("Destinations: " + string.Join(", ",
            _world.AvailableDestinations().Select(l => $"{l.Id}={l.Name}")));
        if (_world.CurrentEncounter is Monster enemy)
            output.WriteLine($"Encounter: {enemy.Name} ({enemy.HitPoints}/{enemy.MaximumHitPoints} HP)" +
                (enemy.IsAlive ? " — travel locked" : " — defeated"));
    }
}
```

### 5.18 `Program.cs`

```csharp
using Tutorial012.ConsoleUI;
using Tutorial012.Factories;
using Tutorial012.Models;
using Tutorial012.World;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    SelfTest();
    return;
}

string path = Path.Combine(Environment.CurrentDirectory, "savegame.json");
new GameSession(path).Run(Console.In, Console.Out);

static void SelfTest()
{
    static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }

    Check(CommandParser.Parse("go 2") == new GameCommand(GameCommandKind.Travel, 2),
        "Travel parser");
    Check(CommandParser.Parse("go xyz").Kind == GameCommandKind.Invalid,
        "Invalid destination rejected");
    Check(CommandParser.Parse("attack now").Kind == GameCommandKind.Invalid,
        "Extra arguments rejected");
    Check(CommandParser.Parse("  HELP ").Kind == GameCommandKind.Help,
        "Case insensitive parser");

    string temp = Path.Combine(Path.GetTempPath(), "soscsrpg-t012-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        var session = new GameSession(temp, new Random(42));
        using var log = new StringWriter();
        session.Execute(new(GameCommandKind.Accept), log);
        session.Execute(new(GameCommandKind.Travel, WorldFactory.ForestTrailId), log);
        session.Execute(new(GameCommandKind.Travel, WorldFactory.SpiderClearingId), log);
        Check(session.World.CurrentEncounter?.IsAlive == true, "Encounter started");
        session.Execute(new(GameCommandKind.Travel, WorldFactory.ForestTrailId), log);
        Check(session.World.CurrentLocationId == WorldFactory.SpiderClearingId,
            "Travel locked during combat");

        session.Execute(new(GameCommandKind.Attack), log);
        int hp = session.World.CurrentEncounter!.HitPoints;
        session.Execute(new(GameCommandKind.Save), log);
        session.Execute(new(GameCommandKind.Attack), log);
        session.Execute(new(GameCommandKind.Load), log);
        Check(session.World.CurrentEncounter!.HitPoints == hp, "Mid-combat HP restored");

        int turns = 0;
        while (session.World.CurrentEncounter!.IsAlive && session.Player.IsAlive && turns++ < 30)
            session.Execute(new(GameCommandKind.Attack), log);
        Check(!session.World.CurrentEncounter!.IsAlive, "Combat victory");
        Check(session.Player.Gold == 5 && session.Player.ExperiencePoints == 10,
            "Rewards granted once");
        Check(session.Player.Inventory.Count(ItemFactory.SpiderSilkId) == 1,
            "Spider loot granted");

        session.Execute(new(GameCommandKind.Attack), log);
        Check(session.Player.Gold == 5, "No duplicate rewards");
        session.Execute(new(GameCommandKind.Save), log);
        session.Execute(new(GameCommandKind.Load), log);
        Check(session.World.CurrentEncounter?.RewardsClaimed == true,
            "Claimed state persists");
        session.Execute(new(GameCommandKind.Travel, WorldFactory.ForestTrailId), log);
        Check(session.World.CurrentLocationId == WorldFactory.ForestTrailId,
            "Travel unlocked after victory");

        var scripted = new GameSession(temp, new Random(1));
        using var reader = new StringReader("help\nstatus\nquit\n");
        using var transcript = new StringWriter();
        scripted.Run(reader, transcript);
        Check(!scripted.IsRunning && transcript.ToString().Contains("Goodbye"),
            "Scripted command loop exits");
        var eofSession = new GameSession(temp);
        eofSession.Run(new StringReader(""), TextWriter.Null);
        Check(!eofSession.IsRunning, "EOF exits without spinning");
    }
    finally
    {
        if (File.Exists(temp)) File.Delete(temp);
    }

    Console.WriteLine("ALL INTERACTIVE GAME CHECKS PASSED");
}
```

## 6. Run the automated checks

The project supports a noninteractive self-test mode:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet run -- --self-test
```

The checks cover:

- Valid and invalid command parsing.
- Travel blocked while a living monster is present.
- Saving and loading a partially damaged encounter.
- Combat victory, Gold, XP, and Spider Silk.
- No duplicate rewards after a second attack.
- Persistence of the claimed-reward flag.
- Traveling after victory.
- Scripted input using `StringReader`.
- Clean shutdown when the input stream reaches EOF.

The final success marker is:

```text
ALL INTERACTIVE GAME CHECKS PASSED
```

**Important:** These checks are part of the source. They have not been executed against a .NET SDK in this environment, so run them on your Ubuntu system before publishing a release.

## 7. Error handling and design limits

### Parsing commands

`CommandParser` normalizes the verb to lowercase and rejects extra arguments. `go` requires exactly one positive integer location ID. Unknown input prints a helpful error rather than terminating the game.

### End of input

`ReadLine()` returns `null` when input ends. The game loop treats this as a clean exit. This matters for piping command scripts into the executable.

### Save/load safety

The loader reconstructs the entire player, quest list, and world before assigning them to the running session. If loading fails validation, the current session remains intact.

Saving uses the existing Tutorial 011 temporary-file-and-rename approach. Disk permission errors on **save** are not swallowed by the command handler; they propagate. This is intentional for the tutorial and should be improved in a production user interface.

### Combat and game-over behavior

An active living encounter prevents travel. At zero HP, the player cannot attack or move; use `load` to restore a prior save, or `quit`.

### Encounter respawns

Every time you return to a monster location, a new encounter is generated. This allows farming Spider Silk for the quest but does not implement persistent cleared-area state.

### Current UI boundary

The command dispatcher uses `TextWriter`, but the inherited `CombatService` prints combat details directly to `Console.WriteLine`. The next refactor should return structured combat results or publish combat events so **all** output can be routed through one presentation interface.

## 8. Debugging in VS Code

1. Open the `Tutorial012` folder in VS Code.
2. Set a breakpoint in `CommandParser.Parse()`.
3. Press **F5** to start the C# debugger.
4. Enter `go 2` and inspect the parsed command kind and argument.
5. Step into `GameSession.Execute()`.
6. Inspect `_world.CurrentEncounter` when attempting to travel from Spider Clearing.
7. Set a breakpoint in `CombatService.Attack()` to follow weapon damage and counterattacks.
8. Set a breakpoint in `SaveGameService.Restore()` and load a saved encounter.

| Shortcut | Action |
|---|---|
| `F5` | Start / continue |
| `F9` | Toggle breakpoint |
| `F10` | Step over |
| `F11` | Step into |
| `Shift+F5` | Stop |

## 9. C# versus C++

| Concept | C# | C++ |
|---|---|---|
| Read terminal line | `Console.ReadLine()` | `std::getline(std::cin, s)` |
| Command enum | `enum GameCommandKind` | `enum class GameCommandKind` |
| Lightweight command value | `record GameCommand(...)` | `struct GameCommand` |
| Input stream injection | `TextReader` | `std::istream&` |
| Output stream injection | `TextWriter` | `std::ostream&` |
| Null on EOF | `string?` | Stream state / EOF |
| Command dispatcher | `switch` on enum | `switch` on enum class |
| Persistence | `System.Text.Json` | JSON library |

## 10. Exercises

- [ ] **A — Better inventory:** Resolve inventory item IDs to names using `ItemFactory`.
- [ ] **B — Help topics:** Support `help combat` and `help travel`.
- [ ] **C — Attack shorthand:** Verify `a` works exactly like `attack`.
- [ ] **D — Save errors:** Catch `IOException` and `UnauthorizedAccessException` during saving.
- [ ] **E — Heal command:** Add `heal` to consume a potion and restore HP.
- [ ] **F — Flee command:** Add an escape action with explicit success/failure rules.
- [ ] **G — World map:** Draw the location graph as ASCII art.
- [ ] **H — Command history:** Store recent command strings for debugging.
- [ ] **I — Combat output abstraction:** Remove direct `Console.WriteLine()` calls from `CombatService`.
- [ ] **J — Regression tests:** Turn the built-in self-checks into a dedicated xUnit test project.

### Engineering challenge

Try this scripted run:

```bash
printf 'help\naccept\nmap\ngo 2\ngo 3\nstatus\nattack\nsave\nquit\n' |
  env -u DOTNET_ROOT /usr/bin/dotnet run
```

Now restart the game and type `load`. Confirm that the encounter resumes with the saved monster HP.

## 11. GitHub workflow

```bash
cat > .gitignore <<'EOF'
bin/
obj/
.vs/
savegame.json
savegame.json.tmp
EOF

git init
git add Program.cs ConsoleUI Models Factories World Combat Persistence Tutorial012.csproj .gitignore
git commit -m "Tutorial 012: interactive terminal game loop"
```

## 12. Knowledge check

1. Why does the parser return a `GameCommand` instead of modifying game state?
2. Why does `GameSession.Run()` take `TextReader` and `TextWriter`?
3. What happens when `Console.ReadLine()` reaches EOF?
4. Why is travel blocked during combat?
5. How is duplicate loot prevented?
6. What is restored when loading a mid-combat save?
7. Why should game rules not depend on Myra widgets?
8. What remains to be refactored to make combat output fully testable?

## 13. Chapter summary

You have progressed from C# basics to a **playable terminal RPG**:

```text
Terminal commands
       |
       v
   GameSession
       |
       +--> World navigation
       +--> Turn-based combat
       +--> Inventory and quests
       +--> JSON save/load
       |
       v
   Persistent adventure
```

**Implemented:** command parsing, interactive input loop, travel, combat, quest management, save/load, invalid-input handling, EOF handling, and deterministic self-checks.

**Next: Tutorial 013 — Game UI Architecture and MonoGame + Myra Integration.** We'll introduce a UI-independent game controller and structured combat messages, then map terminal commands to GUI actions without duplicating engine rules.
