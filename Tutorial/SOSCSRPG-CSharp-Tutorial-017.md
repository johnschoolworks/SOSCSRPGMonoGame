# SOSCSRPG C# Tutorial 017 — Advanced Combat and Loot Tables

**Series:** SOSCSRPG C# Tutorials  
**Platform:** Ubuntu 26.04 · .NET 10 · VS Code  
**Prerequisite:** Tutorial 016 — Equipment, Potions, and Character Progression  
**Project:** `Tutorial017` · **Language:** English

> This is an original educational adaptation inspired by Scott Lilly's SOSCSRPG. The ZIP contains a standalone .NET 10 terminal RPG with a UI-independent controller. It does not contain a compiled MonoGame + Myra window.

## 1. Learning goals

This chapter expands the basic attack/counterattack loop with tactical choices and variable loot:

1. Add a **Defend** action that halves the next immediate enemy hit.
2. Give monsters a **10% dodge chance** against each player attack.
3. Give successful attacks a **15% critical-hit chance** (double damage).
4. Introduce a **data-driven optional loot table** using `LootEntry`.
5. Preserve **guaranteed Spider Silk** so quest progression does not depend on luck.
6. Validate inventory and reward limits before a killing blow.
7. Keep combat results structured for both Console and future Myra interfaces.
8. Retain the JSON **version 4** save format.
9. Add deterministic regression checks and discuss randomness in tests.

## 2. Run the project

Unzip the accompanying source archive, then use:

```bash
cd Tutorial017
env -u DOTNET_ROOT /usr/bin/dotnet restore
env -u DOTNET_ROOT /usr/bin/dotnet build --no-restore
env -u DOTNET_ROOT /usr/bin/dotnet run
```

Run the built-in regression checks:

```bash
env -u DOTNET_ROOT /usr/bin/dotnet run -- --self-test
```

The expected final marker is:

```text
ALL ADVANCED COMBAT CHECKS PASSED
```

**Verification:** The files were generated and packaged, but this authoring environment does not have a .NET SDK. The project has **not been compilation- or runtime-verified**. Run the commands on your Ubuntu machine and address any compiler errors before release.

## 3. New combat commands

| Command | Meaning |
|---|---|
| `attack` or `a` | Attack with equipped weapon; monster may dodge, or attack may crit |
| `defend` or `guard` | Spend a turn guarding; take half of the next immediate monster hit |
| `use 5` | Consume a Healing Herb if wounded |
| `equip 7` | Equip Steel Sword when owned |
| `status` | Show HP, XP, level, Gold, weapon |
| `inventory` | Inspect guaranteed and bonus loot |
| `save` / `load` | Preserve HP, inventory, XP, location, and encounter |

### A suggested adventure

```text
talk 1
choose 1
choose 3
go 2
go 3
defend
attack
attack
attack
inventory
status
save
```

Keep attacking until the monster is defeated. The number of turns is variable because attacks can be dodged and critical hits can shorten a fight.

## 4. Combat probabilities

For each `attack` action:

1. Roll dodge: **10%**. On dodge, damage is zero and the monster still counterattacks.
2. If not dodged, roll critical hit: **15% conditional on the hit**.
3. Roll weapon damage. Critical hits multiply this damage by two.
4. If the monster survives, it counterattacks once.

Therefore, the probability of a critical hit **per attack attempt** is:

\[
P(\text{critical}) = (1 - 0.10)\times 0.15 = 0.135
\]

That is **13.5%**, not 15%, of all attack attempts.

For a weapon that rolls 5–9 damage, a critical hit deals 10–18 damage. Defending is a separate turn and does not attack.

## 5. Defend is an immediate action

`Defend()` rolls the current monster's normal attack damage, halves it using integer division, and applies it immediately:

```csharp
int raw = _random.Next(monster.MinimumDamage, monster.MaximumDamage + 1);
int reduced = raw / 2;
player.TakeDamage(reduced);
```

For example, an incoming hit of 5 becomes 2 damage.

**Design choice:** There is no persistent Guard status effect. The defense is resolved during the action, so it does not require a new save field or special expiration logic. A future version could introduce lasting shields, poison, stun, or cooldowns.

## 6. Loot tables

`LootTable.ForMonster()` provides independent optional drops:

| Monster | Guaranteed drop | Optional drop | Chance |
|---|---|---|---:|
| Venomous Spider | Spider Silk ×1 | Healing Herb ×1 | 25% |
| Forest Wolf | None | Healing Herb ×1 | 35% |

Optional drops are rolled **once when the killing blow is determined**. They are granted only when the monster dies. A defeated encounter records `RewardsClaimed=true` in the save file, preventing the same monster from yielding rewards again after load.

The implementation uses independent Bernoulli rolls. If you add several entries, multiple bonus items may drop in one victory. This differs from a weighted-choice table where exactly one item is selected.

### Why guaranteed quest drops matter

Elena's Spider Silk quest requires three silk. If the quest item were only a random bonus drop, the tutorial's core quest could require an unpredictable number of victories. Keeping it guaranteed preserves predictable quest progression while demonstrating optional loot.

## 7. Architecture

```text
Terminal command / Myra button
             |
             v
GameController.Execute(GameAction)
             |
        +----+------+
        |           |
     Attack       Defend
        |           |
        v           v
     CombatService (Random injected)
        |
        +--> Monster dodge
        +--> Weapon damage / critical
        +--> Monster counterattack
        +--> LootTable.Roll() on lethal hit
        +--> Inventory / Gold / XP
        |
        v
CombatResult { Outcome, Messages }
        |
        v
GameResponse { GameView, Messages, Succeeded }
```

No `Console.WriteLine()` calls are required inside the combat service. The terminal UI displays `GameResponse.Messages`; Myra can append the same messages to its combat log.

## 8. Complete project source code

All source files are included below and in the ZIP. Code carried over from Tutorial 016 is retained so this chapter can run independently.

### 8.1 `Combat/CombatService.cs`

```csharp
using Tutorial017.Factories;
using Tutorial017.Models;
using Tutorial017.World;

namespace Tutorial017.Combat;

public enum CombatOutcome
{
    NoEncounter, PlayerDead, PlayerAttacked, MonsterDefeated, Defended
}

public sealed record CombatResult(CombatOutcome Outcome, IReadOnlyList<string> Messages);

public sealed class CombatService
{
    private readonly Random _random;
    public CombatService(Random random) =>
        _random = random ?? throw new ArgumentNullException(nameof(random));

    // Defensive turn: a monster strikes once, but damage is reduced by half,
    // rounded down. The guard expires immediately; there is no persistent buff.
    public CombatResult Defend(Player player, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(world);
        Monster? monster = world.CurrentEncounter;
        if (monster is null || !monster.IsAlive)
            return new(CombatOutcome.NoEncounter, new[] { "Nothing to defend against." });
        if (!player.IsAlive)
            return new(CombatOutcome.PlayerDead, new[] { "The player has fallen." });

        int raw = _random.Next(monster.MinimumDamage, monster.MaximumDamage + 1);
        int reduced = raw / 2;
        player.TakeDamage(reduced);
        var messages = new List<string>
        {
            $"You guard against {monster.Name}. Incoming damage {raw} -> {reduced}.",
            $"HP: {player.HitPoints}/{player.MaximumHitPoints}"
        };
        return new(player.IsAlive ? CombatOutcome.Defended : CombatOutcome.PlayerDead, messages);
    }

    public CombatResult Attack(Player player, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(world);
        Monster? monster = world.CurrentEncounter;
        if (monster is null || !monster.IsAlive)
            return new(CombatOutcome.NoEncounter, new[] { "No living enemy to attack." });
        if (!player.IsAlive)
            return new(CombatOutcome.PlayerDead, new[] { "The player has fallen." });

        // An enemy has a 10% chance to evade an attack.
        bool dodged = _random.NextDouble() < 0.10;
        bool critical = !dodged && _random.NextDouble() < 0.15;
        int baseDamage = dodged ? 0 : player.EquippedWeapon.RollDamage(_random);
        int damage = critical ? checked(baseDamage * 2) : baseDamage;
        var messages = new List<string>();

        // Decide optional loot BEFORE changing monster state.
        IReadOnlyList<LootEntry> drops = !dodged && damage >= monster.HitPoints
            ? LootTable.Roll(monster.Id, _random)
            : Array.Empty<LootEntry>();

        if (!dodged && damage >= monster.HitPoints)
        {
            if (!player.CanReceiveRewards(monster.RewardGold, monster.RewardExperience))
                throw new OverflowException("Combat reward capacity exceeded.");

            var quantities = new Dictionary<int, long>();
            if (monster.LootItemId is int guaranteedId)
                quantities[guaranteedId] = 1;
            foreach (LootEntry drop in drops)
                quantities[drop.ItemId] = quantities.GetValueOrDefault(drop.ItemId) + drop.Quantity;
            foreach (var (id, amount) in quantities)
                if (player.Inventory.Count(id) + amount > int.MaxValue)
                    throw new OverflowException("Loot inventory capacity exceeded.");
        }

        if (dodged)
        {
            messages.Add($"{monster.Name} dodges your attack!");
        }
        else
        {
            monster.TakeDamage(damage);
            messages.Add(critical
                ? $"CRITICAL HIT! You strike {monster.Name} for {damage}. HP: {monster.HitPoints}"
                : $"You hit {monster.Name} for {damage}. HP: {monster.HitPoints}");
        }

        if (!monster.IsAlive)
        {
            if (!monster.ClaimRewards())
                return new(CombatOutcome.NoEncounter, messages);
            if (monster.LootItemId is int itemId)
            {
                player.Inventory.Add(ItemFactory.Get(itemId));
                messages.Add($"Guaranteed loot: {ItemFactory.Get(itemId).Name} x1");
            }
            foreach (LootEntry drop in drops)
            {
                player.Inventory.Add(ItemFactory.Get(drop.ItemId), drop.Quantity);
                messages.Add($"Bonus loot: {ItemFactory.Get(drop.ItemId).Name} x{drop.Quantity}");
            }
            player.ReceiveRewards(monster.RewardGold, monster.RewardExperience);
            messages.Add($"Victory! +{monster.RewardGold} Gold, +{monster.RewardExperience} XP");
            return new(CombatOutcome.MonsterDefeated, messages);
        }

        int counterDamage = _random.Next(monster.MinimumDamage, monster.MaximumDamage + 1);
        player.TakeDamage(counterDamage);
        messages.Add($"{monster.Name} counterattacks for {counterDamage}. HP: {player.HitPoints}/{player.MaximumHitPoints}");
        return new(player.IsAlive ? CombatOutcome.PlayerAttacked : CombatOutcome.PlayerDead, messages);
    }
}
```

### 8.2 `Combat/LootTable.cs`

```csharp
using Tutorial017.Factories;
using Tutorial017.World;

namespace Tutorial017.Combat;

// Independent optional rolls: each entry has a chance in [0, 1].
// Guaranteed quest loot remains defined on Monster.LootItemId.
public sealed record LootEntry(int ItemId, double Chance, int Quantity = 1);

public static class LootTable
{
    public static IReadOnlyList<LootEntry> ForMonster(int monsterId) => monsterId switch
    {
        MonsterFactory.SpiderId => new[] { new LootEntry(ItemFactory.HealingHerbId, 0.25) },
        MonsterFactory.WolfId => new[] { new LootEntry(ItemFactory.HealingHerbId, 0.35) },
        _ => Array.Empty<LootEntry>()
    };

    public static IReadOnlyList<LootEntry> Roll(int monsterId, Random random) =>
        ForMonster(monsterId).Where(entry => random.NextDouble() < entry.Chance).ToArray();
}
```

### 8.3 `ConsoleUI/CommandParser.cs`

```csharp
namespace Tutorial017.ConsoleUI;

public enum GameCommandKind
{
    Help, Look, Map, Travel, Attack, Defend, Status, Inventory, Quests,
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
            "defend" or "guard" => GameCommandKind.Defend,
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
using Tutorial017.Engine;

namespace Tutorial017.ConsoleUI;

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
            GameCommandKind.Defend => GameActionKind.Defend,
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
using Tutorial017.World;

namespace Tutorial017.Dialogue;

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
using Tutorial017.Factories;
using Tutorial017.Models;
using Tutorial017.World;

namespace Tutorial017.Economy;

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
using Tutorial017.Factories;
using Tutorial017.Models;

namespace Tutorial017.Economy;

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
using Tutorial017.Combat;
using Tutorial017.Dialogue;
using Tutorial017.Economy;
using Tutorial017.Factories;
using Tutorial017.Models;
using Tutorial017.Persistence;
using Tutorial017.World;

namespace Tutorial017.Engine;

public enum GameActionKind
{
    Help, Look, Map, Travel, Attack, Defend, Status, Inventory, Quests,
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
                messages.Add("help | look | map | go <id> | attack | defend | status | inventory | quests | talk <id> | choose <id> | shop <id> | buy <itemId> [qty] | sell <itemId> [qty] | leave | use <itemId> | equip <itemId> | rest | save | load | quit");
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
            case GameActionKind.Defend:
                CombatResult guarded = _combat.Defend(_player, _world);
                messages.AddRange(guarded.Messages);
                success = guarded.Outcome == CombatOutcome.Defended;
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
using Tutorial017.Models;

namespace Tutorial017.Factories;

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
using Tutorial017.Models;

namespace Tutorial017.Factories;

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
using Tutorial017.Models;

namespace Tutorial017.Factories;

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
namespace Tutorial017.Models;

public sealed record GameItem(int Id, string Name);
```

### 8.13 `Models/Inventory.cs`

```csharp
namespace Tutorial017.Models;

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
using Tutorial017.Factories;

namespace Tutorial017.Models;

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
namespace Tutorial017.Models;

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
namespace Tutorial017.Models;

public sealed record Weapon(int Id, string Name, int MinimumDamage, int MaximumDamage)
{
    public int RollDamage(Random random) => random.Next(MinimumDamage, MaximumDamage + 1);
}
```

### 8.17 `Persistence/SaveData.cs`

```csharp
namespace Tutorial017.Persistence;

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
using Tutorial017.Factories;
using Tutorial017.Models;
using Tutorial017.World;

namespace Tutorial017.Persistence;

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
using Tutorial017.Combat;
using Tutorial017.ConsoleUI;
using Tutorial017.Engine;
using Tutorial017.Factories;
using Tutorial017.World;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    SelfTest();
    return;
}

var controller = new GameController(Path.Combine(Environment.CurrentDirectory, "savegame.json"));
new ConsoleGame(controller).Run(Console.In, Console.Out);

static void SelfTest()
{
    static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }

    Check(CommandParser.Parse("defend").Kind == GameCommandKind.Defend,
        "Defend command recognized");
    Check(CommandParser.Parse("guard").Kind == GameCommandKind.Defend,
        "Guard alias recognized");
    Check(CommandParser.Parse("defend extra").Kind == GameCommandKind.Invalid,
        "Unexpected arguments rejected");
    Check(LootTable.ForMonster(MonsterFactory.SpiderId).Single().Chance == 0.25,
        "Spider optional drop chance");
    Check(LootTable.ForMonster(MonsterFactory.WolfId).Single().Chance == 0.35,
        "Wolf optional drop chance");

    string path = Path.Combine(Path.GetTempPath(),
        "soscsrpg-t017-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        var game = new GameController(path, new Random(42));
        Check(!game.Execute(new(GameActionKind.Defend)).Succeeded,
            "Cannot defend without an encounter");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded,
            "Travel to forest");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.SpiderClearingId)).Succeeded,
            "Enter spider clearing");
        int initialHp = game.Snapshot().HitPoints;
        var guard = game.Execute(new(GameActionKind.Defend));
        Check(guard.Succeeded && guard.Messages.Any(m => m.Contains("guard")),
            "Defend consumes a turn and reduces incoming damage");
        Check(game.Snapshot().HitPoints <= initialHp,
            "Defend never heals player");
        Check(!game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded,
            "Cannot leave a living encounter");

        int turns = 0;
        while (game.Snapshot().CanAttack && turns++ < 80)
            game.Execute(new(GameActionKind.Attack));
        Check(game.Snapshot().Encounter?.Alive == false,
            "Spider defeated despite dodge chance");
        Check(game.Snapshot().Gold == 5 && game.Snapshot().Experience == 10,
            "Victory rewards granted exactly once");
        Check(game.Snapshot().Inventory.Any(i => i.Name == "Spider Silk" && i.Quantity == 1),
            "Guaranteed quest drop awarded");
        int gold = game.Snapshot().Gold;
        Check(!game.Execute(new(GameActionKind.Attack)).Succeeded,
            "No attack against defeated monster");
        Check(game.Snapshot().Gold == gold, "No duplicate rewards");
        Check(game.Execute(new(GameActionKind.Save)).Succeeded,
            "Save after combat");
        Check(game.Execute(new(GameActionKind.Load)).Succeeded,
            "Load combat state");
        Check(game.Snapshot().Gold == gold && game.Snapshot().Encounter?.Alive == false,
            "Defeat and rewards persist");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded,
            "Travel unlocked after combat");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.TownSquareId)).Succeeded,
            "Return to town");
        using var reader = new StringReader("help\nquit\n");
        using var writer = new StringWriter();
        new ConsoleGame(new GameController(path)).Run(reader, writer);
        Check(writer.ToString().Contains("defend"), "Console help includes defend");
        Console.WriteLine("ALL ADVANCED COMBAT CHECKS PASSED");
    }
    finally { if (File.Exists(path)) File.Delete(path); }
}
```

### 8.20 `Tutorial017.csproj`

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

### 8.21 `World/GameWorld.cs`

```csharp
namespace Tutorial017.World;

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

### 8.22 `World/Location.cs`

```csharp
namespace Tutorial017.World;

public sealed record Location(int Id, string Name, string Description,
    IReadOnlyList<int> NeighborIds, int? MonsterId = null);
```

### 8.23 `World/MonsterFactory.cs`

```csharp
namespace Tutorial017.World;

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

### 8.24 `World/WorldFactory.cs`

```csharp
namespace Tutorial017.World;

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

## 9. Save/Load and compatibility

The save schema remains **version 4** from Tutorial 016. The following values are persisted:

- Player HP, XP, Gold, inventory, and equipped weapon ID.
- Current world location and monster ID.
- Current monster HP and `RewardsClaimed` state.
- Quest status.

The following values are **not** persisted:

- Random-number-generator internal state.
- Temporary guard effects (none are active between turns).
- An unclaimed optional loot roll (the roll is performed and awarded on victory).
- Open shop and NPC dialogue panels.

**Consequence:** Loading the same mid-combat save can lead to different future dodges, critical hits, and loot. This is acceptable for a casual RPG but not for deterministic replays. A deterministic replay system would serialize a PRNG state or a sequence of game actions and rolls.

The loader intentionally does not migrate old save versions. Tutorial 016 and 017 share v4, but v1–v3 require explicit migration logic.

## 10. Testing strategy

The self-test uses `new Random(42)` so its behavior is reproducible within the same runtime implementation. It validates command parsing, defense, travel locks, combat victory, guaranteed loot, reward-once semantics, save/load, and console output.

It does **not** prove that observed drop frequencies match the configured percentages. For that, add a Monte Carlo test with many independent encounters and an appropriate tolerance.

A useful deterministic unit-test seam is to inject a fake random provider with a known sequence of `NextDouble()` and `Next()` values. This allows testing specific cases such as dodge, critical hit, no bonus loot, and bonus loot without depending on a fixed `Random` seed.

## 11. Security and correctness notes

- Validate `LootEntry.Chance` in the range `[0,1]` and positive quantities if loot tables are loaded from untrusted files. This example uses hardcoded values.
- Reward and inventory capacity checks occur before lethal damage, avoiding a common partial-reward bug.
- Combat rewards and inventory mutations still publish synchronous events. A throwing event subscriber can interrupt the sequence after some mutations. For production, stage state changes and commit atomically.
- Combat remains single-threaded. Do not dispatch overlapping attack actions from GUI input threads.
- The `CombatResult` is a list of narration strings rather than a strongly typed combat event stream. Typed events would improve localization and UI animation.
- A dead player cannot defend or attack; loading a previous save is the recovery mechanism.

## 12. MonoGame + Myra adapter pattern

The engine exposes a UI-neutral action:

```csharp
GameResponse result = controller.Execute(
    new GameAction(GameActionKind.Defend));

// In your existing Myra UI:
// guardButton.Click += (_, _) => Dispatch(new GameAction(GameActionKind.Defend));
// Refresh HP, encounter HP, and combat log using result.View / result.Messages.
```

Do not place dodge or critical-hit logic in Myra event handlers. The same combat rules must work with either the terminal UI or the graphical frontend.

**Scope:** This ZIP is a console executable and a reusable controller. It does not include a runnable MonoGame DesktopGL + Myra application.

## 13. C# versus C++

| Feature | C# | C++ |
|---|---|---|
| RNG | `System.Random` | `std::mt19937` + distributions |
| Critical chance | `random.NextDouble()` | `std::bernoulli_distribution` |
| Loot records | `record LootEntry` | `struct LootEntry` |
| Read-only drop list | `IReadOnlyList<LootEntry>` | `std::span<const LootEntry>` / vector |
| Nullable monster | `Monster?` | Pointer / optional reference |
| Result messages | `IReadOnlyList<string>` | `std::vector<std::string>` |
| Overflow preflight | `long` arithmetic | `std::int64_t` arithmetic |

## 14. Exercises

- [ ] **A:** Add a `flee` action with a 40% success chance.
- [ ] **B:** Add a 10% chance for a monster to land a critical counterattack.
- [ ] **C:** Create a new monster with a different loot table.
- [ ] **D:** Add an uncommon crafting material with a 5% drop chance.
- [ ] **E:** Implement a `LootTable.Validate()` method for modded JSON data.
- [ ] **F:** Add a `CombatEvent` hierarchy instead of string-only narration.
- [ ] **G:** Add an in-game combat log panel in Myra.
- [ ] **H:** Unit-test the dodge/critical branches with a fake RNG.
- [ ] **I:** Add a status effect with a defined duration and save/load fields.
- [ ] **J:** Make rewards and loot commits transactional even if observers throw.

## 15. VS Code debugging

1. Open `Tutorial017` in VS Code.
2. Put a breakpoint in `CombatService.Attack()`.
3. Enter `go 2`, then `go 3`, then `attack`.
4. Inspect `dodged`, `critical`, `baseDamage`, and `damage`.
5. Step into `LootTable.Roll()` when a lethal attack occurs.
6. Inspect `drops` and `monster.RewardsClaimed`.
7. Put a breakpoint in `CombatService.Defend()` and enter `defend`.
8. Save and load after a victory to inspect reward-once behavior.

## 16. GitHub workflow

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
git commit -m "Tutorial 017: advanced combat and optional loot tables"
```

## 17. Knowledge check

1. Why is the overall critical chance 13.5%, not 15%?
2. What is the difference between independent loot rolls and weighted exclusive drops?
3. Why is Spider Silk guaranteed?
4. Why does Defend not need a new save field?
5. Why must rewards be preflight-checked before a killing blow?
6. Why is the RNG state not reproducible after loading a mid-combat save?
7. Why should combat narration be returned as data rather than printed?
8. What would change if loot tables were loaded from user-supplied JSON?

## 18. Chapter summary

Tutorial 017 introduces **defensive turns, monster dodges, critical hits, optional loot, and combat regression checks**, while preserving the UI-neutral architecture from Tutorial 013 and the progression/save format from Tutorial 016.

**Next: Tutorial 018 — Complete World and Quest Integration.** We'll expand the world and tie together locations, monsters, NPCs, quests, and progression into a coherent adventure.
