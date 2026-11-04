# SOSCSRPG C# Tutorial 005 — Collections, Generics, and Inventory

**Learning series:** SOSCSRPG with C# and Visual Studio Code  
**Platform:** Ubuntu 26.04 · .NET 10 · VS Code  
**Prerequisite:** Tutorial 004 — Inheritance, Interfaces, and RPG Items  
**Project:** `Tutorial005` (standalone console application)

> This is an original teaching implementation inspired by Scott Lilly's SOSCSRPG architecture. It is not a verbatim reproduction of the original tutorial. We will integrate MonoGame and Myra in later chapters.

## 1. Learning objectives

In this lesson, you will:

1. Use `List<T>` and `Dictionary<TKey, TValue>`.
2. Understand generics, `foreach`, and LINQ.
3. Distinguish **item definitions** from **owned quantities**.
4. Create an `Inventory` class with safe add, remove, and count operations.
5. Consume healing potions **only when healing succeeds**.
6. Keep a weapon equipped without consuming its inventory entry.
7. Debug inventory state in VS Code and run a few automated checks.

## 2. Environment setup

Open **Visual Studio Code → Terminal → New Terminal** (`Ctrl` + backtick).

Check your tools:

```bash
cat /etc/os-release | grep PRETTY_NAME
env -u DOTNET_ROOT /usr/bin/dotnet --version
code --version | head -n 1
code --list-extensions | grep -Ei 'ms-dotnettools.csharp|ms-dotnettools.csdevkit' || true
```

Install the C# extension if needed:

```bash
code --install-extension ms-dotnettools.csharp
```

On a new Ubuntu machine where the native .NET 10 SDK is available from configured package repositories:

```bash
sudo apt update
sudo apt install dotnet-sdk-10.0 git
```

**Why `env -u DOTNET_ROOT`?** On our Linux development setup, a previously installed Snap .NET SDK caused runtime conflicts. Using `/usr/bin/dotnet` without the `DOTNET_ROOT` override selects the working native SDK.

## 3. Create a new project

Keep the lessons separate so you can review each design independently.

```bash
mkdir -p ~/Projects/SOSCSRPG-Tutorials
cd ~/Projects/SOSCSRPG-Tutorials

env -u DOTNET_ROOT /usr/bin/dotnet new console \
  --name Tutorial005 \
  --framework net10.0

cd Tutorial005
mkdir -p Models
code .
```

Project structure:

```text
Tutorial005/
├── Models/
│   ├── GameItem.cs
│   ├── Weapon.cs
│   ├── HealingPotion.cs
│   ├── Inventory.cs
│   └── Player.cs
├── Program.cs
└── Tutorial005.csproj
```

The SDK-style `.csproj` automatically includes all `.cs` files under the project folder.

## 4. Collections and generics

### `List<T>` — ordered collection

```csharp
List<string> names = new() { "Sword", "Potion", "Spider Silk" };
names.Add("Wolf Fang");

foreach (string name in names)
{
    Console.WriteLine(name);
}
```

`T` is a **type parameter**. `List<string>` holds strings; `List<GameItem>` holds game items.

### `Dictionary<TKey, TValue>` — key/value lookup

```csharp
Dictionary<int, int> quantities = new()
{
    [1] = 1, // Item ID 1: one sword
    [3] = 3  // Item ID 3: three potions
};

Console.WriteLine(quantities[3]); // 3
```

A dictionary is useful when an item ID uniquely identifies a type of item and we need fast quantity lookup.

### Why not store quantity directly on `GameItem`?

An item definition describes **what an item is**: name, price, and behavior. An inventory describes **how many a player owns**.

Two players may own different quantities of the same potion definition. Putting `Quantity` on a shared definition would mix those responsibilities.

## 5. GameItem — shared item definition

Create `Models/GameItem.cs`:

```csharp
namespace Tutorial005.Models;

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
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price));

        Id = id;
        Name = name;
        Price = price;
    }

    public virtual string Describe() => $"{Name} ({Price} Gold)";
}
```

## 6. Weapon — derived item

Create `Models/Weapon.cs`:

```csharp
namespace Tutorial005.Models;

public sealed class Weapon : GameItem
{
    public int MinimumDamage { get; }
    public int MaximumDamage { get; }

    public Weapon(int id, string name, int price,
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

## 7. HealingPotion — consumable item definition

Create `Models/HealingPotion.cs`:

```csharp
namespace Tutorial005.Models;

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

In Tutorial 004, we explored the `IUsableItem` interface. Here we simplify the demonstration to concentrate on inventory ownership and atomic consumption. Later tutorials will reintroduce action interfaces for the full RPG engine.

## 8. Inventory — quantities and ownership

Create `Models/Inventory.cs`:

```csharp
using System.Collections.ObjectModel;

namespace Tutorial005.Models;

public sealed class Inventory
{
    private readonly Dictionary<int, (GameItem Item, int Quantity)> _items = new();

    public int Count(int itemId) =>
        _items.TryGetValue(itemId, out var entry) ? entry.Quantity : 0;

    public bool Contains(int itemId, int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        return Count(itemId) >= quantity;
    }

    public void Add(GameItem item, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        if (_items.TryGetValue(item.Id, out var entry))
        {
            if (!ReferenceEquals(entry.Item, item))
                throw new InvalidOperationException(
                    $"Item ID {item.Id} is already registered with a different definition.");

            _items[item.Id] = (item, checked(entry.Quantity + quantity));
        }
        else
        {
            _items.Add(item.Id, (item, quantity));
        }
    }

    public bool Remove(int itemId, int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        if (!_items.TryGetValue(itemId, out var entry) ||
            entry.Quantity < quantity)
        {
            return false;
        }

        int remaining = entry.Quantity - quantity;

        if (remaining == 0)
            _items.Remove(itemId);
        else
            _items[itemId] = (entry.Item, remaining);

        return true;
    }

    public IReadOnlyList<(GameItem Item, int Quantity)> GetEntries()
    {
        // Snapshot: callers cannot mutate our internal dictionary.
        return _items.Values
            .Select(entry => (entry.Item, entry.Quantity))
            .OrderBy(entry => entry.Item.Id)
            .ToList()
            .AsReadOnly();
    }
}
```

### Important design choices

- `_items` is `private`: callers cannot change quantities directly.
- The dictionary key is the item ID.
- `Add()` rejects zero or negative quantities.
- `Remove()` returns `false` instead of allowing a negative balance.
- Entries with quantity zero are removed.
- `GetEntries()` returns a read-only snapshot for displaying inventory.
- The tutorial requires the **same item definition instance** for a given ID. A future `ItemFactory` will provide canonical definitions.

**C# note:** `Dictionary<int, (GameItem Item, int Quantity)>` uses a named **value tuple**. The tuple is a value type, but it contains a reference to the `GameItem` object.

## 9. Player — owns an Inventory

Create `Models/Player.cs`:

```csharp
namespace Tutorial005.Models;

public sealed class Player
{
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }

    public Inventory Inventory { get; } = new();
    public Weapon? EquippedWeapon { get; private set; }

    public bool IsAlive => HitPoints > 0;

    public Player(string name, int maximumHitPoints = 20)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Player name cannot be empty.", nameof(name));

        if (maximumHitPoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumHitPoints));

        Name = name;
        MaximumHitPoints = maximumHitPoints;
        HitPoints = maximumHitPoints;
    }

    public void TakeDamage(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        HitPoints = (int)Math.Max(0L, (long)HitPoints - amount);
    }

    public bool Equip(Weapon weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);

        if (!Inventory.Contains(weapon.Id))
            return false;

        EquippedWeapon = weapon;
        return true;
    }

    public bool DrinkPotion(HealingPotion potion)
    {
        ArgumentNullException.ThrowIfNull(potion);

        if (!IsAlive || HitPoints == MaximumHitPoints)
            return false;

        if (!Inventory.Contains(potion.Id))
            return false;

        // A successful use consumes exactly one potion.
        // Since the preconditions are checked, removal must succeed.
        int healedHp = (int)Math.Min(
            (long)MaximumHitPoints,
            (long)HitPoints + potion.AmountToHeal);

        if (!Inventory.Remove(potion.Id))
            return false;

        HitPoints = healedHp;
        return true;
    }

    public bool DiscardItem(int itemId, int quantity = 1)
    {
        // Preserve at least one equipped weapon.
        if (EquippedWeapon is not null &&
            EquippedWeapon.Id == itemId &&
            Inventory.Count(itemId) - quantity < 1)
        {
            return false;
        }

        return Inventory.Remove(itemId, quantity);
    }
}
```

### Composition

`Player` **has an** `Inventory`. This is composition, not inheritance.

```text
Player
├── HitPoints
├── EquippedWeapon ───────> Weapon
└── Inventory
    ├── Rusty Sword ×1
    └── Healing Potion ×3
```

The weapon is *equipped*, but still belongs to the inventory. Equipping does not consume or duplicate the item.

## 10. Program.cs — complete example

Replace `Program.cs` with:

```csharp
using Tutorial005.Models;

Console.WriteLine("=== SOSCSRPG Tutorial 005 ===");
Console.WriteLine("Collections, Generics, and Inventory");
Console.WriteLine();

var rustySword = new Weapon(1, "Rusty Sword", 12, 2, 5);
var potion = new HealingPotion(3, "Minor Healing Potion", 8, 5);

var player = new Player("Adventurer");

player.Inventory.Add(rustySword);
player.Inventory.Add(potion, 3);

Console.WriteLine("Initial inventory:");
PrintInventory(player.Inventory);

Console.WriteLine();
Console.WriteLine($"Equip sword: {player.Equip(rustySword)}");
Console.WriteLine($"Equipped: {player.EquippedWeapon?.Name ?? "None"}");

Console.WriteLine();
Console.WriteLine("Spider attacks for 7 damage.");
player.TakeDamage(7);
Console.WriteLine($"HP: {player.HitPoints}/{player.MaximumHitPoints}");

Console.WriteLine($"Drink potion: {player.DrinkPotion(potion)}");
Console.WriteLine($"HP: {player.HitPoints}/{player.MaximumHitPoints}");
Console.WriteLine($"Potions remaining: {player.Inventory.Count(potion.Id)}");

Console.WriteLine();
Console.WriteLine("Try to discard the only equipped sword:");
Console.WriteLine($"Discard succeeded: {player.DiscardItem(rustySword.Id)}");

Console.WriteLine();
Console.WriteLine("Try to drink a potion at full HP:");
player.DrinkPotion(potion); // 18 -> 20, consumes one potion.
bool usedAtFullHp = player.DrinkPotion(potion);
Console.WriteLine($"Used at full HP: {usedAtFullHp}");
Console.WriteLine($"Potions remaining: {player.Inventory.Count(potion.Id)}");

Console.WriteLine();
Console.WriteLine("Final inventory:");
PrintInventory(player.Inventory);

RunChecks();

static void PrintInventory(Inventory inventory)
{
    foreach (var (item, quantity) in inventory.GetEntries())
    {
        Console.WriteLine($"- {item.Name} ×{quantity}");
    }
}

static void RunChecks()
{
    var sword = new Weapon(101, "Test Sword", 1, 1, 2);
    var potion = new HealingPotion(102, "Test Potion", 1, 5);
    var hero = new Player("Tester");

    hero.Inventory.Add(sword);
    hero.Inventory.Add(potion, 2);

    Check(hero.Inventory.Count(potion.Id) == 2, "Starting quantity");
    Check(hero.Equip(sword), "Equip owned weapon");
    Check(!hero.DiscardItem(sword.Id), "Cannot discard equipped weapon");

    Check(!hero.DrinkPotion(potion), "Full HP does not consume potion");
    Check(hero.Inventory.Count(potion.Id) == 2, "Full HP quantity unchanged");

    hero.TakeDamage(8);
    Check(hero.DrinkPotion(potion), "Potion heals damaged player");
    Check(hero.HitPoints == 17, "Potion restores exactly five HP");
    Check(hero.Inventory.Count(potion.Id) == 1, "Potion quantity decreases");

    Check(!hero.Inventory.Remove(potion.Id, 2), "Cannot remove more than owned");
    Check(hero.Inventory.Count(potion.Id) == 1, "Failed removal is atomic");

    Console.WriteLine("\nAll inventory checks passed.");
}

static void Check(bool condition, string description)
{
    if (!condition)
        throw new InvalidOperationException($"FAIL: {description}");

    Console.WriteLine($"PASS: {description}");
}
```

### Expected output

```text
=== SOSCSRPG Tutorial 005 ===
Collections, Generics, and Inventory

Initial inventory:
- Rusty Sword ×1
- Minor Healing Potion ×3

Equip sword: True
Equipped: Rusty Sword

Spider attacks for 7 damage.
HP: 13/20
Drink potion: True
HP: 18/20
Potions remaining: 2

Try to discard the only equipped sword:
Discard succeeded: False

Try to drink a potion at full HP:
Used at full HP: False
Potions remaining: 1

Final inventory:
- Rusty Sword ×1
- Minor Healing Potion ×1
PASS: Starting quantity
PASS: Equip owned weapon
PASS: Cannot discard equipped weapon
PASS: Full HP does not consume potion
PASS: Full HP quantity unchanged
PASS: Potion heals damaged player
PASS: Potion restores exactly five HP
PASS: Potion quantity decreases
PASS: Cannot remove more than owned
PASS: Failed removal is atomic

All inventory checks passed.
```

## 11. Build and run in VS Code

Use the integrated terminal:

```bash
cd ~/Projects/SOSCSRPG-Tutorials/Tutorial005

env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run --no-build
```

Optional strict build:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet build -warnaserror
```

If the compiler reports an error, read the **first** error message before changing unrelated files. A successful restore does not prove the source code compiles.

## 12. Debugging collections in VS Code

1. Open the `Tutorial005` folder in VS Code.
2. Set a breakpoint inside `Inventory.Remove()`.
3. Press **F5** and select the C#/.NET debugger if prompted.
4. Inspect `itemId`, `quantity`, `entry.Quantity`, and `remaining`.
5. Press **F10** to step through a successful removal.
6. Continue until the failed removal test and observe why the method returns `false`.

Also set a breakpoint in `Player.DrinkPotion()` to verify that an item is consumed only after the HP and inventory preconditions pass.

| Shortcut | Action |
|---|---|
| `F5` | Start / continue |
| `F9` | Toggle breakpoint |
| `F10` | Step over |
| `F11` | Step into |
| `Shift+F5` | Stop |

## 13. C# versus C++

| Concept | C# | C++ |
|---|---|---|
| Dynamic array | `List<T>` | `std::vector<T>` |
| Key/value map | `Dictionary<K,V>` | `std::unordered_map<K,V>` |
| Iteration | `foreach` | Range-based `for` |
| Generic programming | Generics | Templates |
| Optional reference | `Weapon?` | Nullable pointer / `std::optional` depending on ownership |
| Read-only view | `IReadOnlyList<T>` | `const` access or views |
| Collection query | LINQ | Algorithms / ranges |

**Important distinction:** C# generics and C++ templates are not implemented identically. They provide similar type-safe collection APIs but differ in runtime representation and compilation.

## 14. Hands-on exercises

- [ ] **Exercise A — Spider Silk.** Add a `QuestItem : GameItem` class and collect 3 Spider Silk items.
- [ ] **Exercise B — Multiple quantities.** Add two more potions and verify the quantity increases rather than creating a second dictionary entry.
- [ ] **Exercise C — Removal rules.** Verify `Remove(itemId, 0)` and `Remove(itemId, -1)` throw `ArgumentOutOfRangeException`.
- [ ] **Exercise D — LINQ.** Add a method that lists only healing potions using `.OfType<HealingPotion>()` on item definitions.
- [ ] **Exercise E — Sorting.** Display inventory entries alphabetically by item name using `.OrderBy(...)`.
- [ ] **Exercise F — Equip validation.** Investigate how `Player.Equip()` should verify that the supplied weapon matches the exact definition held in inventory, not just the ID.
- [ ] **Exercise G — Sell safety.** Design a `SellItem()` method that refuses to sell the last equipped weapon and adds gold only after inventory removal succeeds.

### Engineering challenge: transaction boundaries

What happens if you subtract gold before checking whether a merchant has stock? What happens if you remove a potion before verifying that it can heal the player?

These are examples of **partial state changes**. A reliable RPG engine validates a transaction before committing its changes. In later chapters, we'll centralize these rules in game services/actions.

## 15. GitHub workflow

Create `.gitignore`:

```bash
cat > .gitignore <<'EOF'
bin/
obj/
.vs/
EOF
```

Then commit:

```bash
git init
git add Program.cs Models Tutorial005.csproj .gitignore
git commit -m "Tutorial 005: collections generics and inventory"
```

If Git requests your identity, configure it using your usual Git settings.

## 16. Knowledge check

1. What is the difference between `List<T>` and `Dictionary<TKey, TValue>`?
2. Why does the inventory store quantities separately from item definitions?
3. Why should `Remove()` return `false` when there are insufficient items?
4. Why must `DrinkPotion()` check HP before consuming the item?
5. What does `Weapon?` mean?
6. Why does `GetEntries()` return a snapshot instead of exposing `_items`?
7. How does composition connect `Player` to `Inventory`?
8. What are the limits of checking item ownership by ID alone?

## 17. Chapter summary

We now have:

```text
Player
├── HitPoints / MaximumHitPoints
├── EquippedWeapon ──> Weapon
└── Inventory
    └── Dictionary<ItemId, (GameItem, Quantity)>
        ├── Rusty Sword ×1
        └── Minor Healing Potion ×N
```

**Implemented:** item ownership, quantity tracking, equipping, potion consumption, safe removal, read-only inventory views, and basic checks.

**Not implemented yet:** item factories, traders, quests, monster loot, JSON persistence, and MonoGame UI.

### Next: Tutorial 006 — Factories, Item Catalogs, and Monster Loot

We'll build reusable `ItemFactory` and `MonsterFactory` classes, replace hard-coded object creation, and introduce data-driven loot rewards.
