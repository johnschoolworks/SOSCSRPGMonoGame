namespace Tutorial015.World;

public sealed record Location(int Id, string Name, string Description,
    IReadOnlyList<int> NeighborIds, int? MonsterId = null);
