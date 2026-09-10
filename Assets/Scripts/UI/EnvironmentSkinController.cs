using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 环境层（spec 第 3 节）：战场背景的唯一载体，只含背景、不含任何卡牌/HUD 逻辑。
/// 世界侧 = 桌面地板（纵向渐变 + 氛围光晕 + 磨痕纹理的生成贴图）；
/// 屏幕侧 = 双桌沿、桌沿内外压暗带（oppzone/handzone）、左右暗角。
/// 换肤 = 只换这一层（曜石 / 熔木两套内建皮肤，spec 已验证）。
/// </summary>
public class EnvironmentSkinController : MonoBehaviour
{
    public enum SkinId
    {
        Obsidian = 0,
        Ember = 1
    }

    private struct SkinDefinition
    {
        public string Name;
        public (float t, Color color)[] FloorStops;
        public Color BlueGlow;
        public Color RedGlow;
        public Color CenterGlow;
        public Color EdgeAccent; // 近端桌沿微光
    }

    private static readonly SkinDefinition ObsidianSkin = new SkinDefinition
    {
        Name = "曜石竞技场 OBSIDIAN",
        FloorStops = new (float, Color)[]
        {
            (0f, C("#0B0F17")), (0.24f, C("#0C111A")), (0.55f, C("#111725")),
            (0.74f, C("#131A29")), (0.92f, C("#0F1522")), (1f, C("#0D1220"))
        },
        BlueGlow = C("#4285F4"),
        RedGlow = C("#DC3C5A"),
        CenterGlow = C("#97AAC7"),
        EdgeAccent = C("#4285F4")
    };

    private static readonly SkinDefinition EmberSkin = new SkinDefinition
    {
        Name = "熔木 EMBER",
        FloorStops = new (float, Color)[]
        {
            (0f, C("#070505")), (0.24f, C("#0C0808")), (0.55f, C("#150F0D")),
            (0.74f, C("#1D1512")), (0.92f, C("#191210")), (1f, C("#0E0A08"))
        },
        BlueGlow = C("#FFA657"),
        RedGlow = C("#D6783C"),
        CenterGlow = C("#E6BE8C"),
        EdgeAccent = C("#D6A05A")
    };

    private static Color C(string hex) => IntegratedArenaPalette.Hex(hex);

    [SerializeField] private SkinId skin = SkinId.Obsidian;
    [SerializeField] private bool allowSkinToggleKey = true;

    private MeshRenderer floorRenderer;
    private Image nearEdgeImage;
    private Image farEdgeImage;
    private SkinDefinition currentSkin;

    public SkinId CurrentSkin => skin;

    public void Initialize(Camera worldCamera, Transform canvasTransform)
    {
        currentSkin = skin == SkinId.Ember ? EmberSkin : ObsidianSkin;
        CreateFloor();
        CreateScreenOverlays(canvasTransform);
        ApplySkin(skin);

        if (worldCamera != null)
        {
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = IntegratedArenaPalette.BodyBack;
        }
    }

    private void Update()
    {
        if (floorRenderer == null || nearEdgeImage == null)
        {
            floorRenderer = transform.Find("ArenaFloor")?.GetComponent<MeshRenderer>();
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
            Transform env = canvas != null ? canvas.transform.Find("EnvironmentLayer") : null;
            if (env != null)
            {
                if (nearEdgeImage == null) nearEdgeImage = env.Find("NearEdge")?.GetComponent<Image>();
                if (farEdgeImage == null) farEdgeImage = env.Find("FarEdge")?.GetComponent<Image>();
            }
        }

        if (allowSkinToggleKey && Input.GetKeyDown(KeyCode.F9))
        {
            ApplySkin(skin == SkinId.Obsidian ? SkinId.Ember : SkinId.Obsidian);
        }
    }

    /// <summary>换肤：只重建环境层贴图/着色，卡牌与 HUD 零感知（spec 第 3 节）。</summary>
    public void ApplySkin(SkinId id)
    {
        skin = id;
        currentSkin = id == SkinId.Ember ? EmberSkin : ObsidianSkin;
        if (floorRenderer != null)
        {
            floorRenderer.sharedMaterial.SetTexture("_BaseMap", BuildFloorTexture(currentSkin));
            floorRenderer.sharedMaterial.SetColor("_BaseColor", Color.white);
        }
        if (nearEdgeImage != null) nearEdgeImage.sprite = BuildEdgeTexture(near: true, currentSkin);
        if (farEdgeImage != null) farEdgeImage.sprite = BuildEdgeTexture(near: false, currentSkin);
    }

    // ---------- 世界侧：桌面地板 ----------

    private void CreateFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = "ArenaFloor";
        floor.transform.SetParent(transform, false);
        // Unity Quad 正面朝 −Z：旋转 X +90° 后法线才朝上（−90° 会被背面剔除）。
        // 旋转后 UV v=0 对应近端（−z），贴图采样按 1−t 翻转（t=0 为 CSS 顶部=远端色）
        floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        floor.transform.localPosition = new Vector3(0f, 0f, 0f);
        floor.transform.localScale = new Vector3(30f, 6.6f, 1f);
        if (Application.isPlaying) Object.Destroy(floor.GetComponent<Collider>());
        else Object.DestroyImmediate(floor.GetComponent<Collider>());

        floorRenderer = floor.GetComponent<MeshRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("[MTGB] URP Unlit shader not found; fallback Unlit/Texture");
            shader = Shader.Find("Unlit/Texture");
        }
        Material material = new Material(shader) { name = "ArenaFloorMaterial" };
        material.SetTexture("_BaseMap", BuildFloorTexture(currentSkin));
        material.SetColor("_BaseColor", Color.white);
        floorRenderer.sharedMaterial = material;
    }

    private static Texture2D BuildFloorTexture(in SkinDefinition skin)
    {
        const int width = 512;
        const int height = 384;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = $"ArenaFloor_{skin.Name}",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        // CSS 预览中地板覆盖屏幕带 202..874（672px 高 × 1920 宽），光晕坐标换算到该空间
        const float bandWidth = 1920f;
        const float bandHeight = 672f;
        (Vector2 center, Vector2 radii, Color color, float fade)[] glows =
        {
            (new Vector2(0.50f * bandWidth, 1.06f * bandHeight), new Vector2(1500f, 520f), Alpha(skin.BlueGlow, 0.11f), 0.62f),
            (new Vector2(0.50f * bandWidth, -0.06f * bandHeight), new Vector2(1100f, 360f), Alpha(skin.RedGlow, 0.06f), 0.60f),
            (new Vector2(0.50f * bandWidth, 0.54f * bandHeight), new Vector2(2400f, 800f), Alpha(skin.CenterGlow, 0.05f), 0.70f)
        };

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            // v=0 行 = 近端；CSS t=0 是远端，故按 1−t 采样
            float t = (float)y / (height - 1);
            Color baseColor = IntegratedUiUtility.SampleStops(skin.FloorStops, 1f - t);
            float bandY = (1f - t) * bandHeight;
            for (int x = 0; x < width; x++)
            {
                float bandX = (float)x / (width - 1) * bandWidth;
                Color pixel = baseColor;
                for (int i = 0; i < glows.Length; i++)
                {
                    Vector2 offset = new Vector2((bandX - glows[i].center.x) / glows[i].radii.x,
                        (bandY - glows[i].center.y) / glows[i].radii.y);
                    float distance = Mathf.Sqrt(offset.x * offset.x + offset.y * offset.y);
                    if (distance >= glows[i].fade) continue;
                    float glowAlpha = glows[i].color.a * (1f - distance / glows[i].fade);
                    pixel = AlphaLerp(pixel, glows[i].color, glowAlpha);
                }

                // 极淡横向磨痕：2px 亮纹 / 9px 周期，带 2° 倾斜（CSS repeating-linear-gradient(178deg)）
                float stripe = Mathf.Repeat(y + Mathf.Sin(x * 0.035f) * 2f, 9f);
                if (stripe < 2f) pixel += new Color(0.012f, 0.012f, 0.012f, 0f);

                pixels[y * width + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    // ---------- 屏幕侧：桌沿 / 压暗带 / 暗角 ----------

    private void CreateScreenOverlays(Transform canvasTransform)
    {
        RectTransform root = IntegratedUiUtility.CreateRect(canvasTransform, "EnvironmentLayer");
        IntegratedUiUtility.Stretch(root, 0f, 0f, 0f, 0f);

        MakeBand(root, "OppZoneBand", new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f),
            new Vector2(0f, 200f), IntegratedUiUtility.VerticalGradientTexture(4, 64, new (float, Color)[]
            {
                (0f, Alpha(C("#030509"), 0.90f)), (1f, Alpha(C("#04060A"), 0.60f))
            }));
        MakeBand(root, "HandZoneBand", new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f),
            new Vector2(0f, 200f), IntegratedUiUtility.VerticalGradientTexture(4, 64, new (float, Color)[]
            {
                (0f, Alpha(C("#04060A"), 0.66f)), (1f, Alpha(C("#030509"), 0.90f))
            }));

        Texture2D vignette = IntegratedUiUtility.HorizontalGradientTexture(256, 4, new (float, Color)[]
        {
            (0f, Alpha(C("#030508"), 0.55f)), (1f, Alpha(C("#030508"), 0f))
        });
        MakeBand(root, "VignetteLeft", new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(0f, 0f),
            new Vector2(172.8f, 0f), vignette);
        Texture2D vignetteRight = IntegratedUiUtility.HorizontalGradientTexture(256, 4, new (float, Color)[]
        {
            (0f, Alpha(C("#030508"), 0f)), (1f, Alpha(C("#030508"), 0.55f))
        });
        MakeBand(root, "VignetteRight", new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, 0f),
            new Vector2(172.8f, 0f), vignetteRight);

        // 近端桌沿：线体 y878-880，其上 3px 蓝方微光、其下 5px 压暗（贴图覆盖 y874..886）
        nearEdgeImage = MakeBand(root, "NearEdge", new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 194f),
            new Vector2(0f, 12f), null);
        // 远端桌沿：线体 y200-202，其下 4px 红方微光、其上 4px 压暗（贴图覆盖 y194..206）
        farEdgeImage = MakeBand(root, "FarEdge", new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -194f),
            new Vector2(0f, 12f), null);
    }

    private static Image MakeBand(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPosition, Vector2 sizeDelta, Texture2D texture)
    {
        RectTransform rect = IntegratedUiUtility.CreateRect(parent, name);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        Image image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        if (texture != null) image.sprite = IntegratedUiUtility.CreateSprite(texture, texture.name);
        return image;
    }

    /// <summary>
    /// 生成桌沿贴图：银色线体 + 上/下微光与压暗，全部烘焙进 12 行高的贴图。
    /// 行序（上→下）：near = [微光 0..2][线 5..6][压暗 9..10]；far = [压暗 2..3][线 5..6][微光 8..10]。
    /// 水平方向带 10%–90% 淡出包络（微光 12%–88%）。
    /// </summary>
    private static Sprite BuildEdgeTexture(bool near, in SkinDefinition skin)
    {
        const int width = 960;
        const int height = 12;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = near ? "NearEdgeTexture" : "FarEdgeTexture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color line = Alpha(IntegratedArenaPalette.Silver, near ? 0.30f : 0.26f);
        Color accent = near ? Alpha(skin.EdgeAccent, 0.18f) : Alpha(IntegratedArenaPalette.Red, 0.14f);
        Color shadow = new Color(0f, 0f, 0f, 0.5f);
        int lineRow = 5;
        int accentRow = near ? 1 : 9;   // 近端微光在线上方，远端在线下方
        int shadowRow = near ? 9 : 2;   // 近端压暗在线下方，远端在线上方

        Color[] pixels = new Color[width * height];
        for (int x = 0; x < width; x++)
        {
            float u = (float)x / (width - 1);
            float lineEnvelope = EdgeEnvelope(u, 0.10f, 0.90f);
            float accentEnvelope = EdgeEnvelope(u, 0.12f, 0.88f);
            for (int y = 0; y < height; y++)
            {
                Color pixel = Color.clear;
                if (y == lineRow || y == lineRow + 1) pixel = Alpha(line, line.a * lineEnvelope);
                else if (y == accentRow) pixel = Alpha(accent, accent.a * accentEnvelope);
                else if (y == shadowRow) pixel = Alpha(shadow, shadow.a * lineEnvelope);
                pixels[y * width + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return IntegratedUiUtility.CreateSprite(texture, texture.name);
    }

    private static float EdgeEnvelope(float u, float fadeIn, float fadeOut)
    {
        if (u < fadeIn) return Mathf.Clamp01(u / fadeIn);
        if (u > fadeOut) return Mathf.Clamp01((1f - u) / (1f - fadeOut));
        return 1f;
    }

    private static Color Alpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    /// <summary>以发光色向底色做 alpha 合成（CSS radial-gradient 叠加语义）。</summary>
    private static Color AlphaLerp(Color back, Color glow, float glowAlpha)
    {
        float a = glowAlpha;
        return new Color(
            back.r * (1f - a) + glow.r * a,
            back.g * (1f - a) + glow.g * a,
            back.b * (1f - a) + glow.b * a,
            1f);
    }
}
