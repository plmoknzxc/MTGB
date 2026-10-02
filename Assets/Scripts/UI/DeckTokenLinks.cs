using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Links are limited to this printing's local token references. They never fetch art.
public static class DeckTokenLinks
{
    [Serializable] private sealed class TypeAlias { public string en,zh,ja; public string[] jaAliases; }
    [Serializable] private sealed class TypeIndex { public TypeAlias[] entries; }
    [Serializable] private sealed class NameAlias { public string key; public string[] aliases; }
    [Serializable] private sealed class NameIndex { public NameAlias[] entries; }
    private static Dictionary<string,string[]> tokenNames;
    private static Dictionary<string,string> localTypes;
    private static Dictionary<string,string> japaneseTypes;
    private static Dictionary<string,string[]> japaneseAliases;
    private static readonly Dictionary<string,string> Types = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
    {
        {"Treasure","珍宝"},{"Food","食品"},{"Clue","线索"},{"Blood","血滴"},{"Map","地图"},{"Powerstone","魔力石"},
        {"Incubator","抚育器"},{"Gold","黄金"},{"Junk","废料"},{"Copy","复制品"},{"Army","军队"},
        {"Zombie","灵俑"},{"Orc","半兽人"},{"Goblin","鬼怪"},{"Shaman","祭师"},{"Elemental","元素"},
        {"Spirit","精怪"},{"Soldier","士兵"},{"Warrior","战士"},{"Human","人类"},{"Elf","精灵"},
        {"Knight","骑士"},{"Angel","天使"},{"Demon","恶魔"},{"Devil","魔鬼"},{"Beast","野兽"},
        {"Bird","鸟"},{"Cat","猫"},{"Dog","狗"},{"Wolf","狼"},{"Bear","熊"},{"Dragon","龙"},
        {"Drake","龙兽"},{"Wurm","亚龙"},{"Rat","老鼠"},{"Insect","昆虫"},{"Spider","蜘蛛"},
        {"Snake","蛇"},{"Squirrel","松鼠"},{"Saproling","腐生物"},{"Plant","植物"},{"Fungus","真菌"},
        {"Thopter","振翼机"},{"Servo","自动机"},{"Construct","组构体"},{"Golem","魔像"},{"Myr","秘耳"},
        {"Eldrazi","奥札奇"},{"Spawn","后裔"},{"Scion","孽裔"},{"Phyrexian","非瑞人"},{"Germ","病菌"},
        {"Vampire","吸血鬼"},{"Bat","蝙蝠"},{"Skeleton","骷髅妖"},{"Wizard","法术师"},{"Rogue","浪客"},
        {"Assassin","杀手"},{"Rebel","反抗军"},{"Citizen","公民"},{"Merfolk","人鱼"},{"Faerie","仙灵"},
        {"Dwarf","矮人"},{"Dinosaur","恐龙"},{"Ooze","流浆"},{"Sliver","裂片妖"},{"Goat","羊"},
        {"Horse","马"},{"Unicorn","独角兽"},{"Pegasus","飞马"},{"Kraken","巨海兽"},{"Octopus","章鱼"},
        {"Tentacle","触手"},{"Fish","鱼"},{"Shark","鲨鱼"},{"Crab","蟹"},{"Frog","蛙"},{"Turtle","龟"},
        {"Lizard","蜥蜴"},{"Rabbit","兔"},{"Raccoon","浣熊"},{"Mouse","小鼠"},{"Otter","水獭"},
        {"Role","角色"},{"Monster","怪物"},{"Royal","皇家"},{"Sorcerer","术士"},{"Wicked","恶人"},
        {"Cursed","诅咒"},{"Young","年轻"},{"Hero","英雄"},{"Virtuous","贤者"}
    };

    public static List<DeckKeywordGlossary.Entry> Find(CardDefinition card,string rules)
    {
        var result=new List<DeckKeywordGlossary.Entry>();
        if(card==null||string.IsNullOrWhiteSpace(rules))return result;
        foreach(var token in DeckRelatedTokenArt.GetReferences(card))
        {
            if(!string.IsNullOrEmpty(token.group))continue;
            string translated=Translate(token.name);
            var aliases=new List<string>{token.name,translated,translated.Replace("／","")};
            if(tokenNames==null)
            {
                tokenNames=new Dictionary<string,string[]>(StringComparer.Ordinal);
                var data=UnityEngine.Resources.Load<UnityEngine.TextAsset>("TokenNameAliases");
                if(data!=null)
                {
                    foreach(var entry in UnityEngine.JsonUtility.FromJson<NameIndex>(data.text).entries)
                        tokenNames[entry.key]=entry.aliases;
                    UnityEngine.Resources.UnloadAsset(data);
                }
            }
            if(tokenNames.TryGetValue(token.key,out var namedAliases))aliases.AddRange(namedAliases);
            string japanese=TranslateJapanese(token.name);
            if(japanese!=token.name)aliases.Add(japanese);
            aliases.AddRange(JapaneseNames(token.name));
            string legacy=Translate(token.name,true);
            aliases.Add(legacy);aliases.Add(legacy.Replace("／",""));
            string localized=GamePreferences.CardName(new CardDefinition{cardName=token.name});
            if(localized!=token.name)aliases.Add(localized);
            if(token.name.EndsWith(" Token",StringComparison.Ordinal))
            {
                string baseName=token.name.Substring(0,token.name.Length-6);
                aliases.Add(baseName);
                aliases.Add(GamePreferences.CardName(new CardDefinition{cardName=baseName}));
            }
            // Proper token names are not creature subtypes. Pair explicitly named tokens
            // in the source and localized rules instead of guessing from parts of the name.
            var originalNames=Regex.Matches(card.description??"",@"\bnamed (?<name>.+?)(?= with\b| that\b|[.;\n]|$)",RegexOptions.IgnoreCase);
            var localNames=Regex.Matches(rules,@"名(?:称)?为(?:[「『“""]([^」』”""]+)[」』”""]|([^，,。；;\n]+?)(?=的|[，,。；;\n]|$))");
            if(Regex.IsMatch(rules,@"[\u3040-\u30ff]"))
                localNames=Regex.Matches(rules,@"[「『《]([^」』》]+)[」』》](?:という|と言う)名前");
            else if(localNames.Count==0)
                // Older Chinese printings introduce a named legendary token
                // directly after its type, rather than using 名称为.
                localNames=Regex.Matches(rules,@"派出(?:一个|一個)?传奇衍生(?:神器)?(?:生物|结界|神器)([^，,。；;\n]+)[，,]其为");
            if(originalNames.Count==localNames.Count)
                for(int n=0;n<originalNames.Count;n++)
                    if((originalNames[n].Groups["name"].Value.Trim().Equals(token.name,StringComparison.OrdinalIgnoreCase)||originalNames[n].Groups["name"].Value.Trim().StartsWith(token.name+",",StringComparison.OrdinalIgnoreCase)))
                        aliases.Add((localNames[n].Groups[1].Success?localNames[n].Groups[1].Value:localNames[n].Groups[2].Value).Trim());
            if(originalNames.Count==0&&localNames.Count==1&&Regex.IsMatch(card.description??"",
                @"\bcreate\s+"+Regex.Escape(token.name)+@",\s+(?:a|an)\b",RegexOptions.IgnoreCase))
                aliases.Add((localNames[0].Groups[1].Success?localNames[0].Groups[1].Value:localNames[0].Groups[2].Value).Trim());
            // Historic Chinese token type wording.
            if(token.name=="Golem")aliases.Add("魔像");
            if(token.name=="Blood")aliases.Add("血液");
            if(token.name=="Elephant")aliases.Add("象");
            if(token.name=="Balloon")aliases.Add("气球");
            if(token.name=="Faerie Spy")aliases.Add("仙灵／探子");
            if(token.name=="Lander")aliases.Add("着陸船");
            if(token.name=="Rukh"){aliases.Add("Bird");aliases.Add("鸟");}
            bool permanentCopy=token.name=="Copy"&&rules.Contains("パーマネント")&&rules.Contains("呪文")
                &&(card.description??"").IndexOf("token",StringComparison.OrdinalIgnoreCase)>=0;
            if(permanentCopy)aliases.Add("コピーする");
            var matched=new List<string>();
            foreach(string alias in aliases)
            {
                string pattern=Regex.IsMatch(alias,"[A-Za-z]")?@"(?<![A-Za-z])"+Regex.Escape(alias)+@"(?![A-Za-z])":Regex.Escape(alias);
                if(Regex.IsMatch(alias,@"[\u3040-\u30ff\u3400-\u9fff]"))
                    pattern=string.Join(@"[ \t\r\n]*",Array.ConvertAll(alias.ToCharArray(),c=>Regex.Escape(c.ToString())));
                foreach(Match m in Regex.Matches(rules,pattern,RegexOptions.IgnoreCase))
                {
                    // Only link a token description, not a matching creature/card name elsewhere.
                    string tail=TokenClause(rules,m.Index);
                    if(Regex.IsMatch(tail,@"\btoken\b|衍生|ト[\s]*ー[\s]*ク[\s]*ン",RegexOptions.IgnoreCase)||permanentCopy&&m.Value=="コピーする")
                    { if(!matched.Contains(m.Value))matched.Add(m.Value); }
                }
            }
            if(matched.Count==0)continue;
            result.Add(new DeckKeywordGlossary.Entry(token.name,translated,japanese,"","","")
                {TokenKey=token.key,Aliases=matched.ToArray()});
        }
        return result;
    }

    private static string TokenClause(string rules,int index)
    {
        int start=0,depth=0;
        bool englishQuote=false;
        bool japanese=Regex.IsMatch(rules,@"ト[\s]*ー[\s]*ク[\s]*ン");
        for(int i=0;i<rules.Length;i++)
        {
            char c=rules[i];
            if(c=='「'||c=='『')depth++;
            else if(c=='」'||c=='』')depth=Math.Max(0,depth-1);
            else if(c=='"')englishQuote=!englishQuote;
            // A token can grant a quoted ability containing full stops. Japanese
            // PDF-derived rules also have visual line breaks inside one sentence.
            bool boundary=depth==0&&!englishQuote&&(c=='.'||c=='。'||c==';'||c=='；'||(c=='\n'&&!japanese));
            if(!boundary)continue;
            if(i>=index)return ExtendJapaneseChoice(rules,index,start,i);
            start=i+1;
        }
        return ExtendJapaneseChoice(rules,index,start,rules.Length);
    }

    private static string ExtendJapaneseChoice(string rules,int index,int start,int end)
    {
        string clause=rules.Substring(start,end-start);
        if(clause.Contains("トークン")||rules.IndexOf("トークン",StringComparison.Ordinal)<0)return clause;
        int lineStart=rules.LastIndexOf('\n',index)+1;
        string line=rules.Substring(lineStart,index-lineStart).TrimStart();
        if(line.StartsWith("・")||line.StartsWith("•"))
        {
            int choice=rules.LastIndexOf("以下から",index,StringComparison.Ordinal);
            if(choice>=0)
            {
                string intro=rules.Substring(choice,lineStart-choice);
                if(intro.Contains("トークン")&&intro.Contains("生成"))return intro+clause;
            }
        }
        // A type can be chosen as the card enters, then used by a separate ability.
        if(clause.Contains("から１つを選ぶ")&&rules.Substring(end).Contains("選ばれた色とタイプ"))
            return clause+rules.Substring(end);
        return clause;
    }

    private static string Translate(string name,bool legacy=false)
    {
        if(localTypes==null)
        {
            localTypes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            japaneseTypes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            japaneseAliases=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
            var data=UnityEngine.Resources.Load<UnityEngine.TextAsset>("TokenTypeAliases");
            if(data!=null)
            {
                foreach(var entry in UnityEngine.JsonUtility.FromJson<TypeIndex>(data.text).entries)
                {
                    if(!string.IsNullOrEmpty(entry.zh))localTypes[entry.en]=entry.zh;
                    if(!string.IsNullOrEmpty(entry.ja))japaneseTypes[entry.en]=entry.ja;
                    if(entry.jaAliases!=null)japaneseAliases[entry.en]=entry.jaAliases;
                }
                UnityEngine.Resources.UnloadAsset(data);
            }
        }
        var parts=name.Split(' ');var translated=new List<string>();
        foreach(var part in parts)
        {
            string value;
            if((legacy||!localTypes.TryGetValue(part,out value))&&!Types.TryGetValue(part,out value))return name;
            translated.Add(value);
        }
        return string.Join("／",translated);
    }

    private static string TranslateJapanese(string name)
    {
        // Reuse the same local subtype catalog for compound token names.
        if(localTypes==null)Translate(name);
        if(japaneseTypes.TryGetValue(name,out var whole))return whole;
        var translated=new List<string>();
        foreach(var part in name.Split(' '))
        {
            if(!japaneseTypes.TryGetValue(part,out var value))return name;
            translated.Add(value);
        }
        return string.Join("・",translated);
    }

    private static IEnumerable<string> JapaneseNames(string name)
    {
        // A reversible token reference may name two faces; link each separately.
        foreach(string face in name.Split(new[]{" // "},StringSplitOptions.None))
        {
            if(japaneseAliases.TryGetValue(face,out var exact))
                foreach(var alias in exact)yield return alias;
            var variants=new List<string>{""};
            foreach(string part in face.Split(' '))
            {
                if(!japaneseAliases.TryGetValue(part,out var choices))
                {
                    if(japaneseTypes.TryGetValue(part,out var single))choices=new[]{single};
                    else {variants.Clear();break;}
                }
                var next=new List<string>();
                foreach(var prefix in variants)foreach(var choice in choices)
                    next.Add(prefix.Length==0?choice:prefix+"・"+choice);
                variants=next;
            }
            foreach(var alias in variants)
            {
                yield return alias;
                // Role descriptions insert the Role subtype after the name.
                if(alias=="若き英雄")yield return "若き・英雄";
            }
        }
    }
}
