#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

public static class ArenaPresentationValidation
{
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode in IntegratedBattlefield first.");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        ui.StartCoroutine(Validate(ui));
    }
    public static void RunStack()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        ui.StartCoroutine(ValidateStack(ui));
    }
    private static IEnumerator ValidateStack(ForgeBattlefieldController ui)
    {
        const string folder="Tools/Validation/Reports/LargeStack";Directory.CreateDirectory(folder);File.WriteAllText(folder+"/result.txt","RUNNING");
        var top=new ForgeStackItem{id=1,kind="spell",controllerSeat=1,description="Lightning Bolt",source=Card("Lightning Bolt",910)};
        var state=new ForgeSeatView{seat=1,turn=2,turnSeat=1,phase="MAIN1",stackCount=1,stackItems=new[]{top},stack=new[]{top.description},
            players=new[]{new ForgePlayerView{seat=1,life=20,hand=Array.Empty<ForgeCardView>(),battlefield=Array.Empty<ForgeCardView>(),graveyard=Array.Empty<string>()},
                new ForgePlayerView{seat=2,life=20,hand=Array.Empty<ForgeCardView>(),battlefield=Array.Empty<ForgeCardView>(),graveyard=Array.Empty<string>()}}};
        Action present=()=>ui.Present(state,1,true,null,null,"堆叠展示验证");present();yield return new WaitForSecondsRealtime(.5f);
        var canvas=GameObject.Find("Canvas").transform;var viewport=(RectTransform)canvas.Find("StackColumn/Viewport");var scroll=viewport.GetComponent<ScrollRect>();
        var card=GameObject.Find("StackCard_0").GetComponent<Image>();
        Require(card.sprite!=null && card.preserveAspect && card.rectTransform.rect.width>=240,"Stack card has not been enlarged or lacks full art.");
        var corners=new Vector3[4];card.rectTransform.GetWorldCorners(corners);
        Require(corners.All(c=>viewport.rect.Contains(viewport.InverseTransformPoint(c))),"Enlarged stack card is clipped.");
        Require(!canvas.Find("StackColumn/StackScrollbar").gameObject.activeSelf,"Single stack card should not show a scrollbar.");
        ScreenCapture.CaptureScreenshot(folder+"/single.png");yield return new WaitForEndOfFrame();
        state.stackCount=3;state.stackItems=new[]{top,new ForgeStackItem{id=2,kind="spell",controllerSeat=2,description="Hill Giant",source=Card("Hill Giant",911)},new ForgeStackItem{id=3,kind="spell",controllerSeat=1,description="Shock",source=Card("Shock",912)}};
        state.stack=state.stackItems.Select(e=>e.description).ToArray();present();
        // Arrival temporarily blocks pointer events; verify the settled interactive stack.
        yield return new WaitForSecondsRealtime(.6f);
        Canvas.ForceUpdateCanvases();
        var topRow=(RectTransform)GameObject.Find("StackCard_0").transform.parent;
        var bottomRow=(RectTransform)GameObject.Find("StackCard_2").transform.parent;
        Require(Mathf.Abs(topRow.anchoredPosition.y-bottomRow.anchoredPosition.y)<100 && topRow.GetSiblingIndex()>bottomRow.GetSiblingIndex(),"Stack is not layered with top item in front.");
        for(int i=1;i<3;i++) {
            var tile=(RectTransform)GameObject.Find("StackCard_"+i).transform;
            var above=(RectTransform)GameObject.Find("StackCard_"+(i-1)).transform.parent;
            Require(above.anchoredPosition.x-((RectTransform)tile.parent).anchoredPosition.x>=70,"Covered card exposes only a sliver");
            Vector2 point=RectTransformUtility.WorldToScreenPoint(null,tile.TransformPoint(new Vector2(-90,-90)));
            var hits=new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
            Require(hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>()==tile.GetComponent<Button>(),"Exposed stack card cannot be hovered/clicked");
        }
        ScreenCapture.CaptureScreenshot(folder+"/multiple.png");yield return new WaitForEndOfFrame();
        scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();yield return null;
        var last=GameObject.Find("StackCard_2").GetComponent<Image>();last.rectTransform.GetWorldCorners(corners);
        Require(corners.All(c=>viewport.rect.Contains(viewport.InverseTransformPoint(c))),"Last stack card cannot be scrolled fully into view.");
        float offset=scroll.content.anchoredPosition.y;present();yield return null;
        Require(Mathf.Abs(scroll.content.anchoredPosition.y-offset)<1,"Same stack snapshot reset the scroll position.");
        var hover=new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(last.gameObject,hover,ExecuteEvents.pointerEnterHandler);
        Require(canvas.Find("InspectPanel").gameObject.activeSelf,"Stack hover does not open details.");
        ExecuteEvents.Execute(last.gameObject,hover,ExecuteEvents.pointerExitHandler);
        state.awaitingInput=true;state.request=new ForgeRequest{requestId=930,seat=1,kind="InputSelectTargets",options=new[]{new ForgeOption{id="player:1",label="Player 2"}}};present();yield return new WaitForSecondsRealtime(.4f);
        var stack=(RectTransform)canvas.Find("StackColumn");var preview=(RectTransform)GameObject.Find("TargetOrigin").transform;
        var stackCorners=new Vector3[4];var previewCorners=new Vector3[4];stack.GetWorldCorners(stackCorners);preview.GetWorldCorners(previewCorners);
        Require(stackCorners.Max(c=>c.x)<previewCorners.Min(c=>c.x),"Larger stack overlaps target selection preview.");
        state.awaitingInput=false;state.stackCount=0;state.stackItems=Array.Empty<ForgeStackItem>();state.stack=Array.Empty<string>();present();yield return null;
        Require(!stack.gameObject.activeSelf,"Empty stack panel remains visible.");
        const string result="PASS: 240px full card image; single-card viewport fit; conditional scrollbar; three entries layered with top in front; unchanged stack preserves scroll; hover details; target preview avoids overlap; empty stack hidden.";
        File.WriteAllText(folder+"/result.txt",result);Debug.Log(result);
    }
    private static ForgeCardView Card(string name,int id,bool land=false)
    {
        var definition=CardDatabaseRepository.LoadAll().First(c=>c.cardName==name && !c.forgeAmbiguous);
        return new ForgeCardView{id=id,name=name,printingKey=definition.PrintingKey,land=land,creature=!land,power=2,toughness=1};
    }
    private static Button Find(int id)=>UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="Card_"+id && b.gameObject.activeInHierarchy);
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    private static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        string folder=".tools/forge-migration/arena";Directory.CreateDirectory(folder);File.WriteAllText(folder+"/result.txt","RUNNING");
        var hand=Enumerable.Range(0,7).Select(i=>Card(i%2==0?"Mountain":"Raging Goblin",100+i,i%2==0)).ToArray();
        var lands=Enumerable.Range(0,5).Select(i=>Card("Mountain",200+i,true)).ToArray();lands[3].tapped=lands[4].tapped=true;
        var creature=Card("Goblin Piker",300);var second=Card("Goblin Piker",301);second.attacking=true;second.tapped=true;
        var state=new ForgeSeatView{seat=1,turn=3,turnSeat=1,phase="MAIN1",awaitingInput=true,request=new ForgeRequest{requestId=700,kind="InputPassPriority",seat=1,options=hand.Concat(lands).Select(c=>new ForgeOption{id="card:"+c.id,label=c.name+" [Hand]"}).Concat(new[]{new ForgeOption{id="ok",label="OK"}}).ToArray()},players=new[]{
            new ForgePlayerView{seat=1,life=18,handCount=7,libraryCount=45,hand=hand,battlefield=lands.Concat(new[]{creature,second}).ToArray(),graveyard=Array.Empty<string>()},
            new ForgePlayerView{seat=2,life=15,handCount=4,libraryCount=47,hand=Array.Empty<ForgeCardView>(),battlefield=new[]{Card("Goblin Piker",400),Card("Mountain",500,true),Card("Mountain",501,true)},graveyard=Array.Empty<string>()}}};
        int submitted=0;
        ui.Present(state,1,true,_=>submitted++,null,"交互演示 · 测试状态");
        yield return new WaitForSecondsRealtime(.5f);
        var phaseButton=GameObject.Find("Canvas").transform.Find("CornerCluster/ControlRow/SkipCombatButton");
        Require(phaseButton.GetComponentInChildren<Text>().text=="结束阶段","Own empty-stack priority should show end phase.");
        Require(!GameObject.Find("Canvas").transform.Find("CornerCluster/ControlRow/EndTurnButton").gameObject.activeSelf,"End-turn shortcut remains visible.");
        var playerPlate=(RectTransform)GameObject.Find("PlayerPlate").transform;var opponentPlate=(RectTransform)GameObject.Find("OpponentPlate").transform;
        Require(playerPlate.anchorMin==Vector2.zero && opponentPlate.anchorMin==Vector2.one,"Player plates use the wrong corners.");
        Require(playerPlate.rect.width>playerPlate.rect.height*2,"Player plate is not rectangular.");
        Require(playerPlate.Find("Life").GetComponent<Text>().fontSize==Mathf.RoundToInt(42*GamePreferences.TextScale),"Preference refresh restored the old life font size.");
        state.turnSeat=2;ui.Present(state,1,true,_=>submitted++,null,"对方回合按钮验证");
        Require(phaseButton.GetComponentInChildren<Text>().text=="让过","Opponent-turn priority should only pass.");
        Require(!GameObject.Find("Canvas").transform.Find("ForgeChoices").gameObject.activeSelf,"Priority actions still duplicated in sidebar.");
        state.turnSeat=1;ui.Present(state,1,true,_=>submitted++,null,"自己的阶段按钮验证");
        int instance=Find(100).GetInstanceID();ui.Present(state,1,true,_=>submitted++,null,"交互演示 · 测试状态");
        yield return new WaitForSecondsRealtime(.3f);
        Require(Find(100).GetInstanceID()==instance,"Cards were recreated for an unchanged snapshot.");
        Require(Find(200).GetComponentsInChildren<Text>().Any(t=>t.text=="×3"),"Untapped identical lands did not stack.");
        Require(Find(203).GetComponentsInChildren<Text>().Any(t=>t.text=="×2"),"Tapped lands must form a separate stack.");
        Find(200).onClick.Invoke();yield return null;
        Require(submitted==1 && Find(200).GetComponentsInChildren<Text>().Any(t=>t.text=="×3"),"Stacked land must choose one member without expanding.");
        submitted=0;state.request.requestId++;ui.Present(state,1,true,_=>submitted++,null,"叠放恢复");
        yield return new WaitForSecondsRealtime(.4f);
        var tapped=Find(203);var tappedPointer=tapped.GetComponent<ForgeBattlefieldCardPointer>();
        Require(Quaternion.Angle(tapped.transform.localRotation,Quaternion.Euler(0,0,-12))<1,"Tapped tile should rotate only in the screen plane.");
        Require(((RectTransform)Find(300).transform).rect.width<((RectTransform)Find(100).transform).rect.width*.8f,"Creature tile should be smaller than the hand card.");
        Require(Mathf.Abs(Mathf.DeltaAngle(Find(300).transform.eulerAngles.y,0))<1,"Untapped creature is skewed sideways.");
        foreach(int id in new[]{200,203,300})
        {
            var tile=(RectTransform)Find(id).transform;
            Require(tile.rect.height<tile.rect.width,"Battlefield tile should be compact.");
            var art=tile.Find("Visual/Border/Face/Illustration").GetComponent<RawImage>();
            Require(art.texture!=null,"Missing battlefield illustration.");
            float sourceAspect=art.uvRect.width*art.texture.width/(art.uvRect.height*art.texture.height);
            Require(Mathf.Abs(sourceAspect-art.rectTransform.rect.width/art.rectTransform.rect.height)<.001f,"Illustration crop is stretched.");
            Require(tile.Find("Visual/Border/Face/PrintedName")!=null,"Missing printed card name.");
        }
        var ownTile=(RectTransform)Find(300).transform;
        var opponentTile=(RectTransform)Find(400).transform;
        Require(opponentTile.anchoredPosition.y-ownTile.anchoredPosition.y>280,"Creature rows leave no combat space.");
        Require(Mathf.Abs(opponentTile.anchoredPosition.x-ownTile.anchoredPosition.x)<100,"Opponent row was incorrectly moved to the other side.");
        var tapSymbol=Find(203).transform.Find("Visual/TappedSymbol");
        Require(tapSymbol!=null && tapSymbol.GetComponent<CanvasRenderer>()!=null,"Missing rendered tapped symbol.");
        var tapHover=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
        tappedPointer.OnPointerEnter(tapHover);yield return new WaitForSecondsRealtime(.3f);
        Require(Quaternion.Angle(tapped.transform.localRotation,Quaternion.Euler(0,0,-12))<1,"Hover straightened a tapped card.");
        Require(Vector3.Distance(tapped.transform.Find("Visual").localScale,Vector3.one)<.01f,"Battlefield hover must not enlarge the card.");
        tapHover.position=RectTransformUtility.WorldToScreenPoint(null,tapped.transform.Find("Visual").position);
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(tapHover,hits);
        Require(hits.Count>0 && hits[0].gameObject.transform.IsChildOf(tapped.transform),"Tapped tile hover hit surface is occluded.");
        tappedPointer.OnPointerExit(tapHover);yield return new WaitForSecondsRealtime(.3f);
        UnityEngine.ScreenCapture.CaptureScreenshot(folder+"/board.png");
        yield return new WaitForEndOfFrame();
        // Payment must not expand either player's unrelated land groups; undo merges them again.
        var priority=state.request;
        state.request=new ForgeRequest{requestId=710,kind="InputPayMana",seat=1,options=new[]{new ForgeOption{id="card:200",label="Mountain"}}};
        ui.Present(state,1,true,_=>submitted++,null,"支付时叠放");yield return null;
        Require(Find(200).GetComponentsInChildren<Text>().Any(t=>t.text=="×3"),"Payment split own lands.");
        Require(Find(500).GetComponentsInChildren<Text>().Any(t=>t.text=="×2"),"Payment split opponent lands.");
        foreach(var land in lands)land.tapped=false;
        state.request=priority;state.request.requestId=711;
        ui.Present(state,1,true,_=>submitted++,null,"全部撤回后叠放");yield return null;
        Require(Find(200).GetComponentsInChildren<Text>().Any(t=>t.text=="×5"),"Undo did not regroup restored lands.");
        Require(Find(500).GetComponentsInChildren<Text>().Any(t=>t.text=="×2"),"Undo changed opponent grouping.");
        var card=Find(103);var pointer=card.GetComponent<ForgeBattlefieldCardPointer>();
        var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,card.transform.position)};
        pointer.OnPointerEnter(data);yield return new WaitForSecondsRealtime(.3f);
        Require(card.transform.Find("Visual").localPosition.y>100,"Hand hover did not lift card.");
        UnityEngine.ScreenCapture.CaptureScreenshot(folder+"/hover.png");
        yield return new WaitForEndOfFrame();
        pointer.OnPointerExit(data);yield return new WaitForSecondsRealtime(.3f);
        // Rapid crossing plus a snapshot update used to restore stale sibling indices.
        var adjacent=Find(104).GetComponent<ForgeBattlefieldCardPointer>();
        pointer.OnPointerEnter(data);pointer.OnPointerEnter(data);adjacent.OnPointerEnter(data);
        pointer.OnPointerExit(data);ui.Present(state,1,true,_=>submitted++,null,"手牌顺序回归");adjacent.OnPointerExit(data);
        for(int i=0;i<6;i++)Require(Find(100+i).transform.GetSiblingIndex()<Find(101+i).transform.GetSiblingIndex(),"Hand overlap order drifted after crossing cards.");
        yield return new WaitForSecondsRealtime(.4f);
        Require(card.transform.Find("Visual").localPosition.y<1,"Exited hand card did not settle.");
        pointer.OnBeginDrag(data);data.position=new Vector2(Screen.width*.5f,20);pointer.OnDrag(data);pointer.OnEndDrag(data);
        Require(submitted==0,"Releasing below the battlefield submitted a card.");
        yield return new WaitForSecondsRealtime(.3f);
        pointer.OnBeginDrag(data);data.position=new Vector2(Screen.width*.5f,Screen.height*.5f);pointer.OnDrag(data);pointer.OnEndDrag(data);
        Require(submitted==1,"Valid drag must submit exactly once.");card.onClick.Invoke();Require(submitted==1,"Drag followed by click submitted twice.");
        state.request=new ForgeRequest{requestId=701,kind="dialog",seat=1,sourceCardId=200,options=new[]{
            new ForgeOption{id="choice:0",label="Add {R}."},new ForgeOption{id="choice:1",label="Add {U}."}}};
        ui.Present(state,1,true,_=>submitted++,null,"地牌能力菜单展示测试");
        yield return new WaitForSecondsRealtime(.4f);
        var menu=GameObject.Find("CardAbilityChoices");Require(menu!=null,"Land choices menu was not created.");
        Require(!GameObject.Find("Canvas").transform.Find("ForgeChoices").gameObject.activeSelf,"Land abilities still appear in the sidebar.");
        var menuCorners=new Vector3[4];((RectTransform)menu.transform).GetWorldCorners(menuCorners);
        Require(menuCorners[0].y>Find(200).transform.position.y,"Land menu is not above its source card.");
        var option=menu.GetComponentsInChildren<Button>().First(b=>b.name=="Option_1");
        var choicePointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,option.transform.position),button=PointerEventData.InputButton.Left};
        var choiceHits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(choicePointer,choiceHits);
        Require(choiceHits.Count>0 && choiceHits[0].gameObject.transform.IsChildOf(option.transform),"Anchored land choice is occluded.");
        ScreenCapture.CaptureScreenshot(folder+"/land-choices.png");yield return new WaitForEndOfFrame();
        ExecuteEvents.ExecuteHierarchy(choiceHits[0].gameObject,choicePointer,ExecuteEvents.pointerClickHandler);
        Require(submitted==2,"Land choice did not submit once.");
        state.request=new ForgeRequest{requestId=702,kind="dialog",seat=1,sourceCardId=300,options=new[]{new ForgeOption{id="choice:0",label="First ability"},new ForgeOption{id="choice:1",label="Second ability"}}};
        ui.Present(state,1,true,_=>submitted++,null,"生物能力菜单验证");yield return new WaitForSecondsRealtime(.4f);
        var creatureMenu=(RectTransform)GameObject.Find("CardAbilityChoices").transform;
        Require(creatureMenu.position.y>Find(300).transform.position.y && Mathf.Abs(creatureMenu.position.x-Find(300).transform.position.x)<2,"Creature choices are not anchored above their source.");
        Require(!GameObject.Find("Canvas").transform.Find("ForgeChoices").gameObject.activeSelf,"Creature choices remain in sidebar.");
        ScreenCapture.CaptureScreenshot(folder+"/creature-choices.png");yield return new WaitForEndOfFrame();
        float sparseWidth=((RectTransform)Find(300).transform).rect.width;
        state.players[0].battlefield=Enumerable.Range(0,20).Select(i=>{var c=Card("Goblin Piker",3000+i);c.power=i+1;return c;}).ToArray();
        ui.Present(state,1,false,null,null,"密集战场展示测试");
        yield return new WaitForSecondsRealtime(.4f);
        Require(((RectTransform)Find(3000).transform).rect.width<sparseWidth,"Dense battlefield did not shrink cards.");
        for(int i=0;i<20;i++)Require(Find(3000+i)!=null,"Dense battlefield hid a card behind pagination.");
        UnityEngine.ScreenCapture.CaptureScreenshot(folder+"/dense.png");
        yield return new WaitForEndOfFrame();
        const string result="PASS: rectangular plates in own bottom-left/opponent top-right; persistent life font size; own end-phase/opponent pass in one button; no end-turn shortcut/sidebar priority actions; land and creature ability menus above source; menu raycast and submission; persistent card identity; land grouping/undo; unstretched artwork; hover/drag/duplicate prevention; 20-card layout.";
        File.WriteAllText(folder+"/result.txt",result);Debug.Log(result);
    }
}
public static class AttackDefenderPresentationValidation
{
    const string Folder="Tools/Validation/Reports/AttackDefenders";
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first.");
        foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(notice);
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("AttackValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Validate(ui));
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static ForgeSeatView Load(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Folder+"/"+name+".json"));
    static GameObject Card(int id)=>GameObject.Find("Card_"+id);
    static Vector2 Screen(Transform t)=>RectTransformUtility.WorldToScreenPoint(null,t.position);
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        File.WriteAllText(Folder+"/ui-result.txt","RUNNING");
        var initial=Load("initial");int calls=0;string last=null;
        Action<ForgeSeatView> present=state=>ui.Present(state,1,true,i=>{calls++;last=state.request.options[i].id;},null,"攻击对象交互验证（真实 Forge 快照）");
        present(initial);yield return new WaitForSecondsRealtime(.6f);
        var attackers=initial.players[0].battlefield.Where(c=>c.name=="Goblin Piker").ToArray();
        var walker=initial.players[1].battlefield.First(c=>c.planeswalker);
        var battle=initial.players[0].battlefield.First(c=>c.battle);
        var plate=GameObject.Find("OpponentPlate");
        var destinations=new[]{plate,Card(walker.id),Card(battle.id)};
        for(int i=0;i<3;i++)
        {
            present(initial);yield return new WaitForSecondsRealtime(.3f);
            var source=Card(attackers[i].id);var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=Screen(source.transform)};
            var before=source.transform.position;
            ExecuteEvents.Execute(source,pointer,ExecuteEvents.beginDragHandler);
            yield return null;Canvas.ForceUpdateCanvases();
            Require(calls==i,"Starting an arrow should not declare an attacker.");
            pointer.position=Screen(destinations[i].transform);
            ExecuteEvents.Execute(source,pointer,ExecuteEvents.dragHandler);ui.UpdateAttackArrowAt(pointer.position);
            yield return new WaitForEndOfFrame();
            var arrow=GameObject.Find("AttackTargetArrow").GetComponent<ForgeTargetArrow>();
            Require(arrow.canvasRenderer.GetMesh().vertexCount>100,"Attack arrow has no rendered mesh.");
            Require(Vector3.Distance(before,source.transform.position)<1,"Dragging moved the attacking card off the battlefield.");
            Require(!GameObject.Find("Canvas").transform.Find("ForgeChoices").gameObject.activeSelf,"Attack options leaked into sidebar.");
            ScreenCapture.CaptureScreenshot(Folder+"/drag-"+i+".png");yield return new WaitForEndOfFrame();
            ExecuteEvents.Execute(source,pointer,ExecuteEvents.endDragHandler);
            string expected="attack:"+attackers[i].id+":"+(i==0?"player:"+initial.players[1].targetId:"card:"+(i==1?walker.id:battle.id));
            Require(calls==i+1 && last==expected,"Drop selected the wrong attack pair: "+last+", expected "+expected);
            Require(!ui.TryChooseAttackAt(pointer.position,initial.request.requestId) && calls==i+1,"Duplicate drop submitted twice.");
        }
        present(initial);yield return null;
        Require(ui.BeginAttackSelection(attackers[0].id,initial.request.requestId),"Select attacker failed.");yield return null;
        Require(!ui.TryChooseAttackAt(Screen(destinations[1].transform),initial.request.requestId-1) && calls==3,"Stale drag submitted.");
        Require(!ui.TryChooseAttackAt(new Vector2(3,3),initial.request.requestId) && calls==3,"Empty drop changed combat.");
        var wall=initial.players[0].battlefield.First(c=>c.name=="Wall of Wood");
        Require(!ui.BeginAttackSelection(wall.id,initial.request.requestId),"Defender creature can attack.");
        ui.BeginAttackSelection(attackers[0].id,initial.request.requestId);yield return null;
        var friendly=initial.players[0].battlefield.First(c=>c.planeswalker);
        Require(!ui.TryChooseAttackAt(Screen(Card(friendly.id).transform),initial.request.requestId) && calls==3,"Own planeswalker accepted.");
        ui.BeginAttackSelection(attackers[0].id,initial.request.requestId);yield return null;
        // Alternative click-to-select follows the exact same legal pair.
        Card(walker.id).GetComponent<Button>().onClick.Invoke();
        Require(calls==4 && last=="attack:"+attackers[0].id+":card:"+walker.id,"Click target does not match drag target.");
        var assigned=Load("all-assigned");present(assigned);yield return new WaitForSecondsRealtime(.3f);
        var arrows=UnityEngine.Object.FindObjectsByType<ForgeTargetArrow>(FindObjectsSortMode.None).Where(a=>a.name.StartsWith("AssignedAttack_")).ToArray();
        Require(arrows.Length==3 && arrows.All(a=>a.canvasRenderer.GetMesh().vertexCount>100),"Missing individual assigned attack lines.");
        ScreenCapture.CaptureScreenshot(Folder+"/assignments.png");yield return new WaitForEndOfFrame();
        ui.BeginAttackSelection(attackers[1].id,assigned.request.requestId);yield return null;
        GameObject.Find("RecallAttacker").GetComponent<Button>().onClick.Invoke();
        Require(calls==5 && last=="attack-remove:"+attackers[1].id,"Recall submitted another creature.");
        present(initial);yield return null;ui.BeginAttackSelection(attackers[0].id,initial.request.requestId);yield return null;
        present(assigned);yield return null;
        Require(!ui.TryChooseAttackAt(Screen(plate.transform),initial.request.requestId) && calls==5,"New snapshot reused the old drag.");
        ui.Present(assigned,2,false,null,null,"对手正在分配攻击");yield return null;
        Require(!ui.BeginAttackSelection(attackers[0].id,assigned.request.requestId),"Spectating seat can declare attacks.");
        present(assigned);yield return new WaitForSecondsRealtime(.3f);
        const string result="PASS: actual Forge snapshot replay; per-creature drag to player/planeswalker/own battle; arrow mesh and unchanged card position; exact legal pair callback; click alternative; recall; three distinct assignment lines; illegal/stale/empty/duplicate/opponent input rejected; no attack sidebar.";
        File.WriteAllText(Folder+"/ui-result.txt",result);Debug.Log(result);
    }
}
public static class PreferenceTextLifecycleValidation
{
    public static void Run()
    {
        const string folder="Tools/Validation/Reports/PreferenceTextLifecycle";
        Directory.CreateDirectory(folder);
        var parent=new GameObject("InactivePreferenceValidation");parent.SetActive(false);
        try
        {
            foreach(bool useTmp in new[]{false,true})
            {
                var child=new GameObject(useTmp?"TMP":"UGUI",typeof(RectTransform));child.transform.SetParent(parent.transform);
                var label=useTmp?null:child.AddComponent<Text>();
                var tmp=useTmp?child.AddComponent<TMPro.TextMeshProUGUI>():null;
                if(label!=null){label.text="保留";label.fontSize=16;}else{tmp.text="保留";tmp.fontSize=16;}
                var binding=child.AddComponent<PreferenceText>();
                binding.SetBaseSize(20);binding.SetBaseSize(20);
                Require((label!=null?label.text:tmp.text)==GamePreferences.T("保留"),"Inactive label lost its source.");
                Require(Mathf.Approximately(label!=null?label.fontSize:tmp.fontSize,label!=null?Mathf.RoundToInt(20*GamePreferences.TextScale):20*GamePreferences.TextScale),"Inactive font size is wrong.");
                if(label!=null)label.text="再调度";else tmp.text="再调度";
                parent.SetActive(true);binding.SetBaseSize(20);
                Require((label!=null?label.text:tmp.text)==GamePreferences.T("再调度"),"Awake reset text changed while inactive.");
                parent.SetActive(false);binding.SetBaseSize(22);binding.SetBaseSize(20);
                Require(Mathf.Approximately(label!=null?label.fontSize:tmp.fontSize,label!=null?Mathf.RoundToInt(20*GamePreferences.TextScale):20*GamePreferences.TextScale),"Repeated updates accumulated text scaling.");
            }
            File.WriteAllText(folder+"/result.txt","PASS: inactive parent, uGUI/TMP SetBaseSize before Awake, source preserved on activation, repeat sizing without accumulated scale.");
        }
        catch(Exception error){File.WriteAllText(folder+"/result.txt","FAIL: "+error);throw;}
        finally{UnityEngine.Object.DestroyImmediate(parent);}
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
public static class BattlefieldMenuValidation
{
    public static void Run()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        ui.StartCoroutine(Validate(ui));
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static Button FindButton(Transform root,string name)=>root.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        const string folder="Tools/Validation/Reports/BattlefieldMenu";
        Directory.CreateDirectory(folder);File.WriteAllText(folder+"/result.txt","RUNNING");
        int submissions=0,concessions=0;
        var mountain=new ForgeCardView{id=600,name="Mountain",printingKey="10E/376",land=true,tapped=true};
        var state=new ForgeSeatView{seat=1,turn=2,turnSeat=1,phase="MAIN1",awaitingInput=true,
            request=new ForgeRequest{requestId=800,seat=1,kind="InputPassPriority",options=new[]{new ForgeOption{id="ok",label="OK"},new ForgeOption{id="cancel",label="Undo"}}},
            players=new[]{new ForgePlayerView{seat=1,life=20,handCount=1,hand=new[]{new ForgeCardView{id=601,name="Mountain",printingKey="10E/376",land=true}},battlefield=new[]{mountain},graveyard=Array.Empty<string>()},
                new ForgePlayerView{seat=2,life=20,hand=Array.Empty<ForgeCardView>(),battlefield=Array.Empty<ForgeCardView>(),graveyard=Array.Empty<string>()}}};
        Action present=()=>ui.Present(state,1,true,_=>submissions++,()=>concessions++,"菜单与撤回验证");present();
        yield return new WaitForSecondsRealtime(.5f);
        var canvas=GameObject.Find("Canvas").transform;canvas.Find("InspectPanel").gameObject.SetActive(false);
        var gear=canvas.Find("GearBox/SettingsButton").GetComponent<Button>();
        Require(gear.gameObject.activeInHierarchy && gear.GetComponentInChildren<ForgeGearIcon>().canvasRenderer.GetMesh().vertexCount>0,"Gear icon is not rendered.");
        Require(!canvas.Find("GearBox/ConcedeButton").gameObject.activeSelf,"Standalone concede button remains.");
        ScreenCapture.CaptureScreenshot(folder+"/cards.png");yield return new WaitForEndOfFrame();
        gear.onClick.Invoke();yield return null;
        Require(GameSettingsPanel.IsOpen,"Gear did not open shared settings.");
        var settings=GameObject.Find("GameSettings").transform;
        present();yield return null;yield return new WaitForEndOfFrame();
        Require(settings.GetSiblingIndex()==canvas.childCount-1,"Match refresh raised the hand above settings.");
        foreach(var name in new[]{"简体中文","日本語","English","小","标准","大","认输"})Require(FindButton(settings,name).gameObject.activeInHierarchy,"Missing settings action: "+name);
        Require(!ui.TryUndoPresentedAction(),"Undo should be blocked while settings are open.");
        ScreenCapture.CaptureScreenshot(folder+"/settings.png");yield return new WaitForEndOfFrame();
        FindButton(settings,"认输").onClick.Invoke();Require(concessions==0,"Opening confirmation conceded.");
        FindButton(settings,"继续对局").onClick.Invoke();Require(concessions==0,"Cancel conceded.");
        FindButton(settings,"认输").onClick.Invoke();var confirm=FindButton(settings,"确认认输");confirm.onClick.Invoke();confirm.onClick.Invoke();
        Require(concessions==1 && !GameSettingsPanel.IsOpen,"Concede should execute once, only after confirmation.");
        Require(ui.TryUndoPresentedAction() && submissions==1,"Available Forge undo did not submit.");
        Require(!ui.TryUndoPresentedAction() && submissions==1,"Undo submitted twice.");
        state.request.requestId++;state.request.kind="InputPayMana";state.request.options[1].label="Cancel";present();
        Require(!ui.TryUndoPresentedAction(),"Undo shortcut cancelled payment.");
        state.request.requestId++;state.request.kind="InputPassPriority";state.request.options[1].label="End Turn";present();
        Require(!ui.TryUndoPresentedAction(),"Undo shortcut invoked End Turn.");
        state.request.requestId++;state.request.options[1].label="Undo";present();
#if ENABLE_INPUT_SYSTEM
        var gameView=Type.GetType("UnityEditor.GameView,UnityEditor");UnityEditor.EditorWindow.GetWindow(gameView).Focus();
        var keyboard=InputSystem.AddDevice<Keyboard>();
        try
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftCtrl,Key.Z));yield return null;yield return null;
            Require(submissions==1,"Ctrl+Z unexpectedly submitted game undo.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Z));yield return null;yield return null;
            Require(submissions==2,"Pressing Z did not execute undo through Update.");
            yield return null;Require(submissions==2,"Holding Z repeated the same undo.");
        }
        finally{InputSystem.RemoveDevice(keyboard);}
#endif
        GameSettingsPanel.Show(canvas);Require(!FindButton(settings,"认输").gameObject.activeSelf,"Battlefield concede leaked into non-match settings.");
        FindButton(settings,"完成").onClick.Invoke();
        const string result="PASS: rendered gear; shared language/text-size settings; modal blocks undo; concede confirmation/cancel/duplicate guard; exact Forge Undo only; no payment-cancel/end-turn; Z keyboard path, Ctrl+Z ignored, held-key duplicate blocked; non-match settings omit concede.";
        File.WriteAllText(folder+"/result.txt",result);Debug.Log(result);
    }
}


public static class CastingMotionValidation
{
    private static bool running;
    public static void Run()
    {
        if(running)return;
        if(!Application.isPlaying)throw new Exception("Enter IntegratedBattlefield Play Mode first.");
        running=true;
        foreach(var notice in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(notice);
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>() ?? new GameObject("MotionValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(GuardedCheck(ui));
    }
    private static IEnumerator GuardedCheck(ForgeBattlefieldController ui)
    {
        var check=Check(ui);
        while(true)
        {
            object next;
            try {if(!check.MoveNext())break;next=check.Current;}
            catch(Exception e)
            {
                File.WriteAllText("Tools/Validation/Reports/CastingMotion/result.txt","FAIL: "+e);
                Debug.LogException(e);break;
            }
            yield return next;
        }
        running=false;
    }
    private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    private static IEnumerator Check(ForgeBattlefieldController ui)
    {
        const string folder="Tools/Validation/Reports/CastingMotion";
        Directory.CreateDirectory(folder);File.WriteAllText(folder+"/result.txt","RUNNING");
        var definition=CardDatabaseRepository.LoadAll().First(c=>c.cardName=="Shock" && !c.forgeAmbiguous);
        var spell=new ForgeCardView{id=941,name="Shock",printingKey=definition.PrintingKey};
        var own=new ForgePlayerView{seat=1,targetId=1001,life=20,handCount=1,hand=new[]{spell},battlefield=Array.Empty<ForgeCardView>(),graveyard=Array.Empty<string>()};
        var enemy=new ForgePlayerView{seat=2,targetId=1002,life=20,handCount=4,hand=Array.Empty<ForgeCardView>(),battlefield=Array.Empty<ForgeCardView>(),graveyard=Array.Empty<string>()};
        var state=new ForgeSeatView{seat=1,turn=2,turnSeat=1,phase="MAIN1",awaitingInput=true,players=new[]{own,enemy},stack=Array.Empty<string>(),stackItems=Array.Empty<ForgeStackItem>()};
        Func<long,ForgeRequest> priority=id=>new ForgeRequest{requestId=id,seat=1,kind="InputPassPriority",options=new[]{new ForgeOption{id="card:941",label="Shock [Hand]"},new ForgeOption{id="ok",label="Pass priority"}}};
        int submissions=0;Action present=()=>ui.Present(state,1,true,_=>submissions++,null,"施放动效验证（展示测试）");
        state.request=priority(9001);present();yield return new WaitForSecondsRealtime(.5f);
        var hand=GameObject.Find("Card_941").GetComponent<RectTransform>();
        var face=(RectTransform)hand.Find("Visual");var canvas=(RectTransform)GameObject.Find("Canvas").transform;
        ForgeUiMotion.Pose(face,canvas,out var start,out var startSize,out var startAngle);
        ui.Choose(0,9001);
        var preview=GameObject.Find("TargetOrigin").GetComponent<RectTransform>();
        Require(Vector2.Distance(preview.anchoredPosition,start)<1,"Cast teleported before animation started.");
        Require(hand.GetComponent<CanvasGroup>().alpha==0,"Casting card is duplicated in hand.");
        Require(!ui.TrySubmitPresentedOption(0,9001),"Duplicate cast submitted.");
        float oldTimeScale=Time.timeScale;Time.timeScale=0;
        yield return new WaitForSecondsRealtime(.12f);Time.timeScale=oldTimeScale;
        Require(Vector2.Distance(preview.anchoredPosition,start)>10 && ForgeUiMotion.On(preview).IsMoving,"Cast did not animate with unscaled time.");
        ScreenCapture.CaptureScreenshot(folder+"/01-in-flight.png");yield return new WaitForEndOfFrame();
        own.hand=Array.Empty<ForgeCardView>();own.handCount=0;
        state.request=new ForgeRequest{requestId=9002,seat=1,kind="InputSelectTargets",options=new[]{new ForgeOption{id="player:1002",label="Opponent"},new ForgeOption{id="cancel",label="Cancel"}}};
        present();yield return new WaitForSecondsRealtime(.5f);
        Vector2 dock=preview.anchoredPosition;
        for(int i=0;i<4;i++){present();yield return null;Require(Vector2.Distance(preview.anchoredPosition,dock)<1,"Same snapshot restarted cast motion.");}
        var target=GameObject.Find("OpponentPlate").GetComponent<RectTransform>();
        var screen=RectTransformUtility.WorldToScreenPoint(null,target.position);
        ui.UpdateTargetingAt(screen);Canvas.ForceUpdateCanvases();
        Require(!ui.TryChooseTargetAt(new Vector2(1,1),9002),"Invalid target submitted.");
        Require(!ui.TryChooseTargetAt(screen,9001),"Stale target submitted.");
        ScreenCapture.CaptureScreenshot(folder+"/02-target.png");yield return new WaitForEndOfFrame();
        ui.Choose(1,9002);
        Require(preview.gameObject.activeSelf,"Cancel hides the card before Forge acknowledges.");
        own.hand=new[]{spell};own.handCount=1;state.request=priority(9003);present();
        Require(preview.gameObject.activeSelf && ForgeUiMotion.On(preview).IsMoving,"Cancel did not animate back to the hand.");
        yield return new WaitForSecondsRealtime(.15f);
        ScreenCapture.CaptureScreenshot(folder+"/03-returning.png");yield return new WaitForEndOfFrame();
        yield return new WaitForSecondsRealtime(.4f);
        Require(!preview.gameObject.activeSelf,"Return overlay leaked.");
        face=(RectTransform)GameObject.Find("Card_941").transform.Find("Visual");
        Require(face.GetComponent<CanvasGroup>().alpha==1,"Returned hand card remains invisible.");
        ui.Choose(0,9003);yield return new WaitForSecondsRealtime(.08f);
        own.hand=Array.Empty<ForgeCardView>();own.handCount=0;
        state.stackCount=1;state.stackItems=new[]{new ForgeStackItem{id=901,controllerSeat=1,source=spell,description="Shock"}};state.stack=new[]{"Shock"};
        state.request=new ForgeRequest{requestId=9004,seat=1,kind="InputPassPriority",options=new[]{new ForgeOption{id="ok",label="Pass priority"}}};present();
        Require(GameObject.Find("StackCard_0").transform.parent.GetComponent<CanvasGroup>().alpha==0,"An empty rim flashes while the card is still in flight.");
        yield return new WaitForSecondsRealtime(.45f);
        var stack=GameObject.Find("StackCard_0");int identity=stack.GetInstanceID();
        Require(stack.GetComponent<Image>().sprite!=null && stack.GetComponentInParent<CanvasGroup>().alpha==1,"Stack landing lost art/visibility.");
        Require(!preview.gameObject.activeSelf,"Stack landing left a duplicate card.");
        present();yield return null;Require(GameObject.Find("StackCard_0").GetInstanceID()==identity,"Stack rebuilt on repeated snapshot.");
        var scroll=GameObject.Find("Canvas").transform.Find("StackColumn/Viewport").GetComponent<ScrollRect>();
        ScreenCapture.CaptureScreenshot(folder+"/04-stack.png");yield return new WaitForEndOfFrame();
        var second=new ForgeStackItem{id=902,controllerSeat=2,source=new ForgeCardView{id=942,name="Shock",printingKey=spell.printingKey},description="Shock response"};
        state.stackCount=2;state.stackItems=new[]{second,state.stackItems[0]};state.stack=new[]{"Shock response","Shock"};present();
        yield return new WaitForSecondsRealtime(.5f);
        Require(GameObject.Find("StackCard_1").GetInstanceID()==identity,"Adding a response recreated the original stack card.");
        scroll.verticalNormalizedPosition=0;yield return null;float offset=scroll.content.anchoredPosition.y;
        present();yield return null;Require(Mathf.Abs(scroll.content.anchoredPosition.y-offset)<1,"Refresh reset stack scrolling.");
        state.stackCount=0;state.stackItems=Array.Empty<ForgeStackItem>();state.stack=Array.Empty<string>();present();yield return new WaitForSecondsRealtime(.5f);
        Require(GameObject.Find("StackArrival")==null && !preview.gameObject.activeSelf,"Transient card animation leaked after stack cleared.");
        Require(submissions==3,"Presentation generated an extra rules action.");
        File.WriteAllText(folder+"/result.txt","PASS: real-origin continuous cast; unscaled movement; duplicate submission blocked; stable targeting snapshots; invalid/stale targets blocked; acknowledged cancel returns to hand; fast direct stack landing; stable stack identity and scroll; transient cleanup; exactly three explicit submissions. UI snapshot test, not a rules simulation.");
    }
}
#endif

