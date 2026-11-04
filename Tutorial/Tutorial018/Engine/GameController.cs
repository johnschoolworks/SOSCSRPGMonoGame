using Tutorial018.Combat;
using Tutorial018.Dialogue;
using Tutorial018.Economy;
using Tutorial018.Factories;
using Tutorial018.Models;
using Tutorial018.Persistence;
using Tutorial018.World;

namespace Tutorial018.Engine;

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
