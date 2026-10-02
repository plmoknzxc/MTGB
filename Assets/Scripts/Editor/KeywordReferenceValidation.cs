#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;

public static class KeywordReferenceValidation
{
    public static string Run()
    {
        var card=new CardDefinition {cardName="Ion Storm",setCode="5DN",collectorNumber=68};
        card.features.Add(new CardFeatureDefinition {cardName="离子风暴"});
        foreach(var name in new[]{"Ion Storm","离子风暴"})
        {
            var text=name+" deals 2 damage.\nStorm";
            var entries=DeckKeywordGlossary.Find(text,card:card);
            var markup=DeckKeywordGlossary.Markup(text,entries,card);
            Check(markup.StartsWith(name+" deals"),"Card-name link leaked: "+markup);
            Check(markup.Contains(">Storm</u>"),"Actual storm lost");
            Check(DeckKeywordGlossary.Find(name+" deals 2 damage.",card:card).Count==0,"False keyword summary");
        }
        string affinity="神器共鸣，生物共鸣，坟场风暴，风暴";
        foreach(var sample in new[]{
            ("Overcooked","Conjure a card named Food Fight onto the battlefield.","Fight"),
            ("Storm, Queen of Wakanda","X is Storm's power. Whenever Storm attacks, another creature gains flying.","Storm"),
            ("Cloak and Dagger, Entwined","When Cloak and Dagger enter, choose an opponent.","Cloak"),
            ("Trackhand Trainer","Conjure a card named Training Grounds onto the battlefield.","Training")})
            Check(!DeckKeywordGlossary.Find(sample.Item2,"en",new CardDefinition{cardName=sample.Item1}).Any(e=>e.English==sample.Item3),"Referenced or abbreviated card name counted as mechanic: "+sample.Item1);
        Check(!DeckKeywordGlossary.Find("Whenever a player plays a card exiled with Prowl, draw a card.","en",new CardDefinition{cardName="Prowl, Stoic Strategist"}).Any(e=>e.English=="Prowl"),"Object self-reference counted as Prowl");
        Check(!DeckKeywordGlossary.Find("Exile Tocasia, Digsite Mentor from your graveyard: Return artifacts.","en",new CardDefinition{cardName="Tocasia, Dig Site Mentor"}).Any(e=>e.English=="Mentor"),"Known source self-reference spelling counted as Mentor");
        foreach(var sample in new[]{("Open the plot booster.","Plot"),("Choose from the Paradigm Spellbook.","Paradigm"),("• Kodama's Reach (Search for two basic lands.)","Reach"),("Cast a copy of one of the following cards chosen at random—Insurrection, Warp World.","Warp")})
            Check(!DeckKeywordGlossary.Find(sample.Item1,"en",new CardDefinition{cardName="Test"}).Any(e=>e.English==sample.Item2),"Label or named list counted as keyword: "+sample.Item1);
        Check(DeckKeywordGlossary.Find("• Flying (This creature flies.)","en").Any(e=>e.English=="Flying"),"Real keyword bullet removed with headings");
        foreach(var sample in new[]{("Eivor, Wolf-Kissed","Whenever Eivor, Wolf-Kissed deals combat damage to a player, you mill that many cards.","Mill"),("Tecutlan, the Searing Rift","Whenever you cast a spell using mana produced by Tecutlan, the Searing Rift, discover X.","Discover"),("Takenuma, Abandoned Mire","Channel — {3}{B}, Discard Takenuma, Abandoned Mire: Mill three cards.","Mill")})
            Check(DeckKeywordGlossary.Find(sample.Item2,"en",new CardDefinition{cardName=sample.Item1}).Any(e=>e.English==sample.Item3),"Full legendary name swallowed the following real action: "+sample.Item1);
        foreach(var sample in new[]{("順応３","Adapt"),("発見５","Discover"),("これに容疑をかけてもよい。","Suspect"),("磨三张牌。","Mill"),("不会毁坏。","Indestructible")})
            Check(DeckKeywordGlossary.Find(sample.Item1,sample.Item1.Contains("牌")||sample.Item1.Contains("毁")?"zh-CN":"ja-JP").Any(e=>e.English==sample.Item2),"Actual translated action missing: "+sample.Item1);
        Check(!DeckKeywordGlossary.Find("Put a fuse counter and a plot counter on it.","en").Any(e=>e.English=="Fuse"||e.English=="Plot"),"Counter names counted as keywords");
        Check(DeckKeywordGlossary.Find("{3}{U/P}: Transform this creature.\nFlying","en").Any(e=>e.English=="Flying"),"Phyrexian mana hid next-line mechanic");
        Check(DeckKeywordGlossary.Find("エンチャント（飛行を持つクリーチャー）","ja-JP").Any(e=>e.English=="Flying"),"Enchant target restriction hidden as reminder");
        const string equipmentRules="この装備品が戦場に出たとき、パーマネント１つを対象とする。\n装備しているクリーチャーは＋１/＋０の修整を受け速攻を持つ。\n装備{1}";
        var equipmentKeywords=DeckKeywordGlossary.Find(equipmentRules,"ja-JP");
        var equipmentMarkup=DeckKeywordGlossary.Markup(equipmentRules,equipmentKeywords);
        Check(equipmentMarkup.Contains("この装備品"),"Equipment subtype falsely marked as Equip");
        Check(equipmentMarkup.Contains("装備している"),"Equipped prose falsely marked as Equip");
        Check(equipmentMarkup.Contains(">装備</u></color></link>{1}"),"Actual equip ability lost its link");
        Check(!DeckKeywordGlossary.Find("この装備品を装備しているクリーチャー。","ja-JP").Any(e=>e.English=="Equip"),"Equipment prose counted as ability");
        var equip=equipmentKeywords.Single(e=>e.English=="Equip");
        Check(equip.Japanese=="装備"&&equip.JapaneseHelp.Contains("装備コスト"),"Equip tooltip missing Japanese localization");
        foreach(var pair in new[]{("飛行機械を１体生成する。","Flying"),("多相の戦士１体を対象とする。","Changeling")})
            Check(!DeckKeywordGlossary.Find(pair.Item1,"ja-JP").Any(e=>e.English==pair.Item2),"Japanese subtype falsely marked: "+pair.Item1);
        foreach(var pair in new[]{("飛行","Flying"),("多相","Changeling"),("ストーム","Storm"),("感染","Infect"),("待機{2}","Suspend")})
            Check(DeckKeywordGlossary.Find(pair.Item1,"ja-JP").Any(e=>e.English==pair.Item2),"Japanese mechanic lost: "+pair.Item1);
        foreach(var entry in DeckKeywordGlossary.All)
            Check(System.Text.RegularExpressions.Regex.IsMatch(entry.JapaneseHelp,@"[ぁ-ヿ一-鿿]"),"Missing Japanese keyword help: "+entry.English);
        foreach(var text in new[]{"Amass Orcs 2","Affinity for artifacts","Islandcycling","Forestwalk"})
            foreach(var entry in DeckKeywordGlossary.Find(text,"ja-JP"))
                Check(System.Text.RegularExpressions.Regex.IsMatch(entry.JapaneseHelp,@"[ぁ-ヿ一-鿿]"),"Missing Japanese variant help: "+text);
        var highlighted=DeckKeywordGlossary.Markup(affinity,DeckKeywordGlossary.Find(affinity));
        foreach(var name in new[]{"神器共鸣","生物共鸣","坟场风暴","风暴"})Check(highlighted.Contains(">"+name+"</u>"),"Incomplete link: "+name);
        var source=new CardDefinition {cardName="Young Pyromancer",setCode="M14",collectorNumber=163};
        foreach(var text in new[]{"Create a 1/1 red Elemental creature token.","派出一个1/1红色元素衍生生物。","赤の1/1のエレメンタル・クリーチャー・トークンを１体生成する。"})
        {
            var tokens=DeckTokenLinks.Find(source,text);
            Check(tokens.Count==1&&tokens[0].TokenKey=="TM14/8","Incorrect local token link");
            Check(DeckKeywordGlossary.Markup(text,tokens,source).Contains("<link="),"Token name not highlighted");
        }
        Check(DeckTokenLinks.Find(source,"An Elemental deals damage.").Count==0,"Unrelated creature type linked");
        var crow=CardDatabaseRepository.LoadAll().First(c=>c.cardName=="Murmuration");
        Check(!DeckKeywordGlossary.Find(crow.description,"en",crow).Any(e=>e.English=="Storm"),"Referenced Storm Crow became Storm");
        Check(!DeckKeywordGlossary.Find("Gift of Chaos — Draw a card.","en").Any(e=>e.English=="Gift"),"Ability-word heading became Gift");
        Check(DeckKeywordGlossary.Find("与雅各布·弗莱拍档","zh-CN").Any(e=>e.English=="Partner with"),"Named Chinese partner variant lost");
        Check(DeckKeywordGlossary.Find("ピールとの共闘","ja-JP").Any(e=>e.English=="Partner with"),"Named Japanese partner variant lost");
        foreach(var sample in new[]{("督励してもよい。","Exert"),("給餌してもよい。","Forage"),("你可以觅粮。","Forage")})
            Check(DeckKeywordGlossary.Find(sample.Item1).Any(e=>e.English==sample.Item2),"Inflected or published mechanic name lost: "+sample.Item1);
        Check(DeckTokenLinks.Find(source,"エレメンタルは１点のダメージを与える。").Count==0,"Unrelated Japanese creature type linked");
        foreach(var sample in new[]{("The Hive","10E",324,"派出一个1/1无色，名称为黄蜂，具飞行异能的昆虫衍生神器生物。","黄蜂"),
            ("Dark Depths","2XM",314,"派出传奇衍生生物玛莉雷基，其为20/20黑色，具飞行与不灭异能的圣者。","玛莉雷基"),
            ("Pink Horror","40K",136,"派出两个2/2，蓝红双色，名称为蓝色惧妖的恶魔／惊惧兽衍生生物。","蓝色惧妖")})
        {
            var actual=CardDatabaseRepository.LoadAll().First(c=>c.cardName==sample.Item1&&c.PrintingKey==sample.Item2+"/"+sample.Item3);
            Check(DeckTokenLinks.Find(actual,sample.Item4).Any(e=>e.Aliases.Contains(sample.Item5)),"Historic Chinese named token lost: "+sample.Item1);
        }
        foreach(var sample in new[]{("Flight","飛行","エンチャントされているクリーチャーは飛行を持つ。","ja-JP","Flying"),
            ("Regeneration","重生","{G}：重生所结附的生物。","zh-CN","Regenerate")})
        {
            var actual=new CardDefinition{cardName=sample.Item1};actual.features.Add(new CardFeatureDefinition{cardName=sample.Item2});
            Check(DeckKeywordGlossary.Find(sample.Item3,sample.Item4,actual).Any(e=>e.English==sample.Item5),"Mechanic identical to printed name was suppressed: "+sample.Item1);
        }
        var fable=new CardDefinition{cardName="Fable of the Mirror-Breaker",setCode="NEO",collectorNumber=465};
        const string fableRules="I ― 「このクリーチャーが攻撃するたび、宝物トークン１つを生成する。」を持つ赤の2/2のゴブリン・シャーマン・クリーチャー・トークン１体を生成する。";
        var fableTokens=DeckTokenLinks.Find(fable,fableRules);
        foreach(var name in new[]{"宝物","ゴブリン・シャーマン"})
            Check(fableTokens.Any(e=>e.Aliases.Contains(name)),"Missing Japanese compound/token name: "+name);
        var fableMarkup=DeckKeywordGlossary.Markup(fableRules,fableTokens,fable);
        foreach(var name in new[]{"宝物","ゴブリン・シャーマン"})
            Check(fableMarkup.Contains(">"+name+"</u>"),"Missing Japanese token link span: "+name);
        CheckJapanese("VOW",310,"Bloodtithe Harvester","血トークン１つを生成する。","血");
        CheckJapanese("KLD",201,"Cogworker's Puzzleknot","霊気装置アーティファクト・クリーチャー・トークンを１体生成する。","霊気装置");
        CheckJapanese("MOM",177,"Blighted Burgeoning","培養器・トークン１つを生成する。","培養器");
        CheckJapanese("SLD",879,"Minsc & Boo, Timeless Heroes","「ブー」という名前でトランプルと速攻を持つハムスター・クリーチャー・トークン１体を生成する。","ブー");
        CheckJapanese("PRM",105802,"Mishra, Eminent One","「ミシュラの戦機」という名前で４/４であることを除き、それのコピーであるトークン１つを生成する。","ミシュラの戦機");
        CheckJapanese("NEO",141,"Fable of the Mirror-Breaker","「このクリーチャーが攻撃するたび、宝物トークンを生成する。」を持つゴブリン・シャーマン・クリーチャー・トークンを生成する。","ゴブリン・シャーマン");
        CheckJapanese("MH3",186,"Genku, Future Shaper","以下から１つを選ぶ。それらの特性を持つクリーチャー・トークン１体を生成する。\n・警戒を持つ白の２/２の狐。\n・飛行を持つ青の１/２のムーンフォーク。\n・絆魂を持つ黒の１/１のネズミ。","狐","ムーンフォーク","ネズミ");
        CheckJapanese("ELD",382,"Outlaws' Merriment","以下から１つを選ぶ。クリーチャー・トークン１体を生成する。\n・トランプルを持つ人間・戦士\n・絆魂を持つ人間・クレリック\n・速攻を持つ人間・ならず者","人間・戦士","人間・クレリック","人間・ならず者");
        CheckJapanese("MKM",91,"Leering Onlooker","コウモリ・クリーチャー・トークン２体を生成する。","コウモリ");
        CheckJapanese("J22",743,"Wolverine Riders","エルフ・戦士・クリーチャー・トークン１体を生成する。","エルフ・戦士");
        CheckJapanese("MOM",177,"Blighted Burgeoning","培養器・トー\nクン１つを生成する。","培養器");
        CheckJapanese("EOE",205,"Seedship Impact","着陸\n船・トークン１つを生成する。","着陸\n船");
        Check(DeckTokenLinks.Find(card,"Create an Elemental token.").Count==0,"Unrelated card gained token association");
        foreach(var sample in new[]{
            ("FRA",363,"The Theorist, Jace Beleren","虚影","TFRA/4"),
            ("PRM",83824,"Skyclave Apparition","虚影",""),
            ("INV",295,"Assault // Battery","象",""),
            ("SOS",337,"Ambitious Augmenter","分灵",""),
            ("ECL",402,"Personify","变形兽",""),
            ("DSC",65,"Phenomenon Investigators","惊惧兽",""),
            ("STX",306,"Sedgemoor Witch","害虫",""),
            ("DOM",102,"Rite of Belzenlok","僧侣","")})
        {
            var parent=new CardDefinition{setCode=sample.Item1,collectorNumber=sample.Item2,cardName=sample.Item3};
            string rules="派出一个1/1蓝色"+sample.Item4+"衍生生物。";
            var found=DeckTokenLinks.Find(parent,rules);
            Check(found.Any(e=>e.Aliases.Contains(sample.Item4)&&(sample.Item5==""||e.TokenKey==sample.Item5)),"Missing token link: "+sample.Item3+" / "+sample.Item4);
            Check(DeckKeywordGlossary.Markup(rules,found,parent).Contains(">"+sample.Item4+"</u>"),"Token span not marked");
        }
        var popupObject=new UnityEngine.GameObject("DestroyedTokenPopupTest",typeof(UnityEngine.RectTransform));
        var popup=popupObject.AddComponent<DeckHoverArtPopup>();
        UnityEngine.Object.DestroyImmediate(popupObject);
        // OnDestroy/OnDisable callbacks can arrive after the popup's native object died.
        popup.Hide();popup.RequestHide();popup.Hide();
        const string result="PASS: self-name exclusion, real mechanics, English/Chinese/Japanese token links, Japanese compound token names and unrelated-name negatives.";
        Directory.CreateDirectory(".tools/keyword-reference");File.WriteAllText(".tools/keyword-reference/result.txt",result);
        return result;
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static void CheckJapanese(string set,int number,string name,string rules,params string[] labels)
    {
        var card=new CardDefinition{setCode=set,collectorNumber=number,cardName=name};
        var links=DeckTokenLinks.Find(card,rules);
        string markup=DeckKeywordGlossary.Markup(rules,links,card);
        foreach(string label in labels)Check(markup.Contains(">"+label+"</u>"),"Missing Japanese token label: "+name+" / "+label);
    }
}
#endif
