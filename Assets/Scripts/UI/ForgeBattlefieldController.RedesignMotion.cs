using System.Collections.Generic;
using UnityEngine;

public sealed partial class ForgeBattlefieldController
{
    readonly Dictionary<string,ArenaCard> redesignLeavingCards = new();
    readonly Dictionary<string,StackCardVisual> redesignLeavingStack = new();

    void ShowRedesignElement(RectTransform rect, Vector2 arrival, float opacity = 1, bool interactive = true, bool horizontal = true)
        => BattlefieldUiTransition.On(rect).Place(rect.anchoredPosition, rect.sizeDelta, opacity, arrival, interactive, horizontal);

    void HideRedesignElement(RectTransform rect, Vector2 departure)
    {
        if (rect != null) BattlefieldUiTransition.On(rect).Hide(departure);
    }

    bool RestoreRedesignCard(string key, out ArenaCard item)
    {
        if (!redesignLeavingCards.TryGetValue(key, out item)) return false;
        redesignLeavingCards.Remove(key); arenaCards.Add(key, item); return true;
    }

    void RetireRedesignCard(string key, ArenaCard item)
    {
        arenaCards.Remove(key); redesignLeavingCards[key] = item;
        item.pointer.ResetHover(); item.pointer.DisableActions(); item.button.interactable = false;
        var motion=BattlefieldUiTransition.On(item.root);motion.Rebase();
        motion.Hide(new Vector2(0, item.root.parent == otherHand ? 26 : -26), () => {
            if (redesignLeavingCards.TryGetValue(key, out var current) && current == item) {
                redesignLeavingCards.Remove(key); if (item.root != null) Destroy(item.root.gameObject);
            }
        });
    }

    bool RestoreRedesignStack(string key, out StackCardVisual item)
    {
        if (!redesignLeavingStack.TryGetValue(key, out item)) return false;
        redesignLeavingStack.Remove(key); stackCards.Add(key, item);
        item.row.SetParent(stackContent, true);
        BattlefieldUiTransition.On(item.row).Rebase();
        if (item.row.TryGetComponent<Canvas>(out var overlay)) overlay.overrideSorting = false;
        return true;
    }

    void RetireRedesignStack(string key, StackCardVisual item)
    {
        stackCards.Remove(key); redesignLeavingStack[key] = item;
        // Keep a resolving row visible while the live viewport closes/reflows.
        // This visual has no native action or selectable target after removal.
        item.row.SetParent(surface, true); SetOverlayLayer(item.row, 3);
        foreach (var pointer in item.row.GetComponentsInChildren<ForgeBattlefieldCardPointer>()) {
            pointer.ResetHover(); pointer.DisableActions();
        }
        var motion = BattlefieldUiTransition.On(item.row);
        motion.Rebase();
        motion.Place(item.row.anchoredPosition, item.row.sizeDelta, 1, Vector2.zero, false);
        motion.Hide(new Vector2(28, 0), () => {
            if (redesignLeavingStack.TryGetValue(key, out var current) && current == item) {
                redesignLeavingStack.Remove(key); if (item.row != null) Destroy(item.row.gameObject);
            }
        });
    }

    void ResetRedesignTransitions()
    {
        foreach (var item in redesignLeavingCards.Values) if (item.root != null) Destroy(item.root.gameObject);
        foreach (var item in redesignLeavingStack.Values) if (item.row != null) Destroy(item.row.gameObject);
        redesignLeavingCards.Clear(); redesignLeavingStack.Clear();
    }
}
