using Tutorial014.Factories;
using Tutorial014.Models;
using Tutorial014.World;

namespace Tutorial014.Combat;

public enum CombatOutcome { NoEncounter, PlayerDead, PlayerAttacked, MonsterDefeated }

public sealed record CombatResult(CombatOutcome Outcome, IReadOnlyList<string> Messages);

public sealed class CombatService
{
    private readonly Random _random;
    public CombatService(Random random) => _random = random ?? throw new ArgumentNullException(nameof(random));

    public CombatResult Attack(Player player, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(world);
        Monster? monster = world.CurrentEncounter;
        if (monster is null || !monster.IsAlive)
            return new(CombatOutcome.NoEncounter, new[] { "No living enemy to attack." });
        if (!player.IsAlive)
            return new(CombatOutcome.PlayerDead, new[] { "The player has fallen." });

        int damage = player.EquippedWeapon.RollDamage(_random);
        // Check victory rewards before the lethal hit changes monster state.
        if (damage >= monster.HitPoints)
        {
            if (!player.CanReceiveRewards(monster.RewardGold, monster.RewardExperience))
                throw new OverflowException("Combat reward capacity exceeded.");
            if (monster.LootItemId is int itemId && player.Inventory.Count(itemId) == int.MaxValue)
                throw new OverflowException("Loot inventory capacity exceeded.");
        }

        monster.TakeDamage(damage);
        var messages = new List<string> { $"You hit {monster.Name} for {damage}. Monster HP: {monster.HitPoints}" };
        if (!monster.IsAlive)
        {
            if (!monster.ClaimRewards())
                return new(CombatOutcome.NoEncounter, messages);
            if (monster.LootItemId == ItemFactory.SpiderSilkId)
            {
                player.Inventory.Add(ItemFactory.SpiderSilk);
                messages.Add("Loot: Spider Silk x1");
            }
            player.ReceiveRewards(monster.RewardGold, monster.RewardExperience);
            messages.Add($"Victory! +{monster.RewardGold} Gold, +{monster.RewardExperience} XP");
            return new(CombatOutcome.MonsterDefeated, messages);
        }

        int counterDamage = _random.Next(monster.MinimumDamage, monster.MaximumDamage + 1);
        player.TakeDamage(counterDamage);
        messages.Add($"{monster.Name} hits you for {counterDamage}. HP: {player.HitPoints}/{player.MaximumHitPoints}");
        return new(player.IsAlive ? CombatOutcome.PlayerAttacked : CombatOutcome.PlayerDead, messages);
    }
}
