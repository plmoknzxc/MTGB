#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class StackPolishValidation
{
    const string Output=".tools/template-polish-1008/";
    const string Native=".tools/component-card-1008/native/1791396437633613700/";
    static ForgeSeatView Read(string p)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p));
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);File.AppendAllText(Output+"stack-checks.log",why+" PASS\n");}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.6f);Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();}
    static Vector2 Point(RectTransform r,Vector2 p)=>RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(p));
    static Button Hit(Vector2 p){var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=p},hits);return hits.FirstOrDefault().gameObject?.GetComponentInParent<Button>();}
    public static void Run(){ComponentCardFaceDemo.Stop();Directory.CreateDirectory(Output);File.WriteAllText(Output+"stack-checks.log","");File.WriteAllText(Output+"stack-result.txt","RUNNING");UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>().StartCoroutine(Guard());}
    static IEnumerator Guard(){var notice=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool prior=notice!=null&&notice.enabled;if(notice!=null)notice.enabled=false;var work=Validate();try{while(true){object v;try{if(!work.MoveNext())break;v=work.Current;}catch(Exception e){File.WriteAllText(Output+"stack-result.txt","FAIL "+e);throw;}yield return v;}File.WriteAllText(Output+"stack-result.txt","PASS focused 1/2/3 fan bounds, exposed-card rays, exact native stack target, collapse/restore and smaller scan hover.");}finally{if(notice!=null)notice.enabled=prior;}}
    static IEnumerator Validate(){
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        foreach(var state in new[]{Read(Native+"stack-giant.json"),Read(Native+"stack-adventure.json"),Read("Tools/Validation/Reports/StackInteractions/spells-three.json")}){
            ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(state,1,false,null,null,"");yield return Frame();GameObject.Find("InspectPanel")?.SetActive(false);int n=state.stackItems.Length;
            var viewport=GameObject.Find("StackColumn/Viewport").GetComponent<RectTransform>();
            for(int i=0;i<n;i++){
                var tile=GameObject.Find("StackCard_"+i).GetComponent<RectTransform>();var p=Point(tile,new Vector2(-100,-14));
                Check(RectTransformUtility.RectangleContainsScreenPoint(viewport,p),n+" fan exposed corner inside viewport "+i);
                Check(Hit(p)==tile.GetComponent<Button>(),n+" fan exposed corner reaches own card "+i);
                Check(i==0?Mathf.Abs(Mathf.DeltaAngle(tile.parent.localEulerAngles.z,0))<.1f:Mathf.Abs(Mathf.DeltaAngle(tile.parent.localEulerAngles.z,0))>3,n+" fan angle "+i);
                var phase=GameObject.Find("CornerCluster/ControlRow/TurnChip/TurnLabel").GetComponent<RectTransform>();var corners=new Vector3[4];tile.GetWorldCorners(corners);
                Check(corners.Min(c=>RectTransformUtility.WorldToScreenPoint(null,c).y)>Point(phase,new Vector2(0,phase.rect.yMax)).y+8,n+" fan clears phase "+i);
            }
            ScreenCapture.CaptureScreenshot(Output+"stack-"+n+".png");yield return Frame();
            if(n==2){var b=GameObject.Find("StackColumn/CollapseStack").GetComponent<Button>();b.onClick.Invoke();yield return Frame();Check(!viewport.GetComponent<CanvasGroup>().blocksRaycasts,"collapsed fan ignores rays");b.onClick.Invoke();yield return Frame();Check(viewport.GetComponent<CanvasGroup>().blocksRaycasts,"restored fan receives rays");}
        }
        var target=Read("Tools/Validation/Reports/StackInteractions/triggers-target-2.json");int calls=0;string sent=null;ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(target,1,true,i=>{calls++;sent=target.request.options[i].id;},null,"");yield return Frame();GameObject.Find("InspectPanel")?.SetActive(false);
        var rear=GameObject.Find("StackCard_1").GetComponent<RectTransform>();var exposed=Point(rear,new Vector2(-100,-14));Check(!ui.TryChooseTargetAt(exposed,target.request.requestId-1),"stale target rejected");Check(ui.TryChooseTargetAt(exposed,target.request.requestId)&&calls==1&&sent=="stack:"+target.stackItems[1].id,"fan selects exact native rear target");Check(!ui.TryChooseTargetAt(exposed,target.request.requestId),"duplicate target rejected");
        var two=Read(Native+"stack-adventure.json");ui.Present(null,1,false,null,null,"");yield return Frame();ui.Present(two,1,false,null,null,"");yield return Frame();var card=two.players[0].battlefield.First(c=>c.name=="Bonecrusher Giant");typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{card});yield return Frame();var panel=GameObject.Find("InspectPanel").GetComponent<RectTransform>();var art=GameObject.Find("InspectPanel/Art").GetComponent<Image>();Check(panel.rect.width<=300&&panel.rect.height<=720,"smaller bounded hover panel");Check(art.rectTransform.rect.height<=320&&art.sprite!=null&&art.GetComponentInChildren<TemplateCardFace>()==null,"hover retains smaller original scan");ScreenCapture.CaptureScreenshot(Output+"hover.png");yield return Frame();GameObject.Find("InspectPanel")?.SetActive(false);
    }
}
#endif
