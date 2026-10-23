using UnityEngine;

/// <summary>블록 데미지 = 부순 블록 수 × 블록당 데미지 × 콤보 배율. (동료 데미지는 ComboSystem의 고정값)</summary>
public static class DamageCalculator
{
    public const float DamagePerBlock = 10f;
    public const float ComboBonusPerStep = 0.2f;

    /// <summary>1콤보 ×1.0, 콤보당 +0.2</summary>
    public static float ComboMultiplier(int combo) => 1f + ComboBonusPerStep * Mathf.Max(0, combo - 1);

    public static int Calculate(int blocks, int combo) =>
        Mathf.RoundToInt(blocks * DamagePerBlock * ComboMultiplier(combo));
}
