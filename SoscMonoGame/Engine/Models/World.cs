using System.Collections.Generic;

namespace SoscMonoGame.Engine;

public sealed class World
{
    public Location Home { get; }
    public IReadOnlyList<Location> Locations { get; }

    internal World()
    {
        var (home, locations) = WorldFactory.BuildLocations();
        Home = home;
        Locations = locations;
    }
}
