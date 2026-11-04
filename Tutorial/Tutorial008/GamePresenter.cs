using Tutorial008.Models;

namespace Tutorial008;

// A UI adapter: the engine does not know about Console or Myra.
public sealed class GamePresenter : IDisposable
{
    private readonly Player _player;
    private readonly Quest _quest;
    private bool _disposed;

    public GamePresenter(Player player, Quest quest)
    {
        _player = player;
        _quest = quest;

        _player.Inventory.InventoryChanged += OnInventoryChanged;
        _player.StatsChanged += OnStatsChanged;
        _quest.StatusChanged += OnQuestStatusChanged;
    }

    private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
    {
        if (e.ItemId == _quest.RequiredItemId)
            Console.WriteLine($"[UI] Quest progress: {_quest.Progress(_player)}/{_quest.RequiredQuantity}");
    }

    private void OnStatsChanged(object? sender, PlayerStatsChangedEventArgs e) =>
        Console.WriteLine($"[UI] Player stats: {e.Gold} Gold, {e.ExperiencePoints} XP");

    private void OnQuestStatusChanged(object? sender, QuestStatusChangedEventArgs e) =>
        Console.WriteLine($"[UI] Quest status: {e.Previous} -> {e.Current}");

    public void Dispose()
    {
        if (_disposed) return;
        _player.Inventory.InventoryChanged -= OnInventoryChanged;
        _player.StatsChanged -= OnStatsChanged;
        _quest.StatusChanged -= OnQuestStatusChanged;
        _disposed = true;
    }
}
