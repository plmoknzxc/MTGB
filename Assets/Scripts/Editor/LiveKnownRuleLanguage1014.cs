#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Replays existing native inputs; formatting variants are display tests only.
public static class LiveKnownRuleLanguage1014
{
    public static bool Running;
    public static string Output;
    sealed class Host : MonoBehaviour { }
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> Results=new();
    static ForgeSeatView Read(string file)=>(ForgeSeatView)typeof(OnlineMatchSession).Assembly.GetType("ServerSnapshotJson").GetMethod("Deserialize").MakeGenericMethod(typeof(ForgeSeatView)).Invoke(null,new object[]{File.ReadAllText(file)});
    static void Check(bool pass,string message)=>Results.Add((pass?"PASS ":"FAIL ")+message);
    static string Body(ForgeBattlefieldController ui,ForgeCardView card,string text)=>(string)typeof(ForgeBattlefieldController).GetMethod("AbilityEffectText",Flags).Invoke(ui,new object[]{card,text});
    static bool Chinese(string text)=>System.Text.RegularExpressions.Regex.IsMatch(text??"",@"[\u3400-\u9fff]");
    public static void Run(string phase)
    {
        if(!Application.isPlaying||Running)throw new Exception("Play required");
        Results.Clear();Running=true;Output=".tools/live-fixes-1014/known-language/"+phase+"-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmss");Directory.CreateDirectory(Output);
        new GameObject("LiveKnownRuleLanguage1014").AddComponent<Host>().StartCoroutine(Guard(Exercise()));
    }
    static IEnumerator Guard(IEnumerator iterator)
    {
        while(true) {
            bool more;object next;
            try {more=iterator.MoveNext();next=more?iterator.Current:null;}
            catch(Exception e){Results.Add("FAIL "+e);more=false;next=null;}
            if(!more)break;yield return next;
        }
        File.WriteAllLines(Output+"/result.txt",Results);Running=false;
        Debug.Log("KNOWN RULE LANGUAGE "+(Results.Any(r=>r.StartsWith("FAIL"))?"FAIL":"PASS")+" "+Output);
    }
    static IEnumerator Frame(){yield return null;Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();}
    static IEnumerator Exercise()
    {
        GamePreferences.SetLanguage("zh-CN");float until=Time.realtimeSinceStartup+30;
        while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;
        Check(GamePreferences.Ready&&GamePreferences.Language=="zh-CN","Chinese resources ready");
        foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))notice.enabled=false;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("LanguageBattlefield").AddComponent<ForgeBattlefieldController>();ui.enabled=false;
        var records=File.ReadAllLines(".tools/live-fixes-1014/known-language/baseline-real.tsv").Select(line=>line.Split('\t')).ToArray();
        var first=Read(records[0][0]);ui.Present(first,1,false,null,null,"");yield return Frame();
        var catalog=(Dictionary<string,CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog",Flags).GetValue(ui);
        var outputs=new List<string>();
        foreach(var row in records) {
            var view=Read(row[0]);typeof(ForgeBattlefieldController).GetField("view",Flags).SetValue(ui,view);
            var item=view.stackItems.First(s=>s.description==row[3].Replace("\\n","\n"));
            string translated=GamePreferences.T(Body(ui,item.source,item.description));
            outputs.Add(row[0]+"\t"+row[3]+"\t"+translated.Replace("\n","\\n"));
            if(item.kind!="spell")Check(Chinese(translated),"real ability keeps Chinese: "+item.source.name+" / "+item.id);
            if(item.source.name=="Living End")Check(Chinese(translated)&&translated.Contains("法术力费用")&&!translated.Contains("Sacrifice permanents"),"Living End printed rule plus cast permission stays translated");
        }
        File.WriteAllLines(Output+"/real-outputs.tsv",outputs);
        var subtlety=Read(records.First(r=>r[1]=="Subtlety"&&r[3].StartsWith("When Subtlety"))[0]);
        var trigger=subtlety.stackItems.First(s=>s.description.StartsWith("When Subtlety"));
        string padded=trigger.description.Replace("When Subtlety enters,","When  Subtlety\n enters,");
        Check(Body(ui,trigger.source,padded).Contains("当此生物进场时"),"known rule tolerates native display whitespace");
        string changed=trigger.description.Replace("choose up to one","choose up to two");
        Check(Body(ui,trigger.source,changed).Contains("up to two"),"different rule condition is not replaced by printed translation");
        var definition=catalog[trigger.source.printingKey];var primary=definition.features.First(f=>f.cardName==definition.cardName);
        string original=primary.description;
        try {
            primary.description=original.Replace("Flash\nFlying","Flash Flying");
            Check(Body(ui,trigger.source,primary.description)==GamePreferences.CardRules(definition),"whole existing rule translates despite differing paragraph counts");
        } finally {primary.description=original;}
        Check(Body(ui,trigger.source,"A new condition not present in the printed rule.").Contains("A new condition"),"unknown added rule is retained, not replaced by unrelated Chinese");
        var canvas=GameObject.Find("Canvas").transform;
        var tile=new GameObject("KnownRuleSample",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();tile.SetParent(canvas,false);tile.sizeDelta=new Vector2(240,335);
        var present=typeof(ForgeBattlefieldController).GetMethod("PresentAbilityFace",Flags);
        foreach(var faceName in new[]{"Stomp","Tibalt, Cosmic Impostor"}) {
            var pair=catalog.Values.SelectMany(d=>(d.features??new List<CardFeatureDefinition>()).Where(f=>f!=null&&f.cardName==faceName).Select(f=>new{definition=d,feature=f}))
                .First(p=>Chinese(GamePreferences.FeatureRules(p.feature))&&!string.IsNullOrEmpty(p.feature.description));
            var source=new ForgeCardView{id=901,name=pair.feature.cardName,originalName=pair.feature.cardName,printingKey=pair.definition.PrintingKey,originalRules=pair.feature.description,currentRules=pair.feature.description,currentType="Instant",backFace=pair.feature.faceIndex==1};
            string display=Body(ui,source,source.currentRules);
            Check(display==GamePreferences.FeatureRules(pair.feature),"secondary face uses its own existing translation: "+source.name);
            present.Invoke(ui,new object[]{tile,source,source.currentRules,"trigger",true});yield return Frame();
            string rendered=tile.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Symbols").text;
            Check(Chinese(rendered),"actual ability body renders translated secondary face: "+source.name);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/face-"+source.id+"-"+pair.feature.faceIndex+".png"));
        }
        var bauble=Read(records.First(r=>r[1]=="Mishra's Bauble")[0]).stackItems.First(s=>s.source.name=="Mishra's Bauble").source;
        var baubleDefinition=catalog[bauble.printingKey];
        present.Invoke(ui,new object[]{tile,bauble,baubleDefinition.description,"activated",true});yield return Frame();
        var symbolText=tile.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Symbols").text;
        Check(symbolText.Contains(CardSymbolText.Format("{T}"))&&!symbolText.Contains("{T}"),"known Chinese rule retains tap icon instead of raw token");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/tap-symbol.png"));
        // Actual native castingFace option, with its original identity and cost.
        var folders=records.Select(r=>Path.GetDirectoryName(r[0])).Distinct();
        var optionView=folders.SelectMany(folder=>Directory.GetFiles(folder,"*-seat1.json")).Select(Read)
            .First(v=>v.request?.message=="getAbilityToPlay"&&(v.request.options??Array.Empty<ForgeOption>()).Any(o=>o.castingFace!=null&&Chinese(GamePreferences.FeatureRules(new CardFeatureDefinition{cardName=o.castingFace.name,description=o.castingFace.currentRules}))));
        tile.gameObject.SetActive(false);
        ui.Present(optionView,1,true,_=>{},null,"");yield return Frame();
        var costLabels=GameObject.Find("Canvas").GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="ExtraCost"&&t.gameObject.activeInHierarchy).Select(t=>t.text).ToArray();
        Check(costLabels.Any(t=>t.Contains("蓝色牌"))&&!costLabels.Any(t=>t.Contains("Exile a blue card")),"native evoke cost reuses existing blue-card Chinese recipe");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/casting-option.png"));
        var chosen=optionView.request.options.First(o=>o.castingFace!=null&&o.castingFace.name=="Subtlety");
        var castingTile=new GameObject("NativeCastingFaceSample",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();castingTile.SetParent(canvas,false);castingTile.sizeDelta=new Vector2(250,380);castingTile.anchoredPosition=new Vector2(-360,0);
        typeof(ForgeBattlefieldController).GetMethod("DrawAbilityChoice",Flags).Invoke(ui,new object[]{castingTile,chosen.castingFace,chosen,castingTile.sizeDelta});yield return Frame();
        string castingBody=castingTile.Find("EffectPanel/TextScroll/Viewport/Content/Effect/Symbols")?.GetComponent<TMP_Text>()?.text;
        if(castingBody==null)castingBody=castingTile.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Symbols").text;
        Check(castingBody.Contains("当此生物进场时")&&!castingBody.Contains("When Subtlety enters"),"actual DrawAbilityChoice castingFace body uses its existing Chinese");
        var costMethod=typeof(ForgeBattlefieldController).GetMethod("PrintedCostText",Flags);
        Check(((string)costMethod.Invoke(ui,new object[]{chosen.castingFace,"Exile two blue cards from your hand"})).Contains("two blue cards"),"different payment amount is not replaced by one-card recipe");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/casting-body.png"));
        UnityEngine.Object.Destroy(castingTile.gameObject);
        UnityEngine.Object.Destroy(tile.gameObject);
    }
}
#endif
