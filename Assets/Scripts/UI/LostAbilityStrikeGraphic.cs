using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// TMP's built-in strike skips sprite glyphs. Draw one continuous stroke per line,
// including mana/tap symbols, within the same scroll mask as the text.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class LostAbilityStrikeGraphic : MaskableGraphic
{
    private TextMeshProUGUI source;
    private readonly List<Rect> strokes = new List<Rect>();
    private readonly List<Rect> nextStrokes = new List<Rect>();

    public static void Attach(TextMeshProUGUI text)
    {
        var existing = text.GetComponentInChildren<LostAbilityStrikeGraphic>();
        if (existing != null) return;
        var go = new GameObject("LostAbilityStrikes", typeof(RectTransform));
        go.transform.SetParent(text.transform, false);
        var graphic = go.AddComponent<LostAbilityStrikeGraphic>();
        graphic.source = text;
        graphic.raycastTarget = false;
        graphic.color = new Color32(145, 153, 166, 255);
        graphic.rectTransform.anchorMin = Vector2.zero;
        graphic.rectTransform.anchorMax = Vector2.one;
        graphic.rectTransform.pivot = text.rectTransform.pivot;
        graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (source == null) return;
        if (rectTransform.pivot != source.rectTransform.pivot)
        {
            rectTransform.pivot = source.rectTransform.pivot;
            rectTransform.offsetMin = rectTransform.offsetMax = Vector2.zero;
        }
        if (source.havePropertiesChanged) source.ForceMeshUpdate();
        nextStrokes.Clear();
        var info = source.textInfo;
        for (int i = 0; i < info.characterCount;)
        {
            var first = info.characterInfo[i];
            if ((first.style & FontStyles.Strikethrough) == 0 || first.character == '\n') { i++; continue; }
            float left = first.origin, right = left;
            float y = first.baseLine + first.pointSize * .25f;
            bool hasText = false;
            int line = first.lineNumber;
            while (i < info.characterCount)
            {
                var c = info.characterInfo[i];
                if (c.lineNumber != line || c.character == '\n' || (c.style & FontStyles.Strikethrough) == 0) break;
                if (!char.IsWhiteSpace(c.character)) right = c.xAdvance;
                if (!hasText && c.elementType == TMP_TextElementType.Character && !char.IsWhiteSpace(c.character))
                { y = c.baseLine + c.pointSize * .25f; hasText = true; }
                i++;
            }
            if (right > left) nextStrokes.Add(new Rect(left, y - 1.15f, right - left, 2.3f));
        }
        bool changed = strokes.Count != nextStrokes.Count;
        for (int i = 0; !changed && i < strokes.Count; i++) changed = strokes[i] != nextStrokes[i];
        if (!changed) return;
        strokes.Clear(); strokes.AddRange(nextStrokes); SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        foreach (var r in strokes)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin, r.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMin), color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
