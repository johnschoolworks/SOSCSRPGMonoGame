using System.Text.Json;
using Tutorial019.Engine;
using Tutorial019.World;
using Tutorial019.Models;
using Tutorial019.Factories;
using Tutorial019.Dialogue;
using Tutorial019.Economy;
using Tutorial019.Persistence;
using Tutorial019.ConsoleUI;

int passed = 0, failed = 0;
void Test(string label, Action test)
{
    try { test(); Console.WriteLine("PASS " + label); passed++; }
    catch (Exception ex) { Console.WriteLine("FAIL " + label + ": " + ex.Message); failed++; }
}
void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
string TempSave() => Path.Combine(Path.GetTempPath(), "soscsrpg-018-" + Guid.NewGuid().ToString("N") + ".json");
GameController NewGame(string path) => new(path, new Random(42));

Test("World graph has reciprocal edges", () => {
    foreach (var place in WorldFactory.All())
        foreach (int id in place.NeighborIds)
            Require(WorldFactory.Get(id).NeighborIds.Contains(place.Id), $"Missing reverse edge {id}->{place.Id}");
});
Test("Valid travel and invalid travel", () => {
    var game = NewGame(TempSave());
    Require(!game.Execute(new(GameActionKind.Travel, WorldFactory.SpiderClearingId)).Succeeded, "Skipped forest");
    Require(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Forest inaccessible");
    Require(game.Execute(new(GameActionKind.Travel, WorldFactory.TownSquareId)).Succeeded, "Cannot return to town");
});
Test("Quest acceptance through NPC dialogue", () => {
    var game = NewGame(TempSave());
    Require(game.Execute(new(GameActionKind.Talk, NpcFactory.WeaverId)).Succeeded, "NPC unavailable");
    Require(game.Execute(new(GameActionKind.Choose, 1)).Succeeded, "Quest acceptance failed");
    Require(game.Snapshot().Quests.Single().Status == QuestStatus.Active, "Quest not active");
    Require(!game.Execute(new(GameActionKind.Choose, 1)).Succeeded, "Duplicate acceptance succeeded");
});
Test("Shop rejects unaffordable purchases", () => {
    var game = NewGame(TempSave());
    Require(game.Execute(new(GameActionKind.Shop, ShopCatalog.TownMerchantId)).Succeeded, "Shop unavailable");
    Require(!game.Execute(new(GameActionKind.Buy, ItemFactory.HealingHerbId)).Succeeded, "Free item purchased");
    Require(game.Snapshot().Inventory.Count == 0, "Inventory changed");
});
Test("Save/load roundtrip restores location, quest and inventory", () => {
    string path = TempSave();
    try {
        var game = NewGame(path);
        Require(game.Execute(new(GameActionKind.Talk, NpcFactory.WeaverId)).Succeeded, "Talk");
        Require(game.Execute(new(GameActionKind.Choose, 1)).Succeeded, "Accept");
        Require(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Travel");
        Require(game.Execute(new(GameActionKind.Save)).Succeeded, "Save");
        Require(game.Execute(new(GameActionKind.Travel, WorldFactory.TownSquareId)).Succeeded, "Travel after save");
        Require(game.Execute(new(GameActionKind.Load)).Succeeded, "Load");
        Require(game.Snapshot().LocationName == WorldFactory.Get(WorldFactory.ForestTrailId).Name, "Location not restored");
        Require(game.Snapshot().Quests.Single().Status == QuestStatus.Active, "Quest not restored");
        Require(JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("Version").GetInt32() == 4, "Save schema changed");
    } finally { File.Delete(path); }
});
Test("Malformed save does not overwrite live state", () => {
    string path = TempSave();
    try {
        var game = NewGame(path);
        Require(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Travel");
        string before = game.Snapshot().LocationName;
        File.WriteAllText(path, "{ invalid json }");
        Require(!game.Execute(new(GameActionKind.Load)).Succeeded, "Bad save accepted");
        Require(game.Snapshot().LocationName == before, "State was overwritten");
    } finally { File.Delete(path); }
});
Test("Encounter blocks movement and defend works", () => {
    var game = NewGame(TempSave());
    Require(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Forest");
    Require(game.Execute(new(GameActionKind.Travel, WorldFactory.SpiderClearingId)).Succeeded, "Spider clearing");
    Require(!game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Escaped living encounter");
    Require(game.Execute(new(GameActionKind.Defend)).Succeeded, "Defend failed");
});
Test("Victory gives guaranteed silk and rewards only once", () => {
    var game = NewGame(TempSave());
    Require(game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Forest");
    Require(game.Execute(new(GameActionKind.Travel, WorldFactory.SpiderClearingId)).Succeeded, "Spider clearing");
    int turns = 0;
    while (game.Snapshot().CanAttack && turns++ < 80) game.Execute(new(GameActionKind.Attack));
    Require(game.Snapshot().Encounter?.Alive == false, "Monster still alive");
    Require(game.Snapshot().Inventory.Any(i => i.Name == "Spider Silk" && i.Quantity >= 1), "Silk missing");
    int gold = game.Snapshot().Gold;
    Require(!game.Execute(new(GameActionKind.Attack)).Succeeded, "Repeated attack succeeded");
    Require(game.Snapshot().Gold == gold, "Rewards duplicated");
});
Test("Save/load persists defeated encounter without duplicate rewards", () => {
    string path = TempSave();
    try {
        var game = NewGame(path);
        game.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId));
        game.Execute(new(GameActionKind.Travel, WorldFactory.SpiderClearingId));
        int turns = 0;
        while (game.Snapshot().CanAttack && turns++ < 80) game.Execute(new(GameActionKind.Attack));
        Require(game.Snapshot().Encounter?.Alive == false, "Not defeated");
        int gold = game.Snapshot().Gold;
        Require(game.Execute(new(GameActionKind.Save)).Succeeded, "Save");
        Require(game.Execute(new(GameActionKind.Load)).Succeeded, "Load");
        Require(game.Snapshot().Gold == gold && game.Snapshot().Encounter?.Alive == false, "Defeated encounter lost");
        Require(!game.Execute(new(GameActionKind.Attack)).Succeeded, "Repeated attack succeeded");
        Require(game.Snapshot().Gold == gold, "Duplicate reward after load");
    } finally { File.Delete(path); }
});
Test("Terminal parser retains save, load and guard commands", () => {
    Require(CommandParser.Parse("save").Kind == GameCommandKind.Save, "Save parser");
    Require(CommandParser.Parse("load").Kind == GameCommandKind.Load, "Load parser");
    Require(CommandParser.Parse("guard").Kind == GameCommandKind.Defend, "Guard parser");
});
Console.WriteLine($"Results: {passed} passed, {failed} failed");
if (failed > 0) Environment.ExitCode = 1;
