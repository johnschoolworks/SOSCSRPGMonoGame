# SOSCSRPG C# Tutorial 007 — Quests, Objectives, and Rewards

**Development environment:** Ubuntu 26.04 · .NET 10 · Visual Studio Code  
**Prerequisite:** Tutorial 006 — Factories, Item Catalogs, and Monster Loot  
**Project:** `Tutorial007` · Standalone console application  
**Language:** English

> This is an original C# learning exercise inspired by Scott Lilly's SOSCSRPG architecture, not a verbatim reproduction of his original source. Our console-based engine will later connect to MonoGame + Myra.

## 1. Learning objectives

By the end of this tutorial, you will:

1. Model a quest with `QuestStatus`, objectives, and rewards.
2. Use an `enum` and a finite-state workflow.
3. Track the classic objective **Collect 3 Spider Silk**.
4. Separate quest *definitions* from a player's inventory.
5. Validate turn-in conditions before changing game state.
6. Prevent duplicate quest rewards.
7. Use a `QuestFactory` and run automated engine checks.
8. Debug quest transitions in VS Code.

## 2. Environment setup

Open **Visual Studio Code → Terminal → New Terminal** (`Ctrl` + backtick).

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

If .NET 10 is not installed on another Ubuntu machine and is available through its configured repositories:

```bash
sudo apt update
sudo apt install dotnet-sdk-10.0 git
```

We explicitly use `env -u DOTNET_ROOT /usr/bin/dotnet` to avoid the previous Snap/runtime conflict on the Linux development machine.

## 3. Create the project

```bash
mkdir -p ~/Projects/SOSCSRPG-Tutorials
cd ~/Projects/SOSCSRPG-Tutorials

env -u DOTNET_ROOT /usr/bin/dotnet new console \
  --name Tutorial007 \
  --framework net10.0

cd Tutorial007
mkdir -p Models Factories
code .
```

Create the files in the following structure:

```text
Tutorial007/
├── Factories/
│   ├── ItemFactory.cs
│   └── QuestFactory.cs
├── Models/
│   ├── GameItem.cs
│   ├── Inventory.cs
│   ├── Player.cs
│   └── Quest.cs
├── Program.cs
└── Tutorial007.csproj
```

## 4. Architecture and quest lifecycle

The quest is a small state machine:

```text
NotStarted --Accept()--> Active --TurnIn()--> Completed
                             |
                             +-- insufficient items --> remains Active
                             +-- invalid rewards ---> remains Active
```

The quest checks the player's inventory rather than maintaining a second, potentially inconsistent item counter.

```text
QuestFactory
    └── Quest: Collect Spider Silk
        ├── RequiredItemId = 4
        ├── RequiredQuantity = 3
        ├── RewardGold = 10
        └── RewardExperience = 20

Player
    ├── Inventory: Spider Silk ×N
    ├── Gold
    └── ExperiencePoints
```

**Important design decision:** Quest completion consumes exactly three Spider Silk. Extra Spider Silk stays in the inventory.

## 5. Complete source files

The following code is a self-contained chapter. You do not need to copy source files from Tutorial 006.

### 5.1 Project configuration

**File:** `Tutorial007.csproj`

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

### 5.2 GameItem: immutable item definition

**File:** `Models/GameItem.cs`

```csharp
namespace Tutorial007.Models;

public sealed class GameItem
{
    public int Id { get; }
    public string Name { get; }

    public GameItem(int id, string name)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Id = id;
        Name = name;
    }
}
```

### 5.3 ItemFactory: canonical item IDs

**File:** `Factories/ItemFactory.cs`

```csharp
using Tutorial007.Models;

namespace Tutorial007.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public const int WolfFangId = 5;

    private static readonly Dictionary<int, GameItem> Items = new()
    {
        [SpiderSilkId] = new GameItem(SpiderSilkId, "Spider Silk"),
        [WolfFangId] = new GameItem(WolfFangId, "Wolf Fang")
    };

    public static GameItem Get(int id) =>
        Items.TryGetValue(id, out var item)
            ? item
            : throw new KeyNotFoundException($"Unknown item ID: {id}");
}
```

### 5.4 Inventory: quantities and safe removal

**File:** `Models/Inventory.cs`

```csharp
namespace Tutorial007.Models;

public sealed class Inventory
{
    private readonly Dictionary<int, int> _quantities = new();

    public int Count(int itemId) =>
        _quantities.GetValueOrDefault(itemId);

    public void Add(GameItem item, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        int updated = checked(Count(item.Id) + quantity);
        _quantities[item.Id] = updated;
    }

    public bool Remove(int itemId, int quantity = 1)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        int owned = Count(itemId);
        if (owned < quantity) return false;
        if (owned == quantity) _quantities.Remove(itemId);
        else _quantities[itemId] = owned - quantity;
        return true;
    }
}
```

### 5.5 Player: inventory, gold, and experience

**File:** `Models/Player.cs`

```csharp
namespace Tutorial007.Models;

public sealed class Player
{
    public string Name { get; }
    public Inventory Inventory { get; } = new();
    public int Gold { get; private set; }
    public int ExperiencePoints { get; private set; }

    public Player(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name;
    }

    public bool CanReceiveRewards(int gold, int experience)
    {
        if (gold < 0 || experience < 0)
            throw new ArgumentOutOfRangeException(nameof(gold));
        return (long)Gold + gold <= int.MaxValue &&
               (long)ExperiencePoints + experience <= int.MaxValue;
    }

    public void ReceiveRewards(int gold, int experience)
    {
        if (!CanReceiveRewards(gold, experience))
            throw new OverflowException("Quest rewards exceed player limits.");
        Gold += gold;
        ExperiencePoints += experience;
    }
}
```

### 5.6 Quest: state machine and turn-in rules

**File:** `Models/Quest.cs`

```csharp
namespace Tutorial007.Models;

public enum QuestStatus
{
    NotStarted,
    Active,
    Completed
}

public sealed class Quest
{
    public int Id { get; }
    public string Title { get; }
    public int RequiredItemId { get; }
    public int RequiredQuantity { get; }
    public int RewardGold { get; }
    public int RewardExperience { get; }
    public QuestStatus Status { get; private set; } = QuestStatus.NotStarted;

    public Quest(int id, string title, int requiredItemId,
                 int requiredQuantity, int rewardGold, int rewardExperience)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (requiredItemId <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredItemId));
        if (requiredQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredQuantity));
        if (rewardGold < 0 || rewardExperience < 0)
            throw new ArgumentOutOfRangeException(nameof(rewardGold));

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
            throw new InvalidOperationException("Quest already accepted or completed.");
        Status = QuestStatus.Active;
    }

    public int Progress(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return Math.Min(RequiredQuantity, player.Inventory.Count(RequiredItemId));
    }

    public bool CanComplete(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return Status == QuestStatus.Active &&
               player.Inventory.Count(RequiredItemId) >= RequiredQuantity;
    }

    public bool TurnIn(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!CanComplete(player)) return false;

        // Validate rewards before consuming the objective items.
        if (!player.CanReceiveRewards(RewardGold, RewardExperience))
            return false;

        if (!player.Inventory.Remove(RequiredItemId, RequiredQuantity))
            return false;

        player.ReceiveRewards(RewardGold, RewardExperience);
        Status = QuestStatus.Completed;
        return true;
    }
}
```

### 5.7 QuestFactory: reusable quest construction

**File:** `Factories/QuestFactory.cs`

```csharp
using Tutorial007.Models;

namespace Tutorial007.Factories;

public static class QuestFactory
{
    public const int CollectSpiderSilkId = 1;

    public static Quest Create(int id) => id switch
    {
        CollectSpiderSilkId => new Quest(
            id: CollectSpiderSilkId,
            title: "Collect Spider Silk",
            requiredItemId: ItemFactory.SpiderSilkId,
            requiredQuantity: 3,
            rewardGold: 10,
            rewardExperience: 20),
        _ => throw new KeyNotFoundException($"Unknown quest ID: {id}")
    };
}
```

### 5.8 Program.cs: quest demo and checks

**File:** `Program.cs`

```csharp
using Tutorial007.Factories;
using Tutorial007.Models;

Console.WriteLine("=== SOSCSRPG Tutorial 007 ===");
Console.WriteLine("Quests, Objectives, and Rewards");

var player = new Player("Adventurer");
var quest = QuestFactory.Create(QuestFactory.CollectSpiderSilkId);
var silk = ItemFactory.Get(ItemFactory.SpiderSilkId);

quest.Accept();
Console.WriteLine($"Quest: {quest.Title} [{quest.Status}]");

player.Inventory.Add(silk, 2);
Console.WriteLine($"Progress: {quest.Progress(player)}/{quest.RequiredQuantity}");
Console.WriteLine($"Turn in early: {quest.TurnIn(player)}");

player.Inventory.Add(silk);
Console.WriteLine($"Progress: {quest.Progress(player)}/{quest.RequiredQuantity}");
Console.WriteLine($"Turn in: {quest.TurnIn(player)}");
Console.WriteLine($"Quest status: {quest.Status}");
Console.WriteLine($"Gold: {player.Gold}; XP: {player.ExperiencePoints}");
Console.WriteLine($"Spider Silk remaining: {player.Inventory.Count(silk.Id)}");
Console.WriteLine($"Turn in again: {quest.TurnIn(player)}");

RunChecks();
Console.WriteLine("ALL QUEST CHECKS PASSED");

static void RunChecks()
{
    var silk = ItemFactory.Get(ItemFactory.SpiderSilkId);
    var hero = new Player("Tester");
    var quest = QuestFactory.Create(QuestFactory.CollectSpiderSilkId);

    Check(!quest.TurnIn(hero), "Cannot turn in before accepting");
    quest.Accept();
    Check(!quest.CanComplete(hero), "Quest needs items");
    hero.Inventory.Add(silk, 2);
    Check(quest.Progress(hero) == 2, "Partial progress");
    Check(!quest.TurnIn(hero), "Early turn-in fails");
    Check(hero.Inventory.Count(silk.Id) == 2, "Failed turn-in keeps items");

    hero.Inventory.Add(silk, 2);
    Check(quest.Progress(hero) == 3, "Progress capped at target");
    Check(quest.TurnIn(hero), "Turn-in succeeds");
    Check(quest.Status == QuestStatus.Completed, "Quest marked completed");
    Check(hero.Inventory.Count(silk.Id) == 1, "Only required items consumed");
    Check(hero.Gold == 10 && hero.ExperiencePoints == 20, "Rewards granted");
    Check(!quest.TurnIn(hero), "Second turn-in blocked");
    Check(hero.Gold == 10 && hero.ExperiencePoints == 20, "No duplicate rewards");

    var wealthy = new Player("Wealthy");
    wealthy.ReceiveRewards(int.MaxValue, 0);
    wealthy.Inventory.Add(silk, 3);
    var overflowQuest = QuestFactory.Create(QuestFactory.CollectSpiderSilkId);
    overflowQuest.Accept();
    Check(!overflowQuest.TurnIn(wealthy), "Overflow reward rejected");
    Check(wealthy.Inventory.Count(silk.Id) == 3, "Overflow keeps items");
    Check(overflowQuest.Status == QuestStatus.Active, "Overflow keeps quest active");
}

static void Check(bool result, string description)
{
    if (!result) throw new InvalidOperationException($"FAIL: {description}");
    Console.WriteLine($"PASS: {description}");
}
```

## 6. Build and run from VS Code

In the integrated terminal:

```bash
cd ~/Projects/SOSCSRPG-Tutorials/Tutorial007

env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run --no-build
```

A stricter compile check:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet build -warnaserror
```

### Expected output

```text
=== SOSCSRPG Tutorial 007 ===
Quests, Objectives, and Rewards
Quest: Collect Spider Silk [Active]
Progress: 2/3
Turn in early: False
Progress: 3/3
Turn in: True
Quest status: Completed
Gold: 10; XP: 20
Spider Silk remaining: 0
Turn in again: False
PASS: Cannot turn in before accepting
PASS: Quest needs items
PASS: Partial progress
PASS: Early turn-in fails
PASS: Failed turn-in keeps items
PASS: Progress capped at target
PASS: Turn-in succeeds
PASS: Quest marked completed
PASS: Only required items consumed
PASS: Rewards granted
PASS: Second turn-in blocked
PASS: No duplicate rewards
PASS: Overflow reward rejected
PASS: Overflow keeps items
PASS: Overflow keeps quest active
ALL QUEST CHECKS PASSED
```

## 7. Why the turn-in order matters

The `Quest.TurnIn()` method performs its operations in this order:

1. Confirm that the quest is active and enough items are owned.
2. Check that gold and experience rewards can fit in their `int` fields.
3. Remove exactly the required quantity.
4. Award gold and experience.
5. Mark the quest completed.

This prevents the most common mistakes: consuming items for an incomplete quest, granting rewards twice, and losing items when rewards would overflow.

**Scope limitation:** This tutorial assumes a single-threaded game loop and ordinary in-memory objects. In a concurrent or persistent engine, the whole turn-in should be treated as a transaction with stronger guarantees. Our later architecture can move this operation to a quest service.

## 8. New C# concepts

### Enums

```csharp
public enum QuestStatus
{
    NotStarted,
    Active,
    Completed
}
```

An `enum` represents a closed set of named states, rather than magic integers or strings.

### Expression-bodied methods

```csharp
public int Count(int itemId) =>
    _quantities.GetValueOrDefault(itemId);
```

The `=>` syntax is concise for methods that return one expression.

### Guard clauses

```csharp
if (requiredQuantity <= 0)
    throw new ArgumentOutOfRangeException(nameof(requiredQuantity));
```

Guard clauses reject invalid inputs early and simplify the rest of the method.

### `checked` arithmetic

```csharp
int updated = checked(Count(item.Id) + quantity);
```

`checked` causes an exception on integer overflow instead of allowing silent wraparound.

### `Math.Min`

```csharp
return Math.Min(RequiredQuantity, player.Inventory.Count(RequiredItemId));
```

Progress is capped at the objective target, even if the player owns extra items.

## 9. Debugging in VS Code

1. Open the `Tutorial007` project folder.
2. Set a breakpoint at the start of `Quest.TurnIn()`.
3. Press **F5** and choose the C#/.NET debugger if prompted.
4. Inspect `Status`, `RequiredQuantity`, and `player.Inventory.Count(RequiredItemId)`.
5. Use **F10** to step through the failed early turn-in.
6. Continue until the successful turn-in and watch the quest change from `Active` to `Completed`.
7. Repeat on the second turn-in; it must return `false` without changing gold or XP.

| Shortcut | Action |
|---|---|
| `F5` | Start / continue |
| `F9` | Toggle breakpoint |
| `F10` | Step over |
| `F11` | Step into |
| `Shift+F5` | Stop |

If VS Code debugging fails, first confirm that `dotnet build` succeeds in the integrated terminal and inspect the C# extension's output.

## 10. C# versus C++

| Concept | C# | C++ |
|---|---|---|
| Named states | `enum QuestStatus` | `enum class QuestStatus` |
| Item quantities | `Dictionary<int,int>` | `std::unordered_map<int,int>` |
| Factory | `static class QuestFactory` | Factory function or class |
| Object reference | `Quest` class reference | Object, reference, or smart pointer |
| Invalid arguments | `ArgumentOutOfRangeException` | `std::invalid_argument`, etc. |
| Arithmetic overflow | `checked(...)` | Explicit bounds checks / safe arithmetic |
| Property access | `public int Gold { get; private set; }` | Private member + accessor |

The architecture matters more than the syntax: **the quest owns its state, the player owns rewards, and the inventory owns item quantities.**

## 11. Hands-on exercises

- [ ] **Exercise A — Wolf Fang quest:** Add a second quest that requires two Wolf Fang.
- [ ] **Exercise B — Multiple quests:** Store active quests in `List<Quest>` and display their progress.
- [ ] **Exercise C — Extra items:** Add five Spider Silk, turn in the quest, and verify that two remain.
- [ ] **Exercise D — Quest validation:** Verify that `new Quest(..., requiredQuantity: 0, ...)` throws.
- [ ] **Exercise E — Duplicate acceptance:** Call `Accept()` twice and catch the expected exception.
- [ ] **Exercise F — Quest log:** Write `PrintQuestLog(IEnumerable<Quest> quests, Player player)` and show each quest's status.
- [ ] **Exercise G — Quest prerequisites:** Require another quest to be completed before accepting the Wolf Fang quest.
- [ ] **Exercise H — Automated tests:** Move the `RunChecks()` cases to a dedicated test project and test each quest rule independently.

### Engineering challenge

Suppose a player owns five Spider Silk and has two active quests, each requiring three. Should both quests show `3/3` progress? What happens when the player turns one in?

In our current design, both may initially show enough items, but after one consumes three, the other shows `2/3`. This is intentional for **inventory-based objectives**. A different quest design could track lifetime collection progress without consuming items. Decide which behavior your game needs.

## 12. GitHub workflow

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
git add Program.cs Models Factories Tutorial007.csproj .gitignore
git commit -m "Tutorial 007: quest objectives and one-time rewards"
```

Configure your Git author identity if Git asks for it.

## 13. Knowledge check

1. Why is `QuestStatus` an enum rather than a string?
2. Why does `Quest.Progress()` read the player's inventory?
3. Why does `TurnIn()` check rewards before removing items?
4. How does `Completed` prevent duplicate rewards?
5. Why does `QuestFactory.Create()` return a new quest instance?
6. Why is progress capped at `RequiredQuantity`?
7. What would need to change for a non-consuming quest objective?
8. Which engine component should notify the Myra UI after a quest is completed?

## 14. Chapter summary

You have implemented a small, testable quest workflow:

```text
QuestFactory.Create()
        |
        v
  Quest.Accept()
        |
        v
Player collects Spider Silk
        |
        v
Quest.Progress() == 3/3
        |
        v
Quest.TurnIn()
   |          |
   v          v
Remove 3    Award Gold + XP
   |          |
   +----+-----+
        |
        v
  Status = Completed
```

**Implemented:** quest definitions, acceptance, item objectives, progress display, one-time turn-in, reward validation, inventory consumption, and automated checks.

**Not implemented yet:** NPC quest-giver interactions, multiple objective types, quest persistence, game events, or Myra quest-log widgets.

### Next: Tutorial 008 — Game Events and Quest Notifications

We'll use C# events and delegates to notify the game UI when inventory, quest status, and player rewards change. That will prepare our engine for MonoGame + Myra without coupling game rules to widgets.
