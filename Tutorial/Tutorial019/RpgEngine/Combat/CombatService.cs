using Tutorial019.Factories;
using Tutorial019.Models;
using Tutorial019.World;

namespace Tutorial019.Combat;

public enum CombatOutcome
{
    NoEncounter, PlayerDead, PlayerAttacked, MonsterDefeated, Defended
}

public sealed record CombatResult(CombatOutcome Outcome, IReadOnlyList<string> Messages);

public sealed class CombatService
{
    private readonly Random _random;
    public CombatService(Random random) =>
        _random = random ?? throw new ArgumentNullException(nameof(random));

    // Defensive turn: a monster strikes once, but damage is reduced by half,
    // rounded down. The guard expires immediately; there is no persistent buff.
    public CombatResult Defend(Player player, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(world);
        Monster? monster = world.CurrentEncounter;
        if (monster is null || !monster.IsAlive)
            return new(CombatOutcome.NoEncounter, new[] { "Nothing to defend against." });
        if (!player.IsAlive)
            return new(CombatOutcome.PlayerDead, new[] { "The player has fallen." });

        int raw = _random.Next(monster.MinimumDamage, monster.MaximumDamage + 1);
        int reduced = raw / 2;
        player.TakeDamage(reduced);
        var messages = new List<string>
        {
            $"You guard against {monster.Name}. Incoming damage {raw} -> {reduced}.",
            $"HP: {player.HitPoints}/{player.MaximumHitPoints}"
        };
        return new(player.IsAlive ? CombatOutcome.Defended : CombatOutcome.PlayerDead, messages);
    }

    public CombatResult Attack(Player player, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(world);
        Monster? monster = world.CurrentEncounter;
        if (monster is null || !monster.IsAlive)
            return new(CombatOutcome.NoEncounter, new[] { "No living enemy to attack." });
        if (!player.IsAlive)
            return new(CombatOutcome.PlayerDead, new[] { "The player has fallen." });

        // An enemy has a 10% chance to evade an attack.
        bool dodged = _random.NextDouble() < 0.10;
        bool critical = !dodged && _random.NextDouble() < 0.15;
        int baseDamage = dodged ? 0 : player.EquippedWeapon.RollDamage(_random);
        int damage = critical ? checked(baseDamage * 2) : baseDamage;
        var messages = new List<string>();

        // Decide optional loot BEFORE changing monster state.
        IReadOnlyList<LootEntry> drops = !dodged && damage >= monster.HitPoints
            ? LootTable.Roll(monster.Id, _random)
            : Array.Empty<LootEntry>();

        if (!dodged && damage >= monster.HitPoints)
        {
            if (!player.CanReceiveRewards(monster.RewardGold, monster.RewardExperience))
                throw new OverflowException("Combat reward capacity exceeded.");

            var quantities = new Dictionary<int, long>();
            if (monster.LootItemId is int guaranteedId)
                quantities[guaranteedId] = 1;
            foreach (LootEntry drop in drops)
                quantities[drop.ItemId] = quantities.GetValueOrDefault(drop.ItemId) + drop.Quantity;
            foreach (var (id, amount) in quantities)
                if (player.Inventory.Count(id) + amount > int.MaxValue)
                    throw new OverflowException("Loot inventory capacity exceeded.");
        }

        if (dodged)
        {
            messages.Add($"{monster.Name} dodges your attack!");
        }
        else
        {
            monster.TakeDamage(damage);
            messages.Add(critical
                ? $"CRITICAL HIT! You strike {monster.Name} for {damage}. HP: {monster.HitPoints}"
                : $"You hit {monster.Name} for {damage}. HP: {monster.HitPoints}");
        }

        if (!monster.IsAlive)
        {
            if (!monster.ClaimRewards())
                return new(CombatOutcome.NoEncounter, messages);
            if (monster.LootItemId is int itemId)
            {
                player.Inventory.Add(ItemFactory.Get(itemId));
                messages.Add($"Guaranteed loot: {ItemFactory.Get(itemId).Name} x1");
            }
            foreach (LootEntry drop in drops)
            {
                player.Inventory.Add(ItemFactory.Get(drop.ItemId), drop.Quantity);
                messages.Add($"Bonus loot: {ItemFactory.Get(drop.ItemId).Name} x{drop.Quantity}");
            }
            player.ReceiveRewards(monster.RewardGold, monster.RewardExperience);
            messages.Add($"Victory! +{monster.RewardGold} Gold, +{monster.RewardExperience} XP");
            return new(CombatOutcome.MonsterDefeated, messages);
        }

        int counterDamage = _random.Next(monster.MinimumDamage, monster.MaximumDamage + 1);
        player.TakeDamage(counterDamage);
        messages.Add($"{monster.Name} counterattacks for {counterDamage}. HP: {player.HitPoints}/{player.MaximumHitPoints}");
        return new(player.IsAlive ? CombatOutcome.PlayerAttacked : CombatOutcome.PlayerDead, messages);
    }
}
