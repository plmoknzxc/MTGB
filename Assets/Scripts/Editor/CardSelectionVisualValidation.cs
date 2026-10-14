#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// Presentation-only regression: actual production click/staging, cached picker
// refresh and texture sampling. Does not substitute for native effect evidence.
public static class CardSelectionVisualValidation
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Out=".tools/selection-startup-1013";
    sealed class Host:MonoBehaviour { }
    static readonly List<string> checks=new();
    public static bool Running;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    public static void Run(){Running=true;checks.Clear();new GameObject("SelectionVisualProbe").AddComponent<Host>().StartCoroutine(Guard(Exercise()));}
    static IEnumerator Guard(IEnumerator task){while(true){bool more;object next;try{more=task.MoveNext();next=more?task.Current:null;}catch(Exception e){checks.Add("FAIL "+e);more=false;next=null;}if(!more)break;yield return next;}File.WriteAllLines(Out+"/ui-result.txt",checks);Running=false;}
    static object Field(object obj,string key)=>obj.GetType().GetField(key,Hidden).GetValue(obj);
    static void Call(object obj,string key,params object[] args)=>obj.GetType().GetMethod(key,Hidden).Invoke(obj,args);
    static ForgePlayableGlow Selected()=>UnityEngine.Object.FindObjectsByType<ForgePlayableGlow>(FindObjectsSortMode.None).FirstOrDefault(g=>g.selected&&g.gameObject.activeInHierarchy);
    static IEnumerator Exercise()
    {
        while(!GamePreferences.Ready)yield return null;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("VisualBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;
        var message=JsonUtility.FromJson<OnlineStartMatchMessage>(File.ReadAllText(".tools/opening-sync-fix-1013/legacy-seat1.json"));var v=message.view.forge;
        ui.Present(v,1,true,i=>{},null,"");
        var catalog=(Dictionary<string,CardDefinition>)Field(ui,"catalog");
        string[] names={"Subtlety","Living End","Pinnacle Emissary","Shardless Agent","Island","Forest","Mountain"};
        for(int i=0;i<7;i++) {var c=v.players[0].hand[i];var entry=catalog.First(x=>x.Value.cardName==names[i]);c.printingKey=entry.Key;c.name=names[i];c.manaCost=entry.Value.manaCost;}
        v.request=new ForgeRequest{kind="InputSelectCardsFromList",requestId=21001,seat=1,minCount=0,maxCount=1,options=v.players[0].hand.Select(c=>new ForgeOption{id="card:"+c.id,card=c,label=c.name}).Concat(new[]{new ForgeOption{id="cancel",label="Cancel"}}).ToArray()};v.awaitingInput=true;
        int sent=0;ui.Present(v,1,true,i=>sent++,null,"");yield return new WaitForSecondsRealtime(.35f);
        Check(Selected()==null,"Unselected hand has no gold selected glow");
        ScreenCapture.CaptureScreenshot(Out+"/hand-unselected.png");yield return null;
        GameObject.Find("Card_101").GetComponent<Button>().onClick.Invoke();yield return new WaitForSecondsRealtime(.35f);Canvas.ForceUpdateCanvases();
        var glow=Selected();Check(glow!=null&&glow.name=="SelectedGlow","Clicking an optional hand card shows gold glow immediately");Check(sent==0,"Staging selection makes no network round trip");
        Check(!glow.raycastTarget,"Glow cannot intercept card clicks");
        var mesh=glow.canvasRenderer.GetMesh();Check(mesh!=null&&mesh.vertexCount>0&&mesh.colors.Any(c=>c.r>.9f&&c.g>.7f&&c.b<.5f&&c.a>.95f),"Selected mesh has bright gold inner edge");
        var image=GameObject.Find("Card_101").transform.Find("Face/Border/Face")?.GetComponent<Image>();
        if(image==null)image=GameObject.Find("Card_101").GetComponentsInChildren<Image>().First(x=>x.sprite!=null&&x.sprite.texture.name.StartsWith("CardImage_"));
        var texture=image.sprite.texture;
        Check(texture.mipmapCount>1&&texture.filterMode==FilterMode.Trilinear&&Mathf.Approximately(texture.mipMapBias,.3f),"Actual hand scan uses mipmaps, trilinear filtering and mild minification softness");
        ScreenCapture.CaptureScreenshot(Out+"/hand-selected.png");yield return null;
        GameObject.Find("Card_101").GetComponent<Button>().onClick.Invoke();yield return null;Check(Selected()==null,"Clicking selected hand card again removes its highlight");
        v.request.requestId++;v.request.kind="InputSelectCardsFromList";v.request.options=v.players[0].hand.Select(c=>new ForgeOption{id="card:"+c.id,card=c,label=c.name}).Concat(new[]{new ForgeOption{id="ok",label="OK"}}).ToArray();
        var candidates=v.players[0].hand.Select(c=>JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(c))).ToArray();foreach(var c in candidates)c.zone="Graveyard";
        for(int i=0;i<candidates.Length;i++)v.request.options[i].card=candidates[i];
        v.request.previewCards=candidates;v.request.minCount=1;v.request.maxCount=2;
        ui.Present(v,1,true,i=>{},null,"");yield return null;
        Check(Selected()==null,"Unselected shared graveyard picker starts without selected glow");
        v.request.options[0].selected=true;v.request.selectedCount=1;ui.Present(v,1,true,i=>{},null,"");yield return new WaitForSecondsRealtime(.2f);
        Check(Selected()!=null&&Selected().transform.parent.name=="ChoiceCard_101","Cached picker update creates highlight on selected card");
        ScreenCapture.CaptureScreenshot(Out+"/picker-selected.png");yield return null;
        v.request.options[0].selected=false;v.request.selectedCount=0;ui.Present(v,1,true,i=>{},null,"");yield return null;Check(Selected()==null,"Cached picker deselection hides highlight");
    }
}
#endif
