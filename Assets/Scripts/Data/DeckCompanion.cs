using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static class DeckCompanion
{
    public static string Text(string zh,string en,string ja)=>GamePreferences.Language=="ja-JP"?ja:GamePreferences.Language=="en-US"?en:zh;
    public static bool IsCompanion(CardDefinition card)=>card!=null&&Regex.IsMatch(card.description??"",@"(?:^|\n)Companion\s*[—–-]",RegexOptions.IgnoreCase);
    public static string Condition(CardDefinition card)
    {
        string line=(GamePreferences.CardRules(card)??"").Replace("\r","").Split('\n').FirstOrDefault(s=>Regex.IsMatch(s,@"^(行侣|同伴|Companion|相棒)\s*[—～〜–-]"))??GamePreferences.CardRules(card);
        return Regex.Split(line??"",@"\s*[（(](?=If this card is|If this is|若此牌|如果此牌|このカードが|このカードを相棒)")[0];
    }
    public static bool Has(DeckDefinition deck)=>deck?.companion!=null&&(!string.IsNullOrEmpty(deck.companion.PrintingKey)||deck.companion.cardId>0);
    public static DeckEntry Copy(DeckEntry e)=>e==null||string.IsNullOrEmpty(e.PrintingKey)&&e.cardId==0?null:new DeckEntry{setCode=e.setCode,collectorNumber=e.collectorNumber,cardId=e.cardId,count=1};
    public static bool IsSelected(DeckDefinition deck,DeckEntry e)=>Has(deck)&&e!=null&&string.Equals(deck.companion.PrintingKey,e.PrintingKey,StringComparison.OrdinalIgnoreCase);
    public static bool Select(DeckDefinition deck,CardDefinition card,IReadOnlyList<CardDefinition> cards)
    {
        if(deck==null||!IsCompanion(card)||card.forgeAmbiguous)return false;
        var side=deck.sideboard.FirstOrDefault(e=>e.PrintingKey==card.PrintingKey&&e.count>0);
        if(side==null){
            var main=deck.cards.FirstOrDefault(e=>e.PrintingKey==card.PrintingKey&&e.count>0);
            if(main!=null){main.count--;deck.cards.RemoveAll(e=>e.count<=0);side=new DeckEntry{setCode=card.setCode,collectorNumber=card.CollectorNumber,cardId=card.cardID,count=1};deck.sideboard.Add(side);}
            else if(DeckRepository.AddCard(deck,card,1,true)>0)side=deck.sideboard.First(e=>e.PrintingKey==card.PrintingKey);
        }
        if(side==null)return false;deck.companion=Copy(side);return true;
    }
    public static void ClearMissing(DeckDefinition deck){if(deck?.companion!=null&&deck.sideboard?.Any(e=>e!=null&&e.count>0&&IsSelected(deck,e))!=true)deck.companion=null;}
    public static bool ValidateReference(DeckDefinition deck,IReadOnlyList<CardDefinition> cards,out string error)
    {
        error="";if(!Has(deck))return true;
        var e=deck.companion;var card=DeckRepository.ResolveCard(e,cards);
        if(e.count!=1||string.IsNullOrEmpty(e.PrintingKey)||card==null||!IsCompanion(card)||deck.sideboard?.Any(s=>s!=null&&s.count>0&&IsSelected(deck,s))!=true){
            error=Text("行侣必须指定备牌中一张可唯一识别、具有行侣异能的牌。","Designate one identifiable companion card in the sideboard.","サイドボードの相棒能力を持つカード１枚を指定してください。");return false;
        }
        return true;
    }
    public static string Label(DeckDefinition deck,IReadOnlyList<CardDefinition> cards)=>!Has(deck)?Text("未使用行侣","No companion","相棒なし"):Text("行侣：","Companion: ","相棒：")+GamePreferences.CardName(DeckRepository.ResolveCard(deck.companion,cards));
}
