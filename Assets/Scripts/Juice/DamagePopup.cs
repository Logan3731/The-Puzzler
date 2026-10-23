using UnityEngine;

/// <summary>적 위로 튀어 오르는 데미지 숫자 (월드 공간 TextMesh).</summary>
public class DamagePopup : MonoBehaviour
{
    TextMesh text;
    Vector3 velocity;
    Vector3 baseScale;
    Color color;
    float age;
    const float Life = 0.9f;

    public static TextMesh CreateText(GameObject go, float characterSize, Color color)
    {
        var font = UiUtil.Font;
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        tm.fontSize = 96;
        tm.characterSize = characterSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;
        return tm;
    }

    /// <summary>블록 데미지: 콤보가 높을수록 크고 뜨거운 색.</summary>
    public static void Spawn(Vector3 position, int damage, int combo)
    {
        var color = combo >= 5 ? new Color(1f, 0.45f, 0.15f) : combo >= 3 ? new Color(1f, 0.85f, 0.2f) : Color.white;
        Spawn(position, damage.ToString(), color, 0.07f + 0.008f * Mathf.Min(combo, 10));
    }

    /// <summary>임의 문구 (동료 공격 등).</summary>
    public static void Spawn(Vector3 position, string message, Color color, float size)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = position + new Vector3(Random.Range(-0.6f, 0.6f), 0f, -2f);
        var popup = go.AddComponent<DamagePopup>();
        popup.text = CreateText(go, size, color);
        popup.text.text = message;
        popup.color = color;
        popup.baseScale = go.transform.localScale;
        popup.velocity = new Vector3(Random.Range(-1f, 1f), 5f, 0f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;
        velocity.y -= 12f * dt;
        transform.position += velocity * dt;

        float punch = age < 0.1f ? Mathf.Lerp(1.8f, 1f, age / 0.1f) : 1f;
        transform.localScale = baseScale * punch;

        var c = color;
        c.a = age < Life * 0.6f ? 1f : 1f - (age - Life * 0.6f) / (Life * 0.4f);
        text.color = c;

        if (age >= Life) Destroy(gameObject);
    }
}
