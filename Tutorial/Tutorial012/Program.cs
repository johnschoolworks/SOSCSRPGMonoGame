using Tutorial012.ConsoleUI;
using Tutorial012.Factories;
using Tutorial012.Models;
using Tutorial012.World;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    SelfTest();
    return;
}

string path = Path.Combine(Environment.CurrentDirectory, "savegame.json");
new GameSession(path).Run(Console.In, Console.Out);

static void SelfTest()
{
    static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }

    Check(CommandParser.Parse("go 2") == new GameCommand(GameCommandKind.Travel, 2),
        "Travel parser");
    Check(CommandParser.Parse("go xyz").Kind == GameCommandKind.Invalid,
        "Invalid destination rejected");
    Check(CommandParser.Parse("attack now").Kind == GameCommandKind.Invalid,
        "Extra arguments rejected");
    Check(CommandParser.Parse("  HELP ").Kind == GameCommandKind.Help,
        "Case insensitive parser");

    string temp = Path.Combine(Path.GetTempPath(), "soscsrpg-t012-" + Guid.NewGuid().ToString("N") + ".json");
    try
    {
        var session = new GameSession(temp, new Random(42));
        using var log = new StringWriter();
        session.Execute(new(GameCommandKind.Accept), log);
        session.Execute(new(GameCommandKind.Travel, WorldFactory.ForestTrailId), log);
        session.Execute(new(GameCommandKind.Travel, WorldFactory.SpiderClearingId), log);
        Check(session.World.CurrentEncounter?.IsAlive == true, "Encounter started");
        session.Execute(new(GameCommandKind.Travel, WorldFactory.ForestTrailId), log);
        Check(session.World.CurrentLocationId == WorldFactory.SpiderClearingId,
            "Travel locked during combat");

        session.Execute(new(GameCommandKind.Attack), log);
        int hp = session.World.CurrentEncounter!.HitPoints;
        session.Execute(new(GameCommandKind.Save), log);
        session.Execute(new(GameCommandKind.Attack), log);
        session.Execute(new(GameCommandKind.Load), log);
        Check(session.World.CurrentEncounter!.HitPoints == hp, "Mid-combat HP restored");

        int turns = 0;
        while (session.World.CurrentEncounter!.IsAlive && session.Player.IsAlive && turns++ < 30)
            session.Execute(new(GameCommandKind.Attack), log);
        Check(!session.World.CurrentEncounter!.IsAlive, "Combat victory");
        Check(session.Player.Gold == 5 && session.Player.ExperiencePoints == 10,
            "Rewards granted once");
        Check(session.Player.Inventory.Count(ItemFactory.SpiderSilkId) == 1,
            "Spider loot granted");

        session.Execute(new(GameCommandKind.Attack), log);
        Check(session.Player.Gold == 5, "No duplicate rewards");
        session.Execute(new(GameCommandKind.Save), log);
        session.Execute(new(GameCommandKind.Load), log);
        Check(session.World.CurrentEncounter?.RewardsClaimed == true,
            "Claimed state persists");
        session.Execute(new(GameCommandKind.Travel, WorldFactory.ForestTrailId), log);
        Check(session.World.CurrentLocationId == WorldFactory.ForestTrailId,
            "Travel unlocked after victory");

        var scripted = new GameSession(temp, new Random(1));
        using var reader = new StringReader("help\nstatus\nquit\n");
        using var transcript = new StringWriter();
        scripted.Run(reader, transcript);
        Check(!scripted.IsRunning && transcript.ToString().Contains("Goodbye"),
            "Scripted command loop exits");
        var eofSession = new GameSession(temp);
        eofSession.Run(new StringReader(""), TextWriter.Null);
        Check(!eofSession.IsRunning, "EOF exits without spinning");
    }
    finally
    {
        if (File.Exists(temp)) File.Delete(temp);
    }

    Console.WriteLine("ALL INTERACTIVE GAME CHECKS PASSED");
}
