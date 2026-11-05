using System.Collections.Generic;

namespace SoscMonoGame.Engine;

public static class WorldFactory
{
    public static World Create() => new World();

    internal static (Location home, IReadOnlyList<Location> locations) BuildLocations()
    {
        var home = new Location(1, "Home", "A safe place to rest and recover.", isRestArea: true);
        var town = new Location(2, "Town Square", "The heart of the village. Roads lead in every direction.");
        var forest = new Location(3, "Forest", "The trees rustle. Venomous spiders lurk here.", hasMonster: true);
        var bridge = new Location(4, "Old Bridge", "A quiet crossing over a narrow stream.");
        var clearing = new Location(5, "Forest Clearing", "Sunlight breaks through the branches. More spiders hide nearby.", monsterIds: new[] { MonsterFactory.ForestWolfId });

        home.North = town;
        town.South = home;
        town.North = forest;
        forest.South = town;
        town.East = bridge;
        bridge.West = town;
        forest.East = clearing;
        clearing.West = forest;

        return (home, new[] { home, town, forest, bridge, clearing });
    }
}
