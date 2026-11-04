using System.Text.Json;
using Tutorial019.Factories;
using Tutorial019.Models;
using Tutorial019.World;

namespace Tutorial019.Persistence;

public sealed class SaveGameService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false
    };

    public SaveData Capture(Player player, IEnumerable<Quest> quests, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(world);
        return new SaveData
        {
            PlayerName = player.Name,
            EquippedWeaponId = player.EquippedWeapon.Id,
            CurrentLocationId = world.CurrentLocationId,
            HitPoints = player.HitPoints,
            EncounterMonsterId = world.CurrentEncounter?.Id,
            EncounterHitPoints = world.CurrentEncounter?.HitPoints,
            EncounterRewardsClaimed = world.CurrentEncounter?.RewardsClaimed ?? false,
            Gold = player.Gold,
            ExperiencePoints = player.ExperiencePoints,
            Inventory = player.Inventory.GetEntries()
                .Select(e => new InventoryEntryData { ItemId = e.ItemId, Quantity = e.Quantity })
                .ToList(),
            Quests = quests.Select(q => new QuestEntryData
            {
                QuestId = q.Id,
                Status = q.Status.ToString()
            }).ToList()
        };
    }

    public string Serialize(SaveData save) => JsonSerializer.Serialize(save, Options);

    public SaveData Deserialize(string json) =>
        JsonSerializer.Deserialize<SaveData>(json, Options)
        ?? throw new InvalidDataException("Save file contains JSON null.");

    public void Save(string path, Player player, IEnumerable<Quest> quests, GameWorld world)
    {
        string json = Serialize(Capture(player, quests, world));
        // Temporary file plus rename avoids leaving a truncated target file.
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, fullPath, overwrite: true);
    }

    public (Player Player, List<Quest> Quests, GameWorld World) Load(string path) =>
        Restore(Deserialize(File.ReadAllText(path)));

    public (Player Player, List<Quest> Quests, GameWorld World) Restore(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Version != 4)
            throw new InvalidDataException($"Unsupported save version: {data.Version}");
        if (string.IsNullOrWhiteSpace(data.PlayerName))
            throw new InvalidDataException("Missing player name.");
        if (data.Gold < 0 || data.ExperiencePoints < 0 || data.HitPoints < 0 || data.HitPoints > 20 + (Math.Min(20, data.ExperiencePoints / 25 + 1) - 1) * 5)
            throw new InvalidDataException("Negative player rewards.");
        if (data.Inventory is null || data.Quests is null)
            throw new InvalidDataException("Missing inventory or quest list.");

        // A save stores location identity, not mutable encounter objects.
        try { _ = WorldFactory.Get(data.CurrentLocationId); }
        catch (KeyNotFoundException ex)
        {
            throw new InvalidDataException("Unknown saved location.", ex);
        }

        if (data.EncounterMonsterId is int savedMonsterId)
        {
            if (WorldFactory.Get(data.CurrentLocationId).MonsterId != savedMonsterId ||
                data.EncounterHitPoints is null)
                throw new InvalidDataException("Encounter/location mismatch.");
            try
            {
                var probe = MonsterFactory.Create(savedMonsterId);
                probe.Restore(data.EncounterHitPoints.Value, data.EncounterRewardsClaimed);
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidDataException)
            {
                throw new InvalidDataException("Invalid saved encounter.", ex);
            }
        }
        else if (data.EncounterHitPoints is not null || data.EncounterRewardsClaimed)
            throw new InvalidDataException("Orphaned encounter fields.");

        // Validate everything before publishing the restored game state.
        var inventoryIds = new HashSet<int>();
        foreach (var entry in data.Inventory)
        {
            if (entry is null || entry.Quantity <= 0 || !inventoryIds.Add(entry.ItemId))
                throw new InvalidDataException("Invalid or duplicate inventory entry.");
            _ = ResolveItem(entry.ItemId);
        }
        var questIds = new HashSet<int>();
        var parsedQuests = new List<(Quest Quest, QuestStatus Status)>();
        foreach (var entry in data.Quests)
        {
            if (entry is null || !questIds.Add(entry.QuestId) ||
                !Enum.TryParse<QuestStatus>(entry.Status, ignoreCase: false, out var status) ||
                !Enum.IsDefined(status))
                throw new InvalidDataException("Invalid or duplicate quest entry.");
            Quest quest = entry.QuestId switch
            {
                1 => QuestFactory.CreateSpiderSilkQuest(),
                _ => throw new InvalidDataException($"Unknown quest ID: {entry.QuestId}")
            };
            parsedQuests.Add((quest, status));
        }

        Weapon weapon;
        try { weapon = WeaponFactory.Get(data.EquippedWeaponId); }
        catch (KeyNotFoundException ex) { throw new InvalidDataException("Invalid saved weapon.", ex); }
        if (weapon.Id != WeaponFactory.RustySwordId &&
            !data.Inventory.Any(i => i.ItemId == weapon.Id && i.Quantity > 0))
            throw new InvalidDataException("Equipped weapon is not owned.");

        var player = new Player(data.PlayerName);
        player.RestoreRewards(data.Gold, data.ExperiencePoints);
        player.RestoreHitPoints(data.HitPoints);
        player.Equip(weapon);
        foreach (var entry in data.Inventory)
            player.Inventory.Add(ResolveItem(entry.ItemId), entry.Quantity);
        foreach (var (quest, status) in parsedQuests)
            quest.RestoreStatus(status);
        var world = new GameWorld(data.CurrentLocationId);
        if (data.EncounterMonsterId is int encounterId)
        {
            var encounter = MonsterFactory.Create(encounterId);
            encounter.Restore(data.EncounterHitPoints!.Value, data.EncounterRewardsClaimed);
            world.RestoreEncounter(encounter);
        }
        return (player, parsedQuests.Select(x => x.Quest).ToList(), world);
    }

    private static GameItem ResolveItem(int id)
    {
        try { return ItemFactory.Get(id); }
        catch (KeyNotFoundException ex)
        {
            throw new InvalidDataException($"Unknown item ID: {id}", ex);
        }
    }
}
