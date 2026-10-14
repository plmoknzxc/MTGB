using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private static bool IsSprocketMarker(ForgeCardView c)=>c!=null&&c.effectMarker&&c.markerKind=="effect"&&c.helperArtName=="Contraption Sprockets";
    private static string SprocketName(int n)=>TapPaymentText("齿轮组 ","Sprocket ","からくり列 ")+n;
    private static string SprocketRules()=>TapPaymentText("在你的维持开始时，若你操控装置，将启动标记移到下一齿轮组，然后可以启动该组任意数量的装置。", "At the beginning of your upkeep, if you control a Contraption, move the CRANK! counter to the next sprocket and crank any number of that sprocket's Contraptions.","あなたのアップキープの開始時に、からくりをコントロールしているなら、クランク・カウンターを次の列へ移し、その列のからくりを望む数だけ動かす。");
    private static string AuxiliaryDetail(ForgeCardView c)
    {
        if(c.faceDown)return "";
        if(c.contraption&&c.zone=="Battlefield"&&c.sprocket>0)return TapPaymentText("所属齿轮组：","Assigned sprocket: ","所属するからくり列：")+c.sprocket;
        return c.attraction?TapPaymentText("亮灯号码：","Lit numbers: ","点灯番号：")+string.Join(" · ",(c.attractionLights??Array.Empty<int>()).OrderBy(n=>n)):"";
    }
    private void PresentAuxiliaryBadge(RectTransform face,Vector2 size,ForgeCardView c)
    {
        if(c.faceDown||c.zone!="Battlefield")return;
        string value=c.contraption&&c.sprocket>0?c.sprocket.ToString():c.attraction?string.Join("·",(c.attractionLights??Array.Empty<int>()).OrderBy(n=>n)):"";
        if(value.Length==0)return;
        float h=c.contraption?24:20,w=c.contraption?h:Mathf.Max(38,size.x-12);
        var badge=Panel(face,"AuxiliaryBadge",new Vector2(c.contraption?size.x/2-w/2-3:0,c.contraption?size.y/2-h/2-10:-size.y/2+h/2+3),new Vector2(w,h),Ink);
        badge.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(48,24,c.contraption?12:6);badge.GetComponent<Image>().raycastTarget=false;
        var label=Label(badge,"Value",value,Vector2.zero,new Vector2(w-2,h),c.contraption?16:12,Gold);label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;label.resizeTextForBestFit=false;
        h=Mathf.Max(h,Mathf.Ceil(label.preferredHeight)+2);if(c.contraption)w=Mathf.Max(w,h);
        badge.sizeDelta=new Vector2(w,h);label.rectTransform.sizeDelta=new Vector2(w-2,h);
        badge.anchoredPosition=new Vector2(c.contraption?size.x/2-w/2-3:0,c.contraption?size.y/2-h/2-10:-size.y/2+h/2+3);
    }
    private void PresentSprocketMarker(RectTransform face,Vector2 size,ForgeCardView card)
    {
        face.GetComponent<Image>().color=Color.clear;
        var match=Regex.Match(card.markerStatus??"",@"^CRANK! — ([1-3])$");int current=match.Success?int.Parse(match.Groups[1].Value):0;
        var player=(view?.players??Array.Empty<ForgePlayerView>()).FirstOrDefault(p=>p.seat==card.ownerSeat);
        for(int i=1;i<=3;i++){
            var circle=Panel(face,"Sprocket_"+i,new Vector2(0,(2-i)*30),new Vector2(28,28),i==current?Gold:Ink);
            circle.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(28,28,14);circle.GetComponent<Image>().raycastTarget=false;
            var label=Label(circle,"Value",i.ToString(),Vector2.zero,new Vector2(28,28),18,i==current?Ink:Paper);label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
            float diameter=Mathf.Max(28,Mathf.Ceil(label.preferredHeight)+2);circle.sizeDelta=Vector2.one*diameter;label.rectTransform.sizeDelta=circle.sizeDelta;circle.anchoredPosition=new Vector2(-20,(2-i)*(diameter+3));
            int count=(player?.battlefield??Array.Empty<ForgeCardView>()).Count(c=>c.contraption&&c.sprocket==i);
            var countLabel=Label(face,"SprocketCount_"+i,"×"+count,new Vector2(25,circle.anchoredPosition.y),new Vector2(42,diameter),14,Paper);countLabel.alignment=TextAnchor.MiddleCenter;countLabel.raycastTarget=false;
        }
    }
    private void PresentAuxiliaryEntry(ForgePlayerView p,RectTransform zoneBar)
    {
        bool visible=p.attractionDeckCount>0||p.contraptionDeckCount>0||(p.junkyardCards?.Length??0)>0||(p.battlefield??Array.Empty<ForgeCardView>()).Any(c=>c.attraction||c.contraption);
        var button=zoneBar.Find("AuxiliaryBrowse")?.GetComponent<Button>();
        if(button==null&&visible)button=StackIcon(zoneBar,"AuxiliaryBrowse",StackControlIcon.Kind.View,()=>{});
        if(button==null)return;
        button.gameObject.SetActive(visible);((RectTransform)button.transform).anchoredPosition=new Vector2(193,0);((RectTransform)button.transform).sizeDelta=new Vector2(34,34);
        button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>OpenZoneBrowser(p.seat,"垃圾场"));
    }
    private static string AuxiliarySummary(ForgePlayerView p)=>TapPaymentText($"游乐设施牌库 {p.attractionDeckCount} · 装置牌库 {p.contraptionDeckCount}\n当前启动齿轮组 {p.crankCounter} · 下次 {(p.crankCounter%3)+1}",$"Attraction deck {p.attractionDeckCount} · Contraption deck {p.contraptionDeckCount}\nCRANK {p.crankCounter} · Next {(p.crankCounter%3)+1}",$"アトラクション {p.attractionDeckCount} · からくり {p.contraptionDeckCount}\nクランク {p.crankCounter} · 次 {(p.crankCounter%3)+1}");
    private string AuxiliaryPrompt(string message)
    {
        if(message=="Choose contraptions to crank")return TapPaymentText("选择要启动的装置（可以不选）。","Choose contraptions to crank (you may choose none).","動かすからくりを選択（選ばなくてもよい）。");
        var match=Regex.Match(message??"",@"^Assign (.+) \((\d+)\) to a sprocket$");if(!match.Success)return null;
        var c=(view?.players??Array.Empty<ForgePlayerView>()).SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()).FirstOrDefault(c=>c.contraption&&c.id.ToString()==match.Groups[2].Value&&c.name==match.Groups[1].Value);
        return c==null?null:TapPaymentText("为","Assign ","")+GamePreferences.CardFaceName(c.name)+" ("+c.id+")"+TapPaymentText("选择齿轮组。"," to a sprocket.","のからくり列を選択。");
    }
    private string AuxiliaryOptionLabel(ForgeOption o)
    {
        if(view?.request?.kind!="ChooseOptions"||!o.id.StartsWith("select:",StringComparison.Ordinal)||!Regex.IsMatch(view.request.message??"",@"^Assign .+ \(\d+\) to a sprocket$"))return null;
        var m=Regex.Match(o.label??"",@"^([1-3])(?: \((\d+) Contraptions?\))?( \(Cranked next turn\))?$");if(!m.Success)return null;
        return SprocketName(int.Parse(m.Groups[1].Value))+(m.Groups[2].Success?TapPaymentText(" · 装置 "," · Contraptions "," · からくり ")+m.Groups[2].Value:"")+(m.Groups[3].Success?TapPaymentText("（下次启动）"," (Next crank)","（次に動く）"):"");
    }
}
