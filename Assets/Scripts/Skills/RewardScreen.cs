using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// 스테이지 클리어 보상 3중 택1 화면. 최소 1개는 일반 보상이고, 이미 가진 스킬은 레벨업으로 나온다.
/// </summary>
public class RewardScreen : MonoBehaviour
{
    class Card
    {
        public RectTransform rect;
        public Image bg;
        public Text category, name, level, desc;
    }

    GameObject root;
    readonly List<Card> cards = new List<Card>();
    List<SkillId> options;
    Action<SkillId> onPick;
    float openedAt;

    public bool IsOpen => root != null && root.activeSelf;

    static readonly string[] categoryNames = { "기본", "동료", "특수 블록", "공격", "생존", "액티브" };

    // 등급: 일반(초록) · 희귀(파랑) · 영웅(보라) · 전설(금색)
    public static readonly string[] RarityNames = { "일반", "희귀", "영웅", "전설" };
    static readonly Color[] rarityColors =
    {
        new Color(0.22f, 0.5f, 0.28f), new Color(0.18f, 0.38f, 0.72f), new Color(0.48f, 0.24f, 0.68f), new Color(0.78f, 0.58f, 0.12f),
    };
    static readonly Color[] rarityEdges =
    {
        new Color(0.6f, 1f, 0.65f, 0.7f), new Color(0.6f, 0.8f, 1f, 0.7f), new Color(0.85f, 0.6f, 1f, 0.8f), new Color(1f, 0.92f, 0.5f, 1f),
    };
    /// <summary>카드 한 장마다 이 확률로 등급을 먼저 뽑는다 (일반 55, 희귀 30, 영웅 12, 전설 3).</summary>
    static readonly float[] rarityWeights = { 55f, 30f, 12f, 3f };

    Text title;

    void Awake()
    {
        var canvas = UiUtil.CreateCanvas("RewardCanvas", transform, 30);
        root = new GameObject("Reward", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        title = UiUtil.CreateText(root.transform, "Title", new Vector2(0.5f, 0.5f), new Vector2(0f, 400f), 64);

        for (int i = 0; i < 3; i++) cards.Add(BuildCard(i));
        root.SetActive(false);
    }

    Card BuildCard(int index)
    {
        var go = new GameObject($"Card{index}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        go.transform.SetParent(root.transform, false);
        var card = new Card { rect = go.GetComponent<RectTransform>(), bg = go.GetComponent<Image>() };
        card.rect.anchorMin = card.rect.anchorMax = new Vector2(0.5f, 0.5f);
        card.rect.sizeDelta = new Vector2(320f, 500f);
        card.rect.anchoredPosition = new Vector2((index - 1) * 350f, 0f);
        go.GetComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.6f);
        go.GetComponent<Outline>().effectDistance = new Vector2(4f, -4f);

        var button = go.GetComponent<Button>();
        button.targetGraphic = card.bg;
        button.onClick.AddListener(() => Pick(index));

        card.category = UiUtil.CreateText(go.transform, "Category", new Vector2(0.5f, 1f), new Vector2(0f, -40f), 30);
        card.name = UiUtil.CreateText(go.transform, "Name", new Vector2(0.5f, 1f), new Vector2(0f, -115f), 40);
        card.level = UiUtil.CreateText(go.transform, "Level", new Vector2(0.5f, 1f), new Vector2(0f, -195f), 32);
        card.desc = UiUtil.CreateText(go.transform, "Desc", new Vector2(0.5f, 0f), new Vector2(0f, 140f), 32, FontStyle.Normal);
        card.desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        card.desc.rectTransform.sizeDelta = new Vector2(280f, 240f);
        foreach (var t in new[] { card.category, card.name, card.level }) t.rectTransform.sizeDelta = new Vector2(300f, 80f);
        card.name.horizontalOverflow = HorizontalWrapMode.Wrap;
        return card;
    }

    public void Show(List<SkillId> choices, PlayerSkills skills, Action<SkillId> pick, string heading = "능력을 하나 고르세요")
    {
        options = choices;
        onPick = pick;
        title.text = heading;
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            bool used = i < choices.Count;
            card.rect.gameObject.SetActive(used);
            if (!used) continue;

            var def = SkillCatalog.Get(choices[i]);
            int current = skills.Level(def.id);
            int next = current + 1;
            int rarity = (int)def.rarity;
            card.bg.color = rarityColors[rarity];
            card.rect.GetComponent<Outline>().effectColor = rarityEdges[rarity];
            card.category.text = $"{RarityNames[rarity]} · {categoryNames[(int)def.category]}";
            card.category.color = rarityEdges[rarity] + new Color(0f, 0f, 0f, 1f);
            card.name.text = def.name;
            card.level.text = def.id == SkillId.Heal ? "" : current == 0 ? "NEW" : $"Lv{current} → Lv{next}";
            card.level.color = current == 0 ? new Color(1f, 0.9f, 0.4f) : new Color(0.6f, 1f, 0.7f);
            card.desc.text = def.describe(next);
        }
        root.SetActive(true);
        openedAt = Time.unscaledTime;
        Sfx.Play(SfxType.Chime, 1.2f, 0.7f);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
        onPick = null;
    }

    void Pick(int index)
    {
        if (!IsOpen || index >= options.Count || Time.unscaledTime - openedAt < 0.3f) return; // 실수 클릭 방지
        var callback = onPick;
        var id = options[index];
        Hide();
        Sfx.Play(SfxType.Pop, 1.3f, 0.8f);
        callback?.Invoke(id);
    }

    void Update()
    {
        if (!IsOpen) return;
        float t = Time.unscaledTime - openedAt;
        for (int i = 0; i < cards.Count; i++)
        {
            float k = Mathf.Clamp01((t - i * 0.08f) / 0.2f);
            float s = k < 1f ? Mathf.Lerp(0.6f, 1.08f, k) : 1f;
            cards[i].rect.localScale = Vector3.one * s;
        }
    }

    /// <summary>
    /// 보상 후보 뽑기: 카드마다 등급을 먼저 뽑고(일반 55 · 희귀 30 · 영웅 12 · 전설 3), 그 등급 안에서 고른다.
    /// 그 등급에 남은 게 없으면 한 단계 낮은 등급부터 찾는다. 최대 레벨이거나 쓸모없는 건 뺀다.
    /// </summary>
    public static List<SkillId> Roll(PlayerSkills skills, Player player, int count = 3)
    {
        var candidates = SkillCatalog.All
            .Where(d => skills.Level(d.id) < d.maxLevel)
            .Where(d => d.id != SkillId.Heal || player.Hp < player.maxHp)
            .ToList();

        var result = new List<SkillId>();
        while (result.Count < count && candidates.Count > 0)
        {
            int rarity = RollRarity();
            List<SkillDef> pool = null;
            // 뽑힌 등급 → 낮은 등급 → 높은 등급 순으로 남은 후보를 찾는다
            for (int step = 0; step < 4 && (pool == null || pool.Count == 0); step++)
            {
                int r = rarity - step >= 0 ? rarity - step : step;
                if (r > 3) continue;
                pool = candidates.Where(d => (int)d.rarity == r).ToList();
            }
            if (pool == null || pool.Count == 0) pool = candidates.ToList();
            Take(pool, candidates, result);
        }
        return result;
    }

    static int RollRarity()
    {
        float roll = Random.value * rarityWeights.Sum();
        for (int i = 0; i < rarityWeights.Length; i++)
        {
            roll -= rarityWeights[i];
            if (roll <= 0f) return i;
        }
        return 0;
    }

    static void Take(List<SkillDef> pool, List<SkillDef> candidates, List<SkillId> result)
    {
        float total = pool.Sum(d => d.weight);
        float roll = Random.value * total;
        var chosen = pool[pool.Count - 1];
        foreach (var d in pool)
        {
            roll -= d.weight;
            if (roll <= 0f) { chosen = d; break; }
        }
        result.Add(chosen.id);
        pool.Remove(chosen);
        candidates.Remove(chosen);
    }
}
