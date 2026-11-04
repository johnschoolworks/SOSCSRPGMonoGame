using Tutorial014.ConsoleUI;
using Tutorial014.Engine;
using Tutorial014.Dialogue;
using Tutorial014.Factories;
using Tutorial014.World;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    SelfTest();
    return;
}

var controller = new GameController(Path.Combine(Environment.CurrentDirectory, "savegame.json"));
new ConsoleGame(controller).Run(Console.In, Console.Out);

static void SelfTest()
{
    static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }

    Check(CommandParser.Parse("go 2") == new GameCommand(GameCommandKind.Travel, 2), "Parser travel");
    Check(CommandParser.Parse("go xyz").Kind == GameCommandKind.Invalid, "Parser rejects bad ID");
    Check(CommandParser.Parse("attack now").Kind == GameCommandKind.Invalid, "Parser rejects extra arguments");
    string save = Path.Combine(Path.GetTempPath(), "soscsrpg-t013-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        var game = new GameController(save, new Random(42));
        int notifications = 0;
        game.StateChanged += (_, _) => notifications++;
        Check(game.Snapshot().LocationName == "Town Square", "Initial snapshot");
        Check(game.Snapshot().Npcs.Single().Name == "Elena the Weaver", "NPC visible at town");
        Check(!game.Execute(new(GameActionKind.Choose, 1)).Succeeded, "Cannot choose without conversation");
        Check(!game.Execute(new(GameActionKind.Talk, 999)).Succeeded, "Unknown NPC rejected");
        Check(game.Execute(new(GameActionKind.Talk, NpcFactory.WeaverId)).Succeeded, "Talk to Elena");
        Check(game.Snapshot().Conversation?.Choices.Count == 3, "Dialogue choices visible");
        Check(game.Execute(new(GameActionKind.Choose, 1)).Succeeded, "Quest accepted through dialogue");
        Check(!game.Execute(new(GameActionKind.Choose, 1)).Succeeded, "Quest cannot be accepted twice");
        Check(game.Execute(new(GameActionKind.Choose, 3)).Succeeded, "Goodbye ends conversation");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Travel to forest");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.SpiderClearingId)).Succeeded, "Travel to clearing");
        Check(game.Snapshot().CanAttack && !game.Snapshot().CanTravel, "UI combat flags");
        Check(game.Snapshot().Npcs.Count == 0, "Town NPC absent from clearing");
        Check(!game.Execute(new(GameActionKind.Talk, NpcFactory.WeaverId)).Succeeded, "Remote dialogue blocked");
        Check(!game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Combat travel lock");
        var attack = game.Execute(new(GameActionKind.Attack));
        Check(attack.Messages.Any(m => m.StartsWith("You hit")), "Structured combat messages");
        int savedHp = game.Snapshot().Encounter!.HitPoints;
        Check(game.Execute(new(GameActionKind.Save)).Succeeded, "Save succeeds");
        game.Execute(new(GameActionKind.Attack));
        Check(game.Execute(new(GameActionKind.Load)).Succeeded, "Load succeeds");
        Check(game.Snapshot().Encounter!.HitPoints == savedHp, "Mid-battle HP restored");
        int turns = 0;
        while (game.Snapshot().CanAttack && turns++ < 30)
            game.Execute(new(GameActionKind.Attack));
        Check(!game.Snapshot().Encounter!.Alive, "Monster defeated");
        Check(game.Snapshot().Gold == 5 && game.Snapshot().Experience == 10, "Rewards granted");
        Check(game.Snapshot().Inventory.Single().Quantity == 1, "Spider Silk obtained");
        Check(game.Execute(new(GameActionKind.Save)).Succeeded, "Save defeated encounter");
        Check(game.Execute(new(GameActionKind.Load)).Succeeded, "Reload defeated encounter");
        Check(!game.Execute(new(GameActionKind.Attack)).Succeeded, "No repeated attack rewards");
        Check(game.Snapshot().Gold == 5, "Gold not duplicated");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Travel unlocked");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.TownSquareId)).Succeeded, "Return to town");
        Check(game.Execute(new(GameActionKind.Talk, NpcFactory.WeaverId)).Succeeded, "Talk after travel");
        Check(!game.Execute(new(GameActionKind.Choose, 2)).Succeeded, "Cannot turn in with one silk");
        Check(game.Execute(new(GameActionKind.Save)).Succeeded, "Save quest state");
        Check(game.Execute(new(GameActionKind.Load)).Succeeded, "Load quest state");
        Check(game.Snapshot().Conversation is null, "Conversation cleared after load");
        Check(game.Execute(new(GameActionKind.Talk, NpcFactory.WeaverId)).Succeeded, "Reopen conversation after load");
        Check(game.Snapshot().Quests.Single().Status == Tutorial014.Models.QuestStatus.Active, "Quest persisted");
        Check(notifications > 0, "StateChanged published");
        var scripted = new ConsoleGame(new GameController(save));
        using var reader = new StringReader("help\nstatus\nquit\n".Replace("\\n", "\n"));
        using var writer = new StringWriter();
        scripted.Run(reader, writer);
        Check(writer.ToString().Contains("Goodbye"), "Console adapter runs");
        new ConsoleGame(new GameController(save)).Run(new StringReader(""), TextWriter.Null);
        Console.WriteLine("ALL NPC DIALOGUE CHECKS PASSED");
    }
    finally
    {
        if (File.Exists(save)) File.Delete(save);
    }
}
