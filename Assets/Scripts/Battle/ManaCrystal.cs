using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 수정구슬: 적을 쓰러뜨리고 남은 초과 데미지가 마력 포인트로 쌓인다 (쓰임새는 미정).
/// 포인트가 들어오면 숫자가 "띠리리리링" 소리와 함께 촤라락 올라가고 구슬이 빛난다.
/// 게임 오버가 돼도 지워지지 않는다 (메타 재화 후보). 아직 파일로 저장하지는 않는다.
/// </summary>
public class ManaCrystal : MonoBehaviour
{
    public int Points { get; private set; }

    Image orb, glow;
    Text label, gainText;
    RectTransform orbRect;
    float shown, gainAge = 99f, pulse, tickTimer;
    int pendingGain;

    void Awake()
    {
        var enemy = FindFirstObjectByType<Enemy>();
        if (enemy != null) enemy.Overkill += Add;
        Build();
    }

    void OnDestroy()
    {
        var enemy = FindFirstObjectByType<Enemy>();
        if (enemy != null) enemy.Overkill -= Add;
    }

    void Build()
    {
        var canvas = UiUtil.CreateCanvas("CrystalCanvas", transform, 7);
        var circle = MakeCircleSprite();

        var root = new GameObject("Crystal", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.anchoredPosition = new Vector2(-110f, -80f);

        glow = MakeImage(root.transform, "Glow", Vector2.zero, new Vector2(150f, 150f), new Color(0.7f, 0.5f, 1f, 0f), circle);
        orb = MakeImage(root.transform, "Orb", Vector2.zero, new Vector2(96f, 96f), new Color(0.55f, 0.4f, 0.95f), circle);
        orbRect = orb.rectTransform;
        MakeImage(orb.transform, "Shine", new Vector2(-18f, 20f), new Vector2(30f, 30f), new Color(1f, 1f, 1f, 0.7f), circle);

        label = UiUtil.CreateText(root.transform, "Points", new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), 30);
        label.rectTransform.sizeDelta = new Vector2(260f, 50f);
        gainText = UiUtil.CreateText(root.transform, "Gain", new Vector2(0.5f, 0.5f), new Vector2(0f, -118f), 34);
        gainText.color = new Color(0.85f, 0.7f, 1f);
        gainText.enabled = false;
        Refresh();
    }

    static Image MakeImage(Transform parent, string name, Vector2 pos, Vector2 size, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>원형 스프라이트를 코드로 만든다 (VARCO 아이콘이 생기면 교체).</summary>
    static Sprite MakeCircleSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        float r = size * 0.5f - 1f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
            tex.SetPixel(x, y, d <= r ? Color.white : Color.clear);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Points += amount;
        pendingGain += amount;
        gainText.text = $"+{pendingGain:N0}";
        gainText.enabled = true;
        gainAge = 0f;
    }

    void Refresh() => label.text = $"마력 {Mathf.RoundToInt(shown):N0}";

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // 숫자가 촤라락 올라가며 띠리리리링
        if (shown < Points)
        {
            float gap = Points - shown;
            shown = Mathf.Min(Points, shown + Mathf.Max(20f, gap * 4f) * dt);
            pulse = 1f;
            tickTimer -= dt;
            if (tickTimer <= 0f)
            {
                tickTimer = 0.035f;
                float progress = 1f - Mathf.Clamp01(gap / Mathf.Max(1f, pendingGain));
                Sfx.Play(SfxType.Swap, 1.6f + progress * 1.2f, 0.35f);
            }
            if (shown >= Points)
            {
                Sfx.Play(SfxType.Chime, 1.5f, 0.6f);
                pendingGain = 0;
            }
            Refresh();
        }

        pulse = Mathf.Max(0f, pulse - dt * 2.5f);
        orbRect.localScale = Vector3.one * (1f + 0.15f * pulse + 0.03f * Mathf.Sin(Time.unscaledTime * 3f));
        var g = glow.color;
        g.a = 0.15f + 0.55f * pulse;
        glow.color = g;

        gainAge += dt;
        if (gainText.enabled)
        {
            var c = gainText.color;
            c.a = Mathf.Clamp01(1.6f - gainAge);
            gainText.color = c;
            if (gainAge > 1.6f) gainText.enabled = false;
        }
    }
}
