using Tutorial018.Combat;
using Tutorial018.ConsoleUI;
using Tutorial018.Engine;
using Tutorial018.Factories;
using Tutorial018.World;

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
