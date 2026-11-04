using Tutorial012.Combat;
using Tutorial012.Factories;
using Tutorial012.Models;
using Tutorial012.Persistence;
using Tutorial012.World;

namespace Tutorial012.ConsoleUI;

public sealed class GameSession
{
    private readonly SaveGameService _saves = new();
    private readonly CombatService _combat;
    private readonly string _savePath;
    private Player _player;
    private List<Quest> _quests;
    private GameWorld _world;

    public bool IsRunning { get; private set; } = true;
    public Player Player => _player;
    public GameWorld World => _world;
    public IReadOnlyList<Quest> Quests => _quests;

    public GameSession(string savePath, Random? random = null)
    {
        _savePath = Path.GetFullPath(savePath);
        _combat = new CombatService(random ?? new Random());
        _player = new Player("Adventurer");
        _quests = new List<Quest> { QuestFactory.CreateSpiderSilkQuest() };
        _world = new GameWorld();
    }

    public void Run(TextReader input, TextWriter output)
    {
        output.WriteLine("SOSCSRPG Tutorial 012 — Interactive Adventure");
        output.WriteLine("Type 'help' for commands.");
        Describe(output);
        while (IsRunning)
        {
            output.Write("\n> ");
            output.Flush();
            string? line = input.ReadLine();
            if (line is null)
            {
                output.WriteLine("Input closed. Goodbye.");
                IsRunning = false;
                break;
            }
            Execute(CommandParser.Parse(line), output);
        }
    }

    public void Execute(GameCommand command, TextWriter output)
    {
        switch (command.Kind)
        {
            case GameCommandKind.Help:
                output.WriteLine("help | look | map | go <id> | attack | status | inventory");
                output.WriteLine("quests | accept | turnin | save | load | quit");
                break;
            case GameCommandKind.Look:
                Describe(output);
                break;
            case GameCommandKind.Map:
                foreach (Location location in WorldFactory.All())
                    output.WriteLine($"{location.Id}: {location.Name} -> [{string.Join(", ", location.NeighborIds)}]");
                break;
            case GameCommandKind.Travel:
                if (!_player.IsAlive)
                    output.WriteLine("You cannot travel while defeated.");
                else if (_world.TravelTo(command.Argument!.Value))
                    Describe(output);
                else
                    output.WriteLine("Travel blocked: invalid route or living enemy.");
                break;
            case GameCommandKind.Attack:
                if (!_player.IsAlive)
                {
                    output.WriteLine("You have fallen. Load a save or quit.");
                    break;
                }
                if (_world.CurrentEncounter is null || !_world.CurrentEncounter.IsAlive)
                {
                    output.WriteLine("There is no living enemy here.");
                    break;
                }
                CombatOutcome outcome = _combat.Attack(_player, _world);
                output.WriteLine($"Combat outcome: {outcome}");
                if (outcome == CombatOutcome.PlayerDead)
                    output.WriteLine("Game over. Use 'load' or 'quit'.");
                break;
            case GameCommandKind.Status:
                output.WriteLine($"{_player.Name} | HP {_player.HitPoints}/{_player.MaximumHitPoints} | Gold {_player.Gold} | XP {_player.ExperiencePoints}");
                output.WriteLine($"Weapon: {_player.EquippedWeapon.Name}");
                break;
            case GameCommandKind.Inventory:
                var entries = _player.Inventory.GetEntries();
                if (entries.Count == 0) output.WriteLine("Inventory is empty.");
                foreach (var entry in entries)
                    output.WriteLine($"Item #{entry.ItemId}: {entry.Quantity}");
                break;
            case GameCommandKind.Quests:
                foreach (Quest quest in _quests)
                    output.WriteLine($"{quest.Title}: {quest.Status} ({quest.Progress(_player)}/{quest.RequiredQuantity})");
                break;
            case GameCommandKind.Accept:
                Quest first = _quests[0];
                if (first.Status != QuestStatus.NotStarted)
                    output.WriteLine("Quest already accepted or completed.");
                else
                {
                    first.Accept();
                    output.WriteLine($"Accepted: {first.Title}");
                }
                break;
            case GameCommandKind.TurnIn:
                Quest current = _quests[0];
                output.WriteLine(current.TurnIn(_player)
                    ? $"Quest completed! +{current.RewardGold} Gold, +{current.RewardExperience} XP"
                    : "Cannot turn in: quest inactive, completed, or missing required items.");
                break;
            case GameCommandKind.Save:
                _saves.Save(_savePath, _player, _quests, _world);
                output.WriteLine($"Saved: {_savePath}");
                break;
            case GameCommandKind.Load:
                if (!File.Exists(_savePath))
                {
                    output.WriteLine("No save file exists yet.");
                    break;
                }
                try
                {
                    var restored = _saves.Load(_savePath);
                    // Publish the fully restored state only after validation.
                    _player = restored.Player;
                    _quests = restored.Quests;
                    _world = restored.World;
                    output.WriteLine("Game loaded.");
                    Describe(output);
                }
                catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException
                    or UnauthorizedAccessException or InvalidDataException
                    or ArgumentException or OverflowException)
                {
                    output.WriteLine($"Load failed; current game preserved: {ex.Message}");
                }
                break;
            case GameCommandKind.Quit:
                IsRunning = false;
                output.WriteLine("Goodbye, adventurer.");
                break;
            default:
                output.WriteLine("Unknown command. Type 'help'.");
                break;
        }
    }

    private void Describe(TextWriter output)
    {
        output.WriteLine($"\n{_world.CurrentLocation.Name}");
        output.WriteLine(_world.CurrentLocation.Description);
        output.WriteLine("Destinations: " + string.Join(", ",
            _world.AvailableDestinations().Select(l => $"{l.Id}={l.Name}")));
        if (_world.CurrentEncounter is Monster enemy)
            output.WriteLine($"Encounter: {enemy.Name} ({enemy.HitPoints}/{enemy.MaximumHitPoints} HP)" +
                (enemy.IsAlive ? " — travel locked" : " — defeated"));
    }
}
