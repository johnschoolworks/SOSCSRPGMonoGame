namespace Tutorial002;

public class Player
{
    public string Name = "Adventurer";
    public int HitPoints = 20;
    public int MaximumHitPoints = 20;
    public int Gold = 10;
    public int ExperiencePoints = 0;
    public int Level = 1;

    public void ShowStatus()
    {
        Console.WriteLine($"Name:  {Name}");
        Console.WriteLine($"Level: {Level}");
        Console.WriteLine($"HP:    {HitPoints}/{MaximumHitPoints}");
        Console.WriteLine($"Gold:  {Gold}");
        Console.WriteLine($"XP:    {ExperiencePoints}");
    }
}
