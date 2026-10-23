using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VARCO로 만든 그림(Assets/Resources/Art)을 이름으로 찾는다. 없으면 null → 각 스크립트가 임시 도형을 쓴다.
/// 그림은 판(쿼드)에 붙여 3D 화면 안에 세운다. 화면 전체가 픽셀 그림판으로 그려져 도트 느낌이 맞춰진다.
/// </summary>
public static class ArtLibrary
{
    public static readonly string[] ElementArt = { "element_fire", "element_water", "element_wind", "element_lightning", "element_dark" };

    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
    static readonly Dictionary<Texture2D, Material> materials = new Dictionary<Texture2D, Material>();
    static Shader unlit;

    public static Texture2D Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!cache.TryGetValue(name, out var tex))
        {
            tex = Resources.Load<Texture2D>("Art/" + name);
            cache[name] = tex;
        }
        return tex;
    }

    /// <summary>그림 한 장용 언릿 머티리얼 (투명 부분은 잘라낸다). _BaseColor를 1보다 크게 주면 밝게 번쩍인다.</summary>
    public static Material MaterialFor(Texture2D tex)
    {
        if (materials.TryGetValue(tex, out var mat) && mat != null) return mat;
        if (unlit == null) unlit = Shader.Find("Universal Render Pipeline/Unlit");
        mat = new Material(unlit) { name = "Art_" + tex.name };
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor(MaterialUtil.BaseColorId, Color.white);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        materials[tex] = mat;
        return mat;
    }

    /// <summary>
    /// 그림을 붙인 판을 만든다. 긴 쪽이 size가 되도록 비율을 맞춘다.
    /// bottomAt이 있으면 그림 아래쪽이 그 높이(로컬 y)에 오게 놓는다 (몬스터 발 위치).
    /// </summary>
    public static GameObject Quad(Texture2D tex, Transform parent, float size, float? bottomAt = null, bool fitHeight = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "Art_" + tex.name;
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = MaterialFor(tex);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        SetSize(go.transform, tex, size, bottomAt, fitHeight);
        return go;
    }

    public static void SetSize(Transform quad, Texture2D tex, float size, float? bottomAt = null, bool fitHeight = false)
    {
        float aspect = tex.width / (float)tex.height;
        float w, h;
        if (fitHeight || aspect < 1f) { h = size; w = size * aspect; }
        else { w = size; h = size / aspect; }
        quad.localScale = new Vector3(w, h, 1f);
        quad.localPosition = new Vector3(0f, bottomAt.HasValue ? bottomAt.Value + h * 0.5f : 0f, 0f);
    }

    /// <summary>판의 세로 길이 (bottomAt 기준으로 놓았을 때 꼭대기 계산용).</summary>
    public static float HeightFor(Texture2D tex, float size, bool fitHeight = false)
    {
        float aspect = tex.width / (float)tex.height;
        return fitHeight || aspect < 1f ? size : size / aspect;
    }
}
