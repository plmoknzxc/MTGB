using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;

public static class AdvancedSearchValidation
{
    [MenuItem("MTGB/验证/卡牌/高级搜索")]
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
            Check(!new AdvancedCardSearch { Sort=2, Unique=true, Source=1 }.HasPrintingFilters,
                "Ordering, merging or selecting a source does not implicitly filter by platform");
            Check(!new AdvancedCardSearch { Query="Elf", ColorMode=2, Sets=" , ； " }.HasSearchOptions
                && new AdvancedCardSearch { Sets="ONE" }.HasSearchOptions,"Only effective advanced options mark the toolbar active");
            var eligible = cards.Where(new AdvancedCardSearch().Compile(cards)).ToList();
            void CheckSets(string input, params string[] codes)
            {
                var expected = eligible.Where(c => codes.Contains(c.setCode, StringComparer.OrdinalIgnoreCase));
                var actual = cards.Where(new AdvancedCardSearch { Sets=input }.Compile(cards));
                Check(expected.Any() && actual.SequenceEqual(expected), "Exact printing set selection: " + input);
            }
            CheckSets("one", "ONE");
            CheckSets("ONE；ELD", "ONE", "ELD");
            CheckSets("Phyrexia: All Will Be One", "ONE");
            var official=cards.Where(c=>!c.customContent&&CardSearchMetadata.Get(c)!=null).ToArray();
            for (int mask = 0; mask <= 7; mask++)
            {
                var predicate=new AdvancedCardSearch{Games=mask}.Compile(cards);
                Check(official.All(c=>predicate(c)==((CardSearchMetadata.Get(c).games&mask)!=0&&(CardSearchMetadata.Get(c).legalityMask&3)!=0)),"Platform union "+mask);
            }
            foreach(int state in new[]{0,1,2})
            {
                var predicate=new AdvancedCardSearch{Games=7,Status=state}.Compile(cards);
                int allowed=state==0?3:1<<state;
                Check(official.All(c=>predicate(c)==(CardSearchMetadata.Get(c).games!=0&&(CardSearchMetadata.Get(c).legalityMask&allowed)!=0)),"Legality without format "+state);
            }
            var lotus=official.First(c=>c.PrintingKey=="2ED/233");
            int vintage=Array.IndexOf(DeckFormatLegality.Keys,"vintage")+1;
            Check(Match(new AdvancedCardSearch{Format=vintage},lotus) && Match(new AdvancedCardSearch{Format=vintage,Status=1},lotus)
                && !Match(new AdvancedCardSearch{Format=vintage,Status=2},lotus),"Restricted cards are playable in Vintage");
            var paper=Find("History of Benalia");
            var digital=official.Single(c=>c.PrintingKey=="YBRO/31");
            var duel=new AdvancedCardSearch{Format=Array.IndexOf(DeckFormatLegality.Keys,"duelcommander")+1,Games=7};
            var paperFirst=duel.Compile(cards); var digitalFirst=duel.Compile(cards);
            Check(paperFirst(paper)&&!paperFirst(digital)&&!digitalFirst(digital)&&digitalFirst(paper),"Duel Commander checks each printing regardless of scan order");
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
            Check(Match(new AdvancedCardSearch { Name = "対抗呪文" }, counter)==(GamePreferences.Language=="ja-JP"), "Japanese name follows current locale");
            Check(Match(new AdvancedCardSearch { Name = "反击咒语" }, counter)==(GamePreferences.Language=="zh-CN"), "Chinese name follows current locale");
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
            Check(!Match(new AdvancedCardSearch{Status=1},fake)&&!Match(new AdvancedCardSearch{Status=2},fake),"Unclassified custom content does not match official restrictions");
            var zero=new AdvancedCardSearch{Loyalty="=0"}.Compile(cards);
            Check(!zero(Find("Nissa, Steward of Elements"))&&zero(Find("Jeska, Thrice Reborn")),"Printed X loyalty differs from printed zero");
            Check(!zero(Find("Garruk Relentless")) && !zero(Find("Arlinn Kord"))
                && !zero(Find("Oko, Lorwyn Liege")) && zero(Find("Dakkon, Shadow Slayer")),
                "Absent or nonzero reverse-face loyalty is not zero");
            foreach (var invalid in new[] { new AdvancedCardSearch { Value = "banana" }, new AdvancedCardSearch { Value = new string('9',110) }, new AdvancedCardSearch { From = "2024-02-31" }, new AdvancedCardSearch { From = "2025-01-01", To = "2024-01-01" }, new AdvancedCardSearch { Mana = "UU" }, new AdvancedCardSearch { Mana = "{W/W}" } })
            {
                bool rejected = false; try { invalid.Compile(cards); } catch (FormatException) { rejected = true; }
                Check(rejected, "Invalid input rejected before changing results");
            }
            var bolt = Find("Lightning Bolt"); var meta = CardSearchMetadata.Get(bolt);
            Check(Match(new AdvancedCardSearch { Rarity = Array.IndexOf(AdvancedCardSearch.Rarities, meta.rarity), Artist = meta.artist, Sets = meta.setName }, bolt), "Exact printing metadata");
            Check(!Match(new AdvancedCardSearch { From = "2100-01-01" }, bolt), "Release date filter");
            var onRelease = new AdvancedCardSearch { From="2023-02-10", To="2023-02-10", Sets="ONE", Rarity=3, Layout=1, Value="<=3" };
            var releaseMatches = eligible.Where(c => {
                var info = CardSearchMetadata.Get(c);
                return c.setCode=="ONE" && info != null && info.released==20230210 && info.rarity=="rare" && info.layout=="normal" && info.manaValue<=3;
            }).ToList();
            Check(releaseMatches.Count>0 && cards.Where(onRelease.Compile(cards)).SequenceEqual(releaseMatches),
                "Same-day inclusive release, set, layout, rarity and mana value");
            for (int mode=0;mode<4;mode++)
            {
                bool Expected(int mask) => mode==0 ? (mask&3)!=0 : mode==1 ? (mask&3)==3 : mode==2 ? mask==3 : (mask&~3)==0;
                var color=new AdvancedCardSearch {Colors=3,ColorMode=mode}.Compile(cards);
                var identity=new AdvancedCardSearch {Identity=3,IdentityMode=mode}.Compile(cards);
                Check(eligible.All(c => {var info=CardSearchMetadata.Get(c);return color(c)==(info!=null && Expected(info.colors)) && identity(c)==(info!=null && Expected(info.identity));}),
                    "White/blue colors and color identity mode "+mode);
            }
            var versions = official.Where(c=>c.setCode=="OM1"||c.setCode=="SPM").ToList();
            new AdvancedCardSearch{Sort=2,Unique=true}.Order(versions);
            var ordered=versions.OrderBy(c=>CardSearchMetadata.Get(c).manaValue).ThenBy(c=>c.PrintingKey,StringComparer.Ordinal);
            Check(versions.SequenceEqual(ordered)&&versions.All(c=>c.setCode=="SPM"),"Final representatives follow mana-value and printing-key order");
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
