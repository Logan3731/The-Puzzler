using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카메라 화면을 저해상도 렌더 텍스처에 그린 뒤 점 필터로 확대해 화면 전체를 도트처럼 보이게 한다.
/// 메인 카메라에 붙인다. UI(콤보 글씨 등)는 이 위에 선명하게 그려진다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class PixelRender : MonoBehaviour
{
    [Tooltip("세로 픽셀 수. 작을수록 도트가 굵어진다.")]
    [Range(120, 1080)] public int verticalResolution = 360;

    Camera cam;
    Camera presentCam;
    RenderTexture rt;
    RawImage view;
    int lastScreenW, lastScreenH, lastRes;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        BuildPresenter();
        Rebuild();
    }

    void OnDisable()
    {
        if (cam != null) cam.targetTexture = null;
        if (view != null) Destroy(view.canvas.gameObject);
        if (presentCam != null) Destroy(presentCam.gameObject);
        if (rt != null) rt.Release();
        rt = null;
    }

    void LateUpdate()
    {
        if (Screen.width != lastScreenW || Screen.height != lastScreenH || verticalResolution != lastRes) Rebuild();
    }

    void BuildPresenter()
    {
        // 화면에 아무 카메라도 그리지 않으면 경고가 뜨므로, 검은 배경만 그리는 카메라를 하나 둔다.
        presentCam = new GameObject("PixelPresentCamera").AddComponent<Camera>();
        presentCam.cullingMask = 0;
        presentCam.clearFlags = CameraClearFlags.SolidColor;
        presentCam.backgroundColor = Color.black;
        presentCam.depth = cam.depth - 10;

        var canvas = UiUtil.CreateCanvas("PixelCanvas", null, -100);
        var go = new GameObject("PixelView", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(canvas.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        view = go.GetComponent<RawImage>();
        view.raycastTarget = false;
    }

    void Rebuild()
    {
        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
        lastRes = verticalResolution;

        int h = Mathf.Min(verticalResolution, Screen.height);
        int w = Mathf.Max(1, Mathf.RoundToInt(h * (float)Screen.width / Screen.height));

        cam.targetTexture = null;
        if (rt != null) rt.Release();
        rt = new RenderTexture(w, h, 24) { filterMode = FilterMode.Point, antiAliasing = 1, name = "PixelRT" };
        rt.Create();
        cam.targetTexture = rt;
        view.texture = rt;
    }

    /// <summary>화면 좌표를 이 카메라(저해상도 텍스처) 좌표로 바꾼다.</summary>
    public static Vector2 ScreenToCamera(Camera camera, Vector2 screen)
    {
        var target = camera.targetTexture;
        if (target == null) return screen;
        return new Vector2(screen.x * target.width / Screen.width, screen.y * target.height / Screen.height);
    }
}
