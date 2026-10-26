using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform handSourceTabs;
    private readonly Dictionary<string,Button> handSourceButtons=new();
    private string selectedHandSource="Hand";

    private static string HandSource(ForgeCardView card)=>string.IsNullOrEmpty(card.zone)?"Hand":card.zone;
    private string HandSourceLabel(string zone)=>zone switch {
        "Hand"=>TapPaymentText("手牌","Hand","手札"),
        "Graveyard"=>TapPaymentText("坟墓场","Graveyard","墓地"),
        "Exile"=>TapPaymentText("放逐","Exile","追放"),
        "Command"=>TapPaymentText("统帅区","Command","統率領域"),
        "Battlefield"=>TapPaymentText("备法","Prepared","準備"),
        _=>TapPaymentText("其他区域","Other","その他")
    };

    private ForgeCardView[] PresentHandSources(ForgeCardView[] cards,bool choosingHand)
    {
        if (IsRedesignLayout) return PresentRedesignSources(cards, ownFan, choosingHand);
        var groups=cards.GroupBy(HandSource).ToArray();
        // Opening and direct hand choices always keep the real hand exposed.
        bool handChoice=choosingHand||view?.turn==0;
        if(handChoice||groups.Length==0||groups.All(g=>g.Key=="Hand"))selectedHandSource="Hand";
        else if(!groups.Any(g=>g.Key==selectedHandSource))
            selectedHandSource=groups.Any(g=>g.Key=="Hand")?"Hand":groups[0].Key;
        bool show=!handChoice&&groups.Any(g=>g.Key!="Hand");
        if(!show){if(handSourceTabs!=null)handSourceTabs.gameObject.SetActive(false);return cards.Where(c=>HandSource(c)=="Hand").ToArray();}
        if(handSourceTabs==null){
            handSourceTabs=Rect(surface,"HandSourceTabs",Vector2.zero,new Vector2(600,36));
            handSourceTabs.anchorMin=handSourceTabs.anchorMax=new Vector2(.5f,0);
            handSourceTabs.anchoredPosition=new Vector2(64,22);
            SetOverlayLayer(handSourceTabs,3,true);
        }
        handSourceTabs.gameObject.SetActive(true);
        foreach(var button in handSourceButtons.Values)button.gameObject.SetActive(false);
        var zones=new[]{"Hand"}.Concat(groups.Select(g=>g.Key).Where(z=>z!="Hand")).ToArray();
        var widths=new List<float>();
        foreach(string zone in zones){
            if(!handSourceButtons.TryGetValue(zone,out var button)){
                string captured=zone;
                button=Button(handSourceTabs,"Source_"+zone,"",Vector2.zero,new Vector2(104,36),()=>{
                    if(selectedHandSource==captured)return;
                    selectedHandSource=captured;
                    // A source switch is local browsing, never a native action.
                    ownFan.navigation?.Configure(ownHand,0,null);
                    PresentIntegrated(connectionText?.text??"");
                },Ink);
                handSourceButtons.Add(zone,button);
            }
            button.gameObject.SetActive(true);
            int count=groups.FirstOrDefault(g=>g.Key==zone)?.Count()??0;
            var label=button.GetComponentInChildren<Text>();label.text=HandSourceLabel(zone)+" "+count;
            PreferenceText.SetSize(label,16);label.resizeTextForBestFit=false;
            float width=Mathf.Max(88,Mathf.Ceil(label.preferredWidth)+24);widths.Add(width);
            ((RectTransform)button.transform).sizeDelta=new Vector2(width,36);
            label.rectTransform.sizeDelta=new Vector2(width-12,34);
            button.targetGraphic.color=zone==selectedHandSource?new Color(.16f,.36f,.49f,.97f):Ink;
            button.interactable=count>0;
        }
        float total=widths.Sum()+(zones.Length-1)*6,cursor=-total*.5f;
        handSourceTabs.sizeDelta=new Vector2(total,36);
        for(int i=0;i<zones.Length;i++){
            ((RectTransform)handSourceButtons[zones[i]].transform).anchoredPosition=new Vector2(cursor+widths[i]*.5f,0);cursor+=widths[i]+6;
        }
        return cards.Where(c=>HandSource(c)==selectedHandSource).ToArray();
    }
}
