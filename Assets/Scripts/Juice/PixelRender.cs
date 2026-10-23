using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 화면을 고정 해상도 픽셀 그림판(기본 640×360)에 그린 뒤 점 필터로 확대한다.
/// 화면 비율이 다르면 위아래 또는 좌우에 검은 여백이 생긴다. UI(글씨 등)는 이 위에 선명하게 그려진다.
/// 메인 카메라에 붙인다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class PixelRender : MonoBehaviour
{
    public Vector2Int resolution = new Vector2Int(640, 360);

    static PixelRender instance;

    Camera cam;
    Camera presentCam;
    RenderTexture rt;
    RawImage view;
    int lastScreenW, lastScreenH;
    Vector2Int lastRes;
    Rect viewRect; // 화면 픽셀 기준으로 그림판이 그려지는 영역

    void OnEnable()
    {
        instance = this;
        cam = GetComponent<Camera>();
        BuildPresenter();
        Rebuild();
    }

    void OnDisable()
    {
        if (instance == this) instance = null;
        if (cam != null) cam.targetTexture = null;
        if (view != null) Destroy(view.canvas.gameObject);
        if (presentCam != null) Destroy(presentCam.gameObject);
        if (rt != null) rt.Release();
        rt = null;
    }

    void LateUpdate()
    {
        if (Screen.width != lastScreenW || Screen.height != lastScreenH || resolution != lastRes) Rebuild();
    }

    void BuildPresenter()
    {
        // 화면에 아무 카메라도 그리지 않으면 경고가 뜨므로, 검은 배경만 그리는 카메라를 하나 둔다.
        presentCam = new GameObject("PixelPresentCamera").AddComponent<Camera>();
        presentCam.cullingMask = 0;
        presentCam.clearFlags = CameraClearFlags.SolidColor;
        presentCam.backgroundColor = Color.black;
        presentCam.depth = cam.depth - 10;

        // 그림판은 화면 픽셀 그대로 놓아야 하므로 크기 맞춤 없는 캔버스를 쓴다.
        var go = new GameObject("PixelCanvas", typeof(Canvas));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;

        var img = new GameObject("PixelView", typeof(RectTransform), typeof(RawImage));
        img.transform.SetParent(canvas.transform, false);
        var rect = img.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        view = img.GetComponent<RawImage>();
        view.raycastTarget = false;
    }

    void Rebuild()
    {
        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
        lastRes = resolution;

        cam.targetTexture = null;
        if (rt != null) rt.Release();
        rt = new RenderTexture(resolution.x, resolution.y, 24) { filterMode = FilterMode.Point, antiAliasing = 1, name = "PixelRT" };
        rt.Create();
        cam.targetTexture = rt;
        view.texture = rt;

        // 비율을 지키며 화면에 최대한 크게 (남는 곳은 검은 여백)
        float scale = Mathf.Min(Screen.width / (float)resolution.x, Screen.height / (float)resolution.y);
        float w = resolution.x * scale, h = resolution.y * scale;
        viewRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        var rect = view.rectTransform;
        rect.anchoredPosition = viewRect.position;
        rect.sizeDelta = viewRect.size;
    }

    /// <summary>화면 좌표를 카메라(픽셀 그림판) 좌표로 바꾼다.</summary>
    public static Vector2 ScreenToCamera(Camera camera, Vector2 screen)
    {
        var target = camera.targetTexture;
        if (target == null || instance == null) return screen;
        var r = instance.viewRect;
        return new Vector2((screen.x - r.x) * target.width / r.width, (screen.y - r.y) * target.height / r.height);
    }
}
