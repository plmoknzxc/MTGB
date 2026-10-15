using UnityEngine;
using UnityEngine.UI;

// A geometric chevron keeps the same shape in every font and language.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class DropdownChevron : MaskableGraphic
{
    public static void Apply(Dropdown dropdown)
    {
        StylePopup(dropdown);
        var old = dropdown.transform.Find("Arrow");
        if (old != null) old.gameObject.SetActive(false);
        var existing = dropdown.transform.Find("DropdownChevron");
        var icon = existing != null ? existing.GetComponent<DropdownChevron>()
            : new GameObject("DropdownChevron", typeof(RectTransform), typeof(DropdownChevron)).GetComponent<DropdownChevron>();
        icon.transform.SetParent(dropdown.transform, false);
        if (icon.GetComponent<CanvasRenderer>() == null) icon.gameObject.AddComponent<CanvasRenderer>();
        icon.raycastTarget = false;
        icon.color = new Color(.69f, .75f, .83f, 1);
        var rect = icon.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(16, 16);
        rect.anchoredPosition = new Vector2(-18, 0);
    }

    static void StylePopup(Dropdown dropdown)
    {
        ClipCaption(dropdown.captionText);
        SingleLine(dropdown.itemText);
        if(dropdown.template==null)return;
        var template=dropdown.template;
        template.anchorMin=new Vector2(0,0);template.anchorMax=new Vector2(1,0);
        template.pivot=new Vector2(.5f,1);template.anchoredPosition=new Vector2(0,-6);
        var background=template.GetComponent<Image>()??template.gameObject.AddComponent<Image>();
        background.color=new Color(.16f,.22f,.30f,1);
        var outline=template.GetComponent<Outline>()??template.gameObject.AddComponent<Outline>();
        outline.effectColor=new Color(.43f,.59f,.79f,1);outline.effectDistance=new Vector2(2,-2);
        var shadow=System.Array.Find(template.GetComponents<Shadow>(),s=>s.GetType()==typeof(Shadow))??template.gameObject.AddComponent<Shadow>();
        shadow.effectColor=new Color(0,0,0,.75f);shadow.effectDistance=new Vector2(4,-5);
        foreach(var toggle in template.GetComponentsInChildren<Toggle>(true))
        {
            // Fixed-height rows must clip long names instead of wrapping into
            // the next option. The full option text/value remains unchanged.
            if(toggle.GetComponent<RectMask2D>()==null)toggle.gameObject.AddComponent<RectMask2D>();
            foreach(var text in toggle.GetComponentsInChildren<Text>(true))SingleLine(text);
            var image=toggle.targetGraphic as Image;
            if(image!=null)image.color=Color.white;
            var colors=toggle.colors;
            colors.normalColor=new Color(.16f,.22f,.30f,1);
            colors.highlightedColor=new Color(.27f,.40f,.57f,1);
            colors.selectedColor=new Color(.23f,.36f,.54f,1);
            colors.pressedColor=new Color(.32f,.48f,.69f,1);
            colors.fadeDuration=.08f;toggle.colors=colors;
            if(toggle.graphic!=null)toggle.graphic.color=MenuVisualTheme.Accent;
        }
    }

    static void SingleLine(Text text)
    {
        if(text==null)return;
        text.horizontalOverflow=HorizontalWrapMode.Overflow;
        text.verticalOverflow=VerticalWrapMode.Truncate;
        // Popup rows are reparented outside the Dropdown when cloned, so the
        // preference binder cannot infer that they still contain literal values.
        var binding=text.GetComponent<PreferenceText>()??text.gameObject.AddComponent<PreferenceText>();
        binding.Literal=true;binding.KeepVerticalBounds=true;binding.FitSingleLineHeight=true;binding.Apply();
    }

    static void ClipCaption(Text text)
    {
        if(text==null)return;
        SingleLine(text);
        var rect=text.rectTransform;
        var clip=rect.parent.name=="DropdownCaptionClip"?rect.parent as RectTransform:null;
        if(clip==null)
        {
            clip=new GameObject("DropdownCaptionClip",typeof(RectTransform),typeof(RectMask2D)).GetComponent<RectTransform>();
            clip.SetParent(rect.parent,false);
            clip.SetSiblingIndex(rect.GetSiblingIndex());
            clip.anchorMin=rect.anchorMin;clip.anchorMax=rect.anchorMax;clip.pivot=rect.pivot;
            clip.sizeDelta=rect.sizeDelta;clip.anchoredPosition=rect.anchoredPosition;
            rect.SetParent(clip,false);
        }
        else if(rect.anchorMin!=Vector2.zero||rect.anchorMax!=Vector2.one||rect.offsetMin!=Vector2.zero||rect.offsetMax!=Vector2.zero)
        {
            // Theme/layout refreshes still address the original Text reference.
            // Apply their new insets to the clipping frame, without nesting them.
            clip.anchorMin=rect.anchorMin;clip.anchorMax=rect.anchorMax;clip.pivot=rect.pivot;
            clip.sizeDelta=rect.sizeDelta;clip.anchoredPosition=rect.anchoredPosition;
        }
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
        rect.offsetMin=rect.offsetMax=Vector2.zero;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var center = rectTransform.rect.center;
        var points = new[] { new Vector2(-6, 2), new Vector2(-4.5f, 3.5f), new Vector2(0, -1),
            new Vector2(0, -4), new Vector2(4.5f, 3.5f), new Vector2(6, 2) };
        foreach (var point in points) mesh.AddVert(center + point, color, Vector2.zero);
        mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(0, 2, 3);
        mesh.AddTriangle(3, 2, 4); mesh.AddTriangle(3, 4, 5);
    }
}
