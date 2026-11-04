namespace Tutorial007.Models;

public sealed class Inventory
{
    private readonly Dictionary<int, int> _quantities = new();

    public int Count(int itemId) =>
        _quantities.GetValueOrDefault(itemId);

    public void Add(GameItem item, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        int updated = checked(Count(item.Id) + quantity);
        _quantities[item.Id] = updated;
    }

    public bool Remove(int itemId, int quantity = 1)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        int owned = Count(itemId);
        if (owned < quantity) return false;
        if (owned == quantity) _quantities.Remove(itemId);
        else _quantities[itemId] = owned - quantity;
        return true;
    }
}
