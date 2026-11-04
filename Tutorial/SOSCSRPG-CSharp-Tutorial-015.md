# SOSCSRPG C# Tutorial 015 — Shops, Trading, and Economy

**Series:** SOSCSRPG C# Tutorials  
**Platform:** Ubuntu 26.04 · .NET 10 · Visual Studio Code  
**Prerequisite:** Tutorial 014 — NPC Dialogue and Quest Givers  
**Language:** English · **Project:** `Tutorial015`

> Original educational implementation inspired by Scott Lilly's SOSCSRPG. This ZIP contains a standalone console project and a UI-neutral controller suitable for a future MonoGame + Myra frontend. A compiled Myra GUI is not included.

## 1. Learning objectives

In this chapter, Gold becomes a usable in-game resource. You'll learn to:

1. Define merchant inventories using immutable `ShopDefinition` and `ShopOffer` records.
2. Distinguish **buy prices** from **sell prices**.
3. Implement validated buying and selling with quantities.
4. Prevent negative quantities, integer overflow, insufficient funds, and overselling.
5. Keep merchant access **location-aware** and block trading during combat.
6. Add `Shop`, `Buy`, `Sell`, and `LeaveShop` to the UI-neutral action system.
7. Expose shop offers and the active merchant in `GameView` for Myra.
8. Persist purchased and sold items using the existing JSON save format version 3.
9. Test round-trip persistence and state preservation on rejected transactions.

## 2. Project setup

Extract the ZIP and open the project in VS Code:

```bash
cd Tutorial015
code .
env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run
```

To create the directory layout manually:

```bash
mkdir -p ~/Projects/SOSCSRPG-Tutorials
cd ~/Projects/SOSCSRPG-Tutorials
env -u DOTNET_ROOT /usr/bin/dotnet new console --name Tutorial015 --framework net10.0
cd Tutorial015
mkdir -p Models Factories World Dialogue Economy Combat Persistence Engine ConsoleUI
```

The source ZIP already includes every file from Tutorial 014, updated for Tutorial 015.

## 3. Trading commands

| Command | Description |
|---|---|
| `shop 1` | Open Mara's Trading Post in Town Square |
| `buy 5` | Buy one Healing Herb |
| `buy 5 2` | Buy two Healing Herbs |
| `sell 4` | Sell one Spider Silk |
| `sell 4 3` | Sell three Spider Silk |
| `leave` | Close the current shop |
| `inventory` | Show carried items |
| `status` | Show Gold, HP, XP, and weapon |
| `save` / `load` | Save or restore Gold and inventory |
| `talk 1` / `choose 1` | Speak with Elena and accept the Spider Silk quest |
| `go 2` / `go 3` | Travel to the Forest Trail / Spider Clearing |
| `attack` | Fight spiders to earn Gold and Spider Silk |

### Shop catalog

| Item ID | Item | Buy | Sell |
|---|---|---:|---:|
| 4 | Spider Silk | 6 Gold | 2 Gold |
| 5 | Healing Herb | 4 Gold | 1 Gold |
| 6 | Rope | 8 Gold | 3 Gold |

The merchant has unlimited stock **in this tutorial**. Shop stock is static catalog data, not a per-merchant inventory, and is not persisted. Stock quantities and restocking schedules can be added later.

Healing Herbs are **inventory items only** for now. Tutorial 016 will add consumable effects.

## 4. Architecture

```text
ConsoleGame / future Myra UI
             |
             v
       GameAction
        |    |   |
      Shop  Buy Sell
        |    |   |
        v    v   v
        GameController
           |      |
           |      +--> GameView.Shops / ActiveShop
           v
     TradingService
        |       |
        v       v
    Player.Gold  Inventory
           |
           v
     SaveGameService
        JSON v3
```

`ShopCatalog` describes merchant location and prices. `TradingService` enforces transaction rules. `GameController` verifies that the player is physically at the merchant and has an open shop session. The console parser handles text input but **never changes Gold or inventory directly**.

## 5. Transaction validation

### Buying

For an item with unit price `p` and quantity `q`:

\[
\text{TotalCost} = p \times q
\]

The service validates the item, quantity, Gold balance, and maximum inventory quantity **before** mutating state. The multiplication uses `long` so that large input values cannot silently overflow `int`.

```csharp
long total = (long)offer.BuyPrice * quantity;
if (total > int.MaxValue || player.Gold < total)
    return new(false, "Not enough Gold or total price is too large.");
```

A successful purchase spends Gold and adds the item to inventory.

### Selling

For a sale:

\[
\text{SaleRevenue} = \text{SellPrice} \times \text{Quantity}
\]

The service validates that the player owns the items and that the resulting Gold balance fits within `int`. A successful sale removes the items and credits Gold.

### Why different buy and sell prices?

If buying and selling both used the same price, the player could exchange items for Gold without cost. A price spread gives the shop a meaningful economic role and prevents a trivial free-money exploit when combined with other discounts or rewards.

### Transaction boundaries

The example validates expected failures before updating state. However, inventory and Gold change notifications are **synchronous**. If an external event subscriber throws during an update, the operation could be interrupted after partial mutation. Production systems should implement atomic state transitions and publish notifications only after committing.

## 6. Complete source code

The full source follows, with one code block for every project file.

### 6.1 `Tutorial015.csproj`

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

### 6.2 `Models/GameItem.cs`

```csharp
namespace Tutorial015.Models;

public sealed record GameItem(int Id, string Name);
```

### 6.3 `Models/Weapon.cs`

```csharp
namespace Tutorial015.Models;

public sealed record Weapon(int Id, string Name, int MinimumDamage, int MaximumDamage)
{
    public int RollDamage(Random random) => random.Next(MinimumDamage, MaximumDamage + 1);
}
```

### 6.4 `Models/Inventory.cs`

```csharp
namespace Tutorial015.Models;

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

### 6.5 `Models/Player.cs`

```csharp
namespace Tutorial015.Models;

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

    public bool TrySpendGold(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (Gold < amount) return false;
        Gold -= amount;
        if (amount != 0)
            StatsChanged?.Invoke(this, new PlayerStatsChangedEventArgs(Gold, ExperiencePoints));
        return true;
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

### 6.6 `Models/Quest.cs`

```csharp
namespace Tutorial015.Models;

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

### 6.7 `Factories/ItemFactory.cs`

```csharp
using Tutorial015.Models;

namespace Tutorial015.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public const int HealingHerbId = 5;
    public const int RopeId = 6;

    public static GameItem SpiderSilk { get; } = new(SpiderSilkId, "Spider Silk");
    public static GameItem HealingHerb { get; } = new(HealingHerbId, "Healing Herb");
    public static GameItem Rope { get; } = new(RopeId, "Rope");

    public static GameItem Get(int id) => id switch
    {
        SpiderSilkId => SpiderSilk,
        HealingHerbId => HealingHerb,
        RopeId => Rope,
        _ => throw new KeyNotFoundException($"Unknown item ID: {id}")
    };
}
```

### 6.8 `Factories/QuestFactory.cs`

```csharp
using Tutorial015.Models;

namespace Tutorial015.Factories;

public static class QuestFactory
{
    public static Quest CreateSpiderSilkQuest() =>
        new(id: 1, title: "Collect Spider Silk",
            requiredItemId: ItemFactory.SpiderSilkId,
            requiredQuantity: 3, rewardGold: 10, rewardExperience: 20);
}
```

### 6.9 `World/Location.cs`

```csharp
namespace Tutorial015.World;

public sealed record Location(int Id, string Name, string Description,
    IReadOnlyList<int> NeighborIds, int? MonsterId = null);
```

### 6.10 `World/MonsterFactory.cs`

```csharp
namespace Tutorial015.World;

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

### 6.11 `World/WorldFactory.cs`

```csharp
namespace Tutorial015.World;

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

### 6.12 `World/GameWorld.cs`

```csharp
namespace Tutorial015.World;

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

### 6.13 `Dialogue/NpcFactory.cs`

```csharp
using Tutorial015.World;

namespace Tutorial015.Dialogue;

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

### 6.14 `Economy/ShopCatalog.cs`

```csharp
using Tutorial015.Factories;
using Tutorial015.Models;
using Tutorial015.World;

namespace Tutorial015.Economy;

// Prices are game-design values, not real-world currency.
// Buy and sell prices differ to prevent risk-free trading loops.
public sealed record ShopOffer(int ItemId, string Name, int BuyPrice, int SellPrice);
public sealed record ShopDefinition(int Id, string Name, int LocationId,
    IReadOnlyList<ShopOffer> Offers);

public static class ShopCatalog
{
    public const int TownMerchantId = 1;
    public static ShopDefinition TownMerchant { get; } = new(
        TownMerchantId, "Mara's Trading Post", WorldFactory.TownSquareId,
        new[]
        {
            new ShopOffer(ItemFactory.SpiderSilkId, "Spider Silk", 6, 2),
            new ShopOffer(ItemFactory.HealingHerbId, "Healing Herb", 4, 1),
            new ShopOffer(ItemFactory.RopeId, "Rope", 8, 3)
        });

    public static ShopDefinition? FindAtLocation(int shopId, int locationId) =>
        shopId == TownMerchant.Id && locationId == TownMerchant.LocationId
            ? TownMerchant : null;

    public static IReadOnlyList<ShopDefinition> AtLocation(int locationId) =>
        locationId == TownMerchant.LocationId
            ? new[] { TownMerchant } : Array.Empty<ShopDefinition>();
}
```

### 6.15 `Economy/TradingService.cs`

```csharp
using Tutorial015.Factories;
using Tutorial015.Models;

namespace Tutorial015.Economy;

public sealed record TradeResult(bool Succeeded, string Message);

public static class TradingService
{
    public static TradeResult Buy(Player player, ShopDefinition shop, int itemId, int quantity)
    {
        if (quantity <= 0) return new(false, "Quantity must be positive.");
        ShopOffer? offer = shop.Offers.FirstOrDefault(o => o.ItemId == itemId);
        if (offer is null) return new(false, "Item is not sold here.");

        long total = (long)offer.BuyPrice * quantity;
        if (total > int.MaxValue || player.Gold < total)
            return new(false, "Not enough Gold or total price is too large.");
        if ((long)player.Inventory.Count(itemId) + quantity > int.MaxValue)
            return new(false, "Inventory quantity limit reached.");

        // Validate all expected failure conditions before mutation.
        if (!player.TrySpendGold((int)total))
            return new(false, "Not enough Gold.");

        player.Inventory.Add(ItemFactory.Get(itemId), quantity);
        return new(true, $"Bought {quantity} x {offer.Name} for {total} Gold.");
    }

    public static TradeResult Sell(Player player, ShopDefinition shop, int itemId, int quantity)
    {
        if (quantity <= 0) return new(false, "Quantity must be positive.");
        ShopOffer? offer = shop.Offers.FirstOrDefault(o => o.ItemId == itemId);
        if (offer is null) return new(false, "Merchant does not buy this item.");
        if (player.Inventory.Count(itemId) < quantity)
            return new(false, "Not enough items to sell.");

        long total = (long)offer.SellPrice * quantity;
        if (total > int.MaxValue || (long)player.Gold + total > int.MaxValue)
            return new(false, "Gold limit reached.");

        // Inventory and Gold updates are local synchronous operations.
        // A production engine should batch notifications and commit atomically.
        if (!player.Inventory.Remove(itemId, quantity))
            return new(false, "Not enough items to sell.");

        player.ReceiveRewards((int)total, 0);
        return new(true, $"Sold {quantity} x {offer.Name} for {total} Gold.");
    }
}
```

### 6.16 `Combat/CombatService.cs`

```csharp
using Tutorial015.Factories;
using Tutorial015.Models;
using Tutorial015.World;

namespace Tutorial015.Combat;

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

### 6.17 `Persistence/SaveData.cs`

```csharp
namespace Tutorial015.Persistence;

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

### 6.18 `Persistence/SaveGameService.cs`

```csharp
using System.Text.Json;
using Tutorial015.Factories;
using Tutorial015.Models;
using Tutorial015.World;

namespace Tutorial015.Persistence;

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

    private static GameItem ResolveItem(int id)
    {
        try { return ItemFactory.Get(id); }
        catch (KeyNotFoundException ex)
        {
            throw new InvalidDataException($"Unknown item ID: {id}", ex);
        }
    }
}
```

### 6.19 `Engine/GameController.cs`

```csharp
using Tutorial015.Combat;
using Tutorial015.Dialogue;
using Tutorial015.Economy;
using Tutorial015.Factories;
using Tutorial015.Models;
using Tutorial015.Persistence;
using Tutorial015.World;

namespace Tutorial015.Engine;

public enum GameActionKind
{
    Help, Look, Map, Travel, Attack, Status, Inventory, Quests,
    Talk, Choose, Shop, Buy, Sell, LeaveShop, Save, Load
}

public sealed record GameAction(GameActionKind Kind, int? Argument = null, int Quantity = 1);

// Immutable view models: the UI cannot mutate engine objects through these snapshots.
public sealed record DestinationView(int Id, string Name);
public sealed record InventoryView(string Name, int Quantity);
public sealed record QuestView(string Title, QuestStatus Status, int Progress, int Required);
public sealed record NpcView(int Id, string Name);
public sealed record ShopView(int Id, string Name, IReadOnlyList<ShopOffer> Offers);
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
    IReadOnlyList<ShopView> Shops, ShopView? ActiveShop,
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
    private int? _shoppingAt;

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
                new InventoryView(ItemFactory.Get(e.ItemId).Name, e.Quantity)).ToArray(),
            _quests.Select(q => new QuestView(q.Title, q.Status, q.Progress(_player), q.RequiredQuantity)).ToArray(),
            monster is null ? null : new EncounterView(monster.Name, monster.HitPoints, monster.MaximumHitPoints, monster.IsAlive),
            NpcFactory.AtLocation(_world.CurrentLocationId).Select(n => new NpcView(n.Id, n.Name)).ToArray(),
            CurrentConversation(),
            ShopCatalog.AtLocation(_world.CurrentLocationId)
                .Select(shop => new ShopView(shop.Id, shop.Name, shop.Offers.ToArray())).ToArray(),
            ActiveShop() is ShopDefinition active
                ? new ShopView(active.Id, active.Name, active.Offers.ToArray()) : null,
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

    private ShopDefinition? ActiveShop() =>
        _shoppingAt is int id
            ? ShopCatalog.FindAtLocation(id, _world.CurrentLocationId)
            : null;

    public GameResponse Execute(GameAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var messages = new List<string>();
        bool success = true;
        switch (action.Kind)
        {
            case GameActionKind.Help:
                messages.Add("help | look | map | go <id> | attack | status | inventory | quests | talk <id> | choose <id> | shop <id> | buy <itemId> [qty] | sell <itemId> [qty] | leave | save | load | quit");
                break;
            case GameActionKind.Look:
                messages.Add(_world.CurrentLocation.Name + ": " + _world.CurrentLocation.Description);
                break;
            case GameActionKind.Map:
                messages.AddRange(WorldFactory.All().Select(l => $"{l.Id}: {l.Name} -> [{string.Join(", ", l.NeighborIds)}]"));
                break;
            case GameActionKind.Travel:
                success = action.Argument is int id && _player.IsAlive && _world.TravelTo(id);
                if (success)
                {
                    _talkingTo = null;
                    _shoppingAt = null;
                }
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
                    _shoppingAt = null;
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
            case GameActionKind.Shop:
                var shop = action.Argument is int shopId
                    ? ShopCatalog.FindAtLocation(shopId, _world.CurrentLocationId) : null;
                success = shop is not null && _player.IsAlive &&
                    _world.CurrentEncounter?.IsAlive != true;
                if (success)
                {
                    _shoppingAt = shop!.Id;
                    _talkingTo = null;
                    messages.Add($"Welcome to {shop.Name}.");
                    messages.AddRange(shop.Offers.Select(o =>
                        $"#{o.ItemId} {o.Name}: buy {o.BuyPrice} Gold, sell {o.SellPrice} Gold"));
                }
                else messages.Add("Shop unavailable at this location.");
                break;
            case GameActionKind.Buy:
            case GameActionKind.Sell:
                ShopDefinition? activeShop = ActiveShop();
                success = activeShop is not null && _player.IsAlive &&
                    _world.CurrentEncounter?.IsAlive != true && action.Argument is > 0;
                if (!success)
                {
                    messages.Add("Open a shop first (shop <id>), outside combat.");
                    break;
                }
                TradeResult trade = action.Kind == GameActionKind.Buy
                    ? TradingService.Buy(_player, activeShop!, action.Argument!.Value, action.Quantity)
                    : TradingService.Sell(_player, activeShop!, action.Argument!.Value, action.Quantity);
                success = trade.Succeeded;
                messages.Add(trade.Message);
                break;
            case GameActionKind.LeaveShop:
                success = _shoppingAt is not null;
                _shoppingAt = null;
                messages.Add(success ? "You leave the shop." : "You are not shopping.");
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
                    _shoppingAt = null; // Shop sessions are also transient.
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

### 6.20 `ConsoleUI/CommandParser.cs`

```csharp
namespace Tutorial015.ConsoleUI;

public enum GameCommandKind
{
    Help, Look, Map, Travel, Attack, Status, Inventory, Quests,
    Talk, Choose, Shop, Buy, Sell, LeaveShop, Save, Load, Quit, Invalid
}

public sealed record GameCommand(GameCommandKind Kind, int? Argument = null, int Quantity = 1);

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
            "shop" => GameCommandKind.Shop,
            "buy" => GameCommandKind.Buy,
            "sell" => GameCommandKind.Sell,
            "leave" => GameCommandKind.LeaveShop,
            "save" => GameCommandKind.Save,
            "load" => GameCommandKind.Load,
            "quit" or "exit" => GameCommandKind.Quit,
            _ => GameCommandKind.Invalid
        };

        if (kind is GameCommandKind.Buy or GameCommandKind.Sell)
        {
            if (tokens.Length is < 2 or > 3 ||
                !int.TryParse(tokens[1], out int itemId) || itemId <= 0)
                return new(GameCommandKind.Invalid);
            int quantity = 1;
            if (tokens.Length == 3 &&
                (!int.TryParse(tokens[2], out quantity) || quantity <= 0))
                return new(GameCommandKind.Invalid);
            return new(kind, itemId, quantity);
        }

        bool needsArgument = kind is GameCommandKind.Travel or GameCommandKind.Talk
            or GameCommandKind.Choose or GameCommandKind.Shop;
        if (needsArgument)
            return tokens.Length == 2 &&
                   int.TryParse(tokens[1], out int id) && id > 0
                ? new(kind, id) : new(GameCommandKind.Invalid);

        return tokens.Length == 1 ? new(kind) : new(GameCommandKind.Invalid);
    }
}
```

### 6.21 `ConsoleUI/ConsoleGame.cs`

```csharp
using Tutorial015.Engine;

namespace Tutorial015.ConsoleUI;

public sealed class ConsoleGame
{
    private readonly GameController _controller;

    public ConsoleGame(GameController controller) =>
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));

    public void Run(TextReader input, TextWriter output)
    {
        output.WriteLine("SOSCSRPG Tutorial 015 — Shops, Trading, and Economy");
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
            GameCommandKind.Shop => GameActionKind.Shop,
            GameCommandKind.Buy => GameActionKind.Buy,
            GameCommandKind.Sell => GameActionKind.Sell,
            GameCommandKind.LeaveShop => GameActionKind.LeaveShop,
            GameCommandKind.Save => GameActionKind.Save,
            GameCommandKind.Load => GameActionKind.Load,
            _ => throw new ArgumentException("Unsupported command.", nameof(command))
        }, command.Argument, command.Quantity);

    public static void RenderView(GameView view, TextWriter output)
    {
        output.WriteLine($"\n[{view.LocationName}] {view.LocationDescription}");
        output.WriteLine($"HP {view.HitPoints}/{view.MaximumHitPoints} | Gold {view.Gold} | XP {view.Experience}");
        output.WriteLine("Destinations: " + string.Join(", ", view.Destinations.Select(d => $"{d.Id}={d.Name}")));
        foreach (var npc in view.Npcs) output.WriteLine($"NPC: {npc.Id}={npc.Name}");
        foreach (var shop in view.Shops)
            output.WriteLine($"Shop: {shop.Id}={shop.Name}");
        if (view.ActiveShop is not null)
        {
            output.WriteLine($"Shopping at {view.ActiveShop.Name}:");
            foreach (var offer in view.ActiveShop.Offers)
                output.WriteLine($"  #{offer.ItemId} {offer.Name} | buy {offer.BuyPrice} | sell {offer.SellPrice}");
        }
        if (view.Conversation is not null) output.WriteLine($"Talking to {view.Conversation.NpcName}; choose 1–3");
        if (view.Encounter is not null)
            output.WriteLine($"Enemy: {view.Encounter.Name} ({view.Encounter.HitPoints}/{view.Encounter.MaximumHitPoints} HP)");
    }
}
```

### 6.22 `Program.cs`

```csharp
using Tutorial015.ConsoleUI;
using Tutorial015.Engine;
using Tutorial015.Economy;
using Tutorial015.Factories;
using Tutorial015.World;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    SelfTest();
    return;
}

var controller = new GameController(Path.Combine(Environment.CurrentDirectory, "savegame.json"));
new ConsoleGame(controller).Run(Console.In, Console.Out);

static void SelfTest()
{
    static void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }

    Check(CommandParser.Parse("buy 5 2") == new GameCommand(GameCommandKind.Buy, 5, 2),
        "Buy parser supports quantity");
    Check(CommandParser.Parse("sell 4") == new GameCommand(GameCommandKind.Sell, 4),
        "Sell parser defaults to one");
    Check(CommandParser.Parse("buy 5 0").Kind == GameCommandKind.Invalid,
        "Zero quantity rejected by parser");
    Check(CommandParser.Parse("buy 5 -1").Kind == GameCommandKind.Invalid,
        "Negative quantity rejected");
    Check(CommandParser.Parse("shop 1").Kind == GameCommandKind.Shop,
        "Shop command recognized");

    string path = Path.Combine(Path.GetTempPath(),
        "soscsrpg-t015-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        var game = new GameController(path, new Random(42));
        int notifications = 0;
        game.StateChanged += (_, _) => notifications++;
        Check(game.Snapshot().Shops.Single().Name == "Mara's Trading Post",
            "Town shop is visible");
        Check(!game.Execute(new(GameActionKind.Buy, ItemFactory.HealingHerbId)).Succeeded,
            "Cannot buy without opening a shop");
        Check(game.Execute(new(GameActionKind.Shop, ShopCatalog.TownMerchantId)).Succeeded,
            "Open shop");
        Check(!game.Execute(new(GameActionKind.Buy, ItemFactory.HealingHerbId)).Succeeded,
            "Cannot buy without enough Gold");
        Check(!game.Execute(new(GameActionKind.Sell, ItemFactory.SpiderSilkId)).Succeeded,
            "Cannot sell unowned items");
        Check(!game.Execute(new(GameActionKind.Buy, 999)).Succeeded,
            "Unknown item rejected");
        Check(!game.Execute(new(GameActionKind.Buy, ItemFactory.HealingHerbId, int.MaxValue)).Succeeded,
            "Large purchase rejected without overflow");
        Check(game.Execute(new(GameActionKind.LeaveShop)).Succeeded,
            "Close shop");

        Check(game.Execute(new(GameActionKind.Talk, 1)).Succeeded,
            "Talk to Elena");
        Check(game.Execute(new(GameActionKind.Choose, 1)).Succeeded,
            "Accept quest");
        Check(game.Execute(new(GameActionKind.Choose, 3)).Succeeded,
            "End conversation");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded,
            "Travel to forest");
        Check(!game.Execute(new(GameActionKind.Shop, 1)).Succeeded,
            "Remote shop blocked");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.SpiderClearingId)).Succeeded,
            "Travel to spider");
        Check(!game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded,
            "Travel locked in combat");

        int turns = 0;
        while (game.Snapshot().CanAttack && turns++ < 30)
            game.Execute(new(GameActionKind.Attack));
        Check(game.Snapshot().Encounter?.Alive == false, "Spider defeated");
        Check(game.Snapshot().Gold == 5 &&
              game.Snapshot().Inventory.Single().Quantity == 1,
              "Monster rewards awarded");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded,
            "Return to trail");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.TownSquareId)).Succeeded,
            "Return to town");
        Check(game.Execute(new(GameActionKind.Shop, 1)).Succeeded,
            "Reopen merchant");

        Check(game.Execute(new(GameActionKind.Buy, ItemFactory.HealingHerbId)).Succeeded,
            "Buy Healing Herb for 4 Gold");
        Check(game.Snapshot().Gold == 1 &&
              game.Snapshot().Inventory.Any(i => i.Name == "Healing Herb" && i.Quantity == 1),
              "Gold deducted and inventory updated");
        Check(!game.Execute(new(GameActionKind.Buy, ItemFactory.RopeId)).Succeeded,
            "Insufficient Gold does not mutate state");
        Check(game.Execute(new(GameActionKind.Sell, ItemFactory.SpiderSilkId)).Succeeded,
            "Sell Spider Silk for 2 Gold");
        Check(game.Snapshot().Gold == 3 &&
              game.Snapshot().Inventory.All(i => i.Name != "Spider Silk"),
              "Sell updates Gold and removes item");
        Check(game.Execute(new(GameActionKind.Save)).Succeeded,
            "Save economy state");
        Check(game.Execute(new(GameActionKind.Sell, ItemFactory.HealingHerbId)).Succeeded,
            "Change state after saving");
        Check(game.Execute(new(GameActionKind.Load)).Succeeded,
            "Load saved state");
        Check(game.Snapshot().Gold == 3 &&
              game.Snapshot().Inventory.Single().Name == "Healing Herb",
              "Gold and merchant items restored");
        Check(game.Snapshot().ActiveShop is null,
            "Shop session not serialized");
        Check(!game.Execute(new(GameActionKind.Sell, ItemFactory.HealingHerbId)).Succeeded,
            "Cannot trade after load without reopening shop");
        Check(game.Execute(new(GameActionKind.Shop, 1)).Succeeded,
            "Reopen shop after load");
        Check(!game.Execute(new(GameActionKind.Sell, ItemFactory.HealingHerbId, 2)).Succeeded,
            "Cannot sell more than owned");
        Check(notifications > 0, "StateChanged notifications");

        using var reader = new StringReader("help\nshop 1\nquit\n");
        using var output = new StringWriter();
        new ConsoleGame(new GameController(path)).Run(reader, output);
        Check(output.ToString().Contains("Mara's Trading Post"),
            "Console shop interface renders");
        Console.WriteLine("ALL SHOP AND ECONOMY CHECKS PASSED");
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
```

## 7. Playthrough: Earn, buy, and sell

Start the game:

```text
help
look
shop 1
buy 5
```

You begin with **0 Gold**, so the purchase is rejected.

Close the shop, accept Elena's quest, and head into the forest:

```text
leave
talk 1
choose 1
choose 3
go 2
go 3
attack
attack
attack
```

Continue attacking until the spider is defeated. Each spider rewards **5 Gold**, **10 XP**, and **one Spider Silk**. Return to Town Square:

```text
go 2
go 1
shop 1
buy 5
inventory
status
sell 4
status
save
quit
```

After one spider victory, buying one Healing Herb for 4 Gold leaves 1 Gold. Selling one Spider Silk for 2 Gold raises the balance to 3 Gold.

**Quest trade-off:** Spider Silk is also required for Elena's quest. Selling it is allowed, but reduces quest progress. You must collect additional silk before turning in the quest.

## 8. Save/Load and the merchant session

Tutorial 015 continues using **JSON version 3**. The durable state already includes Gold and item quantities; therefore no schema change is needed.

```text
Saved:
  Player Gold
  Player inventory quantities
  Quest progress/status
  Player HP and current location
  Current monster encounter and reward state

Not saved:
  Which shop window is currently open
  Which NPC conversation is currently open
  Merchant stock (unlimited static catalog)
```

After `load`, the controller clears `_shoppingAt` and `_talkingTo`. The UI should rebuild its view from the newly loaded `GameView`.

If the player wants to trade again after loading, they must enter `shop 1`.

## 9. MonoGame + Myra integration

The same controller can power Myra widgets:

```csharp
using Tutorial015.Engine;

GameView view = controller.Snapshot();

// Build merchant buttons from view.Shops.
// Clicking merchant 1:
GameResponse open = controller.Execute(
    new GameAction(GameActionKind.Shop, 1));

// Build offer rows from open.View.ActiveShop?.Offers.
// Clicking "Buy 2 Healing Herbs":
GameResponse bought = controller.Execute(
    new GameAction(GameActionKind.Buy, 5, 2));

// Refresh Gold, inventory, shop offers, and message log
// from bought.View and bought.Messages.
```

**UI design suggestion:** A Myra shop panel can have an item-name label, buy price, sell price, quantity selector, and Buy/Sell buttons. Disable Buy when the player lacks Gold; disable Sell when they lack the requested quantity. Still validate all transactions in `TradingService` because GUI validation is never a substitute for engine rules.

The ZIP includes the console adapter, **not** a fully built MonoGame/Myra shop window.

## 10. Automated tests

Run:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet run -- --self-test
```

Checks cover:

- `buy <id> [quantity]` and `sell <id> [quantity]` parsing.
- Zero/negative quantities and unknown item IDs.
- Buying without first opening a shop.
- Buying with insufficient Gold.
- Selling items not owned.
- Rejecting very large transactions without integer overflow.
- Blocking remote shop access.
- Earning Gold and loot through combat.
- Buying an item and deducting Gold.
- Selling an item and crediting Gold.
- Saving, changing state, and restoring Gold/inventory.
- Clearing active shop state after loading.
- Rejecting trades after load until the merchant is reopened.
- Rendering shop information in the console adapter.

The expected final line is:

```text
ALL SHOP AND ECONOMY CHECKS PASSED
```

**Verification status:** The source includes these tests, but the C# project has not been compiled or executed against a .NET SDK in this authoring environment. Run the commands above on your Ubuntu machine.

## 11. VS Code debugging

1. Open the `Tutorial015` folder.
2. Set a breakpoint in `CommandParser.Parse()` and type `buy 5 2`.
3. Inspect `GameCommand.Argument` and `Quantity`.
4. Step into `GameController.Execute()` and inspect `ActiveShop()`.
5. Step into `TradingService.Buy()` and inspect `total`.
6. Inspect `Player.Gold` and `Inventory.Count(5)` before and after buying.
7. Set a breakpoint in `TradingService.Sell()` and test selling more items than owned.
8. Save, load, and confirm the shop session has been cleared.

## 12. C# versus C++

| Concept | C# | C++ |
|---|---|---|
| Shop catalog | `record ShopDefinition` | `struct` / immutable class |
| Price calculation | `long` intermediate | `std::int64_t` |
| Optional offer | `ShopOffer?` | `std::optional<ShopOffer>` |
| Inventory storage | `Dictionary<int,int>` | `std::unordered_map<int,int>` |
| UI action | `record GameAction` | Value-type command struct |
| Transaction result | `record TradeResult` | Result struct / expected type |
| JSON persistence | `System.Text.Json` | External JSON library |

## 13. Exercises

- [ ] **A — Shop stock:** Track finite stock quantities for each offer.
- [ ] **B — Restocking:** Restore merchant stock after a configurable number of game days.
- [ ] **C — Discounts:** Add a reputation-based discount without allowing negative prices.
- [ ] **D — Potion usage:** Make Healing Herbs restore HP (covered more deeply in Tutorial 016).
- [ ] **E — More merchants:** Add a second merchant to Forest Trail with different prices.
- [ ] **F — Quest items:** Prevent selling items needed for active quests.
- [ ] **G — Myra shop panel:** Display offers and Buy/Sell buttons.
- [ ] **H — Overflow tests:** Test a sale when the player's Gold is near `int.MaxValue`.
- [ ] **I — Transaction rollback:** Make Gold/inventory changes atomic even when observers throw.
- [ ] **J — xUnit:** Extract self-tests into a separate test project.

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
git commit -m "Tutorial 015: shops trading and economy"
```

## 15. Knowledge check

1. Why does `TradingService` use `long` for the price calculation?
2. Why is a shop session tied to the current location?
3. Why does loading clear the active shop?
4. Why do buy and sell prices differ?
5. Why does the engine recheck affordability even if the UI disables Buy?
6. How does selling Spider Silk affect Elena's quest?
7. What must change to persist limited merchant stock?
8. What would make buying and selling truly atomic?

## 16. Chapter summary

Your RPG now supports a basic merchant economy. The player can earn Gold by fighting monsters, buy items, sell loot, and preserve those transactions across save/load.

**Implemented:** merchant catalog, price spreads, validated quantities, buy/sell actions, Gold deduction, inventory updates, location-aware shop access, UI-neutral shop snapshots, and automated self-checks.

**Not yet implemented:** limited merchant stock, variable prices, equipment purchases, consumable effects, or a compiled Myra shop interface.

### Next: Tutorial 016 — Equipment, Potions, and Character Progression

We'll turn inventory items into usable equipment and consumables, then add character progression, levels, and stronger monsters.
