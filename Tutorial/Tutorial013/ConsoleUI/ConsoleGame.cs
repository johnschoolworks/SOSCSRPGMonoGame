using Tutorial013.Engine;

namespace Tutorial013.ConsoleUI;

public sealed class ConsoleGame
{
    private readonly GameController _controller;

    public ConsoleGame(GameController controller) =>
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));

    public void Run(TextReader input, TextWriter output)
    {
        output.WriteLine("SOSCSRPG Tutorial 013 — UI-independent RPG Engine");
        output.WriteLine("Type 'help' for commands.");
        RenderView(_controller.Snapshot(), output);
        while (true)
        {
            output.Write("\n> ");
            output.Flush();
            string? line = input.ReadLine();
            if (line is null) { output.WriteLine("Input closed. Goodbye."); return; }
            if (line.Trim().Equals("quit", StringComparison.OrdinalIgnoreCase) ||
                line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                output.WriteLine("Goodbye, adventurer.");
                return;
            }
            var command = CommandParser.Parse(line);
            if (command.Kind == GameCommandKind.Invalid)
            {
                output.WriteLine("Unknown command. Type 'help'.");
                continue;
            }
            GameResponse result = _controller.Execute(ToAction(command));
            foreach (string message in result.Messages) output.WriteLine(message);
            RenderView(result.View, output);
        }
    }

    public static GameAction ToAction(GameCommand command) =>
        new(command.Kind switch
        {
            GameCommandKind.Help => GameActionKind.Help,
            GameCommandKind.Look => GameActionKind.Look,
            GameCommandKind.Map => GameActionKind.Map,
            GameCommandKind.Travel => GameActionKind.Travel,
            GameCommandKind.Attack => GameActionKind.Attack,
            GameCommandKind.Status => GameActionKind.Status,
            GameCommandKind.Inventory => GameActionKind.Inventory,
            GameCommandKind.Quests => GameActionKind.Quests,
            GameCommandKind.Accept => GameActionKind.Accept,
            GameCommandKind.TurnIn => GameActionKind.TurnIn,
            GameCommandKind.Save => GameActionKind.Save,
            GameCommandKind.Load => GameActionKind.Load,
            _ => throw new ArgumentException("Unsupported command.", nameof(command))
        }, command.Argument);

    public static void RenderView(GameView view, TextWriter output)
    {
        output.WriteLine($"\n[{view.LocationName}] {view.LocationDescription}");
        output.WriteLine($"HP {view.HitPoints}/{view.MaximumHitPoints} | Gold {view.Gold} | XP {view.Experience}");
        output.WriteLine("Destinations: " + string.Join(", ", view.Destinations.Select(d => $"{d.Id}={d.Name}")));
        if (view.Encounter is not null)
            output.WriteLine($"Enemy: {view.Encounter.Name} ({view.Encounter.HitPoints}/{view.Encounter.MaximumHitPoints} HP)");
    }
}
