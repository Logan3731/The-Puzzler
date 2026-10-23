using UnityEngine;

/// <summary>
/// 블록 데미지 = (10 + 공격력 보너스) × 콤보 배율(연쇄의 리듬) × 원소 상성 × 원소 친화 × 첫 일격.
/// 동료 데미지는 상성과 무관한 고정값이다 (ComboSystem).
/// 원소 순서: 0 불, 1 물, 2 바람, 3 번개, 4 어둠. 상성: 번개 > 물 > 불 > 어둠 > 바람 > 번개.
/// </summary>
public static class DamageCalculator
{
    public const float DamagePerBlock = 10f;
    public const float ResistMultiplier = 0.5f;

    // beats[a] = a가 이기는 원소
    static readonly int[] beats = { 4, 0, 3, 1, 2 };

    public static bool Beats(int attacker, int defender) => attacker >= 0 && defender >= 0 && beats[attacker] == defender;

    /// <summary>이 속성의 적에게 ×2가 들어가는 원소.</summary>
    public static int WeakTo(int enemyElement)
    {
        for (int a = 0; a < beats.Length; a++)
            if (beats[a] == enemyElement) return a;
        return -1;
    }

    /// <summary>이 속성의 적이 ×0.5로 버티는 원소.</summary>
    public static int Resists(int enemyElement) => enemyElement >= 0 ? beats[enemyElement] : -1;

    public static float ElementMultiplier(int attacker, int enemyElement, float weakMultiplier)
    {
        if (Beats(attacker, enemyElement)) return weakMultiplier;
        if (Beats(enemyElement, attacker)) return ResistMultiplier;
        return 1f;
    }

    public static float BlockDamage(int element, int combo, int enemyElement, PlayerSkills skills, bool firstStep)
    {
        float d = DamagePerBlock + skills.BonusPerBlock;
        d *= 1f + skills.ComboBonus * Mathf.Max(0, combo - 1);
        d *= ElementMultiplier(element, enemyElement, skills.WeakMultiplier);
        d *= skills.Affinity(element);
        if (firstStep) d *= skills.FirstStrikeMultiplier;
        return d;
    }
}
