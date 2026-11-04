namespace Tutorial018.ConsoleUI;

public enum GameCommandKind
{
    Help, Look, Map, Travel, Attack, Defend, Status, Inventory, Quests,
    Talk, Choose, Shop, Buy, Sell, LeaveShop, Use, Equip, Rest, Save, Load, Quit, Invalid
}

public sealed record GameCommand(GameCommandKind Kind, int? Argument = null, int Quantity = 1);

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
            "defend" or "guard" => GameCommandKind.Defend,
            "status" or "stats" => GameCommandKind.Status,
            "inventory" or "inv" => GameCommandKind.Inventory,
            "quests" or "quest" => GameCommandKind.Quests,
            "talk" => GameCommandKind.Talk,
            "choose" => GameCommandKind.Choose,
            "shop" => GameCommandKind.Shop,
            "buy" => GameCommandKind.Buy,
            "sell" => GameCommandKind.Sell,
            "leave" => GameCommandKind.LeaveShop,
            "use" => GameCommandKind.Use,
            "equip" => GameCommandKind.Equip,
            "rest" => GameCommandKind.Rest,
            "save" => GameCommandKind.Save,
            "load" => GameCommandKind.Load,
            "quit" or "exit" => GameCommandKind.Quit,
            _ => GameCommandKind.Invalid
        };

        if (kind is GameCommandKind.Buy or GameCommandKind.Sell)
        {
            if (tokens.Length is < 2 or > 3 ||
                !int.TryParse(tokens[1], out int itemId) || itemId <= 0)
                return new(GameCommandKind.Invalid);
            int quantity = 1;
            if (tokens.Length == 3 &&
                (!int.TryParse(tokens[2], out quantity) || quantity <= 0))
                return new(GameCommandKind.Invalid);
            return new(kind, itemId, quantity);
        }

        bool needsArgument = kind is GameCommandKind.Travel or GameCommandKind.Talk
            or GameCommandKind.Choose or GameCommandKind.Shop
            or GameCommandKind.Use or GameCommandKind.Equip;
        if (needsArgument)
            return tokens.Length == 2 &&
                   int.TryParse(tokens[1], out int id) && id > 0
                ? new(kind, id) : new(GameCommandKind.Invalid);

        return tokens.Length == 1 ? new(kind) : new(GameCommandKind.Invalid);
    }
}
