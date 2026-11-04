using Tutorial011.Factories;
using Tutorial011.Models;
using Tutorial011.World;

namespace Tutorial011.Combat;

public enum CombatOutcome { NoEncounter, PlayerDead, PlayerAttacked, MonsterDefeated }

public sealed class CombatService
{
    private readonly Random _random;
    public CombatService(Random random) => _random = random ?? throw new ArgumentNullException(nameof(random));

    public CombatOutcome Attack(Player player, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(world);
        Monster? monster = world.CurrentEncounter;
        if (monster is null || !monster.IsAlive) return CombatOutcome.NoEncounter;
        if (!player.IsAlive) return CombatOutcome.PlayerDead;

        int damage = player.EquippedWeapon.RollDamage(_random);
        monster.TakeDamage(damage);
        Console.WriteLine($"You hit {monster.Name} for {damage}. Monster HP: {monster.HitPoints}");
        if (!monster.IsAlive)
        {
            // Preflight all reward additions before claiming the encounter.
            if (!player.CanReceiveRewards(monster.RewardGold, monster.RewardExperience))
                throw new OverflowException("Combat reward capacity exceeded.");
            if (monster.LootItemId is int itemId && player.Inventory.Count(itemId) == int.MaxValue)
                throw new OverflowException("Loot inventory capacity exceeded.");
            if (!monster.ClaimRewards()) return CombatOutcome.NoEncounter;
            if (monster.LootItemId is int lootId)
            {
                player.Inventory.Add(ItemFactory.SpiderSilk);
                Console.WriteLine("Loot: Spider Silk x1");
            }
            player.ReceiveRewards(monster.RewardGold, monster.RewardExperience);
            Console.WriteLine($"Victory! +{monster.RewardGold} Gold, +{monster.RewardExperience} XP");
            return CombatOutcome.MonsterDefeated;
        }

        int counterDamage = _random.Next(monster.MinimumDamage, monster.MaximumDamage + 1);
        player.TakeDamage(counterDamage);
        Console.WriteLine($"{monster.Name} hits you for {counterDamage}. HP: {player.HitPoints}/{player.MaximumHitPoints}");
        return player.IsAlive ? CombatOutcome.PlayerAttacked : CombatOutcome.PlayerDead;
    }
}
