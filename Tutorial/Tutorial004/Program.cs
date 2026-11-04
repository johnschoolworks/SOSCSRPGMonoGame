using Tutorial004.Interfaces;
using Tutorial004.Models;

Console.WriteLine("=== SOSCSRPG Tutorial 004 ===");
Console.WriteLine("Inheritance, Interfaces, and RPG Items");
Console.WriteLine();

var player = new Player("Adventurer");

var rustySword = new Weapon(
    id: 1,
    name: "Rusty Sword",
    price: 12,
    minimumDamage: 2,
    maximumDamage: 5);

var ironSword = new Weapon(
    id: 2,
    name: "Iron Sword",
    price: 45,
    minimumDamage: 5,
    maximumDamage: 9);

var potion = new HealingPotion(
    id: 3,
    name: "Minor Healing Potion",
    price: 8,
    amountToHeal: 5);

// Polymorphism: one collection holds different derived item types.
List<GameItem> catalog = new()
{
    rustySword,
    ironSword,
    potion
};

Console.WriteLine("Item catalog:");
foreach (GameItem item in catalog)
{
    Console.WriteLine($"- {item.Describe()}");
}

Console.WriteLine();
Console.WriteLine($"Player HP: {player.HitPoints}/{player.MaximumHitPoints}");

player.TakeDamage(7);
Console.WriteLine($"After taking 7 damage: {player.HitPoints} HP");

// An interface reference can point to a HealingPotion.
IUsableItem usableItem = potion;

bool used = usableItem.Use(player);
Console.WriteLine($"Potion used successfully: {used}");
Console.WriteLine($"After healing: {player.HitPoints} HP");

Console.WriteLine();
Console.WriteLine("Trying to heal at full HP:");
player.Heal(100);
bool usedAtFullHealth = potion.Use(player);
Console.WriteLine($"Potion used successfully: {usedAtFullHealth}");
Console.WriteLine($"Current HP: {player.HitPoints}/{player.MaximumHitPoints}");

Console.WriteLine();
Console.WriteLine($"Rusty Sword damage roll: {rustySword.RollDamage()}");

Console.WriteLine();
Console.WriteLine("Item type inspection:");
foreach (GameItem item in catalog)
{
    if (item is Weapon weapon)
    {
        Console.WriteLine($"{weapon.Name} is a weapon.");
    }
    else if (item is IUsableItem)
    {
        Console.WriteLine($"{item.Name} is usable.");
    }
}
