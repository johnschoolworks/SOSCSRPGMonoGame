using System.Collections.ObjectModel;

namespace Tutorial005.Models;

public sealed class Inventory
{
    private readonly Dictionary<int, (GameItem Item, int Quantity)> _items = new();

    public int Count(int itemId) =>
        _items.TryGetValue(itemId, out var entry) ? entry.Quantity : 0;

    public bool Contains(int itemId, int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        return Count(itemId) >= quantity;
    }

    public void Add(GameItem item, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        if (_items.TryGetValue(item.Id, out var entry))
        {
            if (!ReferenceEquals(entry.Item, item))
                throw new InvalidOperationException(
                    $"Item ID {item.Id} is already registered with a different definition.");

            _items[item.Id] = (item, checked(entry.Quantity + quantity));
        }
        else
        {
            _items.Add(item.Id, (item, quantity));
        }
    }

    public bool Remove(int itemId, int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        if (!_items.TryGetValue(itemId, out var entry) ||
            entry.Quantity < quantity)
        {
            return false;
        }

        int remaining = entry.Quantity - quantity;

        if (remaining == 0)
            _items.Remove(itemId);
        else
            _items[itemId] = (entry.Item, remaining);

        return true;
    }

    public IReadOnlyList<(GameItem Item, int Quantity)> GetEntries()
    {
        // Snapshot: callers cannot mutate our internal dictionary.
        return _items.Values
            .Select(entry => (entry.Item, entry.Quantity))
            .OrderBy(entry => entry.Item.Id)
            .ToList()
            .AsReadOnly();
    }
}
