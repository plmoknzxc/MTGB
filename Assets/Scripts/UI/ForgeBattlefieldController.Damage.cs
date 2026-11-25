using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    long lastCombatEvent;
    readonly Dictionary<int,int> lastPlayerLife = new();
    readonly Dictionary<(int target, bool player, bool loss),long> damageAmounts = new();
    readonly List<BattlefieldDamageGraphic> damageEffects = new();
    RectTransform damageOverlay;
    float combatStrikeAge = float.PositiveInfinity;
    bool combatImpactPending;
    float CombatContactDelay => Mathf.Max(0, BattlefieldWorldCard.AttackContactDelay - combatStrikeAge);

    void AdvanceCombatFeedback()
    {
        if (combatStrikeAge >= BattlefieldWorldCard.AttackLungeDuration) return;
        combatStrikeAge += Mathf.Min(Time.unscaledDeltaTime, .05f);
        if (combatImpactPending && CombatContactDelay == 0) {
            combatImpactPending = false;
            GameAudio.Play(TableSound.Impact);
        }
    }

    void PresentCombatFeedback(Dictionary<int,ZonePose> current, Dictionary<string,RectTransform> surfaces)
    {
        if (!IsRedesignLayout || view?.players == null) return;
        damageAmounts.Clear();
        var events = view.combatFeedback;
        if (events != null && events.Length > 0 && events[events.Length-1].id > lastCombatEvent) {
            foreach (var e in events) {
                if (e.id <= lastCombatEvent) continue;
                lastCombatEvent = e.id;
                if (!hasPreviousZoneView) continue;
                switch (e.kind) {
                    case "draw": GameAudio.Play(TableSound.Draw); continue;
                    case "land": GameAudio.Play(TableSound.Land); continue;
                    case "block": GameAudio.Play(TableSound.Block); continue;
                }
                if (e.kind == "attack") continue;
                if (e.kind == "strike") {
                    if (!current.TryGetValue(e.attacker, out var attacker) || attacker.zone != "Battlefield" || attacker.size==Vector2.zero)
                        zonePoses.TryGetValue(e.attacker,out attacker);
                    if(attacker==null || attacker.zone!="Battlefield" || attacker.size==Vector2.zero)continue;
                    var face = ZoneSurface(attacker, surfaces);
                    var world = arenaCards.Values.FirstOrDefault(c => c.world!=null && ((face!=null && c.face==face) || c.card?.id==e.attacker))?.world;
                    if (world != null && CombatPoint(e.target, e.player, false, current, surfaces, out var to, out _)){
                        world.StartAttackLunge(to - attacker.position);
                        combatStrikeAge=0;
                        GameAudio.Play(TableSound.Attack);
                    }
                } else if (e.amount > 0 && (e.kind == "hit" || e.kind == "loss")) {
                    var key = (e.target, e.player, e.kind == "loss");
                    damageAmounts.TryGetValue(key, out long accumulated);
                    damageAmounts[key] = accumulated + e.amount;
                }
            }
        }

        foreach (var p in view.players) {
            var key = (p.targetId, true, true);
            if (hasPreviousZoneView && !damageAmounts.ContainsKey(key) && lastPlayerLife.TryGetValue(p.targetId, out int before) && before > p.life)
                damageAmounts[key] = (long)before - p.life;
            lastPlayerLife[p.targetId] = p.life;
        }
        if (hasPreviousZoneView && damageAmounts.Count > 0) {
            if(CombatContactDelay>0)combatImpactPending=true;
            else GameAudio.Play(TableSound.Impact);
        }
        foreach (var pair in damageAmounts) {
            var (target, player, loss) = pair.Key;
            if (!player && dyingCreatures.Contains(target)) continue;
            if (CombatPoint(target, player, loss, current, surfaces, out var position, out var anchor))
                PlayDamageEffect(position, anchor, target, player, loss, pair.Value);
        }
        foreach (var effect in damageEffects)
            if (effect.gameObject.activeSelf && !effect.Player && dyingCreatures.Contains(effect.Target))
                effect.gameObject.SetActive(false);
    }

    bool CombatPoint(int target, bool player, bool loss, Dictionary<int,ZonePose> current,
        Dictionary<string,RectTransform> surfaces, out Vector2 point, out RectTransform anchor)
    {
        point = default;
        anchor = null;
        if (player) {
            var plate = AttackEntity("player", target);
            if (plate == null || !plate.gameObject.activeInHierarchy) return false;
            anchor = plate.Find(loss ? "Life" : "Avatar") as RectTransform ?? plate;
            point = surface.InverseTransformPoint(anchor.TransformPoint(anchor.rect.center));
            return true;
        }
        // Lethal damage and moving to the graveyard can arrive in one snapshot.
        // In that case the impact belongs where the card was on the table.
        if (current.TryGetValue(target, out var pose) && pose.zone == "Battlefield" && pose.size != Vector2.zero)
            anchor = ZoneSurface(pose, surfaces);
        else
            zonePoses.TryGetValue(target, out pose);
        if (pose == null || pose.zone != "Battlefield" || pose.size == Vector2.zero) return false;
        point = pose.position;
        return true;
    }

    void PlayDamageEffect(Vector2 point, RectTransform anchor, int target, bool player, bool loss, long amount)
    {
        var effect = damageEffects.Find(e => e.gameObject.activeSelf && e.Target == target && e.Player == player && e.LifeLoss == loss)
            ?? damageEffects.Find(e => !e.gameObject.activeSelf);
        if (effect == null) {
            if (damageOverlay == null) {
                damageOverlay = Rect(surface, "CombatFeedback", Vector2.zero, Vector2.zero);
                damageOverlay.anchorMin = Vector2.zero;
                damageOverlay.anchorMax = Vector2.one;
                damageOverlay.offsetMin = damageOverlay.offsetMax = Vector2.zero;
                SetOverlayLayer(damageOverlay, NoticeLayerOffset-1);
            }
            var root = Rect(damageOverlay, "DamageFeedback", Vector2.zero, new Vector2(100,80));
            effect = root.gameObject.AddComponent<BattlefieldDamageGraphic>();
            var label = Label(root, "Amount", "", Vector2.zero, new Vector2(134,42), 27, Color.white);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 13;
            label.resizeTextMaxSize = 27;
            var outline = label.gameObject.GetComponent<Outline>() ?? label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.09f,.05f,.09f,.96f);
            outline.effectDistance = new Vector2(1.6f,-1.6f);
            effect.Initialize(label);
            damageEffects.Add(effect);
        }
        effect.Play(point, anchor, target, player, loss, amount,CombatContactDelay);
    }

}
