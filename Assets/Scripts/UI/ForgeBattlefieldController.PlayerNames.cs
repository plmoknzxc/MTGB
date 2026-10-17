using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    // Presentation only. All selection and ownership continue to use native IDs.
    private string PlayerDisplayName(int playerSeat)
    {
        string name=service?.LobbyState?.players?.FirstOrDefault(p=>p!=null&&p.seat==playerSeat)?.displayName;
        if(!string.IsNullOrWhiteSpace(name))return name;
        if(playerSeat==seat&&service?.IsSpectator!=true)return TapPaymentText("你","You","あなた");
        if(service?.IsSpectator!=true&&playerSeat>0&&(view?.players?.Length??2)<=2)
            return TapPaymentText("对手","Opponent","対戦相手");
        return TapPaymentText("玩家 ","Player ","プレイヤー ")+playerSeat;
    }
    private readonly System.Collections.Generic.List<(bool exists,int seat,string name,bool connected,bool hasDeck,bool ready)> nameInputs=new();
    private string playerNamesKey=string.Empty;
    private string PlayerNamesKey()
    {
        var players=service?.LobbyState?.players;
        int count=players?.Count??0;
        bool changed=count!=nameInputs.Count;
        for(int i=0;!changed&&i<count;i++) {
            var p=players[i];
            changed=nameInputs[i]!=(p!=null,p?.seat??0,p?.displayName,p?.connected??false,p?.hasDeck??false,p?.ready??false);
        }
        if(!changed)return playerNamesKey;
        nameInputs.Clear();
        for(int i=0;i<count;i++) {
            var p=players[i];nameInputs.Add((p!=null,p?.seat??0,p?.displayName,p?.connected??false,p?.hasDeck??false,p?.ready??false));
        }
        // Serialize only when lobby values change, retaining the exact original
        // identity for nicknames containing separators and updates made in place.
        playerNamesKey=players==null?string.Empty:string.Join("|",players.Where(p=>p!=null).Select(p=>p.seat+":"+JsonUtility.ToJson(p)));
        return playerNamesKey;
    }

    // Carry native identity through generated prose without adding visible IDs.
    private static string PlayerTextReference(int playerSeat)=>"\uE000"+playerSeat+"\uE001";

    // The surrounding fixed words have already been localized. A nickname is
    // literal user data, even if it happens to equal a key such as "Ready".
    private string PlayerNamesRichText(string value,int exactSeat=0)
    {
        value=value??"";
        bool colors=service?.IsSpectator!=true&&(view?.players?.Length??0)==2;
        string Color(int player)=>player==seat?"#5DB9F5":"#F17C7C";
        if(exactSeat>0)return colors?ColoredDetail(value,Color(exactSeat)):SafeDetail(value);
        var references=System.Text.RegularExpressions.Regex.Matches(value,"\uE000([0-9]+)\uE001");
        if(references.Count>0) {
            var resolved=new System.Text.StringBuilder();int from=0;
            foreach(System.Text.RegularExpressions.Match reference in references) {
                resolved.Append(PlayerNamesRichText(value.Substring(from,reference.Index-from)));
                int player=int.Parse(reference.Groups[1].Value);
                resolved.Append(PlayerNamesRichText(PlayerDisplayName(player),player));
                from=reference.Index+reference.Length;
            }
            return resolved.Append(PlayerNamesRichText(value.Substring(from))).ToString();
        }
        if(!colors)return SafeDetail(value);
        var names=view.players.Where(p=>p.seat>0).Select(p=>new{p.seat,name=PlayerDisplayName(p.seat)})
            .GroupBy(p=>p.name).Where(g=>g.Count()==1).Select(g=>g.First()).OrderByDescending(p=>p.name.Length).ToArray();
        if(names.Length==0)return SafeDetail(value);
        string pattern=string.Join("|",names.Select(p=>
            (p.name[0]<128&&char.IsLetterOrDigit(p.name[0])?@"(?<![A-Za-z0-9_/{])":"")
            +System.Text.RegularExpressions.Regex.Escape(p.name)
            +(p.name[p.name.Length-1]<128&&char.IsLetterOrDigit(p.name[p.name.Length-1])?@"(?![A-Za-z0-9_/}])":"")));
        var result=new System.Text.StringBuilder();int at=0;
        foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(value,pattern)) {
            result.Append(SafeDetail(value.Substring(at,match.Index-at)));
            var player=names.First(p=>p.name==match.Value);
            result.Append(ColoredDetail(match.Value,Color(player.seat)));at=match.Index+match.Length;
        }
        return result.Append(SafeDetail(value.Substring(at))).ToString();
    }
    private void SetNamedPlayerText(Text label,string value,int playerSeat=0)
    {
        var binding=label.GetComponent<PreferenceText>()??label.gameObject.AddComponent<PreferenceText>();
        binding.Literal=true;
        if(playerSeat>0) {
            // Marquee truncation operates on literal text elements, not tags.
            label.supportRichText=false;label.text=SafeDetail(value);
            label.color=service?.IsSpectator==true||(view?.players?.Length??0)!=2?Paper:
                playerSeat==seat?new Color(93/255f,185/255f,245/255f):new Color(241/255f,124/255f,124/255f);
        } else {label.supportRichText=true;label.text=PlayerNamesRichText(value);}
        binding.Apply();
    }
}
