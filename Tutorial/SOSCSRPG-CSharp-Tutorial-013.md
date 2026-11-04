# SOSCSRPG C# Tutorial 013 — Game UI Architecture and MonoGame + Myra Integration

**Series:** SOSCSRPG C# Tutorials  
**Environment:** Ubuntu 26.04 · .NET 10 · Visual Studio Code  
**Prerequisite:** Tutorial 012 — Interactive Game Loop, Commands, and Player Actions  
**Language:** English · **Standalone project:** `Tutorial013`

> Original learning implementation inspired by Scott Lilly's SOSCSRPG. The included executable is a console application. This chapter also shows how to attach a MonoGame + Myra UI, but the ZIP does **not** claim to include a fully built MonoGame window.

## 1. Learning objectives

By the end of this tutorial, you will:

1. Understand the distinction between **domain engine**, **application controller**, and **UI adapter**.
2. Introduce `GameController` as the only gameplay entry point for either UI.
3. Replace `CombatService`'s direct console printing with structured `CombatResult` messages.
4. Expose immutable `GameView` snapshots for labels, lists, and button states.
5. Publish `StateChanged` notifications after actions and successful loads.
6. Keep command parsing exclusively in `ConsoleUI`.
7. Understand the Myra `TextButton` → `GameAction` → `GameView` flow.
8. Preserve JSON v3 save/load without serializing UI objects or event subscribers.
9. Run automated checks for console and engine parity.

## 2. Why this refactor matters

Tutorial 012 works, but its command handler and engine are closely coupled. In particular, the old combat service calls `Console.WriteLine()` directly. If we simply add a Myra button on top, the game still writes combat narration to the terminal and the UI has to inspect engine objects to discover what changed.

Tutorial 013 separates these responsibilities:

```text
           Console input               Myra buttons
                |                            |
                v                            v
        ConsoleGame adapter            Myra UI adapter
                |                            |
                +-------------+--------------+
                              |
                              v
                 GameController.Execute()
                    |               |
                    v               v
                GameAction       GameResponse
                                    |
                           +--------+--------+
                           |                 |
                        Messages          GameView
                           |                 |
                    Combat log         UI widgets
                              |
                  Domain / world / quests
                              |
                         JSON v3
```

The **controller never references `Console`, `Myra`, or MonoGame**. The executable still has a console adapter, while a GUI can be added without rewriting game rules.

## 3. Run the complete console application

Extract the source archive, then run:

```bash
cd Tutorial013

env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run
```

If you prefer creating the project manually:

```bash
mkdir -p ~/Projects/SOSCSRPG-Tutorials
cd ~/Projects/SOSCSRPG-Tutorials
env -u DOTNET_ROOT /usr/bin/dotnet new console --name Tutorial013 --framework net10.0
cd Tutorial013
mkdir -p Models Factories World Combat Persistence Engine ConsoleUI
code .
```

The source ZIP already contains all required files.

## 4. Architecture and responsibilities

| Layer | Key types | Responsibility |
|---|---|---|
| Domain | `Player`, `Inventory`, `Quest`, `Monster` | Rules and mutable gameplay state |
| World | `GameWorld`, `WorldFactory` | Locations, movement, encounters |
| Combat | `CombatService`, `CombatResult` | Damage, counterattack, rewards, messages |
| Persistence | `SaveGameService`, `SaveData` | JSON v3 save/load |
| Application | `GameController`, `GameAction`, `GameView` | UI-neutral actions and state snapshots |
| Console adapter | `ConsoleGame`, `CommandParser` | Input parsing and terminal rendering |
| Future GUI adapter | MonoGame + Myra widgets | Buttons, labels, layout, game log |

**Important boundary:** `GameView` is a projection of the game state. The UI receives new values but not mutable `Player`, `Quest`, or `GameWorld` references.

## 5. Complete source code

The following is the entire standalone Tutorial 013 project. Each section maps directly to a file in the ZIP.

### 5.1 `Tutorial013.csproj`

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
namespace Tutorial013.Models;

public sealed record GameItem(int Id, string Name);
```

### 5.3 `Models/Weapon.cs`

```csharp
namespace Tutorial013.Models;

public sealed record Weapon(int Id, string Name, int MinimumDamage, int MaximumDamage)
{
    public int RollDamage(Random random) => random.Next(MinimumDamage, MaximumDamage + 1);
}
```

### 5.4 `Models/Inventory.cs`

```csharp
namespace Tutorial013.Models;

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
namespace Tutorial013.Models;

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
namespace Tutorial013.Models;

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
using Tutorial013.Models;

namespace Tutorial013.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public static GameItem SpiderSilk { get; } = new(SpiderSilkId, "Spider Silk");
}
```

### 5.8 `Factories/QuestFactory.cs`

```csharp
using Tutorial013.Models;

namespace Tutorial013.Factories;

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
namespace Tutorial013.World;

public sealed record Location(int Id, string Name, string Description,
    IReadOnlyList<int> NeighborIds, int? MonsterId = null);
```

### 5.10 `World/MonsterFactory.cs`

```csharp
namespace Tutorial013.World;

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
namespace Tutorial013.World;

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
namespace Tutorial013.World;

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
using Tutorial013.Factories;
using Tutorial013.Models;
using Tutorial013.World;

namespace Tutorial013.Combat;

public enum CombatOutcome { NoEncounter, PlayerDead, PlayerAttacked, MonsterDefeated }

public sealed record CombatResult(CombatOutcome Outcome, IReadOnlyList<string> Messages);

public sealed class CombatService
{
    private readonly Random _random;
    public CombatService(Random random) => _random = random ?? throw new ArgumentNullException(nameof(random));

    public CombatResult Attack(Player player, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(world);
        Monster? monster = world.CurrentEncounter;
        if (monster is null || !monster.IsAlive)
            return new(CombatOutcome.NoEncounter, new[] { "No living enemy to attack." });
        if (!player.IsAlive)
            return new(CombatOutcome.PlayerDead, new[] { "The player has fallen." });

        int damage = player.EquippedWeapon.RollDamage(_random);
        // Check victory rewards before the lethal hit changes monster state.
        if (damage >= monster.HitPoints)
        {
            if (!player.CanReceiveRewards(monster.RewardGold, monster.RewardExperience))
                throw new OverflowException("Combat reward capacity exceeded.");
            if (monster.LootItemId is int itemId && player.Inventory.Count(itemId) == int.MaxValue)
                throw new OverflowException("Loot inventory capacity exceeded.");
        }

        monster.TakeDamage(damage);
        var messages = new List<string> { $"You hit {monster.Name} for {damage}. Monster HP: {monster.HitPoints}" };
        if (!monster.IsAlive)
        {
            if (!monster.ClaimRewards())
                return new(CombatOutcome.NoEncounter, messages);
            if (monster.LootItemId == ItemFactory.SpiderSilkId)
            {
                player.Inventory.Add(ItemFactory.SpiderSilk);
                messages.Add("Loot: Spider Silk x1");
            }
            player.ReceiveRewards(monster.RewardGold, monster.RewardExperience);
            messages.Add($"Victory! +{monster.RewardGold} Gold, +{monster.RewardExperience} XP");
            return new(CombatOutcome.MonsterDefeated, messages);
        }

        int counterDamage = _random.Next(monster.MinimumDamage, monster.MaximumDamage + 1);
        player.TakeDamage(counterDamage);
        messages.Add($"{monster.Name} hits you for {counterDamage}. HP: {player.HitPoints}/{player.MaximumHitPoints}");
        return new(player.IsAlive ? CombatOutcome.PlayerAttacked : CombatOutcome.PlayerDead, messages);
    }
}
```

### 5.14 `Persistence/SaveData.cs`

```csharp
namespace Tutorial013.Persistence;

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
using Tutorial013.Factories;
using Tutorial013.Models;
using Tutorial013.World;

namespace Tutorial013.Persistence;

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

### 5.16 `Engine/GameController.cs`

```csharp
using Tutorial013.Combat;
using Tutorial013.Factories;
using Tutorial013.Models;
using Tutorial013.Persistence;
using Tutorial013.World;

namespace Tutorial013.Engine;

public enum GameActionKind
{
    Help, Look, Map, Travel, Attack, Status, Inventory, Quests,
    Accept, TurnIn, Save, Load
}

public sealed record GameAction(GameActionKind Kind, int? Argument = null);

// Immutable view models: the UI cannot mutate engine objects through these snapshots.
public sealed record DestinationView(int Id, string Name);
public sealed record InventoryView(string Name, int Quantity);
public sealed record QuestView(string Title, QuestStatus Status, int Progress, int Required);
public sealed record EncounterView(string Name, int HitPoints, int MaximumHitPoints, bool Alive);
public sealed record GameView(
    string PlayerName, int HitPoints, int MaximumHitPoints, int Gold, int Experience,
    string WeaponName, string LocationName, string LocationDescription,
    IReadOnlyList<DestinationView> Destinations,
    IReadOnlyList<InventoryView> Inventory,
    IReadOnlyList<QuestView> Quests,
    EncounterView? Encounter,
    bool CanTravel, bool CanAttack);

public sealed record GameResponse(GameView View, IReadOnlyList<string> Messages, bool Succeeded);

public sealed class GameController
{
    private readonly SaveGameService _saves = new();
    private readonly CombatService _combat;
    private readonly string _savePath;
    private Player _player;
    private List<Quest> _quests;
    private GameWorld _world;

    public event EventHandler<GameResponse>? StateChanged;

    public GameController(string savePath, Random? random = null)
    {
        _savePath = Path.GetFullPath(savePath);
        _combat = new CombatService(random ?? new Random());
        _player = new Player("Adventurer");
        _quests = new List<Quest> { QuestFactory.CreateSpiderSilkQuest() };
        _world = new GameWorld();
    }

    public GameView Snapshot()
    {
        Monster? monster = _world.CurrentEncounter;
        return new GameView(
            _player.Name, _player.HitPoints, _player.MaximumHitPoints,
            _player.Gold, _player.ExperiencePoints, _player.EquippedWeapon.Name,
            _world.CurrentLocation.Name, _world.CurrentLocation.Description,
            _world.AvailableDestinations().Select(l => new DestinationView(l.Id, l.Name)).ToArray(),
            _player.Inventory.GetEntries().Select(e =>
                new InventoryView(e.ItemId == ItemFactory.SpiderSilkId ? "Spider Silk" : $"Item #{e.ItemId}", e.Quantity)).ToArray(),
            _quests.Select(q => new QuestView(q.Title, q.Status, q.Progress(_player), q.RequiredQuantity)).ToArray(),
            monster is null ? null : new EncounterView(monster.Name, monster.HitPoints, monster.MaximumHitPoints, monster.IsAlive),
            _player.IsAlive && monster?.IsAlive != true,
            _player.IsAlive && monster?.IsAlive == true);
    }

    public GameResponse Execute(GameAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var messages = new List<string>();
        bool success = true;
        switch (action.Kind)
        {
            case GameActionKind.Help:
                messages.Add("help | look | map | go <id> | attack | status | inventory | quests | accept | turnin | save | load | quit");
                break;
            case GameActionKind.Look:
                messages.Add(_world.CurrentLocation.Name + ": " + _world.CurrentLocation.Description);
                break;
            case GameActionKind.Map:
                messages.AddRange(WorldFactory.All().Select(l => $"{l.Id}: {l.Name} -> [{string.Join(", ", l.NeighborIds)}]"));
                break;
            case GameActionKind.Travel:
                success = action.Argument is int id && _player.IsAlive && _world.TravelTo(id);
                messages.Add(success ? "Traveled to " + _world.CurrentLocation.Name : "Travel blocked: invalid route or living enemy.");
                break;
            case GameActionKind.Attack:
                CombatResult combat = _combat.Attack(_player, _world);
                messages.AddRange(combat.Messages);
                success = combat.Outcome is CombatOutcome.PlayerAttacked or CombatOutcome.MonsterDefeated;
                break;
            case GameActionKind.Status:
                messages.Add($"{_player.Name} | HP {_player.HitPoints}/{_player.MaximumHitPoints} | Gold {_player.Gold} | XP {_player.ExperiencePoints}");
                break;
            case GameActionKind.Inventory:
                messages.AddRange(Snapshot().Inventory.Select(i => $"{i.Name} x{i.Quantity}"));
                if (messages.Count == 0) messages.Add("Inventory is empty.");
                break;
            case GameActionKind.Quests:
                messages.AddRange(Snapshot().Quests.Select(q => $"{q.Title}: {q.Status} ({q.Progress}/{q.Required})"));
                break;
            case GameActionKind.Accept:
                success = _quests[0].Status == QuestStatus.NotStarted;
                if (success) _quests[0].Accept();
                messages.Add(success ? "Quest accepted." : "Quest already accepted or completed.");
                break;
            case GameActionKind.TurnIn:
                success = _quests[0].TurnIn(_player);
                messages.Add(success ? "Quest completed! Rewards granted." : "Cannot turn in this quest yet.");
                break;
            case GameActionKind.Save:
                try
                {
                    _saves.Save(_savePath, _player, _quests, _world);
                    messages.Add("Game saved.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    success = false;
                    messages.Add("Save failed: " + ex.Message);
                }
                break;
            case GameActionKind.Load:
                try
                {
                    var restored = _saves.Load(_savePath);
                    // No partial publication: swap only after complete validation.
                    _player = restored.Player;
                    _quests = restored.Quests;
                    _world = restored.World;
                    messages.Add("Game loaded.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                    or System.Text.Json.JsonException or InvalidDataException
                    or ArgumentException or OverflowException)
                {
                    success = false;
                    messages.Add("Load failed; current game preserved: " + ex.Message);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), "Unknown action.");
        }

        GameResponse result = new(Snapshot(), messages.ToArray(), success);
        StateChanged?.Invoke(this, result);
        return result;
    }
}
```

### 5.17 `ConsoleUI/CommandParser.cs`

```csharp
namespace Tutorial013.ConsoleUI;

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

### 5.18 `ConsoleUI/ConsoleGame.cs`

```csharp
using Tutorial013.Engine;

namespace Tutorial013.ConsoleUI;

public sealed class ConsoleGame
{
    private readonly GameController _controller;

    public ConsoleGame(GameController controller) =>
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));

    public void Run(TextReader input, TextWriter output)
    {
        output.WriteLine("SOSCSRPG Tutorial 013 — UI-independent RPG Engine");
        output.WriteLine("Type 'help' for commands.");
        RenderView(_controller.Snapshot(), output);
        while (true)
        {
            output.Write("\n> ");
            output.Flush();
            string? line = input.ReadLine();
            if (line is null) { output.WriteLine("Input closed. Goodbye."); return; }
            if (line.Trim().Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                output.WriteLine("Goodbye, adventurer.");
                return;
            }
            var command = CommandParser.Parse(line);
            if (command.Kind == GameCommandKind.Invalid)
            {
                output.WriteLine("Unknown command. Type 'help'.");
                continue;
            }
            GameResponse result = _controller.Execute(ToAction(command));
            foreach (string message in result.Messages) output.WriteLine(message);
            RenderView(result.View, output);
        }
    }

    public static GameAction ToAction(GameCommand command) =>
        new(command.Kind switch
        {
            GameCommandKind.Help => GameActionKind.Help,
            GameCommandKind.Look => GameActionKind.Look,
            GameCommandKind.Map => GameActionKind.Map,
            GameCommandKind.Travel => GameActionKind.Travel,
            GameCommandKind.Attack => GameActionKind.Attack,
            GameCommandKind.Status => GameActionKind.Status,
            GameCommandKind.Inventory => GameActionKind.Inventory,
            GameCommandKind.Quests => GameActionKind.Quests,
            GameCommandKind.Accept => GameActionKind.Accept,
            GameCommandKind.TurnIn => GameActionKind.TurnIn,
            GameCommandKind.Save => GameActionKind.Save,
            GameCommandKind.Load => GameActionKind.Load,
            _ => throw new ArgumentException("Unsupported command.", nameof(command))
        }, command.Argument);

    public static void RenderView(GameView view, TextWriter output)
    {
        output.WriteLine($"\n[{view.LocationName}] {view.LocationDescription}");
        output.WriteLine($"HP {view.HitPoints}/{view.MaximumHitPoints} | Gold {view.Gold} | XP {view.Experience}");
        output.WriteLine("Destinations: " + string.Join(", ", view.Destinations.Select(d => $"{d.Id}={d.Name}")));
        if (view.Encounter is not null)
            output.WriteLine($"Enemy: {view.Encounter.Name} ({view.Encounter.HitPoints}/{view.Encounter.MaximumHitPoints} HP)");
    }
}
```

### 5.19 `Program.cs`

```csharp
using Tutorial013.ConsoleUI;
using Tutorial013.Engine;
using Tutorial013.Factories;
using Tutorial013.World;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    SelfTest();
    return;
}

var controller = new GameController(Path.Combine(Environment.CurrentDirectory, "savegame.json"));
new ConsoleGame(controller).Run(Console.In, Console.Out);

static void SelfTest()
{
    static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }

    Check(CommandParser.Parse("go 2") == new GameCommand(GameCommandKind.Travel, 2), "Parser travel");
    Check(CommandParser.Parse("go xyz").Kind == GameCommandKind.Invalid, "Parser rejects bad ID");
    Check(CommandParser.Parse("attack now").Kind == GameCommandKind.Invalid, "Parser rejects extra arguments");
    string save = Path.Combine(Path.GetTempPath(), "soscsrpg-t013-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        var game = new GameController(save, new Random(42));
        int notifications = 0;
        game.StateChanged += (_, _) => notifications++;
        Check(game.Snapshot().LocationName == "Town Square", "Initial snapshot");
        Check(game.Execute(new(GameActionKind.Accept)).Succeeded, "Quest accepted");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Travel to forest");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.SpiderClearingId)).Succeeded, "Travel to clearing");
        Check(game.Snapshot().CanAttack && !game.Snapshot().CanTravel, "UI combat flags");
        Check(!game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Combat travel lock");
        var attack = game.Execute(new(GameActionKind.Attack));
        Check(attack.Messages.Any(m => m.StartsWith("You hit")), "Structured combat messages");
        int savedHp = game.Snapshot().Encounter!.HitPoints;
        Check(game.Execute(new(GameActionKind.Save)).Succeeded, "Save succeeds");
        game.Execute(new(GameActionKind.Attack));
        Check(game.Execute(new(GameActionKind.Load)).Succeeded, "Load succeeds");
        Check(game.Snapshot().Encounter!.HitPoints == savedHp, "Mid-battle HP restored");
        int turns = 0;
        while (game.Snapshot().CanAttack && turns++ < 30)
            game.Execute(new(GameActionKind.Attack));
        Check(!game.Snapshot().Encounter!.Alive, "Monster defeated");
        Check(game.Snapshot().Gold == 5 && game.Snapshot().Experience == 10, "Rewards granted");
        Check(game.Snapshot().Inventory.Single().Quantity == 1, "Spider Silk obtained");
        Check(game.Execute(new(GameActionKind.Save)).Succeeded, "Save defeated encounter");
        Check(game.Execute(new(GameActionKind.Load)).Succeeded, "Reload defeated encounter");
        Check(!game.Execute(new(GameActionKind.Attack)).Succeeded, "No repeated attack rewards");
        Check(game.Snapshot().Gold == 5, "Gold not duplicated");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Travel unlocked");
        Check(notifications > 0, "StateChanged published");
        var scripted = new ConsoleGame(new GameController(save));
        using var reader = new StringReader("help\nstatus\nquit\n".Replace("\\n", "\n"));
        using var writer = new StringWriter();
        scripted.Run(reader, writer);
        Check(writer.ToString().Contains("Goodbye"), "Console adapter runs");
        new ConsoleGame(new GameController(save)).Run(new StringReader(""), TextWriter.Null);
        Console.WriteLine("ALL UI ARCHITECTURE CHECKS PASSED");
    }
    finally
    {
        if (File.Exists(save)) File.Delete(save);
    }
}
```

## 6. A UI-neutral action and response

A UI sends a value object:

```csharp
new GameAction(GameActionKind.Travel, 2)
```

The controller returns a `GameResponse` containing:

- `Succeeded`: whether the action succeeded.
- `Messages`: narration and feedback suitable for a combat log.
- `View`: a fresh `GameView` snapshot.

For example:

```csharp
var response = controller.Execute(new GameAction(GameActionKind.Attack));
foreach (string message in response.Messages)
    Console.WriteLine(message);

Console.WriteLine($"Player HP: {response.View.HitPoints}");
```

This code belongs in an adapter or test—not inside the domain engine.

## 7. Hooking the controller into MonoGame + Myra

**Integration guide:** The following code is an adapter pattern for an existing MonoGame DesktopGL + Myra project. It is **not part of the standalone console build**, and package versions, namespaces, and widget initialization may need adjustment for your specific Myra version.

The important point is to keep the existing MonoGame `Game` class responsible for `Update`, `Draw`, `SpriteBatch`, and Myra's desktop, while the `GameController` remains a plain C# class.

### Step A — Reference the engine

For a production repository, move `Models`, `Factories`, `World`, `Combat`, `Persistence`, and `Engine` into a class library (for example, `SOSCSRPG.Engine`) and reference it from both console and MonoGame projects:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet new classlib -n SOSCSRPG.Engine -f net10.0
env -u DOTNET_ROOT /usr/bin/dotnet add SOSCSRPG.MonoGame/SOSCSRPG.MonoGame.csproj \
  reference SOSCSRPG.Engine/SOSCSRPG.Engine.csproj
```

The second command assumes the indicated MonoGame project already exists; substitute your real path. Moving source files into a library requires updating namespaces or preserving the existing `Tutorial013.*` namespaces consistently.

### Step B — Build the UI

Illustrative Myra wiring (adapt to your installed Myra API):

```csharp
using Tutorial013.Engine;
using Myra.Graphics2D.UI;

// These widgets are created and added to your Myra layout elsewhere.
TextButton attackButton = /* your Attack button */;
TextButton saveButton = /* your Save button */;
Label locationLabel = /* your location label */;
Label hpLabel = /* your HP label */;

var controller = new GameController("savegame.json");

void Refresh(GameView view)
{
    locationLabel.Text = view.LocationName;
    hpLabel.Text = $"HP: {view.HitPoints}/{view.MaximumHitPoints}";
    attackButton.Enabled = view.CanAttack;
    // Rebuild destination buttons from view.Destinations.
    // Render view.Encounter, view.Inventory, and view.Quests.
}

void Dispatch(GameAction action)
{
    GameResponse result = controller.Execute(action);
    Refresh(result.View);
    // Append result.Messages to your Myra combat-log widget.
}

attackButton.Click += (_, _) => Dispatch(new GameAction(GameActionKind.Attack));
saveButton.Click += (_, _) => Dispatch(new GameAction(GameActionKind.Save));
Refresh(controller.Snapshot());
```

The widget variables above are placeholders: they are not meant to compile as-is. The snippet demonstrates how to connect existing Myra widgets to the controller without embedding combat rules in click handlers.

### Step C — Travel buttons

Each destination button should capture a stable location ID, not a mutable `Location` object:

```csharp
foreach (DestinationView destination in controller.Snapshot().Destinations)
{
    int id = destination.Id;
    // Create a Myra TextButton labeled destination.Name.
    // On click: Dispatch(new GameAction(GameActionKind.Travel, id));
}
```

After moving, rebuild the destination list from the **new** `GameView`. Do not assume that destination buttons remain valid across location changes or a loaded save.

### Step D — Refresh after load

`GameController.Execute(GameActionKind.Load)` reconstructs a fresh player, quest list, and world. An adapter that stores old engine object references will become stale. Always render from `GameResponse.View` or call `controller.Snapshot()` again.

### Step E — MonoGame frame loop

Myra handles widget input through the host MonoGame game loop. Avoid calling `GameController.Execute()` every frame: call it **once per user action**, then update the displayed view. Otherwise one click could cause repeated attacks or duplicate save operations.

## 8. Testing without opening a window

Run the self-test mode:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet run -- --self-test
```

The checks exercise:

- Command parsing and argument validation.
- `GameController.Snapshot()` before any actions.
- Accepting a quest and traveling to an encounter.
- `CanAttack` / `CanTravel` UI state flags.
- Blocking travel while a monster is alive.
- Receiving combat narration as structured messages.
- Saving and loading a partially damaged monster.
- Defeating the monster and receiving rewards once.
- Saving and loading a claimed encounter.
- Publishing `StateChanged`.
- Running the terminal adapter with scripted input and EOF.

Expected final line:

```text
ALL UI ARCHITECTURE CHECKS PASSED
```

The self-tests are included but have **not been executed in this authoring environment**; verify them with your local .NET 10 SDK.

## 9. Design limitations

- **No fully runnable MonoGame/Myra window is included.** This chapter delivers a runnable-targeted console project plus an integration recipe, not a GUI package.
- **Single-threaded assumption:** Controller actions are expected on the game/UI thread. Multi-threaded dispatch would require synchronization.
- **Event handlers can throw:** `StateChanged` is synchronous. A failing subscriber may throw after the action already changed state.
- **UI snapshots are read-only by convention:** The snapshot uses read-only interfaces, but underlying arrays can be cast and modified by hostile callers. For stronger immutability, return immutable collections.
- **Save version remains 3:** The UI refactor does not change the persisted data model.
- **No migration:** Version 1/2 saves still need an explicit migration path.
- **No NPC/shop UI yet:** These systems are planned for Tutorials 014–015.

## 10. Debugging with VS Code

1. Open `Tutorial013` in VS Code.
2. Set a breakpoint in `ConsoleUI/CommandParser.cs`.
3. Press **F5** and enter `go 2`.
4. Step into `ConsoleGame.ToAction()`.
5. Step into `GameController.Execute()` and inspect the `GameAction`.
6. Inspect `GameResponse.View` and `GameResponse.Messages`.
7. Set a breakpoint in `CombatService.Attack()` and confirm it never calls `Console.WriteLine()`.
8. Set a breakpoint in `SaveGameService.Restore()` and load a save.

| Shortcut | Purpose |
|---|---|
| `F5` | Start / continue |
| `F9` | Toggle breakpoint |
| `F10` | Step over |
| `F11` | Step into |
| `Shift+F5` | Stop |

## 11. C# versus C++

| Idea | C# | C++ |
|---|---|---|
| UI-neutral controller | `GameController` | Controller/service class |
| Action value | `record GameAction` | `struct GameAction` |
| Immutable projection | `record GameView` | Value-type view DTO |
| Structured messages | `IReadOnlyList<string>` | `std::vector<std::string>` |
| Change notifications | `EventHandler<T>` | Observer/callback/signal |
| GUI adapter | Myra widget callbacks | Qt signals/slots, ImGui handlers |
| Terminal adapter | `TextReader`/`TextWriter` | `std::istream`/`std::ostream` |

## 12. Exercises

- [ ] **A — UI notifications:** Subscribe a console logger to `StateChanged` without duplicating output.
- [ ] **B — View rendering:** Display quest progress and inventory in `ConsoleGame.RenderView()`.
- [ ] **C — Better errors:** Replace user-visible exception messages with friendly error codes.
- [ ] **D — Action history:** Log every successful `GameAction` and timestamp.
- [ ] **E — Typed combat events:** Introduce `CombatEvent` records for damage, defeat, and loot.
- [ ] **F — Myra prototype:** Add a location label, Attack button, and HP label to your existing MonoGame project.
- [ ] **G — Travel widget:** Generate destination buttons dynamically from `GameView.Destinations`.
- [ ] **H — UI tests:** Verify `CanAttack` and `CanTravel` after loading a mid-combat save.
- [ ] **I — Engine library:** Move engine files into a class library referenced by two frontends.
- [ ] **J — xUnit:** Replace the built-in self-test with a separate automated test project.

## 13. GitHub workflow

```bash
cat > .gitignore <<'EOF'
bin/
obj/
.vs/
savegame.json
savegame.json.tmp
EOF

git init
git add .
git commit -m "Tutorial 013: UI-neutral controller and MonoGame Myra integration guide"
```

## 14. Knowledge check

1. Why should `CombatService` not call `Console.WriteLine()`?
2. Why is `GameView` better for UI binding than passing `Player` directly?
3. What happens to UI references after `Load` replaces engine objects?
4. Why should a Myra click handler dispatch one `GameAction`?
5. What does `StateChanged` publish?
6. Which layer owns the JSON save format?
7. Why is a separate engine class library useful?
8. What additional work is needed for a fully running MonoGame + Myra window?

## 15. Chapter summary

You now have a cleaner separation between the **RPG engine** and the **presentation layer**. The console frontend remains usable, and the same controller API can be called by MonoGame + Myra without rewriting combat, quests, navigation, or save/load.

**Delivered:** complete standalone .NET 10 console source, structured combat messages, `GameController`, action/response types, view snapshots, state-change notifications, self-tests, and Myra adapter guidance.

**Not delivered:** a packaged MonoGame GUI executable or a tested Myra widget implementation.

### Next: Tutorial 014 — NPC Dialogue and Quest Givers

We'll introduce NPC definitions, dialogue choices, quest-giver interactions, and UI-neutral conversation actions that can be rendered in both terminal and Myra interfaces.
