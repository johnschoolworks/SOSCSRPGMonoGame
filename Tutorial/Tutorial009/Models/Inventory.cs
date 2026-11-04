namespace Tutorial009.Models;

public sealed class Inventory
{
    private readonly Dictionary<int, int> _items = new();

    public event EventHandler<InventoryChangedEventArgs>? InventoryChanged;

    public int Count(int itemId) => _items.GetValueOrDefault(itemId);

    public IReadOnlyList<(int ItemId, int Quantity)> GetEntries() =>
        _items.Select(e => (ItemId: e.Key, Quantity: e.Value))
            .OrderBy(e => e.ItemId).ToList().AsReadOnly();

    public void Add(GameItem item, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Id <= 0) throw new ArgumentOutOfRangeException(nameof(item));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));

        int updated = checked(Count(item.Id) + quantity);
        _items[item.Id] = updated;
        OnInventoryChanged(item.Id, updated);
    }

    public bool Remove(int itemId, int quantity = 1)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        int owned = Count(itemId);
        if (owned < quantity) return false;

        int remaining = owned - quantity;
        if (remaining == 0) _items.Remove(itemId);
        else _items[itemId] = remaining;

        OnInventoryChanged(itemId, remaining);
        return true;
    }

    private void OnInventoryChanged(int itemId, int newQuantity) =>
        InventoryChanged?.Invoke(this, new InventoryChangedEventArgs(itemId, newQuantity));
}

public sealed class InventoryChangedEventArgs : EventArgs
{
    public int ItemId { get; }
    public int NewQuantity { get; }

    public InventoryChangedEventArgs(int itemId, int newQuantity)
    {
        ItemId = itemId;
        NewQuantity = newQuantity;
    }
}
