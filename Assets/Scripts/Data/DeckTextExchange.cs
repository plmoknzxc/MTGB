using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// Clipboard formats are converted atomically: an invalid line never becomes a partial deck.
public sealed class DeckTextExchange
{
    public enum Format { MO, MTGA, MTGB }
    public sealed class Result
    {
        public DeckDefinition Deck;
        public readonly List<string> Errors=new List<string>();
        public bool Success=>Deck!=null && Errors.Count==0;
    }
    readonly Dictionary<string,List<CardDefinition>> names=new Dictionary<string,List<CardDefinition>>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,CardDefinition> printings=new Dictionary<string,CardDefinition>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<int,List<CardDefinition>> legacy=new Dictionary<int,List<CardDefinition>>();
    static string Key(string name)=>Regex.Replace((name??"").Normalize(NormalizationForm.FormKC).Replace('’','\''),@"\s+"," ").Trim();
    public DeckTextExchange(IEnumerable<CardDefinition> cards)
    {
        foreach(var card in cards.Where(c=>c!=null && !c.forgeAmbiguous).OrderBy(c=>c.PrintingKey,StringComparer.OrdinalIgnoreCase))
        {
            if(string.IsNullOrEmpty(card.PrintingKey))continue;
            if(!printings.ContainsKey(card.PrintingKey))printings.Add(card.PrintingKey,card);
            AddName(card.cardName,card);
            if(card.cardName.Contains(" // "))AddName(card.cardName.Split(new[]{" // "},StringSplitOptions.None)[0],card);
            if(!legacy.TryGetValue(card.cardID,out var list))legacy[card.cardID]=list=new List<CardDefinition>();
            list.Add(card);
        }
    }
    void AddName(string name,CardDefinition card)
    {
        string key=Key(name);if(key.Length==0)return;
        if(!names.TryGetValue(key,out var list))names[key]=list=new List<CardDefinition>();
        if(!list.Contains(card))list.Add(card);
    }
    public void AddAlias(string canonical,string translated)
    {
        if(names.TryGetValue(Key(canonical),out var values))foreach(var card in values.ToArray())AddName(translated,card);
    }
    public Result Import(string text)
    {
        var result=new Result();var deck=new DeckDefinition{name="Imported Deck"};
        if(string.IsNullOrWhiteSpace(text)){result.Errors.Add("请粘贴卡组文本。");return result;}
        if(text.Length>200000){result.Errors.Add("牌表过长（最多 200000 字符）。");return result;}
        string[] lines=text.TrimStart('\uFEFF').Replace("\r\n","\n").Replace('\r','\n').Split('\n');
        bool native=lines.Any(l=>Regex.IsMatch(l.Trim(),@"^(#name |#main$|!side$|#commander$)",RegexOptions.IgnoreCase));
        bool explicitSections=lines.Any(l=>Section(l.Trim().TrimEnd(':'),out _));
        int section=0,total=0;bool gap=false;
        for(int i=0;i<lines.Length;i++)
        {
            string line=lines[i].Trim();
            if(line.Length==0){if(total>0 && section==0)gap=true;continue;}
            if(line.StartsWith("#name ",StringComparison.OrdinalIgnoreCase)){deck.name=line.Substring(6).Trim();continue;}
            if(line.StartsWith("#format ",StringComparison.OrdinalIgnoreCase))
            {
                string value=line.Substring(8).Trim().ToLowerInvariant();
                if(!DeckFormatLegality.Keys.Contains(value))result.Errors.Add($"第 {i+1} 行：未知赛制 {value}");
                else deck.format=value;
                continue;
            }
            if(line.StartsWith("#banlist ",StringComparison.OrdinalIgnoreCase))
            {
                string value=line.Substring(9).Trim();
                if(value!="current" && value!="none" && !Regex.IsMatch(value,@"^\d{4}-\d{2}-\d{2}$"))
                    result.Errors.Add($"第 {i+1} 行：禁表日期无效 {value}");
                else deck.banlist=value;
                continue;
            }
            if(line.StartsWith("Name ",StringComparison.OrdinalIgnoreCase)){deck.name=line.Substring(5).Trim();continue;}
            if(line.Equals("About",StringComparison.OrdinalIgnoreCase))continue;
            if(Section(line.TrimEnd(':'),out int next)){section=next;gap=false;continue;}
            if(line.StartsWith("//"))continue;
            if(line.StartsWith("#")){if(native)result.Errors.Add($"第 {i+1} 行：不支持的分区 {line}");continue;}
            bool sb=line.StartsWith("SB:",StringComparison.OrdinalIgnoreCase);if(sb)line=line.Substring(3).Trim();
            if(!explicitSections && !sb && gap){section=1;gap=false;}
            var match=Regex.Match(line,@"^(\d+)(?:[xX])?\s+(.+)$");
            if(!match.Success || !int.TryParse(match.Groups[1].Value,out int count) || count<1 || count>1000)
            {result.Errors.Add($"第 {i+1} 行：需要“数量 卡名”或“数量 系列/编号”。");continue;}
            string token=match.Groups[2].Value.Trim();CardDefinition card=null;
            if(printings.TryGetValue(token,out var exact))card=exact;
            else if(native && int.TryParse(token,out int id))
            {if(legacy.TryGetValue(id,out var versions) && versions.Count==1)card=versions[0];}
            else
            {
                // Deck sites append treatment/finish annotations to both Arena and MO text.
                // These are printing metadata, never part of the canonical card name.
                token=Regex.Replace(token,@"\s*<[^<>\r\n]+>",string.Empty).Trim();
                token=Regex.Replace(token,@"\s*(?:\((?:F|Foil|Nonfoil|Etched|Etched Foil)\)|\*[FE]\*)\s*$",string.Empty,RegexOptions.IgnoreCase).Trim();
                var printing=Regex.Match(token,@"^(.+?)\s+\(([^()]+)\)\s+(\S+)$");
                var bracket=Regex.Match(token,@"^(.+?)\s+\[([^\[\]]+)\](?:\s+#?(\S+))?$");
                string name=printing.Success?printing.Groups[1].Value:bracket.Success?bracket.Groups[1].Value:token;
                names.TryGetValue(Key(name),out var candidates);
                if(printing.Success)
                {
                    string key=printing.Groups[2].Value+"/"+printing.Groups[3].Value;
                    if(printings.TryGetValue(key,out var version) && candidates!=null && candidates.Contains(version))card=version;
                }
                else if(bracket.Success && candidates!=null)
                {
                    string set=bracket.Groups[2].Value, number=bracket.Groups[3].Value;
                    // Legacy deck-site codes and Forge's Mystery Booster/List representation.
                    switch(set.ToUpperInvariant())
                    {
                        case "UZ":case "US":set="USG";break;
                        case "MI":set="MIR";break;
                    }
                    var matches=candidates.Where(c=>(string.Equals(c.setCode,set,StringComparison.OrdinalIgnoreCase)
                        || string.Equals(c.forgeSet,set,StringComparison.OrdinalIgnoreCase))
                        && (number.Length==0 || string.Equals(c.CollectorNumber,number,StringComparison.OrdinalIgnoreCase))).ToList();
                    if(matches.Count==0 && number.Length==0 && set.Equals("MB1",StringComparison.OrdinalIgnoreCase))
                        matches=candidates.Where(c=>c.forgeSet=="PLST").ToList();
                    if(matches.Count>0 && matches.Select(c=>Key(c.cardName)).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1)card=matches[0];
                }
                else if(candidates!=null)
                {
                    // Different printings are allowed; different identities sharing an alias are not.
                    if(candidates.Select(c=>Key(c.cardName)).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1)card=candidates[0];
                }
            }
            if(card==null){result.Errors.Add($"第 {i+1} 行：找不到或无法唯一识别 {token}");continue;}
            if(section==3){
                if(count!=1||DeckCompanion.Has(deck)||!DeckCompanion.IsCompanion(card))result.Errors.Add($"第 {i+1} 行：行侣分区必须是一张具有行侣异能的牌。");
                else deck.companion=new DeckEntry{setCode=card.setCode,collectorNumber=card.CollectorNumber,cardId=card.cardID,count=1};
                continue;
            }
            total+=count;if(total>1000){result.Errors.Add("卡组总张数超过 1000，未导入。");break;}
            var entries=sb || section==1?deck.sideboard:section==2?deck.commanders:deck.cards;
            var entry=entries.FirstOrDefault(e=>e.PrintingKey.Equals(card.PrintingKey,StringComparison.OrdinalIgnoreCase));
            if(entry==null){entry=new DeckEntry{setCode=card.setCode,collectorNumber=card.CollectorNumber,cardId=card.cardID,count=0};entries.Add(entry);}
            entry.count+=count;
        }
        if(DeckCompanion.Has(deck)&&!deck.sideboard.Any(e=>e.PrintingKey==deck.companion.PrintingKey))deck.sideboard.Add(DeckCompanion.Copy(deck.companion));
        if(total==0 && result.Errors.Count==0)result.Errors.Add("未找到卡牌条目。");
        // Preserve quantities, including draft/casual lists. Format legality belongs to match validation.
        if(result.Errors.Count==0)result.Deck=deck;
        return result;
    }
    static bool Section(string line,out int section)
    {
        switch(line.ToLowerInvariant())
        {
            case "deck":case "main":case "mainboard":case "maindeck":case "#main":case "デッキ":case "牌组":case "套牌":case "主牌":section=0;return true;
            case "sideboard":case "side":case "!side":case "#side":case "备牌":case "サイドボード":section=1;return true;
            case "commander":case "#commander":case "!commander":case "指挥官":case "統率者":section=2;return true;
            case "companion":case "#companion":case "行侣":case "同伴":case "相棒":section=3;return true;
            default:section=0;return false;
        }
    }
    public string Export(DeckDefinition deck,Format format)
    {
        if(deck==null)throw new ArgumentException("没有卡组。");
        if(format==Format.MTGB)return DeckRepository.SerializeLineDeck(deck);
        if(format==Format.MO&&DeckCompanion.Has(deck))throw new ArgumentException("MO 纯文本不能保留行侣指定，请使用 MTGA 或 MTGB 格式。");
        if(format==Format.MO && deck.commanders.Count>0)throw new ArgumentException("MO 纯文本不能保留当前卡组的指挥官标记，请使用 MTGA 或 MTGB 格式。");
        var lines=new List<string>();
        void Add(IEnumerable<DeckEntry> entries)
        {
            foreach(var entry in entries)
            {
                if(!printings.TryGetValue(entry.PrintingKey,out var card))throw new ArgumentException("无法导出未识别的卡牌："+entry.PrintingKey);
                if(card.customContent)throw new ArgumentException(format+" 不支持 DIY 卡牌："+card.cardName);
                lines.Add(entry.count+" "+card.cardName+(format==Format.MTGA?" ("+card.setCode+") "+card.CollectorNumber:""));
            }
        }
        if(format==Format.MTGA&&DeckCompanion.Has(deck)){lines.Add("Companion");Add(new[]{deck.companion});lines.Add("");}
        if(format==Format.MTGA && deck.commanders.Count>0){lines.Add("Commander");Add(deck.commanders);lines.Add("");}
        if(format==Format.MTGA)lines.Add("Deck");Add(deck.cards);
        if(deck.sideboard.Count>0){lines.Add("");if(format==Format.MTGA)lines.Add("Sideboard");Add(deck.sideboard);}
        return string.Join("\n",lines)+"\n";
    }
}
