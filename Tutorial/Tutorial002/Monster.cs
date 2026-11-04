namespace Tutorial002;

public class Monster
{
    public string Name = "Venomous Spider";
    public int HitPoints = 10;
    public int MaximumHitPoints = 10;
    public int MinimumDamage = 1;
    public int MaximumDamage = 3;
    public int RewardExperiencePoints = 10;
    public int RewardGold = 5;

    public void ShowStatus()
    {
        Console.WriteLine($"Enemy: {Name}");
        Console.WriteLine($"HP:    {HitPoints}/{MaximumHitPoints}");
    }
}
