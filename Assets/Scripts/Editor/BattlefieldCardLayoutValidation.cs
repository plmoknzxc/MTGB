#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Only the changed card/stack layout is checked. Archived native requests keep
// their original engine identity; the seven-entry case is a layout adapter.
public static class BattlefieldCardLayoutValidation
{
    const string Output=".tools/battlefield-layout-1008/";
    const string Native=".tools/component-card-1008/native/1791396437633613700/";
    static bool running;
    static ForgeSeatView Read(string path)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);File.AppendAllText(Output+"steps.log",why+" PASS\n");}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.65f);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();}
    static IEnumerator Shot(string name,bool hover=false){if(!hover)GameObject.Find("InspectPanel")?.SetActive(false);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Output+name+".png");yield return Frame();}
    static Vector2 Point(RectTransform r,Vector2 p)=>RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(p));
    static Button Hit(Vector2 point){var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);return hits.FirstOrDefault().gameObject?.GetComponentInParent<Button>();}
    public static void Run()
    {
        if(running)return;if(!Application.isPlaying)throw new Exception("Play mode required");running=true;ComponentCardFaceDemo.Stop();
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"steps.log","");File.WriteAllText(Output+"result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CardLayoutValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool prior=banner!=null&&banner.enabled;if(banner!=null)banner.enabled=false;
        var test=Validate(ui);
        try{while(true){object next;try{if(!test.MoveNext())break;next=test.Current;}catch(Exception e){File.WriteAllText(Output+"result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"result.txt","PASS: focused original-cost removal, top-anchored battlefield art, larger scan inspection, 1/2/3/7 stack layout and exact two-stack target rays. Archived engine evidence reused; seven objects are display-only.");}
        finally{running=false;if(banner!=null)banner.enabled=prior;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        GamePreferences.SetLanguage("zh-CN");yield return Frame();
        var two=Read(Native+"stack-adventure.json");var one=Read(Native+"stack-giant.json");var three=Read("Tools/Validation/Reports/StackInteractions/spells-three.json");
        var many=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(three));
        many.stackItems=Enumerable.Range(0,7).Select(i=>{var e=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(three.stackItems[i%three.stackItems.Length]));e.id=10000+i;return e;}).ToArray();many.stack=many.stackItems.Select(e=>e.description).ToArray();many.stackCount=7;
        foreach(var state in new[]{one,two,three,many,two}) {
            ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(state,1,false,null,null,"");yield return Frame();
            GameObject.Find("InspectPanel")?.SetActive(false);int n=state.stackItems.Length;
            var viewport=(RectTransform)GameObject.Find("StackColumn/Viewport").transform;
            for(int i=0;i<n;i++) {
                var tile=(RectTransform)GameObject.Find("StackCard_"+i).transform;
                if(n<=3) {
                    Check(Mathf.Abs(Mathf.DeltaAngle(tile.parent.localEulerAngles.z,0))<.01f,n+" stack entry "+i+" stays upright");
                    var p=Point(tile,new Vector2(0,-42));Check(RectTransformUtility.RectangleContainsScreenPoint(viewport,p),n+" stack header "+i+" is inside viewport");
                    Check(Hit(p)==tile.GetComponent<Button>(),n+" stack header "+i+" has its own topmost ray target");
                    var phase=(RectTransform)GameObject.Find("CornerCluster/ControlRow/TurnChip/TurnLabel").transform;
                    Check(Point(tile,new Vector2(0,-tile.rect.height)).y>Point(phase,new Vector2(0,phase.rect.yMax)).y+8,n+" stack entry "+i+" clears phase header");
                }
                foreach(var face in tile.GetComponentsInChildren<TemplateCardFace>())Check(face.GetComponentsInChildren<TextMeshProUGUI>().All(t=>t.name!="PrintedCost"),"component stack has no original-fee footer");
            }
            Check(GameObject.Find("StackColumn/ViewStack")!=null== (n>=3),n+" stack overview threshold preserved");
            yield return Shot("stack-"+n);
            if(n==2) {
                var button=GameObject.Find("StackColumn/CollapseStack").GetComponent<Button>();button.onClick.Invoke();yield return Frame();
                Check(!viewport.GetComponent<CanvasGroup>().blocksRaycasts,"collapsed small stack blocks input");button.onClick.Invoke();yield return Frame();
                Check(viewport.GetComponent<CanvasGroup>().blocksRaycasts,"restored small stack receives input");
            }
        }
        var inspect=typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(string name in new[]{"Elvish Visionary","Bonecrusher Giant"}) {
            var card=two.players[0].battlefield.First(c=>c.name==name);var host=GameObject.Find("Card_"+card.id);
            var art=host.GetComponentsInChildren<RawImage>().First(i=>i.name=="Illustration");var source=host.GetComponentsInChildren<RawImage>().First(i=>i.name=="PrintedName");
            var sprite=typeof(ForgeBattlefieldController).GetMethod("TryBattlefieldArt",BindingFlags.Instance|BindingFlags.NonPublic);object[] args={card,null};sprite.Invoke(ui,args);var full=(Sprite)args[1];
            float top=(full.rect.y+full.rect.height*.88f)/full.texture.height;
            Check(Mathf.Abs(art.uvRect.yMax-top)<.0001f,name+" battlefield art preserves illustration top");
            Check(art.uvRect.width>0&&source.texture==art.texture,name+" battlefield art keeps source scan");
            inspect.Invoke(ui,new object[]{card});yield return Frame();var scan=GameObject.Find("InspectPanel/Art").GetComponent<Image>();
            Check(scan.rectTransform.rect.height>=390&&scan.sprite!=null&&scan.GetComponentInChildren<TemplateCardFace>()==null,name+" hover shows enlarged original scan");
            yield return Shot("hover-"+(name=="Bonecrusher Giant"?"giant":"visionary"),true);GameObject.Find("InspectPanel")?.SetActive(false);
        }
        var target=Read("Tools/Validation/Reports/StackInteractions/triggers-target-2.json");int calls=0;string sent=null;
        ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(target,1,true,i=>{calls++;sent=target.request.options[i].id;},null,"");yield return Frame();GameObject.Find("InspectPanel")?.SetActive(false);
        var back=(RectTransform)GameObject.Find("StackCard_1").transform;var exposed=Point(back,new Vector2(0,-42));
        Check(Hit(exposed)==back.GetComponent<Button>(),"actual target request exposes rear header");
        Check(!ui.TryChooseTargetAt(exposed,target.request.requestId-1),"stale stack target remains rejected");
        Check(ui.TryChooseTargetAt(exposed,target.request.requestId)&&sent=="stack:"+target.stackItems[1].id&&calls==1,"exposed rear header submits exact native stack identity once");
        Check(!ui.TryChooseTargetAt(exposed,target.request.requestId),"duplicate stack target remains rejected");
        ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(two,1,false,null,null,"");yield return Frame();yield return Shot("final-two");
    }
}
#endif
