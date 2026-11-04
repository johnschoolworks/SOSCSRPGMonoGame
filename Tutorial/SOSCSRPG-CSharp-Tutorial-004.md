# SOSCSRPG C# Tutorial 004 — Inheritance, Interfaces, and RPG Items

**Learning track:** SOSCSRPG with C# · Visual Studio Code · Ubuntu Linux  
**Runtime:** .NET 10 (`net10.0`)  
**Prerequisite:** Tutorial 003 — Constructors, Properties, and Encapsulation  
**Application:** Console RPG (MonoGame + Myra integration comes later)

> This is an original educational implementation inspired by the SOSCSRPG architectural approach, not a reproduction of Scott Lilly's source code or lesson text. Each tutorial is a standalone project. We will deliberately introduce inheritance, interfaces, and composition before adding a full inventory system.

## 1. Learning objectives

After this tutorial, you should be able to:

- Explain inheritance and distinguish a base class from a derived class.
- Use `abstract`, `sealed`, `virtual`, `override`, and `base(...)`.
- Define and implement a C# interface.
- Recognize polymorphism when storing different item types in one collection.
- Understand the difference between **is-a** (inheritance) and **has-a** (composition).
- Build a small item catalog containing weapons and healing potions.
- Use VS Code's C# debugger and run the project from its integrated terminal.

## 2. Development environment

### 2.1 Requirements

- Ubuntu Linux (the examples use Ubuntu 26.04).
- Native .NET SDK 10 (`/usr/bin/dotnet`).
- Visual Studio Code with Microsoft's C# extension.
- Git (optional for committing the lesson).

Open **Terminal → New Terminal** in VS Code (`Ctrl` + backtick), then verify:

```bash
cat /etc/os-release | grep PRETTY_NAME
env -u DOTNET_ROOT /usr/bin/dotnet --version
code --version | head -n 1
code --list-extensions | grep -Ei 'ms-dotnettools.csharp|ms-dotnettools.csdevkit' || true
```

If the C# extension is missing:

```bash
code --install-extension ms-dotnettools.csharp
```

Optional:

```bash
code --install-extension ms-dotnettools.csdevkit
```

If the .NET SDK is missing on another Ubuntu machine and the package is available in its configured repositories:

```bash
sudo apt update
sudo apt install dotnet-sdk-10.0
```

**Linux note:** We use `env -u DOTNET_ROOT /usr/bin/dotnet` to avoid conflicts with a previously installed Snap SDK. Do not use `sudo dotnet` for normal project builds.

### 2.2 Create the project

```bash
mkdir -p ~/Projects/SOSCSRPG-Tutorials
cd ~/Projects/SOSCSRPG-Tutorials

env -u DOTNET_ROOT /usr/bin/dotnet new console \
  --name Tutorial004 \
  --framework net10.0

cd Tutorial004
mkdir -p Models Interfaces
code .
```

Project layout after adding the files below:

```text
Tutorial004/
├── Interfaces/
│   └── IUsableItem.cs
├── Models/
│   ├── GameItem.cs
│   ├── Weapon.cs
│   ├── HealingPotion.cs
│   └── Player.cs
├── Program.cs
└── Tutorial004.csproj
```

`.cs` files under these folders are included automatically by the SDK-style project.

## 3. Inheritance: a shared GameItem base class

A weapon and a potion are different kinds of game items. They both have an ID, a name, and a price.

Instead of duplicating those properties, we can define a shared **base class**.

Create `Models/GameItem.cs`:

```csharp
namespace Tutorial004.Models;

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
            throw new ArgumentException("Item name cannot be empty.", nameof(name));

        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price));

        Id = id;
        Name = name;
        Price = price;
    }

    public virtual string Describe()
    {
        return $"{Name} (ID: {Id}, Price: {Price} Gold)";
    }
}
```

### New C# keywords

| Keyword | Purpose |
|---|---|
| `abstract` | Prevents creating a plain `GameItem` instance |
| `protected` | Makes a constructor/member accessible to derived classes |
| `virtual` | Allows a derived class to replace a method implementation |
| `override` | Supplies that replacement |
| `base(...)` | Calls a constructor in the base class |

This will **not** compile:

```csharp
// var item = new GameItem(1, "Unknown", 1);
// Error: Cannot create an instance of an abstract class.
```

That is intentional: our design requires every actual item to be a more specific type.

## 4. Derive a Weapon from GameItem

Create `Models/Weapon.cs`:

```csharp
namespace Tutorial004.Models;

public sealed class Weapon : GameItem
{
    public int MinimumDamage { get; }
    public int MaximumDamage { get; }

    public Weapon(
        int id,
        string name,
        int price,
        int minimumDamage,
        int maximumDamage)
        : base(id, name, price)
    {
        if (minimumDamage < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumDamage));

        if (maximumDamage < minimumDamage)
            throw new ArgumentOutOfRangeException(nameof(maximumDamage));

        MinimumDamage = minimumDamage;
        MaximumDamage = maximumDamage;
    }

    public int RollDamage()
    {
        // Random.Shared.Next uses an exclusive upper bound.
        // The two-argument overload requires MaximumDamage < int.MaxValue.
        if (MaximumDamage == int.MaxValue)
            throw new InvalidOperationException(
                "MaximumDamage is too large for this tutorial's damage roller.");

        return Random.Shared.Next(MinimumDamage, MaximumDamage + 1);
    }

    public override string Describe()
    {
        return $"{base.Describe()} | Damage: {MinimumDamage}-{MaximumDamage}";
    }
}
```

`Weapon : GameItem` expresses an **is-a** relationship: every weapon is a game item.

The `sealed` keyword means another class cannot inherit from `Weapon`. We do not need deeper inheritance in this lesson.

### Why `base.Describe()`?

The weapon reuses the common description and adds damage information. The runtime selects the overridden method even when the object is stored in a `GameItem` variable. That behavior is called **polymorphism**.

## 5. Interfaces: defining a usable item

An interface specifies what an object can do without prescribing its implementation.

Create `Interfaces/IUsableItem.cs`:

```csharp
using Tutorial004.Models;

namespace Tutorial004.Interfaces;

public interface IUsableItem
{
    bool Use(Player player);
}
```

The contract says: a usable item can be used on a player and returns `true` when it has an effect.

For this tutorial, `Weapon` does **not** implement `IUsableItem`: weapons participate in combat, while consumable potions are activated through `Use()`. Later we can introduce an action interface for weapons.

## 6. Create a Player class

We'll use a simplified version of the player from Tutorial 003 so this project remains standalone.

Create `Models/Player.cs`:

```csharp
namespace Tutorial004.Models;

public sealed class Player
{
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }

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

        HitPoints = Math.Max(0, HitPoints - amount);
    }

    public bool Heal(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        if (HitPoints == 0 || HitPoints == MaximumHitPoints)
            return false;

        int previous = HitPoints;
        HitPoints = (int)Math.Min(
            (long)MaximumHitPoints,
            (long)HitPoints + amount);

        return HitPoints > previous;
    }
}
```

**Rule chosen for this lesson:** a potion cannot revive a defeated player (0 HP). Healing also cannot exceed maximum HP.

The calculation uses `long` during addition to avoid overflow if a very large healing amount is passed.

## 7. Implement HealingPotion

Create `Models/HealingPotion.cs`:

```csharp
using Tutorial004.Interfaces;

namespace Tutorial004.Models;

public sealed class HealingPotion : GameItem, IUsableItem
{
    public int AmountToHeal { get; }

    public HealingPotion(
        int id,
        string name,
        int price,
        int amountToHeal)
        : base(id, name, price)
    {
        if (amountToHeal <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountToHeal));

        AmountToHeal = amountToHeal;
    }

    public bool Use(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Heal(AmountToHeal);
    }

    public override string Describe()
    {
        return $"{base.Describe()} | Restores {AmountToHeal} HP";
    }
}
```

`HealingPotion : GameItem, IUsableItem` means:

- A healing potion **is a** game item.
- A healing potion **can be used** through the `IUsableItem` contract.

A class can inherit from one base class and implement multiple interfaces.

## 8. Program.cs — complete working example

Replace the generated `Program.cs` with:

```csharp
using Tutorial004.Interfaces;
using Tutorial004.Models;

Console.WriteLine("=== SOSCSRPG Tutorial 004 ===");
Console.WriteLine("Inheritance, Interfaces, and RPG Items");
Console.WriteLine();

var player = new Player("Adventurer");

var rustySword = new Weapon(
    id: 1,
    name: "Rusty Sword",
    price: 12,
    minimumDamage: 2,
    maximumDamage: 5);

var ironSword = new Weapon(
    id: 2,
    name: "Iron Sword",
    price: 45,
    minimumDamage: 5,
    maximumDamage: 9);

var potion = new HealingPotion(
    id: 3,
    name: "Minor Healing Potion",
    price: 8,
    amountToHeal: 5);

// Polymorphism: one collection holds different derived item types.
List<GameItem> catalog = new()
{
    rustySword,
    ironSword,
    potion
};

Console.WriteLine("Item catalog:");
foreach (GameItem item in catalog)
{
    Console.WriteLine($"- {item.Describe()}");
}

Console.WriteLine();
Console.WriteLine($"Player HP: {player.HitPoints}/{player.MaximumHitPoints}");

player.TakeDamage(7);
Console.WriteLine($"After taking 7 damage: {player.HitPoints} HP");

// An interface reference can point to a HealingPotion.
IUsableItem usableItem = potion;

bool used = usableItem.Use(player);
Console.WriteLine($"Potion used successfully: {used}");
Console.WriteLine($"After healing: {player.HitPoints} HP");

Console.WriteLine();
Console.WriteLine("Trying to heal at full HP:");
player.Heal(100);
bool usedAtFullHealth = potion.Use(player);
Console.WriteLine($"Potion used successfully: {usedAtFullHealth}");
Console.WriteLine($"Current HP: {player.HitPoints}/{player.MaximumHitPoints}");

Console.WriteLine();
Console.WriteLine($"Rusty Sword damage roll: {rustySword.RollDamage()}");

Console.WriteLine();
Console.WriteLine("Item type inspection:");
foreach (GameItem item in catalog)
{
    if (item is Weapon weapon)
    {
        Console.WriteLine($"{weapon.Name} is a weapon.");
    }
    else if (item is IUsableItem)
    {
        Console.WriteLine($"{item.Name} is usable.");
    }
}
```

### Expected output

The random damage value will be between 2 and 5; one possible run is:

```text
=== SOSCSRPG Tutorial 004 ===
Inheritance, Interfaces, and RPG Items

Item catalog:
- Rusty Sword (ID: 1, Price: 12 Gold) | Damage: 2-5
- Iron Sword (ID: 2, Price: 45 Gold) | Damage: 5-9
- Minor Healing Potion (ID: 3, Price: 8 Gold) | Restores 5 HP

Player HP: 20/20
After taking 7 damage: 13 HP
Potion used successfully: True
After healing: 18 HP

Trying to heal at full HP:
Potion used successfully: False
Current HP: 20/20

Rusty Sword damage roll: 4

Item type inspection:
Rusty Sword is a weapon.
Iron Sword is a weapon.
Minor Healing Potion is usable.
```

**Important:** This chapter introduces item *definitions* and usage behavior, not consumable quantities. Calling `Use()` does not remove a potion from an inventory yet. That will be implemented in the next chapter.

## 9. Build and run from VS Code

In VS Code's integrated terminal:

```bash
cd ~/Projects/SOSCSRPG-Tutorials/Tutorial004

env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run --no-build
```

For a stricter compile check:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet build \
  -warnaserror
```

To remove generated build output:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet clean
```

## 10. Debug inheritance and polymorphism in VS Code

1. Open `Tutorial004` as the workspace folder.
2. Set a breakpoint on `Console.WriteLine($"- {item.Describe()}");` in `Program.cs`.
3. Press **F5** and select the C#/.NET debugger if prompted.
4. Inspect the runtime type of `item` during each loop iteration.
5. Use **F11** to step into `Describe()`.
6. Notice that the runtime executes `Weapon.Describe()` or `HealingPotion.Describe()` rather than only the base implementation.
7. Set another breakpoint in `HealingPotion.Use()` and inspect `player.HitPoints`.

| Shortcut | Action |
|---|---|
| `F5` | Start / continue |
| `F9` | Toggle breakpoint |
| `F10` | Step over |
| `F11` | Step into |
| `Shift+F5` | Stop debugging |

If the debugger cannot find the SDK, verify the native CLI build succeeds first. Check the C# extension output and any stale `DOTNET_ROOT` environment setting before changing project code.

## 11. Inheritance versus composition

**Inheritance (is-a):**

```csharp
public sealed class Weapon : GameItem
{
    // ...
}
```

**Composition (has-a):**

```csharp
public sealed class EquipmentSlot
{
    public Weapon? EquippedWeapon { get; private set; }

    public void Equip(Weapon weapon)
    {
        EquippedWeapon = weapon ??
            throw new ArgumentNullException(nameof(weapon));
    }
}
```

An equipment slot **has a** weapon. It is not a weapon itself.

In real RPG engines, composition is often preferable for behavior that can vary independently. For example, a magical sword might have a damage action, a status-effect action, or both. We will revisit this when implementing combat actions.

## 12. C# versus C++

| Concept | C# | C++ |
|---|---|---|
| Inheritance | `class Weapon : GameItem` | `class Weapon : public GameItem` |
| Virtual method | `virtual` / `override` | `virtual` / `override` |
| Abstract method | `abstract` | Pure virtual `= 0` |
| Interface | `interface IUsableItem` | Usually an abstract base class |
| Base constructor | `: base(...)` | `: Base(...)` |
| Object references | Managed references for classes | Objects, pointers, references, smart pointers |
| Memory | Garbage collection for managed objects | RAII and explicit ownership patterns |
| Prevent derivation | `sealed` | `final` |

C# does not support multiple **class** inheritance, but a class may implement multiple interfaces.

## 13. Hands-on exercises

Complete these in VS Code. Rebuild after each change.

- [ ] **Exercise A — Add Armor.** Create `Armor : GameItem` with a `Defense` property. Validate that defense cannot be negative. Override `Describe()`.
- [ ] **Exercise B — Add an item category.** Create an `enum ItemCategory` and decide whether it belongs on `GameItem` or the derived classes. Explain your design.
- [ ] **Exercise C — Polymorphism.** Add an `Armor` instance to `List<GameItem>` and verify that the correct `Describe()` implementation runs.
- [ ] **Exercise D — Interface contract.** Add a second usable item type, such as `GreaterHealingPotion` or `ManaPotion` (the latter requires a Mana property on `Player`).
- [ ] **Exercise E — Validation.** Try constructing a weapon with `minimumDamage: 8` and `maximumDamage: 3`; catch the exception and print the error message.
- [ ] **Exercise F — Design question.** Why should an inventory hold item quantities separately instead of storing a mutable `Quantity` on every shared item definition?

### Optional challenge: test potion behavior

Add this temporary code to `Program.cs`:

```csharp
var testPlayer = new Player("Tester");
testPlayer.TakeDamage(4);

var testPotion = new HealingPotion(99, "Test Potion", 1, 5);

if (!testPotion.Use(testPlayer) || testPlayer.HitPoints != 20)
    throw new Exception("Potion healing test failed.");

if (testPotion.Use(testPlayer))
    throw new Exception("Potion should not work at full HP.");

Console.WriteLine("Potion checks passed.");
```

This is a lightweight self-check, not a replacement for a proper automated test project.

## 14. GitHub workflow

Create a `.gitignore` file:

```bash
cat > .gitignore <<'EOF'
bin/
obj/
.vs/
EOF
```

Commit the tutorial:

```bash
git init
git add Program.cs Models Interfaces Tutorial004.csproj .gitignore
git commit -m "Tutorial 004: inheritance interfaces and RPG items"
```

If Git requests author identity, configure it using your normal Git settings.

## 15. Knowledge check

1. Why is `GameItem` abstract?
2. What does `: base(id, name, price)` do?
3. Why can `List<GameItem>` contain both `Weapon` and `HealingPotion`?
4. What is the difference between `override` and implementing an interface?
5. Why does `IUsableItem.Use()` return `bool`?
6. What is the difference between **is-a** and **has-a**?
7. Why should a potion's item definition not automatically represent its inventory quantity?

## 16. Chapter summary and next steps

You now have a small, type-safe item catalog:

```text
GameItem (abstract)
├── Weapon
└── HealingPotion ── implements IUsableItem

Player
└── HitPoints / MaximumHitPoints

List<GameItem>
└── Stores heterogeneous item definitions
```

**What we have not implemented yet:** inventory quantities, adding/removing items, consuming potions, equipping weapons, and saving item state. Those are intentional next steps.

**Next: Tutorial 005 — Collections, Generics, and Inventory.** We'll build an `Inventory` that tracks item IDs and quantities, handles consumption safely, and prepares for SOSCSRPG's item factories and trading systems.
