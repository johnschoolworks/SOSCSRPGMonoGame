using Tutorial002;

Console.WriteLine("================================");
Console.WriteLine("        SOSCSRPG Tutorial 002   ");
Console.WriteLine("================================");
Console.WriteLine();

Player player = new Player();
Monster monster = new Monster();

Console.WriteLine("=== PLAYER ===");
player.ShowStatus();

Console.WriteLine();
Console.WriteLine("=== ENCOUNTER ===");
monster.ShowStatus();

int weaponDamage = 4;
monster.HitPoints = Math.Max(0, monster.HitPoints - weaponDamage);

Console.WriteLine();
Console.WriteLine($"You attack for {weaponDamage} damage!");
monster.ShowStatus();

if (monster.HitPoints == 0)
{
    Console.WriteLine($"You defeated {monster.Name}!");
    player.Gold += monster.RewardGold;
    player.ExperiencePoints += monster.RewardExperiencePoints;
}
else
{
    Console.WriteLine($"{monster.Name} is still alive!");
}

Console.WriteLine();
Console.WriteLine("=== PLAYER AFTER COMBAT ===");
player.ShowStatus();
