using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 왼쪽 액티브 스킬 바: 마나 게이지 + 얻은 액티브 스킬 버튼(세로) + 원소 변환용 원소 고르기.
/// 액티브 스킬을 하나도 얻지 않았으면 숨긴다.
/// </summary>
public class SkillBar : MonoBehaviour
{
    static readonly SkillId[] order = { SkillId.ActiveConvert, SkillId.ActiveHammer, SkillId.ActiveShuffle, SkillId.ActiveDelay };

    PlayerSkills skills;
    BoardManager board;
    GameObject root, palette;
    RectTransform manaFill;
    Text manaText, hint;
    readonly Dictionary<SkillId, (Button button, Text label)> buttons = new Dictionary<SkillId, (Button, Text)>();
    Vector2Int convertCell;

    const float ManaWidth = 560f;

    void Awake()
    {
        skills = FindFirstObjectByType<PlayerSkills>();
        board = FindFirstObjectByType<BoardManager>();
        Build();
    }

    void OnEnable()
    {
        if (board != null) board.ConvertTargetChosen += OpenPalette;
    }

    void OnDisable()
    {
        if (board != null) board.ConvertTargetChosen -= OpenPalette;
    }

    void Build()
    {
        var canvas = UiUtil.CreateCanvas("SkillBarCanvas", transform, 6);
        root = new GameObject("SkillBar", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        var rootRect = root.GetComponent<RectTransform>();
        // 오른쪽 절반 아래 (플레이어 HP 바 밑)
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.75f, 0f);
        rootRect.anchoredPosition = Vector2.zero;

        // 마나 게이지
        var bar = MakeImage(root.transform, "ManaBar", new Vector2(0f, 270f), new Vector2(ManaWidth + 10f, 28f), new Color(0.1f, 0.08f, 0.15f, 0.9f));
        var fill = MakeImage(bar.transform, "Fill", Vector2.zero, new Vector2(ManaWidth, 22f), new Color(0.35f, 0.6f, 1f));
        manaFill = fill.rectTransform;
        manaFill.anchorMin = manaFill.anchorMax = new Vector2(0f, 0.5f);
        manaFill.pivot = new Vector2(0f, 0.5f);
        manaFill.anchoredPosition = new Vector2(5f, 0f);
        manaText = UiUtil.CreateText(bar.transform, "ManaText", new Vector2(0.5f, 0.5f), Vector2.zero, 26);

        hint = UiUtil.CreateText(root.transform, "Hint", new Vector2(0.5f, 0.5f), new Vector2(0f, 500f), 34);
        hint.color = new Color(1f, 0.9f, 0.4f);
        hint.rectTransform.sizeDelta = new Vector2(600f, 60f);

        for (int i = 0; i < order.Length; i++)
        {
            var id = order[i];
            var img = MakeImage(root.transform, $"Skill_{id}", new Vector2((i - 1.5f) * 215f, 170f), new Vector2(200f, 100f), new Color(0.62f, 0.48f, 0.18f));
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => OnSkillClicked(id));
            var label = UiUtil.CreateText(img.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, 30);
            label.rectTransform.sizeDelta = new Vector2(220f, 110f);
            buttons[id] = (button, label);
        }

        // 원소 변환: 바꿀 원소 고르기
        palette = new GameObject("Palette", typeof(RectTransform));
        palette.transform.SetParent(root.transform, false);
        var colors = board != null ? board.blockColors : new Color[0];
        for (int t = 0; t < colors.Length; t++)
        {
            int type = t;
            var img = MakeImage(palette.transform, $"Element{t}", new Vector2((t - 2.5f) * 105f, 420f), new Vector2(95f, 85f), colors[t]);
            img.gameObject.AddComponent<Button>().onClick.AddListener(() => PickElement(type));
            UiUtil.CreateText(img.transform, "Name", new Vector2(0.5f, 0.5f), Vector2.zero, 30).text = SkillCatalog.ElementNames[t];
        }
        var cancel = MakeImage(palette.transform, "Cancel", new Vector2(2.5f * 105f, 420f), new Vector2(95f, 85f), new Color(0.3f, 0.3f, 0.35f));
        cancel.gameObject.AddComponent<Button>().onClick.AddListener(ClosePalette);
        UiUtil.CreateText(cancel.transform, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, 28).text = "취소";
        palette.SetActive(false);
    }

    static Image MakeImage(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    void OnSkillClicked(SkillId id)
    {
        if (board.Targeting == id)
        {
            board.CancelTargeting();
            return;
        }
        ClosePalette();
        if (board.TryUseActive(id)) Sfx.Play(SfxType.Swap, 1.2f, 0.7f);
    }

    void OpenPalette(Vector2Int cell)
    {
        convertCell = cell;
        palette.SetActive(true);
    }

    void PickElement(int type)
    {
        palette.SetActive(false);
        board.ConvertBlock(convertCell, type);
    }

    void ClosePalette()
    {
        if (!palette.activeSelf) return;
        palette.SetActive(false);
        board.CancelTargeting();
    }

    void Update()
    {
        bool show = skills != null && skills.HasAnyActive;
        root.SetActive(show);
        if (!show) return;

        manaFill.sizeDelta = new Vector2(ManaWidth * skills.Mana / Mathf.Max(1, skills.maxMana), manaFill.sizeDelta.y);
        manaText.text = $"마나 {skills.Mana} / {skills.maxMana}";

        foreach (var id in order)
        {
            var (button, label) = buttons[id];
            bool owned = skills.Has(id);
            button.gameObject.SetActive(owned);
            if (!owned) continue;
            int cost = skills.Cost(id);
            int cooldown = skills.Cooldown(id);
            bool targeting = board.Targeting == id;
            button.interactable = targeting || (board.CanUseSkills && skills.Mana >= cost && cooldown <= 0);
            string sub = cooldown > 0 ? $"<color=#ff9a80>쿨타임 {cooldown}턴</color>" : $"마나 {cost}";
            label.text = targeting ? "취소" : $"{SkillCatalog.Get(id).name}\n<size=24>{sub}</size>";
        }

        hint.text = board.Targeting == SkillId.ActiveHammer ? "부술 블록을 누르세요"
                  : board.Targeting == SkillId.ActiveConvert ? "바꿀 블록을 누르세요"
                  : palette.activeSelf ? "바꿀 원소를 고르세요" : "";
    }
}
