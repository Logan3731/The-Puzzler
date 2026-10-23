using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 런타임에 임시(플레이스홀더) 머티리얼을 만들어 캐시한다.
/// VARCO 3D 모델로 교체하면 모델 자체 머티리얼을 쓰므로 블록에는 쓰이지 않는다.
/// </summary>
public static class MaterialUtil
{
    static Shader litShader;
    static Material spriteDefault;
    static readonly Dictionary<(Color, float), Material> cache = new Dictionary<(Color, float), Material>();

    public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    public static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public static Material Lit(Color color, float emission = 0f)
    {
        var key = (color, emission);
        if (cache.TryGetValue(key, out var mat) && mat != null) return mat;

        if (litShader == null)
            litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        mat = new Material(litShader);
        mat.SetColor(BaseColorId, color);
        mat.SetFloat("_Smoothness", 0.6f);
        if (emission > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor(EmissionColorId, color * emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        cache[key] = mat;
        return mat;
    }

    /// <summary>정점 색상을 쓰는 언릿 머티리얼 (트레일 등).</summary>
    public static Material SpriteDefault
    {
        get
        {
            if (spriteDefault == null) spriteDefault = new Material(Shader.Find("Sprites/Default"));
            return spriteDefault;
        }
    }

    public static GameObject Primitive(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, float emission = 0f)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = Lit(color, emission);
        return go;
    }
}
