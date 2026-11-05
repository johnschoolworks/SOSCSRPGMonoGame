using System.Collections.Generic;
using System.Linq;

namespace SoscMonoGame.Engine;

public sealed class Inventory
{
    private readonly Dictionary<int, InventoryEntry> _entries = new();
    public IReadOnlyList<InventoryEntry> Entries => _entries.Values.OrderBy(e => e.Item.Id).ToArray();

    public int Count(int itemId) => _entries.TryGetValue(itemId, out var entry) ? entry.Quantity : 0;

    public void Add(Item item, int quantity = 1)
    {
        if (quantity <= 0) throw new System.ArgumentOutOfRangeException(nameof(quantity));
        if (_entries.TryGetValue(item.Id, out var entry)) entry.Quantity += quantity;
        else _entries[item.Id] = new InventoryEntry(item, quantity);
    }

    public bool Remove(int itemId, int quantity = 1)
    {
        if (quantity <= 0) return false;
        if (!_entries.TryGetValue(itemId, out var entry) || entry.Quantity < quantity) return false;
        entry.Quantity -= quantity;
        if (entry.Quantity == 0) _entries.Remove(itemId);
        return true;
    }

    public T? Find<T>(int itemId) where T : Item =>
        _entries.TryGetValue(itemId, out var entry) ? entry.Item as T : null;
}

public sealed class InventoryEntry
{
    public Item Item { get; }
    public int Quantity { get; internal set; }
    public InventoryEntry(Item item, int quantity) { Item = item; Quantity = quantity; }
}
