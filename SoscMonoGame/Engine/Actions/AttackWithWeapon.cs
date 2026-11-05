using System;

namespace SoscMonoGame.Engine;

/// <summary>Pure combat operation: calculate and apply a weapon hit.</summary>
public static class AttackWithWeapon
{
    public static int Execute(Weapon weapon, Monster target, Random random)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(random);
        if (target.IsDead) return 0;
        int damage = random.Next(weapon.MinimumDamage, weapon.MaximumDamage + 1);
        target.CurrentHitPoints = Math.Max(0, target.CurrentHitPoints - damage);
        return damage;
    }
}
