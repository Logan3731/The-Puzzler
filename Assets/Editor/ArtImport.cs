using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// VARCO로 뽑은 이미지·소리를 프로젝트로 가져온다. 메뉴: Puzzler > Import VARCO Art and Sound
/// - 원본 위치: 프로젝트 폴더 옆의 picture/, sound/ (C:\Users\KGA01\puzzle\picture, \sound)
/// - 이미지: 테두리에서부터 흰(밝은 회색) 배경을 지워 투명하게 만들고, 그림만 남게 잘라 512px 이하로 줄인다
///           → Assets/Resources/Art/{영문 이름}.png (게임이 이름으로 찾아 쓴다)
/// - 소리: 앞 무음을 자르고 첫 소리만 짧게 남긴 뒤 끝을 줄인다 → Assets/Resources/Audio/sfx_{이름}.wav
/// </summary>
public static class ArtImport
{
    // 원본 파일 이름(확장자 제외) → 게임에서 쓰는 이름
    static readonly Dictionary<string, string> imageNames = new Dictionary<string, string>
    {
        { "원소_불", "element_fire" }, { "원소_물", "element_water" }, { "원소_바람", "element_wind" },
        { "원소_번개", "element_lightning" }, { "원소_어둠", "element_dark" },
        { "슬라임", "monster_slime" }, { "1챕터_불꽃_고블린", "monster_goblin" }, { "1챕터_늪지_두꺼비", "monster_toad" },
        { "1챕터_보스_그림자_늑대", "monster_wolf_boss" },
        { "마법미사일", "missile" }, { "전사의_검", "sword" },
    };

    // 소리 파일 이름의 첫 단어 → (게임 이름, 최대 길이 초)
    static readonly Dictionary<string, (string name, float maxSeconds)> soundNames = new Dictionary<string, (string, float)>(StringComparer.OrdinalIgnoreCase)
    {
        { "Pop", ("sfx_pop", 0.3f) }, { "Hit", ("sfx_hit", 0.35f) }, { "Swap", ("sfx_swap", 0.15f) },
        { "Whoosh", ("sfx_whoosh", 0.45f) }, { "Impact", ("sfx_impact", 0.8f) }, { "Chime", ("sfx_chime", 0.9f) },
    };

    const int MaxTextureSize = 512;

    [MenuItem("Puzzler/Import VARCO Art and Sound")]
    public static void ImportAll()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        int images = ImportImages(Path.Combine(root, "picture"), "Assets/Resources/Art");
        int sounds = ImportSounds(Path.Combine(root, "sound"), "Assets/Resources/Audio");
        AssetDatabase.Refresh();
        ApplyTextureSettings("Assets/Resources/Art");
        Debug.Log($"[ArtImport] 이미지 {images}개, 소리 {sounds}개를 가져왔습니다.");
    }

    // ───────────── 이미지 ─────────────

    static int ImportImages(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir)) return 0;
        Directory.CreateDirectory(targetDir);
        int count = 0;
        foreach (var file in Directory.GetFiles(sourceDir, "*.png"))
        {
            string key = Path.GetFileNameWithoutExtension(file);
            if (!imageNames.TryGetValue(key, out var name))
            {
                Debug.LogWarning($"[ArtImport] 이름 대응이 없는 이미지는 건너뜀: {key}");
                continue;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(file));
            var result = RemoveBackground(tex);
            File.WriteAllBytes(Path.Combine(targetDir, name + ".png"), result.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(result);
            count++;
        }
        return count;
    }

    static int Sat(Color32 c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));
    static int Lum(Color32 c) => (c.r + c.g + c.b) / 3;
    static int Diff(Color32 a, Color32 b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

    /// <summary>테두리에서 이어진 밝은 무채색 배경만 지운다 (그림 안쪽의 흰색은 남는다).</summary>
    static Texture2D RemoveBackground(Texture2D src)
    {
        int w = src.width, h = src.height;
        var px = src.GetPixels32();

        // 배경 기준색 = 네 모서리 평균
        Color32[] corners = { px[0], px[w - 1], px[(h - 1) * w], px[h * w - 1] };
        var bgRef = new Color32((byte)Avg(corners, 0), (byte)Avg(corners, 1), (byte)Avg(corners, 2), 255);
        bool IsBg(Color32 c) => Sat(c) <= 24 && Diff(c, bgRef) <= 30;

        var bg = new bool[w * h];
        var queue = new Queue<int>();
        void Seed(int i)
        {
            if (bg[i] || !IsBg(px[i])) return;
            bg[i] = true;
            queue.Enqueue(i);
        }
        for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
        for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % w, y = i / w;
            TryAdd(x + 1, y, i); TryAdd(x - 1, y, i); TryAdd(x, y + 1, i); TryAdd(x, y - 1, i);
        }
        void TryAdd(int x, int y, int from)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int n = y * w + x;
            if (bg[n] || !IsBg(px[n]) || Diff(px[n], px[from]) > 14) return;
            bg[n] = true;
            queue.Enqueue(n);
        }

        // 배경에 붙은 밝은 테두리(안티앨리어싱 흰 띠) 한 겹 정리
        var halo = new List<int>();
        for (int y = 1; y < h - 1; y++)
        for (int x = 1; x < w - 1; x++)
        {
            int i = y * w + x;
            if (bg[i]) continue;
            bool touches = bg[i - 1] || bg[i + 1] || bg[i - w] || bg[i + w];
            if (touches && Lum(px[i]) > 215 && Sat(px[i]) < 30) halo.Add(i);
        }
        foreach (var i in halo) bg[i] = true;

        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int i = 0; i < px.Length; i++)
        {
            if (bg[i]) { px[i] = new Color32(0, 0, 0, 0); continue; }
            int x = i % w, y = i / w;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        if (maxX < 0) { minX = minY = 0; maxX = w - 1; maxY = h - 1; }

        // 그림만 남게 잘라내고 (여백 4px), 512px 이하로 줄인다
        const int pad = 4;
        minX = Mathf.Max(0, minX - pad); minY = Mathf.Max(0, minY - pad);
        maxX = Mathf.Min(w - 1, maxX + pad); maxY = Mathf.Min(h - 1, maxY + pad);
        int cw = maxX - minX + 1, chh = maxY - minY + 1;
        int k = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(cw, chh) / (float)MaxTextureSize));
        int ow = cw / k, oh = chh / k;
        var outPx = new Color32[ow * oh];
        for (int oy = 0; oy < oh; oy++)
        for (int ox = 0; ox < ow; ox++)
        {
            // k×k 블록 평균 (알파 가중)
            float r = 0, g = 0, b = 0, a = 0;
            for (int dy = 0; dy < k; dy++)
            for (int dx = 0; dx < k; dx++)
            {
                var c = px[(minY + oy * k + dy) * w + minX + ox * k + dx];
                float ca = c.a / 255f;
                r += c.r * ca; g += c.g * ca; b += c.b * ca; a += ca;
            }
            outPx[oy * ow + ox] = a > 0f
                ? new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(255f * a / (k * k)))
                : new Color32(0, 0, 0, 0);
        }
        var result = new Texture2D(ow, oh, TextureFormat.RGBA32, false);
        result.SetPixels32(outPx);
        result.Apply();
        return result;
    }

    static int Avg(Color32[] cs, int channel)
    {
        int sum = 0;
        foreach (var c in cs) sum += channel == 0 ? c.r : channel == 1 ? c.g : c.b;
        return sum / cs.Length;
    }

    static void ApplyTextureSettings(string dir)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = MaxTextureSize;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
    }

    // ───────────── 소리 ─────────────

    static int ImportSounds(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir)) return 0;
        Directory.CreateDirectory(targetDir);
        int count = 0;
        foreach (var file in Directory.GetFiles(sourceDir, "*.wav"))
        {
            string first = Path.GetFileNameWithoutExtension(file).Split(' ', '(', '_')[0];
            if (!soundNames.TryGetValue(first, out var info))
            {
                Debug.LogWarning($"[ArtImport] 이름 대응이 없는 소리는 건너뜀: {Path.GetFileName(file)}");
                continue;
            }
            var bytes = TrimWav(File.ReadAllBytes(file), info.maxSeconds);
            File.WriteAllBytes(Path.Combine(targetDir, info.name + ".wav"), bytes);
            count++;
        }
        return count;
    }

    /// <summary>16비트 PCM WAV의 앞 무음을 자르고 maxSeconds 안에서 첫 소리만 남긴 뒤 끝을 줄인다.</summary>
    static byte[] TrimWav(byte[] wav, float maxSeconds)
    {
        int channels = 0, rate = 0, bits = 0, dataStart = -1, dataLen = 0;
        int p = 12;
        while (p + 8 <= wav.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(wav, p, 4);
            int len = BitConverter.ToInt32(wav, p + 4);
            if (id == "fmt ")
            {
                channels = BitConverter.ToInt16(wav, p + 10);
                rate = BitConverter.ToInt32(wav, p + 12);
                bits = BitConverter.ToInt16(wav, p + 22);
            }
            else if (id == "data")
            {
                dataStart = p + 8;
                dataLen = Mathf.Min(len, wav.Length - dataStart);
                break;
            }
            p += 8 + len + (len & 1);
        }
        if (bits != 16 || dataStart < 0) return wav; // 지원하지 않는 형식은 그대로

        int frames = dataLen / (2 * channels);
        var s = new short[frames * channels];
        Buffer.BlockCopy(wav, dataStart, s, 0, frames * channels * 2);

        int peak = 1;
        foreach (var v in s) peak = Mathf.Max(peak, Mathf.Abs((int)v));
        int threshold = peak / 20; // 최대 음량의 5%
        int start = 0;
        while (start < frames && Mathf.Abs((int)s[start * channels]) < threshold) start++;
        start = Mathf.Max(0, start - rate / 500); // 2ms 앞에서 시작

        int maxFrames = Mathf.Min(frames - start, Mathf.RoundToInt(maxSeconds * rate));
        int last = start;
        for (int f = start; f < start + maxFrames; f++)
            if (Mathf.Abs((int)s[f * channels]) >= threshold) last = f;
        int length = Mathf.Min(maxFrames, last - start + rate / 50); // 마지막 소리 + 20ms

        var outS = new short[length * channels];
        int fadeIn = Mathf.Min(length, rate / 500), fadeOut = Mathf.Min(length, Mathf.Max(rate / 25, length / 3));
        for (int f = 0; f < length; f++)
        {
            float g = 1f;
            if (f < fadeIn) g = f / (float)fadeIn;
            int fromEnd = length - f;
            if (fromEnd < fadeOut) g *= fromEnd / (float)fadeOut;
            for (int c = 0; c < channels; c++)
                outS[f * channels + c] = (short)(s[(start + f) * channels + c] * g);
        }

        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            int byteLen = outS.Length * 2;
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + byteLen);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); bw.Write(16);
            bw.Write((short)1); bw.Write((short)channels); bw.Write(rate);
            bw.Write(rate * channels * 2); bw.Write((short)(channels * 2)); bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data")); bw.Write(byteLen);
            var buf = new byte[byteLen];
            Buffer.BlockCopy(outS, 0, buf, 0, byteLen);
            bw.Write(buf);
            return ms.ToArray();
        }
    }
}
