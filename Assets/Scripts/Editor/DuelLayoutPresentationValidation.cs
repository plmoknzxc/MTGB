#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Archived native decisions plus explicit display-only density fixtures.
// This suite adds no new Forge card-effect coverage.
public static class DuelLayoutPresentationValidation
{
    const string Output=".tools/battlefield-layout-1005/ui";
    static bool running;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static ForgeSeatView Read(string path)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/"+path+".json"));
    static ForgeCardView Copy(ForgeCardView card,int id,int owner){var copy=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(card));copy.id=id;copy.ownerSeat=owner;return copy;}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.6f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static Rect Bounds(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c.Min(v=>v.x),c.Min(v=>v.y),c.Max(v=>v.x),c.Max(v=>v.y));}
    static void Click(Button button)
    {
        Check(button!=null&&button.interactable,"Button disabled: "+button?.name);
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Pointer blocked: "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static ForgeCardView Card(string name,int id,int owner,bool land=false,bool creature=false,bool walker=false,bool battle=false)
    {
        var definition=CardDatabaseRepository.LoadAll().First(c=>c.cardName==name&&!c.forgeAmbiguous);
        return new ForgeCardView{id=id,ownerSeat=owner,name=name,printingKey=definition.PrintingKey,zone="Battlefield",land=land,creature=creature,planeswalker=walker,battle=battle,power=2,toughness=2,loyalty=4,defense=4};
    }
    public static ForgeSeatView DensityFixture(bool dense)
    {
        var view=Read("ModeChoices/choose-one-stack");
        var ring=Read("CommonMechanics/ring-tempt-5").players[0].effectMarkers.First();
        var blessing=Read("CommonMechanics/ascend-persistent").players[0].effectMarkers.First();
        var dungeon=Read("CommonMechanics/dungeon-dialog-4").players.SelectMany(p=>p.effectMarkers).First(c=>c.markerKind=="dungeon");
        var emblem=Read("EmblemFaceDown/emblem").players.SelectMany(p=>p.emblems).First();
        foreach(var player in view.players) {
            int next=30000+player.seat*1000;
            var field=new List<ForgeCardView>();
            foreach(string name in new[]{"Grizzly Bears","Raging Goblin","Goblin Piker"})field.Add(Card(name,next++,player.seat,creature:true));
            foreach(string name in (dense?new[]{"Sol Ring","Arcane Signet","Mind Stone","Lightning Greaves","Skullclamp"}:new[]{"Sol Ring"}))field.Add(Card(name,next++,player.seat));
            foreach(string name in (dense?new[]{"Rhystic Study","Smothering Tithe","Doubling Season","Ghostly Prison","Sylvan Library"}:new[]{"Ghostly Prison"}))field.Add(Card(name,next++,player.seat));
            foreach(string name in (dense?new[]{"Jace, the Mind Sculptor","Chandra, Torch of Defiance","Liliana of the Veil","Teferi, Hero of Dominaria","Garruk Wildspeaker"}:new[]{"Jace, the Mind Sculptor"}))field.Add(Card(name,next++,player.seat,walker:true));
            field.Add(Card("Invasion of Zendikar",next++,player.seat,battle:true));
            foreach(string name in new[]{"Mountain","Island","Forest"})field.Add(Card(name,next++,player.seat,land:true));
            player.battlefield=field.ToArray();player.manaPool=player.seat==view.seat?new[]{0,2,0,1,0,1}:new[]{1,0,0,0,2,0};
            player.counters=new[]{new ForgeCardCounter{kind="ENERGY",count=4,label="能量"},new ForgeCardCounter{kind="POISON",count=2,label="中毒"}};
            player.emblems=Enumerable.Range(0,dense?4:1).Select(_=>Copy(emblem,next++,player.seat)).ToArray();
            var markers=new List<ForgeCardView>{Copy(ring,next++,player.seat),Copy(blessing,next++,player.seat),Copy(dungeon,next++,player.seat)};
            foreach(int i in Enumerable.Range(0,dense?3:1)) {
                var delayed=Card("Boros Charm",next++,player.seat);delayed.effectMarker=true;delayed.markerKind="delayed";delayed.creature=false;delayed.zone="Command";markers.Add(delayed);
            }
            player.effectMarkers=markers.ToArray();
        }
        view.globalEffects=new[]{new ForgeCardView{id=-2000001,name="Day",helperArtName="Day",effectMarker=true,markerKind="game",zone="Command"}};
        if(dense){var top=view.stackItems[0];view.stackItems=Enumerable.Range(0,7).Select(i=>{var item=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(top));item.id=80000+i;item.controllerSeat=i%2+1;return item;}).ToArray();view.stackCount=7;view.stack=view.stackItems.Select(c=>c.description).ToArray();}
        return view;
    }
    public static void Preview(bool dense)
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("DuelLayoutPreview").AddComponent<ForgeBattlefieldController>();
        var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();if(banner!=null)banner.enabled=false;
        var v=DensityFixture(dense);ui.Present(v,v.seat,true,_=>{},null,"");
    }
    public static void Run()
    {
        Check(Application.isPlaying,"Play Mode required");if(running)return;running=true;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("DuelLayoutValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        string original=GamePreferences.Language;var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();if(banner!=null)banner.enabled=false;
        var iterator=Validate(ui);
        try {
            while(true){object next;try{if(!iterator.MoveNext())break;next=iterator.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: native player selection exact-index raycasts in both seats; zh/en/ja fixed phase/AUTO fonts and view/return persistence; density adapters cover 3 creatures, 5 distinct artifacts, 5 enchantments, 5 planeswalkers, 1 battle and 3 lands per player, both pools, state/emblem independent paging and object retention. UI-only evidence, not new effect or network validation.");
        } finally {running=false;GamePreferences.SetLanguage(original);ui.RestoreBattlefieldWindows();}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            GamePreferences.SetLanguage(language);float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;
            Check(GamePreferences.Ready,"Locale not ready");
            foreach(string sample in new[]{"own","opponent","seat-two"}) {
                var v=Read("LibraryCosts/"+sample+"-players-1");int chosen=-1;ui.Present(v,v.seat,true,i=>chosen=i,null,"");yield return Frame();
                int selectedSeat=sample=="opponent"?2:v.seat;var player=v.players.Single(p=>p.seat==selectedSeat);
                Click(GameObject.Find(selectedSeat==v.seat?"PlayerPlate":"OpponentPlate").GetComponent<Button>());
                Check(chosen==Array.FindIndex(v.request.options,o=>o.id=="player:"+player.targetId),"Native player identity changed");
            }
            var dense=DensityFixture(true);ui.Present(dense,dense.seat,true,_=>{},null,"");yield return Frame();
            var canvas=GameObject.Find("Canvas").transform;
            foreach(string playerPlate in new[]{"PlayerPlate","OpponentPlate"}) {
                var avatar=canvas.Find(playerPlate+"/Avatar/Portrait").GetComponent<PlayerAvatarGraphic>();
                Check(avatar.GetComponent<CanvasRenderer>()!=null&&avatar.canvasRenderer.GetMesh()?.vertexCount>0,"Portrait silhouette not rendered");
            }
            Check(Bounds((RectTransform)canvas.Find("PlayerPlate")).xMax<Screen.width*.25f&&Bounds((RectTransform)canvas.Find("OpponentPlate")).xMax<Screen.width*.25f,"Players not in left lane");
            foreach(var player in dense.players)foreach(var card in player.battlefield) {
                var tile=GameObject.Find("Card_"+card.id);Check(tile!=null,"Dense object missing "+card.name);
                var bounds=Bounds((RectTransform)tile.transform);Check(bounds.xMin>=Screen.width*.2f&&bounds.xMax<=Screen.width*.84f,"Dense object outside board "+card.name);
                Check(bounds.yMin>=Screen.height*.18f&&bounds.yMax<=Screen.height*.93f,"Dense row overlaps hand or leaves screen "+card.name);
            }
            var plate=canvas.Find("PlayerPlate");
            var first=dense.players.First(p=>p.seat==dense.seat);var stateIds=first.effectMarkers.Where(c=>!string.IsNullOrEmpty(c.helperArtName)).Select(c=>c.id).Concat(dense.globalEffects.Select(c=>c.id)).ToArray();
            Check(stateIds.Count(id=>GameObject.Find("Card_"+id)!=null)==3,"State row exceeds capacity");
            var keep=GameObject.Find("Card_"+first.emblems[0].id);int instance=keep.GetInstanceID();ui.Present(dense,dense.seat,true,_=>{},null,"");yield return Frame();Check(GameObject.Find(keep.name).GetInstanceID()==instance,"Unchanged marker recreated");
            Click(plate.Find("StatesNext").GetComponent<Button>());yield return Frame();Check(GameObject.Find("Card_-2000001")!=null&&stateIds.Count(id=>GameObject.Find("Card_"+id)!=null)==1,"State arrow wrong page");
            Click(plate.Find("EmblemsNext").GetComponent<Button>());yield return Frame();Check(GameObject.Find("Card_"+first.emblems[3].id)!=null,"Emblem paging lost identity");
            Check(canvas.Find("PlayerPlateWithZones").GetSiblingIndex()<canvas.Find("ZonePiles_PlayerBottomLeft").GetSiblingIndex(),"Player shell covers zone icons after refresh");
            Check(canvas.Find("OpponentPlateWithZones").GetSiblingIndex()<canvas.Find("ZonePiles_OpponentTopRight").GetSiblingIndex(),"Opponent shell covers zone icons after refresh");
            ScreenCapture.CaptureScreenshot(Output+"/"+language+"-dense.png");yield return new WaitForEndOfFrame();
            var choice=Read("ModeChoices/choose-one-choices");ui.Present(choice,choice.seat,true,_=>{},null,"");yield return Frame();
            var labels=new[]{"CornerCluster/ControlRow/PhaseText","CornerCluster/ControlRow/TurnChip/TurnLabel","PriorityControlMode/Label"}.Select(p=>canvas.Find(p)?.GetComponent<Text>()).Where(t=>t!=null).ToArray();
            var sizes=labels.Select(t=>t.fontSize).ToArray();Check(labels.All(t=>!t.resizeTextForBestFit),"HUD auto shrinks");
            Check(labels.All(t=>t.preferredHeight<=t.rectTransform.rect.height+.5f),"HUD text exceeds height at current font scale");
            ui.ViewBattlefield();yield return Frame();ui.RestoreBattlefieldWindows();yield return Frame();
            for(int i=0;i<labels.Length;i++)Check(labels[i].fontSize==sizes[i],"View/return changes HUD font");
            ui.Present(dense,dense.seat,false,null,null,"连接中断 · 正在恢复对局");yield return Frame();
            var status=canvas.Find("PriorityPill/Text").GetComponent<Text>();
            Check(status.text==(language=="zh-CN"?"正在重新连接…":language=="en-US"?"Reconnecting…":"再接続中…"),"Hidden phase room line loses reconnect status");
            Check(status.preferredHeight<=status.rectTransform.rect.height+.5f,"Transport status clipped");
            Preview(false);yield return Frame();ScreenCapture.CaptureScreenshot(Output+"/"+language+"-normal.png");yield return new WaitForEndOfFrame();
        }
    }
}
#endif
