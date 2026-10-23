using UnityEngine;

public enum SfxType { Pop, Hit, Swap, Whoosh, Impact, Chime }

/// <summary>
/// 코드로 합성한 임시 효과음. 나중에 실제 사운드 파일로 바꾸려면 clips 배열만 교체하면 된다.
/// </summary>
public class Sfx : MonoBehaviour
{
    const int Rate = 44100;
    static Sfx instance;

    AudioClip[] clips;
    AudioSource[] sources;
    int next;
    readonly System.Random rng = new System.Random(7);

    public static void Play(SfxType type, float pitch = 1f, float volume = 1f)
    {
        if (instance == null) instance = new GameObject("Sfx").AddComponent<Sfx>();
        var source = instance.sources[instance.next];
        instance.next = (instance.next + 1) % instance.sources.Length;
        source.pitch = pitch;
        source.PlayOneShot(instance.clips[(int)type], volume);
    }

    void Awake()
    {
        sources = new AudioSource[16];
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].spatialBlend = 0f;
        }

        clips = new AudioClip[6];
        clips[(int)SfxType.Pop] = Make("Pop", 0.14f, Pop);
        clips[(int)SfxType.Hit] = Make("Hit", 0.12f, Hit);
        clips[(int)SfxType.Swap] = Make("Swap", 0.06f, Swap);
        clips[(int)SfxType.Whoosh] = Make("Whoosh", 0.3f, Whoosh);
        clips[(int)SfxType.Impact] = Make("Impact", 0.55f, Impact);
        clips[(int)SfxType.Chime] = Make("Chime", 0.6f, Chime);
    }

    static AudioClip Make(string name, float length, System.Action<float[]> fill)
    {
        var data = new float[Mathf.RoundToInt(length * Rate)];
        fill(data);
        for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    float Noise() => (float)rng.NextDouble() * 2f - 1f;

    // 뽁: 빠르게 떨어지는 음정 + 짧은 잡음 어택
    void Pop(float[] d)
    {
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            phase += 2f * Mathf.PI * (320f + 700f * Mathf.Exp(-t * 40f)) / Rate;
            float env = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t * 28f);
            float tone = Mathf.Sin(phase) * 0.75f + Mathf.Sign(Mathf.Sin(phase)) * 0.12f;
            d[i] = tone * env * 0.8f + Noise() * Mathf.Exp(-t * 220f) * 0.5f;
        }
    }

    // 퍽: 낮은 타격음
    void Hit(float[] d)
    {
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            phase += 2f * Mathf.PI * (90f + 180f * Mathf.Exp(-t * 30f)) / Rate;
            float s = Mathf.Sin(phase) * Mathf.Exp(-t * 32f) + Noise() * Mathf.Exp(-t * 260f) * 0.7f;
            d[i] = (float)System.Math.Tanh(s * 1.8f) * 0.85f;
        }
    }

    // 틱: 스왑
    void Swap(float[] d)
    {
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            d[i] = Mathf.Sin(2f * Mathf.PI * 1500f * t) * Mathf.Exp(-t * 70f) * 0.35f;
        }
    }

    // 휙: 저역 통과 잡음이 부풀었다 사라짐
    void Whoosh(float[] d)
    {
        float y = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float k = i / (float)d.Length;
            float env = Mathf.Sin(k * Mathf.PI);
            y += (0.02f + 0.3f * env * env) * (Noise() - y);
            d[i] = y * env * 2.2f;
        }
    }

    // 쿵: 무거운 강타
    void Impact(float[] d)
    {
        float phase = 0f, y = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            phase += 2f * Mathf.PI * (45f + 110f * Mathf.Exp(-t * 12f)) / Rate;
            y += 0.15f * (Noise() - y);
            float s = Mathf.Sin(phase) * Mathf.Exp(-t * 6f) * 1.2f + y * Mathf.Exp(-t * 20f) * 2.5f;
            d[i] = (float)System.Math.Tanh(s * 1.6f) * 0.95f;
        }
    }

    // 띠링: 콤보 단계 돌파
    void Chime(float[] d)
    {
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 6f);
            float s = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.5f
                    + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.3f
                    + Mathf.Sin(2f * Mathf.PI * 1760f * t) * 0.2f;
            d[i] = s * env * 0.6f;
        }
    }
}
