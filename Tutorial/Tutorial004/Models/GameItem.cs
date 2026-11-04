namespace Tutorial004.Models;

public abstract class GameItem
{
    public int Id { get; }
    public string Name { get; }
    public int Price { get; }

    protected GameItem(int id, string name, int price)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Item name cannot be empty.", nameof(name));

        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price));

        Id = id;
        Name = name;
        Price = price;
    }

    public virtual string Describe()
    {
        return $"{Name} (ID: {Id}, Price: {Price} Gold)";
    }
}
