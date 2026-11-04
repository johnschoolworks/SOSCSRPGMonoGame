# SOSCSRPG C# Tutorial 011 — Turn-Based Combat and Encounter State

**Series:** Learn C# by building SOSCSRPG  
**Platform:** Ubuntu 26.04 · .NET 10 · Visual Studio Code  
**Prerequisite:** Tutorial 010 — Game State, Locations, and Navigation  
**Language:** English · **Project:** `Tutorial011`

> Original educational implementation inspired by Scott Lilly's SOSCSRPG. This is a standalone console engine, not the MonoGame + Myra port.

## 1. Learning objectives

This chapter integrates the systems built in Tutorials 001–010:

- Implement **turn-based combat** using a weapon damage range.
- Give monsters their own attack ranges and reward values.
- Add player **hit points**, damage, healing, and death state.
- Stop travel while an encounter is alive.
- Grant **gold, experience, and Spider Silk** after victory.
- Prevent duplicate rewards by tracking `RewardsClaimed`.
- Extend the JSON save format to **version 3**, preserving player HP and encounter state.
- Verify a mid-battle save restores monster HP and the combat lock.
- Practice debugging a multi-object game loop in VS Code.

## 2. Setup

In **VS Code → Terminal → New Terminal**:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet --version
code --version | head -n 1
code --list-extensions | grep -Ei 'ms-dotnettools.csharp|ms-dotnettools.csdevkit' || true
```

Install the C# extension if needed:

```bash
code --install-extension ms-dotnettools.csharp
```

Create a standalone project:

```bash
mkdir -p ~/Projects/SOSCSRPG-Tutorials
cd ~/Projects/SOSCSRPG-Tutorials

env -u DOTNET_ROOT /usr/bin/dotnet new console \
  --name Tutorial011 --framework net10.0

cd Tutorial011
mkdir -p Models Factories World Combat Persistence
code .
```

Alternatively, extract the complete source ZIP accompanying this tutorial. The ZIP is easier because this chapter includes all dependencies from Tutorial 010.

## 3. Architecture

```text
Player (HP, Weapon, Inventory, Gold, XP)
    |
    v
CombatService.Attack(player, world)
    |
    +--> Weapon.RollDamage(Random)
    +--> Monster.TakeDamage(...)
    |      |
    |      +-- alive --> monster counterattacks player
    |      |
    |      +-- defeated --> claim rewards exactly once
    |                        +--> Inventory: Spider Silk
    |                        +--> Player: Gold and XP
    |
GameWorld
    +--> CurrentLocation
    +--> CurrentEncounter
    +--> TravelTo() blocks when monster alive
    |
SaveGameService
    +--> savegame.json (version 3)
         Player HP + current location + encounter HP
         + rewards-claimed state + inventory + quests
```

### Combat flow

1. The player attacks with the equipped Rusty Sword (2–5 damage).
2. If the monster survives, it attacks the player.
3. If the monster dies, its reward is validated and claimed.
4. Victory adds Gold, XP, and any guaranteed loot.
5. Defeated encounters cannot grant rewards again.
6. Leaving an area and re-entering creates a fresh encounter.

The current lesson uses **one guaranteed Spider Silk** from a spider. Probabilistic loot from Tutorial 006 can be reintroduced in a future combat/loot service.

## 4. Complete source code

Copy the following files into the indicated paths. This chapter is standalone and includes its own quest and save/load classes.

### 4.1 `Tutorial011.csproj`

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

### 4.2 `Models/GameItem.cs`

```csharp
namespace Tutorial011.Models;

public sealed record GameItem(int Id, string Name);
```

### 4.3 `Models/Weapon.cs`

```csharp
namespace Tutorial011.Models;

public sealed record Weapon(int Id, string Name, int MinimumDamage, int MaximumDamage)
{
    public int RollDamage(Random random) => random.Next(MinimumDamage, MaximumDamage + 1);
}
```

### 4.4 `Models/Inventory.cs`

```csharp
namespace Tutorial011.Models;

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

### 4.5 `Models/Player.cs`

```csharp
namespace Tutorial011.Models;

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

### 4.6 `Models/Quest.cs`

```csharp
namespace Tutorial011.Models;

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

### 4.7 `Factories/ItemFactory.cs`

```csharp
using Tutorial011.Models;

namespace Tutorial011.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public static GameItem SpiderSilk { get; } = new(SpiderSilkId, "Spider Silk");
}
```

### 4.8 `Factories/QuestFactory.cs`

```csharp
using Tutorial011.Models;

namespace Tutorial011.Factories;

public static class QuestFactory
{
    public static Quest CreateSpiderSilkQuest() =>
        new(id: 1, title: "Collect Spider Silk",
            requiredItemId: ItemFactory.SpiderSilkId,
            requiredQuantity: 3, rewardGold: 10, rewardExperience: 20);
}
```

### 4.9 `World/Location.cs`

```csharp
namespace Tutorial011.World;

public sealed record Location(int Id, string Name, string Description,
    IReadOnlyList<int> NeighborIds, int? MonsterId = null);
```

### 4.10 `World/MonsterFactory.cs`

```csharp
namespace Tutorial011.World;

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

### 4.11 `World/WorldFactory.cs`

```csharp
namespace Tutorial011.World;

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

### 4.12 `World/GameWorld.cs`

```csharp
namespace Tutorial011.World;

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

### 4.13 `Combat/CombatService.cs`

```csharp
using Tutorial011.Factories;
using Tutorial011.Models;
using Tutorial011.World;

namespace Tutorial011.Combat;

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

### 4.14 `Persistence/SaveData.cs`

```csharp
namespace Tutorial011.Persistence;

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

### 4.15 `Persistence/SaveGameService.cs`

```csharp
using System.Text.Json;
using Tutorial011.Factories;
using Tutorial011.Models;
using Tutorial011.World;

namespace Tutorial011.Persistence;

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

### 4.16 `Program.cs`

```csharp
using Tutorial011.Combat;
using Tutorial011.Factories;
using Tutorial011.Models;
using Tutorial011.Persistence;
using Tutorial011.World;

Console.WriteLine("=== SOSCSRPG Tutorial 011 ===");
Console.WriteLine("Turn-Based Combat and Encounter State");

var player = new Player("Adventurer");
var quest = QuestFactory.CreateSpiderSilkQuest();
quest.Accept();
var world = new GameWorld();
var combat = new CombatService(new Random(42));

Check(world.TravelTo(WorldFactory.ForestTrailId), "Travel to forest");
Check(world.TravelTo(WorldFactory.SpiderClearingId), "Enter spider clearing");
Check(!world.TravelTo(WorldFactory.ForestTrailId), "Cannot leave living encounter");

while (world.CurrentEncounter?.IsAlive == true && player.IsAlive)
    combat.Attack(player, world);

Check(player.IsAlive, "Player survived encounter");
Check(world.CurrentEncounter is { RewardsClaimed: true }, "Rewards claimed once");
Check(player.Gold == 5 && player.ExperiencePoints == 10, "Combat rewards awarded");
Check(player.Inventory.Count(ItemFactory.SpiderSilkId) == 1, "Spider Silk dropped");
Check(combat.Attack(player, world) == CombatOutcome.NoEncounter, "Cannot repeat rewards");

var save = new SaveGameService();
string path = Path.Combine(Environment.CurrentDirectory, "savegame.json");
save.Save(path, player, new[] { quest }, world);
var (loadedPlayer, loadedQuests, loadedWorld) = save.Load(path);
Check(loadedPlayer.HitPoints == player.HitPoints, "HP restored");
Check(loadedWorld.CurrentEncounter is { RewardsClaimed: true, HitPoints: 0 },
    "Defeated encounter state restored");
Check(combat.Attack(loadedPlayer, loadedWorld) == CombatOutcome.NoEncounter,
    "No duplicate rewards after load");
Check(loadedWorld.TravelTo(WorldFactory.ForestTrailId), "Can leave defeated encounter");
Check(loadedQuests.Single().Status == QuestStatus.Active, "Quest state restored");

// Save during an active encounter and verify that monster HP persists.
var midPlayer = new Player("MidBattle");
var midWorld = new GameWorld(WorldFactory.ForestTrailId);
Check(midWorld.TravelTo(WorldFactory.WolfDenId), "Enter wolf den");
midWorld.CurrentEncounter!.TakeDamage(4);
var data = save.Capture(midPlayer, Array.Empty<Quest>(), midWorld);
var (_, _, restoredWorld) = save.Restore(data);
Check(restoredWorld.CurrentEncounter is { HitPoints: 14, RewardsClaimed: false },
    "Active encounter HP restored");
Check(!restoredWorld.TravelTo(WorldFactory.ForestTrailId), "Combat lock survives load");
Console.WriteLine("ALL COMBAT CHECKS PASSED");

static void Check(bool result, string name)
{
    if (!result) throw new InvalidOperationException($"FAIL: {name}");
    Console.WriteLine($"PASS: {name}");
}
```

## 5. Build and run in VS Code

```bash
cd ~/Projects/SOSCSRPG-Tutorials/Tutorial011

env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run --no-build
```

Optional strict build:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet build -warnaserror
```

The console program uses `new Random(42)` for a repeatable local demonstration, attacks a spider until defeated, verifies rewards, saves and loads the defeated encounter, and separately tests saving an injured wolf during combat.

The final success marker is:

```text
ALL COMBAT CHECKS PASSED
```

The program creates `savegame.json` in the current working directory.

## 6. Combat invariants

### Travel lock

`GameWorld.TravelTo()` rejects movement when the current encounter is alive:

```csharp
if ((CurrentEncounter?.IsAlive ?? false) ||
    !CurrentLocation.NeighborIds.Contains(destinationId))
    return false;
```

A defeated monster does not prevent travel. This chapter has no **flee** command; adding one requires an explicit rule for escaping an active encounter.

### Reward-once protection

The `Monster.RewardsClaimed` flag records whether victory rewards have been claimed. `ClaimRewards()` returns `false` if the monster is alive or already claimed.

The save format persists this flag. Reloading a defeated encounter cannot grant its rewards again.

### Reward validation

The combat service checks the player's Gold, XP, and relevant loot quantity before claiming rewards. This avoids simple integer-overflow problems. It is not a full transactional system: synchronous event handlers can still throw after a state change, as discussed in Tutorial 008.

### Death

A player at zero HP cannot attack. This chapter does not implement a respawn screen or game-over menu.

## 7. JSON save format version 3

Version 3 adds:

```json
{
  "Version": 3,
  "CurrentLocationId": 3,
  "HitPoints": 17,
  "EncounterMonsterId": 1,
  "EncounterHitPoints": 0,
  "EncounterRewardsClaimed": true
}
```

These are illustrative fields from the complete save object, not a full save file.

The loader checks:

- Save version is 3.
- Player HP is within 0–20.
- The monster ID matches the current location's monster.
- Monster HP is within its valid range.
- Claimed rewards are not attached to a living monster.
- An absent encounter does not have orphaned HP or reward fields.
- Inventory and quest IDs remain valid.

**Compatibility:** Version 1 and 2 save files are not migrated automatically. This tutorial deliberately rejects them. A future migration layer can upgrade older saves.

## 8. Debugging combat in VS Code

1. Open the `Tutorial011` project folder.
2. Set a breakpoint in `CombatService.Attack()`.
3. Press **F5** to start debugging.
4. Inspect `player.HitPoints`, `monster.HitPoints`, and the weapon's damage range.
5. Use **F10** to step through a player attack and a monster counterattack.
6. Place a breakpoint in `Monster.ClaimRewards()` to inspect the one-time reward flag.
7. Place a breakpoint in `SaveGameService.Restore()` to inspect the restored encounter.
8. Confirm that reloading a defeated monster does not grant additional Gold, XP, or loot.

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
| Random damage | `Random.Next(min,max+1)` | `<random>` distribution |
| Encounter ownership | `Monster? CurrentEncounter` | `std::optional<Monster>` / smart pointer |
| Combat result | `enum CombatOutcome` | `enum class CombatOutcome` |
| Domain service | `CombatService` class | Combat service class/function |
| Reward state | `bool RewardsClaimed` | `bool` state field |
| JSON | `System.Text.Json` | External JSON library |
| Nullable reference | `Monster?` | Pointer / optional ownership model |

## 10. Exercises

- [ ] **A — Weapon upgrade:** Add an Iron Sword with damage 5–9 and make it equipable.
- [ ] **B — Healing potion:** Consume a potion during the player's turn and allow the monster to counterattack.
- [ ] **C — Game over:** Display a game-over message when HP reaches zero.
- [ ] **D — Wolf loot:** Give the Forest Wolf a Wolf Fang reward and register it in the item catalog.
- [ ] **E — Flee:** Add a flee action with a 50% success chance.
- [ ] **F — Experience levels:** Add `Level` and increase Max HP when enough XP is earned.
- [ ] **G — Test player death:** Use a deterministic random source to verify a lethal counterattack.
- [ ] **H — Save migration:** Upgrade a version 2 save by supplying default HP and encounter state.
- [ ] **I — Event handling:** Publish `CombatTurnCompleted` and `MonsterDefeated` events for the future Myra UI.

### Engineering challenge

The combat loop in this lesson is automated rather than interactive. Replace the `while` loop with a command menu:

```text
1. Attack
2. Inventory
3. Save
4. Quit
```

What should happen if the player chooses Save halfway through combat? Our version 3 format already supports saving the current encounter's HP and reward-claim state.

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
git add Program.cs Models Factories World Combat Persistence Tutorial011.csproj .gitignore
git commit -m "Tutorial 011: turn-based combat and encounter persistence"
```

## 12. Knowledge check

1. Why is `CombatService` separate from `Player` and `Monster`?
2. Why does the monster counterattack only if it survives?
3. What prevents a defeated monster from awarding rewards twice?
4. Why must reward-claim state be saved?
5. How does `TravelTo()` enforce the combat lock?
6. What happens if a player saves halfway through a fight?
7. Why is `Random` injected into `CombatService`?
8. Which part of this architecture would a Myra combat panel subscribe to?

## 13. Chapter summary

You now have a connected RPG engine with:

- A navigable world and encounter locations.
- Player HP, a weapon, and damage calculation.
- Monster counterattacks and player death detection.
- Victory Gold, XP, and guaranteed Spider Silk.
- One-time reward protection.
- JSON version 3 save/load, including mid-combat state.
- Console checks for travel restrictions, reward duplication, and encounter restoration.

**Not yet implemented:** an interactive battle menu, potion turns, levels, multiple weapons, random loot tables in combat, or a Myra battle UI.

### Next: Tutorial 012 — Interactive Game Loop, Commands, and Player Actions

We'll replace the automated demonstration with a playable terminal interface using `Console.ReadLine()`, then organize actions so the same engine can later drive MonoGame + Myra.
