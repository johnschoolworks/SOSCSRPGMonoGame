namespace Tutorial007.Models;

public sealed class Player
{
    public string Name { get; }
    public Inventory Inventory { get; } = new();
    public int Gold { get; private set; }
    public int ExperiencePoints { get; private set; }

    public Player(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name;
    }

    public bool CanReceiveRewards(int gold, int experience)
    {
        if (gold < 0 || experience < 0)
            throw new ArgumentOutOfRangeException(nameof(gold));
        return (long)Gold + gold <= int.MaxValue &&
               (long)ExperiencePoints + experience <= int.MaxValue;
    }

    public void ReceiveRewards(int gold, int experience)
    {
        if (!CanReceiveRewards(gold, experience))
            throw new OverflowException("Quest rewards exceed player limits.");
        Gold += gold;
        ExperiencePoints += experience;
    }
}
