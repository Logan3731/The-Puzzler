using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화면 오른쪽 절반의 길 풍경 (임시 도형). 주인공 눈높이에서 본 것처럼 원근을 흉내 낸 흙길과
/// 하늘, 먼 언덕, 길가 나무·바위가 있고, Run()을 부르면 길과 소품이 화면 쪽으로 흘러와 달리는 느낌을 준다.
/// 카메라 하나(직교)로 보드와 같이 그려야 빛구슬이 보드에서 몬스터까지 이어서 날아간다.
/// </summary>
public class RoadScene : MonoBehaviour
{
    public float horizonY = 2.2f;
    public float bottomY = -5f;
    public float roadTopHalf = 0.25f;
    public float roadBottomHalf = 3.2f;
    public float runSpeed = 1.6f;

    public Color sky = new Color(0.5f, 0.72f, 0.95f);
    public Color grass = new Color(0.38f, 0.62f, 0.32f);
    public Color road = new Color(0.62f, 0.47f, 0.32f);

    float cx, left, right, phase, bobTime;
    bool running;
    Transform root;
    readonly List<Transform> dashes = new List<Transform>();
    readonly List<(Transform t, int side, bool tree)> props = new List<(Transform, int, bool)>();

    const int DashCount = 7;
    const int PropsPerSide = 5;

    /// <summary>오른쪽 패널 위치를 받아 풍경을 만든다 (BoardManager가 부른다).</summary>
    public void Build(float centerX, float panelWidth, float topY)
    {
        cx = centerX;
        left = centerX - panelWidth * 0.5f;
        right = centerX + panelWidth * 0.5f;
        root = new GameObject("RoadRoot").transform;
        root.SetParent(transform, false);

        Quad("Sky", new Vector3(cx, (horizonY + topY) * 0.5f, 6f), new Vector3(panelWidth, topY - horizonY, 0.01f), sky);
        Quad("Grass", new Vector3(cx, (horizonY + bottomY) * 0.5f, 5.9f), new Vector3(panelWidth, horizonY - bottomY, 0.01f), grass);
        for (int i = 0; i < 4; i++)
        {
            float x = Mathf.Lerp(left + 2.1f, right - 2.1f, i / 3f);
            var hill = MaterialUtil.Primitive(PrimitiveType.Sphere, root, new Vector3(x, horizonY, 5.95f), new Vector3(3f + i % 2, 1.1f + 0.3f * (i % 2), 0.1f), new Color(0.3f, 0.5f, 0.35f));
            hill.name = "Hill";
        }

        var roadGo = new GameObject("Road", typeof(MeshFilter), typeof(MeshRenderer));
        roadGo.transform.SetParent(root, false);
        roadGo.GetComponent<MeshRenderer>().sharedMaterial = MaterialUtil.Lit(road);
        var mesh = new Mesh
        {
            vertices = new[]
            {
                new Vector3(cx - roadBottomHalf, bottomY, 5.8f), new Vector3(cx + roadBottomHalf, bottomY, 5.8f),
                new Vector3(cx + roadTopHalf, horizonY, 5.8f), new Vector3(cx - roadTopHalf, horizonY, 5.8f),
            },
            triangles = new[] { 0, 2, 1, 0, 3, 2 },
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        roadGo.GetComponent<MeshFilter>().sharedMesh = mesh;

        for (int i = 0; i < DashCount; i++)
            dashes.Add(Quad("Dash", Vector3.zero, Vector3.one, new Color(0.78f, 0.66f, 0.48f)));

        for (int side = -1; side <= 1; side += 2)
        for (int i = 0; i < PropsPerSide; i++)
        {
            bool tree = (i + (side > 0 ? 1 : 0)) % 2 == 0;
            var prop = new GameObject(tree ? "Tree" : "Rock").transform;
            prop.SetParent(root, false);
            if (tree)
            {
                MaterialUtil.Primitive(PrimitiveType.Cube, prop, new Vector3(0f, 0.3f, 0f), new Vector3(0.18f, 0.6f, 0.18f), new Color(0.45f, 0.3f, 0.18f));
                MaterialUtil.Primitive(PrimitiveType.Sphere, prop, new Vector3(0f, 0.85f, 0f), new Vector3(0.8f, 0.8f, 0.5f), new Color(0.25f, 0.55f, 0.28f));
            }
            else
            {
                MaterialUtil.Primitive(PrimitiveType.Sphere, prop, new Vector3(0f, 0.18f, 0f), new Vector3(0.55f, 0.36f, 0.4f), new Color(0.55f, 0.55f, 0.58f));
            }
            props.Add((prop, side, tree));
        }

        // 풍경은 그림자를 만들지도 받지도 않는다 (풀밭에 검은 얼룩이 생기지 않게)
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        Layout();
    }

    Transform Quad(string name, Vector3 pos, Vector3 scale, Color color)
    {
        var go = MaterialUtil.Primitive(PrimitiveType.Cube, root, pos, scale, color);
        go.name = name;
        return go.transform;
    }

    /// <summary>길 위 깊이 s(0 = 지평선, 1 = 화면 아래)를 원근처럼 휘어 화면 높이 비율로 바꾼다.</summary>
    static float Depth(float s) => s * s;

    float YAt(float f) => Mathf.Lerp(horizonY, bottomY, f);
    float HalfWidthAt(float f) => Mathf.Lerp(roadTopHalf, roadBottomHalf, f);

    void Layout()
    {
        float bob = running ? Mathf.Abs(Mathf.Sin(bobTime * 9f)) * 0.08f : 0f;
        root.localPosition = new Vector3(0f, -bob, 0f);

        for (int i = 0; i < dashes.Count; i++)
        {
            float s0 = Mathf.Repeat(i / (float)DashCount + phase, 1f);
            float s1 = Mathf.Min(1f, s0 + 0.45f / DashCount);
            float f0 = Depth(s0), f1 = Depth(s1);
            float y0 = YAt(f0), y1 = YAt(f1);
            var d = dashes[i];
            d.localPosition = new Vector3(cx, (y0 + y1) * 0.5f, 5.75f);
            d.localScale = new Vector3(Mathf.Lerp(0.03f, 0.3f, f0), Mathf.Max(0.02f, y0 - y1), 0.01f);
        }

        for (int i = 0; i < props.Count; i++)
        {
            var (t, side, tree) = props[i];
            int index = i % PropsPerSide;
            float s = Mathf.Repeat(index / (float)PropsPerSide + phase + (side > 0 ? 0.1f : 0f), 1f);
            float f = Depth(s);
            float scale = Mathf.Lerp(0.12f, tree ? 1.5f : 1.1f, f);
            float x = cx + side * (HalfWidthAt(f) + Mathf.Lerp(0.15f, 0.9f, f));
            t.localPosition = new Vector3(x, YAt(f), 5.7f - f);
            t.localScale = Vector3.one * scale;
            // 왼쪽 보드 쪽으로 넘어가거나 패널 밖으로 나가는 소품은 숨긴다
            t.gameObject.SetActive(x - 0.5f * scale > left && x + 0.5f * scale < right);
        }
    }

    void Update()
    {
        if (root == null) return;
        if (running)
        {
            phase += runSpeed * Time.deltaTime * 0.35f;
            bobTime += Time.deltaTime;
        }
        Layout();
    }

    /// <summary>스테이지 사이 달리기 연출.</summary>
    public IEnumerator Run(float duration)
    {
        running = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            if (Mathf.Repeat(t, 0.32f) < Time.deltaTime) Sfx.Play(SfxType.Swap, 0.5f, 0.5f); // 발소리
            yield return null;
        }
        running = false;
    }
}
