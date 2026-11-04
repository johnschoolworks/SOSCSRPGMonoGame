using System;
using System.Collections.Generic;

namespace SoscMonoGame.Core;

public class GameSession
{
    public Player CurrentPlayer { get; }
    public Monster CurrentMonster { get; private set; } = null!;
    public List<string> CombatLog { get; } = new();

    public GameSession()
    {
        CurrentPlayer = new Player(20, 10);
        SpawnMonster();
    }

    public void SpawnMonster()
    {
        CurrentMonster = new Monster("Venomous Spider", 10, 1, 3, 10, 5);
        AddLog($"A {CurrentMonster.Name} emerges from the shadows!");
    }

    public void Attack()
    {
        if (CurrentMonster == null || CurrentMonster.IsDead) return;

        int playerDamage = Random.Shared.Next(2, 6);
        CurrentMonster.CurrentHitPoints = Math.Max(0, CurrentMonster.CurrentHitPoints - playerDamage);
        AddLog($"You strike the {CurrentMonster.Name} for {playerDamage} damage.");

        if (CurrentMonster.IsDead)
        {
            AddLog($"You defeated the {CurrentMonster.Name}! Gained {CurrentMonster.RewardGold} Gold and {CurrentMonster.RewardExp} XP.");
            CurrentPlayer.Gold += CurrentMonster.RewardGold;
            CurrentPlayer.ExperiencePoints += CurrentMonster.RewardExp;
            SpawnMonster();
            return;
        }

        int monsterDamage = Random.Shared.Next(CurrentMonster.MinDamage, CurrentMonster.MaxDamage + 1);
        CurrentPlayer.TakeDamage(monsterDamage);
        AddLog($"The {CurrentMonster.Name} attacks back, dealing {monsterDamage} damage!");
    }

    public void UsePotion()
    {
        CurrentPlayer.Heal(5);
        AddLog("You drink a Minor Healing Potion, recovering 5 HP.");
    }

    private void AddLog(string msg)
    {
        if (CombatLog.Count >= 6) CombatLog.RemoveAt(0);
        CombatLog.Add(msg);
    }
}
