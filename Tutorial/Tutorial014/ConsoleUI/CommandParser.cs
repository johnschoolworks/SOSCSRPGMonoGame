namespace Tutorial014.ConsoleUI;

public enum GameCommandKind
{
    Help, Look, Map, Travel, Attack, Status, Inventory, Quests,
    Talk, Choose, Save, Load, Quit, Invalid
}

public sealed record GameCommand(GameCommandKind Kind, int? Argument = null);

public static class CommandParser
{
    public static GameCommand Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new(GameCommandKind.Invalid);

        string[] tokens = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string verb = tokens[0].ToLowerInvariant();

        GameCommandKind kind = verb switch
        {
            "help" or "h" or "?" => GameCommandKind.Help,
            "look" or "l" => GameCommandKind.Look,
            "map" => GameCommandKind.Map,
            "go" or "travel" => GameCommandKind.Travel,
            "attack" or "a" => GameCommandKind.Attack,
            "status" or "stats" => GameCommandKind.Status,
            "inventory" or "inv" => GameCommandKind.Inventory,
            "quests" or "quest" => GameCommandKind.Quests,
            "talk" => GameCommandKind.Talk,
            "choose" => GameCommandKind.Choose,
            "save" => GameCommandKind.Save,
            "load" => GameCommandKind.Load,
            "quit" or "exit" => GameCommandKind.Quit,
            _ => GameCommandKind.Invalid
        };

        bool needsArgument = kind is GameCommandKind.Travel or GameCommandKind.Talk or GameCommandKind.Choose;
        if (needsArgument)
            return tokens.Length == 2 &&
                   int.TryParse(tokens[1], out int id) && id > 0
                ? new(kind, id) : new(GameCommandKind.Invalid);

        return tokens.Length == 1 ? new(kind) : new(GameCommandKind.Invalid);
    }
}
