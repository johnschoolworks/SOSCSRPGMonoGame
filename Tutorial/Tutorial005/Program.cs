using Tutorial005.Models;

Console.WriteLine("=== SOSCSRPG Tutorial 005 ===");
Console.WriteLine("Collections, Generics, and Inventory");
Console.WriteLine();

var rustySword = new Weapon(1, "Rusty Sword", 12, 2, 5);
var potion = new HealingPotion(3, "Minor Healing Potion", 8, 5);

var player = new Player("Adventurer");

player.Inventory.Add(rustySword);
player.Inventory.Add(potion, 3);

Console.WriteLine("Initial inventory:");
PrintInventory(player.Inventory);

Console.WriteLine();
Console.WriteLine($"Equip sword: {player.Equip(rustySword)}");
Console.WriteLine($"Equipped: {player.EquippedWeapon?.Name ?? "None"}");

Console.WriteLine();
Console.WriteLine("Spider attacks for 7 damage.");
player.TakeDamage(7);
Console.WriteLine($"HP: {player.HitPoints}/{player.MaximumHitPoints}");

Console.WriteLine($"Drink potion: {player.DrinkPotion(potion)}");
Console.WriteLine($"HP: {player.HitPoints}/{player.MaximumHitPoints}");
Console.WriteLine($"Potions remaining: {player.Inventory.Count(potion.Id)}");

Console.WriteLine();
Console.WriteLine("Try to discard the only equipped sword:");
Console.WriteLine($"Discard succeeded: {player.DiscardItem(rustySword.Id)}");

Console.WriteLine();
Console.WriteLine("Try to drink a potion at full HP:");
player.DrinkPotion(potion); // 18 -> 20, consumes one potion.
bool usedAtFullHp = player.DrinkPotion(potion);
Console.WriteLine($"Used at full HP: {usedAtFullHp}");
Console.WriteLine($"Potions remaining: {player.Inventory.Count(potion.Id)}");

Console.WriteLine();
Console.WriteLine("Final inventory:");
PrintInventory(player.Inventory);

RunChecks();

static void PrintInventory(Inventory inventory)
{
    foreach (var (item, quantity) in inventory.GetEntries())
    {
        Console.WriteLine($"- {item.Name} ×{quantity}");
    }
}

static void RunChecks()
{
    var sword = new Weapon(101, "Test Sword", 1, 1, 2);
    var potion = new HealingPotion(102, "Test Potion", 1, 5);
    var hero = new Player("Tester");

    hero.Inventory.Add(sword);
    hero.Inventory.Add(potion, 2);

    Check(hero.Inventory.Count(potion.Id) == 2, "Starting quantity");
    Check(hero.Equip(sword), "Equip owned weapon");
    Check(!hero.DiscardItem(sword.Id), "Cannot discard equipped weapon");

    Check(!hero.DrinkPotion(potion), "Full HP does not consume potion");
    Check(hero.Inventory.Count(potion.Id) == 2, "Full HP quantity unchanged");

    hero.TakeDamage(8);
    Check(hero.DrinkPotion(potion), "Potion heals damaged player");
    Check(hero.HitPoints == 17, "Potion restores exactly five HP");
    Check(hero.Inventory.Count(potion.Id) == 1, "Potion quantity decreases");

    Check(!hero.Inventory.Remove(potion.Id, 2), "Cannot remove more than owned");
    Check(hero.Inventory.Count(potion.Id) == 1, "Failed removal is atomic");

    Console.WriteLine("\nAll inventory checks passed.");
}

static void Check(bool condition, string description)
{
    if (!condition)
        throw new InvalidOperationException($"FAIL: {description}");

    Console.WriteLine($"PASS: {description}");
}
