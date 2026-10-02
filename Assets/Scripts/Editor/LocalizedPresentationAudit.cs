#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Batched on editor updates so a full catalog audit does not freeze the editor.
public static class LocalizedPresentationAudit
{
    [Serializable] public sealed class Gap { public string key, name, language, field, detail; }
    [Serializable] public sealed class Report
    {
        public int cards, faces, keywordComparisons, tokenComparisons;
        public bool complete;
        public List<Gap> untranslated = new List<Gap>();
        public List<Gap> keywordReview = new List<Gap>();
        public List<Gap> tokenReview = new List<Gap>();
        public string limitation = "Automated display and matching audit; differing Oracle versions and synonyms require review. It does not verify every translation's meaning or every game's resolution.";
    }
    static List<CardDefinition> queue;
    static int cursor;
    static Report report;
    static Report baseline;
    static Dictionary<string, Dictionary<string, string[]>> translations;
    static FieldInfo cardsField = typeof(GamePreferences).GetField("cards", BindingFlags.NonPublic | BindingFlags.Static);
    const string Output = "Tools/ForgeBridge/Reports/LocalizedPresentationAudit.json";

    public static string Start(bool recheck = false)
    {
        EditorApplication.update -= Tick;
        queue = CardDatabaseRepository.LoadAll().Where(c => !c.forgeAmbiguous)
            .GroupBy(c => c.cardName).Select(g => g.First()).ToList();
        baseline = recheck ? JsonUtility.FromJson<Report>(File.ReadAllText(Output)) : null;
        if(baseline!=null)
        {
            if(!baseline.complete)throw new InvalidOperationException("Full audit must finish before rechecking");
            var keys=new HashSet<string>(baseline.untranslated.Concat(baseline.keywordReview).Concat(baseline.tokenReview).Select(g=>g.key));
            queue=queue.Where(c=>keys.Contains(c.PrintingKey)).ToList();
        }
        var reader = typeof(GamePreferences).GetMethod("ReadCards", BindingFlags.NonPublic | BindingFlags.Static);
        translations = new Dictionary<string, Dictionary<string, string[]>>();
        foreach (var language in new[] { "zh-CN", "ja-JP" })
            translations[language] = (Dictionary<string, string[]>)reader.Invoke(null,
                new object[] { Path.Combine(Application.streamingAssetsPath, "CardTranslations", language + ".txt") });
        cursor = 0; report = new Report();
        EditorApplication.update += Tick;
        return "Started " + queue.Count + " unique cards";
    }
    public static string Status() => report == null ? "Not started" :
        cursor + "/" + queue.Count + "; complete=" + report.complete + "; missing fields=" + report.untranslated.Count +
        "; keyword reviews=" + report.keywordReview.Count + "; token reviews=" + report.tokenReview.Count;

    static void Tick()
    {
        var previous = cardsField.GetValue(null);
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                var card = queue[cursor++];
                foreach (var locale in translations)
                {
                    cardsField.SetValue(null, locale.Value);
                    Check(card, card.cardName, card.description, card.GetTypeLine(), locale.Key);
                    foreach (var face in card.features ?? new List<CardFeatureDefinition>())
                        if (face != null && face.cardName != card.cardName)
                            Check(card, face.cardName, face.description, "", locale.Key);
                }
                report.cards++;
            } while (cursor < queue.Count && watch.ElapsedMilliseconds < 80);
            EditorApplication.QueuePlayerLoopUpdate();
            if (cursor == queue.Count)
            {
                report.complete = true;
                EditorApplication.update -= Tick;
                if(baseline!=null)
                {
                    baseline.untranslated=report.untranslated;
                    baseline.keywordReview=report.keywordReview;
                    baseline.tokenReview=report.tokenReview;
                    baseline.limitation+=" Rechecked "+report.cards+" previously flagged cards after repairs. Flavor text and exhaustive visual/semantic review are outside this automated scan.";
                    report=baseline;
                }
                File.WriteAllText(Output, JsonUtility.ToJson(report, true));
            }
        }
        catch (Exception e)
        {
            EditorApplication.update -= Tick;
            File.WriteAllText(Output, JsonUtility.ToJson(report, true));
            Debug.LogException(e);
        }
        finally { cardsField.SetValue(null, previous); }
    }
    static void Check(CardDefinition card, string name, string englishRules, string englishType, string language)
    {
        report.faces++;
        translations[language].TryGetValue(name, out var fields);
        string localizedRules = fields != null && !string.IsNullOrWhiteSpace(fields[3]) ? fields[3] : englishRules;
        for (int column = 1; column <= 3; column++)
        {
            if (column == 3 && string.IsNullOrWhiteSpace(englishRules)) continue;
            string value = fields == null ? "" : fields[column];
            if(column==3&&!string.IsNullOrWhiteSpace(value)&&!Regex.IsMatch(Regex.Replace(value,@"\{[^}]*\}",""),@"[A-Za-z\u3040-\u30ff\u3400-\u9fff]"))continue;
            if (column == 3 && !Regex.IsMatch(Regex.Replace(englishRules ?? "", @"\{[^}]*\}", ""), @"[A-Za-z]")) continue;
            if (string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value, @"[\u3040-\u30ff\u3400-\u9fff]"))
                report.untranslated.Add(new Gap { key = card.PrintingKey, name = name, language = language,
                    field = column == 1 ? "name" : column == 2 ? "type" : "rules", detail = value });
        }
        var expected = DeckKeywordGlossary.Find(englishRules, "en", card);
        var actual = DeckKeywordGlossary.Find(localizedRules, language, card);
        var families = new HashSet<string>(actual.Select(e => Family(e.English)));
        foreach (var mechanic in expected)
        {
            report.keywordComparisons++;
            if (!families.Contains(Family(mechanic.English)))
                report.keywordReview.Add(new Gap { key = card.PrintingKey, name = name, language = language,
                    field = "keyword", detail = mechanic.English });
        }
        // Name-link expectations require an explicit token clause. Checking
        // ordinary cards would repeatedly resolve unrelated helper references.
        if (name != card.cardName || (englishRules ?? "").IndexOf("token", StringComparison.OrdinalIgnoreCase) < 0) return;
        var tokens = DeckTokenLinks.Find(card, englishRules);
        var localized = DeckTokenLinks.Find(card, localizedRules);
        foreach (var token in tokens)
        {
            report.tokenComparisons++;
            if (!localized.Any(t => t.TokenKey == token.TokenKey))
                report.tokenReview.Add(new Gap { key = card.PrintingKey, name = name, language = language,
                    field = "token", detail = token.English + " / " + token.TokenKey });
        }
    }
    static string Family(string key)
    {
        if (key.StartsWith("Affinity")) return "Affinity";
        if (key.StartsWith("Amass")) return "Amass";
        if (key.EndsWith("cycling", StringComparison.OrdinalIgnoreCase)) return "Cycling";
        if (key.EndsWith("walk", StringComparison.OrdinalIgnoreCase)) return "Landwalk";
        return key;
    }
}
#endif
