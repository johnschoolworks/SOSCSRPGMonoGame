using Tutorial011.Models;

namespace Tutorial011.Factories;

public static class ItemFactory
{
    public const int SpiderSilkId = 4;
    public static GameItem SpiderSilk { get; } = new(SpiderSilkId, "Spider Silk");
}
