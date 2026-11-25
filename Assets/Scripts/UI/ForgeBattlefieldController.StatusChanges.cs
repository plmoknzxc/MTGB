using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform statusChangeBanner;
    private Text statusChangeLabel;
    private Image statusChangeArt;
    private float statusChangeExpires;
    private bool statusBaselineReady;
    private string observedDayTime;
    private Dictionary<(int seat,int id),ForgeCardView> previousStatuses=new(), currentStatuses=new();
    private readonly Queue<(string label,Sprite art)> statusChanges=new();

    private void ResetStatusChanges()
    {
        sidebarInitializedSeats.Clear();
        statusBaselineReady=false;
        observedDayTime=null;
        previousStatuses.Clear();
        currentStatuses.Clear();
        statusChanges.Clear();
        statusChangeExpires=0;
        if(statusChangeBanner!=null)statusChangeBanner.gameObject.SetActive(false);
    }

    private void ObserveStatusChanges()
    {
        if(!integrated || view?.players==null || view.players.Length<2)return;
        string day=view.dayTime??"";
        if(statusBaselineReady && day!=observedDayTime && (day=="day" || day=="night")){
            bool night=day=="night";
            SpecialEffectArt.TryLoad(night?"Night":"Day",out var art);
            statusChanges.Enqueue((night?TapPaymentText("转为黑夜","Night falls","夜になる")
                :TapPaymentText("转为白昼","Day breaks","昼になる"),art));
        }
        observedDayTime=day;
        currentStatuses.Clear();
        foreach(var player in view.players){
            if(player==null)continue;
            Observe(player,VisibleEffectMarkers(player));
            Observe(player,player.emblems);
        }
        if(statusBaselineReady){
            foreach(var previous in previousStatuses){
                if(currentStatuses.ContainsKey(previous.Key))continue;
                // A transferred marker already announced its new holder.
                bool transferred=false;
                foreach(var player in view.players)
                    if(player!=null && currentStatuses.ContainsKey((player.seat,previous.Key.id))){transferred=true;break;}
                if(!transferred)QueueStatusChange(previous.Key.seat,previous.Value,true);
            }
        }
        (previousStatuses,currentStatuses)=(currentStatuses,previousStatuses);
        statusBaselineReady=true;
        UpdateStatusChange();

        void Observe(ForgePlayerView player,IEnumerable<ForgeCardView> markers)
        {
            if(markers==null)return;
            foreach(var marker in markers){
                if(marker==null || marker.faceDown || string.IsNullOrWhiteSpace(marker.name) || marker.markerKind=="game")continue;
                var key=(player.seat,marker.id);
                if(!currentStatuses.TryAdd(key,marker) || !statusBaselineReady)continue;
                if(!previousStatuses.TryGetValue(key,out var old) || old.name!=marker.name)
                    QueueStatusChange(player.seat,marker,false);
            }
        }
    }

    private void QueueStatusChange(int seat, ForgeCardView marker, bool removed)
    {
        string title=marker.emblem?EmblemLocalizedName(marker):GamePreferences.CardFaceName(MarkerName(marker));
        if(removed)title+=TapPaymentText(" · 已结束"," · ended"," · 終了");
        TryBattlefieldArt(marker,out var art);
        statusChanges.Enqueue((PlayerDisplayName(seat)+" · "+title,art));
    }

    private void UpdateStatusChange()
    {
        if(statusChangeExpires>0){
            if(Time.unscaledTime<statusChangeExpires)return;
            statusChangeExpires=0;
            BattlefieldUiTransition.On(statusChangeBanner).Hide(new Vector2(0,8));
        }
        if(statusChanges.Count==0 || (statusChangeBanner!=null && statusChangeBanner.gameObject.activeSelf))return;
        var next=statusChanges.Dequeue();
        if(statusChangeBanner==null){
            statusChangeBanner=Panel(surface,"StatusChangeBanner",Vector2.zero,new Vector2(340,48),HudInk);
            statusChangeBanner.anchorMin=statusChangeBanner.anchorMax=new Vector2(.5f,.75f);
            StyleHudSurface(statusChangeBanner,HudInk,HudEdge);
            SetOverlayLayer(statusChangeBanner,NoticeLayerOffset,false);
            statusChangeArt=Rect(statusChangeBanner,"Art",Vector2.zero,new Vector2(34,34)).gameObject.AddComponent<Image>();
            statusChangeArt.preserveAspect=true;
            statusChangeLabel=Label(statusChangeBanner,"Change","",Vector2.zero,new Vector2(325,46),20,HudText);
            MenuTypography.Use(statusChangeLabel,MenuTypography.Face.Heading);
            statusChangeLabel.alignment=TextAnchor.MiddleCenter;
            foreach(var graphic in statusChangeBanner.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
        }
        statusChangeLabel.text=next.label;
        bool hasArt=next.art!=null;
        statusChangeArt.sprite=next.art;
        statusChangeArt.gameObject.SetActive(hasArt);
        float w=Mathf.Clamp(statusChangeLabel.preferredWidth+(hasArt?74:38),158,Mathf.Max(158,surface.rect.width-60));
        float textWidth=w-(hasArt?62:26);
        statusChangeLabel.rectTransform.sizeDelta=new Vector2(textWidth,48);
        float height=Mathf.Max(48,statusChangeLabel.preferredHeight+14);
        statusChangeLabel.rectTransform.sizeDelta=new Vector2(textWidth,height);
        statusChangeLabel.rectTransform.anchoredPosition=new Vector2(hasArt?19:0,0);
        statusChangeArt.rectTransform.anchoredPosition=new Vector2(25-w*.5f,0);
        BattlefieldUiTransition.On(statusChangeBanner).Place(Vector2.zero,new Vector2(w,height),1,new Vector2(0,-10),false);
        statusChangeExpires=Time.unscaledTime+2;
    }
}
