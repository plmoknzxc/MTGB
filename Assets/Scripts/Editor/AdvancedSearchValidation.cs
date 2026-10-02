using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;

public static class AdvancedSearchValidation
{
    [MenuItem("MTGB/Validation/Advanced Card Search")]
    public static async void Run()
    {
        SessionState.SetString("AdvancedSearch.Validation", "running");
        try
        {
            await CardSearchMetadata.LoadAsync();
            var cards = CardDatabaseRepository.LoadAll();
            var results = new List<string>();
            void Check(bool ok, string name) { if (!ok) throw new Exception(name); results.Add("PASS " + name); }
            CardDefinition Find(string name) => cards.First(c => c.cardName == name && CardSearchMetadata.Get(c) is CardSearchMetadata.Entry m && (m.games&1)!=0 && (m.legalityMask&1)!=0);
            bool Match(AdvancedCardSearch query, CardDefinition card) => query.Compile(cards)(card);
            var counter = Find("Counterspell");
            Check(new AdvancedCardSearch().Games==1,"Default platform is paper");
            var official=cards.Where(c=>!c.customContent&&CardSearchMetadata.Get(c)!=null).ToArray();
            foreach(int mask in new[]{0,1,2,4,3,7})
            {
                var predicate=new AdvancedCardSearch{Games=mask}.Compile(cards);
                Check(official.All(c=>predicate(c)==((CardSearchMetadata.Get(c).games&mask)!=0&&(CardSearchMetadata.Get(c).legalityMask&1)!=0)),"Platform union "+mask);
            }
            foreach(int state in new[]{0,1,2})
            {
                var predicate=new AdvancedCardSearch{Games=7,Status=state}.Compile(cards);
                Check(official.All(c=>predicate(c)==(CardSearchMetadata.Get(c).games!=0&&(CardSearchMetadata.Get(c).legalityMask&(1<<state))!=0)),"Legality without format "+state);
            }
            Check(Match(new AdvancedCardSearch{Sets="神河"},Find("Fable of the Mirror-Breaker")),"Localized set name search");
            Check(SearchInsertionMenu.InsertText("{2}{U}", 3, 3, "{B/P}", false, out int caret) == "{2}{B/P}{U}" && caret == 8, "Insert mana at caret");
            Check(SearchInsertionMenu.InsertText("{2}{U}", 6, 3, "{Z}", false, out caret) == "{2}{Z}" && caret == 6, "Replace reverse selection");
            Check(SearchInsertionMenu.InsertText("Legendary Elf", 10, 10, "Creature", true, out caret) == "Legendary Creature Elf", "Type insertion adds word separators");
            foreach (var symbol in SearchInsertionMenu.ManaGroups().SelectMany(g => g.Values))
                new AdvancedCardSearch { Mana = symbol }.Compile(cards);
            Check(true, "Every offered mana symbol accepted by search parser");
            Check(Match(new AdvancedCardSearch { Name = "Counterspell", Colors = 2, ColorMode = 2, Value = "<=2", Mana = "{U}{U}", Type = "Instant" }, counter), "Combined name/color/value/mana/type");
            Check(!Match(new AdvancedCardSearch { Mana = "{U}{U}{U}" }, counter), "Repeated mana symbols counted");
            Check(!Match(new AdvancedCardSearch { Power = "=0" }, counter), "Noncreatures have no power");
            Check(Match(new AdvancedCardSearch { Name = "対抗呪文" }, counter), "Japanese search");
            Check(Match(new AdvancedCardSearch { Name = "反击咒语" }, counter), "Chinese search");
            Check(!Match(new AdvancedCardSearch { Name = "Instant" }, counter), "Name search cannot match type");
            Check(Match(new AdvancedCardSearch { Name = "Soporific Springs" }, Find("Sink into Stupor")), "Reverse face name");
            Check(Match(new AdvancedCardSearch { Colors = 32, Identity = 8 }, Find("Kozilek's Return")), "Devoid is colorless with colored identity");
            Check(AdvancedCardSearch.ColorMatches(3, 2, 0) && !AdvancedCardSearch.ColorMatches(3, 2, 2), "Any vs exact colors");
            Check(AdvancedCardSearch.ColorMatches(0, 2, 3) && !AdvancedCardSearch.ColorMatches(3, 2, 3), "Identity subset includes colorless");
            var fake = new CardDefinition { cardName = "test", customContent = true, features = new List<CardFeatureDefinition> {
                new CardFeatureDefinition {cardTypes=CardType.Creature,powerExpression="5",toughnessExpression="1"},
                new CardFeatureDefinition {cardTypes=CardType.Creature,powerExpression="1",toughnessExpression="5"} } };
            Check(!Match(new AdvancedCardSearch { Power = ">=5", Toughness = ">=5" }, fake), "Stats must belong to same face");
            fake.features[0].powerExpression = "*";
            Check(!Match(new AdvancedCardSearch { Power = "=0" }, fake), "Variable power is not numeric zero");
            foreach (var invalid in new[] { new AdvancedCardSearch { Value = "banana" }, new AdvancedCardSearch { From = "2024-02-31" }, new AdvancedCardSearch { From = "2025-01-01", To = "2024-01-01" }, new AdvancedCardSearch { Mana = "UU" } })
            {
                bool rejected = false; try { invalid.Compile(cards); } catch (FormatException) { rejected = true; }
                Check(rejected, "Invalid input rejected before changing results");
            }
            var bolt = Find("Lightning Bolt"); var meta = CardSearchMetadata.Get(bolt);
            Check(Match(new AdvancedCardSearch { Rarity = Array.IndexOf(AdvancedCardSearch.Rarities, meta.rarity), Artist = meta.artist, Sets = meta.setName }, bolt), "Exact printing metadata");
            Check(!Match(new AdvancedCardSearch { From = "2100-01-01" }, bolt), "Release date filter");
            foreach (var query in new[] { new AdvancedCardSearch(), new AdvancedCardSearch { Mana = "{U}" }, new AdvancedCardSearch { Power = ">=2", Toughness = ">=3" }, new AdvancedCardSearch { Format = 3, Colors = 2, Type = "Instant", Value = "<=2" } })
            {
                var timer = Stopwatch.StartNew(); var predicate = query.Compile(cards);
                var matches = cards.Where(predicate).ToList();
                Check(matches.Count > 0, "Whole catalog scan");
                results.Add(matches.Count + " matches / " + timer.ElapsedMilliseconds + " ms");
                query.Unique = true; query.Sort = 2; query.Order(matches);
                Check(matches.Select(c => c.cardName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == matches.Count, "Unique printings after sorting");
            }
            results.Add("Search metadata: " + cards.Count(c => CardSearchMetadata.Get(c) != null) + " / " + cards.Count);
            SessionState.SetString("AdvancedSearch.Validation", string.Join("\n", results));
        }
        catch (Exception ex) { SessionState.SetString("AdvancedSearch.Validation", "FAILED " + ex); UnityEngine.Debug.LogException(ex); }
    }
}
