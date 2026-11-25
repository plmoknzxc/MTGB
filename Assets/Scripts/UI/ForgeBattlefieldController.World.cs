using System;
using System.Linq;
using UnityEngine;

public sealed partial class ForgeBattlefieldController
{
    BattlefieldWorldTable worldTable;
    bool worldActionHintsBlocked;
    void UpdateWorldActionHintVisibility()
    {
        bool blocked=viewingBattlefield||GameSettingsPanel.IsOpen;
        if(worldActionHintsBlocked==blocked)return;
        worldActionHintsBlocked=blocked;
        if(blocked)ClearWorldActionHints();
        else if(integrated&&view!=null)PresentIntegrated(connectionText.text);
    }
    BattlefieldWorldActionHint WorldActionHint(ForgeCardView card,bool hand,bool selected,bool playableHand)
    {
        if(card==null||!canAct||submitted||viewingBattlefield||GameSettingsPanel.IsOpen
            ||view?.awaitingInput!=true||view.request==null||view.request.seat!=seat)return BattlefieldWorldActionHint.None;
        if(selected && (!hand || !card.faceDown))return BattlefieldWorldActionHint.Selected;
        if(hand)return playableHand?BattlefieldWorldActionHint.HandAvailable:BattlefieldWorldActionHint.None;
        if(card.zone!="Battlefield")return BattlefieldWorldActionHint.None;
        var options=view.request.options??Array.Empty<ForgeOption>();
        if(view.request.kind=="InputAttack"&&!selected&&!card.attacking&&options.Any(o=>!o.disabled&&o.attackerId==card.id&&o.id?.StartsWith("attack:",StringComparison.Ordinal)==true))
            return BattlefieldWorldActionHint.Attack;
        if((view.request.kind=="InputPassPriority"||view.request.kind?.StartsWith("InputPayMana",StringComparison.Ordinal)==true)
            &&options.Any(o=>o.id=="card:"+card.id&&!o.disabled&&!o.requiresPaymentCheck))return BattlefieldWorldActionHint.Activate;
        return BattlefieldWorldActionHint.None;
    }
    internal static string SicknessBadge=>GamePreferences.Language=="zh-CN"?"失调":"Zzz";
    void BindWorldCard(ArenaCard item,ForgeCardView card,int count,bool hand,int order,bool selected,bool actionable,BattlefieldWorldActionHint actionHint,BattlefieldExilePreview exile)
    {
        TryBattlefieldArt(card,out var art);
        Color accent=selected?ForgePlayableGlow.ChosenLight:card.attacking?new Color(1,.4f,.23f):!hand&&actionable?new Color(.31f,.78f,.88f):new Color(.25f,.29f,.30f);
        if(card.land&&!hand&&!selected&&!actionable)RedesignLandPalette(card,out accent,out _);
        var attributes=AttributeStateLabels(card).ToList();
        if(card.hasSicknessState&&card.summoningSick){attributes.Remove(TapPaymentText("召唤失调","Summoning sickness","召喚酔い"));attributes.Insert(0,SicknessBadge);}
        if(card.detained)attributes.Add(TapPaymentText("拘留","Detained","留置"));
        // Marked damage is shown on the battlefield toughness digit. The
        // inspector still reports native toughness and damage separately.
        if(card.ringBearer)attributes.Add(TapPaymentText("持戒人","Ring-bearer","指輪所持者"));
        if(card.pendingDepartures?.Length>0)attributes.Add(TapPaymentText("延迟触发","Delayed","遅延誘発"));
        foreach(var counter in card.counters??Array.Empty<ForgeCardCounter>()) {
            // Loyalty and defense already occupy the card's numeric shield.
            if(card.planeswalker&&string.Equals(counter.kind,"LOYALTY",StringComparison.OrdinalIgnoreCase)
                || card.battle&&string.Equals(counter.kind,"DEFENSE",StringComparison.OrdinalIgnoreCase))continue;
            attributes.Add(PermanentCounterName(counter)+" "+counter.count);
        }
        var costs=WorldCosts(card);
        item.world.Bind(card,art,hand,count,order,accent,costs.main,costs.secondary,string.Join(" · ",attributes.Distinct()),costs,
            selected||card.attacking||actionable&&actionHint==BattlefieldWorldActionHint.None,actionHint,exile:exile);
    }
    void ClearWorldActionHints()
    {
        foreach(var item in arenaCards.Values){
            if(item.world==null||item.world.ActionHint==BattlefieldWorldActionHint.None)continue;
            item.world.ClearActionHint();item.appearance=null;
        }
    }
    BattlefieldWorldCosts WorldCosts(ForgeCardView card)
    {
        var result=new BattlefieldWorldCosts{main=card.previewManaCost??card.manaCost};
        var definition=ComponentDefinition(card,false);
        if(definition?.features==null||card.faceDown)return result;
        var f=definition.features.FirstOrDefault(x=>x.cardName==card.name&&x.faceIndex==(card.backFace?1:0))
            ??definition.features.FirstOrDefault(x=>x.faceIndex==(card.backFace?1:0));
        var primary=definition.features.FirstOrDefault(x=>x.faceIndex==0&&x.featureIndex==0)??f;
        var secondary=definition.features.FirstOrDefault(x=>x.faceIndex==1&&x.featureIndex==0)
            ??definition.features.FirstOrDefault(x=>x.faceIndex==0&&x.featureIndex>0);
        bool pair=secondary!=null&&new[]{"Adventure","Omen","Modal","Transform","Split","Room"}.Contains(definition.forgeSplitType);
        string Cost(CardFeatureDefinition feature){
            if(feature==null)return null;
            var offered=(view?.request?.options??Array.Empty<ForgeOption>()).Select(x=>x.castingFace).FirstOrDefault(x=>x!=null&&x.id==card.id&&x.name==feature.cardName);
            if(offered?.previewManaCost!=null)return offered.previewManaCost;
            if(feature==secondary&&card.secondaryPreviewManaCost!=null)return card.secondaryPreviewManaCost;
            if(feature==secondary&&definition.forgeSplitType=="Adventure"&&card.adventurePreviewManaCost!=null)return card.adventurePreviewManaCost;
            if(feature==f&&(!pair||card.name==feature.cardName)&&card.previewManaCost!=null)return card.previewManaCost;
            return feature==f&&(!pair||card.name==feature.cardName)?card.manaCost??feature.manaCost:feature.manaCost;
        }
        bool inset=definition.forgeSplitType is "Adventure" or "Omen";
        result.rotated=definition.forgeSplitType is "Split" or "Room";
        result.printed=result.rotated||inset?primary?.manaCost:f?.manaCost??definition.manaCost;
        result.main=Cost(pair?primary:f);
        result.secondary=pair?Cost(secondary):null;
        result.printedSecondary=pair&&(inset||result.rotated)?secondary.manaCost:null;
        result.highTitle=((f?.cardTypes??definition.cardTypes)&CardType.Planeswalker)!=0;
        result.printedIsSecondary=pair&&!inset&&!result.rotated&&f==secondary;
        if(card.battle||definition.forgeSplitType=="Aftermath")result.printed=result.printedSecondary=null;
        return result;
    }
}
