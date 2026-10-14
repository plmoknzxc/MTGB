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
    private string PlayerNamesKey()=>string.Join("|",(service?.LobbyState?.players??new System.Collections.Generic.List<OnlineLobbyPlayer>())
        .Where(p=>p!=null).Select(p=>p.seat+":"+JsonUtility.ToJson(p)));

    // The surrounding fixed words have already been localized. A nickname is
    // literal user data, even if it happens to equal a key such as "Ready".
    private static void SetNamedPlayerText(Text label,string value)
    {
        var binding=label.GetComponent<PreferenceText>()??label.gameObject.AddComponent<PreferenceText>();
        binding.Literal=true;label.supportRichText=false;label.text=value;binding.Apply();
    }
}
