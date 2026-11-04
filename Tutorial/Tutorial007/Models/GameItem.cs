namespace Tutorial007.Models;

public sealed class GameItem
{
    public int Id { get; }
    public string Name { get; }

    public GameItem(int id, string name)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Id = id;
        Name = name;
    }
}
