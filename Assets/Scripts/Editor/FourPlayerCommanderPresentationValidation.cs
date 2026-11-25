#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A display-only sample for the dedicated scene. It does not register a
// four-player match with Forge or the online room service.
public static class FourPlayerCommanderPresentationValidation
{
    private static ForgeSeatView LoadSample() => JsonConvert.DeserializeObject<ForgeSeatView>(
        File.ReadAllText("Assets/Scenes/PreviewData/CommanderFourSeatSample.json"));

    public static void ShowOpening()
    {
        if(!Application.isPlaying || SceneManager.GetActiveScene().name!=ForgeBattlefieldController.FourPlayerSceneName)
            throw new InvalidOperationException("Open the four-seat scene in Play Mode first.");
        var view=LoadSample();
        foreach(var player in view.players) {
            var field=player.battlefield;
            player.battlefield=new[]{field.First(c=>c.creature),field.First(c=>c.land),field.First(c=>!c.land&&!c.creature)};
        }
        UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>().Present(view,1,false,null,null,"");
    }

    // Run after ShowOpening has settled, with the default seat arrangement.
    public static string ValidateOpeningRanks()
    {
        var view=LoadSample();
        var cards=UnityEngine.Object.FindObjectsByType<BattlefieldWorldCard>(FindObjectsSortMode.None)
            .Where(c=>c.Anchor!=null && c.Anchor.parent.name=="ForgePermanents")
            .ToDictionary(c=>c.Anchor.name);
        foreach(var player in view.players){
            var creature=player.battlefield.First(c=>c.creature);
            var land=player.battlefield.First(c=>c.land && !c.creature);
            var front=cards["Card_"+creature.id].Anchor;
            var rear=cards["Card_"+land.id].Anchor;
            float gap=front.anchoredPosition.y-rear.anchoredPosition.y;
            if(player.seat is 2 or 3)gap=-gap;
            if(gap<(front.rect.height+rear.rect.height)*.5f)
                throw new InvalidOperationException("Creature and land ranks overlap in seat "+player.seat);
        }
        return "Creature and land ranks separate and face the table centre in all four seats.";
    }

    public static void ShowCrowded(bool selectTargets=false,bool singleCrowdedSeat=false)
    {
        if(!Application.isPlaying||SceneManager.GetActiveScene().name!=ForgeBattlefieldController.FourPlayerSceneName)
            throw new InvalidOperationException("Open the four-seat scene in Play Mode first.");
        var view=LoadSample();
        if(singleCrowdedSeat)foreach(var player in view.players.Where(p=>p.seat!=2)) {
            var field=player.battlefield;
            player.battlefield=field.Where(c=>c.creature).Take(3)
                .Concat(field.Where(c=>!c.creature).GroupBy(c=>c.name).Take(4).SelectMany(g=>g)).ToArray();
        }
        if(selectTargets) {
            string[] picks={"Skullclamp","Sol Ring","Mind Stone"};
            var choices=view.players.Skip(1).Select((p,i)=>p.battlefield.First(c=>c.name==picks[i]))
                .Select(c=>new ForgeOption{id="card:"+c.id,label=c.name,card=c,selected=true,chosenCount=1}).ToList();
            choices.Add(new ForgeOption{id="ok",label="OK"});
            view.request=new ForgeRequest{requestId=901,contextId=900,kind="InputSelectTargets",seat=1,playerSeat=1,
                abilityKind="spell",sourceCard=view.players[0].hand[0],selectedCount=3,minCount=3,maxCount=3,options=choices.ToArray()};
            view.awaitingInput=true;
        }
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        ui.Present(view,1,selectTargets,_=>{},null,"");
    }

    // Read-only fixture for inspecting partner names, hidden zones and a stack with four controllers.
    public static void ShowPartnersAndStack(int viewer=1,bool stack=true)
        =>ShowSeatSample(PartnerView(viewer,stack),viewer);

    private static void ShowSeatSample(ForgeSeatView view,int viewer)
        =>UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>().Present(view,viewer,false,null,null,"");

    private static ForgeSeatView PartnerView(int viewer,bool stack)
    {
        var view=LoadSample();
        int id=120000;
        ForgeCardView Card(string name,string zone,int owner){
            var printing=CardDatabaseRepository.LoadAll().First(c=>c.cardName==name);
            return new ForgeCardView{id=++id,name=name,printingKey=printing.PrintingKey,zone=zone,
                ownerSeat=owner,commander=zone=="Command",manaCost=printing.manaCost,
                currentRules=printing.description};
        }
        var first=view.players[0];
        var second=view.players[1];
        first.commanders=new[]{Card("Thrasios, Triton Hero","Command",1),Card("Tymna the Weaver","Command",1)};
        second.commanders=new[]{Card("Kraum, Ludevic's Opus","Command",2),Card("Vial Smasher the Fierce","Command",2)};
        first.commandCards=first.commanders;second.commandCards=second.commanders;
        first.commanderCastCounts=new[]{1,0};
        second.hand=new[]{Card("Mountain","Hand",2)};
        second.graveyardCards=new[]{Card("Island","Graveyard",2)};
        second.exileCards=new[]{Card("Swamp","Exile",2)};second.exileCount=1;
        string[] spells={"Brainstorm","Consider","Opt","Thrill of Possibility"};
        view.stackItems=stack?spells.Select((name,i)=>new ForgeStackItem{id=1400+i,kind="spell",controllerSeat=i+1,
            source=Card(name,"Stack",i+1)}).ToArray():Array.Empty<ForgeStackItem>();
        view.stackCount=view.stackItems.Length;
        view.stack=view.stackItems.Select(item=>item.source.name).ToArray();
        view.seat=viewer;
        return view;
    }

    public static void ShowSeatSources(int viewer=1,bool consumed=false,bool withSideboard=false)
    {
        var view=PartnerView(viewer,true);
        int id=123000;
        ForgeCardView Card(string name,string zone,int owner){
            var printing=CardDatabaseRepository.LoadAll().First(c=>c.cardName==name);
            return new ForgeCardView{id=++id,name=name,printingKey=printing.PrintingKey,zone=zone,
                ownerSeat=owner,manaCost=printing.manaCost,currentRules=printing.description};
        }
        var opponent=view.players[1];
        var companion=Card("Obosh, the Preypiercer",consumed?"Hand":"Command",2);
        companion.keywords=new[]{"COMPANION"};
        if(consumed){opponent.hand=opponent.hand.Append(companion).ToArray();opponent.handCount++;}
        else opponent.commandCards=opponent.commanders.Append(companion).ToArray();
        opponent.exileCards=opponent.exileCards.Concat(new[]{Card("Mountain","Exile",2),Card("Island","Exile",2)}).ToArray();
        opponent.exileCount=opponent.exileCards.Length;
        opponent.graveyardCards=opponent.graveyardCards.Append(Card("Faithless Looting","Graveyard",2)).ToArray();
        opponent.publicPlayableExile=consumed?Array.Empty<ForgeCardView>():opponent.exileCards.Skip(1).Take(1).ToArray();
        opponent.publicPlayableGraveyard=consumed?Array.Empty<ForgeCardView>():opponent.graveyardCards.Skip(1).ToArray();
        view.players[viewer-1].sideboardCards=withSideboard?new[]{Card("Island","Sideboard",viewer)}:Array.Empty<ForgeCardView>();
        ShowSeatSample(view,viewer);
    }

    // Call after ShowPartnersAndStack and a rendered frame, including after resizing a seat.
    public static string ValidatePartnersAndStack(int viewer=1)
    {
        var canvas=GameObject.Find("Canvas").transform;
        var owners=canvas.Find("StackColumn").GetComponentsInChildren<Text>().Where(t=>t.name=="CompactOwner").ToArray();
        var colors=new HashSet<Color32>();
        for(int s=1;s<=4;s++){
            bool local=s==viewer;
            var plate=canvas.Find(local?"PlayerPlate":"FourSeat_"+s);
            var name=plate.Find("Name").GetComponent<Text>();
            colors.Add(name.color);
            string identity="<color=#"+ColorUtility.ToHtmlStringRGB(name.color)+">"+name.text+"</color>";
            if(!owners.Any(t=>t.supportRichText && t.text.Contains(identity)))
                throw new InvalidOperationException("Stack does not identify seat "+s);
            var label=plate.Find(local?"Sub":"CommanderName").GetComponent<Text>();
            var visible=System.Text.RegularExpressions.Regex.Replace(label.text,"<[^>]*>","");
            if(!label.gameObject.activeInHierarchy || visible.Length==0 ||
                (s<=2 && !(local?visible.Contains("\n"):visible.Contains(" / "))) ||
                label.cachedTextGenerator.characterCountVisible<visible.Length ||
                (label.resizeTextForBestFit?label.cachedTextGenerator.fontSizeUsedForBestFit:label.fontSize)<15)
                throw new InvalidOperationException("Missing or clipped commander at seat "+s+": "+label.text);
        }
        if(colors.Count!=4)throw new InvalidOperationException("Seat colors are not distinct");
        return "Four seat colors match the stack; both partner pairs fit in their labels.";
    }

    public static string ValidateDensity()
    {
        ShowCrowded(true);
        Canvas.ForceUpdateCanvases();
        var canvas=GameObject.Find("Canvas").transform;
        Transform Page(int seat)=>canvas.Find(seat==1?"FourLocalPager":"FourSeat_"+seat);
        void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
        void Click(Transform button){
            var b=button.GetComponent<Button>();
            Check(b.isActiveAndEnabled&&b.interactable,"Unavailable "+button.name);
            b.onClick.Invoke();Canvas.ForceUpdateCanvases();
        }
        for(int seat=1;seat<=4;seat++)
            while(Page(seat).Find("Previous").GetComponent<Button>().interactable)Click(Page(seat).Find("Previous"));
        int CountField()=>canvas.Find("ForgePermanents").Cast<Transform>().Count(t=>t.gameObject.activeInHierarchy && t.name.StartsWith("Card_"));
        Check(CountField()==32,"Expected eight visible groups for each seat");
        for(int seat=1;seat<=4;seat++){
            Click(Page(seat).Find("Next"));
            Check(CountField()==32,"Second page lost a card");
        }
        for(int seat=1;seat<=4;seat++){
            Click(Page(seat).Find("Next"));
            Check(CountField()==32-7*seat,"Paging changed another seat");
            Check(!Page(seat).Find("Next").GetComponent<Button>().interactable,"Page after the last");
        }
        for(int seat=1;seat<=4;seat++){
            Click(Page(seat).Find("Previous"));
            Click(Page(seat).Find("Previous"));
        }
        var controller=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        controller.ViewBattlefield();
        var list=canvas.Find("FourSelectedTargets/List/Content");
        Check(list!=null&&list.Cast<Transform>().Count(t=>t.gameObject.activeInHierarchy)==3,"Expected three target links");
        Click(list.GetChild(0));
        Check(Page(2).Find("Page").GetComponent<Text>().text=="2 / 3" && CountField()==32,"Off-page target not located");
        Check(Page(1).Find("Page").GetComponent<Text>().text=="1 / 3" && Page(3).Find("Page").GetComponent<Text>().text=="1 / 3" &&
            Page(4).Find("Page").GetComponent<Text>().text=="1 / 3","Jump changed an unrelated page");
        controller.RestoreBattlefieldWindows();
        return "Four seats: 32 / 32 / 4 visible groups; independent 8+8+1 pages; three selected targets; located seat 2 on page 2.";
    }

    // Run on a settled Play Mode frame, after ShowOpening or ShowCrowded and any page transitions.
    public static string ValidateTableGeometry()
    {
        var camera=UnityEngine.Object.FindFirstObjectByType<BattlefieldWorldTable>().TableCamera;
        var bounds=new List<Rect>();
        var hits=new List<RaycastResult>();
        int covered=0;
        var canvas=GameObject.Find("Canvas").GetComponent<Canvas>();
        var stack=canvas.transform.Find("StackColumn") as RectTransform;
        var hand=canvas.transform.Find("RedesignHandBackplate") as RectTransform;
        bool InPanel(RectTransform panel,Vector2 point) {
            return panel!=null&&panel.gameObject.activeInHierarchy&&
                RectTransformUtility.RectangleContainsScreenPoint(panel,point,canvas.worldCamera);
        }
        foreach(var card in UnityEngine.Object.FindObjectsByType<BattlefieldWorldCard>(FindObjectsSortMode.None)){
            if(card.UsesHudCamera || card.Anchor==null || !card.Anchor.gameObject.activeInHierarchy || card.Anchor.parent.name!="ForgePermanents")continue;
            var box=card.GetComponent<BoxCollider>();
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            var max=-min;
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1}){
                Vector3 corner=box.center+new Vector3(x*box.size.x*.5f,y*box.size.y*.5f,0);
                Vector2 point=camera.WorldToScreenPoint(card.transform.TransformPoint(corner));
                min=Vector2.Min(min,point);max=Vector2.Max(max,point);
            }
            var rect=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            if(min.x<0 || min.y<0 || max.x>Screen.width || max.y>Screen.height || bounds.Any(b=>b.Overlaps(rect)))
                throw new InvalidOperationException("Field card overlaps another card or the viewport edge: "+card.Anchor.name);
            bounds.Add(rect);
            hits.Clear();
            var pointer=new PointerEventData(EventSystem.current){position=rect.center};
            EventSystem.current.RaycastAll(pointer,hits);
            var top=hits.Count>0?hits[0].gameObject.transform:null;
            if(top!=null&&top.IsChildOf(card.Anchor)){
                if(InPanel(stack,rect.center)||InPanel(hand,rect.center))
                    throw new InvalidOperationException("Click passed through the visible HUD to "+card.Anchor.name);
                continue;
            }
            var layer=top;
            while(layer!=null && layer.name is not ("StackColumn" or "RedesignStackControls" or "PlayerHandRoot" or "RedesignHandBackplate" or "HandSourceTabs"))
                layer=layer.parent;
            if(layer==null)throw new InvalidOperationException("Field card centre blocked: "+card.Anchor.name+" by "+top?.name);
            covered++;
        }
        if(bounds.Count==0)throw new InvalidOperationException("No field cards");
        var width=bounds.OrderBy(r=>r.width).ElementAt(bounds.Count/2).width;
        var height=bounds.OrderBy(r=>r.height).ElementAt(bounds.Count/2).height;
        return $"{bounds.Count} field cards at {Screen.width}x{Screen.height}, {bounds.Count-covered} exposed and clickable, {covered} behind a floating HUD, median {width:F0}x{height:F0} pixels.";
    }

    public static void Preview()
    {
        if(!Application.isPlaying||SceneManager.GetActiveScene().name!=ForgeBattlefieldController.FourPlayerSceneName)
            throw new InvalidOperationException("Open IntegratedBattlefieldCommander4 in Play Mode first.");
        var view=DuelLayoutPresentationValidation.DensityFixture(false);
        var third=JsonUtility.FromJson<ForgePlayerView>(JsonUtility.ToJson(view.players[1]));
        var fourth=JsonUtility.FromJson<ForgePlayerView>(JsonUtility.ToJson(view.players[1]));
        third.seat=3;third.targetId=3;fourth.seat=4;fourth.targetId=4;
        view.players=new[]{view.players[0],view.players[1],third,fourth};
        view.formatId="commander";
        foreach(var player in view.players) {
            player.life=40;player.handCount=7;player.hand=Array.Empty<ForgeCardView>();
            foreach(var card in player.battlefield) {
                if(player.seat>=3)card.id+=player.seat*100000;
                card.ownerSeat=player.seat;
            }
            var source=player.battlefield.First(card=>card.planeswalker);
            var commander=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(source));
            commander.id=900000+player.seat;commander.ownerSeat=player.seat;
            commander.zone="Command";commander.commander=true;
            player.commanders=new[]{commander};player.commandCards=new[]{commander};
            player.effectMarkers=Array.Empty<ForgeCardView>();player.emblems=Array.Empty<ForgeCardView>();
        }
        view.request=null;view.awaitingInput=false;view.inputPending=false;
        view.stackItems=Array.Empty<ForgeStackItem>();view.stack=Array.Empty<string>();view.stackCount=0;
        view.firstPlayerSeat=3;
        view.mulliganCounts=new[]{0,1,0,2};
        view.mulliganHandSizes=new[]{7,6,7,5};
        view.mulliganKept=new[]{true,true,true,true};
        view.notices=view.players.Select((player,index)=>new ForgeNotice{
            id=940000+index,kind="cast",actorSeat=player.seat,turn=index+1,
            turnSeat=player.seat,phase="MAIN1",fromZone="Hand",
            sourceCard=player.battlefield.FirstOrDefault(card=>!card.land)
        }).ToArray();
        view.turn=4;view.turnSeat=4;view.phase="MAIN1";
        var controller=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()
            ??new GameObject("FourSeatPreview").AddComponent<ForgeBattlefieldController>();
        controller.enabled=false; // Keep any local practice service from replacing this fixture.
        controller.GetType().GetMethod("ResetSidebarHistory",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(controller,null);
        controller.Present(view,view.seat,true,_=>{},null,"");
        var root=GameObject.Find("Canvas").transform;
        for(int participant=2;participant<=4;participant++)
            if(root.Find("FourSeat_"+participant)==null)
                throw new InvalidOperationException("Missing four-seat player plate "+participant);
        int visibleWorldCards=UnityEngine.Object.FindObjectsByType<BattlefieldWorldCard>(FindObjectsSortMode.None)
            .Count(card=>card.gameObject.activeInHierarchy);
        if(visibleWorldCards<27)throw new InvalidOperationException("A four-seat battlefield is missing cards: "+visibleWorldCards);
    }
}
#endif
