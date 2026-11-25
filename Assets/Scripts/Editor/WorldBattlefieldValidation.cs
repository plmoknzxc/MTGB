#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class WorldBattlefieldValidation
{
    const string Output=".tools/world-table-1104";
    static readonly List<string> results=new();
    static ForgeSeatView Sample()=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Assets/Scenes/PreviewData/BattlefieldRedesignSample.json"));
    static BattlefieldWorldCard[] Cards()=>UnityEngine.Object.FindObjectsByType<BattlefieldWorldCard>(FindObjectsSortMode.None);
    static IEnumerator Settle(){yield return new WaitForSecondsRealtime(.8f);yield return new WaitForEndOfFrame();}
    static void Check(bool value,string message){results.Add((value?"PASS ":"FAIL ")+message);if(!value)throw new Exception(message);}
    public static void Run()
    {
        if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="IntegratedBattlefield")throw new Exception("Requires world scene in Play mode");
        results.Clear();Directory.CreateDirectory(Output);
        UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>().StartCoroutine(Guard());
    }
    static IEnumerator Guard()
    {
        var run=Validate();
        try{while(true){object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){results.Add(e.ToString());throw;}yield return next;}}
        finally{File.WriteAllLines(Output+"/mesh-validation.txt",results);var input=UnityEngine.Object.FindFirstObjectByType<StandaloneInputModule>();if(input!=null)input.enabled=true;}
    }
    static IEnumerator Validate()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        var table=UnityEngine.Object.FindFirstObjectByType<BattlefieldWorldTable>();
        var input=EventSystem.current.GetComponent<StandaloneInputModule>();input.enabled=false;
        ui.Present(null,1,false,null,null,"");yield return Settle();
        var sample=Sample();ui.Present(sample,sample.seat,false,null,null,"");yield return Settle();
        // This fixture checks the expanded hand. A real field click during an
        // earlier run may have tucked it; do not rely on prior editor input.
        typeof(ForgeBattlefieldController).GetMethod("SetRedesignHandExpanded",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(ui,new object[]{true});
        yield return Settle();
        foreach(var card in Cards())card.Anchor.GetComponent<ForgeBattlefieldCardPointer>().ResetHover();
        var inspect=GameObject.Find("Canvas").transform.Find("InspectPanel");inspect.gameObject.SetActive(false);
        Check(Cards().Length>20,"Battlefield and hands produce real world cards");
        var link=(ForgeTargetArrow)typeof(ForgeBattlefieldController).GetMethod("NewAttackArrow",
            System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(ui,new object[]{"HandOcclusionProbe"});
        int linkOrder=link.GetComponent<Canvas>().sortingOrder;
        foreach(var pair in new[]{("RedesignHandBackplate","PlayerHandRoot"),("RedesignOtherHandBackplate","OpponentHandRoot")}){
            var tray=GameObject.Find(pair.Item1).GetComponent<Canvas>();
            var fan=GameObject.Find(pair.Item2).GetComponent<Canvas>();
            Check(tray.overrideSorting && fan.overrideSorting && linkOrder<tray.sortingOrder && tray.sortingOrder<fan.sortingOrder,
                pair.Item1+" covers battlefield links under its cards");
        }
        UnityEngine.Object.Destroy(link.gameObject);
        var markers=sample.players.SelectMany(p=>(p.emblems??Array.Empty<ForgeCardView>()).Concat(p.effectMarkers??Array.Empty<ForgeCardView>())).ToArray();
        Check(markers.Length>0&&!Cards().Any(c=>markers.Any(m=>m.id==c.NativeId)),"Player status markers retain their dedicated HUD presentation");
        // TextMeshPro's 3D component inherits Graphic but renders a MeshRenderer;
        // only TextMeshProUGUI needs Canvas rendering. Do not reject native 3D text.
        Check(Cards().All(c=>c.GetComponentInParent<Canvas>()==null&&c.GetComponentsInChildren<Canvas>().Length==0
            &&c.GetComponentsInChildren<Graphic>().All(g=>g is TMPro.TextMeshPro&&g.GetComponent<MeshRenderer>()!=null)),"No Canvas or Canvas-rendered graphic in any visible card hierarchy");
        Check(Cards().All(c=>c.GetComponent<BoxCollider>()!=null&&c.GetComponentsInChildren<MeshRenderer>().Length>=4),"Every world card has native geometry and a physical hit shape");
        Check(Cards().All(c=>c.Anchor.Find("Visual").GetComponentsInChildren<Graphic>().Length==0),"Compatibility anchors have no uGUI card faces");
        ScreenCapture.CaptureScreenshot(Output+"/overview-"+Screen.width+"x"+Screen.height+".png");yield return Settle();
        var handIds=sample.players.First(p=>p.seat==sample.seat).hand.Select(c=>c.id).ToHashSet();
        var own=Cards().Where(c=>c.Anchor.parent.name=="PlayerHandRoot"&&handIds.Contains(c.NativeId)).OrderBy(c=>c.Anchor.position.x).ToArray();
        Check(Cards().Where(c=>c.UsesHudCamera).All(c=>Vector3.Dot(c.transform.forward,table.HudCamera.transform.forward)>.999f),"Both resting hands face the screen without table perspective");
        var raised=own.Last();var pointer=raised.Anchor.GetComponent<ForgeBattlefieldCardPointer>();
        pointer.OnPointerEnter(new PointerEventData(EventSystem.current){position=raised.Anchor.position});yield return Settle();
        Check(raised.Lifted&&raised.gameObject.layer==BattlefieldWorldTable.LiftLayer,"Hovered hand card moves to the HUD camera above stack, below decision dialogs");
        Check(raised.GetComponentsInChildren<MeshRenderer>().All(r=>r.sortingOrder==34),"Lifted card surfaces share intended draw order");
        // Freeze the last pixel of the neighbour's return. At this point its cost row
        // can physically overlap the rules text of the currently enlarged hand card.
        var neighbour=own[own.Length-2];
        var returningPointer=neighbour.Anchor.GetComponent<ForgeBattlefieldCardPointer>();
        var returningVisual=neighbour.Anchor.Find("Visual");
        returningPointer.enabled=false;
        try {
            returningVisual.localPosition=Vector3.up;
            yield return Settle();
            var ink=neighbour.transform.Find("Current costs").GetComponentsInChildren<MeshRenderer>();
            var paper=raised.transform.Find("Artwork").GetComponent<MeshRenderer>();
            Check(neighbour.Lifted&&ink.Length>1&&ink.All(r=>r.sortingOrder<paper.sortingOrder &&
                table.HudCamera.WorldToScreenPoint(r.bounds.center).z>table.HudCamera.WorldToScreenPoint(paper.bounds.center).z),
                "Returning card's mana symbols sort and depth-test behind the hovered card face");
        } finally {
            returningVisual.localPosition=Vector3.zero;
            returningPointer.enabled=true;
        }
        ScreenCapture.CaptureScreenshot(Output+"/hover-"+Screen.width+"x"+Screen.height+".png");yield return Settle();
        pointer.OnPointerExit(new PointerEventData(EventSystem.current));yield return Settle();
        Check(!raised.Lifted&&raised.UsesHudCamera,"Hover exit restores resting hand without table perspective");
        var empty=Sample();var player=empty.players.First(p=>p.seat==empty.seat);player.hand=Array.Empty<ForgeCardView>();player.handCount=0;
        ui.Present(empty,empty.seat,false,null,null,"");yield return Settle();
        Check(!Cards().Any(c=>handIds.Contains(c.NativeId)),"Empty hand removes all former hand meshes and colliders");
        Check(player.commandCards.All(cmd=>Cards().Any(c=>c.NativeId==cmd.id&&c.Anchor.parent.name=="PlayerHandRoot")),"Pinned commander remains visible when the hand is empty");
        ScreenCapture.CaptureScreenshot(Output+"/empty-"+Screen.width+"x"+Screen.height+".png");yield return Settle();
        ui.Present(Sample(),sample.seat,false,null,null,"");yield return Settle();
        Check(Cards().Where(c=>c.Anchor.parent.name=="PlayerHandRoot"&&handIds.Contains(c.NativeId)).Select(c=>c.NativeId).ToHashSet().SetEquals(handIds),"Repopulating the hand creates the complete card set");
        Check(table.TableCamera!=table.HudCamera&&table.HudCamera.orthographic&&!table.TableCamera.orthographic,"Perspective table and pixel-compatible HUD cameras remain independent");
        var opponent=sample.players.First(p=>p.seat!=sample.seat);
        var hidden=Cards().Where(c=>c.Anchor.parent.name=="OpponentHandRoot" && !opponent.commandCards.Any(cmd=>cmd.id==c.NativeId)).ToArray();
        Check(hidden.Length==opponent.handCount && hidden.All(c=>c.Concealed),"Ordinary opponent hand remains anonymous backs");
        inspect.gameObject.SetActive(false);ScreenCapture.CaptureScreenshot(Output+"/overview-"+Screen.width+"x"+Screen.height+".png");
        results.Add("PASS bounded mesh/layer/lifecycle checks; sample is visual fixture, not Forge rules proof.");
    }
}
#endif
