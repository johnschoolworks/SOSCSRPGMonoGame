namespace Tutorial006.Models;

public sealed class Inventory
{
    private readonly Dictionary<int, (GameItem Item, int Quantity)> _entries = new();

    public int Count(int itemId) =>
        _entries.TryGetValue(itemId, out var entry) ? entry.Quantity : 0;

    public void Add(GameItem item, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        if (_entries.TryGetValue(item.Id, out var existing))
        {
            if (!ReferenceEquals(existing.Item, item))
                throw new InvalidOperationException(
                    $"Conflicting definitions for item ID {item.Id}.");

            _entries[item.Id] = (item, checked(existing.Quantity + quantity));
        }
        else
        {
            _entries.Add(item.Id, (item, quantity));
        }
    }

    public bool Remove(int itemId, int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        if (!_entries.TryGetValue(itemId, out var existing) ||
            existing.Quantity < quantity)
            return false;

        int remaining = existing.Quantity - quantity;

        if (remaining == 0)
            _entries.Remove(itemId);
        else
            _entries[itemId] = (existing.Item, remaining);

        return true;
    }

    public IReadOnlyList<(GameItem Item, int Quantity)> GetEntries() =>
        _entries.Values
            .OrderBy(entry => entry.Item.Id)
            .ToList()
            .AsReadOnly();
}
