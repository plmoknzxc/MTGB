using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Short hover explanations for the keywords visible in the selected card's rules text.
// Local reviewed data and coverage baseline: Tools/ForgeBridge/Build-KeywordGlossary.py
public static class DeckKeywordGlossary
{
    public sealed class Entry
    {
        public readonly string English, Chinese, Japanese;
        public readonly string EnglishHelp, ChineseHelp, JapaneseHelp;
        public string[] Aliases = Array.Empty<string>();
        public bool AutoDisplay = true;
        public string TokenKey;
        public Entry(string en, string zh, string ja, string enHelp, string zhHelp, string jaHelp)
        { English=en; Chinese=zh; Japanese=ja; EnglishHelp=enHelp; ChineseHelp=zhHelp; JapaneseHelp=string.IsNullOrEmpty(jaHelp)?enHelp:jaHelp; }
        public string Name => GamePreferences.Language.StartsWith("zh") ? Chinese : GamePreferences.Language.StartsWith("ja") ? Japanese : English;
        public string Help => GamePreferences.Language.StartsWith("zh") ? ChineseHelp : GamePreferences.Language.StartsWith("ja") ? JapaneseHelp : EnglishHelp;
    }

    [Serializable] private sealed class Row
    {
        public string en, zh, ja, enHelp, zhHelp, jaHelp;
        public string[] aliases;
        public bool autoDisplay;
    }
    [Serializable] private sealed class Catalog { public Row[] entries; }
    private static readonly List<Entry> entries = new List<Entry>();
    private static readonly Dictionary<string,List<Entry>> terms = new Dictionary<string,List<Entry>>(StringComparer.OrdinalIgnoreCase);
    private static Regex matcher;
    private static readonly Regex amassEnglish = new Regex(@"(?<![A-Za-z])amass\s+(?<type>[A-Za-z]+(?:[ -][A-Za-z]+)*)\s+(?<n>\d+|x)(?![A-Za-z])", RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    private static readonly Regex amassChinese = new Regex(@"囤兵(?<type>[\p{IsCJKUnifiedIdeographs}]+)(?<n>\d+|[Xx])", RegexOptions.CultureInvariant);
    private static readonly Regex cyclingEnglish = new Regex(@"(?<![A-Za-z])(?<type>[A-Za-z]+(?:\s+[A-Za-z]+)?cycling)(?![A-Za-z])", RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    // Chinese typecycling names occur in both “循环树林” and “裂片妖循环” forms.
    // Keep this closed to known mechanic names: a broad Chinese+循环 pattern also
    // captures ordinary prose such as “当你循环此牌时”.
    private static readonly Regex cyclingChinese = new Regex(@"(?<type>循环(?:神器地|雪境地|基本地|树林|海岛|平原|沼泽|山脉|裂片妖|法术师|半身人)|(?:神器地|雪境地|基本地|树林|海岛|平原|沼泽|山脉|裂片妖|法术师|半身人)循环)", RegexOptions.CultureInvariant);
    private static readonly Regex landwalkEnglish = new Regex(@"(?<![A-Za-z])(?<type>Snow-covered landwalk|Nonbasic landwalk|Snow landwalk|Mountainwalk|Desertwalk|Forestwalk|Islandwalk|Plainswalk|Swampwalk)(?![A-Za-z])", RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    private static readonly Regex landwalkChinese = new Regex(@"(?<type>雪境地行者|非基本地行者|山脉行者|沙漠行者|树林行者|海岛行者|平原行者|沼泽行者)", RegexOptions.CultureInvariant);
    private static readonly Regex affinityEnglish = new Regex(@"(?<![A-Za-z])Affinity\s+for\s+(?<type>[A-Za-z][A-Za-z -]*?)(?=\s*\(|[.;\r\n]|$)", RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    private const string AffinityChineseKinds = "出身非神器的神器生物|史迹永久物|历史永久物|神器生物|非瑞克西亚人|非瑞人|鹏洛客|雪境地|衍生物|裂片妖|城门|门|武具|盟友|伙伴|树林|海岛|沼泽|山脉|平原|骑士|灵俑|精怪|生物|鸟|羊|匪徒|狂徒|猫|蛙|食品|蜥蜴|城镇|市镇|达雷克|戴立克|人类|公民|市民|精灵|妖精|神器|结界";
    private static readonly Regex affinityChinese = new Regex(@"(?<type>共鸣(?:"+AffinityChineseKinds+")|(?:"+AffinityChineseKinds+")共鸣)", RegexOptions.CultureInvariant);
    public static IReadOnlyList<Entry> All { get { EnsureLoaded(); return entries; } }

    private static void EnsureLoaded()
    {
        if(matcher!=null)return;
        var asset=Resources.Load<TextAsset>("KeywordGlossary");
        if(asset==null)throw new InvalidOperationException("Missing local keyword glossary.");
        var catalog=JsonUtility.FromJson<Catalog>(asset.text);
        foreach(var row in catalog.entries)
        {
            var entry=new Entry(row.en,string.IsNullOrEmpty(row.zh)?row.en:row.zh,
                string.IsNullOrEmpty(row.ja)?row.en:row.ja,row.enHelp,row.zhHelp,
                string.IsNullOrEmpty(row.jaHelp)?row.enHelp:row.jaHelp);
            entry.Aliases=row.aliases??Array.Empty<string>();
            entry.AutoDisplay=row.autoDisplay;
            entries.Add(entry);
            if(!row.autoDisplay)continue;
            AddTerm(entry.English,entry);AddTerm(entry.Chinese,entry);AddTerm(entry.Japanese,entry);
            foreach(var alias in entry.Aliases)AddTerm(alias,entry);
        }
        Resources.UnloadAsset(asset);
        var names=new List<string>(terms.Keys);
        names.Sort((a,b)=>b.Length.CompareTo(a.Length));
        var patterns=new List<string>();
        foreach(var name in names)patterns.Add(TermPattern(name));
        matcher=new Regex(string.Join("|",patterns),RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    }
    private static void AddTerm(string term,Entry entry)
    {
        if(string.IsNullOrWhiteSpace(term))return;
        if(!terms.TryGetValue(term,out var matches))terms.Add(term,matches=new List<Entry>());
        if(!matches.Contains(entry))matches.Add(entry);
    }
    private static string TermPattern(string name)
    {
        // Creature subtype names containing a mechanic are not that mechanic.
        if(name=="飛行")return @"飛行(?!機械)";
        if(name=="多相")return @"多相(?!の戦士)";
        if(name=="Plot")return @"(?<![A-Za-z])Plot(?![A-Za-z]|\s+(?:counters?|booster)\b)";
        if(name=="Paradigm")return @"(?<![A-Za-z])Paradigm(?![A-Za-z]|\s+Spellbook\b)";
        // 装備品 is a card subtype; 装備している describes an attached creature.
        // Only the ability/cost or an explicit reference to that ability is a keyword.
        if(name=="装備")return @"装備(?=[ \t]*(?:\\?\{|[0-9０-９XＸ]|[—―]|能力|コスト|を持|[、，,。;；「」『』]|$))";
        bool latin=Regex.IsMatch(name,@"[A-Za-z]");
        return latin?@"(?<![A-Za-z])"+Regex.Escape(name)+@"(?![A-Za-z]|\s+counters?\b)":Regex.Escape(name);
    }
    // Preserve offsets for links; ignore nested reminder text, not just the first ')'.
    private static string WithoutReminders(string text)
    {
        var chars=text.ToCharArray();var opens=new Stack<int>();bool mana=false;
        for(int i=0;i<chars.Length;i++)
        {
            char c=text[i];
            if(c=='{'){mana=true;continue;}if(c=='}'){mana=false;continue;}if(mana)continue;
            if(c=='('||c=='（')opens.Push(i);
            if((c==')'||c=='）')&&opens.Count>0)
            {
                int start=opens.Pop();
                if(opens.Count>0)continue;
                string prefix=text.Substring(Math.Max(0,start-14),Math.Min(start,14)).TrimEnd();
                if(prefix.EndsWith("エンチャント",StringComparison.Ordinal)||prefix.EndsWith("Enchant",StringComparison.OrdinalIgnoreCase))continue;
                for(int j=start;j<=i;j++)chars[j]=' ';
            }
        }
        return new string(chars);
    }
    private static string VisibleRules(string rules,CardDefinition card,bool maskTokenNames=true)
    {
        string visible=Regex.Replace(rules,@"(?m)^(?<bullet>[•・]\s*)(?<heading>[A-Z][A-Za-z'’ ,]+)(?=\s*\()",m=>terms.ContainsKey(m.Groups["heading"].Value.Trim())?m.Value:m.Groups["bullet"].Value+new string(' ',m.Groups["heading"].Length));
        visible=WithoutReminders(visible);
        if(maskTokenNames)visible=Regex.Replace(visible,@"《[^》]+》",m=>new string(' ',m.Length));
        // English ability-word headings are descriptive labels, not keywords.
        visible=Regex.Replace(visible,@"(?m)^(?:[•・]\s*)?[A-Za-z][A-Za-z,'’ -]+(?= [—―]| ~)",m=>
        {
            string heading=m.Value.Trim().TrimStart('•','・').Trim();
            if(terms.ContainsKey(heading)||Regex.IsMatch(heading,@"^(?:Affinity for |Partner with |Gift a |Champion an? |Protection from )",RegexOptions.IgnoreCase))return m.Value;
            return new string(' ',m.Length);
        });
        if(card==null)return visible;
        // Referenced card names and the short name of a rebalanced card are
        // identities, not mechanic words (Proud Mentor, A-Dwarfhold Champion).
        if(maskTokenNames)
        {
            visible=Regex.Replace(visible,@"(?im)(?<=chosen at random[—―])[^.\r\n]+",m=>new string(' ',m.Length));
            visible=Regex.Replace(visible,@"(?i)\b(?<prefix>named|partner with) +(?<name>[^.\r\n(]+?)(?= with\b| and put\b| and a spell\b| onto\b|, a creature\b|\. |\.$|[\r\n(]|$)",m=>m.Groups["prefix"].Value+new string(' ',m.Length-m.Groups["prefix"].Length));
            var identities=new List<string>{card.cardName};
            if(card.features!=null)foreach(var face in card.features)if(face!=null)identities.Add(face.cardName);
            foreach(string identity in identities)
            {
                if(string.IsNullOrEmpty(identity))continue;
                if(identity.StartsWith("A-",StringComparison.Ordinal))
                    visible=visible.Replace(identity.Substring(2),new string(' ',identity.Length-2));
                int comma=identity.IndexOf(',');
                if(comma<=0)continue;
                string shortName=identity.Substring(0,comma);
                visible=Regex.Replace(visible,@"(?<=with )"+Regex.Escape(shortName)+@"(?=[,.])",m=>new string(' ',m.Length),RegexOptions.IgnoreCase);
                visible=Regex.Replace(visible,@"(?<![A-Za-z])"+Regex.Escape(shortName)+@"(?='s\b|\s+(?:enter\b|enters\b|attacks\b|deals\b|leave\b|leaves\b|would\b|is\b|has\b)|[.:])",m=>new string(' ',m.Length),RegexOptions.IgnoreCase);
            }
            if(card.cardName=="1996 World Champion")visible=visible.Replace("World Champion",new string(' ',14));
        }
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase){card.cardName,GamePreferences.CardName(card)};
        // The canonical English source spells this self-reference without a space.
        if(card.cardName=="Tocasia, Dig Site Mentor")names.Add("Tocasia, Digsite Mentor");
        if(card.features!=null)foreach(var face in card.features)
            if(face!=null){names.Add(face.cardName);names.Add(GamePreferences.FeatureName(face));}
        if(maskTokenNames)foreach(string name in DeckRelatedTokenArt.GetTokenNames(card))
        {
            names.Add(name);
            names.Add(GamePreferences.CardName(new CardDefinition{cardName=name}));
        }
        foreach(var name in names)
            if(!string.IsNullOrWhiteSpace(name))
            {
                // A printed name can itself be a mechanic (Flight is 飛行;
                // Regeneration is 再生/重生). Keep real uses in the rules.
                if(terms.ContainsKey(name))continue;
                // Names are literals. Avoid compiling a separate regex for every
                // printing whenever the detail view or hover is refreshed.
                bool latin=false;
                foreach(char c in name)if(IsLatinLetter(c)){latin=true;break;}
                var masked=visible.ToCharArray();
                for(int offset=0;offset<=visible.Length-name.Length;)
                {
                    int index=visible.IndexOf(name,offset,StringComparison.OrdinalIgnoreCase);
                    if(index<0)break;
                    int end=index+name.Length;
                    if(!latin||(index==0||!IsLatinLetter(visible[index-1]))&&(end==visible.Length||!IsLatinLetter(visible[end])))
                        for(int i=index;i<end;i++)masked[i]=' ';
                    offset=end;
                }
                visible=new string(masked);
            }
        return visible;
    }
    private static bool IsLatinLetter(char c)=>(c>='A'&&c<='Z')||(c>='a'&&c<='z');

    public static List<Entry> Find(string rules, string language = null, CardDefinition card = null)
    {
        EnsureLoaded();var found=new List<Entry>();
        if(string.IsNullOrWhiteSpace(rules))return found;
        language=language??GamePreferences.Language;
        string visible=VisibleRules(rules,card);
        var masked=visible.ToCharArray();
        foreach(Match match in Regex.Matches(visible,@"磨(?:掉)?(?:[一二三四五六七八九十百千万两零0-9Xx]+张牌|[一二三四五六七八九十百千万两零0-9Xx]+張牌|若干牌|等量的牌|等同于|两倍于|两倍的|其牌库数量|该数量|等量|你牌库顶的)"))
        {
            var original=entries.Find(e=>e.English=="Mill");
            found.Add(new Entry(original.English,original.Chinese,original.Japanese,original.EnglishHelp,original.ChineseHelp,original.JapaneseHelp){Aliases=new[]{match.Value}});
            for(int i=match.Index;i<match.Index+match.Length;i++)masked[i]=' ';
        }
        foreach(var pattern in new[]{@"与其他[^，。\r\n（）()]+结合",@"他の[^。\r\n（）()]+とのバンド"})
            foreach(Match match in Regex.Matches(visible,pattern))
            {
                var original=entries.Find(e=>e.English=="Bands with other");
                found.Add(new Entry(original.English,original.Chinese,original.Japanese,original.EnglishHelp,original.ChineseHelp,original.JapaneseHelp){Aliases=new[]{match.Value}});
                for(int i=match.Index;i<match.Index+match.Length;i++)masked[i]=' ';
            }
        foreach(var pattern in new[]{@"与[^，。\r\n（）()]+拍档",@"[^。\r\n（）()]+との共闘"})
            foreach(Match match in Regex.Matches(visible,pattern))
            {
                var original=entries.Find(e=>e.English=="Partner with");
                if(original==null)continue;
                var variant=new Entry(original.English,original.Chinese,original.Japanese,original.EnglishHelp,original.ChineseHelp,original.JapaneseHelp){Aliases=new[]{match.Value}};
                found.Add(variant);
                for(int i=match.Index;i<match.Index+match.Length;i++)masked[i]=' ';
            }
        visible=new string(masked);
        AddParameterized(amassEnglish,visible,masked,"amass");
        AddParameterized(amassChinese,visible,masked,"amass-zh");
        AddParameterized(cyclingEnglish,visible,masked,"cycling");
        AddParameterized(cyclingChinese,visible,masked,"cycling-zh");
        AddParameterized(landwalkEnglish,visible,masked,"landwalk");
        AddParameterized(landwalkChinese,visible,masked,"landwalk-zh");
        AddParameterized(affinityEnglish,visible,masked,"affinity");
        AddParameterized(affinityChinese,visible,masked,"affinity-zh");
        foreach(Match match in matcher.Matches(new string(masked)))
        {
            if(!terms.TryGetValue(match.Value,out var candidates))continue;
            // The same characters can mean different mechanics across languages
            // (Chinese Meld and Japanese Fuse both use 融合).
            var entry=candidates.Find(e=>string.Equals(language.StartsWith("zh")?e.Chinese:language.StartsWith("ja")?e.Japanese:e.English,match.Value,StringComparison.OrdinalIgnoreCase))??candidates[0];
            if(!found.Contains(entry))found.Add(entry);
        }
        return found;

        void AddParameterized(Regex regex,string source,char[] mask,string family)
        {
            foreach(Match match in regex.Matches(source))
            {
                string type=match.Groups["type"].Value;
                Entry variant=CreateVariant(family,type);
                variant.Aliases=new[]{match.Value.TrimEnd('0','1','2','3','4','5','6','7','8','9').Trim()};
                if(!found.Exists(e=>e.English==variant.English && e.Chinese==variant.Chinese))found.Add(variant);
                for(int i=match.Index;i<match.Index+match.Length;i++)mask[i]=' ';
            }
        }
    }

    private static Entry CreateVariant(string family,string rawType)
    {
        string type=Regex.Replace(rawType.Trim(),@"\s+"," ");
        if(family.StartsWith("amass",StringComparison.Ordinal))
        {
            bool chinese=family.EndsWith("-zh",StringComparison.Ordinal);
            string enType=chinese?AmassEnglishType(type):type;
            string zhType=chinese?type:AmassChineseType(type);
            return new Entry("Amass "+enType,"囤兵"+zhType,"動員（指定された種類）",
                "To amass "+enType+" N, create a 0/0 black "+enType+" Army token if you control no Army. Then put N +1/+1 counters on an Army you control and make it "+enType+" in addition to its other types.",
                "囤兵"+zhType+"时，若你未操控军队，则派出一个0/0黑色"+zhType+"／军队衍生生物；然后在一个由你操控的军队上放置所述数量的+1/+1指示物，并使其额外具有"+zhType+"类别。",
                "軍団をコントロールしていないなら、指定された種類の黒の０/０の軍団・トークン１体を生成する。その後、自分の軍団１体に指定数の＋１/＋１カウンターを置き、それを他のタイプに加えて指定された種類にする。");
        }
        if(family.StartsWith("landwalk",StringComparison.Ordinal))
        {
            bool chineseWalk=family.EndsWith("-zh",StringComparison.Ordinal);
            string enWalk=chineseWalk?LandwalkEnglish(type):type;
            string zhWalk=chineseWalk?type:LandwalkChinese(type);
            string targetZh=zhWalk.Replace("地行者","");
            return new Entry(enWalk,zhWalk,"土地渡り（指定された土地）",
                "This creature can't be blocked as long as defending player controls "+LandwalkTarget(enWalk)+".",
                "只要防御牌手操控"+LandwalkTargetChinese(enWalk,targetZh)+"，此生物便不能被阻挡。",
                "防御プレイヤーが指定された土地をコントロールしているかぎり、このクリーチャーはブロックされない。");
        }
        if(family.StartsWith("affinity",StringComparison.Ordinal))
        {
            bool chinese=family.EndsWith("-zh",StringComparison.Ordinal);
            string raw=type;
            if(chinese)raw=raw.Replace("共鸣","");
            string enType=chinese?AffinityEnglishType(raw):type;
            string zhType=chinese?raw:AffinityChineseType(type);
            return new Entry("Affinity for "+enType,"共鸣"+zhType,"親和（指定された種類）",
                "This spell costs {1} less to cast for each permanent you control of that kind.",
                "此咒语每有一个由你操控的"+zhType+"，其施放费用便减少{1}。",
                "あなたがコントロールする指定された種類のパーマネント１つにつき、この呪文を唱えるコストは{1}少なくなる。");
        }
        bool zh=family.EndsWith("-zh",StringComparison.Ordinal);
        string en=zh?CyclingEnglishType(type):type;
        string chineseType=zh?type:CyclingChineseType(type);
        string enTarget=en.Substring(0,en.Length-7);
        string zhTarget=chineseType.StartsWith("循环",StringComparison.Ordinal)?chineseType.Substring(2):chineseType.Substring(0,chineseType.Length-2);
        return new Entry(en,chineseType,"タイプ・サイクリング",
            "Pay the cycling cost and discard this card: search your library for a "+CyclingTarget(enTarget)+" card, reveal it, put it into your hand, then shuffle.",
            "支付循环费用并弃掉此牌：从你的牌库搜寻一张"+CyclingTargetChinese(zhTarget)+"牌，展示并置于手上，然后洗牌。",
            "サイクリング・コストを支払い、このカードを捨てる。指定されたタイプのカードをライブラリーから探し、公開して手札に加え、その後ライブラリーを切り直す。");
    }
    private static string LandwalkChinese(string en)
    {
        switch(en.ToLowerInvariant()){case "mountainwalk":return "山脉行者";case "desertwalk":return "沙漠行者";case "forestwalk":return "树林行者";case "islandwalk":return "海岛行者";case "plainswalk":return "平原行者";case "swampwalk":return "沼泽行者";case "snow landwalk":case "snow-covered landwalk":return "雪境地行者";case "nonbasic landwalk":return "非基本地行者";default:return en;}
    }
    private static string AffinityChineseType(string en)
    {
        switch(en.ToLowerInvariant())
        {
            case "enchantments":return "结界";case "allies":return "伙伴";
            case "spirits":return "精怪";case "historic permanents":return "史迹永久物";
            case "towns":return "市镇";case "elves":return "妖精";case "outlaws":return "狂徒";
            case "daleks":return "戴立克";case "phyrexians":return "非瑞人";case "citizens":return "市民";
        }
        switch(en.ToLowerInvariant()){case "artifacts":return "神器";case "artifact creatures that weren't originally artifact creatures":return "出身非神器的神器生物";case "snow lands":return "雪境地";case "gates":return "城门";case "equipment":return "武具";case "tokens":return "衍生物";case "allies":return "盟友";case "forests":return "树林";case "islands":return "海岛";case "swamps":return "沼泽";case "mountains":return "山脉";case "plains":return "平原";case "birds":return "鸟";case "knights":return "骑士";case "spirits":return "灵俑";case "planeswalkers":return "鹏洛客";case "artifact creatures":return "神器生物";case "creatures":return "生物";case "goats":return "羊";case "outlaws":return "匪徒";case "historic permanents":return "历史永久物";case "cats":return "猫";case "frogs":return "蛙";case "foods":return "食品";case "slivers":return "裂片妖";case "lizards":return "蜥蜴";case "towns":return "城镇";case "elves":return "精灵";case "daleks":return "达雷克";case "phyrexians":return "非瑞克西亚人";case "humans":return "人类";case "citizens":return "公民";default:return en;}
    }
    private static string AffinityEnglishType(string zh)
    {
        switch(zh)
        {
            case "结界":return "enchantments";case "伙伴":return "Allies";case "门":return "Gates";
            case "精怪":return "Spirits";case "史迹永久物":return "historic permanents";
            case "市镇":return "Towns";case "妖精":return "Elves";case "狂徒":return "outlaws";
            case "戴立克":return "Daleks";case "非瑞人":return "Phyrexians";case "市民":return "Citizens";
        }
        switch(zh){case "神器":return "artifacts";case "出身非神器的神器生物":return "artifact creatures that weren't originally artifact creatures";case "雪境地":return "snow lands";case "城门":return "Gates";case "武具":return "Equipment";case "衍生物":return "tokens";case "盟友":return "Allies";case "树林":return "Forests";case "海岛":return "Islands";case "沼泽":return "Swamps";case "山脉":return "Mountains";case "平原":return "Plains";case "鸟":return "Birds";case "骑士":return "Knights";case "灵俑":return "Spirits";case "鹏洛客":return "planeswalkers";case "神器生物":return "artifact creatures";case "生物":return "creatures";case "羊":return "Goats";case "匪徒":return "outlaws";case "历史永久物":return "historic permanents";case "猫":return "Cats";case "蛙":return "Frogs";case "食品":return "Foods";case "裂片妖":return "Slivers";case "蜥蜴":return "Lizards";case "城镇":return "Towns";case "精灵":return "Elves";case "达雷克":return "Daleks";case "非瑞克西亚人":return "Phyrexians";case "人类":return "Humans";case "公民":return "Citizens";default:return zh;}
    }
    private static string LandwalkEnglish(string zh)
    {
        switch(zh){case "山脉行者":return "Mountainwalk";case "沙漠行者":return "Desertwalk";case "树林行者":return "Forestwalk";case "海岛行者":return "Islandwalk";case "平原行者":return "Plainswalk";case "沼泽行者":return "Swampwalk";case "雪境地行者":return "Snow-covered landwalk";case "非基本地行者":return "Nonbasic landwalk";default:return zh;}
    }
    private static string LandwalkTarget(string en)
    {
        if(en.Equals("Snow-covered landwalk",StringComparison.OrdinalIgnoreCase)||en.Equals("Snow landwalk",StringComparison.OrdinalIgnoreCase))return "a snow-covered land";
        if(en.Equals("Nonbasic landwalk",StringComparison.OrdinalIgnoreCase))return "a nonbasic land";
        return "a "+en.Substring(0,en.Length-4);
    }
    private static string LandwalkTargetChinese(string en,string type)
    {
        if(en.IndexOf("snow",StringComparison.OrdinalIgnoreCase)>=0)return "雪境地";
        if(en.Equals("Nonbasic landwalk",StringComparison.OrdinalIgnoreCase))return "非基本地";
        return type+"地";
    }
    private static string AmassChineseType(string type)
    {
        switch(type.ToLowerInvariant()){case "orcs":return "半兽人";case "zombies":return "灵俑";case "goblins":return "鬼怪";case "slivers":return "裂片妖";case "fans":return "粉丝";default:return type;}
    }
    private static string AmassEnglishType(string type)
    {
        switch(type){case "半兽人":return "Orcs";case "灵俑":return "Zombies";case "鬼怪":return "Goblins";case "裂片妖":return "Slivers";case "粉丝":return "Fans";default:return type;}
    }
    private static string CyclingChineseType(string type)
    {
        switch(type.ToLowerInvariant())
        {
            case "basic landcycling":return "基本地循环";case "landcycling":return "地循环";case "artifact landcycling":return "神器地循环";case "snow landcycling":return "雪境地循环";
            case "islandcycling":return "循环海岛";case "mountaincycling":return "循环山脉";case "forestcycling":return "循环树林";case "plainscycling":return "循环平原";case "swampcycling":return "循环沼泽";
            case "slivercycling":return "裂片妖循环";case "wizardcycling":return "法术师循环";case "halflingcycling":return "半身人循环";
            default:return type;
        }
    }
    private static string CyclingEnglishType(string type)
    {
        switch(type.ToLowerInvariant()){case "循环海岛":return "Islandcycling";case "循环山脉":return "Mountaincycling";case "循环树林":return "Forestcycling";case "循环平原":return "Plainscycling";case "循环沼泽":return "Swampcycling";case "islandcycling":return "Islandcycling";case "mountaincycling":return "Mountaincycling";case "forestcycling":return "Forestcycling";case "plainscycling":return "Plainscycling";case "swampcycling":return "Swampcycling";case "slivercycling":return "Slivercycling";case "wizardcycling":return "Wizardcycling";case "halflingcycling":return "Halflingcycling";case "basic landcycling":return "Basic landcycling";case "landcycling":return "Landcycling";case "artifact landcycling":return "Artifact landcycling";case "snow landcycling":return "Snow landcycling";}
        string chinese=type.StartsWith("循环",StringComparison.Ordinal)?type.Substring(2):type.EndsWith("循环",StringComparison.Ordinal)?type.Substring(0,type.Length-2):type;
        switch(chinese){case "神器地":return "Artifact landcycling";case "雪境地":return "Snow landcycling";case "基本地":return "Basic landcycling";case "树林":return "Forestcycling";case "海岛":return "Islandcycling";case "平原":return "Plainscycling";case "沼泽":return "Swampcycling";case "山脉":return "Mountaincycling";case "裂片妖":return "Slivercycling";case "法术师":return "Wizardcycling";case "半身人":return "Halflingcycling";default:return type.EndsWith("cycling",StringComparison.OrdinalIgnoreCase)?type:type+"cycling";}
    }
    private static string CyclingTarget(string type)
    {
        if(type.Equals("Basic land",StringComparison.OrdinalIgnoreCase))return "basic land";
        if(type.Equals("Land",StringComparison.OrdinalIgnoreCase))return "land";
        if(type.Equals("Artifact land",StringComparison.OrdinalIgnoreCase))return "artifact land";
        if(type.Equals("Snow land",StringComparison.OrdinalIgnoreCase))return "snow-covered land";
        return "card with the type "+type;
    }
    private static string CyclingTargetChinese(string type)
    {
        if(type.Equals("Basic land",StringComparison.OrdinalIgnoreCase))return "基本地";
        if(type.Equals("Land",StringComparison.OrdinalIgnoreCase))return "地";
        if(type.Equals("Artifact land",StringComparison.OrdinalIgnoreCase))return "神器地";
        if(type.Equals("Snow land",StringComparison.OrdinalIgnoreCase))return "雪境地";
        string zh=type;
        switch(type.ToLowerInvariant()){case "island":zh="海岛";break;case "mountain":zh="山脉";break;case "forest":zh="树林";break;case "plains":zh="平原";break;case "swamp":zh="沼泽";break;case "sliver":zh="裂片妖";break;case "wizard":zh="法术师";break;case "halfling":zh="半身人";break;}
        return "具有“"+zh+"”类别的";
    }

    private struct LinkSpan
    {
        public int Start, Length, EntryIndex;
        public LinkSpan(int start, int length, int entryIndex) { Start=start; Length=length; EntryIndex=entryIndex; }
    }

    public static string Markup(string rules, List<Entry> entries, CardDefinition card = null)
    {
        if (string.IsNullOrEmpty(rules) || entries == null || entries.Count == 0) return rules ?? string.Empty;
        string visible = VisibleRules(rules,card);
        string tokenVisible = VisibleRules(rules,card,false);
        string keywordVisible = visible;
        var spans = new List<LinkSpan>();
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            visible = entry.TokenKey == null ? keywordVisible : tokenVisible;
            AddSpans(entry.English, true, i);
            if (entry.Chinese != entry.English) AddSpans(entry.Chinese, false, i);
            if (entry.Japanese != entry.English && entry.Japanese != entry.Chinese) AddSpans(entry.Japanese, false, i);
            foreach(var alias in entry.Aliases) AddSpans(alias, Regex.IsMatch(alias,@"[A-Za-z]"), i);
        }
        spans.Sort((a,b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.Length.CompareTo(a.Length));
        var result = new StringBuilder(rules.Length + spans.Count * 45);
        int cursor = 0;
        foreach (LinkSpan span in spans)
        {
            if (span.Start < cursor) continue;
            result.Append(rules, cursor, span.Start - cursor);
            result.Append("<link=\"k").Append(span.EntryIndex).Append("\"><color=#97CAFF><u>");
            result.Append(rules, span.Start, span.Length);
            result.Append("</u></color></link>");
            cursor = span.Start + span.Length;
        }
        result.Append(rules, cursor, rules.Length - cursor);
        return result.ToString();

        void AddSpans(string name, bool english, int index)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            foreach (Match match in Regex.Matches(visible, TermPattern(name), RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))
                spans.Add(new LinkSpan(match.Index, match.Length, index));
        }
    }
}

public sealed class DeckKeywordLinkHover : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    private List<DeckKeywordGlossary.Entry> entries;
    private Font font;
    private TextMeshProUGUI rules;
    private int activeIndex = -1;
    private CardDefinition sourceCard;
    private System.Func<CardDefinition,RectTransform,DeckKeywordGlossary.Entry,bool,bool> relatedHover;
    private static GameObject popup;
    private static DeckKeywordLinkHover owner;

    public void Configure(List<DeckKeywordGlossary.Entry> values, Font textFont, CardDefinition card = null,
        System.Func<CardDefinition,RectTransform,DeckKeywordGlossary.Entry,bool,bool> onRelatedHover = null)
    {
        if(activeIndex>=0&&rules!=null)relatedHover?.Invoke(sourceCard,rules.rectTransform,null,false);
        if(owner==this)Hide();
        entries = values;
        font = textFont;
        sourceCard=card;
        relatedHover=onRelatedHover;
        rules = GetComponent<TextMeshProUGUI>();
        activeIndex = -1;
    }

    public void OnPointerEnter(PointerEventData eventData) { OnPointerMove(eventData); }
    public void OnPointerMove(PointerEventData eventData)
    {
        if (rules == null || entries == null || entries.Count == 0) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(rules, eventData.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera);
        if (linkIndex < 0 || linkIndex >= rules.textInfo.linkCount)
        {
            if(activeIndex>=0)relatedHover?.Invoke(sourceCard,rules.rectTransform,null,false);
            activeIndex=-1;
            if (owner == this) Hide();
            return;
        }
        string id = rules.textInfo.linkInfo[linkIndex].GetLinkID();
        if (id.Length < 2 || id[0] != 'k' || !int.TryParse(id.Substring(1), out int index) || index < 0 || index >= entries.Count) return;
        if(relatedHover!=null)
        {
            bool related=relatedHover(sourceCard,rules.rectTransform,entries[index],true);
            if(related)
            {
                if(owner==this)Hide();
                activeIndex=index;
                return;
            }
        }
        // Token names are image-only links; never fall back to an empty help panel.
        if(entries[index].TokenKey!=null){if(owner==this)Hide();return;}
        if (owner != this || popup == null)
        {
            Hide();
            activeIndex = index;
            CreatePopup(entries[index], canvas);
        }
        else if (activeIndex != index)
        {
            activeIndex = index;
            RefreshPopup(entries[index]);
        }
        PositionPopup(canvas, eventData.position);
        if (popup != null && !popup.activeSelf) popup.SetActive(true);
    }

    private void CreatePopup(DeckKeywordGlossary.Entry entry, Canvas canvas)
    {
        owner = this;
        popup = new GameObject("KeywordExplanation", typeof(RectTransform), typeof(Image));
        popup.SetActive(false);
        popup.transform.SetParent(canvas.transform, false);
        popup.transform.SetAsLastSibling();
        var rect = (RectTransform)popup.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(0, 1);
        var background = popup.GetComponent<Image>();
        background.sprite = IntegratedUiUtility.RoundedRect(400, 100, 8);
        background.color = new Color(.075f, .105f, .15f, .98f);
        background.raycastTarget = false;
        var outline = popup.AddComponent<Outline>();
        outline.effectColor = new Color(.54f, .67f, .80f, .9f);
        outline.effectDistance = new Vector2(1, -1);
        Text heading = AddText("Keyword", entry.Name, 18, new Color(.85f, .72f, .48f));
        heading.fontStyle = FontStyle.Bold;
        Text body = AddText("Explanation", entry.Help, 16, new Color(.92f, .94f, .97f));
        RefreshPopup(entry);
    }

    private static void RefreshPopup(DeckKeywordGlossary.Entry entry)
    {
        if (popup == null) return;
        var texts = popup.GetComponentsInChildren<Text>();
        Text heading = System.Array.Find(texts, t => t.name == "Keyword");
        Text body = System.Array.Find(texts, t => t.name == "Explanation");
        heading.text = entry.Name;
        body.text = entry.Help;
        heading.GetComponent<PreferenceText>().SetBaseSize(18);
        body.GetComponent<PreferenceText>().SetBaseSize(16);
        var canvasRect=(RectTransform)popup.transform.parent;
        float width=Mathf.Min(400,canvasRect.rect.width-32);
        float availableHeight=Mathf.Max(128,canvasRect.rect.height-32);
        body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0, 1);
        body.rectTransform.sizeDelta = new Vector2(width-32, 100);
        // Fit complete definitions at a stable font size instead of overflowing
        // the old fixed 300px box, especially for Japanese compound mechanics.
        while(body.preferredHeight+60>availableHeight && width<Mathf.Min(720,canvasRect.rect.width-32))
        {
            width=Mathf.Min(width+80,Mathf.Min(720,canvasRect.rect.width-32));
            body.rectTransform.sizeDelta=new Vector2(width-32,100);
        }
        float height = Mathf.Max(body.preferredHeight + 60, 128);
        var rect = (RectTransform)popup.transform;
        rect.sizeDelta = new Vector2(width, height);
        PlaceText(heading.rectTransform, new Vector2(16, -38), new Vector2(-16, -12));
        PlaceText(body.rectTransform, new Vector2(16, -height + 14), new Vector2(-16, -46));
    }

    private static void PositionPopup(Canvas canvas, Vector2 screenPosition)
    {
        if (popup == null) return;
        RectTransform canvasRect = (RectTransform)canvas.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, canvas.worldCamera, out Vector2 local);
        var rect = (RectTransform)popup.transform;
        float height = rect.rect.height;
        rect.anchoredPosition = new Vector2(Mathf.Clamp(local.x + 18, canvasRect.rect.xMin + 8, canvasRect.rect.xMax - rect.rect.width - 8),
            Mathf.Clamp(local.y - 12, canvasRect.rect.yMin + height + 8, canvasRect.rect.yMax - 8));
    }

    public void OnPointerExit(PointerEventData eventData) { if(activeIndex>=0)relatedHover?.Invoke(sourceCard,rules.rectTransform,null,false);activeIndex=-1;if (owner == this) Hide(); }
    private void OnDisable() { if(activeIndex>=0&&rules!=null)relatedHover?.Invoke(sourceCard,rules.rectTransform,null,false);activeIndex=-1;if (owner == this) Hide(); }
    public static void Hide()
    {
        if (owner != null) owner.activeIndex = -1;
        owner = null;
        if (popup != null) UnityEngine.Object.Destroy(popup);
        popup = null;
    }
    private Text AddText(string name, string value, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(popup.transform, false);
        var text = go.GetComponent<Text>();
        text.font = GamePreferences.Font != null ? GamePreferences.Font : font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        go.AddComponent<PreferenceText>().SetBaseSize(size);
        return text;
    }
    private static void PlaceText(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(.5f, 1);
        rect.offsetMin = min;
        rect.offsetMax = max;
    }
}
