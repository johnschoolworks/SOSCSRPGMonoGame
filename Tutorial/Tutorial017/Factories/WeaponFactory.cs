using Tutorial017.Models;

namespace Tutorial017.Factories;

public static class WeaponFactory
{
    public const int RustySwordId = 1;
    public const int SteelSwordId = 7;
    public static Weapon RustySword { get; } = new(RustySwordId, "Rusty Sword", 2, 5);
    public static Weapon SteelSword { get; } = new(SteelSwordId, "Steel Sword", 5, 9);
    public static Weapon Get(int id) => id switch
    {
        RustySwordId => RustySword,
        SteelSwordId => SteelSword,
        _ => throw new KeyNotFoundException($"Unknown weapon: {id}")
    };
}
