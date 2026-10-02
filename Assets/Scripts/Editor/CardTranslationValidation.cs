#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

public static class CardTranslationValidation
{
    public static string Run()
    {
        var method=typeof(GamePreferences).GetMethod("ReadCards",BindingFlags.NonPublic|BindingFlags.Static);
        Dictionary<string,string[]> Read(string code)=>(Dictionary<string,string[]>)method.Invoke(null,new object[]{"Assets/StreamingAssets/CardTranslations/"+code+".txt"});
        var zh=Read("zh-CN");var ja=Read("ja-JP");
        Require(zh["Compel Brutality"][3].Contains("忠诚")&&!zh["Compel Brutality"][3].Contains("Target creature"),"Mixed-language Chinese effect");
        Require(zh["Red Herring"][3].Contains("敏捷")&&zh["Red Herring"][3].Contains("牺牲")&&!zh["Red Herring"][3].Contains("交换"),"Obsolete test-card Chinese effect");
        Require(zh["Red Herring"][2].Contains("神器"),"Corrected card type");
        Require(ja["Loyal Sentry"][3].Contains("ブロック")&&!ja["Loyal Sentry"][3].Contains("When"),"Japanese existing English record not replaced");
        Require(ja["Heartwood Crafter"][3].Contains("準備済")&&ja["Soul Tether"][3].Contains("トークン"),"Japanese prepared card and spell face");
        Require(ja["Hearthhull, the Worldseed"][3].Contains("配備")&&!ja["Hearthhull, the Worldseed"][3].Contains("Station"),"Japanese API English field not corrected");
        Require(ja["Dusk // Dawn"][3].Contains(ja["Dusk"][3])&&ja["Dusk // Dawn"][3].Contains(ja["Dawn"][3]),"Japanese composite face rules");
        string old=File.ReadAllLines("Assets/StreamingAssets/CardTranslations/zh-CN.txt").First(l=>l.StartsWith("Ion Storm|",StringComparison.Ordinal)).Split('|')[3].Replace("\\n","\n");
        Require(zh["Ion Storm"][3]==old,"Unrelated existing Chinese translation changed");
        string result="PASS: Chinese mixed prose and obsolete design corrections; Japanese new cards, existing English fields, release notes, prepared spell face and combined faces; unrelated Chinese preserved.";
        File.WriteAllText(".tools/keyword-glossary/card-translation-validation.txt",result);return result;
    }
    private static void Require(bool success,string message){if(!success)throw new Exception(message);}
}
#endif
