using Tutorial017.Engine;

namespace Tutorial017.ConsoleUI;

public sealed class ConsoleGame
{
    private readonly GameController _controller;

    public ConsoleGame(GameController controller) =>
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));

    public void Run(TextReader input, TextWriter output)
    {
        output.WriteLine("SOSCSRPG Tutorial 015 — Shops, Trading, and Economy");
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
            GameCommandKind.Defend => GameActionKind.Defend,
            GameCommandKind.Status => GameActionKind.Status,
            GameCommandKind.Inventory => GameActionKind.Inventory,
            GameCommandKind.Quests => GameActionKind.Quests,
            GameCommandKind.Talk => GameActionKind.Talk,
            GameCommandKind.Choose => GameActionKind.Choose,
            GameCommandKind.Shop => GameActionKind.Shop,
            GameCommandKind.Buy => GameActionKind.Buy,
            GameCommandKind.Sell => GameActionKind.Sell,
            GameCommandKind.LeaveShop => GameActionKind.LeaveShop,
            GameCommandKind.Use => GameActionKind.Use,
            GameCommandKind.Equip => GameActionKind.Equip,
            GameCommandKind.Rest => GameActionKind.Rest,
            GameCommandKind.Save => GameActionKind.Save,
            GameCommandKind.Load => GameActionKind.Load,
            _ => throw new ArgumentException("Unsupported command.", nameof(command))
        }, command.Argument, command.Quantity);

    public static void RenderView(GameView view, TextWriter output)
    {
        output.WriteLine($"\n[{view.LocationName}] {view.LocationDescription}");
        output.WriteLine($"HP {view.HitPoints}/{view.MaximumHitPoints} | Gold {view.Gold} | XP {view.Experience} | Level {view.Level} | {view.WeaponName}");
        output.WriteLine("Destinations: " + string.Join(", ", view.Destinations.Select(d => $"{d.Id}={d.Name}")));
        foreach (var npc in view.Npcs) output.WriteLine($"NPC: {npc.Id}={npc.Name}");
        foreach (var shop in view.Shops)
            output.WriteLine($"Shop: {shop.Id}={shop.Name}");
        if (view.ActiveShop is not null)
        {
            output.WriteLine($"Shopping at {view.ActiveShop.Name}:");
            foreach (var offer in view.ActiveShop.Offers)
                output.WriteLine($"  #{offer.ItemId} {offer.Name} | buy {offer.BuyPrice} | sell {offer.SellPrice}");
        }
        if (view.Conversation is not null) output.WriteLine($"Talking to {view.Conversation.NpcName}; choose 1–3");
        if (view.Encounter is not null)
            output.WriteLine($"Enemy: {view.Encounter.Name} ({view.Encounter.HitPoints}/{view.Encounter.MaximumHitPoints} HP)");
    }
}
