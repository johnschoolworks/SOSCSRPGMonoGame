# SOSCSRPG C# Tutorial 014 — NPC Dialogue and Quest Givers

**Series:** SOSCSRPG C# Tutorials  
**Platform:** Ubuntu 26.04 · .NET 10 · Visual Studio Code  
**Prerequisite:** Tutorial 013 — Game UI Architecture and MonoGame + Myra Integration  
**Language:** English · **Project:** `Tutorial014`

> Original educational C# RPG implementation inspired by Scott Lilly's SOSCSRPG. The source ZIP is a standalone console project; Myra integration is described as an adapter pattern, not shipped as a graphical executable.

## 1. Objectives

In this chapter, we add **non-player characters (NPCs)** and **choice-based dialogue** to the UI-independent RPG engine.

You'll learn to:

1. Define an NPC catalog with immutable `record` models.
2. Associate NPCs with world locations.
3. Display a conversation with numbered dialogue choices.
4. Accept a quest **through an NPC conversation**.
5. Turn in collected Spider Silk to the quest giver.
6. Reject remote dialogue, invalid NPC IDs, and repeated quest acceptance.
7. Expose NPC and conversation view models for both terminal and Myra.
8. Keep conversation state transient while saving durable quest progress to JSON v3.
9. Run scripted self-tests for dialogue and quest persistence.

## 2. Project setup

Extract the source ZIP into your projects folder and open it in VS Code:

```bash
cd Tutorial014
code .
env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run
```

You can also scaffold the project manually:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet new console --name Tutorial014 --framework net10.0
cd Tutorial014
mkdir -p Models Factories World Dialogue Combat Persistence Engine ConsoleUI
```

The ZIP contains all source files from Tutorial 013 plus the new dialogue subsystem.

## 3. New player commands

| Command | Meaning |
|---|---|
| `look` | Show the current location and visible NPCs |
| `talk 1` | Start a conversation with Elena the Weaver |
| `choose 1` | Ask Elena about her quest; accept if not already accepted |
| `choose 2` | Offer three Spider Silk to complete the active quest |
| `choose 3` | End the conversation |
| `quests` | Display quest status and progress |
| `inventory` | Display carried items |
| `go 2` | Travel to the Forest Trail |
| `go 3` | Travel from the trail to Spider Clearing |
| `attack` | Fight the spider to obtain Spider Silk |
| `save` / `load` | Persist or restore the adventure |
| `help` / `map` / `status` / `quit` | General commands |

The old direct `accept` and `turnin` commands have intentionally been removed. The quest is now offered and completed **in conversation** with its giver.

## 4. World and dialogue architecture

```text
Town Square (location 1)
  └── Elena the Weaver (NPC 1)
       ├── Choice 1: Ask about Spider Silk quest
       ├── Choice 2: Offer collected Spider Silk
       └── Choice 3: Say goodbye

Forest Trail (location 2)
  └── Spider Clearing (location 3)
       └── Venomous Spider -> Spider Silk ×1 per victory

Terminal / Myra widgets
          |
          v
GameAction(Talk, npcId) / GameAction(Choose, choiceId)
          |
          v
GameController
          ├── NpcFactory (definitions and location lookup)
          ├── Quest (accept, progress, turn in)
          ├── GameWorld (current location)
          └── GameView (NPC list + conversation + quest state)
```

The **NPC definition** is static data. The **current conversation** is runtime controller state. Quest status is durable gameplay state saved in JSON.

## 5. Complete source listings

Every file below is included in the downloadable ZIP.

### 5.1 `Tutorial014.csproj`

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
namespace Tutorial014.Models;

public sealed record GameItem(int Id, string Name);
```

### 5.3 `Models/Weapon.cs`

```csharp
namespace Tutorial014.Models;

public sealed record Weapon(int Id, string Name, int MinimumDamage, int MaximumDamage)
{
    public int RollDamage(Random random) => random.Next(MinimumDamage, MaximumDamage + 1);
}
```

### 5.4 `Models/Inventory.cs`

```csharp
namespace Tutorial014.Models;

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
namespace Tutorial014.Models;

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
namespace Tutorial014.Models;

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
using Tutorial014.Models;

namespace Tutorial014.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public static GameItem SpiderSilk { get; } = new(SpiderSilkId, "Spider Silk");
}
```

### 5.8 `Factories/QuestFactory.cs`

```csharp
using Tutorial014.Models;

namespace Tutorial014.Factories;

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
namespace Tutorial014.World;

public sealed record Location(int Id, string Name, string Description,
    IReadOnlyList<int> NeighborIds, int? MonsterId = null);
```

### 5.10 `World/MonsterFactory.cs`

```csharp
namespace Tutorial014.World;

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
namespace Tutorial014.World;

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
namespace Tutorial014.World;

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

### 5.13 `Dialogue/NpcFactory.cs`

```csharp
using Tutorial014.World;

namespace Tutorial014.Dialogue;

public sealed record DialogueChoice(int Id, string Text);
public sealed record NpcDefinition(int Id, string Name, int LocationId, string Greeting,
    IReadOnlyList<DialogueChoice> Choices);

public static class NpcFactory
{
    public const int WeaverId = 1;
    private static readonly NpcDefinition Weaver = new(
        WeaverId, "Elena the Weaver", WorldFactory.TownSquareId,
        "Welcome, traveler. The forest spiders have silk I need.",
        new[] {
            new DialogueChoice(1, "Ask about the Spider Silk quest"),
            new DialogueChoice(2, "Offer collected Spider Silk"),
            new DialogueChoice(3, "Say goodbye")
        });

    public static IReadOnlyList<NpcDefinition> AtLocation(int locationId) =>
        locationId == Weaver.LocationId ? new[] { Weaver } : Array.Empty<NpcDefinition>();

    public static NpcDefinition? FindAtLocation(int npcId, int locationId) =>
        npcId == Weaver.Id && locationId == Weaver.LocationId ? Weaver : null;
}
```

### 5.14 `Combat/CombatService.cs`

```csharp
using Tutorial014.Factories;
using Tutorial014.Models;
using Tutorial014.World;

namespace Tutorial014.Combat;

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

### 5.15 `Persistence/SaveData.cs`

```csharp
namespace Tutorial014.Persistence;

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

### 5.16 `Persistence/SaveGameService.cs`

```csharp
using System.Text.Json;
using Tutorial014.Factories;
using Tutorial014.Models;
using Tutorial014.World;

namespace Tutorial014.Persistence;

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

### 5.17 `Engine/GameController.cs`

```csharp
using Tutorial014.Combat;
using Tutorial014.Dialogue;
using Tutorial014.Factories;
using Tutorial014.Models;
using Tutorial014.Persistence;
using Tutorial014.World;

namespace Tutorial014.Engine;

public enum GameActionKind
{
    Help, Look, Map, Travel, Attack, Status, Inventory, Quests,
    Talk, Choose, Save, Load
}

public sealed record GameAction(GameActionKind Kind, int? Argument = null);

// Immutable view models: the UI cannot mutate engine objects through these snapshots.
public sealed record DestinationView(int Id, string Name);
public sealed record InventoryView(string Name, int Quantity);
public sealed record QuestView(string Title, QuestStatus Status, int Progress, int Required);
public sealed record NpcView(int Id, string Name);
public sealed record ConversationView(int NpcId, string NpcName, string Greeting, IReadOnlyList<DialogueChoice> Choices);
public sealed record EncounterView(string Name, int HitPoints, int MaximumHitPoints, bool Alive);
public sealed record GameView(
    string PlayerName, int HitPoints, int MaximumHitPoints, int Gold, int Experience,
    string WeaponName, string LocationName, string LocationDescription,
    IReadOnlyList<DestinationView> Destinations,
    IReadOnlyList<InventoryView> Inventory,
    IReadOnlyList<QuestView> Quests,
    EncounterView? Encounter,
    IReadOnlyList<NpcView> Npcs, ConversationView? Conversation,
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
    private int? _talkingTo;

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
            NpcFactory.AtLocation(_world.CurrentLocationId).Select(n => new NpcView(n.Id, n.Name)).ToArray(),
            CurrentConversation(),
            _player.IsAlive && monster?.IsAlive != true,
            _player.IsAlive && monster?.IsAlive == true);
    }

    private ConversationView? CurrentConversation()
    {
        if (_talkingTo is not int id) return null;
        var npc = NpcFactory.FindAtLocation(id, _world.CurrentLocationId);
        return npc is null ? null : new ConversationView(npc.Id, npc.Name, npc.Greeting,
            npc.Choices.ToArray());
    }

    public GameResponse Execute(GameAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var messages = new List<string>();
        bool success = true;
        switch (action.Kind)
        {
            case GameActionKind.Help:
                messages.Add("help | look | map | go <id> | attack | status | inventory | quests | talk <id> | choose <id> | save | load | quit");
                break;
            case GameActionKind.Look:
                messages.Add(_world.CurrentLocation.Name + ": " + _world.CurrentLocation.Description);
                break;
            case GameActionKind.Map:
                messages.AddRange(WorldFactory.All().Select(l => $"{l.Id}: {l.Name} -> [{string.Join(", ", l.NeighborIds)}]"));
                break;
            case GameActionKind.Travel:
                success = action.Argument is int id && _player.IsAlive && _world.TravelTo(id);
                if (success) _talkingTo = null;
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
            case GameActionKind.Talk:
                var npc = action.Argument is int npcId
                    ? NpcFactory.FindAtLocation(npcId, _world.CurrentLocationId) : null;
                success = npc is not null && _player.IsAlive && _world.CurrentEncounter?.IsAlive != true;
                if (success)
                {
                    _talkingTo = npc!.Id;
                    messages.Add($"{npc.Name}: {npc.Greeting}");
                    messages.AddRange(npc.Choices.Select(c => $"{c.Id}. {c.Text}"));
                }
                else messages.Add("That NPC is not here, or you cannot talk right now.");
                break;
            case GameActionKind.Choose:
                success = _talkingTo == NpcFactory.WeaverId &&
                    NpcFactory.FindAtLocation(NpcFactory.WeaverId, _world.CurrentLocationId) is not null &&
                    _player.IsAlive && _world.CurrentEncounter?.IsAlive != true &&
                    action.Argument is >= 1 and <= 3;
                if (!success)
                {
                    messages.Add("No valid dialogue choice is available.");
                    break;
                }
                switch (action.Argument!.Value)
                {
                    case 1:
                        if (_quests[0].Status == QuestStatus.NotStarted)
                        {
                            _quests[0].Accept();
                            messages.Add("Elena: Bring me three Spider Silk. Quest accepted!");
                        }
                        else
                        {
                            success = false;
                            messages.Add("Elena: We've already discussed this quest.");
                        }
                        break;
                    case 2:
                        success = _quests[0].TurnIn(_player);
                        messages.Add(success
                            ? "Elena: Thank you! Here is your reward. Quest completed!"
                            : "Elena: I still need three Spider Silk, and an active quest.");
                        break;
                    case 3:
                        _talkingTo = null;
                        messages.Add("Elena: Safe travels!");
                        break;
                }
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
                    _talkingTo = null; // Dialogue is transient UI state, not persisted.
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

### 5.18 `ConsoleUI/CommandParser.cs`

```csharp
namespace Tutorial014.ConsoleUI;

public enum GameCommandKind
{
    Help, Look, Map, Travel, Attack, Status, Inventory, Quests,
    Talk, Choose, Save, Load, Quit, Invalid
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
            "talk" => GameCommandKind.Talk,
            "choose" => GameCommandKind.Choose,
            "save" => GameCommandKind.Save,
            "load" => GameCommandKind.Load,
            "quit" or "exit" => GameCommandKind.Quit,
            _ => GameCommandKind.Invalid
        };

        bool needsArgument = kind is GameCommandKind.Travel or GameCommandKind.Talk or GameCommandKind.Choose;
        if (needsArgument)
            return tokens.Length == 2 &&
                   int.TryParse(tokens[1], out int id) && id > 0
                ? new(kind, id) : new(GameCommandKind.Invalid);

        return tokens.Length == 1 ? new(kind) : new(GameCommandKind.Invalid);
    }
}
```

### 5.19 `ConsoleUI/ConsoleGame.cs`

```csharp
using Tutorial014.Engine;

namespace Tutorial014.ConsoleUI;

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
            GameCommandKind.Talk => GameActionKind.Talk,
            GameCommandKind.Choose => GameActionKind.Choose,
            GameCommandKind.Save => GameActionKind.Save,
            GameCommandKind.Load => GameActionKind.Load,
            _ => throw new ArgumentException("Unsupported command.", nameof(command))
        }, command.Argument);

    public static void RenderView(GameView view, TextWriter output)
    {
        output.WriteLine($"\n[{view.LocationName}] {view.LocationDescription}");
        output.WriteLine($"HP {view.HitPoints}/{view.MaximumHitPoints} | Gold {view.Gold} | XP {view.Experience}");
        output.WriteLine("Destinations: " + string.Join(", ", view.Destinations.Select(d => $"{d.Id}={d.Name}")));
        foreach (var npc in view.Npcs) output.WriteLine($"NPC: {npc.Id}={npc.Name}");
        if (view.Conversation is not null) output.WriteLine($"Talking to {view.Conversation.NpcName}; choose 1–3");
        if (view.Encounter is not null)
            output.WriteLine($"Enemy: {view.Encounter.Name} ({view.Encounter.HitPoints}/{view.Encounter.MaximumHitPoints} HP)");
    }
}
```

### 5.20 `Program.cs`

```csharp
using Tutorial014.ConsoleUI;
using Tutorial014.Engine;
using Tutorial014.Dialogue;
using Tutorial014.Factories;
using Tutorial014.World;

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
        Check(game.Snapshot().Npcs.Single().Name == "Elena the Weaver", "NPC visible at town");
        Check(!game.Execute(new(GameActionKind.Choose, 1)).Succeeded, "Cannot choose without conversation");
        Check(!game.Execute(new(GameActionKind.Talk, 999)).Succeeded, "Unknown NPC rejected");
        Check(game.Execute(new(GameActionKind.Talk, NpcFactory.WeaverId)).Succeeded, "Talk to Elena");
        Check(game.Snapshot().Conversation?.Choices.Count == 3, "Dialogue choices visible");
        Check(game.Execute(new(GameActionKind.Choose, 1)).Succeeded, "Quest accepted through dialogue");
        Check(!game.Execute(new(GameActionKind.Choose, 1)).Succeeded, "Quest cannot be accepted twice");
        Check(game.Execute(new(GameActionKind.Choose, 3)).Succeeded, "Goodbye ends conversation");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Travel to forest");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.SpiderClearingId)).Succeeded, "Travel to clearing");
        Check(game.Snapshot().CanAttack && !game.Snapshot().CanTravel, "UI combat flags");
        Check(game.Snapshot().Npcs.Count == 0, "Town NPC absent from clearing");
        Check(!game.Execute(new(GameActionKind.Talk, NpcFactory.WeaverId)).Succeeded, "Remote dialogue blocked");
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
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.TownSquareId)).Succeeded, "Return to town");
        Check(game.Execute(new(GameActionKind.Talk, NpcFactory.WeaverId)).Succeeded, "Talk after travel");
        Check(!game.Execute(new(GameActionKind.Choose, 2)).Succeeded, "Cannot turn in with one silk");
        Check(game.Execute(new(GameActionKind.Save)).Succeeded, "Save quest state");
        Check(game.Execute(new(GameActionKind.Load)).Succeeded, "Load quest state");
        Check(game.Snapshot().Conversation is null, "Conversation cleared after load");
        Check(game.Execute(new(GameActionKind.Talk, NpcFactory.WeaverId)).Succeeded, "Reopen conversation after load");
        Check(game.Snapshot().Quests.Single().Status == Tutorial014.Models.QuestStatus.Active, "Quest persisted");
        Check(notifications > 0, "StateChanged published");
        var scripted = new ConsoleGame(new GameController(save));
        using var reader = new StringReader("help\nstatus\nquit\n".Replace("\\n", "\n"));
        using var writer = new StringWriter();
        scripted.Run(reader, writer);
        Check(writer.ToString().Contains("Goodbye"), "Console adapter runs");
        new ConsoleGame(new GameController(save)).Run(new StringReader(""), TextWriter.Null);
        Console.WriteLine("ALL NPC DIALOGUE CHECKS PASSED");
    }
    finally
    {
        if (File.Exists(save)) File.Delete(save);
    }
}
```

## 6. Example playthrough

Start the game and enter:

```text
look
talk 1
choose 1
quests
choose 3
go 2
go 3
attack
attack
attack
attack
```

The spider may require more or fewer attacks depending on random damage. After victory, return to the Forest Trail (`go 2`) and revisit Spider Clearing (`go 3`) for another spider. Collect **three Spider Silk**, then return to Town Square:

```text
go 2
go 1
talk 1
choose 2
quests
status
save
quit
```

If you have not collected three Spider Silk, Elena refuses the turn-in. If you have enough, `Quest.TurnIn()` consumes the silk, grants the quest's Gold and XP, and marks it completed.

**Important:** The existing quest awards its own reward in addition to the individual monster rewards. Rewards cannot be claimed twice for the same completed quest.

## 7. Conversation lifecycle

### Starting dialogue

`GameActionKind.Talk` takes an NPC ID. The controller checks that the NPC is **in the player's current location**, that the player is alive, and that no living enemy is present.

### Selecting a choice

`GameActionKind.Choose` takes a numeric choice ID. It succeeds only if a valid conversation is active. Choice 1 attempts to accept the quest, choice 2 attempts to turn it in, and choice 3 ends the conversation.

### Leaving the location

Successful travel clears the active conversation. The NPC is not remotely accessible from another location.

### Loading a saved game

Loading reconstructs the player, quests, inventory, world, and encounter. It **clears the current conversation**. This prevents a stale dialogue from surviving after a location change caused by loading.

### Why conversation state is not serialized

The conversation in this chapter has no persistent branches or irreversible decisions beyond quest status. Its menu can be rebuilt from the NPC catalog, and the player can talk to Elena again after loading.

For more complex narrative games, dialogue flags, reputation, relationship scores, and branching choices should be saved explicitly, with versioned validation.

## 8. UI-neutral integration with MonoGame + Myra

The controller returns:

```csharp
GameView view = controller.Snapshot();

foreach (NpcView npc in view.Npcs)
{
    // Create a Myra button labeled npc.Name.
    // On click, dispatch GameAction(GameActionKind.Talk, npc.Id).
}

if (view.Conversation is not null)
{
    // Show view.Conversation.NpcName and Greeting.
    foreach (var choice in view.Conversation.Choices)
    {
        // Create a Myra dialogue choice button.
        // On click, dispatch GameAction(GameActionKind.Choose, choice.Id).
    }
}
```

These comments describe **widget wiring**, not a standalone compilable Myra form. Use the actual widget creation and layout API from your MonoGame + Myra project.

Every click should dispatch exactly one action:

```csharp
GameResponse result = controller.Execute(
    new GameAction(GameActionKind.Choose, 1));

// Refresh labels, quest list, dialogue choices and message log
// from result.View and result.Messages.
```

The same engine methods power the console commands. No quest logic belongs inside the Myra click handlers.

## 9. Automated self-tests

Run:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet run -- --self-test
```

The checks include:

- NPC visibility at Town Square.
- Invalid choice without a conversation.
- Unknown NPC rejection.
- Opening Elena's conversation.
- Accepting a quest through dialogue.
- Preventing repeated acceptance.
- Clearing dialogue when saying goodbye.
- Blocking remote dialogue from Spider Clearing.
- Preserving mid-battle save/load and victory rewards.
- Rejecting quest turn-in without enough Spider Silk.
- Clearing conversation after load.
- Preserving quest status in JSON.
- Maintaining console adapter and EOF handling.

Expected final line:

```text
ALL NPC DIALOGUE CHECKS PASSED
```

The test suite is provided as source. It has **not been executed against a .NET SDK in this authoring environment**.

## 10. C# versus C++

| Topic | C# | C++ |
|---|---|---|
| Immutable NPC definition | `record NpcDefinition` | Immutable struct/class |
| Dialogue choices | `IReadOnlyList<DialogueChoice>` | `std::vector<DialogueChoice>` |
| Optional conversation | `ConversationView?` | `std::optional<ConversationView>` |
| NPC lookup | `NpcFactory.FindAtLocation()` | Registry lookup |
| Action dispatch | `switch (GameActionKind)` | `switch (enum class)` |
| UI notifications | `EventHandler<GameResponse>` | Observer / signal |
| Save DTOs | `System.Text.Json` | JSON serialization library |

## 11. VS Code debugging

1. Open `Tutorial014` in VS Code.
2. Set a breakpoint in `CommandParser.Parse()` and enter `talk 1`.
3. Step into `GameController.Execute()`.
4. Inspect `_world.CurrentLocationId` and `NpcFactory.FindAtLocation()`.
5. Step through `GameActionKind.Choose` with `choose 1`.
6. Watch `Quest.Status` change from `NotStarted` to `Active`.
7. Set a breakpoint in `Quest.TurnIn()` and test an incomplete quest.
8. Save, load, and verify `_talkingTo` is reset to `null`.

## 12. Exercises

- [ ] **A — Second NPC:** Add a guard NPC in Forest Trail.
- [ ] **B — Dialogue branches:** Change NPC responses depending on quest status.
- [ ] **C — NPC inventory:** Let an NPC show items they want.
- [ ] **D — Dialogue flags:** Persist a boolean representing whether Elena has been met.
- [ ] **E — More quests:** Add a second quest and associate it with a different NPC.
- [ ] **F — NPC ID validation:** Test `talk 0`, `talk -1`, and `talk abc`.
- [ ] **G — Myra dialogue panel:** Display `ConversationView` choices in a Myra `VerticalStackPanel`.
- [ ] **H — Conversation transitions:** Replace the fixed menu with a dialogue-node graph.
- [ ] **I — Localization:** Store dialogue keys separately from displayed English strings.
- [ ] **J — Regression tests:** Convert built-in checks to xUnit and add NPC quest-completion tests.

## 13. Engineering notes

- This chapter introduces **one fully defined NPC** and a three-choice conversation. It is an extensible foundation, not a complete dialogue authoring system.
- NPC catalog definitions are in code. A later version can load dialogue content from JSON or other data files.
- Conversation choices do not advance game time or trigger monster counterattacks.
- The `StateChanged` event is synchronous; subscribers should not throw exceptions.
- Save format remains **version 3** because no new durable gameplay fields are introduced.
- Myra GUI widgets are **not packaged** in the standalone console ZIP.

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
git add .
git commit -m "Tutorial 014: NPC dialogue and quest givers"
```

## 15. Knowledge check

1. Why does the NPC catalog store location IDs?
2. Why does the controller reject dialogue with an NPC in another location?
3. What is the difference between `NpcDefinition` and `ConversationView`?
4. Why does loading clear the current conversation?
5. Why is quest acceptance implemented in the engine rather than the console?
6. What prevents repeated quest rewards?
7. How would you make NPC dialogue dependent on previous decisions?
8. How can the same dialogue engine drive both Terminal and Myra?

## 16. Chapter summary

The world now has a quest giver with whom the player can speak. NPCs are tied to locations, dialogue choices drive quest transitions, and the UI remains separate from gameplay rules.

**Implemented:** NPC catalog, location-aware conversations, quest acceptance and turn-in through dialogue, UI snapshots, and regression checks.

**Not yet implemented:** full branching dialogue graphs, persistent narrative flags, NPC schedules, multiple quest givers, or a compiled Myra dialogue window.

### Next: Tutorial 015 — Shops, Trading, and Economy

We'll add merchants, item prices, buying and selling, transaction validation, and a UI-neutral shop view that works with the same controller architecture.
