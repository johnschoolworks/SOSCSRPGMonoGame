# SOSCSRPG C# Tutorial 009 — JSON Save/Load and Game State Persistence

**Environment:** Ubuntu 26.04 · .NET 10 · Visual Studio Code  
**Prerequisite:** Tutorial 008 — Game Events, Delegates, and Quest Notifications  
**Project:** `Tutorial009` (standalone C# console application)  
**Language:** English

> This original educational implementation is inspired by the SOSCSRPG architecture. It is not a copy of Scott Lilly's source. The save model is designed so that a later MonoGame + Myra UI can attach to a reconstructed game session.

## 1. Learning objectives

- Serialize and deserialize JSON using `System.Text.Json`.
- Use **Data Transfer Objects (DTOs)** rather than serializing engine objects and event handlers.
- Save a player's name, gold, XP, item IDs and quantities, and quest statuses.
- Restore canonical item definitions through a factory and recreate quest objects.
- Validate save-file version, invalid item IDs, duplicate entries, and malformed data.
- Write files using a temporary file and replacement rename.
- Verify round-trip persistence and one-time quest rewards.

## 2. Environment setup

In VS Code's integrated terminal (`Ctrl` + backtick):

```bash
cat /etc/os-release | grep PRETTY_NAME
env -u DOTNET_ROOT /usr/bin/dotnet --version
code --version | head -n 1
code --list-extensions | grep -Ei 'ms-dotnettools.csharp|ms-dotnettools.csdevkit' || true
```

If needed, install the VS Code C# extension:

```bash
code --install-extension ms-dotnettools.csharp
```

If your configured Ubuntu repositories provide the native .NET 10 SDK:

```bash
sudo apt update
sudo apt install dotnet-sdk-10.0 git
```

The `env -u DOTNET_ROOT /usr/bin/dotnet` pattern avoids the earlier Snap/native .NET conflict.

## 3. Create the project

```bash
mkdir -p ~/Projects/SOSCSRPG-Tutorials
cd ~/Projects/SOSCSRPG-Tutorials
env -u DOTNET_ROOT /usr/bin/dotnet new console --name Tutorial009 --framework net10.0
cd Tutorial009
mkdir -p Models Factories Persistence
code .
```

Project tree:

```text
Tutorial009/
├── Factories/
│   ├── ItemFactory.cs
│   └── QuestFactory.cs
├── Models/
│   ├── GameItem.cs
│   ├── Inventory.cs
│   ├── Player.cs
│   └── Quest.cs
├── Persistence/
│   ├── SaveData.cs
│   └── SaveGameService.cs
├── Program.cs
└── Tutorial009.csproj
```

## 4. Save architecture

```text
Player + Inventory + Quests
           |
           v
SaveGameService.Capture()
           |
           v
SaveData DTO (Version, PlayerName, Gold, XP, Inventory[], Quests[])
           |
           v
System.Text.Json -> savegame.json
           |
           v
Deserialize -> Validate -> Rebuild Player + Quests
                            |
                            +--> ItemFactory resolves item IDs
                            +--> QuestFactory creates new quest instances
```

**Do not serialize event subscribers.** After loading, a UI presenter should subscribe to the new player's and quests' events. A loaded session contains new objects, not the original object identities.

### Why DTOs?

Game objects often contain behaviors, references, UI listeners, and derived values. A save file should contain only stable, minimal data. Our DTO stores item IDs and quantities, not duplicated item definitions.

## 5. Complete source code

Copy the following files exactly. The ZIP accompanying this chapter already includes them.

### 5.1 `Tutorial009.csproj`

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
namespace Tutorial009.Models;

public sealed record GameItem(int Id, string Name);
```

### 5.3 `Factories/ItemFactory.cs`

```csharp
using Tutorial009.Models;

namespace Tutorial009.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public static GameItem SpiderSilk { get; } = new(SpiderSilkId, "Spider Silk");
}
```

### 5.4 `Models/Inventory.cs`

```csharp
namespace Tutorial009.Models;

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
namespace Tutorial009.Models;

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
namespace Tutorial009.Models;

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

### 5.7 `Factories/QuestFactory.cs`

```csharp
using Tutorial009.Models;

namespace Tutorial009.Factories;

public static class QuestFactory
{
    public static Quest CreateSpiderSilkQuest() =>
        new(id: 1, title: "Collect Spider Silk",
            requiredItemId: ItemFactory.SpiderSilkId,
            requiredQuantity: 3, rewardGold: 10, rewardExperience: 20);
}
```

### 5.8 `Persistence/SaveData.cs`

```csharp
namespace Tutorial009.Persistence;

public sealed class SaveData
{
    public int Version { get; set; } = 1;
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

### 5.9 `Persistence/SaveGameService.cs`

```csharp
using System.Text.Json;
using Tutorial009.Factories;
using Tutorial009.Models;

namespace Tutorial009.Persistence;

public sealed class SaveGameService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false
    };

    public SaveData Capture(Player player, IEnumerable<Quest> quests)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(quests);
        return new SaveData
        {
            PlayerName = player.Name,
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

    public void Save(string path, Player player, IEnumerable<Quest> quests)
    {
        string json = Serialize(Capture(player, quests));
        // Temporary file plus rename avoids leaving a truncated target file.
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, fullPath, overwrite: true);
    }

    public (Player Player, List<Quest> Quests) Load(string path) =>
        Restore(Deserialize(File.ReadAllText(path)));

    public (Player Player, List<Quest> Quests) Restore(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Version != 1)
            throw new InvalidDataException($"Unsupported save version: {data.Version}");
        if (string.IsNullOrWhiteSpace(data.PlayerName))
            throw new InvalidDataException("Missing player name.");
        if (data.Gold < 0 || data.ExperiencePoints < 0)
            throw new InvalidDataException("Negative player rewards.");
        if (data.Inventory is null || data.Quests is null)
            throw new InvalidDataException("Missing inventory or quest list.");

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
        foreach (var entry in data.Inventory)
            player.Inventory.Add(ResolveItem(entry.ItemId), entry.Quantity);
        foreach (var (quest, status) in parsedQuests)
            quest.RestoreStatus(status);
        return (player, parsedQuests.Select(x => x.Quest).ToList());
    }

    private static GameItem ResolveItem(int id) => id switch
    {
        ItemFactory.SpiderSilkId => ItemFactory.SpiderSilk,
        _ => throw new InvalidDataException($"Unknown item ID: {id}")
    };
}
```

### 5.10 `Program.cs`

```csharp
using Tutorial009.Factories;
using Tutorial009.Models;
using Tutorial009.Persistence;

Console.WriteLine("=== SOSCSRPG Tutorial 009 ===");
Console.WriteLine("JSON Save/Load and Game State Persistence");

var service = new SaveGameService();
var player = new Player("Adventurer");
var quest = QuestFactory.CreateSpiderSilkQuest();
quest.Accept();
player.Inventory.Add(ItemFactory.SpiderSilk, 5);
player.ReceiveRewards(7, 11);

string path = Path.Combine(Environment.CurrentDirectory, "savegame.json");
service.Save(path, player, new[] { quest });
Console.WriteLine($"Saved: {path}");
Console.WriteLine(File.ReadAllText(path));

var (loadedPlayer, loadedQuests) = service.Load(path);
var loadedQuest = loadedQuests.Single();
Console.WriteLine($"Loaded: {loadedPlayer.Name}, {loadedPlayer.Gold} Gold, {loadedPlayer.ExperiencePoints} XP");
Console.WriteLine($"Quest: {loadedQuest.Title} [{loadedQuest.Status}], progress {loadedQuest.Progress(loadedPlayer)}/3");

Check(loadedPlayer.Name == "Adventurer", "Player name restored");
Check(loadedPlayer.Gold == 7 && loadedPlayer.ExperiencePoints == 11, "Rewards restored");
Check(loadedPlayer.Inventory.Count(ItemFactory.SpiderSilkId) == 5, "Inventory restored");
Check(loadedQuest.Status == QuestStatus.Active, "Quest status restored");
Check(loadedQuest.TurnIn(loadedPlayer), "Restored quest can be completed");
Check(loadedPlayer.Inventory.Count(ItemFactory.SpiderSilkId) == 2, "Only required items consumed");
Check(loadedPlayer.Gold == 17 && loadedPlayer.ExperiencePoints == 31, "Quest rewards added");
Check(!loadedQuest.TurnIn(loadedPlayer), "Duplicate reward prevented");

var completed = service.Deserialize(service.Serialize(service.Capture(loadedPlayer, loadedQuests)));
var (again, againQuests) = service.Restore(completed);
Check(againQuests.Single().Status == QuestStatus.Completed, "Completed quest survives round-trip");
Check(again.Gold == 17, "Round-trip gold correct");

ExpectInvalid("Unknown item", () => service.Restore(new SaveData
{
    PlayerName = "Bad", Inventory = new() { new() { ItemId = 999, Quantity = 1 } }
}));
ExpectInvalid("Duplicate quest", () => service.Restore(new SaveData
{
    PlayerName = "Bad", Quests = new()
    {
        new() { QuestId = 1, Status = "Active" },
        new() { QuestId = 1, Status = "Completed" }
    }
}));
ExpectInvalid("Unsupported version", () => service.Restore(new SaveData
{
    Version = 99, PlayerName = "Bad"
}));
Console.WriteLine("ALL SAVE/LOAD CHECKS PASSED");

static void Check(bool ok, string description)
{
    if (!ok) throw new InvalidOperationException($"FAIL: {description}");
    Console.WriteLine($"PASS: {description}");
}

static void ExpectInvalid(string description, Action action)
{
    try { action(); }
    catch (InvalidDataException)
    {
        Console.WriteLine($"PASS: {description} rejected");
        return;
    }
    throw new InvalidOperationException($"FAIL: {description} accepted");
}
```

## 6. Build and run

```bash
cd ~/Projects/SOSCSRPG-Tutorials/Tutorial009
env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run --no-build
```

For stricter warnings:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet build -warnaserror
```

The program writes `savegame.json` to the **current working directory**, prints the saved JSON, restores the player and quest, and runs validation checks. Look for:

```text
PASS: Player name restored
PASS: Rewards restored
PASS: Inventory restored
PASS: Quest status restored
PASS: Restored quest can be completed
PASS: Only required items consumed
PASS: Quest rewards added
PASS: Duplicate reward prevented
PASS: Completed quest survives round-trip
PASS: Round-trip gold correct
PASS: Unknown item rejected
PASS: Duplicate quest rejected
PASS: Unsupported version rejected
ALL SAVE/LOAD CHECKS PASSED
```

### Example saved JSON

```json
{
  "Version": 1,
  "PlayerName": "Adventurer",
  "Gold": 7,
  "ExperiencePoints": 11,
  "Inventory": [
    { "ItemId": 4, "Quantity": 5 }
  ],
  "Quests": [
    { "QuestId": 1, "Status": "Active" }
  ]
}
```

The output is deliberately human-readable. Never treat a locally editable JSON save file as a trusted authority in a competitive multiplayer game.

## 7. What happens during loading?

`SaveGameService.Restore()` first validates the version, player values, inventory IDs and quantities, duplicate entries, and quest IDs/statuses. It then constructs a new player and quest collection.

`RestoreRewards()` and `RestoreStatus()` initialize state without raising gameplay notifications. **Subscribe your UI presenter only after restoration.** This prevents misleading "reward earned" or "quest completed" messages during loading.

The current tutorial supports one item ID (Spider Silk) and one quest ID. Expand the factories and resolution switches as you add content.

## 8. Save-file safety and limitations

- **Temporary write:** writing `savegame.json.tmp` and renaming it reduces the risk of leaving a truncated target file. It is not a full durability guarantee (for example, power loss and filesystem-specific behavior remain).
- **Versioning:** unknown versions are rejected instead of silently interpreted.
- **Validation:** malformed or inconsistent entries are rejected before the restored session is returned.
- **Duplicate IDs:** inventory and quest duplicates are rejected.
- **Serialization boundary:** only DTO data is saved; runtime event subscriptions are not.
- **Untrusted files:** this tutorial does not impose maximum JSON size, nesting depth beyond serializer defaults, or application-specific resource limits. Add those before accepting arbitrary files.
- **Multiple quests:** the simple save format supports a list, but this chapter's factory recognizes only one quest.
- **Completed quest rules:** completion status is restored as-is. A larger game should validate cross-field invariants and track quest histories.
- **Disk errors:** file writes, renames, and reads can throw `IOException` or `UnauthorizedAccessException`; production code should handle these and offer recovery/backups.
- **Atomicity:** event callbacks in the engine are synchronous. Loading is performed before subscribing UI observers, but normal quest turn-in is not yet a fully isolated transaction.

## 9. Debugging in VS Code

1. Open `Tutorial009` in VS Code.
2. Set a breakpoint in `SaveGameService.Capture()` and inspect the DTO fields.
3. Set a breakpoint in `SaveGameService.Restore()` and step through validation.
4. Inspect `data.Inventory`, `data.Quests`, and the reconstructed `player`.
5. Use **F11** to step into `ItemFactory` and `QuestFactory`.
6. Verify that restored quests have the correct status and can be completed once.

| Shortcut | Action |
|---|---|
| `F5` | Start / continue |
| `F9` | Toggle breakpoint |
| `F10` | Step over |
| `F11` | Step into |
| `Shift+F5` | Stop |

## 10. C# versus C++

| Concept | C# | C++ |
|---|---|---|
| JSON serialization | `System.Text.Json` | Libraries such as `nlohmann/json` |
| DTO | Plain class with properties | Struct/class with data fields |
| Optional failure | Exceptions | Exceptions or `std::expected` |
| File I/O | `File.ReadAllText`, `File.WriteAllText` | `std::ifstream`, `std::ofstream` |
| Type-safe IDs | `int` constants (here) | `enum class` / strong types |
| Reconstruction | Factories | Factories / constructors |
| Collection | `List<T>` | `std::vector<T>` |

## 11. Exercises

- [ ] **A — Multiple items:** Add Wolf Fang to `ItemFactory`, then save and load both item types.
- [ ] **B — More quests:** Add a Wolf Fang quest to `QuestFactory` and persist two independent quest statuses.
- [ ] **C — Corrupted JSON:** Try `service.Deserialize("{invalid")` and catch `JsonException`.
- [ ] **D — Negative quantity:** Construct a DTO with quantity `-2` and verify rejection.
- [ ] **E — Duplicate inventory IDs:** Add the same item ID twice in a DTO and verify rejection.
- [ ] **F — UI reattachment:** After `Load()`, attach a new `GamePresenter` and confirm that future events reach the new presenter.
- [ ] **G — Backups:** Before replacing a save file, preserve a `.bak` copy and provide a recovery option.
- [ ] **H — Version 2:** Add a player level field and implement a migration from version 1.

## 12. GitHub workflow

Add a `.gitignore`:

```bash
cat > .gitignore <<'EOF'
bin/
obj/
.vs/
savegame.json
savegame.json.tmp
EOF
```

Then commit:

```bash
git init
git add Program.cs Models Factories Persistence Tutorial009.csproj .gitignore
git commit -m "Tutorial 009: JSON save load and state restoration"
```

Do not commit personal game saves or generated build artifacts by accident.

## 13. Knowledge check

1. Why is a DTO safer than serializing `Player` directly?
2. Why are item IDs stored instead of full item objects?
3. Why should event subscribers not be serialized?
4. Why must the loader reject unknown quest and item IDs?
5. What is the purpose of a save-file version number?
6. Why is a temporary file only a partial safeguard against data loss?
7. Why do we reconstruct objects before subscribing the UI?
8. What happens if a quest was already completed before saving?

## 14. Chapter summary

You now have a JSON persistence pipeline that can round-trip player stats, inventory quantities, and quest status while keeping the engine separate from the presentation layer.

**Next: Tutorial 010 — Game State, Locations, and Navigation.** We'll introduce location definitions, travel rules, encounters, and the first steps toward a world map suitable for MonoGame + Myra.
