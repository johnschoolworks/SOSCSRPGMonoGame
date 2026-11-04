# SOSCSRPG C# Tutorial 016 — Equipment, Potions, and Character Progression

**Series:** SOSCSRPG C# Tutorials (16 of 20)  
**Environment:** Ubuntu 26.04 · .NET 10 · VS Code · Linux Terminal  
**Prerequisite:** Tutorial 015 — Shops, Trading, and Economy  
**Language:** English  
**Project:** `Tutorial016`

> This is an original educational RPG implementation inspired by Scott Lilly's SOSCSRPG. The downloadable ZIP is a standalone .NET 10 console application. MonoGame + Myra UI integration is described through the existing UI-neutral `GameController`, but no graphical executable is included.

## 1. Objectives

Tutorial 015 introduced a merchant and inventory economy. This chapter gives those items gameplay meaning and adds a simple character progression model. You'll learn how to:

1. Represent equipped weapons independently of carried inventory.
2. Purchase a Steel Sword and equip it from inventory.
3. Consume Healing Herbs to restore hit points.
4. Reject healing at full HP, while defeated, or without an herb.
5. Earn XP in combat and calculate a character level.
6. Increase maximum HP as the level rises.
7. Rest safely in Town Square between encounters.
8. Save and restore the equipped weapon using a versioned JSON format.
9. Expose equipment and level information through `GameView`.
10. Write deterministic console-level regression checks.

## 2. Quick start

Extract `SOSCSRPG-Tutorial016-Source.zip` and run:

```bash
cd Tutorial016
code .
env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run
```

To run the included self-tests:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet run -- --self-test
```

**Verification note:** The project and its tests were authored here, but no .NET SDK is installed in the authoring environment. Compilation and test execution have **not** been verified; run them locally before treating the project as a validated release.

## 3. New gameplay commands

| Command | Effect |
|---|---|
| `shop 1` | Open Mara's Trading Post in Town Square |
| `buy 5` | Buy a Healing Herb (4 Gold) |
| `use 5` | Consume one Healing Herb; restore up to 10 HP |
| `buy 7` | Buy a Steel Sword (18 Gold) |
| `equip 7` | Equip an owned Steel Sword |
| `rest` | Restore full HP in Town Square, if alive |
| `status` | Display HP, Gold, XP, level, and equipped weapon |
| `inventory` | Display items in inventory |
| `attack` | Fight with the currently equipped weapon |
| `save` / `load` | Persist and restore equipment and progression |

All earlier commands (`go`, `look`, `map`, `talk`, `choose`, `quests`, `sell`, `leave`, `quit`) remain available.

## 4. New equipment and item data

| ID | Item | Buy price | Sell price | Effect |
|---|---|---:|---:|---|
| 4 | Spider Silk | 6 | 2 | Quest material |
| 5 | Healing Herb | 4 | 1 | Restores up to 10 HP when used |
| 6 | Rope | 8 | 3 | Inventory item; no active effect yet |
| 7 | Steel Sword | 18 | 7 | Equippable weapon, 5–9 damage |

The starting Rusty Sword is a built-in weapon (ID 1, 2–5 damage) and does not require an inventory entry. A purchased Steel Sword must remain in inventory to be restored as equipped from a save.

## 5. Progression rules

The example uses deterministic, easy-to-understand progression:

```text
Level = min(20, floor(XP / 25) + 1)
Maximum HP = 20 + (Level - 1) * 5
```

| Total XP | Level | Maximum HP |
|---:|---:|---:|
| 0 | 1 | 20 |
| 10 | 1 | 20 |
| 25 | 2 | 25 |
| 50 | 3 | 30 |
| 75 | 4 | 35 |

A defeated spider awards 10 XP and 5 Gold. After three spider victories, the player reaches level 2. Maximum HP increases immediately, but **current HP is not automatically refilled**. Use a Healing Herb or return to Town Square and type `rest`.

This chapter intentionally uses a **derived level** instead of storing a mutable level counter. That avoids the risk of saved `Level` disagreeing with saved XP. More complex games may use explicit XP thresholds and level-up events.

## 6. Layered architecture

```text
Console UI / future MonoGame + Myra
                |
          GameAction
                |
         GameController
        /       |        \
   Combat    Inventory   Progression
      |          |            |
  WeaponFactory  ItemFactory  Player.Level
      |          |            |
      +----------+------------+
                 |
        SaveGameService v4
```

The engine handles item consumption and equipment rules. The console adapter only parses input and renders the resulting `GameView`; a Myra button can call the same `GameController.Execute()` method.

## 7. Important design decisions

### Equipment is not consumed

`equip 7` changes `Player.EquippedWeapon` to the Steel Sword, but does not remove the sword from inventory. The inventory record proves ownership. This differs from `use 5`, which removes one Healing Herb.

### Healing is bounded

Healing never raises current HP above maximum HP. A herb is not consumed if the player is already at full HP or defeated. A herb heals up to 10 HP; it is not a resurrection item.

### Rest is a safe-zone action

`rest` works only for living players in Town Square. It is included so the tutorial's combat-and-leveling loop remains playable without an elaborate potion economy. Later chapters can replace it with inn costs, time passage, and status effects.

### Selling equipped equipment

**Known limitation:** The basic trading service still permits selling a Steel Sword that is equipped. The save loader detects this inconsistency and rejects a save whose equipped weapon is no longer owned. A production engine should prevent selling equipped items, or automatically unequip them before the sale. Implement this as an exercise.

### Save schema version 4

Tutorial 015 used JSON v3. Tutorial 016 introduces `EquippedWeaponId`, so it uses **JSON v4**. The level is reconstructed from XP and maximum HP is derived from level. The loader validates the equipped weapon ID and its inventory ownership. Version 3 saves are not automatically migrated.

## 8. Full project source code

The following files are the complete standalone project included in the ZIP. They include all previously implemented world, dialogue, combat, trading, and persistence systems.

### 8.1 `Tutorial016.csproj`

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

### 8.2 `Combat/CombatService.cs`

```csharp
using Tutorial016.Factories;
using Tutorial016.Models;
using Tutorial016.World;

namespace Tutorial016.Combat;

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

### 8.3 `ConsoleUI/CommandParser.cs`

```csharp
namespace Tutorial016.ConsoleUI;

public enum GameCommandKind
{
    Help, Look, Map, Travel, Attack, Status, Inventory, Quests,
    Talk, Choose, Shop, Buy, Sell, LeaveShop, Use, Equip, Rest, Save, Load, Quit, Invalid
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
            "use" => GameCommandKind.Use,
            "equip" => GameCommandKind.Equip,
            "rest" => GameCommandKind.Rest,
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
            or GameCommandKind.Choose or GameCommandKind.Shop
            or GameCommandKind.Use or GameCommandKind.Equip;
        if (needsArgument)
            return tokens.Length == 2 &&
                   int.TryParse(tokens[1], out int id) && id > 0
                ? new(kind, id) : new(GameCommandKind.Invalid);

        return tokens.Length == 1 ? new(kind) : new(GameCommandKind.Invalid);
    }
}
```

### 8.4 `ConsoleUI/ConsoleGame.cs`

```csharp
using Tutorial016.Engine;

namespace Tutorial016.ConsoleUI;

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
            GameCommandKind.Use => GameActionKind.Use,
            GameCommandKind.Equip => GameActionKind.Equip,
            GameCommandKind.Rest => GameActionKind.Rest,
            GameCommandKind.Save => GameActionKind.Save,
            GameCommandKind.Load => GameActionKind.Load,
            _ => throw new ArgumentException("Unsupported command.", nameof(command))
        }, command.Argument, command.Quantity);

    public static void RenderView(GameView view, TextWriter output)
    {
        output.WriteLine($"\n[{view.LocationName}] {view.LocationDescription}");
        output.WriteLine($"HP {view.HitPoints}/{view.MaximumHitPoints} | Gold {view.Gold} | XP {view.Experience} | Level {view.Level} | {view.WeaponName}");
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

### 8.5 `Dialogue/NpcFactory.cs`

```csharp
using Tutorial016.World;

namespace Tutorial016.Dialogue;

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

### 8.6 `Economy/ShopCatalog.cs`

```csharp
using Tutorial016.Factories;
using Tutorial016.Models;
using Tutorial016.World;

namespace Tutorial016.Economy;

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
            new ShopOffer(ItemFactory.RopeId, "Rope", 8, 3),
            new ShopOffer(ItemFactory.SteelSwordId, "Steel Sword", 18, 7)
        });

    public static ShopDefinition? FindAtLocation(int shopId, int locationId) =>
        shopId == TownMerchant.Id && locationId == TownMerchant.LocationId
            ? TownMerchant : null;

    public static IReadOnlyList<ShopDefinition> AtLocation(int locationId) =>
        locationId == TownMerchant.LocationId
            ? new[] { TownMerchant } : Array.Empty<ShopDefinition>();
}
```

### 8.7 `Economy/TradingService.cs`

```csharp
using Tutorial016.Factories;
using Tutorial016.Models;

namespace Tutorial016.Economy;

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

### 8.8 `Engine/GameController.cs`

```csharp
using Tutorial016.Combat;
using Tutorial016.Dialogue;
using Tutorial016.Economy;
using Tutorial016.Factories;
using Tutorial016.Models;
using Tutorial016.Persistence;
using Tutorial016.World;

namespace Tutorial016.Engine;

public enum GameActionKind
{
    Help, Look, Map, Travel, Attack, Status, Inventory, Quests,
    Talk, Choose, Shop, Buy, Sell, LeaveShop, Use, Equip, Rest, Save, Load
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
    int Level, string WeaponName, string LocationName, string LocationDescription,
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
            _player.Gold, _player.ExperiencePoints, _player.Level, _player.EquippedWeapon.Name,
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
                messages.Add("help | look | map | go <id> | attack | status | inventory | quests | talk <id> | choose <id> | shop <id> | buy <itemId> [qty] | sell <itemId> [qty] | leave | use <itemId> | equip <itemId> | rest | save | load | quit");
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
                messages.Add($"{_player.Name} | HP {_player.HitPoints}/{_player.MaximumHitPoints} | Gold {_player.Gold} | XP {_player.ExperiencePoints} | Level {_player.Level} | Weapon {_player.EquippedWeapon.Name}");
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
            case GameActionKind.Use:
                success = action.Argument == ItemFactory.HealingHerbId && _player.IsAlive &&
                    _player.HitPoints < _player.MaximumHitPoints &&
                    _player.Inventory.Count(ItemFactory.HealingHerbId) > 0;
                if (success)
                {
                    _player.Inventory.Remove(ItemFactory.HealingHerbId);
                    int before = _player.HitPoints;
                    _player.Heal(10);
                    messages.Add($"Used Healing Herb. Restored {_player.HitPoints - before} HP.");
                }
                else messages.Add("Cannot use that item: need a Healing Herb and missing HP.");
                break;
            case GameActionKind.Equip:
                success = action.Argument == WeaponFactory.SteelSwordId &&
                    _player.Inventory.Count(WeaponFactory.SteelSwordId) > 0;
                if (success)
                {
                    _player.Equip(WeaponFactory.SteelSword);
                    messages.Add("Equipped Steel Sword (5–9 damage).");
                }
                else messages.Add("Weapon not owned or not equippable.");
                break;
            case GameActionKind.Rest:
                success = _player.IsAlive && _world.CurrentLocationId == WorldFactory.TownSquareId;
                if (success)
                {
                    _player.RestoreHitPoints(_player.MaximumHitPoints);
                    messages.Add("Rested in town. HP fully restored.");
                }
                else messages.Add("You can only rest in Town Square while alive.");
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

### 8.9 `Factories/ItemFactory.cs`

```csharp
using Tutorial016.Models;

namespace Tutorial016.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public const int HealingHerbId = 5;
    public const int RopeId = 6;
    public const int SteelSwordId = WeaponFactory.SteelSwordId;

    public static GameItem SpiderSilk { get; } = new(SpiderSilkId, "Spider Silk");
    public static GameItem HealingHerb { get; } = new(HealingHerbId, "Healing Herb");
    public static GameItem Rope { get; } = new(RopeId, "Rope");
    public static GameItem SteelSword { get; } = new(SteelSwordId, "Steel Sword");

    public static GameItem Get(int id) => id switch
    {
        SpiderSilkId => SpiderSilk,
        HealingHerbId => HealingHerb,
        RopeId => Rope,
        SteelSwordId => SteelSword,
        _ => throw new KeyNotFoundException($"Unknown item ID: {id}")
    };
}
```

### 8.10 `Factories/QuestFactory.cs`

```csharp
using Tutorial016.Models;

namespace Tutorial016.Factories;

public static class QuestFactory
{
    public static Quest CreateSpiderSilkQuest() =>
        new(id: 1, title: "Collect Spider Silk",
            requiredItemId: ItemFactory.SpiderSilkId,
            requiredQuantity: 3, rewardGold: 10, rewardExperience: 20);
}
```

### 8.11 `Factories/WeaponFactory.cs`

```csharp
using Tutorial016.Models;

namespace Tutorial016.Factories;

public static class WeaponFactory
{
    public const int RustySwordId = 1;
    public const int SteelSwordId = 7;
    public static Weapon RustySword { get; } = new(RustySwordId, "Rusty Sword", 2, 5);
    public static Weapon SteelSword { get; } = new(SteelSwordId, "Steel Sword", 5, 9);
    public static Weapon Get(int id) => id switch
    {
        RustySwordId => RustySword,
        SteelSwordId => SteelSword,
        _ => throw new KeyNotFoundException($"Unknown weapon: {id}")
    };
}
```

### 8.12 `Models/GameItem.cs`

```csharp
namespace Tutorial016.Models;

public sealed record GameItem(int Id, string Name);
```

### 8.13 `Models/Inventory.cs`

```csharp
namespace Tutorial016.Models;

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

### 8.14 `Models/Player.cs`

```csharp
using Tutorial016.Factories;

namespace Tutorial016.Models;

public sealed class Player
{
    public string Name { get; }
    public Inventory Inventory { get; } = new();
    public int Level => Math.Min(20, ExperiencePoints / 25 + 1);
    public int MaximumHitPoints => 20 + (Level - 1) * 5;
    public int HitPoints { get; private set; } = 20;
    public bool IsAlive => HitPoints > 0;
    public Weapon EquippedWeapon { get; private set; } = WeaponFactory.RustySword;
    public void Equip(Weapon weapon) => EquippedWeapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
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

### 8.15 `Models/Quest.cs`

```csharp
namespace Tutorial016.Models;

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

### 8.16 `Models/Weapon.cs`

```csharp
namespace Tutorial016.Models;

public sealed record Weapon(int Id, string Name, int MinimumDamage, int MaximumDamage)
{
    public int RollDamage(Random random) => random.Next(MinimumDamage, MaximumDamage + 1);
}
```

### 8.17 `Persistence/SaveData.cs`

```csharp
namespace Tutorial016.Persistence;

public sealed class SaveData
{
    public int Version { get; set; } = 4;
    public int EquippedWeaponId { get; set; } = 1;
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

### 8.18 `Persistence/SaveGameService.cs`

```csharp
using System.Text.Json;
using Tutorial016.Factories;
using Tutorial016.Models;
using Tutorial016.World;

namespace Tutorial016.Persistence;

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
            EquippedWeaponId = player.EquippedWeapon.Id,
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
        if (data.Version != 4)
            throw new InvalidDataException($"Unsupported save version: {data.Version}");
        if (string.IsNullOrWhiteSpace(data.PlayerName))
            throw new InvalidDataException("Missing player name.");
        if (data.Gold < 0 || data.ExperiencePoints < 0 || data.HitPoints < 0 || data.HitPoints > 20 + (Math.Min(20, data.ExperiencePoints / 25 + 1) - 1) * 5)
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

        Weapon weapon;
        try { weapon = WeaponFactory.Get(data.EquippedWeaponId); }
        catch (KeyNotFoundException ex) { throw new InvalidDataException("Invalid saved weapon.", ex); }
        if (weapon.Id != WeaponFactory.RustySwordId &&
            !data.Inventory.Any(i => i.ItemId == weapon.Id && i.Quantity > 0))
            throw new InvalidDataException("Equipped weapon is not owned.");

        var player = new Player(data.PlayerName);
        player.RestoreRewards(data.Gold, data.ExperiencePoints);
        player.RestoreHitPoints(data.HitPoints);
        player.Equip(weapon);
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

### 8.19 `Program.cs`

```csharp
using Tutorial016.ConsoleUI;
using Tutorial016.Engine;
using Tutorial016.Factories;
using Tutorial016.World;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    SelfTest();
    return;
}

var controller = new GameController(Path.Combine(Environment.CurrentDirectory, "savegame.json"));
new ConsoleGame(controller).Run(Console.In, Console.Out);

static void SelfTest()
{
    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }

    Check(CommandParser.Parse("use 5") == new GameCommand(GameCommandKind.Use, 5), "Use command");
    Check(CommandParser.Parse("equip 7") == new GameCommand(GameCommandKind.Equip, 7), "Equip command");
    Check(CommandParser.Parse("equip nope").Kind == GameCommandKind.Invalid, "Invalid equipment ID");
    string path = Path.Combine(Path.GetTempPath(), "soscsrpg-t016-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        var game = new GameController(path, new Random(42));
        Check(game.Snapshot().Level == 1 && game.Snapshot().WeaponName == "Rusty Sword", "Initial level and weapon");
        Check(!game.Execute(new(GameActionKind.Equip, 7)).Succeeded, "Cannot equip unowned weapon");
        Check(!game.Execute(new(GameActionKind.Use, 5)).Succeeded, "Cannot use unowned herb");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Travel to forest");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.SpiderClearingId)).Succeeded, "Travel to spider");
        int turns = 0;
        while (game.Snapshot().CanAttack && turns++ < 30)
            game.Execute(new(GameActionKind.Attack));
        Check(game.Snapshot().Encounter?.Alive == false, "Defeated spider");
        Check(game.Snapshot().Gold == 5, "Gold reward");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Leave clearing");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.TownSquareId)).Succeeded, "Return to town");
        Check(game.Execute(new(GameActionKind.Shop, 1)).Succeeded, "Open shop");
        Check(game.Execute(new(GameActionKind.Buy, ItemFactory.HealingHerbId)).Succeeded, "Buy herb");
        Check(game.Execute(new(GameActionKind.Use, ItemFactory.HealingHerbId)).Succeeded, "Use healing herb");
        Check(game.Snapshot().Inventory.All(i => i.Name != "Healing Herb"), "Herb consumed");
        Check(!game.Execute(new(GameActionKind.Use, ItemFactory.HealingHerbId)).Succeeded, "No repeat consumption");

        // Award additional XP and Gold through the existing combat rules.
        int victories = 1;
        while (victories < 4)
        {
            Check(game.Execute(new(GameActionKind.Rest)).Succeeded, "Rest in town before battle");
            Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Enter forest");
            Check(game.Execute(new(GameActionKind.Travel, WorldFactory.SpiderClearingId)).Succeeded, "Enter clearing");
            turns = 0;
            while (game.Snapshot().CanAttack && turns++ < 30)
                game.Execute(new(GameActionKind.Attack));
            Check(game.Snapshot().Encounter?.Alive == false, "Defeated another spider");
            victories++;
            Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Exit clearing");
            Check(game.Execute(new(GameActionKind.Travel, WorldFactory.TownSquareId)).Succeeded, "Return town");
        }
        Check(game.Snapshot().Level == 2, "Level 2 at 25 XP");
        Check(game.Snapshot().MaximumHitPoints == 25, "Max HP grows with level");
        Check(game.Execute(new(GameActionKind.Shop, 1)).Succeeded, "Open shop again");
        Check(game.Execute(new(GameActionKind.Sell, ItemFactory.SpiderSilkId)).Succeeded, "Sell silk to afford weapon");
        Check(game.Execute(new(GameActionKind.Buy, ItemFactory.SteelSwordId)).Succeeded, "Buy steel sword");
        Check(game.Execute(new(GameActionKind.Equip, ItemFactory.SteelSwordId)).Succeeded, "Equip steel sword");
        Check(game.Snapshot().WeaponName == "Steel Sword", "Weapon view updated");
        Check(game.Execute(new(GameActionKind.Save)).Succeeded, "Save progression");
        Check(game.Execute(new(GameActionKind.Load)).Succeeded, "Load progression");
        Check(game.Snapshot().Level == 2 && game.Snapshot().WeaponName == "Steel Sword", "Level and equipment restored");
        Check(game.Snapshot().Inventory.Any(i => i.Name == "Steel Sword"), "Equipment ownership restored");
        using var reader = new StringReader("help\nstatus\nquit\n");
        using var writer = new StringWriter();
        new ConsoleGame(new GameController(path)).Run(reader, writer);
        Check(writer.ToString().Contains("Level"), "Console shows progression");
        Console.WriteLine("ALL EQUIPMENT AND PROGRESSION CHECKS PASSED");
    }
    finally { if (File.Exists(path)) File.Delete(path); }
}
```

### 8.20 `World/GameWorld.cs`

```csharp
namespace Tutorial016.World;

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

### 8.21 `World/Location.cs`

```csharp
namespace Tutorial016.World;

public sealed record Location(int Id, string Name, string Description,
    IReadOnlyList<int> NeighborIds, int? MonsterId = null);
```

### 8.22 `World/MonsterFactory.cs`

```csharp
namespace Tutorial016.World;

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

### 8.23 `World/WorldFactory.cs`

```csharp
namespace Tutorial016.World;

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

## 9. Guided playthrough

Start with zero Gold. Defeat spiders to earn enough to buy equipment:

```text
status
go 2
go 3
attack
attack
attack
attack
```

Continue attacking until the spider is defeated, then return to Town Square:

```text
go 2
go 1
rest
shop 1
buy 5
inventory
```

Repeat the spider encounter until you have at least 18 Gold. The `rest` command can restore HP between fights. Then:

```text
shop 1
buy 7
equip 7
status
save
quit
```

Restart the application and type `load`, then `status` to confirm the Steel Sword is still equipped and XP/level are restored. The sword's 5–9 damage range is stronger than the starting 2–5 Rusty Sword.

### Using a Healing Herb

After taking damage, return to town if needed and type:

```text
shop 1
buy 5
use 5
status
```

A successful use removes one herb from inventory. If you are at full HP, the command is rejected and the herb is retained.

## 10. Automated regression tests

Run:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet run -- --self-test
```

The built-in checks cover command parsing, invalid equipment use, combat rewards, buying and consuming herbs, repeated consumption rejection, XP-driven leveling, max HP growth, purchasing and equipping a Steel Sword, JSON v4 round-trip, and console rendering.

Expected final line **if all checks pass**:

```text
ALL EQUIPMENT AND PROGRESSION CHECKS PASSED
```

This is a target test marker, **not** a claim that the test has been run here.

## 11. MonoGame + Myra integration

The existing `GameController` remains UI-neutral:

```csharp
using Tutorial016.Engine;

// Called by a Myra button handler:
GameResponse result = controller.Execute(
    new GameAction(GameActionKind.Equip, 7));

// Refresh labels from result.View:
string levelText = $"Level {result.View.Level}";
string weaponText = result.View.WeaponName;
string hpText = $"{result.View.HitPoints}/{result.View.MaximumHitPoints}";

// Show result.Messages in the game's message log.
```

To wire a consumable item button:

```csharp
GameResponse healed = controller.Execute(
    new GameAction(GameActionKind.Use, 5));
```

For a shop Buy button:

```csharp
GameResponse purchased = controller.Execute(
    new GameAction(GameActionKind.Buy, 7, 1));
```

The GUI should disable unusable actions when possible, but the controller must continue to validate them. The ZIP does **not** contain a compiled Myra interface.

## 12. VS Code debugging

1. Set a breakpoint in `Player.Level` and inspect the XP-to-level calculation.
2. Step into `CombatService.Attack()` to inspect the equipped weapon's damage roll.
3. Set a breakpoint in `GameController.Execute()` on `GameActionKind.Use`.
4. Inspect inventory count before and after a herb is consumed.
5. Set a breakpoint in `SaveGameService.Restore()` to inspect `EquippedWeaponId` validation.
6. Verify `GameView.WeaponName` and `GameView.Level` after loading.

| Key | Debugging action |
|---|---|
| F5 | Start / continue |
| F9 | Toggle breakpoint |
| F10 | Step over |
| F11 | Step into |
| Shift+F5 | Stop |

## 13. C# versus C++

| Concept | C# | C++ |
|---|---|---|
| Derived level | Expression property | Const getter method |
| Weapon registry | Static `WeaponFactory` | Registry / factory |
| Equipped item | `Weapon` property | Object / smart pointer / ID |
| Nullable argument | `int?` | `std::optional<int>` |
| Inventory validation | `Dictionary<int,int>` | `std::unordered_map<int,int>` |
| Save format | `System.Text.Json` | JSON serialization library |
| UI-independent actions | `record GameAction` | Value-type command struct |

## 14. Exercises

- [ ] **A — Prevent equipment selling:** Block selling an equipped Steel Sword, or unequip it first.
- [ ] **B — Weapon switching:** Add `equip 1` to switch back to the Rusty Sword.
- [ ] **C — Multiple potions:** Introduce a stronger potion that heals 20 HP.
- [ ] **D — Level thresholds:** Replace the linear XP formula with an increasing XP table.
- [ ] **E — Level-up event:** Emit a structured `LevelUp` event with old/new level.
- [ ] **F — Defense:** Add armor and reduce incoming monster damage.
- [ ] **G — Shop UI:** Build a Myra equipment panel with Equip and Use buttons.
- [ ] **H — Persistence migration:** Upgrade valid JSON v3 saves to v4.
- [ ] **I — Transaction safety:** Make inventory/Gold mutations atomic even if observers throw.
- [ ] **J — xUnit:** Move self-tests to a separate .NET test project.

## 15. GitHub workflow

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
git commit -m "Tutorial 016: equipment potions and progression"
```

## 16. Knowledge check

1. Why is level derived from XP instead of stored separately?
2. Why is the equipped weapon ID added to JSON v4?
3. What happens when `use 5` is called at full HP?
4. Why does `equip 7` not consume the Steel Sword?
5. What must happen when an equipped sword is sold?
6. Why does a level-up increase maximum HP but not automatically heal?
7. Why can Myra use `GameController.Execute()` without knowing combat rules?
8. Which features would you test before calling this a production-ready RPG?

## 17. Summary and next chapter

Tutorial 016 adds real equipment effects, consumable healing, XP-based levels, and a persistence schema that records the selected weapon. The console application remains a self-contained RPG and the engine is structured for later Myra integration.

**Next: Tutorial 017 — Advanced Combat and Loot Tables.** We'll add more varied combat actions, enemy behavior, and data-driven loot selection while preserving the same UI-neutral architecture.
