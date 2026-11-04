using System;

Console.WriteLine("================================");
Console.WriteLine("        SOSCSRPG Tutorial       ");
Console.WriteLine("================================");
Console.WriteLine();

string playerName = "Adventurer";
int hitPoints = 20;
int maximumHitPoints = 20;
int gold = 10;
int experiencePoints = 0;
int level = 1;

Console.WriteLine($"Name:  {playerName}");
Console.WriteLine($"Level: {level}");
Console.WriteLine($"HP:    {hitPoints}/{maximumHitPoints}");
Console.WriteLine($"Gold:  {gold}");
Console.WriteLine($"XP:    {experiencePoints}");

Console.WriteLine();
Console.WriteLine("Welcome to the world of SOSCSRPG!");

Console.WriteLine();
Console.WriteLine("A venomous spider appears!");

string monsterName = "Venomous Spider";
int monsterHitPoints = 10;
int weaponDamage = 4;

Console.WriteLine($"Enemy: {monsterName}");
Console.WriteLine($"Enemy HP: {monsterHitPoints}");

Console.WriteLine();
Console.WriteLine("You attack with your rusty sword!");

monsterHitPoints -= weaponDamage;

Console.WriteLine($"Damage dealt: {weaponDamage}");
Console.WriteLine($"Enemy HP: {monsterHitPoints}");

if (monsterHitPoints <= 0)
{
    Console.WriteLine($"You defeated {monsterName}!");
    gold += 5;
    experiencePoints += 10;
}
else
{
    Console.WriteLine($"{monsterName} is still alive!");
}

Console.WriteLine($"Current Gold: {gold}");
Console.WriteLine($"Current XP: {experiencePoints}");
