using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private int browsedSeat;
    private string browsedZone, zoneBrowserSignature;
    private void PresentZonePiles(ForgePlayerView player,string path)
    {
        bool own=player.seat==seat;
        string plate=own?"PlayerPlate":"OpponentPlate";
        ArenaPlace(plate,own?Vector2.zero:Vector2.one,own?new Vector2(166,122):new Vector2(-166,-76),new Vector2(268,92));
        ArenaPlace(path,own?Vector2.zero:Vector2.one,own?new Vector2(166,52):new Vector2(-166,-146),new Vector2(268,48));
        var node=(RectTransform)Node(path);
        var shell=Node(plate).parent.Find(plate+"WithZones") as RectTransform;
        if(shell==null) {
            shell=Panel(Node(plate).parent,plate+"WithZones",Vector2.zero,new Vector2(268,140),new Color(.045f,.065f,.085f,.97f));
            shell.GetComponent<Image>().sprite=IntegratedUiUtility.RoundedRect(268,140,8);
            shell.GetComponent<Image>().raycastTarget=false;
            shell.gameObject.AddComponent<Outline>().effectColor=new Color(.3f,.4f,.48f,.5f);
        }
        shell.anchorMin=shell.anchorMax=own?Vector2.zero:Vector2.one;
        shell.anchoredPosition=own?new Vector2(166,98):new Vector2(-166,-100);
        shell.SetSiblingIndex(Math.Min(Node(plate).GetSiblingIndex(),node.GetSiblingIndex()));
        var outline=Node(plate).GetComponent<Outline>();if(outline!=null)outline.enabled=false;
        var backdrop=node.GetComponent<Image>();if(backdrop!=null)backdrop.color=Color.clear;
        if(node.Find("BarDivider")==null) {
            var divider=Panel(node,"BarDivider",new Vector2(0,24),new Vector2(236,1),new Color(.5f,.62f,.7f,.25f));
            divider.GetComponent<Image>().raycastTarget=false;
        }
        PresentZonePile(player,path,"牌库",0,player.libraryCount);
        var grave=ZoneCards(player,"坟场");var exile=ZoneCards(player,"放逐");
        PresentZonePile(player,path,"坟场",1,grave.Length);
        PresentZonePile(player,path,"放逐",2,Math.Max(player.exileCount,exile.Length));
    }
    private void PresentZonePile(ForgePlayerView player,string path,string zone,int index,int count)
    {
        string pilePath=path+"/Pile_"+zone;
        var root=(RectTransform)Node(pilePath);
        ArenaPlace(pilePath,new Vector2(.5f,.5f),new Vector2((index-1)*88,0),new Vector2(84,40));
        var button=SceneButton(pilePath);
        button.interactable=true;button.transition=Selectable.Transition.None;
        button.onClick.AddListener(()=>OpenZoneBrowser(player.seat,zone));
        var image=root.GetComponent<Image>();image.sprite=IntegratedUiUtility.RoundedRect(84,40,4);image.color=Color.clear;
        var outline=root.GetComponent<Outline>();if(outline!=null)outline.enabled=false;
        var face=root.Find("ZoneEntry") as RectTransform;
        if(face==null)
        {
            foreach(Transform child in root)child.gameObject.SetActive(false);
            face=Rect(root,"ZoneEntry",Vector2.zero,new Vector2(84,40));
            var icon=Rect(face,"Icon",new Vector2(-24,0),new Vector2(22,22)).gameObject.AddComponent<ZoneRegionIcon>();
            icon.Kind=index;icon.color=new Color(.73f,.8f,.85f);
            Label(face,"ZoneCaption",zone=="坟场"?"墓地":zone,new Vector2(15,0),new Vector2(48,24),14,Paper);
            Label(root,"HoverCount","",new Vector2(0,-34),new Vector2(68,26),18,Paper);
            if(index>0)Panel(root,"Separator",new Vector2(-44,0),new Vector2(1,20),new Color(.5f,.62f,.7f,.18f));
        }
        foreach(var graphic in root.GetComponentsInChildren<Graphic>(true))if(graphic.transform!=root)graphic.raycastTarget=false;
        var hover=root.GetComponent<ZonePileHover>()??root.gameObject.AddComponent<ZonePileHover>();
        hover.Configure(root.Find("HoverCount").GetComponent<Text>(),face,count);
    }
    private static ForgeCardView[] ZoneCards(ForgePlayerView player,string zone)
    {
        if(zone=="牌库")return player.reviewLibrary??Array.Empty<ForgeCardView>();
        if(zone=="放逐")return player.exileCards??player.reviewExile??Array.Empty<ForgeCardView>();
        if(player.graveyardCards!=null)return player.graveyardCards;
        if(player.reviewGraveyard!=null)return player.reviewGraveyard;
        // Older replays carried names only. Do not merge identical copies or invent live object IDs.
        return (player.graveyard??Array.Empty<string>()).Select((name,i)=>new ForgeCardView{id=-i-1,name=name,zone="Graveyard",ownerSeat=player.seat}).ToArray();
    }
    private void OpenZoneBrowser(int playerSeat,string zone)
    {
        if(IsCardPickerRequest || endDialog!=null&&endDialog.gameObject.activeSelf)return;
        CloseReviewCards();browsedSeat=playerSeat;browsedZone=zone;selectionPage=0;
        RefreshZoneBrowser();
    }
    private void RefreshZoneBrowser()
    {
        var player=view?.players?.FirstOrDefault(p=>p.seat==browsedSeat);
        if(player==null){CloseReviewCards();return;}
        bool hiddenLibrary=browsedZone=="牌库" && (player.seat!=seat||service?.IsSpectator==true||player.reviewLibrary==null);
        var cards=hiddenLibrary?Array.Empty<ForgeCardView>():ZoneCards(player,browsedZone);
        cards=cards.Select(c=>{
            if(!c.faceDown||string.IsNullOrEmpty(c.knownFaceName)||service?.IsSpectator==true)return c;
            var visible=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(c));
            visible.faceDown=false;visible.name=c.knownFaceName;visible.printingKey=c.knownFacePrintingKey;
            visible.currentRules=c.knownFaceRules;return visible;
        }).ToArray();
        string signature=browsedSeat+"/"+browsedZone+"/"+surface.rect.size+"/"+GamePreferences.Revision+"/"+player.libraryCount+"/"+string.Join("|",cards.Select(JsonUtility.ToJson));
        if(signature==zoneBrowserSignature && cardSelectionOverlay!=null && cardSelectionOverlay.gameObject.activeSelf)return;
        zoneBrowserSignature=signature;
        if(endDetail!=null)Destroy(endDetail.gameObject);
        string title=(player.seat==seat?"你的":"对手的")+(browsedZone=="坟场"?"墓地":browsedZone=="放逐"?"放逐区":"剩余牌库");
        RenderCardSelection(new ForgeRequest{kind="BrowseZone",options=Array.Empty<ForgeOption>(),previewCards=cards},title);
        if(hiddenLibrary) {
            selectionPanel.Find("Count").GetComponent<Text>().text=player.libraryCount+" 张";
            selectionPanel.Find("Instruction").GetComponent<Text>().text="牌库中的牌及其顺序为非公开信息。";
            selectionPanel.Find("Empty").GetComponent<Text>().text="通过效果查看牌库时，会显示可查看的卡牌。";
        }
    }
}
