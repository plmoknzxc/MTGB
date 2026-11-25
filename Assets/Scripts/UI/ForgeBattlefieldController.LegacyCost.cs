using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private bool HasLegacyCost(ForgeCardView card)
    {
        if(GamePreferences.UseComponentCardFaces||card==null||card.faceDown||card.emblem||card.effectMarker
            ||card.zone is not ("Hand" or "Graveyard" or "Exile" or "Sideboard"))return false;
        var d=ComponentDefinition(card,false);
        return d!=null&&!card.battle&&d.forgeSplitType!="Aftermath"
            &&(d.forgeSplitType!="Split"||card.room||d.features.Any(f=>(f.subtypes??"").Split(' ').Contains("Room")))
            &&((card.previewManaCost??card.manaCost??d.manaCost??"").Contains('{')
                ||d.features.Any(f=>(f.manaCost??"").Contains('{')));
    }
    private (string printed,string current,string printedSecond,string second,bool highTitle,bool primaryFirst,bool pair,bool rotated,bool back) LegacyCosts(ForgeCardView card)
    {
        var d=ComponentDefinition(card,false);
        var f=d.features?.FirstOrDefault(x=>x.cardName==card.name&&x.faceIndex==(card.backFace?1:0))
            ??d.features?.FirstOrDefault(x=>x.faceIndex==(card.backFace?1:0));
        var primary=d.features.FirstOrDefault(x=>x.faceIndex==0&&x.featureIndex==0)??f;
        var secondary=d.features.FirstOrDefault(x=>x.faceIndex==1&&x.featureIndex==0)
            ??d.features.FirstOrDefault(x=>x.faceIndex==0&&x.featureIndex>0);
        bool pair=secondary!=null&&new[]{"Adventure","Omen","Modal","Transform","Split","Room"}.Contains(d.forgeSplitType);
        bool permitted=card.zone=="Hand"||card.zone=="Graveyard"&&(view?.playableGraveyard??Array.Empty<ForgeCardView>()).Any(x=>x.id==card.id)
            ||card.zone=="Exile"&&(view?.playableExile??Array.Empty<ForgeCardView>()).Any(x=>x.id==card.id)
            ||card.zone=="Sideboard"&&card.previewManaCost!=null;
        string Cost(CardFeatureDefinition feature){
            if(feature==null)return "";
            if(permitted){
                var offered=(view?.request?.options??Array.Empty<ForgeOption>()).Select(x=>x.castingFace)
                    .FirstOrDefault(x=>x!=null&&x.id==card.id&&x.name==feature.cardName);
                if(offered?.previewManaCost!=null)return offered.previewManaCost;
                if(feature==secondary&&card.secondaryPreviewManaCost!=null)return card.secondaryPreviewManaCost;
                if(feature==secondary&&d.forgeSplitType=="Adventure"&&card.adventurePreviewManaCost!=null)return card.adventurePreviewManaCost;
                if(feature==f&&(!pair||card.name==feature.cardName)&&card.previewManaCost!=null)return card.previewManaCost;
            }
            return feature==f&&(!pair||card.name==feature.cardName)?card.manaCost??feature.manaCost:feature.manaCost;
        }
        bool inset=d.forgeSplitType is "Adventure" or "Omen";
        bool rotated=d.forgeSplitType is "Split" or "Room";
        string printed=inset?primary?.manaCost:f?.manaCost??d.manaCost;
        return (rotated?primary?.manaCost:printed,Cost(pair?primary:f),
            pair&&(inset||rotated)?secondary.manaCost:null,pair?Cost(secondary):null,
            ((f?.cardTypes??d.cardTypes)&CardType.Planeswalker)!=0,!inset,pair,rotated,pair&&!inset&&!rotated&&f==secondary);
    }

    private string PickerAuthoritySignature()
    {
        var options=view?.request?.options??Array.Empty<ForgeOption>();
        return string.Join(",",(view?.playableExile??Array.Empty<ForgeCardView>()).Select(c=>c.id))+"/"+
            string.Join(",",(view?.playableGraveyard??Array.Empty<ForgeCardView>()).Select(c=>c.id))+"/"+
            string.Join("|",options.Where(o=>o.castingFace!=null).Select(o=>o.castingFace.id+":"+o.castingFace.name+":"+o.castingFace.previewManaCost));
    }
    private string PickerCostText(ForgeCardView card)
    {
        if(!HasLegacyCost(card))return "";
        var cost=LegacyCosts(card);
        string Shown(string text)=>string.IsNullOrWhiteSpace(text)||text=="no cost"?"—":text;
        if(cost.pair)return Shown(cost.current)+" / "+Shown(cost.second);
        return LegacyCardCost.SameCost(cost.printed,cost.current)?"":Shown(cost.current);
    }

    private void PresentLegacyCost(RectTransform parent,ForgeCardView card,float clearance=2)
    {
        var existing=parent.GetComponent<LegacyCardCost>();
        var image=parent.GetComponent<Image>();
        if(!HasLegacyCost(card)||image==null||image.sprite==null||image.color.a==0){if(existing!=null)existing.Hide();return;}
        var cost=LegacyCosts(card);
        if(existing==null)existing=parent.gameObject.AddComponent<LegacyCardCost>();
        existing.Clearance=clearance;
        existing.Bind(image.sprite,cost.printed,cost.current,cost.printedSecond,cost.second,cost.highTitle,cost.primaryFirst,cost.pair,cost.rotated,cost.back);
    }
}
