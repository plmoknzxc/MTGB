using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform matchPreparationRoot;
    private string preparationKey;
    private bool playDrawSubmitted;
    private bool WaitingForOpeningView => service!=null && service.IsInMatch
        && (service.ForgeView?.forge?.players==null || service.ForgeView.forge.players.Length<2);
    private void CloseMatchPreparation()
    {
        if(matchPreparationRoot!=null)Destroy(matchPreparationRoot.gameObject);
        matchPreparationRoot=null;preparationKey=null;
    }
    private void PresentMatchPreparation()
    {
        Build();
        var lobby=service.LobbyState;
        int chooser=lobby?.playDrawChooserSeat??0;
        if(chooser==0){CloseMatchPreparation();return;}
        string key=chooser+"/"+service.LocalSeat+"/"+service.IsConnected+"/"+PlayerNamesKey()+"/"+GamePreferences.Revision;
        if(preparationKey==key)return;
        preparationKey=key;playDrawSubmitted=false;
        if(endCanvas!=null){Destroy(endCanvas.gameObject);endCanvas=null;}
        if(leaveReviewButton!=null){Destroy(leaveReviewButton.gameObject);leaveReviewButton=null;}
        CloseReviewCards();reviewedMatch=null;
        if(integrated)Node("CornerCluster/ControlRow").gameObject.SetActive(true);
        var seats=(lobby?.players??new System.Collections.Generic.List<OnlineLobbyPlayer>()).Where(p=>p.seat<=2).Select(p=>new ForgePlayerView{seat=p.seat,life=lobby?.config?.startingLife??20,hand=Array.Empty<ForgeCardView>(),battlefield=Array.Empty<ForgeCardView>()}).ToArray();
        Present(new ForgeSeatView{players=seats},service.IsSpectator?1:service.LocalSeat,false,null,null,"");
        if(matchPreparationRoot!=null)Destroy(matchPreparationRoot.gameObject);
        matchPreparationRoot=Panel(surface,"BattlefieldPlayDraw",Vector2.zero,surface.rect.size,new Color(0,0,0,.42f));
        SetOverlayLayer(matchPreparationRoot,30,true);
        var dialog=Panel(matchPreparationRoot,"Choice",Vector2.zero,new Vector2(620,242),Ink);
        bool local=chooser>0&&chooser==service.LocalSeat;
        string title=chooser==0?TapPaymentText("正在准备起手牌…","Preparing opening hands…","初手を準備中…"):local?TapPaymentText("选择先攻或后攻","Choose play or draw","先攻か後攻を選択"):TapPaymentText("等待 "+PlayerDisplayName(chooser)+" 选择先后攻","Waiting for "+PlayerDisplayName(chooser)+" to choose play or draw",PlayerDisplayName(chooser)+"の先攻・後攻選択を待機中");
        Label(dialog,"Title",title,new Vector2(0,70),new Vector2(572,48),28,Paper);
        Label(dialog,"Help",TapPaymentText("选择后发放起手牌，再决定保留或调度","Choose before opening hands and mulligans","選択後に初手を配り、マリガンを行います"),new Vector2(0,20),new Vector2(572,34),18,Paper);
        if(local){
            var first=Button(dialog,"PlayFirst",TapPaymentText("先攻","Play","先攻"),new Vector2(-148,-58),new Vector2(264,54),()=>SubmitPlayDraw(true),Gold);
            var draw=Button(dialog,"DrawFirst",TapPaymentText("后攻","Draw","後攻"),new Vector2(148,-58),new Vector2(264,54),()=>SubmitPlayDraw(false),Gold);
            first.interactable=draw.interactable=service.IsConnected;
        }
    }
    private void SubmitPlayDraw(bool first)
    {
        if(playDrawSubmitted || service?.IsConnected!=true || service.LobbyState?.playDrawChooserSeat!=service.LocalSeat)return;
        playDrawSubmitted=true;
        foreach(var button in matchPreparationRoot.GetComponentsInChildren<Button>())button.interactable=false;
        service.ChoosePlayDraw(first);
    }
}
