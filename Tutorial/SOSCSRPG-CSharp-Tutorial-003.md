# SOSCSRPG C# Tutorial 003 — Constructors, Properties, and Encapsulation

**Platform:** Ubuntu 26.04 · .NET 10 · Visual Studio Code  
**Prerequisite:** Tutorial 002 — Classes and Objects  
**Project:** `Tutorial003`  
**Goal:** Build safer `Player` and `Monster` classes using constructors, properties, access modifiers, and validation.

> This is an original learning exercise inspired by the architecture of Scott Lilly's SOSCSRPG. It is not a verbatim reproduction of his lessons. We use a console application first; MonoGame + Myra will be integrated in later tutorials.

## 1. Learning objectives

By the end of this chapter, you will be able to:

1. Explain why constructors initialize objects.
2. Distinguish fields from properties.
3. Use `public`, `private`, and `get`/`set` accessors.
4. Enforce RPG rules through encapsulation.
5. Use constructor chaining and read-only properties.
6. Debug object state in Visual Studio Code.
7. Compare C# object design with C++.

## 2. Environment setup

Open **Visual Studio Code** and use **Terminal → New Terminal** (shortcut: `Ctrl+``).

Verify the development tools:

```bash
cat /etc/os-release | grep PRETTY_NAME
env -u DOTNET_ROOT /usr/bin/dotnet --version
code --version | head -n 1
code --list-extensions | grep -Ei 'ms-dotnettools.csharp|ms-dotnettools.csdevkit' || true
```

On the development machine used for earlier chapters, the native .NET 10 SDK is at `/usr/bin/dotnet`. We explicitly unset `DOTNET_ROOT` to avoid conflicts with a previously installed Snap SDK.

If the C# extension is missing:

```bash
code --install-extension ms-dotnettools.csharp
```

For a new Ubuntu machine without the SDK, first verify that the Ubuntu package repositories provide it, then install:

```bash
sudo apt update
sudo apt install dotnet-sdk-10.0 git
```

The optional **C# Dev Kit** extension provides additional project tooling:

```bash
code --install-extension ms-dotnettools.csdevkit
```

## 3. Create the project

Keep each chapter independent so you can compare implementations later.

```bash
mkdir -p ~/Projects/SOSCSRPG-Tutorials
cd ~/Projects/SOSCSRPG-Tutorials

env -u DOTNET_ROOT /usr/bin/dotnet new console \
  --name Tutorial003 \
  --framework net10.0

code Tutorial003
```

Create a `Models` folder in VS Code Explorer or from the integrated terminal:

```bash
cd ~/Projects/SOSCSRPG-Tutorials/Tutorial003
mkdir -p Models
```

Final source layout:

```text
Tutorial003/
├── Models/
│   ├── Player.cs
│   └── Monster.cs
├── Program.cs
└── Tutorial003.csproj
```

The .NET SDK automatically includes `.cs` files in subdirectories by default.

## 4. Constructors: establishing a valid starting state

In Tutorial 002, we created classes and objects. Now consider a player with zero HP or an empty name. We want to reject invalid starting values.

A **constructor** runs when an object is created with `new`. Its name matches the class name, and it has **no return type**.

Create `Models/Player.cs`:

```csharp
namespace Tutorial003.Models;

public sealed class Player
{
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }
    public int Gold { get; private set; }
    public int ExperiencePoints { get; private set; }

    public bool IsAlive => HitPoints > 0;

    public Player(string name, int maximumHitPoints, int startingGold)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Player name cannot be empty.", nameof(name));

        if (maximumHitPoints <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maximumHitPoints),
                "Maximum HP must be positive.");

        if (startingGold < 0)
            throw new ArgumentOutOfRangeException(
                nameof(startingGold),
                "Starting gold cannot be negative.");

        Name = name;
        MaximumHitPoints = maximumHitPoints;
        HitPoints = maximumHitPoints;
        Gold = startingGold;
        ExperiencePoints = 0;
    }

    // Constructor overload: convenient default values.
    public Player(string name) : this(name, 20, 10)
    {
    }

    public void TakeDamage(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        HitPoints = Math.Max(0, HitPoints - amount);
    }

    public void Heal(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        HitPoints = Math.Min(MaximumHitPoints, HitPoints + amount);
    }

    public void EarnGold(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        Gold = checked(Gold + amount);
    }

    public bool SpendGold(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        if (amount > Gold)
            return false;

        Gold -= amount;
        return true;
    }

    public void GainExperience(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        ExperiencePoints = checked(ExperiencePoints + amount);
    }
}
```

### Why two constructors?

```csharp
var first = new Player("Adventurer");
var second = new Player("Aether Gear", 30, 25);
```

The first constructor calls the second via `: this(name, 20, 10)`. This is **constructor chaining**: one place contains the validation and initialization rules.

## 5. Properties versus fields

A field stores data inside an object:

```csharp
private int _hitPoints;
```

A property exposes controlled access to data:

```csharp
public int HitPoints { get; private set; }
```

In this tutorial we use **auto-properties**. The compiler generates backing storage automatically.

| Declaration | Meaning |
|---|---|
| `public string Name { get; }` | Readable publicly; initialized in the constructor |
| `public int HitPoints { get; private set; }` | Readable publicly; writable only within `Player` |
| `public bool IsAlive => HitPoints > 0;` | Computed read-only property |
| `public int Gold { get; set; }` | Publicly readable and writable; **not** used here because it bypasses game rules |

The following is intentionally **not allowed**:

```csharp
var player = new Player("Adventurer");
// player.HitPoints = 999; // Compile-time error: setter is private.
```

Instead, change state through meaningful methods such as `TakeDamage()`, `Heal()`, or `EarnGold()`.

**Encapsulation** means an object protects its own invariants: HP remains between zero and maximum HP, and Gold never becomes negative.

## 6. Build a Monster class

Create `Models/Monster.cs`:

```csharp
namespace Tutorial003.Models;

public sealed class Monster
{
    public string Name { get; }
    public int MaximumHitPoints { get; }
    public int HitPoints { get; private set; }
    public int MinimumDamage { get; }
    public int MaximumDamage { get; }
    public int RewardGold { get; }
    public int RewardExperience { get; }

    public bool IsAlive => HitPoints > 0;

    public Monster(
        string name,
        int maximumHitPoints,
        int minimumDamage,
        int maximumDamage,
        int rewardGold,
        int rewardExperience)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Monster name cannot be empty.", nameof(name));

        if (maximumHitPoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumHitPoints));

        if (minimumDamage < 0 || maximumDamage < minimumDamage)
            throw new ArgumentOutOfRangeException(nameof(maximumDamage));

        if (rewardGold < 0)
            throw new ArgumentOutOfRangeException(nameof(rewardGold));

        if (rewardExperience < 0)
            throw new ArgumentOutOfRangeException(nameof(rewardExperience));

        Name = name;
        MaximumHitPoints = maximumHitPoints;
        HitPoints = maximumHitPoints;
        MinimumDamage = minimumDamage;
        MaximumDamage = maximumDamage;
        RewardGold = rewardGold;
        RewardExperience = rewardExperience;
    }

    public void TakeDamage(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        HitPoints = Math.Max(0, HitPoints - amount);
    }
}
```

The monster's damage range and rewards are read-only after construction. Combat logic can use these values without being allowed to rewrite the monster's definition.

## 7. Complete Program.cs

Replace `Program.cs` with:

```csharp
using Tutorial003.Models;

Console.WriteLine("=== SOSCSRPG Tutorial 003 ===");
Console.WriteLine("Constructors, Properties, and Encapsulation");
Console.WriteLine();

var player = new Player("Adventurer");
var spider = new Monster(
    name: "Venomous Spider",
    maximumHitPoints: 10,
    minimumDamage: 1,
    maximumDamage: 3,
    rewardGold: 5,
    rewardExperience: 10);

ShowStatus(player, spider);

Console.WriteLine("\nThe spider attacks for 3 damage.");
player.TakeDamage(3);
Console.WriteLine($"Player HP: {player.HitPoints}/{player.MaximumHitPoints}");

Console.WriteLine("\nThe player drinks a healing potion (+5 HP).");
player.Heal(5);
Console.WriteLine($"Player HP: {player.HitPoints}/{player.MaximumHitPoints}");

Console.WriteLine("\nThe player attacks the spider for 12 damage.");
spider.TakeDamage(12);
Console.WriteLine($"Spider HP: {spider.HitPoints}/{spider.MaximumHitPoints}");

if (!spider.IsAlive)
{
    Console.WriteLine("Spider defeated!");
    player.EarnGold(spider.RewardGold);
    player.GainExperience(spider.RewardExperience);
}

Console.WriteLine("\nThe player tries to spend 100 gold.");
bool purchaseSucceeded = player.SpendGold(100);
Console.WriteLine($"Purchase succeeded: {purchaseSucceeded}");

Console.WriteLine("\nFinal status:");
ShowStatus(player, spider);

static void ShowStatus(Player player, Monster monster)
{
    Console.WriteLine(
        $"{player.Name}: HP {player.HitPoints}/{player.MaximumHitPoints}, " +
        $"Gold {player.Gold}, XP {player.ExperiencePoints}");

    Console.WriteLine(
        $"{monster.Name}: HP {monster.HitPoints}/{monster.MaximumHitPoints}, " +
        $"Alive {monster.IsAlive}");
}
```

### Run

```bash
cd ~/Projects/SOSCSRPG-Tutorials/Tutorial003

env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run --no-build
```

Expected output:

```text
=== SOSCSRPG Tutorial 003 ===
Constructors, Properties, and Encapsulation

Adventurer: HP 20/20, Gold 10, XP 0
Venomous Spider: HP 10/10, Alive True

The spider attacks for 3 damage.
Player HP: 17/20

The player drinks a healing potion (+5 HP).
Player HP: 20/20

The player attacks the spider for 12 damage.
Spider HP: 0/10
Spider defeated!

The player tries to spend 100 gold.
Purchase succeeded: False

Final status:
Adventurer: HP 20/20, Gold 15, XP 10
Venomous Spider: HP 0/10, Alive False
```

## 8. C# versus C++: constructors and object ownership

**C++ example:**

```cpp
class Player {
public:
    explicit Player(int hp) : hitPoints(hp) {}
    int getHitPoints() const { return hitPoints; }
private:
    int hitPoints;
};

Player player(20); // Typically a stack object.
```

**C# example:**

```csharp
public sealed class Player
{
    public int HitPoints { get; }

    public Player(int hitPoints)
    {
        HitPoints = hitPoints;
    }
}

var player = new Player(20);
```

Important differences:

- C# `class` instances are **reference types**; `player` holds a reference to an object.
- C# uses a garbage collector to manage ordinary object memory. `Dispose()` and `using` still matter for resources such as file handles.
- C# properties provide a language-level way to expose controlled access.
- `sealed` prevents inheritance from a class. We'll explore inheritance and interfaces in later lessons.
- C# does not require a header/source (`.h`/`.cpp`) split.

## 9. Debug in VS Code

1. Open the `Tutorial003` folder in VS Code.
2. Install the Microsoft C# extension if needed.
3. Place a breakpoint on `HitPoints = Math.Max(0, HitPoints - amount);` in `Player.cs`.
4. Press **F5** and select the C#/.NET debugger if prompted.
5. In the Variables panel, inspect `amount`, `HitPoints`, and `MaximumHitPoints`.
6. Press **F10** to step over the assignment.
7. Confirm that HP becomes 17 after taking 3 damage, and returns to 20 after healing.

If F5 does not detect the project, first verify the integrated terminal can run:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet build
```

Then reopen the project folder and check the C# extension's output. The CLI build and VS Code debugging are separate diagnostic steps.

## 10. Experiments: verify the invariants

Try each experiment independently.

### A. HP cannot fall below zero

```csharp
player.TakeDamage(999);
Console.WriteLine(player.HitPoints); // 0
```

### B. Healing cannot exceed maximum HP

```csharp
player.Heal(999);
Console.WriteLine(player.HitPoints); // 20
```

### C. Insufficient gold cannot make Gold negative

```csharp
bool ok = player.SpendGold(999);
Console.WriteLine($"{ok}, {player.Gold}"); // False, current gold
```

### D. Invalid constructor arguments are rejected

```csharp
try
{
    var invalidPlayer = new Player("", 20, 10);
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Validation worked: {ex.Message}");
}
```

Do not add all experiments permanently to `Program.cs` if you want to preserve the expected output from Section 7.

## 11. Hands-on exercises

- [ ] **Exercise 1 — Add a Level property.** Initialize `Level` to `1` and expose only a public getter.
- [ ] **Exercise 2 — Add Mana.** Add `MaximumMana` and `Mana` to `Player`, with validated constructor parameters or defaults.
- [ ] **Exercise 3 — Add a RestoreMana method.** Prevent mana from exceeding its maximum.
- [ ] **Exercise 4 — Add an attack method.** Create `Monster.AttackDamage()` that returns a random integer from `MinimumDamage` through `MaximumDamage`, inclusive. Consider using `Random.Shared.Next(MinimumDamage, MaximumDamage + 1)`; think about the integer-overflow edge case if `MaximumDamage` were `int.MaxValue`.
- [ ] **Exercise 5 — Test invalid input.** Verify negative damage throws an `ArgumentOutOfRangeException`.

### Challenge: separate combat from models

Right now, `Program.cs` decides when to award gold and experience. In a later tutorial, we'll move this rule into a combat service or action. Why might that separation make testing easier?

## 12. Optional: initialize a Git repository

From the tutorial folder:

```bash
cd ~/Projects/SOSCSRPG-Tutorials/Tutorial003

cat > .gitignore <<'EOF'
bin/
obj/
.vs/
EOF

git init
git add Program.cs Models Tutorial003.csproj .gitignore
git commit -m "Tutorial 003: constructors properties and encapsulation"
```

If Git asks for an author identity, configure it according to your normal Git workflow before committing.

## 13. Knowledge check

1. What is the difference between a constructor and a normal method?
2. Why does `HitPoints` use `private set`?
3. What happens if you call `new Player("", 20, 10)`?
4. What does `public bool IsAlive => HitPoints > 0;` calculate?
5. Why is `SpendGold()` safer than a publicly writable `Gold` property?
6. How does `: this(name, 20, 10)` prevent duplicated initialization code?

**Expected understanding:** Constructors create valid initial states; properties expose data safely; encapsulation protects the rules of your RPG.

---

## Next: Tutorial 004 — Inheritance, Interfaces, and RPG Items

We'll introduce `GameItem`, `Weapon`, and `HealingPotion`, compare inheritance with composition, and start building a type-safe inventory.

**Development workflow for every chapter:** VS Code → edit C# → build in integrated terminal → debug → complete exercises → commit to Git.
