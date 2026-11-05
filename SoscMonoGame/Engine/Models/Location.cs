using System.Collections.Generic;

namespace SoscMonoGame.Engine;

public sealed class Location
{
    public int Id { get; }
    public string Name { get; }
    public string Description { get; }
    public bool HasMonster => MonsterIds.Count > 0;
    public bool IsRestArea { get; }
    public IReadOnlyList<int> MonsterIds { get; }
    public Location? North { get; set; }
    public Location? South { get; set; }
    public Location? East { get; set; }
    public Location? West { get; set; }

    public Location(int id, string name, string description, bool hasMonster = false,
        bool isRestArea = false, IReadOnlyList<int>? monsterIds = null)
    {
        Id = id;
        Name = name;
        Description = description;
        IsRestArea = isRestArea;
        MonsterIds = monsterIds ?? (hasMonster ? new[] { MonsterFactory.VenomousSpiderId } : System.Array.Empty<int>());
    }
}
