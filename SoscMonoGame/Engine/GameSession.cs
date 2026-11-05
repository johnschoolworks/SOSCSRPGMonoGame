using System;
using System.Collections.Generic;
using System.Linq;

namespace SoscMonoGame.Engine;

public enum Direction { North, South, East, West }

public class GameSession
{
    private readonly Random _random;
    public event EventHandler? StateChanged;
    internal int? CurrentMonsterId => CurrentMonster?.Name switch
    {
        "Venomous Spider" => MonsterFactory.VenomousSpiderId,
        "Forest Wolf" => MonsterFactory.ForestWolfId,
        _ => null
    };
    private void NotifyChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    public Player CurrentPlayer { get; }
    public World World { get; }
    public Trader TownMerchant { get; } = TraderFactory.CreateTownMerchant();
    public bool IsAtTrader => CurrentLocation.Id == 2;
    public Location CurrentLocation { get; private set; }
    public Monster? CurrentMonster { get; private set; }
    public List<string> CombatLog { get; } = new();
    public IReadOnlyList<QuestProgress> Quests { get; } =
        QuestFactory.CreateAll().Select(q => new QuestProgress(q)).ToArray();
    public QuestProgress VillageQuest => Quests[0];
    public QuestProgress? ActiveQuest => Quests.FirstOrDefault(q =>
        !q.IsCompleted && (q.Quest.PrerequisiteQuestId is null ||
        Quests.Any(previous => previous.Quest.Id == q.Quest.PrerequisiteQuestId && previous.IsCompleted)));

    public GameSession(Random? random = null)
    {
        _random = random ?? Random.Shared;
        CurrentPlayer = new Player(20, 10);
        CurrentPlayer.Inventory.Add(ItemCatalog.RustySword);
        CurrentPlayer.Inventory.Add(ItemCatalog.MinorPotion, 3);
        World = WorldFactory.Create();
        CurrentLocation = World.Home;
        AddLog($"You are at {CurrentLocation.Name}. {CurrentLocation.Description}");
        AddLog($"Quest: {VillageQuest.Quest.Description}");
    }

    public bool Move(Direction direction)
    {
        var destination = direction switch
        {
            Direction.North => CurrentLocation.North,
            Direction.South => CurrentLocation.South,
            Direction.East => CurrentLocation.East,
            Direction.West => CurrentLocation.West,
            _ => null
        };

        if (destination is null)
        {
            AddLog($"There is no path to the {direction.ToString().ToLowerInvariant()}.");
            return false;
        }
        if (CurrentPlayer.CurrentHitPoints <= 0)
        {
            AddLog("You are unconscious. You cannot travel.");
            return false;
        }
        if (CurrentMonster is { IsDead: false })
        {
            AddLog("Defeat the monster before leaving this area!");
            return false;
        }

        CurrentLocation = destination;
        CurrentMonster = null;
        AddLog($"You arrive at {destination.Name}. {destination.Description}");
        if (destination.IsRestArea)
        {
            CurrentPlayer.Heal(CurrentPlayer.MaximumHitPoints);
            AddLog("You rest at home and recover all HP.");
        }
        if (destination.HasMonster) SpawnMonster();
        if (destination.Id == 2 && ActiveQuest is { } active)
            AddLog($"Quest board: {active.Quest.Description}");
        return true;
    }

    public void SpawnMonster()
    {
        if (!CurrentLocation.HasMonster) return;
        int id = CurrentLocation.MonsterIds[_random.Next(CurrentLocation.MonsterIds.Count)];
        CurrentMonster = MonsterFactory.Create(id);
        AddLog($"A {CurrentMonster.Name} emerges from the shadows!");
    }

    public void Attack()
    {
        if (CurrentPlayer.CurrentHitPoints <= 0)
        {
            AddLog("You are unconscious and cannot attack.");
            return;
        }
        if (CurrentMonster is null || CurrentMonster.IsDead)
        {
            AddLog("There is no monster to attack here.");
            return;
        }

        var weapon = CurrentPlayer.EquippedWeapon;
        int playerDamage = AttackWithWeapon.Execute(weapon, CurrentMonster, _random);
        AddLog($"You strike the {CurrentMonster.Name} with {weapon.Name} for {playerDamage} damage.");
        if (CurrentMonster.IsDead)
        {
            AddLog($"You defeated the {CurrentMonster.Name}! Gained {CurrentMonster.RewardGold} Gold and {CurrentMonster.RewardExp} XP.");
            CurrentPlayer.Gold += CurrentMonster.RewardGold;
            int levels = CurrentPlayer.GainExperience(CurrentMonster.RewardExp);
            if (levels > 0) AddLog($"Level up! Level {CurrentPlayer.Level}, max HP {CurrentPlayer.MaximumHitPoints}.");
            foreach (var (item, quantity) in LootFactory.Roll(CurrentMonster, _random))
            {
                CurrentPlayer.Inventory.Add(item, quantity);
                AddLog($"Loot obtained: {quantity} {item.Name}.");
            }
            if (ActiveQuest is { } active)
                AddLog($"Quest progress: {Math.Min(active.Quest.RequiredQuantity, CurrentPlayer.Inventory.Count(active.Quest.RequiredItemId))}/{active.Quest.RequiredQuantity} {ItemFactory.Get(active.Quest.RequiredItemId).Name}.");
            CurrentMonster = null;
            return;
        }

        int monsterDamage = _random.Next(CurrentMonster.MinDamage, CurrentMonster.MaxDamage + 1);
        CurrentPlayer.TakeDamage(monsterDamage);
        AddLog($"The {CurrentMonster.Name} attacks back, dealing {monsterDamage} damage!");
        if (CurrentPlayer.CurrentHitPoints <= 0)
        {
            AddLog("You have fallen in battle. Your adventure ends here.");
        }
    }

    public void TurnInQuest()
    {
        if (CurrentLocation.Id != 2)
        {
            AddLog("Return to Town Square to turn in the quest.");
            return;
        }
        var progress = ActiveQuest;
        if (progress is null)
        {
            AddLog("All available quests are completed.");
            return;
        }
        var quest = progress.Quest;
        if (!progress.Complete(CurrentPlayer.Inventory))
        {
            int amount = CurrentPlayer.Inventory.Count(quest.RequiredItemId);
            AddLog($"You need {quest.RequiredQuantity} {ItemFactory.Get(quest.RequiredItemId).Name} ({amount}/{quest.RequiredQuantity}).");
            return;
        }
        CurrentPlayer.Gold += quest.RewardGold;
        int levels = CurrentPlayer.GainExperience(quest.RewardExperience);
        CurrentPlayer.Inventory.Add(quest.RewardItem);
        AddLog($"Quest complete: {quest.Name}! +{quest.RewardGold} Gold, +{quest.RewardExperience} XP, {quest.RewardItem.Name}.");
        if (levels > 0) AddLog($"Level up! Level {CurrentPlayer.Level}, max HP {CurrentPlayer.MaximumHitPoints}.");
        if (ActiveQuest is { } next) AddLog($"New quest unlocked: {next.Quest.Name}.");
    }

    public bool BuyItem(int itemId)
    {
        if (!IsAtTrader) { AddLog("Visit Town Square to trade."); return false; }
        if (CurrentPlayer.CurrentHitPoints <= 0) { AddLog("You cannot trade while unconscious."); return false; }
        var item = TownMerchant.Stock.Entries.FirstOrDefault(e => e.Item.Id == itemId)?.Item;
        if (item is null) { AddLog("The merchant is out of stock."); return false; }
        int price = TownMerchant.BuyPrice(item);
        if (CurrentPlayer.Gold < price) { AddLog($"Not enough gold: {item.Name} costs {price} G."); return false; }
        if (!TownMerchant.Stock.Remove(item.Id)) { AddLog("Out of stock."); return false; }
        CurrentPlayer.Gold -= price;
        CurrentPlayer.Inventory.Add(item);
        AddLog($"Bought {item.Name} for {price} G.");
        return true;
    }

    public bool SellItem(int itemId)
    {
        if (!IsAtTrader) { AddLog("Visit Town Square to trade."); return false; }
        if (CurrentPlayer.CurrentHitPoints <= 0) { AddLog("You cannot trade while unconscious."); return false; }
        var item = CurrentPlayer.Inventory.Entries.FirstOrDefault(e => e.Item.Id == itemId)?.Item;
        if (item is null) { AddLog("You do not own that item."); return false; }
        if (item is Weapon && CurrentPlayer.EquippedWeapon.Id == item.Id && CurrentPlayer.Inventory.Count(item.Id) == 1)
        {
            AddLog("You cannot sell your last equipped weapon. Equip another first.");
            return false;
        }
        int price = TownMerchant.SellPrice(item);
        if (!CurrentPlayer.Inventory.Remove(item.Id)) return false;
        TownMerchant.Stock.Add(item);
        CurrentPlayer.Gold += price;
        AddLog($"Sold {item.Name} for {price} G.");
        return true;
    }

    public void UsePotion()
    {
        if (CurrentPlayer.CurrentHitPoints <= 0)
        {
            AddLog("You cannot drink a potion while unconscious.");
            return;
        }
        if (CurrentPlayer.CurrentHitPoints == CurrentPlayer.MaximumHitPoints)
        {
            AddLog("Your HP is already full. Potion not consumed.");
            return;
        }
        if (!CurrentPlayer.Inventory.Remove(ItemCatalog.MinorPotion.Id))
        {
            AddLog("You have no healing potions left.");
            return;
        }
        int restored = Heal.Execute(CurrentPlayer, ItemCatalog.MinorPotion);
        AddLog($"You drink a potion and recover {restored} HP.");
    }

    public void EquipIronSword()
    {
        if (CurrentPlayer.Equip(ItemCatalog.IronSword)) AddLog("Equipped Iron Sword.");
        else AddLog("You do not own an Iron Sword.");
    }

    public void EquipRustySword()
    {
        if (CurrentPlayer.Equip(ItemCatalog.RustySword)) AddLog("Equipped Rusty Sword.");
        else AddLog("You do not own a Rusty Sword.");
    }

    // Loading bypasses movement/combat rules but validates the entire snapshot first.
    internal void RestoreState(GameSaveData data)
    {
        var p = CurrentPlayer;
        p.Restore(data.Player.Level, data.Player.ExperiencePoints, data.Player.MaximumHitPoints,
            data.Player.CurrentHitPoints, data.Player.Gold, data.Player.Name);
        foreach (var entry in p.Inventory.Entries.ToArray()) p.Inventory.Remove(entry.Item.Id, entry.Quantity);
        foreach (var entry in data.Player.Inventory) p.Inventory.Add(ItemFactory.Get(entry.ItemId), entry.Quantity);
        p.Equip(ItemFactory.Get<Weapon>(data.Player.EquippedWeaponId));
        CurrentLocation = World.Locations.Single(l => l.Id == data.LocationId);
        CurrentMonster = data.Monster is null ? null : MonsterFactory.Create(data.Monster.MonsterId);
        if (CurrentMonster is not null) CurrentMonster.CurrentHitPoints = data.Monster!.CurrentHitPoints;
        foreach (var quest in Quests) quest.RestoreCompleted(data.CompletedQuestIds.Contains(quest.Quest.Id));
        foreach (var entry in TownMerchant.Stock.Entries.ToArray()) TownMerchant.Stock.Remove(entry.Item.Id, entry.Quantity);
        foreach (var entry in data.MerchantStock) TownMerchant.Stock.Add(ItemFactory.Get(entry.ItemId), entry.Quantity);
        CombatLog.Clear();
        foreach (var message in data.CombatLog.TakeLast(8)) CombatLog.Add(message);
        AddLog("Saved adventure loaded.");
    }

    public void AddSystemMessage(string message) => AddLog(message);

    private void AddLog(string msg)
    {
        if (CombatLog.Count >= 8) CombatLog.RemoveAt(0);
        CombatLog.Add(msg);
        NotifyChanged();
    }
}
