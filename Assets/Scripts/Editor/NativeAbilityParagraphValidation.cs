#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Display-boundary regressions from the user's actual Emry and payment logs.
// Native effects and targets remain unchanged; these checks claim no rules PASS.
public static class NativeAbilityParagraphValidation
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static string RunCurrentLocale()
    {
        if(!Application.isPlaying||!GamePreferences.Ready)throw new InvalidOperationException("Ready locale required");
        string language=GamePreferences.Language;
        var lines=new List<string>();int failures=0;
        void Check(bool ok,string name){lines.Add((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
        var go=new GameObject("NativeAbilityParagraphValidation");var ui=go.AddComponent<ForgeBattlefieldController>();ui.enabled=false;
        var catalog=(Dictionary<string,CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog",Private).GetValue(ui);
        foreach(var c in CardDatabaseRepository.LoadAll())if(!c.forgeAmbiguous)catalog.TryAdd(c.PrintingKey,c);
        var ability=typeof(ForgeBattlefieldController).GetMethod("AbilityEffectText",Private);
        var printed=typeof(ForgeBattlefieldController).GetMethod("PrintedRuleText",Private);
        const string effect="Choose target artifact card in your graveyard. You may cast that card this turn.";
        var source=new ForgeCardView{id=123,name="Emry, Lurker of the Loch",originalName="Emry, Lurker of the Loch",printingKey="BRC/81",zone="Battlefield"};
        string expected=language=="zh-CN"?"选择目标在你坟墓场中的神器牌。本回合中，你可以施放该牌。":language=="ja-JP"?"あなたの墓地にあるアーティファクト・カード１枚を対象とする。このターン、あなたはそのカードを唱えてもよい。":effect;
        string Display(string description,bool recipe=true){
            var item=new ForgeStackItem{source=source,description=description,rulesText=recipe?effect:null,rulesSource=recipe?source:null,kind="activated"};
            typeof(ForgeBattlefieldController).GetField("view",Private).SetValue(ui,new ForgeSeatView{stackItems=new[]{item}});
            string before=JsonUtility.ToJson(item);string result=(string)ability.Invoke(ui,new object[]{source,description});
            Check(JsonUtility.ToJson(item)==before,"Formatter preserves native fields");return result;
        }
        try{
            foreach(string description in new[]{"{T}: "+effect,source.name+" (123) - {T}: "+effect,effect}){
                string actual=Display(description);lines.Add("DISPLAY "+description+" => "+actual.Replace("\n"," / "));
                Check(actual.Contains(expected),"Omitted reminder binds the exact localized activation");
                Check(!actual.Contains("神器共鸣")&&!actual.Contains("Affinity for artifacts")&&!actual.Contains("mill four"),"Unrelated source abilities are not substituted");
                if(language!="en-US")Check(!BattleTextDiagnostics.IsUntranslatedDisplay(actual),"No English prose on Emry ability");
            }
            string legacy=Display("{T}: "+effect,false);
            Check(legacy.Contains(expected),"Older payload without recipe still uses its own printed ability");
            foreach(string unknown in new[]{"{U}: "+effect,effect+" Additional unknown effect.","When Emry, Lurker of the Loch enters, mill 123 cards."}){
                string text=(string)printed.Invoke(ui,new object[]{source,unknown});
                Check(language=="en-US"?text==unknown:!text.Contains(expected),"Different cost, effect or amount is not replaced by Emry's rule");
            }
            if(language!="en-US"){
                var field=typeof(GamePreferences).GetField("cards",BindingFlags.NonPublic|BindingFlags.Static);
                var translations=(Dictionary<string,string[]>)field.GetValue(null);var original=translations[source.name];var wrapped=(string[])original.Clone();
                wrapped[3]=wrapped[3].Replace(expected,expected.Replace(language=="zh-CN"?"。本回合":"。このターン","。\n"+(language=="zh-CN"?"本回合":"このターン")));
                Check(wrapped[3]!=original[3],"Visual wrapping fixture changes the localized paragraph");
                try{
                    translations[source.name]=wrapped;
                    ((Dictionary<string,string>)typeof(ForgeBattlefieldController).GetField("printedRuleCache",Private).GetValue(ui)).Clear();
                    Check(Display("{T}: "+effect).Replace("\n","").Contains(expected),"Localized visual line breaks do not break the activation binding");
                }finally{
                    translations[source.name]=original;
                    ((Dictionary<string,string>)typeof(ForgeBattlefieldController).GetField("printedRuleCache",Private).GetValue(ui)).Clear();
                }
            }
            foreach(string message in new[]{
                "Kappa Cannoneer - Creature 4 / 4",
                "Choose artifact to tap for Improvise.","Remaining mana cost is {5}{U}",
                "Select target artifact card in your graveyard",
                "The colors provided by Claws of Gix (108) you've chosen cannot be used to pay the manacost of {U}",
                "The colors provided by Drone Token (154) you've chosen cannot be used to pay the manacost of {U}"}){
                string text=GamePreferences.NativeText(message);lines.Add("MESSAGE "+message+" => "+text);
                if(language!="en-US")Check(!BattleTextDiagnostics.IsUntranslatedDisplay(text),"Captured payment/target message follows selected locale");
                if(message.Contains("{U}"))Check(text.Contains("{U}"),"Actual mana cost symbols preserved");
            }
            string token=GamePreferences.CardFaceName("Drone Token (154)");
            Check(language=="en-US"?token=="Drone Token":token.Contains(language=="zh-CN"?"奴兽":"ドローン"),"Generated token name uses existing subtype translations");
            string unsupported="The colors provided by unknown custom source you've chosen cannot be used to pay the manacost of banana";
            Check(GamePreferences.NativeText(unsupported).Contains("banana"),"Unknown custom cost is not silently replaced");
            const string unknownSource="The colors provided by unknown custom source you've chosen cannot be used to pay the manacost of {U}";
            Check(GamePreferences.NativeText(unknownSource)==unknownSource,"Unknown identity is not registered or accepted by the typed grammar");
        }finally{
            UnityEngine.Object.Destroy(go);Directory.CreateDirectory(".tools/emry-localization-1016/regression");
            lines.Add("Failures="+failures+"; locale="+language);
            File.WriteAllLines(".tools/emry-localization-1016/regression/"+language+".txt",lines);
        }
        if(failures>0)throw new InvalidOperationException("Native paragraph regression failures: "+failures);
        return "PASS "+language+": "+lines.Count+" display records";
    }
}
#endif
