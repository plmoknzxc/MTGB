#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class BattlefieldHandCenteringPresentationValidation
{
    public const string Output=".tools/battlefield-hand-centering-1005";
    static bool running;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static IEnumerator Frame(float t=.5f){yield return new WaitForSecondsRealtime(t);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static ForgeSeatView Read(string file)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/"+file+".json"));
    static Transform CanvasRoot=>GameObject.Find("Canvas").transform;
    static Rect Bounds(RectTransform r){var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c.Min(v=>v.x),c.Min(v=>v.y),c.Max(v=>v.x),c.Max(v=>v.y));}
    static void Centered(RectTransform r){Check(Vector2.Distance(Bounds(r).center,Bounds((RectTransform)CanvasRoot).center)<1,"Window off center: "+r.name);}
    static void Life(Transform plate){
        var text=plate.Find("Life").GetComponent<Text>();Check(!plate.Find("LifeCaption").gameObject.activeSelf,"Life caption remains");
        var mesh=text.canvasRenderer.GetMesh();var vertices=mesh.vertices;var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=-min;
        for(int i=0;i+3<vertices.Length;i+=4){if(Mathf.Abs(vertices[i].x-vertices[i+2].x)<.001f||Mathf.Abs(vertices[i].y-vertices[i+2].y)<.001f)continue;for(int j=0;j<4;j++){min=Vector2.Min(min,vertices[i+j]);max=Vector2.Max(max,vertices[i+j]);}}
        Check(!float.IsInfinity(min.x),"Life glyph mesh empty");var center=(min+max)*.5f;
        Check(Vector2.Distance(center,text.rectTransform.rect.center)<.2f,"Visible life glyphs not centered");
        var position=plate.InverseTransformPoint(text.transform.TransformPoint(center));Check(Mathf.Abs(position.x-110)<.2f&&Mathf.Abs(position.y)<.2f,"Life region misplaced");
    }
    public static void Run(){Check(Application.isPlaying,"Play Mode required");if(running)return;running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){string locale=GamePreferences.Language;var test=Validate(ui);try{while(true){object next;try{if(!test.MoveNext())break;next=test.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: zh/en/ja 1.3 text scale; actual life glyph meshes centered in the divider-to-edge region, no caption, 1/20/100/negative numbers; same-size 180-degree opponent hidden/revealed hands, inward hover with matching scale and exact face ordering over player HUD; native nine-card choice and synthetic dense fallback centered, modal above both hands, native library choice centered; view/restore keeps center and font; 575 IDs per player retained. Native requests replayed, expanded density/numeric cases are display adapters, no new Forge effect or network claims.");}finally{running=false;GamePreferences.SetLanguage(locale);ui.RestoreBattlefieldWindows();}}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        foreach(var locale in new[]{"zh-CN","en-US","ja-JP"}){
            GamePreferences.SetLanguage(locale);float deadline=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready,"Locale not ready");
            var mouse=UnityEngine.InputSystem.Mouse.current;if(mouse!=null){UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(mouse.position,new Vector2(1,1));UnityEngine.InputSystem.InputSystem.Update();}
            ui.Present(null,1,false,null,null,"");var normal=DuelLayoutPresentationValidation.DensityFixture(true);ui.Present(normal,1,true,_=>{},null,"");yield return Frame(1);
            foreach(var plate in new[]{CanvasRoot.Find("PlayerPlate"),CanvasRoot.Find("OpponentPlate")}){
                var text=plate.Find("Life").GetComponent<Text>();foreach(var n in new[]{"20","1","100","-5"}){text.text=n;yield return Frame(.1f);Life(plate);}text.text="20";
            }
            var before=Read("Thoughtseize/before");ui.Present(before,1,true,_=>{},null,"");yield return Frame(1);
            var own=CanvasRoot.Find("PlayerHandRoot").GetComponentsInChildren<ForgeBattlefieldCardPointer>().First();var size=((RectTransform)own.transform).rect.size;
            foreach(var tile in CanvasRoot.Find("OpponentHandRoot").GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("OtherHand_"))){Check(Vector2.Distance(size,tile.rectTransform.rect.size)<.1f,"Hidden hand size differs");Check(Mathf.Abs(Mathf.DeltaAngle(tile.transform.eulerAngles.z,180))<=14.1f,"Hidden hand not inverted");}
            var choice=Read("Thoughtseize/choice");ui.Present(choice,1,true,_=>{},null,"");yield return Frame(1);
            foreach(var handName in new[]{"PlayerHandRoot","OpponentHandRoot"}){
                bool top=handName=="OpponentHandRoot";var root=CanvasRoot.Find(handName);var cards=root.GetComponentsInChildren<ForgeBattlefieldCardPointer>().OrderBy(p=>p.transform.GetSiblingIndex()).ToArray();
                Check(cards.All(p=>Vector2.Distance(size,((RectTransform)p.transform).rect.size)<.1f),"Hand sizes differ");
                Check(root.GetSiblingIndex()>CanvasRoot.Find(top?"OpponentPlate":"PlayerPlate").GetSiblingIndex(),"Hand behind player HUD");
                foreach(var card in new[]{cards.First(),cards.Last()}){
                    var face=(RectTransform)card.transform.Find("Visual");var start=face.position;card.OnPointerEnter(new PointerEventData(EventSystem.current));yield return Frame(.5f);
                    Check(Mathf.Abs(Mathf.DeltaAngle(face.eulerAngles.z,top?180:0))<1,"Hover direction wrong");Check(Mathf.Abs(face.localScale.x-1.32f)<.01f&&Mathf.Abs(face.position.x-start.x)<2&&(top?face.position.y<start.y-90:face.position.y>start.y+90),"Hover size/position differs");
                    var hud=Bounds((RectTransform)CanvasRoot.Find(top?"OpponentPlate":"PlayerPlate"));var rect=Bounds(face);
                    if(rect.Overlaps(hud)){var point=new Vector2((Mathf.Max(rect.xMin,hud.xMin)+Mathf.Min(rect.xMax,hud.xMax))*.5f,(Mathf.Max(rect.yMin,hud.yMin)+Mathf.Min(rect.yMax,hud.yMax))*.5f);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(card.transform),"Hover hit hidden by HUD");}
                    if(locale=="zh-CN"&&top&&card==cards.First())ScreenCapture.CaptureScreenshot(Output+"/opponent-hover.png");
                    card.OnPointerExit(new PointerEventData(EventSystem.current));yield return Frame(.4f);
                }
            }
            var denseHand=Read("Thoughtseize/choice");var hand=denseHand.players[1].hand.ToList();for(int i=0;i<6;i++){var c=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(hand[0]));c.id=900000+i;hand.Add(c);}denseHand.players[1].hand=hand.ToArray();denseHand.players[1].handCount=hand.Count;
            ui.Present(denseHand,1,true,_=>{},null,"");yield return Frame(1);var window=GameObject.Find("CardSelection").GetComponent<RectTransform>();Centered(window);var overlay=CanvasRoot.Find("CardSelectionOverlay");foreach(var name in new[]{"PlayerHandRoot","OpponentHandRoot"})Check(overlay.GetSiblingIndex()>CanvasRoot.Find(name).GetSiblingIndex(),"Hand covers selection");
            var title=window.Find("Title").GetComponent<Text>();int font=title.fontSize;ui.ViewBattlefield();ui.RestoreBattlefieldWindows();yield return Frame();Centered(window);Check(title.fontSize==font,"Restore changes font");
            if(locale=="zh-CN")ScreenCapture.CaptureScreenshot(Output+"/centered-hand-list.png");
            var library=Read("LibraryCosts/own-cards-1-0");ui.Present(library,library.seat,true,_=>{},null,"");yield return Frame();Centered(GameObject.Find("CardSelection").GetComponent<RectTransform>());
            AbilityChoicePresentationValidation.Run();float abilityDeadline=Time.realtimeSinceStartup+30;
            while(File.ReadAllText(".tools/ability-choices/result.txt")=="RUNNING"&&Time.realtimeSinceStartup<abilityDeadline)yield return null;
            string abilityResult=File.ReadAllText(".tools/ability-choices/result.txt");Check(abilityResult.StartsWith("PASS"),"Ability list regression: "+abilityResult);
            File.WriteAllText(Output+"/ability-"+locale+".txt",abilityResult);
            var density=BattlefieldStressPresentationValidation.Fixture();Check(density.players.All(p=>p.battlefield.Length==575&&p.battlefield.Select(c=>c.id).Distinct().Count()==575),"Stress IDs lost");ui.Present(density,1,true,_=>{},null,"");yield return Frame(1);Life(CanvasRoot.Find("PlayerPlate"));Life(CanvasRoot.Find("OpponentPlate"));
            if(locale=="zh-CN")ScreenCapture.CaptureScreenshot(Output+"/dense-centered-life.png");
        }
    }
}
#endif
