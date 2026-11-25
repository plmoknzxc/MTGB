using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    static readonly Color AttackInk = new Color(1f,.60f,.31f);
    static readonly Color BlockInk = new Color(.40f,.81f,1f);
    static readonly Color TargetInk = new Color(.52f,.93f,.94f);
    readonly List<BattlefieldChoicePulse> choicePulses = new();
    readonly List<RaycastResult> choiceHits = new();
    EventSystem choiceEventSystem;
    PointerEventData choicePointer;
    int nextPulse;

    Button ChoiceButtonAt(Vector2 position)
    {
        var system = EventSystem.current;
        if (system == null) return null;
        if (choiceEventSystem != system)
        {
            choiceEventSystem = system;
            choicePointer = new PointerEventData(system);
        }
        choicePointer.position = position;
        system.RaycastAll(choicePointer, choiceHits);
        var button = choiceHits.Count == 0 ? null : choiceHits[0].gameObject.GetComponentInParent<Button>();
        choiceHits.Clear();
        return button != null && button.IsInteractable() ? button : null;
    }

    void PulseChoice(RectTransform target, Color tint, bool release = false)
    {
        if (!IsRedesignLayout || target == null || !target.gameObject.activeInHierarchy) return;
        var mark = choicePulses.Find(p => !p.gameObject.activeSelf);
        if (mark == null && choicePulses.Count < 6)
        {
            var root = Rect(surface, "ChoiceContact", Vector2.zero, Vector2.one * 68);
            mark = root.gameObject.AddComponent<BattlefieldChoicePulse>();
            mark.raycastTarget = false;
            choicePulses.Add(mark);
        }
        if (mark == null) mark = choicePulses[nextPulse++ % choicePulses.Count];
        SetOverlayLayer(mark.rectTransform, DragArrowLayerOffset);
        mark.Play(surface.InverseTransformPoint(target.TransformPoint(target.rect.center)), tint, release);
    }

    RectTransform TargetEntity(string id)
    {
        int split = id.IndexOf(':');
        if (split < 0 || !int.TryParse(id.Substring(split+1), out int number)) return null;
        if (id.StartsWith("stack:")) return stackCards.TryGetValue("id:"+number, out var row) ? row.tile : null;
        return id.StartsWith("card:") || id.StartsWith("player:") ? AttackEntity(id.Substring(0,split), number) : null;
    }

    void ShowSelectedPlayer(RectTransform plate, int option)
    {
        bool selected = IsRedesignLayout && IsTargeting && option >= 0 && view.request.options[option].selected;
        var frame = plate.Find("ChosenTarget") as RectTransform;
        if (frame == null && selected)
        {
            frame = Rect(plate,"ChosenTarget",Vector2.zero,Vector2.zero);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = frame.offsetMax = Vector2.zero;
            var edge = frame.gameObject.AddComponent<ForgePlayableGlow>();
            edge.selected = edge.SheetFrame = true;
            edge.color = TargetInk;
            edge.raycastTarget = false;
        }
        if (frame != null) frame.gameObject.SetActive(selected);
    }

    void ShowSubmittedContact(ForgeOption option)
    {
        if (!IsRedesignLayout) return;
        if ((IsDeclaringAttack && option.id.StartsWith("attack-remove:")) || (IsBlockingChoice && option.id.StartsWith("block-remove:")))
        {
            if (int.TryParse(option.id.Substring(option.id.IndexOf(':')+1), out int id))
                PulseChoice(AttackEntity("card",id), IsDeclaringAttack ? AttackInk : BlockInk,true);
        }
        else if (IsBlockingChoice && option.id.StartsWith("block:"))
            PulseChoice(AttackEntity("card",option.attackerId), BlockInk,option.selected);
        else if (IsDeclaringAttack && option.id.StartsWith("attack:"))
            PulseChoice(AttackEntity(option.defenderKind,option.defenderId), AttackInk);
        else if (IsTargeting && !UsesTargetWindow)
        {
            if (option.id is "cancel" or "reset-targets")
            {
                foreach (var entry in view.request.options.Where(o => o.selected))
                    PulseChoice(TargetEntity(entry.id),TargetInk,true);
            }
            else PulseChoice(TargetEntity(option.id),TargetInk,option.selected);
        }
    }

    // Match by both ends. Removing the first of several blockers must never
    // animate its arrow across the board to impersonate a surviving link.
    sealed class CombatLink
    {
        public (int source, string kind, int target) key;
        public ForgeTargetArrow arrow;
        public bool seen;
    }

    void ShowCombatLink(List<CombatLink> links, string prefix, int source, string kind, int target, Color tint)
    {
        var from = AttackEntity("card",source);
        var to = AttackEntity(kind,target);
        if (from == null || to == null || !from.gameObject.activeInHierarchy || !to.gameObject.activeInHierarchy) return;
        var key = (source,kind,target);
        var line = links.Find(l => l.key == key);
        if (line == null)
        {
            line = links.Find(l => !l.arrow.gameObject.activeSelf);
            if (line == null)
            {
                line = new CombatLink { arrow = NewAttackArrow(prefix + links.Count) };
                links.Add(line);
            }
            line.key = key;
        }
        line.seen = true;
        line.arrow.SetVisible(true);
        line.arrow.transform.SetSiblingIndex(cardsRoot.GetSiblingIndex());
        var sourceFace = worldTable==null ? null : arenaCards.Values.FirstOrDefault(c=>c.root==from)?.world;
        var targetFace = kind!="card" || worldTable==null ? null : arenaCards.Values.FirstOrDefault(c=>c.root==to)?.world;
        line.arrow.FollowCombat(from,sourceFace,to,targetFace,tint);
    }

    static void FinishCombatLinks(List<CombatLink> links)
    {
        foreach (var line in links)
        {
            if (!line.seen) line.arrow.SetVisible(false);
            line.seen = false;
        }
    }

    void ResetCombatMotion()
    {
        lastCombatEvent = 0;
        combatStrikeAge=float.PositiveInfinity;
        combatImpactPending=false;
        lastPlayerLife.Clear();
        foreach (var effect in damageEffects) if (effect != null) effect.gameObject.SetActive(false);
        foreach (var line in assignedAttackArrows) if (line.arrow != null) line.arrow.gameObject.SetActive(false);
        foreach (var line in blockLines) if (line.arrow != null) line.arrow.gameObject.SetActive(false);
        foreach (var mark in choicePulses) if (mark != null) mark.gameObject.SetActive(false);
        if (attackArrow != null) attackArrow.gameObject.SetActive(false);
        foreach (var arrow in extraAttackArrows) if (arrow != null) arrow.gameObject.SetActive(false);
        if (blockArrow != null) blockArrow.gameObject.SetActive(false);
        if (targetArrow != null) targetArrow.gameObject.SetActive(false);
        ClearAttackSelection();
        selectedBlocker = -1;
    }
}
