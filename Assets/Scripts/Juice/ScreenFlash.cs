using UnityEngine;
using UnityEngine.UI;

/// <summary>화면 전체가 번쩍하는 플래시 (전사 강타, 적 처치 등 큰 순간용).</summary>
public class ScreenFlash : MonoBehaviour
{
    static ScreenFlash instance;

    Image image;
    float alpha, fadePerSecond;

    public static void Flash(Color color, float strength, float duration)
    {
        if (instance == null) instance = Create();
        color.a = 1f;
        instance.image.color = color;
        instance.alpha = Mathf.Max(instance.alpha, strength);
        instance.fadePerSecond = strength / Mathf.Max(0.01f, duration);
    }

    static ScreenFlash Create()
    {
        var go = new GameObject("ScreenFlash");
        var flash = go.AddComponent<ScreenFlash>();
        var canvas = UiUtil.CreateCanvas("FlashCanvas", go.transform, 20);
        var imageGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        imageGo.transform.SetParent(canvas.transform, false);
        var rect = imageGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        flash.image = imageGo.GetComponent<Image>();
        flash.image.raycastTarget = false;
        flash.image.enabled = false;
        return flash;
    }

    void Update()
    {
        if (alpha <= 0f) return;
        alpha = Mathf.Max(0f, alpha - fadePerSecond * Time.unscaledDeltaTime);
        var c = image.color;
        c.a = alpha;
        image.color = c;
        image.enabled = alpha > 0f;
    }
}
