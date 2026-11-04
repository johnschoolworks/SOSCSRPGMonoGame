using Tutorial015.ConsoleUI;
using Tutorial015.Engine;
using Tutorial015.Economy;
using Tutorial015.Factories;
using Tutorial015.World;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    SelfTest();
    return;
}

var controller = new GameController(Path.Combine(Environment.CurrentDirectory, "savegame.json"));
new ConsoleGame(controller).Run(Console.In, Console.Out);

static void SelfTest()
{
    static void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }

    Check(CommandParser.Parse("buy 5 2") == new GameCommand(GameCommandKind.Buy, 5, 2),
        "Buy parser supports quantity");
    Check(CommandParser.Parse("sell 4") == new GameCommand(GameCommandKind.Sell, 4),
        "Sell parser defaults to one");
    Check(CommandParser.Parse("buy 5 0").Kind == GameCommandKind.Invalid,
        "Zero quantity rejected by parser");
    Check(CommandParser.Parse("buy 5 -1").Kind == GameCommandKind.Invalid,
        "Negative quantity rejected");
    Check(CommandParser.Parse("shop 1").Kind == GameCommandKind.Shop,
        "Shop command recognized");

    string path = Path.Combine(Path.GetTempPath(),
        "soscsrpg-t015-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        var game = new GameController(path, new Random(42));
        int notifications = 0;
        game.StateChanged += (_, _) => notifications++;
        Check(game.Snapshot().Shops.Single().Name == "Mara's Trading Post",
            "Town shop is visible");
        Check(!game.Execute(new(GameActionKind.Buy, ItemFactory.HealingHerbId)).Succeeded,
            "Cannot buy without opening a shop");
        Check(game.Execute(new(GameActionKind.Shop, ShopCatalog.TownMerchantId)).Succeeded,
            "Open shop");
        Check(!game.Execute(new(GameActionKind.Buy, ItemFactory.HealingHerbId)).Succeeded,
            "Cannot buy without enough Gold");
        Check(!game.Execute(new(GameActionKind.Sell, ItemFactory.SpiderSilkId)).Succeeded,
            "Cannot sell unowned items");
        Check(!game.Execute(new(GameActionKind.Buy, 999)).Succeeded,
            "Unknown item rejected");
        Check(!game.Execute(new(GameActionKind.Buy, ItemFactory.HealingHerbId, int.MaxValue)).Succeeded,
            "Large purchase rejected without overflow");
        Check(game.Execute(new(GameActionKind.LeaveShop)).Succeeded,
            "Close shop");

        Check(game.Execute(new(GameActionKind.Talk, 1)).Succeeded,
            "Talk to Elena");
        Check(game.Execute(new(GameActionKind.Choose, 1)).Succeeded,
            "Accept quest");
        Check(game.Execute(new(GameActionKind.Choose, 3)).Succeeded,
            "End conversation");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded,
            "Travel to forest");
        Check(!game.Execute(new(GameActionKind.Shop, 1)).Succeeded,
            "Remote shop blocked");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.SpiderClearingId)).Succeeded,
            "Travel to spider");
        Check(!game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded,
            "Travel locked in combat");

        int turns = 0;
        while (game.Snapshot().CanAttack && turns++ < 30)
            game.Execute(new(GameActionKind.Attack));
        Check(game.Snapshot().Encounter?.Alive == false, "Spider defeated");
        Check(game.Snapshot().Gold == 5 &&
              game.Snapshot().Inventory.Single().Quantity == 1,
              "Monster rewards awarded");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded,
            "Return to trail");
        Check(game.Execute(new(GameActionKind.Travel, WorldFactory.TownSquareId)).Succeeded,
            "Return to town");
        Check(game.Execute(new(GameActionKind.Shop, 1)).Succeeded,
            "Reopen merchant");

        Check(game.Execute(new(GameActionKind.Buy, ItemFactory.HealingHerbId)).Succeeded,
            "Buy Healing Herb for 4 Gold");
        Check(game.Snapshot().Gold == 1 &&
              game.Snapshot().Inventory.Any(i => i.Name == "Healing Herb" && i.Quantity == 1),
              "Gold deducted and inventory updated");
        Check(!game.Execute(new(GameActionKind.Buy, ItemFactory.RopeId)).Succeeded,
            "Insufficient Gold does not mutate state");
        Check(game.Execute(new(GameActionKind.Sell, ItemFactory.SpiderSilkId)).Succeeded,
            "Sell Spider Silk for 2 Gold");
        Check(game.Snapshot().Gold == 3 &&
              game.Snapshot().Inventory.All(i => i.Name != "Spider Silk"),
              "Sell updates Gold and removes item");
        Check(game.Execute(new(GameActionKind.Save)).Succeeded,
            "Save economy state");
        Check(game.Execute(new(GameActionKind.Sell, ItemFactory.HealingHerbId)).Succeeded,
            "Change state after saving");
        Check(game.Execute(new(GameActionKind.Load)).Succeeded,
            "Load saved state");
        Check(game.Snapshot().Gold == 3 &&
              game.Snapshot().Inventory.Single().Name == "Healing Herb",
              "Gold and merchant items restored");
        Check(game.Snapshot().ActiveShop is null,
            "Shop session not serialized");
        Check(!game.Execute(new(GameActionKind.Sell, ItemFactory.HealingHerbId)).Succeeded,
            "Cannot trade after load without reopening shop");
        Check(game.Execute(new(GameActionKind.Shop, 1)).Succeeded,
            "Reopen shop after load");
        Check(!game.Execute(new(GameActionKind.Sell, ItemFactory.HealingHerbId, 2)).Succeeded,
            "Cannot sell more than owned");
        Check(notifications > 0, "StateChanged notifications");

        using var reader = new StringReader("help\nshop 1\nquit\n");
        using var output = new StringWriter();
        new ConsoleGame(new GameController(path)).Run(reader, output);
        Check(output.ToString().Contains("Mara's Trading Post"),
            "Console shop interface renders");
        Console.WriteLine("ALL SHOP AND ECONOMY CHECKS PASSED");
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
