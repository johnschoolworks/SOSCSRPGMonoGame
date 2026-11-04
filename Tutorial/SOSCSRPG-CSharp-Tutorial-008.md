# SOSCSRPG C# Tutorial 008 — Game Events, Delegates, and Quest Notifications

**Series:** Learn C# by building SOSCSRPG  
**Environment:** Ubuntu 26.04 · .NET 10 · Visual Studio Code  
**Prerequisite:** Tutorial 007 — Quests, Objectives, and Rewards  
**Project:** `Tutorial008` · Standalone console app  
**Language:** English

> Original educational implementation inspired by Scott Lilly's SOSCSRPG design. The engine stays independent of MonoGame and Myra; the console presenter demonstrates how a future graphical UI can subscribe to game-state changes.

## 1. Learning objectives

After completing this chapter, you will understand:

1. What delegates and events are in C#.
2. How `EventHandler<TEventArgs>` provides a strongly typed notification.
3. How publishers and subscribers communicate without direct UI dependencies.
4. How to notify observers about inventory changes, player rewards, and quest status.
5. Why event subscriptions should be removed when a UI presenter is disposed.
6. How to test event counts and prevent duplicate notifications.
7. The limits of synchronous events and their effect on transaction boundaries.

## 2. Environment setup and project creation

In **VS Code → Terminal → New Terminal** (`Ctrl` + backtick):

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

On a new Ubuntu machine, if the .NET 10 SDK is available through its configured repositories:

```bash
sudo apt update
sudo apt install dotnet-sdk-10.0 git
```

We use `/usr/bin/dotnet` with `DOTNET_ROOT` unset to avoid conflicts with the earlier Snap runtime.

Create a fresh standalone project:

```bash
mkdir -p ~/Projects/SOSCSRPG-Tutorials
cd ~/Projects/SOSCSRPG-Tutorials

env -u DOTNET_ROOT /usr/bin/dotnet new console \
  --name Tutorial008 --framework net10.0

cd Tutorial008
mkdir -p Models Factories
code .
```

The complete project contains:

```text
Tutorial008/
├── Factories/
│   ├── ItemFactory.cs
│   └── QuestFactory.cs
├── Models/
│   ├── GameItem.cs
│   ├── Inventory.cs
│   ├── Player.cs
│   └── Quest.cs
├── GamePresenter.cs
├── Program.cs
└── Tutorial008.csproj
```

## 3. From direct calls to events

Without events, a game engine might contain UI code:

```csharp
// Bad coupling: the engine should not directly update UI widgets.
// questLabel.Text = "Completed";
```

Instead, the engine **publishes a notification**, and the UI subscribes:

```text
Player.Inventory.Add(...)
          |
          v
 InventoryChanged event
          |
          v
 GamePresenter (subscriber)
          |
          v
 Console output now / Myra widget later
```

The engine does not import Myra, MonoGame, or console UI types.

### Delegate versus event

A **delegate** is a type-safe reference to a method:

```csharp
Action<string> print = Console.WriteLine;
print("Hello from a delegate!");
```

An **event** is a restricted notification interface built on delegates. Outside code can subscribe and unsubscribe, but only the declaring class can raise the event:

```csharp
public event EventHandler<InventoryChangedEventArgs>? InventoryChanged;
```

`EventHandler<TEventArgs>` expects a handler with the shape:

```csharp
void Handler(object? sender, InventoryChangedEventArgs args)
```

The `?` after the event declaration allows it to have no subscribers. The `?.Invoke(...)` operator raises it only if subscribers exist.

## 4. Complete source code

Copy each code block into the named file. This tutorial is standalone and does not require copying code from Tutorial 007.

### 4.1 Project configuration

**File:** `Tutorial008.csproj`

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

### 4.2 GameItem and item identity

**File:** `Models/GameItem.cs`

```csharp
namespace Tutorial008.Models;

public sealed record GameItem(int Id, string Name);
```

### 4.3 ItemFactory

**File:** `Factories/ItemFactory.cs`

```csharp
using Tutorial008.Models;

namespace Tutorial008.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public static GameItem SpiderSilk { get; } = new(SpiderSilkId, "Spider Silk");
}
```

### 4.4 InventoryChanged event

**File:** `Models/Inventory.cs`

```csharp
namespace Tutorial008.Models;

public sealed class Inventory
{
    private readonly Dictionary<int, int> _items = new();

    public event EventHandler<InventoryChangedEventArgs>? InventoryChanged;

    public int Count(int itemId) => _items.GetValueOrDefault(itemId);

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

### 4.5 Player StatsChanged event

**File:** `Models/Player.cs`

```csharp
namespace Tutorial008.Models;

public sealed class Player
{
    public string Name { get; }
    public Inventory Inventory { get; } = new();
    public int Gold { get; private set; }
    public int ExperiencePoints { get; private set; }

    public event EventHandler<PlayerStatsChangedEventArgs>? StatsChanged;

    public Player(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name;
    }

    public bool CanReceiveRewards(int gold, int experience)
    {
        if (gold < 0) throw new ArgumentOutOfRangeException(nameof(gold));
        if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
        return (long)Gold + gold <= int.MaxValue &&
               (long)ExperiencePoints + experience <= int.MaxValue;
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

### 4.6 Quest StatusChanged event

**File:** `Models/Quest.cs`

```csharp
namespace Tutorial008.Models;

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

### 4.7 QuestFactory

**File:** `Factories/QuestFactory.cs`

```csharp
using Tutorial008.Models;

namespace Tutorial008.Factories;

public static class QuestFactory
{
    public static Quest CreateSpiderSilkQuest() =>
        new(id: 1, title: "Collect Spider Silk",
            requiredItemId: ItemFactory.SpiderSilkId,
            requiredQuantity: 3, rewardGold: 10, rewardExperience: 20);
}
```

### 4.8 GamePresenter: subscribing and unsubscribing

**File:** `GamePresenter.cs`

```csharp
using Tutorial008.Models;

namespace Tutorial008;

// A UI adapter: the engine does not know about Console or Myra.
public sealed class GamePresenter : IDisposable
{
    private readonly Player _player;
    private readonly Quest _quest;
    private bool _disposed;

    public GamePresenter(Player player, Quest quest)
    {
        _player = player;
        _quest = quest;

        _player.Inventory.InventoryChanged += OnInventoryChanged;
        _player.StatsChanged += OnStatsChanged;
        _quest.StatusChanged += OnQuestStatusChanged;
    }

    private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
    {
        if (e.ItemId == _quest.RequiredItemId)
            Console.WriteLine($"[UI] Quest progress: {_quest.Progress(_player)}/{_quest.RequiredQuantity}");
    }

    private void OnStatsChanged(object? sender, PlayerStatsChangedEventArgs e) =>
        Console.WriteLine($"[UI] Player stats: {e.Gold} Gold, {e.ExperiencePoints} XP");

    private void OnQuestStatusChanged(object? sender, QuestStatusChangedEventArgs e) =>
        Console.WriteLine($"[UI] Quest status: {e.Previous} -> {e.Current}");

    public void Dispose()
    {
        if (_disposed) return;
        _player.Inventory.InventoryChanged -= OnInventoryChanged;
        _player.StatsChanged -= OnStatsChanged;
        _quest.StatusChanged -= OnQuestStatusChanged;
        _disposed = true;
    }
}
```

### 4.9 Program.cs: demonstration and event checks

**File:** `Program.cs`

```csharp
using Tutorial008;
using Tutorial008.Factories;
using Tutorial008.Models;

Console.WriteLine("=== SOSCSRPG Tutorial 008 ===");
Console.WriteLine("Events, Delegates, and Quest Notifications");

var player = new Player("Adventurer");
var quest = QuestFactory.CreateSpiderSilkQuest();

using (var presenter = new GamePresenter(player, quest))
{
    quest.Accept();
    player.Inventory.Add(ItemFactory.SpiderSilk, 2);
    player.Inventory.Add(ItemFactory.SpiderSilk);
    Console.WriteLine($"Turn in: {quest.TurnIn(player)}");
    Console.WriteLine($"Turn in again: {quest.TurnIn(player)}");
}

RunChecks();
Console.WriteLine("ALL EVENT CHECKS PASSED");

static void RunChecks()
{
    var player = new Player("Tester");
    var quest = QuestFactory.CreateSpiderSilkQuest();
    int inventoryEvents = 0;
    int statsEvents = 0;
    int questEvents = 0;

    player.Inventory.InventoryChanged += (_, e) =>
    {
        inventoryEvents++;
        Check(e.ItemId == ItemFactory.SpiderSilkId, "Inventory event item ID");
    };
    player.StatsChanged += (_, e) =>
    {
        statsEvents++;
        Check(e.Gold == 10 && e.ExperiencePoints == 20, "Stats event values");
    };
    quest.StatusChanged += (_, _) => questEvents++;

    quest.Accept();
    Check(questEvents == 1, "Accept raises one status event");
    player.Inventory.Add(ItemFactory.SpiderSilk, 3);
    Check(inventoryEvents == 1, "Add raises one inventory event");

    Check(quest.TurnIn(player), "Quest completes");
    Check(inventoryEvents == 2, "Turn-in raises inventory event");
    Check(statsEvents == 1, "Turn-in raises stats event");
    Check(questEvents == 2, "Completion raises status event");
    Check(!quest.TurnIn(player), "Second turn-in rejected");
    Check(inventoryEvents == 2 && statsEvents == 1 && questEvents == 2,
        "Failed turn-in raises no events");

    int calls = 0;
    EventHandler<InventoryChangedEventArgs> temporary = (_, _) => calls++;
    player.Inventory.InventoryChanged += temporary;
    player.Inventory.InventoryChanged -= temporary;
    player.Inventory.Add(ItemFactory.SpiderSilk);
    Check(calls == 0, "Unsubscribed handler is not invoked");
}

static void Check(bool ok, string name)
{
    if (!ok) throw new InvalidOperationException($"FAIL: {name}");
    Console.WriteLine($"PASS: {name}");
}
```

## 5. Build and run

Use the VS Code integrated terminal:

```bash
cd ~/Projects/SOSCSRPG-Tutorials/Tutorial008

env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run --no-build
```

For a strict compilation check:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet build -warnaserror
```

### Expected output

```text
=== SOSCSRPG Tutorial 008 ===
Events, Delegates, and Quest Notifications
[UI] Quest status: NotStarted -> Active
[UI] Quest progress: 2/3
[UI] Quest progress: 3/3
[UI] Quest progress: 0/3
[UI] Player stats: 10 Gold, 20 XP
[UI] Quest status: Active -> Completed
Turn in: True
Turn in again: False
PASS: Accept raises one status event
PASS: Inventory event item ID
PASS: Add raises one inventory event
PASS: Inventory event item ID
PASS: Stats event values
PASS: Quest completes
PASS: Turn-in raises inventory event
PASS: Turn-in raises stats event
PASS: Completion raises status event
PASS: Second turn-in rejected
PASS: Failed turn-in raises no events
PASS: Inventory event item ID
PASS: Unsubscribed handler is not invoked
ALL EVENT CHECKS PASSED
```

**Notice:** The presenter briefly displays `0/3` when the quest consumes the required Spider Silk, *before* the completion notification. This is a useful demonstration of why the order of events matters. In a full UI, the quest log should use the quest's `Completed` state rather than display `0/3` as an unfinished objective.

## 6. Event ordering and design tradeoffs

In `Quest.TurnIn()` the engine:

1. Validates the quest state, inventory quantity, and reward capacity.
2. Removes three Spider Silk — raises `InventoryChanged`.
3. Grants Gold and XP — raises `StatsChanged`.
4. Marks the quest completed — raises `StatusChanged`.

The notifications are synchronous. A subscriber runs immediately during `Invoke()`.

**Important production caveat:** If a subscriber throws an exception, it can interrupt the method after some state changes have already occurred. In this lesson, handlers are simple and do not throw. A production engine can isolate observer failures, publish immutable state-change messages after a committed transaction, or queue notifications for processing on the game loop.

Also, do not let an event handler modify the same inventory recursively without designing for reentrancy.

## 7. Why GamePresenter implements IDisposable

This code subscribes to events:

```csharp
_player.Inventory.InventoryChanged += OnInventoryChanged;
_player.StatsChanged += OnStatsChanged;
_quest.StatusChanged += OnQuestStatusChanged;
```

And this code removes those subscriptions:

```csharp
_player.Inventory.InventoryChanged -= OnInventoryChanged;
_player.StatsChanged -= OnStatsChanged;
_quest.StatusChanged -= OnQuestStatusChanged;
```

An event publisher retains references to its subscribers. If a screen closes but remains subscribed, it may continue receiving notifications and remain in memory longer than intended.

`using var presenter = new GamePresenter(player, quest);` calls `Dispose()` at the end of the scope. In our demo, a `using (...)` block makes the subscription lifetime explicit.

## 8. Debug in Visual Studio Code

1. Open the `Tutorial008` project folder.
2. Place a breakpoint in `Inventory.OnInventoryChanged()`.
3. Press **F5** to launch the C# debugger.
4. Inspect `itemId`, `newQuantity`, and the event's subscribers.
5. Use **F11** to step into `GamePresenter.OnInventoryChanged()`.
6. Set a breakpoint in `Quest.SetStatus()` and inspect `previous` and `next`.
7. Observe the order of inventory, stats, and quest notifications during `TurnIn()`.

| Shortcut | Action |
|---|---|
| `F5` | Start / continue |
| `F9` | Toggle breakpoint |
| `F10` | Step over |
| `F11` | Step into |
| `Shift+F5` | Stop |

If the debugger fails, first confirm the CLI build succeeds and check the C# extension's output.

## 9. C# versus C++

| Topic | C# | C++ |
|---|---|---|
| Function reference | Delegate, e.g. `Action<T>` | `std::function`, function pointer |
| Publisher/subscriber | `event EventHandler<T>` | Observer pattern, signals/slots libraries |
| Subscription | `+=` | Register callback |
| Unsubscription | `-=` | Unregister callback / scoped connection |
| Notification payload | `EventArgs` subclass or record | Struct/class |
| Lifetime cleanup | `IDisposable` | RAII / scoped connection |
| UI decoupling | Engine event → presenter | Domain signal → view adapter |

C# events make the observer pattern convenient, but they do not automatically provide thread safety, transactionality, or error isolation.

## 10. Hands-on exercises

- [ ] **A — Quest progress event:** Add a separate `QuestProgressChanged` event. Decide whether the quest or a quest manager should subscribe to inventory changes.
- [ ] **B — Notification log:** Collect event messages in `List<string>` and assert their exact order.
- [ ] **C — More than one subscriber:** Add a logger and a presenter subscribed to the same event. Verify both receive notifications.
- [ ] **D — Unsubscribe:** Dispose the presenter, add Spider Silk, and verify that the presenter no longer prints updates.
- [ ] **E — Inventory removal:** Verify a failed `Remove()` does not raise an event.
- [ ] **F — Zero reward:** Verify `ReceiveRewards(0, 0)` does not raise `StatsChanged`.
- [ ] **G — Myra adapter sketch:** Write a `QuestPanelPresenter` that receives events and stores text properties without referencing Myra yet.
- [ ] **H — Failure analysis:** Add a deliberately throwing subscriber and observe what happens to state and subsequent handlers. Remove it afterward.

### Engineering challenge: event snapshots

Instead of having UI subscribers query mutable objects during an event, define a snapshot:

```csharp
public sealed record QuestSnapshot(
    int QuestId,
    QuestStatus Status,
    int CurrentQuantity,
    int RequiredQuantity);
```

Would this make UI rendering more predictable? What should happen if a player completes two quests during one update tick?

## 11. GitHub workflow

```bash
cat > .gitignore <<'EOF'
bin/
obj/
.vs/
EOF

git init
git add Program.cs GamePresenter.cs Models Factories Tutorial008.csproj .gitignore
git commit -m "Tutorial 008: events and quest notifications"
```

Configure your Git author identity if needed.

## 12. Knowledge check

1. What is the difference between a delegate and an event?
2. Why is `EventHandler<TEventArgs>` preferable to a callback with unrelated arguments?
3. Why can `Inventory` raise an event without knowing about Myra?
4. Why must a temporary presenter unsubscribe?
5. What is the order of notifications during quest turn-in?
6. What happens when an event subscriber throws?
7. Why might a completed quest briefly display `0/3` progress?
8. How can we avoid duplicate UI updates when several state changes happen in one transaction?

## 13. Chapter summary

You now have a notification-based game engine:

```text
Player.Inventory --InventoryChanged--+
                                     |
Player -----------StatsChanged-------+--> GamePresenter --> Console
                                     |                     (Myra later)
Quest ---------StatusChanged---------+
```

**Implemented:** delegates, strongly typed events, event payloads, event subscriptions, unsubscriptions, quest/inventory/stat notifications, and basic checks.

**Not yet implemented:** event batching, robust transactional notifications, multiple UI screens, persistent save/load, and the MonoGame + Myra presentation layer.

### Next: Tutorial 009 — JSON Save/Load and Game State Persistence

We'll introduce `System.Text.Json`, save player inventory and quest status, restore item definitions through factories, and verify round-trip state without serializing event subscribers.
