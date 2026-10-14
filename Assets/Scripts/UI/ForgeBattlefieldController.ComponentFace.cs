using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private bool UsesComponentFace(ForgeCardView card)
    {
        if(!GamePreferences.UseComponentCardFaces||card==null||card.faceDown||card.emblem||card.effectMarker)return false;
        var definition=ComponentDefinition(card,false);if(definition==null)return false;
        // Preserve independent orientation and mode contracts until their exact
        // templates are supported. The legacy renderer remains fully available.
        if(card.room||card.battle&&!card.backFace||definition.HasType(CardType.Battle)&&!card.backFace)return false;
        if(new[]{"Split","Aftermath","Room"}.Contains(definition.forgeSplitType))return false;
        var entry=view?.stackItems?.FirstOrDefault(e=>e.source?.id==card.id);
        if(entry!=null&&(HasSpellChoices(entry)||entry.hasXValue))return false;
        return true;
    }
    private CardDefinition ComponentDefinition(ForgeCardView card,bool original)
    {
        if(card==null)return null;
        string key=!original&&card.copy&&!string.IsNullOrEmpty(card.currentArtPrintingKey)?card.currentArtPrintingKey:card.printingKey;
        if(!string.IsNullOrEmpty(key)&&key.EndsWith("/back",StringComparison.Ordinal))key=key.Substring(0,key.Length-5);
        catalog.TryGetValue(key??"",out var definition);return definition;
    }
    private bool PresentComponentFace(RectTransform parent,ForgeCardView card,bool compact=false,bool original=false)
    {
        var existing=parent.Find("ComponentFace");
        if(!UsesComponentFace(card)) {
            if(existing!=null){existing.gameObject.SetActive(false);var scan=parent.GetComponent<Image>();if(scan!=null)scan.color=scan.sprite!=null?Color.white:Ink;}
            return false;
        }
        var definition=ComponentDefinition(card,original);
        var feature=definition.features?.FirstOrDefault(f=>f.cardName==card.name&&f.faceIndex==(card.backFace&&!original?1:0))
            ??definition.features?.FirstOrDefault(f=>f.faceIndex==(card.backFace&&!original?1:0));
        string nativeName=original?feature?.cardName??definition.cardName:card.name??feature?.cardName??definition.cardName;
        bool secondary=!original&&feature!=null&&feature.cardName!=definition.cardName&&feature.faceIndex==0;
        var adventure=!secondary&&definition.forgeSplitType=="Adventure"?definition.features?.FirstOrDefault(f=>f.faceIndex==0&&f.cardName!=definition.cardName):null;
        string printed=feature?.manaCost??definition.manaCost;
        string intrinsic=original?printed:card.manaCost??printed;
        bool permitted=card.zone=="Hand"||card.zone=="Graveyard"&&(view?.playableGraveyard??Array.Empty<ForgeCardView>()).Any(c=>c.id==card.id)
            ||card.zone=="Exile"&&(view?.playableExile??Array.Empty<ForgeCardView>()).Any(c=>c.id==card.id)||card.zone=="Library"&&OptionIndex("card:"+card.id)>=0;
        string cost=original?printed:card.zone=="Stack"?card.castingManaCost??intrinsic:permitted?card.previewManaCost??intrinsic:intrinsic;
        if(string.Equals(cost,"no cost",StringComparison.OrdinalIgnoreCase))cost="";
        string type=feature!=null?GamePreferences.FeatureType(feature,card.currentType):GamePreferences.CardType(definition);
        string rules=feature!=null?GamePreferences.FeatureRules(feature):GamePreferences.CardRules(definition);
        string title=GamePreferences.CardFaceName(nativeName);
        if(!original){
            if(card.originalType!=null&&card.currentType!=null&&card.originalType!=card.currentType)type=ChangedTypeText(card.originalType,card.currentType);
            if(card.hasAbilityChanges)rules=LocalizedChangedRules(card,card.originalAbilityText,card.currentAbilityText);
            else if(card.originalRules!=null&&card.currentRules!=null&&card.originalRules!=card.currentRules)rules=LocalizedChangedRules(card,card.originalRules,card.currentRules);
            if(card.originalName!=null&&card.name!=card.originalName&&!secondary&&!card.backFace)title=ColoredDetail(title,ChangedBlue);
        }
        var types=feature?.cardTypes??definition.cardTypes;
        bool creature=(types&CardType.Creature)!=0,walker=(types&CardType.Planeswalker)!=0;
        string color=TemplateColor(printed,types,card.hasColorBaseline?(ManaColorFlags)card.currentColors:definition.colorIndicator);
        bool saga=(feature?.subtypes??definition.subtypes??"").Split(' ').Contains("Saga");
        string canonicalRules=feature?.description??definition.description??"";
        bool fourRows=walker&&(System.Text.RegularExpressions.Regex.Matches(canonicalRules,@"(?:^|\n)\s*\[?[+−-]?\d+\]?:").Count>=4||canonicalRules.Length>400);
        string frame=(adventure!=null?"Adventure":walker?fourRows?"Planeswalker4":"Planeswalker":saga?"Saga":"Basic")+color;
        if(Resources.Load<Texture2D>("UI/CardTemplates/"+frame)==null){if(existing!=null)existing.gameObject.SetActive(false);var scan=parent.GetComponent<Image>();if(scan!=null)scan.color=scan.sprite!=null?Color.white:Ink;return false;}
        if(existing!=null&&existing.GetComponent<TemplateCardFace>()==null){existing.gameObject.SetActive(false);existing.name="RetiredComponentFace";Destroy(existing.gameObject);existing=null;}
        if(existing==null){var r=Rect(parent,"ComponentFace",Vector2.zero,Vector2.zero);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.gameObject.AddComponent<TemplateCardFace>();existing=r;}
        existing.gameObject.SetActive(true);existing.SetAsLastSibling();
        var backing=parent.GetComponent<Image>();
        TryBattlefieldArt(card,out var art);
        string stats=creature?original?PrintedStats(feature,definition):ModifiedStatsText(card,false):walker?(original||card.zone!="Battlefield"?feature?.loyalty??definition.loyalty:card.loyalty).ToString():null;
        string adventureCost=adventure==null?null:!original&&permitted?card.adventurePreviewManaCost??adventure.manaCost:adventure.manaCost;
        existing.GetComponent<TemplateCardFace>().Bind(art,title,cost,type,rules,stats,printed,
            adventure==null?null:GamePreferences.FeatureName(adventure),adventureCost,
            adventure==null?null:GamePreferences.FeatureType(adventure,"Instant — Adventure"),adventure==null?null:GamePreferences.FeatureRules(adventure),frame,compact);
        if(!existing.GetComponent<TemplateCardFace>().Fits){existing.gameObject.SetActive(false);if(backing!=null)backing.color=backing.sprite!=null?Color.white:Ink;return false;}
        foreach(string legacy in new[]{"PreparedSpellFace","ChosenSpellFace"})parent.Find(legacy)?.gameObject.SetActive(false);
        if(backing!=null&&!compact)backing.color=Color.clear;
        return true;
    }
    static string PrintedStats(CardFeatureDefinition feature,CardDefinition definition)
        =>(string.IsNullOrEmpty(feature?.powerExpression)?(feature?.attack??definition.attack).ToString():feature.powerExpression)+"/"
          +(string.IsNullOrEmpty(feature?.toughnessExpression)?(feature?.defence??definition.defence).ToString():feature.toughnessExpression);
    static string TemplateColor(string printed,CardType types,ManaColorFlags indicator)
    {
        if((types&CardType.Land)!=0)return "L";
        string colors=string.Concat("WUBRG".Where(c=>(printed??"").Contains(c.ToString())));
        if(colors.Length==0&&indicator!=0)colors=string.Concat("WUBRG".Where((c,i)=>((int)indicator&(1<<i))!=0));
        if(colors.Length==1)return colors;
        if(colors.Length>0)return "M";
        return (types&CardType.Artifact)!=0?"A":"C";
    }
}
