# SOSCSRPG C# Tutorial 006 — Factories, Item Catalogs, and Monster Loot

**Series:** Learn C# by building SOSCSRPG  
**Platform:** Ubuntu 26.04 · .NET 10 · Visual Studio Code  
**Prerequisite:** Tutorial 005 — Collections, Generics, and Inventory  
**Project:** `Tutorial006` · Standalone console application  
**Language:** English

> This is an original teaching project inspired by Scott Lilly's SOSCSRPG design. It is not a copy of the upstream source. The lessons build an engine that can later be connected to MonoGame + Myra.

## 1. What you will build

In Tutorial 005, `Program.cs` directly constructed every sword and potion. That becomes difficult to maintain when an RPG contains hundreds of items and monsters.

In this chapter, we will introduce **factories** and **loot tables**:

```text
Program.cs
   |
   +--> ItemFactory ----------> GameItem definitions
   |      |                       |
   |      +--> Rusty Sword        +--> Weapon
   |      +--> Healing Potion     +--> HealingPotion
   |      +--> Spider Silk        +--> QuestItem
   |      +--> Wolf Fang          +--> QuestItem
   |
   +--> MonsterFactory -------> Monster instances
   |      |
   |      +--> Venomous Spider --> LootTable --> ItemFactory
   |      +--> Forest Wolf ------> LootTable --> ItemFactory
   |
   +--> Player ----------------> Inventory
```

**Important distinction:** an item definition is shared and immutable; a monster is a fresh, mutable instance. Two spiders must not accidentally share hit points.

### Learning objectives

- Create factory classes with `static` methods.
- Use `Dictionary<int, GameItem>` as a canonical item catalog.
- Introduce item and monster ID constants.
- Build a `LootEntry` and a `LootTable`.
- Use an injectable random-number generator for repeatable tests.
- Roll loot when a monster is defeated and add it to inventory.
- Write small checks for identity, isolation, probability boundaries, and inventory quantities.
- Understand how factories support the future MonoGame + Myra UI.

## 2. VS Code and .NET environment

Open **Terminal → New Terminal** in Visual Studio Code (`Ctrl` + backtick).

Verify your tools:

```bash
cat /etc/os-release | grep PRETTY_NAME
env -u DOTNET_ROOT /usr/bin/dotnet --version
code --version | head -n 1
code --list-extensions | grep -Ei 'ms-dotnettools.csharp|ms-dotnettools.csdevkit' || true
```

Install the C# extension if necessary:

```bash
code --install-extension ms-dotnettools.csharp
```

If the native .NET 10 SDK is not installed and is available from your configured Ubuntu repositories:

```bash
sudo apt update
sudo apt install dotnet-sdk-10.0 git
```

We use `/usr/bin/dotnet` and clear `DOTNET_ROOT` to avoid the conflicting Snap-based runtime encountered earlier in this Linux development environment.

## 3. Create the Tutorial006 project

```bash
mkdir -p ~/Projects/SOSCSRPG-Tutorials
cd ~/Projects/SOSCSRPG-Tutorials

env -u DOTNET_ROOT /usr/bin/dotnet new console \
  --name Tutorial006 \
  --framework net10.0

cd Tutorial006
mkdir -p Models Factories
code .
```

Your finished directory structure will be:

```text
Tutorial006/
├── Factories/
│   ├── ItemFactory.cs
│   └── MonsterFactory.cs
├── Models/
│   ├── GameItem.cs
│   ├── Weapon.cs
│   ├── HealingPotion.cs
│   ├── QuestItem.cs
│   ├── Inventory.cs
│   ├── Player.cs
│   ├── LootEntry.cs
│   ├── LootTable.cs
│   └── Monster.cs
├── Program.cs
└── Tutorial006.csproj
```

The default SDK-style project automatically compiles `.cs` files under the project directory.

## 4. Item IDs: replace magic numbers

A **magic number** is a raw number whose meaning is not obvious.

Instead of writing:

```csharp
// What does 3 mean?
// var potion = ItemFactory.GetItem(3);
```

we will write:

```csharp
var potion = ItemFactory.GetItem(ItemFactory.MinorHealingPotionId);
```

The ID still exists, but it now has a meaningful name.

## 5. Define the item model

Create `Models/GameItem.cs`:

```csharp
namespace Tutorial006.Models;

public abstract class GameItem
{
    public int Id { get; }
    public string Name { get; }
    public int Price { get; }

    protected GameItem(int id, string name, int price)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Item name is required.", nameof(name));

        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price));

        Id = id;
        Name = name;
        Price = price;
    }

    public virtual string Describe() => $"{Name} ({Price} Gold)";
}
```

Create `Models/Weapon.cs`:

```csharp
namespace Tutorial006.Models;

public sealed class Weapon : GameItem
{
    public int MinimumDamage { get; }
    public int MaximumDamage { get; }

    public Weapon(
        int id, string name, int price,
        int minimumDamage, int maximumDamage)
        : base(id, name, price)
    {
        if (minimumDamage < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumDamage));

        if (maximumDamage < minimumDamage)
            throw new ArgumentOutOfRangeException(nameof(maximumDamage));

        MinimumDamage = minimumDamage;
        MaximumDamage = maximumDamage;
    }

    public override string Describe() =>
        $"{base.Describe()} | Damage {MinimumDamage}-{MaximumDamage}";
}
```

Create `Models/HealingPotion.cs`:

```csharp
namespace Tutorial006.Models;

public sealed class HealingPotion : GameItem
{
    public int AmountToHeal { get; }

    public HealingPotion(int id, string name, int price, int amountToHeal)
        : base(id, name, price)
    {
        if (amountToHeal <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountToHeal));

        AmountToHeal = amountToHeal;
    }

    public override string Describe() =>
        $"{base.Describe()} | Restores {AmountToHeal} HP";
}
```

Create `Models/QuestItem.cs`:

```csharp
namespace Tutorial006.Models;

public sealed class QuestItem : GameItem
{
    public QuestItem(int id, string name, int price)
        : base(id, name, price)
    {
    }
}
```

These definitions are immutable after construction. Their inventory quantities will be tracked separately.

## 6. ItemFactory: one catalog, shared definitions

Create `Factories/ItemFactory.cs`:

```csharp
using Tutorial006.Models;

namespace Tutorial006.Factories;

public static class ItemFactory
{
    public const int RustySwordId = 1;
    public const int IronSwordId = 2;
    public const int MinorHealingPotionId = 3;
    public const int SpiderSilkId = 4;
    public const int WolfFangId = 5;

    private static readonly Dictionary<int, GameItem> Items = new()
    {
        [RustySwordId] = new Weapon(
            RustySwordId, "Rusty Sword", 12, 2, 5),

        [IronSwordId] = new Weapon(
            IronSwordId, "Iron Sword", 45, 5, 9),

        [MinorHealingPotionId] = new HealingPotion(
            MinorHealingPotionId, "Minor Healing Potion", 8, 5),

        [SpiderSilkId] = new QuestItem(
            SpiderSilkId, "Spider Silk", 2),

        [WolfFangId] = new QuestItem(
            WolfFangId, "Wolf Fang", 4)
    };

    public static GameItem GetItem(int itemId)
    {
        if (!Items.TryGetValue(itemId, out GameItem? item))
            throw new KeyNotFoundException($"Unknown item ID: {itemId}");

        return item;
    }

    public static T GetItem<T>(int itemId) where T : GameItem
    {
        GameItem item = GetItem(itemId);

        if (item is not T typedItem)
            throw new InvalidOperationException(
                $"Item {itemId} is not a {typeof(T).Name}.");

        return typedItem;
    }

    public static IReadOnlyList<GameItem> GetAllItems() =>
        Items.Values.OrderBy(item => item.Id).ToList().AsReadOnly();
}
```

### New C# concepts

**Static class:** `ItemFactory` does not need an object instance.

```csharp
GameItem item = ItemFactory.GetItem(ItemFactory.SpiderSilkId);
```

**Generic method constraint:**

```csharp
Weapon sword = ItemFactory.GetItem<Weapon>(
    ItemFactory.RustySwordId);
```

The clause `where T : GameItem` restricts the type parameter to game-item types.

**Canonical definitions:** repeated calls for the same ID return the **same object**:

```csharp
GameItem a = ItemFactory.GetItem(ItemFactory.SpiderSilkId);
GameItem b = ItemFactory.GetItem(ItemFactory.SpiderSilkId);

Console.WriteLine(ReferenceEquals(a, b)); // True
```

Because the item objects are immutable, sharing them is safe in this lesson.

> A `readonly` dictionary field prevents reassignment of the field; it does **not** make the dictionary itself immutable. We keep the dictionary `private` and expose only lookup and snapshot methods.

## 7. Inventory: collect item definitions and quantities

Create `Models/Inventory.cs`:

```csharp
namespace Tutorial006.Models;

public sealed class Inventory
{
    private readonly Dictionary<int, (GameItem Item, int Quantity)> _entries = new();

    public int Count(int itemId) =>
        _entries.TryGetValue(itemId, out var entry) ? entry.Quantity : 0;

    public void Add(GameItem item, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        if (_entries.TryGetValue(item.Id, out var existing))
        {
            if (!ReferenceEquals(existing.Item, item))
                throw new InvalidOperationException(
                    $"Conflicting definitions for item ID {item.Id}.");

            _entries[item.Id] = (item, checked(existing.Quantity + quantity));
        }
        else
        {
            _entries.Add(item.Id, (item, quantity));
        }
    }

    public bool Remove(int itemId, int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        if (!_entries.TryGetValue(itemId, out var existing) ||
            existing.Quantity < quantity)
            return false;

        int remaining = existing.Quantity - quantity;

        if (remaining == 0)
            _entries.Remove(itemId);
        else
            _entries[itemId] = (existing.Item, remaining);

        return true;
    }

    public IReadOnlyList<(GameItem Item, int Quantity)> GetEntries() =>
        _entries.Values
            .OrderBy(entry => entry.Item.Id)
            .ToList()
            .AsReadOnly();
}
```

This continues the inventory architecture from Tutorial 005.

## 8. Player: inventory and gold rewards

Create `Models/Player.cs`:

```csharp
namespace Tutorial006.Models;

public sealed class Player
{
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }
    public int Gold { get; private set; }
    public int ExperiencePoints { get; private set; }

    public Inventory Inventory { get; } = new();

    public Player(string name, int maximumHitPoints = 20)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Player name is required.", nameof(name));

        if (maximumHitPoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumHitPoints));

        Name = name;
        MaximumHitPoints = maximumHitPoints;
        HitPoints = maximumHitPoints;
    }

    public void TakeDamage(int damage)
    {
        if (damage < 0)
            throw new ArgumentOutOfRangeException(nameof(damage));

        HitPoints = (int)Math.Max(0L, (long)HitPoints - damage);
    }

    public void GainRewards(int gold, int experience)
    {
        if (gold < 0)
            throw new ArgumentOutOfRangeException(nameof(gold));

        if (experience < 0)
            throw new ArgumentOutOfRangeException(nameof(experience));

        // Calculate both results before changing either property.
        int newGold = checked(Gold + gold);
        int newExperience = checked(ExperiencePoints + experience);

        Gold = newGold;
        ExperiencePoints = newExperience;
    }
}
```

Notice the use of `checked`. If the player's gold would overflow an `int`, the code throws rather than silently wrapping around.

## 9. LootEntry: one possible drop

Create `Models/LootEntry.cs`:

```csharp
namespace Tutorial006.Models;

public sealed class LootEntry
{
    public int ItemId { get; }
    public int DropChancePercent { get; }
    public int MinimumQuantity { get; }
    public int MaximumQuantity { get; }

    public LootEntry(
        int itemId,
        int dropChancePercent,
        int minimumQuantity = 1,
        int maximumQuantity = 1)
    {
        if (itemId <= 0)
            throw new ArgumentOutOfRangeException(nameof(itemId));

        if (dropChancePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(dropChancePercent));

        if (minimumQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumQuantity));

        if (maximumQuantity < minimumQuantity)
            throw new ArgumentOutOfRangeException(nameof(maximumQuantity));

        ItemId = itemId;
        DropChancePercent = dropChancePercent;
        MinimumQuantity = minimumQuantity;
        MaximumQuantity = maximumQuantity;
    }
}
```

For example:

```csharp
new LootEntry(
    itemId: ItemFactory.SpiderSilkId,
    dropChancePercent: 75,
    minimumQuantity: 1,
    maximumQuantity: 2);
```

This means a **75% chance** of receiving **one or two** Spider Silk.

## 10. LootTable: probability and deterministic testing

Create `Models/LootTable.cs`:

```csharp
using Tutorial006.Factories;

namespace Tutorial006.Models;

public sealed class LootTable
{
    private readonly List<LootEntry> _entries = new();

    public void Add(LootEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Fail early if the item ID is not in the catalog.
        _ = ItemFactory.GetItem(entry.ItemId);

        _entries.Add(entry);
    }

    public IReadOnlyList<(GameItem Item, int Quantity)> Roll(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var drops = new List<(GameItem Item, int Quantity)>();

        foreach (LootEntry entry in _entries)
        {
            // Next(100) yields an integer from 0 through 99.
            int roll = random.Next(100);

            if (roll >= entry.DropChancePercent)
                continue;

            int quantity = entry.MinimumQuantity == entry.MaximumQuantity
                ? entry.MinimumQuantity
                : (int)random.NextInt64(
                    entry.MinimumQuantity,
                    (long)entry.MaximumQuantity + 1);

            GameItem item = ItemFactory.GetItem(entry.ItemId);
            drops.Add((item, quantity));
        }

        return drops.AsReadOnly();
    }
}
```

### How probability works

```csharp
int roll = random.Next(100); // 0 through 99

if (roll < 75)
{
    // 75 out of 100 possible values: a 75% drop chance.
}
```

Each `LootEntry` is rolled **independently**. Therefore, a monster can drop multiple items. This is different from a weighted table that selects exactly one reward.

### Why pass `Random` into `Roll()`?

```csharp
Random random = new Random(42);
var drops = lootTable.Roll(random);
```

Passing a random generator makes the dependency explicit and allows repeatable test scenarios.

**Caution:** `Random` is for game simulation, not security-sensitive randomness. A seed produces a repeatable sequence for a given implementation, but do not treat that sequence as a permanent cross-runtime compatibility contract.

## 11. Monster: a fresh instance per encounter

Create `Models/Monster.cs`:

```csharp
namespace Tutorial006.Models;

public sealed class Monster
{
    public int Id { get; }
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }
    public int RewardGold { get; }
    public int RewardExperience { get; }
    public LootTable Loot { get; }

    public bool IsAlive => HitPoints > 0;

    public Monster(
        int id,
        string name,
        int maximumHitPoints,
        int rewardGold,
        int rewardExperience,
        LootTable loot)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Monster name is required.", nameof(name));

        if (maximumHitPoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumHitPoints));

        if (rewardGold < 0)
            throw new ArgumentOutOfRangeException(nameof(rewardGold));

        if (rewardExperience < 0)
            throw new ArgumentOutOfRangeException(nameof(rewardExperience));

        ArgumentNullException.ThrowIfNull(loot);

        Id = id;
        Name = name;
        MaximumHitPoints = maximumHitPoints;
        HitPoints = maximumHitPoints;
        RewardGold = rewardGold;
        RewardExperience = rewardExperience;
        Loot = loot;
    }

    public void TakeDamage(int damage)
    {
        if (damage < 0)
            throw new ArgumentOutOfRangeException(nameof(damage));

        HitPoints = (int)Math.Max(0L, (long)HitPoints - damage);
    }
}
```

`HitPoints` changes during combat, so `Monster` is a mutable encounter object.

## 12. MonsterFactory: create spiders and wolves

Create `Factories/MonsterFactory.cs`:

```csharp
using Tutorial006.Models;

namespace Tutorial006.Factories;

public static class MonsterFactory
{
    public const int VenomousSpiderId = 1;
    public const int ForestWolfId = 2;

    public static Monster CreateMonster(int monsterId) =>
        monsterId switch
        {
            VenomousSpiderId => CreateSpider(),
            ForestWolfId => CreateWolf(),
            _ => throw new KeyNotFoundException(
                $"Unknown monster ID: {monsterId}")
        };

    private static Monster CreateSpider()
    {
        var loot = new LootTable();

        loot.Add(new LootEntry(
            ItemFactory.SpiderSilkId,
            dropChancePercent: 75,
            minimumQuantity: 1,
            maximumQuantity: 2));

        loot.Add(new LootEntry(
            ItemFactory.MinorHealingPotionId,
            dropChancePercent: 20));

        return new Monster(
            id: VenomousSpiderId,
            name: "Venomous Spider",
            maximumHitPoints: 10,
            rewardGold: 5,
            rewardExperience: 10,
            loot: loot);
    }

    private static Monster CreateWolf()
    {
        var loot = new LootTable();

        loot.Add(new LootEntry(
            ItemFactory.WolfFangId,
            dropChancePercent: 80));

        loot.Add(new LootEntry(
            ItemFactory.MinorHealingPotionId,
            dropChancePercent: 10));

        return new Monster(
            id: ForestWolfId,
            name: "Forest Wolf",
            maximumHitPoints: 18,
            rewardGold: 9,
            rewardExperience: 15,
            loot: loot);
    }
}
```

### The factory pattern

Instead of:

```csharp
// Repeated, hard-coded construction scattered across the program.
var spider = new Monster(/* many arguments */);
```

we write:

```csharp
Monster spider = MonsterFactory.CreateMonster(
    MonsterFactory.VenomousSpiderId);
```

The factory centralizes the rules for constructing a valid monster.

**Critical:** Each call creates a new monster **and** a new loot table:

```csharp
Monster first = MonsterFactory.CreateMonster(
    MonsterFactory.VenomousSpiderId);

Monster second = MonsterFactory.CreateMonster(
    MonsterFactory.VenomousSpiderId);

Console.WriteLine(ReferenceEquals(first, second)); // False
```

That prevents damage to one spider from changing another spider's HP.

## 13. Program.cs — a complete encounter and loot example

Replace `Program.cs`:

```csharp
using Tutorial006.Factories;
using Tutorial006.Models;

Console.WriteLine("=== SOSCSRPG Tutorial 006 ===");
Console.WriteLine("Factories, Item Catalogs, and Monster Loot");
Console.WriteLine();

Console.WriteLine("Available items:");
foreach (GameItem item in ItemFactory.GetAllItems())
{
    Console.WriteLine($"- [{item.Id}] {item.Describe()}");
}

var player = new Player("Adventurer");
var random = new Random(42);

Monster spider = MonsterFactory.CreateMonster(
    MonsterFactory.VenomousSpiderId);

Console.WriteLine();
Console.WriteLine($"A {spider.Name} appears!");
Console.WriteLine($"HP: {spider.HitPoints}/{spider.MaximumHitPoints}");

Console.WriteLine("The player attacks for 12 damage.");
spider.TakeDamage(12);

if (!spider.IsAlive)
{
    Console.WriteLine($"{spider.Name} defeated!");

    player.GainRewards(
        spider.RewardGold,
        spider.RewardExperience);

    Console.WriteLine(
        $"Rewards: {spider.RewardGold} Gold, " +
        $"{spider.RewardExperience} XP");

    foreach (var (item, quantity) in spider.Loot.Roll(random))
    {
        player.Inventory.Add(item, quantity);
        Console.WriteLine($"Loot: {item.Name} x{quantity}");
    }
}

Console.WriteLine();
Console.WriteLine("Player inventory:");
foreach (var (item, quantity) in player.Inventory.GetEntries())
{
    Console.WriteLine($"- {item.Name} x{quantity}");
}

Console.WriteLine($"Gold: {player.Gold}");
Console.WriteLine($"XP: {player.ExperiencePoints}");

Console.WriteLine();
Console.WriteLine("Creating two independent spiders:");

Monster spiderA = MonsterFactory.CreateMonster(
    MonsterFactory.VenomousSpiderId);

Monster spiderB = MonsterFactory.CreateMonster(
    MonsterFactory.VenomousSpiderId);

spiderA.TakeDamage(4);

Console.WriteLine($"Spider A HP: {spiderA.HitPoints}");
Console.WriteLine($"Spider B HP: {spiderB.HitPoints}");

RunChecks();

static void RunChecks()
{
    Console.WriteLine();
    Console.WriteLine("=== ENGINE CHECKS ===");

    GameItem silk1 = ItemFactory.GetItem(ItemFactory.SpiderSilkId);
    GameItem silk2 = ItemFactory.GetItem(ItemFactory.SpiderSilkId);

    Check(ReferenceEquals(silk1, silk2),
        "ItemFactory returns canonical item definitions");

    var first = MonsterFactory.CreateMonster(
        MonsterFactory.VenomousSpiderId);

    var second = MonsterFactory.CreateMonster(
        MonsterFactory.VenomousSpiderId);

    Check(!ReferenceEquals(first, second),
        "MonsterFactory creates independent instances");

    first.TakeDamage(3);

    Check(first.HitPoints == 7 && second.HitPoints == 10,
        "Monster HP is isolated");

    var guaranteed = new LootTable();
    guaranteed.Add(new LootEntry(
        ItemFactory.SpiderSilkId,
        dropChancePercent: 100,
        minimumQuantity: 2,
        maximumQuantity: 2));

    var drops = guaranteed.Roll(new Random(123));

    Check(drops.Count == 1 && drops[0].Quantity == 2,
        "100% loot always drops the fixed quantity");

    var impossible = new LootTable();
    impossible.Add(new LootEntry(
        ItemFactory.WolfFangId,
        dropChancePercent: 0));

    Check(impossible.Roll(new Random(123)).Count == 0,
        "0% loot never drops");

    var inventory = new Inventory();
    inventory.Add(silk1, 2);
    inventory.Add(silk2, 3);

    Check(inventory.Count(ItemFactory.SpiderSilkId) == 5,
        "Repeated loot stacks into one inventory entry");

    Check(!inventory.Remove(ItemFactory.SpiderSilkId, 6),
        "Cannot remove more than the owned quantity");

    Check(inventory.Count(ItemFactory.SpiderSilkId) == 5,
        "Failed removal preserves inventory state");

    Console.WriteLine("ALL ENGINE CHECKS PASSED");
}

static void Check(bool condition, string description)
{
    if (!condition)
        throw new InvalidOperationException($"FAIL: {description}");

    Console.WriteLine($"PASS: {description}");
}
```

### Expected behavior

The loot result is probabilistic, so the precise drops depend on the random sequence. The deterministic checks at the end do **not** depend on a specific random sequence.

A run will show:

```text
=== SOSCSRPG Tutorial 006 ===
Factories, Item Catalogs, and Monster Loot

Available items:
- [1] Rusty Sword (12 Gold) | Damage 2-5
- [2] Iron Sword (45 Gold) | Damage 5-9
- [3] Minor Healing Potion (8 Gold) | Restores 5 HP
- [4] Spider Silk (2 Gold)
- [5] Wolf Fang (4 Gold)

A Venomous Spider appears!
HP: 10/10
The player attacks for 12 damage.
Venomous Spider defeated!
Rewards: 5 Gold, 10 XP

Player inventory:
...
Gold: 5
XP: 10

Creating two independent spiders:
Spider A HP: 6
Spider B HP: 10

=== ENGINE CHECKS ===
PASS: ItemFactory returns canonical item definitions
PASS: MonsterFactory creates independent instances
PASS: Monster HP is isolated
PASS: 100% loot always drops the fixed quantity
PASS: 0% loot never drops
PASS: Repeated loot stacks into one inventory entry
PASS: Cannot remove more than the owned quantity
PASS: Failed removal preserves inventory state
ALL ENGINE CHECKS PASSED
```

The `Loot:` lines and player inventory entries may vary.

## 14. Build and run in the VS Code terminal

```bash
cd ~/Projects/SOSCSRPG-Tutorials/Tutorial006

env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run --no-build
```

For a stricter compile check:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet build -warnaserror
```

The final line should be:

```text
ALL ENGINE CHECKS PASSED
```

**Note:** The checks verify selected invariants. They are not yet a full unit-test suite.

## 15. Debug the factory and loot pipeline

Open the `Tutorial006` folder in VS Code, then:

1. Place a breakpoint in `MonsterFactory.CreateSpider()`.
2. Press **F5** to start debugging.
3. Use **F11** to step into the factory and watch the `LootTable` being constructed.
4. Place a breakpoint inside `LootTable.Roll()` on `int roll = random.Next(100);`.
5. Inspect `roll` and `entry.DropChancePercent`.
6. Place a breakpoint in `Inventory.Add()` to see the dropped item added to the player's inventory.

| Shortcut | Action |
|---|---|
| `F5` | Start / continue |
| `F9` | Toggle breakpoint |
| `F10` | Step over |
| `F11` | Step into |
| `Shift+F5` | Stop |

If the debugger does not launch, first verify the terminal build succeeds. Check the C# extension's output and whether the debugger inherited an unwanted `DOTNET_ROOT` value.

## 16. C# versus C++: factories and lookup tables

| Concept | C# | C++ |
|---|---|---|
| Static factory | `static class ItemFactory` | Namespace functions or static class members |
| Dictionary | `Dictionary<int, GameItem>` | `std::unordered_map<int, ...>` |
| Shared immutable definition | Shared managed object reference | `shared_ptr<const T>` or another ownership strategy |
| Fresh encounter | `new Monster(...)` | Construct object / `unique_ptr<Monster>` |
| Generic method | `GetItem<T>() where T : GameItem` | Function template with constraints |
| Switch expression | `id switch { ... }` | `switch`, factories, or dispatch tables |
| Random numbers | `System.Random` | `<random>` engines and distributions |
| Read-only property | `public int Id { get; }` | Private member plus const getter |

**Architectural point:** `ItemFactory` reuses immutable definitions, while `MonsterFactory` creates new mutable encounters. That distinction matters more than the language syntax.

## 17. Design caveats before building a full RPG

This chapter intentionally keeps the engine small. Several production concerns remain:

- **Loot awarded once:** `Program.cs` grants rewards after checking `!spider.IsAlive`, but calling that reward block twice could grant loot twice. A later combat service should track whether rewards were already claimed.
- **Inventory transaction:** if multiple drops are awarded and a later addition throws, earlier drops have already been added. A production implementation should validate or stage the full reward transaction.
- **Shared loot tables:** we create one loot table per monster; its `Add()` method remains public. A later design can expose an immutable loot-table definition.
- **Persistence:** save item IDs and quantities, then resolve IDs through `ItemFactory` when loading. Do not serialize shared item objects redundantly.
- **Data-driven content:** hard-coded factory definitions are ideal for learning, but JSON or another content format can become useful for larger games.
- **RNG boundaries:** random rolls are testable by injecting `Random`, but a more advanced design may inject an `IRandomSource` interface to control every roll precisely.

These are deliberate boundaries of Tutorial 006, not completed features.

## 18. Hands-on exercises

- [ ] **Exercise A — Giant Spider:** Add a third monster with 25 HP, 15 Gold, and 20 XP.
- [ ] **Exercise B — Rare drop:** Add a new `QuestItem` called `Spider Venom` with a 5% drop chance.
- [ ] **Exercise C — Loot quantities:** Change Spider Silk to drop between 1 and 3 units. Run the program several times with different seeds.
- [ ] **Exercise D — Factory errors:** Call `ItemFactory.GetItem(999)` inside a `try`/`catch` block. Explain the `KeyNotFoundException`.
- [ ] **Exercise E — Generic lookup:** Retrieve the Rusty Sword using `GetItem<Weapon>()`. Try requesting it as a `HealingPotion` and handle the exception.
- [ ] **Exercise F — Wolf encounter:** Create a Forest Wolf, defeat it, and award its loot.
- [ ] **Exercise G — Guaranteed drop:** Add a 100% drop to the wolf's loot table and verify it always appears.
- [ ] **Exercise H — Reward protection:** Design a `ClaimRewards()` method that can succeed only once per defeated monster. Decide which object should own that state.

### Challenge: create a real test project

The self-checks are useful for learning, but a production engine should use automated tests.

Consider a separate project:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet new xunit \
  --name Tutorial006.Tests \
  --framework net10.0
```

A test project may require NuGet network access. We will introduce a complete test-project workflow in a later chapter.

## 19. GitHub workflow

Create `.gitignore`:

```bash
cat > .gitignore <<'EOF'
bin/
obj/
.vs/
EOF
```

Commit your work:

```bash
git init
git add Program.cs Models Factories Tutorial006.csproj .gitignore
git commit -m "Tutorial 006: item factories monster factories and loot tables"
```

Configure your Git author identity if Git asks for it.

## 20. Knowledge check

1. Why does `ItemFactory` return a shared object, but `MonsterFactory` creates a new object?
2. What does `where T : GameItem` mean?
3. Why does `LootTable.Roll()` accept a `Random` parameter?
4. What is the difference between an independent drop chance and a weighted-choice table?
5. Why should the inventory store item IDs and quantities separately from item definitions?
6. Why does `checked()` matter when accumulating gold or quantities?
7. What could go wrong if rewards are granted twice?
8. Which part of the engine should later notify the MonoGame + Myra UI that inventory changed?

## 21. Chapter summary

You have now built the foundations for data-driven RPG content:

```text
ItemFactory
    -> Canonical GameItem definitions

MonsterFactory
    -> Fresh Monster encounters
        -> LootTable
            -> LootEntry
                -> ItemFactory lookup

Defeated Monster
    -> Gold and Experience
    -> Loot rolls
        -> Player.Inventory
```

**Implemented:** factories, item IDs, item catalog, spider and wolf definitions, probabilistic loot, inventory integration, deterministic boundary checks, and VS Code debugging guidance.

**Not yet implemented:** full combat turns, quest progression, merchant transactions, JSON save/load, MonoGame rendering, or Myra widgets.

### Next: Tutorial 007 — Quests, Objectives, and Rewards

We'll build a quest model, track required items (such as **three Spider Silk**), complete objectives, and award quest rewards without granting them twice.
