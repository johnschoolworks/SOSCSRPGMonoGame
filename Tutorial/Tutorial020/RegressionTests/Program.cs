using System.Text.Json;
using Tutorial020.Engine;
using Tutorial020.World;
using Tutorial020.Models;
using Tutorial020.Factories;
using Tutorial020.Dialogue;
using Tutorial020.Economy;
using Tutorial020.Persistence;
using Tutorial020.ConsoleUI;

int passed = 0, failed = 0;
void Test(string label, Action test)
{
    try { test(); Console.WriteLine("PASS " + label); passed++; }
    catch (Exception ex) { Console.WriteLine("FAIL " + label + ": " + ex.Message); failed++; }
}
void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
string TempSave() => Path.Combine(Path.GetTempPath(), "soscsrpg-020-" + Guid.NewGuid().ToString("N") + ".json");
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

// Final-release regression expansion: independent assertions, deterministic inputs.
Test("Initial player and quest invariants", () => {
    var v = NewGame(TempSave()).Snapshot();
    Require(v.HitPoints == 20 && v.HitPoints == v.MaximumHitPoints, "Initial HP");
    Require(v.Gold == 0 && v.Experience == 0, "Initial rewards");
    Require(v.Quests.Count == 1 && v.Quests[0].Status == QuestStatus.NotStarted, "Initial quest");
});
Test("Invalid NPC is rejected", () => {
    var g = NewGame(TempSave());
    Require(!g.Execute(new(GameActionKind.Talk, 9999)).Succeeded, "Unknown NPC accepted");
});
Test("Dialogue choice without conversation is rejected", () => {
    var g = NewGame(TempSave());
    Require(!g.Execute(new(GameActionKind.Choose, 1)).Succeeded, "Orphan choice accepted");
});
Test("Invalid destination preserves location", () => {
    var g = NewGame(TempSave()); var before = g.Snapshot().LocationName;
    Require(!g.Execute(new(GameActionKind.Travel, 9999)).Succeeded, "Invalid travel accepted");
    Require(g.Snapshot().LocationName == before, "Location changed");
});
Test("Attack and defend outside combat rejected", () => {
    var g = NewGame(TempSave());
    Require(!g.Execute(new(GameActionKind.Attack)).Succeeded, "Attack in town");
    Require(!g.Execute(new(GameActionKind.Defend)).Succeeded, "Defend in town");
});
Test("Cannot use unowned healing herb", () => {
    var g = NewGame(TempSave());
    Require(!g.Execute(new(GameActionKind.Use, ItemFactory.HealingHerbId)).Succeeded, "Unowned herb used");
});
Test("Cannot equip unowned sword", () => {
    var g = NewGame(TempSave());
    Require(!g.Execute(new(GameActionKind.Equip, WeaponFactory.SteelSwordId)).Succeeded, "Unowned sword equipped");
});
Test("Cannot trade without opening shop", () => {
    var g = NewGame(TempSave());
    Require(!g.Execute(new(GameActionKind.Buy, ItemFactory.HealingHerbId)).Succeeded, "Bought without shop");
    Require(!g.Execute(new(GameActionKind.Sell, ItemFactory.HealingHerbId)).Succeeded, "Sold without shop");
});
Test("Invalid shop and leave-shop rejected", () => {
    var g = NewGame(TempSave());
    Require(!g.Execute(new(GameActionKind.Shop, 9999)).Succeeded, "Unknown shop");
    Require(!g.Execute(new(GameActionKind.LeaveShop)).Succeeded, "Left unopened shop");
});
Test("Rest in town works and rest in forest is rejected", () => {
    var g = NewGame(TempSave());
    Require(g.Execute(new(GameActionKind.Rest)).Succeeded, "Town rest failed");
    Require(g.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Forest travel");
    Require(!g.Execute(new(GameActionKind.Rest)).Succeeded, "Forest rest succeeded");
});
Test("Missing save file preserves current state", () => {
    var path = TempSave(); var g = NewGame(path);
    Require(g.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Travel");
    var before = g.Snapshot().LocationName;
    Require(!g.Execute(new(GameActionKind.Load)).Succeeded, "Missing file accepted");
    Require(g.Snapshot().LocationName == before, "State mutated");
});
Test("Unsupported save version preserves state", () => {
    var path = TempSave();
    try {
        var g = NewGame(path);
        Require(g.Execute(new(GameActionKind.Save)).Succeeded, "Save");
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        node["Version"] = 999;
        File.WriteAllText(path, node.ToJsonString());
        var before = g.Snapshot().LocationName;
        Require(!g.Execute(new(GameActionKind.Load)).Succeeded, "Unknown version accepted");
        Require(g.Snapshot().LocationName == before, "State mutated");
    } finally { File.Delete(path); }
});
Test("Save and load clear transient shop session", () => {
    var path = TempSave();
    try {
        var g = NewGame(path);
        Require(g.Execute(new(GameActionKind.Shop, ShopCatalog.TownMerchantId)).Succeeded, "Open shop");
        Require(g.Execute(new(GameActionKind.Save)).Succeeded, "Save");
        Require(g.Execute(new(GameActionKind.Load)).Succeeded, "Load");
        Require(g.Snapshot().ActiveShop is null, "Shop session survived load");
    } finally { File.Delete(path); }
});
Test("Save and load clear transient dialogue", () => {
    var path = TempSave();
    try {
        var g = NewGame(path);
        Require(g.Execute(new(GameActionKind.Talk, NpcFactory.WeaverId)).Succeeded, "Talk");
        Require(g.Execute(new(GameActionKind.Save)).Succeeded, "Save");
        Require(g.Execute(new(GameActionKind.Load)).Succeeded, "Load");
        Require(g.Snapshot().Conversation is null, "Conversation survived load");
    } finally { File.Delete(path); }
});
Test("Complete gameplay flow: quest, combat, save, reload", () => {
    var path = TempSave();
    try {
        var g = NewGame(path);
        Require(g.Execute(new(GameActionKind.Talk, NpcFactory.WeaverId)).Succeeded, "Talk");
        Require(g.Execute(new(GameActionKind.Choose, 1)).Succeeded, "Accept quest");
        Require(g.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Forest");
        Require(g.Execute(new(GameActionKind.Travel, WorldFactory.SpiderClearingId)).Succeeded, "Spider");
        int turns = 0;
        while (g.Snapshot().CanAttack && turns++ < 80) g.Execute(new(GameActionKind.Attack));
        Require(g.Snapshot().Encounter?.Alive == false, "Spider undefeated");
        Require(g.Snapshot().Inventory.Any(i => i.Name == "Spider Silk" && i.Quantity > 0), "No silk");
        Require(g.Execute(new(GameActionKind.Save)).Succeeded, "Save");
        Require(g.Execute(new(GameActionKind.Load)).Succeeded, "Reload");
        Require(g.Snapshot().Quests.Single().Status == QuestStatus.Active, "Quest lost");
        Require(g.Snapshot().Encounter?.Alive == false, "Encounter reset");
        Require(g.Execute(new(GameActionKind.Travel, WorldFactory.ForestTrailId)).Succeeded, "Exit clearing");
        Require(g.Execute(new(GameActionKind.Travel, WorldFactory.TownSquareId)).Succeeded, "Return town");
    } finally { File.Delete(path); }
});

Console.WriteLine($"Results: {passed} passed, {failed} failed");
if (failed > 0) Environment.ExitCode = 1;
