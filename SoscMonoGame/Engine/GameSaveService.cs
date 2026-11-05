using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SoscMonoGame.Engine;

/// <summary>Versioned JSON snapshots. The Engine has no dependency on MonoGame/Myra.</summary>
public static class GameSaveService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SoscMonoGame", "save-v1.json");

    public static GameSaveData Capture(GameSession session)
    {
        var p = session.CurrentPlayer;
        return new GameSaveData
        {
            Player = new PlayerSaveData
            {
                Name = p.Name, Level = p.Level, ExperiencePoints = p.ExperiencePoints,
                MaximumHitPoints = p.MaximumHitPoints, CurrentHitPoints = p.CurrentHitPoints,
                Gold = p.Gold, EquippedWeaponId = p.EquippedWeapon.Id,
                Inventory = CaptureItems(p.Inventory)
            },
            LocationId = session.CurrentLocation.Id,
            Monster = session.CurrentMonster is null ? null : new MonsterSaveData
            {
                MonsterId = session.CurrentMonsterId ?? throw new InvalidOperationException("Unknown monster"),
                CurrentHitPoints = session.CurrentMonster.CurrentHitPoints
            },
            CompletedQuestIds = session.Quests.Where(q => q.IsCompleted).Select(q => q.Quest.Id).ToList(),
            MerchantStock = CaptureItems(session.TownMerchant.Stock),
            CombatLog = session.CombatLog.ToList()
        };
    }

    private static List<ItemStackSaveData> CaptureItems(Inventory inventory) =>
        inventory.Entries.Select(e => new ItemStackSaveData { ItemId = e.Item.Id, Quantity = e.Quantity }).ToList();

    public static void Save(GameSession session, string? path = null)
    {
        path ??= DefaultPath;
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporary = fullPath + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(Capture(session), Options));
            File.Move(temporary, fullPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static GameSession Load(string? path = null, Random? random = null)
    {
        var data = JsonSerializer.Deserialize<GameSaveData>(File.ReadAllText(path ?? DefaultPath), Options)
            ?? throw new InvalidDataException("Empty save file");
        Validate(data);
        var session = new GameSession(random);
        session.RestoreState(data);
        return session;
    }

    private static void Validate(GameSaveData data)
    {
        if (data.Version != 1) throw new InvalidDataException("Unsupported save version");
        var p = data.Player ?? throw new InvalidDataException("Missing player");
        if (string.IsNullOrWhiteSpace(p.Name) || p.Level < 1 || p.Level > 100000 ||
            p.ExperiencePoints < 0 || p.ExperiencePoints > 100000000 ||
            p.ExperiencePoints < (p.Level - 1) * 25 ||
            p.ExperiencePoints >= p.Level * 25 ||
            p.MaximumHitPoints != 20 + (p.Level - 1) * 5 ||
            p.CurrentHitPoints < 0 || p.CurrentHitPoints > p.MaximumHitPoints || p.Gold < 0)
            throw new InvalidDataException("Invalid player state");
        if (!WorldFactory.Create().Locations.Any(l => l.Id == data.LocationId))
            throw new InvalidDataException("Unknown location");
        if (data.CompletedQuestIds is null || data.MerchantStock is null || p.Inventory is null || data.CombatLog is null)
            throw new InvalidDataException("Missing save collections");
        if (data.CombatLog.Any(m => m is null) || data.CombatLog.Count > 10000)
            throw new InvalidDataException("Invalid combat log");
        ValidateItems(p.Inventory);
        ValidateItems(data.MerchantStock);
        if (!p.Inventory.Any(i => i.ItemId == p.EquippedWeaponId && ItemFactory.Get(i.ItemId) is Weapon))
            throw new InvalidDataException("Equipped weapon not owned");
        var quests = QuestFactory.CreateAll();
        if (data.CompletedQuestIds.Distinct().Count() != data.CompletedQuestIds.Count ||
            data.CompletedQuestIds.Any(id => quests.All(q => q.Id != id)))
            throw new InvalidDataException("Invalid quest IDs");
        foreach (var q in quests.Where(q => data.CompletedQuestIds.Contains(q.Id)))
            if (q.PrerequisiteQuestId is int prerequisite && !data.CompletedQuestIds.Contains(prerequisite))
                throw new InvalidDataException("Quest prerequisites not completed");
        if (data.Monster is not null)
        {
            var monster = MonsterFactory.Create(data.Monster.MonsterId);
            var location = WorldFactory.Create().Locations.Single(l => l.Id == data.LocationId);
            if (!location.MonsterIds.Contains(data.Monster.MonsterId) ||
                data.Monster.CurrentHitPoints <= 0 || data.Monster.CurrentHitPoints > monster.MaximumHitPoints)
                throw new InvalidDataException("Invalid monster state");
        }
    }

    private static void ValidateItems(List<ItemStackSaveData> entries)
    {
        if (entries.Count > 10000 || entries.Any(e => e is null || e.Quantity <= 0 || e.Quantity > 1000000) ||
            entries.Select(e => e.ItemId).Distinct().Count() != entries.Count)
            throw new InvalidDataException("Invalid inventory quantities");
        foreach (var entry in entries)
        {
            try { ItemFactory.Get(entry.ItemId); }
            catch (Exception e) when (e is ArgumentException or KeyNotFoundException)
            { throw new InvalidDataException("Unknown item ID", e); }
        }
    }
}
