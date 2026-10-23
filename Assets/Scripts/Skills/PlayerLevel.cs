using System;
using UnityEngine;

/// <summary>
/// 퍼즐러 레벨과 경험치. 블록을 부술 때마다 경험치가 오르고, 레벨업마다 능력을 하나 고른다.
/// 다음 레벨까지 필요한 경험치 = baseXp + xpGrowth × (현재 레벨 − 1) → 12, 16, 20, 24 ...
/// 게임 오버가 되면 레벨 1로 돌아간다.
/// </summary>
public class PlayerLevel : MonoBehaviour
{
    public int xpPerBlock = 1;
    public int baseXp = 12;
    public int xpGrowth = 4;

    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }
    /// <summary>아직 고르지 않은 레벨업 보상 수.</summary>
    public int PendingPicks { get; private set; }
    public int XpToNext => baseXp + xpGrowth * (Level - 1);

    public event Action Changed;
    public event Action<int> LeveledUp;

    public void AddBlocks(int blocks) => AddXp(blocks * xpPerBlock);

    public void AddXp(int amount)
    {
        if (amount <= 0) return;
        Xp += amount;
        while (Xp >= XpToNext)
        {
            Xp -= XpToNext;
            Level++;
            PendingPicks++;
            LeveledUp?.Invoke(Level);
        }
        Changed?.Invoke();
    }

    /// <summary>레벨업과 별개로 선택 기회를 하나 더 준다 (원 턴 킬 보너스).</summary>
    public void GrantBonusPick()
    {
        PendingPicks++;
        Changed?.Invoke();
    }

    public void ConsumePick()
    {
        if (PendingPicks > 0) PendingPicks--;
        Changed?.Invoke();
    }

    public void ResetRun()
    {
        Level = 1;
        Xp = 0;
        PendingPicks = 0;
        Changed?.Invoke();
    }
}
