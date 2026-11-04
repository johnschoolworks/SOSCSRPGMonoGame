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
