using Tutorial008;
using Tutorial008.Factories;
using Tutorial008.Models;

Console.WriteLine("=== SOSCSRPG Tutorial 008 ===");
Console.WriteLine("Events, Delegates, and Quest Notifications");

var player = new Player("Adventurer");
var quest = QuestFactory.CreateSpiderSilkQuest();

using (var presenter = new GamePresenter(player, quest))
{
    quest.Accept();
    player.Inventory.Add(ItemFactory.SpiderSilk, 2);
    player.Inventory.Add(ItemFactory.SpiderSilk);
    Console.WriteLine($"Turn in: {quest.TurnIn(player)}");
    Console.WriteLine($"Turn in again: {quest.TurnIn(player)}");
}

RunChecks();
Console.WriteLine("ALL EVENT CHECKS PASSED");

static void RunChecks()
{
    var player = new Player("Tester");
    var quest = QuestFactory.CreateSpiderSilkQuest();
    int inventoryEvents = 0;
    int statsEvents = 0;
    int questEvents = 0;

    player.Inventory.InventoryChanged += (_, e) =>
    {
        inventoryEvents++;
        Check(e.ItemId == ItemFactory.SpiderSilkId, "Inventory event item ID");
    };
    player.StatsChanged += (_, e) =>
    {
        statsEvents++;
        Check(e.Gold == 10 && e.ExperiencePoints == 20, "Stats event values");
    };
    quest.StatusChanged += (_, _) => questEvents++;

    quest.Accept();
    Check(questEvents == 1, "Accept raises one status event");
    player.Inventory.Add(ItemFactory.SpiderSilk, 3);
    Check(inventoryEvents == 1, "Add raises one inventory event");

    Check(quest.TurnIn(player), "Quest completes");
    Check(inventoryEvents == 2, "Turn-in raises inventory event");
    Check(statsEvents == 1, "Turn-in raises stats event");
    Check(questEvents == 2, "Completion raises status event");
    Check(!quest.TurnIn(player), "Second turn-in rejected");
    Check(inventoryEvents == 2 && statsEvents == 1 && questEvents == 2,
        "Failed turn-in raises no events");

    int calls = 0;
    EventHandler<InventoryChangedEventArgs> temporary = (_, _) => calls++;
    player.Inventory.InventoryChanged += temporary;
    player.Inventory.InventoryChanged -= temporary;
    player.Inventory.Add(ItemFactory.SpiderSilk);
    Check(calls == 0, "Unsubscribed handler is not invoked");
}

static void Check(bool ok, string name)
{
    if (!ok) throw new InvalidOperationException($"FAIL: {name}");
    Console.WriteLine($"PASS: {name}");
}
