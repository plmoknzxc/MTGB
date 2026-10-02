#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Re-evaluate every original candidate, preserving identity and current evidence.
public static class ReviewedPresentationValidation
{
    [Serializable] public class Context { public LocalizedPresentationAudit.Gap gap; public string englishRules,cardName; }
    [Serializable] public class Contexts { public Context[] entries; }
    [Serializable] public class Result
    {
        public LocalizedPresentationAudit.Gap original;
        public string rules;
        public string[] currentFields;
        public string[] expected, actual, tokenKeys;
        public bool expectedStillPresent, actualPresent, mechanicRouteVerified;
        public int localImages, relatedImages;
    }
    [Serializable] public class Report { public bool complete; public List<Result> entries=new List<Result>(); }
    static string Family(string key)=>key.StartsWith("Affinity")?"Affinity":key.StartsWith("Amass")?"Amass":key.EndsWith("cycling",StringComparison.OrdinalIgnoreCase)?"Cycling":key.EndsWith("walk",StringComparison.OrdinalIgnoreCase)?"Landwalk":key;
    public static string Run()
    {
        var contexts=JsonUtility.FromJson<Contexts>("{\"entries\":"+File.ReadAllText(".tools/presentation-audit/review-contexts.json")+"}").entries;
        var cards=CardDatabaseRepository.LoadAll().GroupBy(c=>c.PrintingKey).ToDictionary(g=>g.Key,g=>g.First());
        var reader=typeof(GamePreferences).GetMethod("ReadCards",BindingFlags.Static|BindingFlags.NonPublic);
        var field=typeof(GamePreferences).GetField("cards",BindingFlags.Static|BindingFlags.NonPublic);
        var previous=field.GetValue(null);
        var translations=new Dictionary<string,Dictionary<string,string[]>>();
        foreach(var language in new[]{"zh-CN","ja-JP"})translations[language]=(Dictionary<string,string[]>)reader.Invoke(null,new object[]{Path.Combine(Application.streamingAssetsPath,"CardTranslations",language+".txt")});
        var report=new Report();
        var host=new GameObject("ReviewedMechanicRouteValidation");host.SetActive(false);
        var menu=host.AddComponent<MainMenuController>();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        try
        {
            foreach(var context in contexts)
            {
                var gap=context.gap;var result=new Result{original=gap};report.entries.Add(result);
                if(!cards.TryGetValue(gap.key,out var card))throw new Exception("Missing printing identity: "+gap.key);
                field.SetValue(null,translations[gap.language]);
                translations[gap.language].TryGetValue(gap.name,out var row);
                result.currentFields=row;
                result.rules=row!=null&&!string.IsNullOrWhiteSpace(row[3])?row[3]:context.englishRules;
                if(gap.field!="keyword"&&gap.field!="token")continue;
                var actual=DeckKeywordGlossary.Find(result.rules,gap.language,card);
                result.actual=actual.Select(e=>e.English).ToArray();
                if(gap.field=="keyword")
                {
                    result.expected=DeckKeywordGlossary.Find(context.englishRules,"en",card).Select(e=>e.English).ToArray();
                    result.expectedStillPresent=result.expected.Any(e=>Family(e)==Family(gap.detail));
                    result.actualPresent=result.actual.Any(e=>Family(e)==Family(gap.detail));
                }
                else
                {
                    string target=gap.detail.Substring(gap.detail.LastIndexOf(" / ",StringComparison.Ordinal)+3);
                    result.actualPresent=DeckTokenLinks.Find(card,result.rules).Any(e=>e.TokenKey==target);
                    var arts=DeckRelatedTokenArt.GetReferences(card,true);
                    typeof(MainMenuController).GetField("detailTokenArts",flags).SetValue(menu,arts);
                    var route=typeof(MainMenuController).GetMethod("RelatedTokensForMechanic",flags);
                    foreach(var entry in actual)
                    {
                        var matching=(List<DeckRelatedTokenArt.Art>)route.Invoke(menu,new object[]{entry});
                        if(!matching.Any(a=>a.key==target))continue;
                        result.mechanicRouteVerified=true;
                        result.tokenKeys=matching.Select(a=>a.key).ToArray();
                        result.relatedImages=matching.Count;result.localImages=matching.Count(a=>a.sprite!=null);
                        break;
                    }
                }
            }
            report.complete=true;
            File.WriteAllText("Tools/ForgeBridge/Reports/ReviewedPresentationCurrentEvidence.json",JsonUtility.ToJson(report,true));
            return "Reviewed "+report.entries.Count+" original candidates; token mechanics routed="+report.entries.Count(r=>r.mechanicRouteVerified);
        }
        finally {field.SetValue(null,previous);UnityEngine.Object.DestroyImmediate(host);}
    }
}
#endif
