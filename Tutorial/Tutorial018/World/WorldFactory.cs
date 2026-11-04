namespace Tutorial018.World;

public static class WorldFactory
{
    public const int TownSquareId = 1;
    public const int ForestTrailId = 2;
    public const int SpiderClearingId = 3;
    public const int WolfDenId = 4;

    private static readonly Dictionary<int, Location> Locations = new()
    {
        [TownSquareId] = new(TownSquareId, "Town Square",
            "A peaceful place to begin your journey.", new[] { ForestTrailId }),
        [ForestTrailId] = new(ForestTrailId, "Forest Trail",
            "The road splits among ancient trees.",
            new[] { TownSquareId, SpiderClearingId, WolfDenId }),
        [SpiderClearingId] = new(SpiderClearingId, "Spider Clearing",
            "Silvery webs cover the branches.", new[] { ForestTrailId }, MonsterFactory.SpiderId),
        [WolfDenId] = new(WolfDenId, "Wolf Den",
            "Tracks disappear into a shadowy hollow.", new[] { ForestTrailId }, MonsterFactory.WolfId)
    };

    public static Location Get(int id) =>
        Locations.TryGetValue(id, out var location)
            ? location : throw new KeyNotFoundException($"Unknown location: {id}");

    public static IReadOnlyList<Location> All() =>
        Locations.Values.OrderBy(l => l.Id).ToList().AsReadOnly();
}
