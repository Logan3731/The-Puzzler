using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "N COMBO!" 팝업과 그 아래 이번 턴 누적 데미지 카운터.
/// 콤보가 클수록 글씨가 커지고 색이 뜨거워지며, 데미지는 촤라락 올라간다. 캔버스는 스스로 만든다.
/// </summary>
public class ComboText : MonoBehaviour
{
    public float holdTime = 0.9f;
    public float fadeTime = 0.35f;

    Text comboLabel, damageLabel;
    float age, comboAge, damageAge;
    float shownDamage;
    int targetDamage;
    bool visible;

    void Awake()
    {
        var canvas = UiUtil.CreateCanvas("ComboCanvas", transform, 10);
        comboLabel = UiUtil.CreateText(canvas.transform, "Combo", new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), 100, FontStyle.BoldAndItalic);
        comboLabel.GetComponent<Outline>().effectDistance = new Vector2(5f, -5f);
        damageLabel = UiUtil.CreateText(canvas.transform, "Damage", new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), 80, FontStyle.Bold);
        comboLabel.enabled = damageLabel.enabled = false;
    }

    /// <summary>새 턴의 연쇄가 시작될 때.</summary>
    public void ResetChain()
    {
        shownDamage = 0f;
        targetDamage = 0;
        comboLabel.enabled = false;
        damageLabel.enabled = false;
    }

    public void Show(int combo)
    {
        comboLabel.text = $"{combo} COMBO!";
        comboLabel.fontSize = 100 + Mathf.Min(combo, 10) * 12;
        comboLabel.color = ComboColor(combo);
        comboLabel.enabled = true;
        comboAge = 0f;
        Wake();
    }

    public void SetDamage(int total)
    {
        targetDamage = total;
        damageLabel.enabled = true;
        damageAge = 0f;
        Wake();
    }

    void Wake()
    {
        age = 0f;
        visible = true;
    }

    static Color ComboColor(int combo) =>
        combo >= 7 ? new Color(1f, 0.3f, 0.9f)
        : combo >= 5 ? new Color(1f, 0.45f, 0.15f)
        : combo >= 3 ? new Color(1f, 0.85f, 0.2f)
        : Color.white;

    void Update()
    {
        if (!visible) return;
        float dt = Time.unscaledDeltaTime;
        age += dt;
        comboAge += dt;
        damageAge += dt;

        comboLabel.rectTransform.localScale = Vector3.one * (comboAge < 0.12f ? Mathf.Lerp(1.9f, 1f, comboAge / 0.12f) : 1f);
        comboLabel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, comboAge < 0.2f ? Random.Range(-5f, 5f) : 0f);

        float gap = targetDamage - shownDamage;
        shownDamage = Mathf.MoveTowards(shownDamage, targetDamage, Mathf.Max(60f, gap * 10f) * dt);
        damageLabel.text = Mathf.RoundToInt(shownDamage).ToString("N0");
        damageLabel.rectTransform.localScale = Vector3.one * (damageAge < 0.1f ? Mathf.Lerp(1.35f, 1f, damageAge / 0.1f) : 1f);

        float alpha = age < holdTime ? 1f : 1f - (age - holdTime) / fadeTime;
        SetAlpha(comboLabel, alpha);
        SetAlpha(damageLabel, alpha);

        if (alpha <= 0f)
        {
            visible = false;
            comboLabel.enabled = damageLabel.enabled = false;
        }
    }

    static void SetAlpha(Text label, float alpha)
    {
        var c = label.color;
        c.a = Mathf.Clamp01(alpha);
        label.color = c;
    }
}
