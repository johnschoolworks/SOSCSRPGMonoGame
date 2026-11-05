using System;

namespace SoscMonoGame.Engine;

public static class Heal
{
    public static int Execute(Player player, HealingPotion potion)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(potion);
        int before = player.CurrentHitPoints;
        player.Heal(potion.HealingAmount);
        return player.CurrentHitPoints - before;
    }
}
