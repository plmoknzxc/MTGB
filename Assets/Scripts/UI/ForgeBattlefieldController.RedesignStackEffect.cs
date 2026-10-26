using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private static ForgeCardView RedesignRulesOrigin(ForgeStackItem entry)
    {
        // JsonUtility can create an empty reference object for absent fields in
        // an older recording; a non-null object alone is not source provenance.
        var origin = entry.rulesSource;
        return origin != null && (origin.faceDown || !string.IsNullOrWhiteSpace(origin.name)
            || !string.IsNullOrWhiteSpace(origin.originalName) || !string.IsNullOrWhiteSpace(origin.printingKey)
            || !string.IsNullOrWhiteSpace(origin.originalRules) || !string.IsNullOrWhiteSpace(origin.currentRules)) ? origin : entry.source;
    }
    private string BindPublicStackName(ForgeCardView origin, string text)
    {
        string name = origin?.faceDown != true ? (!string.IsNullOrWhiteSpace(origin?.name) ? origin.name : origin?.originalName) : null;
        return (text ?? "").Replace("CARDNAME", !string.IsNullOrWhiteSpace(name) ? GamePreferences.CardFaceName(name)
            : TapPaymentText("此物件", "this object", "このオブジェクト"));
    }

    private static string StackEffectKey(string text) => Regex.Replace((text ?? "").Replace("\\n", "\n"), @"\s+", " ").Trim();
    private static string StackModeBody(string text)
    {
        text = (text ?? "").Trim();
        text = Regex.Replace(text, @"^(?:\{(?:P|oH+)\}\s*)+[—―－〜～~-]\s*", "");
        text = Regex.Replace(text, @"^[+＋]\s*(?:\{[^}]+\}\s*)+\s*[—－〜～~-]\s*", "");
        return Regex.Replace(text, @"^[•・—]\s*", "");
    }

    private string RedesignChosenModeText(ForgeCardView origin, ForgeChosenMode mode)
    {
        // Match the complete public mode, not an ordinal into a possibly changed
        // printing. New/modified modes retain their supplied text on a miss.
        string native = mode.description ?? "";
        string Bound(string value) => origin?.faceDown != true && !string.IsNullOrWhiteSpace(origin?.name) ? value.Replace("CARDNAME", origin.name) : BindPublicStackName(origin, value);
        if (origin != null && !origin.faceDown)
            foreach (string line in (string.IsNullOrWhiteSpace(origin.originalRules) ? origin.currentRules ?? "" : origin.originalRules).Replace("\\n", "\n").Split('\n'))
                if (StackEffectKey(StackModeBody(Bound(line))) == StackEffectKey(StackModeBody(Bound(native))))
                    return BindPublicStackName(origin, StackModeBody(PrintedRuleText(origin, line)));
        string bound = Bound(native), translated = PrintedRuleText(origin, bound);
        return BindPublicStackName(origin, translated == bound ? GamePreferences.NativeText(bound) : translated);
    }

    private string RedesignStackEffect(ForgeStackItem entry, string fallback)
    {
        if (entry == null) return "";
        if (entry.source?.faceDown == true)
            return entry.kind == "spell" ? TapPaymentText("面朝下的咒语", "Face-down spell", "裏向きの呪文")
                : TapPaymentText("面朝下来源的异能", "Ability from a face-down source", "裏向きの発生源の能力");
        var metadata = new List<string>();
        if (entry.hasXValue) metadata.Add("X = " + entry.xValue);
        if (entry.copied) metadata.Add(TapPaymentText("复制品", "Copy", "コピー"));
        foreach (var choice in entry.castingChoices ?? Array.Empty<ForgeCastingChoice>()) {
            if (choice == null || string.IsNullOrWhiteSpace(choice.kind)) continue;
            string label = CastingChoiceName(choice.kind);
            if (choice.kind == "CASUALTY") label += " " + choice.count;
            else if (choice.kind == "HARMONIZE") label += " (" + TapPaymentText("力量", "power", "パワー") + " " + choice.count + ")";
            else if (choice.count != 1) label += " (" + choice.count + ")";
            metadata.Add(label);
        }
        // Visibility is a presentation choice, independent of whether a recording
        // includes the newer payment receipt. Missing fields never restore rules prose.
        var payment=entry.hasPaymentReceipt?entry.payment:null;
        if(ManaPaymentAffectsRules(entry.source)&&!string.IsNullOrWhiteSpace(payment?.spentMana))metadata.Add(TapPaymentText("实付：","Spent: ","支払済み：")+payment.spentMana);
        if(payment?.lifePaid>0)metadata.Add(TapPaymentText("支付生命：","Life paid: ","支払ったライフ：")+payment.lifePaid);
        foreach(var mode in entry.chosenModes??Array.Empty<ForgeChosenMode>())
            if(mode!=null)metadata.Add(TapPaymentText("所选模式","Chosen mode","選んだモード")+(mode.order>0?" "+mode.order:"")+(string.IsNullOrWhiteSpace(mode.cost)?"":" · "+RedesignPaymentCostText(entry.source,mode.cost)));
        return PlayerNamesRichText(string.Join("\n",metadata));
    }

    private string RedesignPaymentCostText(ForgeCardView source,string value)
    {
        string translated=PrintedCostText(source,value);
        if(translated!=value)return translated;
        return string.Join(", ",value.Split(new[]{", "},StringSplitOptions.None).Select(part=>part=="Gift something"?CastingChoiceName("PromiseGift"):GamePreferences.NativeText(CostOrderLabel(part))));
    }

    private readonly Dictionary<string, string> redesignStackDetailKeys = new();

    private ScrollRect PrepareRedesignStackDetails(RectTransform row)
    {
        var viewport = row.Find("CompactDetails") as RectTransform;
        if (viewport == null) {
            viewport = Panel(row, "CompactDetails", Vector2.zero, Vector2.one, Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();
            var createdScroll = viewport.gameObject.AddComponent<ScrollRect>();
            createdScroll.viewport = viewport;
            createdScroll.content = Rect(viewport, "Content", Vector2.zero, Vector2.one);
            createdScroll.horizontal = false; createdScroll.movementType = ScrollRect.MovementType.Clamped;
            createdScroll.scrollSensitivity = 32;
        }
        return viewport.GetComponent<ScrollRect>();
    }

    private float LayoutRedesignStackEffect(RectTransform parent, ForgeStackItem entry, string fallback, float top, float width)
    {
        string value = RedesignStackEffect(entry, fallback);
        var source = RedesignStackText(parent, "CompactNote", value, 18, new Color(.75f, .79f, .75f), 0, top, width);
        source.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
        if(!source.gameObject.activeSelf)return 0;
        source.supportRichText = true;
        source.GetComponent<PreferenceText>().Literal = true;
        var text = SymbolizeStackLabel(source); text.raycastTarget = false;
        text.overflowMode = TMPro.TextOverflowModes.Overflow;
        float fullHeight = Mathf.Ceil(text.GetPreferredValues(text.text, width, Mathf.Infinity).y) + 4;
        source.rectTransform.sizeDelta = new Vector2(width, fullHeight);
        return fullHeight;
    }

    private void LayoutRedesignStackDetails(ScrollRect scroll, string identity, float left, float top, float width, float fullHeight,float visibleHeight)
    {
        var viewport = scroll.viewport;
        var content = scroll.content;
        float previousOffset = content.anchoredPosition.y;
        // Short entries fit their content/artwork; long information scrolls
        // inside the bounded row while artwork remains outside the viewport.
        string key = string.Join("\n", content.GetComponentsInChildren<Text>(true).Where(t => t.gameObject.activeSelf).Select(t => t.text));
        bool same = redesignStackDetailKeys.TryGetValue(identity, out string previous) && previous == key;
        redesignStackDetailKeys[identity] = key;
        RedesignPlace(content, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(width - 12, fullHeight));
        RedesignPlace(viewport, new Vector2(0, 1), new Vector2(0, 1), new Vector2(left, -top), new Vector2(width, visibleHeight));
        bool overflow = fullHeight > visibleHeight + 1;
        scroll.enabled = overflow; scroll.StopMovement();
        viewport.GetComponent<Image>().raycastTarget = overflow;
        content.anchoredPosition = new Vector2(0, same ? Mathf.Clamp(previousOffset, 0, Mathf.Max(0, fullHeight - visibleHeight)) : 0);
        if (scroll.verticalScrollbar == null) AddStackScrollIndicator(content);
        scroll.verticalScrollbar.gameObject.SetActive(overflow);
        var track = (RectTransform)scroll.verticalScrollbar.transform;
        RedesignPlace(track, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(4, visibleHeight));
    }
}
