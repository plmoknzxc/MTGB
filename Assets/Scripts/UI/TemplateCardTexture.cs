using UnityEngine;

/// <summary>
/// 生成模板卡贴图（spec 第 5 节）：无图卡的战场面/替代面。
/// 结构化样式：名牌 / 法力费 / 色框 / 类型行 / 规则文本行 / 攻防角标（文字由 TextMesh 叠加，
/// 贴图只负责底、色框与骨架线）。禁止退回扁平色块。
/// </summary>
public static class TemplateCardTexture
{
    public static Texture2D Build(CardDefinition data, int width, int height)
    {
        width = Mathf.Clamp(width, 92, 512);
        height = Mathf.Clamp(height, 128, 712);
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = $"TemplateCard_{data?.cardName ?? "Card"}",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        ManaColor color = data != null ? CardVisualStyle.GetPrimaryColor(data) : ManaColor.Colorless;
        GetColorSchemePublic(color, out Color nameBand, out Color artA, out Color artB, out Color artMark);

        Color back = IntegratedArenaPalette.GenBack;
        Color border = IntegratedArenaPalette.CardBorder;
        Color artBorder = IntegratedArenaPalette.GenArtBorder;
        Color ruleLine = IntegratedArenaPalette.GenRuleLine;
        Color ptBack = IntegratedArenaPalette.GenPtBack;
        Color ptBorder = IntegratedArenaPalette.GenPtBorder;

        // 相对布局（基于 92×128 基准，按比例缩放）
        float u2px = width / 92f;
        int borderPx = Mathf.Max(1, Mathf.RoundToInt(1f * u2px));
        int nameTop = 0, nameBottom = Mathf.RoundToInt(17f * u2px);          // 名牌带
        int artLeft = Mathf.RoundToInt(7f * u2px), artRight = width - artLeft;
        int artTop = Mathf.RoundToInt(20f * u2px), artBottom = height - Mathf.RoundToInt(33f * u2px);
        int typeTop = artBottom + Mathf.RoundToInt(2f * u2px);
        int typeBottom = typeTop + Mathf.RoundToInt(11f * u2px);
        int rulesTop = typeBottom + Mathf.RoundToInt(4f * u2px);

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = back;
                bool edge = x < borderPx || x >= width - borderPx || y < borderPx || y >= height - borderPx;
                if (edge) pixel = border;

                if (y >= nameTop && y < nameBottom && !edge)
                {
                    pixel = nameBand;
                    if (y >= nameBottom - borderPx) pixel = IntegratedArenaPalette.LineStrong;
                }
                else if (x >= artLeft && x < artRight && y >= artTop && y < artBottom)
                {
                    float k = Mathf.InverseLerp(artTop, artBottom, y);
                    pixel = Color.Lerp(artA, artB, k);
                    if (x < artLeft + borderPx || x >= artRight - borderPx
                        || y < artTop + borderPx || y >= artBottom - borderPx) pixel = artBorder;

                    // 中央菱形符号
                    int cx = width / 2, cy = (artTop + artBottom) / 2;
                    int mark = Mathf.RoundToInt(Mathf.Min(artBottom - artTop, artRight - artLeft) * 0.13f);
                    int dx = Mathf.Abs(x - cx), dy = Mathf.Abs(y - cy);
                    if (dx + dy < mark) pixel = artMark;
                    else if (dx + dy < mark + 2) pixel = Color.Lerp(artMark, artB, 0.4f);
                }
                else if (y >= typeTop && y < typeBottom && !edge)
                {
                    // 类型行底为卡底色，仅保留下边框线
                    pixel = back;
                    if (y >= typeBottom - 1) pixel = IntegratedArenaPalette.LineStrong;
                }
                else if (y >= rulesTop && y < height - Mathf.RoundToInt(10f * u2px) && !edge)
                {
                    // 规则文本骨架线：三条，宽度 100%/78%/56%
                    float local = y - rulesTop;
                    int lineIndex = Mathf.FloorToInt(local / (5f * u2px));
                    float lineOffset = Mathf.Repeat(local, 5f * u2px);
                    int lineWidth = lineIndex switch
                    {
                        0 => Mathf.RoundToInt(58f * u2px),
                        1 => Mathf.RoundToInt(45f * u2px),
                        _ => Mathf.RoundToInt(33f * u2px)
                    };
                    int lineLeft = Mathf.RoundToInt(8f * u2px);
                    if (lineIndex <= 2 && lineOffset < 3f * u2px && x >= lineLeft && x < lineLeft + lineWidth)
                        pixel = ruleLine;
                }

                // 攻防角标（右下）
                int ptRight = width - Mathf.RoundToInt(4f * u2px);
                int ptLeft = ptRight - Mathf.RoundToInt(20f * u2px);
                int ptTop = height - Mathf.RoundToInt(13f * u2px);
                int ptBottom = height - Mathf.RoundToInt(4f * u2px);
                if (data != null && data.cardTypes.Has(CardType.Creature)
                    && x >= ptLeft && x < ptRight && y >= ptTop && y < ptBottom)
                {
                    pixel = ptBack;
                    if (x < ptLeft + 1 || x >= ptRight - 1 || y < ptTop + 1 || y >= ptBottom - 1) pixel = ptBorder;
                }

                pixels[y * width + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    public static void GetColorSchemePublic(ManaColor color, out Color nameBand, out Color artA, out Color artB, out Color artMark)
    {
        switch (color)
        {
            case ManaColor.White:
                nameBand = IntegratedArenaPalette.GenWName; artA = IntegratedArenaPalette.GenWArtA;
                artB = IntegratedArenaPalette.GenWArtB; artMark = IntegratedArenaPalette.GenWArtMark;
                return;
            case ManaColor.Black:
                nameBand = IntegratedArenaPalette.GenBName; artA = IntegratedArenaPalette.GenBArtA;
                artB = IntegratedArenaPalette.GenBArtB; artMark = IntegratedArenaPalette.GenBArtMark;
                return;
            case ManaColor.Red:
                nameBand = IntegratedArenaPalette.GenRName; artA = IntegratedArenaPalette.GenRArtA;
                artB = IntegratedArenaPalette.GenRArtB; artMark = IntegratedArenaPalette.GenRArtMark;
                return;
            case ManaColor.Green:
                nameBand = IntegratedArenaPalette.GenGName; artA = IntegratedArenaPalette.GenGArtA;
                artB = IntegratedArenaPalette.GenGArtB; artMark = IntegratedArenaPalette.GenGArtMark;
                return;
            case ManaColor.Blue:
                nameBand = IntegratedArenaPalette.GenUName; artA = IntegratedArenaPalette.GenUArtA;
                artB = IntegratedArenaPalette.GenUArtB; artMark = IntegratedArenaPalette.GenUArtMark;
                return;
            default:
                nameBand = IntegratedArenaPalette.GenCName; artA = IntegratedArenaPalette.GenCArtA;
                artB = IntegratedArenaPalette.GenCArtB; artMark = IntegratedArenaPalette.GenCArtMark;
                return;
        }
    }
}

/// <summary>中线贴图：2px 红→银→蓝渐变 + 中央菱形 10px + 轻辉光（spec 第 5 节）。</summary>
public static class MidlineTexture
{
    public static Texture2D Build()
    {
        const int width = 1024;
        const int height = 48;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Midline",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color red = Alpha(IntegratedArenaPalette.Red, 0.42f);
        Color silver = Alpha(new Color(196f / 255f, 206f / 255f, 220f / 255f), 0.45f);
        Color blue = Alpha(IntegratedArenaPalette.Blue, 0.42f);
        Color glowColor = Alpha(new Color(151f / 255f, 170f / 255f, 199f / 255f), 0.20f);
        Color diamondFill = IntegratedArenaPalette.Hex("#0B0E14FF");
        Color diamondBorder = IntegratedArenaPalette.Hex("#6B7889FF");

        Color[] pixels = new Color[width * height];
        for (int x = 0; x < width; x++)
        {
            float u = (float)x / (width - 1);
            float envelope = u < 0.03f ? u / 0.03f : u > 0.97f ? (1f - u) / 0.03f : 1f;
            Color lineColor = u < 0.5f ? Color.Lerp(red, silver, u * 2f) : Color.Lerp(silver, blue, (u - 0.5f) * 2f);

            for (int y = 0; y < height; y++)
            {
                Color pixel = Color.clear;
                float dy = y - (height - 1) * 0.5f;
                // 线体（中心 ±2px）
                if (Mathf.Abs(dy) <= 1.5f) pixel = Alpha(lineColor, lineColor.a * envelope);
                // 辉光（高斯衰减）
                else pixel = Alpha(glowColor, glowColor.a * Mathf.Exp(-dy * dy / 42f) * envelope);

                // 中央菱形（CSS: 10px 方形旋转 45°）。屏幕↔贴图换算：
                // 横向 1024 tex ↔ ~1590px 屏幕，纵向 48 tex ↔ ~31px 屏幕
                float dxTex = x - width * 0.5f;
                float dxNorm = dxTex / 3.2f;
                float dyNorm = dy / 7.7f;
                float diamondDistance = Mathf.Abs(dxNorm) + Mathf.Abs(dyNorm);
                if (diamondDistance < 1f)
                {
                    pixel = diamondFill;
                    if (diamondDistance > 0.7f) pixel = diamondBorder;
                }

                pixels[y * width + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Color Alpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
