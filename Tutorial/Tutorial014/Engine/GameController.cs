using Tutorial014.Combat;
using Tutorial014.Dialogue;
using Tutorial014.Factories;
using Tutorial014.Models;
using Tutorial014.Persistence;
using Tutorial014.World;

namespace Tutorial014.Engine;

public enum GameActionKind
{
    Help, Look, Map, Travel, Attack, Status, Inventory, Quests,
    Talk, Choose, Save, Load
}

public sealed record GameAction(GameActionKind Kind, int? Argument = null);

// Immutable view models: the UI cannot mutate engine objects through these snapshots.
public sealed record DestinationView(int Id, string Name);
public sealed record InventoryView(string Name, int Quantity);
public sealed record QuestView(string Title, QuestStatus Status, int Progress, int Required);
public sealed record NpcView(int Id, string Name);
public sealed record ConversationView(int NpcId, string NpcName, string Greeting, IReadOnlyList<DialogueChoice> Choices);
public sealed record EncounterView(string Name, int HitPoints, int MaximumHitPoints, bool Alive);
public sealed record GameView(
    string PlayerName, int HitPoints, int MaximumHitPoints, int Gold, int Experience,
    string WeaponName, string LocationName, string LocationDescription,
    IReadOnlyList<DestinationView> Destinations,
    IReadOnlyList<InventoryView> Inventory,
    IReadOnlyList<QuestView> Quests,
    EncounterView? Encounter,
    IReadOnlyList<NpcView> Npcs, ConversationView? Conversation,
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
            _player.Gold, _player.ExperiencePoints, _player.EquippedWeapon.Name,
            _world.CurrentLocation.Name, _world.CurrentLocation.Description,
            _world.AvailableDestinations().Select(l => new DestinationView(l.Id, l.Name)).ToArray(),
            _player.Inventory.GetEntries().Select(e =>
                new InventoryView(e.ItemId == ItemFactory.SpiderSilkId ? "Spider Silk" : $"Item #{e.ItemId}", e.Quantity)).ToArray(),
            _quests.Select(q => new QuestView(q.Title, q.Status, q.Progress(_player), q.RequiredQuantity)).ToArray(),
            monster is null ? null : new EncounterView(monster.Name, monster.HitPoints, monster.MaximumHitPoints, monster.IsAlive),
            NpcFactory.AtLocation(_world.CurrentLocationId).Select(n => new NpcView(n.Id, n.Name)).ToArray(),
            CurrentConversation(),
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

    public GameResponse Execute(GameAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var messages = new List<string>();
        bool success = true;
        switch (action.Kind)
        {
            case GameActionKind.Help:
                messages.Add("help | look | map | go <id> | attack | status | inventory | quests | talk <id> | choose <id> | save | load | quit");
                break;
            case GameActionKind.Look:
                messages.Add(_world.CurrentLocation.Name + ": " + _world.CurrentLocation.Description);
                break;
            case GameActionKind.Map:
                messages.AddRange(WorldFactory.All().Select(l => $"{l.Id}: {l.Name} -> [{string.Join(", ", l.NeighborIds)}]"));
                break;
            case GameActionKind.Travel:
                success = action.Argument is int id && _player.IsAlive && _world.TravelTo(id);
                if (success) _talkingTo = null;
                messages.Add(success ? "Traveled to " + _world.CurrentLocation.Name : "Travel blocked: invalid route or living enemy.");
                break;
            case GameActionKind.Attack:
                CombatResult combat = _combat.Attack(_player, _world);
                messages.AddRange(combat.Messages);
                success = combat.Outcome is CombatOutcome.PlayerAttacked or CombatOutcome.MonsterDefeated;
                break;
            case GameActionKind.Status:
                messages.Add($"{_player.Name} | HP {_player.HitPoints}/{_player.MaximumHitPoints} | Gold {_player.Gold} | XP {_player.ExperiencePoints}");
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
