#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class ChoiceArtAspectValidation
{
    const string Output=".tools/choice-art-top-review-1004";
    static bool running;
    static readonly List<string> observations=new List<string>();
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static ForgeSeatView Read(string path)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));
    static ForgeSeatView Combat(string name){var m=File.ReadLines("Tools/Validation/Reports/CombatAllocation/"+name+".jsonl").Select(JsonUtility.FromJson<ForgeWireMessage>).First(m=>m.request?.kind=="AssignAmount");var v=m.views[m.request.seat-1];v.request=m.request;v.awaitingInput=true;return v;}
    static float DisplayAspect(RawImage image){var r=image.rectTransform;return r.TransformVector(Vector3.right*r.rect.width).magnitude/r.TransformVector(Vector3.up*r.rect.height).magnitude;}
    static void Aspect(RawImage image,string label)
    {
        Check(image.texture!=null,"Missing source art "+label);
        float source=image.uvRect.width*image.texture.width/(image.uvRect.height*image.texture.height),display=DisplayAspect(image);
        Check(Mathf.Abs(source/display-1)<.0001f,"Stretched art "+label+" source="+source+" frame="+display);
        Check(!image.raycastTarget,"Art blocks selection "+label);
        observations.Add(GamePreferences.Language+"\t"+label+"\t"+source+"\t"+display+"\t"+image.uvRect);
    }
    static bool Affected(RawImage image)=>image.name=="Artwork"&&image.transform.parent.name=="CardHeader"||image.name=="CardHeader"&&image.transform.parent.name=="ChosenSpellFace"||image.name=="Art"&&(image.transform.parent.name.StartsWith("Recipient_")||image.transform.parent.name=="Face"||image.transform.parent.name=="AbilityFace"||image.transform.parent.name=="PreparedSpellFace");
    static void Click(Button b){var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,b.transform.TransformPoint(((RectTransform)b.transform).rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked click "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    public static void Run()
    {
        Check(Application.isPlaying,"Play Mode required");if(running)return;running=true;observations.Clear();Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("ChoiceArtAspectValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        string locale=GamePreferences.Language;var notice=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=notice!=null&&notice.enabled;if(notice!=null)notice.enabled=false;
        var it=Validate(ui);
        try{while(true){object next;try{if(!it.MoveNext())break;next=it.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: sprite-subrect / wide / tall frames / nonuniform ancestor scale / rotation / null reset, maximal crop and source bounds, top-aligned illustrations and centered headers; three locales, native many/small combat and effect assignment, casting options, prepared payment/stack, ability stack/order, modal options and chosen spell header actual pixel/display aspect; selection raycast identity/stale/duplicate/viewer guards, stable same-request recipient art and scroll, last of eleven reachable, View/Return. Only archived native UI replay; no new engine effects.");}
        finally{File.WriteAllLines(Output+"/aspect-observations.tsv",observations);running=false;ui.RestoreBattlefieldWindows();GamePreferences.SetLanguage(locale);if(notice!=null)notice.enabled=enabled;}
    }
    static void CropMatrix()
    {
        var texture=new Texture2D(640,800);var sprite=Sprite.Create(texture,new Rect(48,96,240,335),new Vector2(.5f,.5f));
        var parent=new GameObject("CropMatrix",typeof(RectTransform));var go=new GameObject("Image",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(parent.transform,false);var image=go.GetComponent<RawImage>();image.raycastTarget=false;
        try{
            foreach(var size in new[]{new Vector2(166,66),new Vector2(218,118),new Vector2(66,166),new Vector2(226,176)})foreach(var scale in new[]{Vector3.one,new Vector3(1.2f,.75f,1)})foreach(float angle in new[]{0f,15f})foreach(var region in new[]{new Rect(.07f,.43f,.86f,.45f),new Rect(0,.45f,1,.55f)}){
                bool illustration=region.x>.01f;
                image.rectTransform.sizeDelta=size;parent.transform.localScale=scale;parent.transform.localRotation=Quaternion.Euler(0,0,angle);
                if(illustration)CardArtCrop.Illustration(image,sprite);else CardArtCrop.Header(image,sprite);
                Aspect(image,"matrix "+(illustration?"top illustration":"center header")+"/"+size+"/"+scale+"/"+angle);
                var uv=image.uvRect;var crop=new Rect(uv.x*texture.width,uv.y*texture.height,uv.width*texture.width,uv.height*texture.height);var allowed=new Rect(48+240*region.x,96+335*region.y,240*region.width,335*region.height);
                Check(crop.xMin>=allowed.xMin-.001f&&crop.xMax<=allowed.xMax+.001f&&crop.yMin>=allowed.yMin-.001f&&crop.yMax<=allowed.yMax+.001f,"Crop leaks outside card region");
                Check(Mathf.Abs(crop.center.x-allowed.center.x)<.001f,"Crop is not horizontally centered");
                if(illustration)Check(Mathf.Abs(crop.yMax-allowed.yMax)<.001f,"Illustration loses its top edge");else Check(Mathf.Abs(crop.center.y-allowed.center.y)<.001f,"Header is not vertically centered");
                Check(Mathf.Abs(crop.width-allowed.width)<.001f||Mathf.Abs(crop.height-allowed.height)<.001f,"Crop discards both dimensions");
            }
            CardArtCrop.Illustration(image,null);Check(image.texture==null&&image.uvRect==new Rect(0,0,1,1),"Missing art retains previous texture/crop");
        }finally{UnityEngine.Object.Destroy(parent);UnityEngine.Object.Destroy(sprite);UnityEngine.Object.Destroy(texture);}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        CropMatrix();
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
            GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale not ready");
            var cases=new[]{("many",Combat("many-blockers")),("small",Combat("deathtouch-trample")),("effect",Read("Tools/Validation/Reports/AdvancedInteractions/damage-assign.json")),("casting",Read("Tools/Validation/Reports/CastingChoices/cast-adventure-menu.json")),("prepared-payment",Read("Tools/Validation/Reports/Prepare/payment.json")),("prepared-stack",Read("Tools/Validation/Reports/Prepare/stack.json")),("ability-stack",Read("Tools/Validation/Reports/StackInteractions/triggers-target-2.json")),("ability-order",Read("Tools/Validation/Reports/StackInteractions/triggers-order.json")),("modes",Read("Tools/Validation/Reports/ModeChoices/spree-rich-choices.json")),("chosen-spell",Read("Tools/Validation/Reports/ModeChoices/offspring-stack.json"))};
            foreach(var sample in cases){
                var v=sample.Item2;int sent=0,last=-1;Action<int> submit=i=>{sent++;last=i;};ui.Present(null,v.seat,false,null,null,"");yield return null;
                if(sample.Item1=="prepared-payment"){
                    // Payment keeps the casting intent from the native ready
                    // request; starting with payment alone loses that identity.
                    var ready=Read("Tools/Validation/Reports/Prepare/ready.json");ui.Present(ready,ready.seat,true,_=>{},null,"");yield return new WaitForSecondsRealtime(.5f);
                    var source=ready.players.SelectMany(p=>p.battlefield).First(c=>c.preparedSpell?.name=="Demonic Tutor");GameObject.Find("Card_"+source.id).GetComponent<Button>().onClick.Invoke();
                }
                ui.Present(v,v.seat,true,submit,null,"");yield return new WaitForSecondsRealtime(.8f);Canvas.ForceUpdateCanvases();
                var images=GameObject.Find("Canvas").GetComponentsInChildren<RawImage>().Where(Affected).ToArray();Check(images.Length>0,"No affected art in "+sample.Item1);
                foreach(var image in images.Where(i=>i.texture!=null))Aspect(image,sample.Item1+"/"+image.transform.parent.name+"/"+image.name);
                Check(images.Any(i=>i.texture!=null),"All local art missing "+sample.Item1);
                ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-"+sample.Item1+".png");yield return new WaitForEndOfFrame();
                if(sample.Item1=="many"||sample.Item1=="small"||sample.Item1=="effect"){
                    var panel=GameObject.Find("DamageAllocation");var art=panel.GetComponentsInChildren<RawImage>().First(i=>i.texture!=null);var rect=art.rectTransform;var uv=art.uvRect;
                    var scroll=panel.GetComponentInChildren<ScrollRect>();if(sample.Item1=="many"){
                        Check(v.request.allocations.Length==11,"Wrong many-recipient archive");scroll.horizontalNormalizedPosition=1;Canvas.ForceUpdateCanvases();yield return null;var lastTile=(RectTransform)scroll.content.Find("Recipient_"+v.request.allocations.Last().id);var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,lastTile);Check(scroll.viewport.rect.Contains(bounds.center),"Last recipient unreachable");ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-many-last.png");yield return new WaitForEndOfFrame();scroll.horizontalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
                    }
                    ui.ViewBattlefield();yield return null;ui.RestoreBattlefieldWindows();yield return null;Aspect(art,sample.Item1+" returned");ui.Present(v,v.seat,true,submit,null,"");yield return null;Check(art!=null&&art.rectTransform==rect&&art.uvRect==uv,"Same request recreated/rescaled recipient art");
                    // Some archives already assign the entire amount; only
                    // subtraction is then a legal native change.
                    var change=v.request.options.First(o=>o.id.StartsWith("amount-add:")||o.id.StartsWith("amount-sub:"));int index=Array.FindIndex(v.request.options,o=>o.id==change.id);var b=scroll.content.Find("Recipient_"+change.id.Split(':')[1]+(change.id.StartsWith("amount-add:")?"/Plus":"/Minus")).GetComponent<Button>();
                    // Scroll the chosen native recipient into view before raycast.
                    var tile=(RectTransform)b.transform.parent;float hidden=Mathf.Max(0,scroll.content.rect.width-scroll.viewport.rect.width);scroll.horizontalNormalizedPosition=hidden>0?Mathf.Clamp01((tile.anchoredPosition.x-8)/hidden):0;Canvas.ForceUpdateCanvases();yield return null;
                    ui.Choose(index,v.request.requestId-1);Check(sent==0,"Stale assignment submitted");Click(b);b.onClick.Invoke();Check(sent==1&&last==index,"Recipient raycast identity/duplicate guard");ui.Present(v,v.seat,false,submit,null,"");yield return null;Check(!b.interactable,"Viewer may assign");ui.Choose(index,v.request.requestId);Check(sent==1,"Viewer submitted");
                }
            }
        }
    }
}
#endif
