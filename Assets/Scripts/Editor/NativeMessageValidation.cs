#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class NativeMessageValidation
{
    [Serializable] sealed class Fixture { public Sample[] samples; }
    [Serializable] sealed class Sample { public string name; public ForgeStackItem item; }
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static string RunCurrentLocale()
    {
        if (!Application.isPlaying || !GamePreferences.Ready) throw new InvalidOperationException("Ready locale required");
        var checks=new List<string>();
        void Check(bool value,string name) { if(!value)throw new InvalidOperationException(name);checks.Add("PASS "+name); }
        var go=new GameObject("NativeMessageValidation");var ui=go.AddComponent<ForgeBattlefieldController>();ui.enabled=false;
        var catalog=(Dictionary<string,CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog",Private).GetValue(ui);
        foreach(var c in CardDatabaseRepository.LoadAll())if(!c.forgeAmbiguous)catalog.TryAdd(c.PrintingKey,c);
        var method=typeof(ForgeBattlefieldController).GetMethod("AbilityEffectText",Private);
        string language=GamePreferences.Language;
        try
        {
            var fixture=JsonUtility.FromJson<Fixture>(File.ReadAllText("Tools/Validation/Fixtures/BattleTextProjection1016.json"));
            Check(fixture.samples.Length>=5,"Nonempty native display fixtures");
            foreach(var row in fixture.samples)
            {
                var item=row.item;string before=JsonUtility.ToJson(item);
                var state=new ForgeSeatView{stackItems=new[]{item},players=Array.Empty<ForgePlayerView>()};
                if(row.name.StartsWith("order-",StringComparison.Ordinal))state=new ForgeSeatView{stackItems=Array.Empty<ForgeStackItem>(),request=new ForgeRequest{kind="OrderAbilities",options=new[]{new ForgeOption{ability=item}}}};
                typeof(ForgeBattlefieldController).GetField("view",Private).SetValue(ui,state);
                string actual=(string)method.Invoke(ui,new object[]{item.source,item.description});
                string original=GamePreferences.CardRules(catalog[item.rulesSource.printingKey]);
                var definition=catalog[item.rulesSource.printingKey];
                var face=definition.features?.FirstOrDefault(f=>f.cardName==item.rulesSource.name);
                if(face!=null)original=GamePreferences.FeatureRules(face);
                string expected=original.Replace("\\n","\n").Split('\n')[0];
                if(row.name=="adapter-granted-ability"){
                    var quote=System.Text.RegularExpressions.Regex.Match(expected,"[“「『\"](.+)[”」』\"]");
                    expected=System.Text.RegularExpressions.Regex.Replace(quote.Groups[1].Value,@"^(?:\{[^{}]+\}\s*[,，]?\s*)+[:：]\s*","");
                }
                if(language!="en-US")Check(actual==expected,row.name+" exact paragraph from its own face");
                Check(JsonUtility.ToJson(item)==before,row.name+" projection does not mutate native input");
                var publicState=new ForgeSeatView{stackItems=new[]{item}};
                var spectator=OnlineSpectatorView.Create(new OnlinePlayerViewState{forge=publicState},3);
                Check(spectator.forge.stackItems[0].rulesText==item.rulesText&&spectator.forge.stackItems[0].rulesSource.name==item.rulesSource.name,row.name+" spectator retains public provenance");
                item.source.faceDown=true;
                spectator=OnlineSpectatorView.Create(new OnlinePlayerViewState{forge=publicState},3);
                Check(spectator.forge.stackItems[0].rulesText==null&&spectator.forge.stackItems[0].rulesSource==null,row.name+" face-down provenance not forwarded");
                item.source.faceDown=false;
            }
            const string compound="Static Discharge (151) deals 3 damage to Player 2. Player 1 perpetually increases the intensity of all cards they own named Static Discharge by 1.";
            string text=GamePreferences.NativeText(compound);
            if(language=="en-US")Check(text.Contains("Player 2.")&&text.Contains("by 1."),"English compound kept in native order");
            else Check(text.Contains("3")&&text.Contains("1")&&text.Contains("2")&&!text.Contains("perpetually")&&!text.Contains("Player 2. Player 1"),"Damage and perpetual-intensity clauses separately localized");
            foreach(string source in new[]{"Pyroclasm (151) deals 2 damage to each creature.","Mizzium Mortars deals 4 damage to each creature you don't control.","Player 1 draws three cards. Player 2 loses 7 life."})
            {
                string result=GamePreferences.NativeText(source);
                if(language!="en-US")Check(!result.Contains("deals")&&!result.Contains("draws")&&!result.Contains("loses"),"Typed message: "+source);
            }
            string unknown=GamePreferences.NativeText("Unknown caster deals 5 damage to Unknown target. Additional unknown effect stays here.");
            Check(unknown.Contains("5")&&unknown.Contains("Additional unknown effect stays here."),"Unknown clauses remain visible without being swallowed");
            string malformed=GamePreferences.NativeText("Pyroclasm deals banana damage to each creature.");
            Check(malformed.Contains("banana"),"Unknown amount is not rewritten as a known damage rule");
        }
        finally {UnityEngine.Object.Destroy(go);}
        Directory.CreateDirectory(".tools/battle-text-fix-1016/regression");
        File.WriteAllLines(".tools/battle-text-fix-1016/regression/"+language+".txt",checks);
        return "PASS "+language+": "+checks.Count+" checks";
    }

    public static string RunDiagnosticStorage()
    {
        if(!GamePreferences.Ready||GamePreferences.Language=="en-US")throw new InvalidOperationException("Non-English locale required");
        var checks=new List<string>();
        void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks.Add("PASS "+name);}
        BattleTextDiagnostics.FlushOnExit();
        string path=".tools/battle-text-fix-1016/diagnostic-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ")+".jsonl";
        string marker="Unknown diagnostic sentence "+Guid.NewGuid().ToString("N");
        string returned=GamePreferences.NativeText(marker);
        GamePreferences.NativeText(marker);
        BattleTextDiagnostics.FlushAsync(path).GetAwaiter().GetResult();
        var entries=File.ReadAllLines(path).Select(JsonUtility.FromJson<BattleTextDiagnostics.Entry>).ToArray();
        Check(entries.Count(e=>e.source==marker)==1,"Duplicate display records deduplicated");
        Check(entries.Single(e=>e.source==marker).display==returned,"Actual displayed text saved");
        GamePreferences.ClearLanguageDiagnostics();
        Check(File.ReadAllText(path).Contains(marker),"Clearing locale diagnostics preserves disk evidence");
        string hidden="Hidden diagnostic sentence "+Guid.NewGuid().ToString("N");
        BattleTextDiagnostics.Record("validation",hidden,hidden,new ForgeCardView{faceDown=true,name="Secret card"});
        BattleTextDiagnostics.FlushAsync(path).GetAwaiter().GetResult();
        Check(!File.ReadAllText(path).Contains(hidden),"Concealed card is not recorded");
        for(int i=0;i<700;i++)BattleTextDiagnostics.Record("validation","Unknown pending diagnostic sentence "+i,"Unknown pending diagnostic sentence "+i);
        BattleTextDiagnostics.FlushAsync(path).GetAwaiter().GetResult();
        Check(File.ReadAllLines(path).Count(l=>l.Contains("Unknown pending diagnostic sentence"))<=128,"Pending evidence bounded under stress");
        string locked=path+".locked";
        using(var stream=new FileStream(locked,FileMode.Create,FileAccess.Write,FileShare.None))
        {
            GamePreferences.NativeText("Unknown locked diagnostic sentence");
            try{BattleTextDiagnostics.FlushAsync(locked).GetAwaiter().GetResult();throw new InvalidOperationException("Locked file accepted");}
            catch(IOException){checks.Add("PASS Diagnostic write failure does not affect translation");}
        }
        BattleTextDiagnostics.FlushAsync(locked).GetAwaiter().GetResult();
        Check(File.ReadAllText(locked).Contains("Unknown locked diagnostic sentence"),"Failed write retried after unlock");
        File.WriteAllLines(".tools/battle-text-fix-1016/diagnostic-storage.txt",checks);
        return string.Join("\n",checks);
    }

    // Replays the recorded display contract, not an entire duel or its effects.
    // Unknown/card-catalog-missing inputs remain explicit failures.
    public static string ReplaySavedMessages(string path)
    {
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var go=new GameObject("SavedBattleTextReplay");var ui=go.AddComponent<ForgeBattlefieldController>();ui.enabled=false;
        var catalog=(Dictionary<string,CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog",flags).GetValue(ui);
        foreach(var card in CardDatabaseRepository.LoadAll())if(!card.forgeAmbiguous)catalog.TryAdd(card.PrintingKey,card);
        var method=typeof(ForgeBattlefieldController).GetMethod("AbilityEffectText",flags);
        var lines=new List<string>();int failed=0;
        try{
            foreach(string line in File.ReadLines(path)){
                var entry=JsonUtility.FromJson<BattleTextDiagnostics.Entry>(line);
                if(entry.language!=GamePreferences.Language)continue;
                if(entry.truncated){failed++;lines.Add("FAIL truncated record requires original input");continue;}
                string actual;
                if(entry.channel=="ability"){
                    var card=new ForgeCardView{name=entry.cardName,originalName=entry.originalName,printingKey=entry.printingKey,currentArtPrintingKey=entry.currentArtPrintingKey,copy=entry.copy,merged=entry.merged};
                    var origin=new ForgeCardView{name=entry.ruleSourceName,originalName=entry.ruleSourceName,printingKey=entry.ruleSourcePrintingKey};
                    var item=new ForgeStackItem{source=card,description=entry.source,rulesText=entry.ruleText,rulesSource=origin};
                    typeof(ForgeBattlefieldController).GetField("view",flags).SetValue(ui,new ForgeSeatView{stackItems=new[]{item}});
                    actual=(string)method.Invoke(ui,new object[]{card,entry.source});
                }else if(entry.channel=="catalog-rules"&&catalog.TryGetValue(entry.printingKey??"",out var definition))actual=GamePreferences.CardRules(definition);
                else actual=GamePreferences.NativeText(entry.source);
                bool gap=BattleTextDiagnostics.IsUntranslatedDisplay(actual);
                if(gap)failed++;
                lines.Add((gap?"FAIL ":"PASS ")+entry.channel+" "+entry.source.Replace("\n","\\n")+" => "+actual.Replace("\n","\\n"));
            }
        }finally{UnityEngine.Object.Destroy(go);}
        File.WriteAllLines(path+".replay.txt",lines);
        if(failed>0)throw new InvalidOperationException("Saved display inputs still untranslated: "+failed+"; "+path+".replay.txt");
        return "PASS saved display replay: "+lines.Count;
    }
}
#endif
