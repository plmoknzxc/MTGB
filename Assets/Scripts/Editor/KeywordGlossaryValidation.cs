#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class KeywordGlossaryValidation
{
    public static string Run()
    {
        int terms=0;
        foreach(var entry in DeckKeywordGlossary.All)
        {
            Require(!string.IsNullOrWhiteSpace(entry.EnglishHelp)&&!string.IsNullOrWhiteSpace(entry.ChineseHelp),"Missing help: "+entry.English);
            if(!entry.AutoDisplay)continue;
            Require(DeckKeywordGlossary.Find(entry.English,"en").Any(e=>e.English==entry.English),"English name: "+entry.English);
            Require(DeckKeywordGlossary.Find(entry.Chinese,"zh-CN").Any(e=>e.English==entry.English),"Chinese name: "+entry.Chinese);
            Require(DeckKeywordGlossary.Find(entry.Japanese,"ja-JP").Any(e=>e.English==entry.English),"Japanese name: "+entry.Japanese);
            foreach(var term in new[]{entry.English,entry.Chinese,entry.Japanese}.Concat(entry.Aliases).Distinct())
            {
                Require(new[]{"en","zh-CN","ja-JP"}.Any(lang=>DeckKeywordGlossary.Find(term,lang).Any(e=>SameFamily(e,entry))),"Wrong or missing alias: "+term+" -> "+entry.English);
                terms++;
            }
        }
        Has("在你的维持开始时，你失去1点生命且囤兵1。", "Amass");
        Has("At the beginning of your upkeep, amass Zombies 1.", "Amass Zombies");
        Has("在你的维持开始时，你失去1点生命且囤兵半兽人2。", "Amass Orcs");
        Has("从你的坟墓场搜证6。", "Collect Evidence");
        Has("呼魂～从你手上放逐一张蓝色牌。", "Evoke");
        Only("Islandcycling {1}{U}","Islandcycling");
        Only("海岛循环{1}{U}","Islandcycling");
        Only("Snow-covered landwalk", "Snow-covered landwalk");
        Only("非基本地行者", "Nonbasic landwalk");
        Only("神器地循环{2}", "Artifact landcycling");
        Only("雪境地循环{2}", "Snow landcycling");
        Only("结界共鸣", "Affinity for enchantments");
        Only("伙伴共鸣", "Affinity for Allies");
        Only("精怪共鸣", "Affinity for Spirits");
        Only("非瑞人共鸣", "Affinity for Phyrexians");
        Only("妖精共鸣", "Affinity for Elves");
        Only("Flashback {2}{U}","Flashback");
        Only("Multikicker {1}","Multikicker");
        Only("Flying (Can be blocked by flying or reach (not other creatures).)","Flying");
        Only("威力变身{3}","Megamorph");
        Require(DeckKeywordGlossary.Find("Draw a card. (This reminder mentions amass and flying.)").Count==0,"Reminder-only terms leaked");
        string source="囤兵1。（说明里有飞行。）";
        string marked=DeckKeywordGlossary.Markup(source,DeckKeywordGlossary.Find(source));
        Require(marked.Contains(">囤兵</u>")&&!marked.Contains(">飞行</u>"),"Links don't follow visible keyword spans");
        Require(DeckKeywordGlossary.Find("When this enters, draw a card.").Count==0,"Ordinary rules became a mechanism box");
        Only("化生{5}{U}","Emerge");
        Only("神器化生{5}{U}","Emerge");
        Only("侵染", "Infect");
        Only("运土3。", "Earthbend");
        Only("对目标生物截气。", "Airbend");
        Only("截水{3}：抓一张牌。", "Waterbend");
        Only("振火2", "Firebending");
        Only("延缓4～{1}{U}", "Suspend");
        Only("Suspend 4—{1}{U}", "Suspend");
        int fixtures=CheckActualTranslations();
        string result=$"PASS: {DeckKeywordGlossary.All.Count} entries, {terms} multilingual terms, {fixtures} actual translated rules; embedded keywords, typecycling/landwalk variants, prefix collisions, nested reminder filtering and links.";
        Directory.CreateDirectory(".tools/keyword-glossary");File.WriteAllText(".tools/keyword-glossary/result.txt",result);
        return result;
    }
    // Parameterized mechanics intentionally return the specific variant, not the
    // generic catalog label. Test that family without weakening ordinary aliases.
    private static bool SameFamily(DeckKeywordGlossary.Entry found,DeckKeywordGlossary.Entry expected)
    {
        if(found.English==expected.English)return true;
        if(expected.English=="Landwalk")return found.English.EndsWith("walk",StringComparison.OrdinalIgnoreCase);
        if(expected.English=="TypeCycling")return found.English.EndsWith("cycling",StringComparison.OrdinalIgnoreCase)&&found.English!="Cycling";
        return false;
    }
    [Serializable] private sealed class Fixture { public string mechanic, card, chineseName, english, chinese; }
    [Serializable] private sealed class Fixtures { public Fixture[] entries; }
    private static int CheckActualTranslations()
    {
        var data=JsonUtility.FromJson<Fixtures>(File.ReadAllText("Tools/ForgeBridge/KeywordMechanicFixtures.json"));
        foreach(var row in data.entries)
        {
            var card=new CardDefinition {cardName=row.card};
            card.features.Add(new CardFeatureDefinition {cardName=row.chineseName});
            foreach(var sample in new[]{Tuple.Create(row.english,"en"),Tuple.Create(row.chinese,"zh-CN")})
            {
                var found=DeckKeywordGlossary.Find(sample.Item1,sample.Item2,card);
                Require(found.Any(e=>e.English==row.mechanic),"Actual card missing: "+row.card+" / "+row.mechanic+" / "+sample.Item2);
                var marked=DeckKeywordGlossary.Markup(sample.Item1,found,card);
                Require(marked.Contains("<link=\"k"),"Actual card lacks links: "+row.card+" / "+sample.Item2);
            }
        }
        return data.entries.Length*2;
    }
    public static string RunCatalogTranslationAudit()
    {
        const string path=".tools/keyword-glossary/catalog-translated-headers.tsv";
        var failures=new System.Collections.Generic.List<string>();
        var mechanics=new System.Collections.Generic.HashSet<string>();
        var lines=File.ReadAllLines(path);
        var translatedNames=File.ReadAllLines("Assets/StreamingAssets/CardTranslations/zh-CN.txt")
            .Concat(File.ReadAllLines("Assets/StreamingAssets/CardTranslations/zh-CN.supplement.txt"))
            .Select(l=>l.Split(new[]{'|'},4)).Where(r=>r.Length==4)
            .GroupBy(r=>r[0]).ToDictionary(g=>g.Key,g=>g.Last()[1]);
        foreach(var line in lines)
        {
            var row=line.Split('\t').Select(x=>System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(x))).ToArray();
            var expected=DeckKeywordGlossary.All.First(e=>e.English==row[0]);
            mechanics.Add(row[0]);
            var card=new CardDefinition {cardName=row[1]};
            card.features.Add(new CardFeatureDefinition {cardName=translatedNames[row[1]]});
            var found=DeckKeywordGlossary.Find(row[3],"zh-CN",card);
            int index=found.FindIndex(e=>SameFamily(e,expected)||expected.English=="Affinity"&&e.English.StartsWith("Affinity for ")||expected.English=="Amass"&&e.English.StartsWith("Amass "));
            if(index<0||!DeckKeywordGlossary.Markup(row[3],found,card).Contains("<link=\"k"+index+"\">"))
                failures.Add(row[1]+" / "+row[0]+" / "+row[2]);
        }
        File.WriteAllLines(".tools/keyword-glossary/catalog-translation-gaps.txt",failures);
        Require(failures.Count==0,"Catalog translation gaps: "+string.Join("; ",failures.Take(10)));
        string result=$"PASS: {lines.Length} translated keyword headers, {mechanics.Count} distinct mechanics; actual card text detection and links. Uses whole translated face because line order can differ.";
        File.WriteAllText(".tools/keyword-glossary/catalog-translation-result.txt",result);
        return result;
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static void Has(string text,string key){Require(DeckKeywordGlossary.Find(text).Any(e=>e.English==key),"Not detected: "+text);}
    private static void Only(string text,string key){var found=DeckKeywordGlossary.Find(text);Require(found.Count==1&&found[0].English==key,"Incorrect detection: "+text+" -> "+string.Join(",",found.Select(e=>e.English)));}
}
#endif
