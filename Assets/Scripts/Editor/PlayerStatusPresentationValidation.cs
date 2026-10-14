#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PlayerStatusPresentationValidation
{
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    const string Base=".tools/player-status-1006/ui";
    [Serializable] sealed class Meta{public string folder,status,runtimeHash;}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.12f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    public static void Run(){Check(Application.isPlaying&&!Running,"Play required/already running");Running=true;Output=Base+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(Base+"/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/PlayerStatusPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Emblems.cs","Assets/Scripts/UI/ForgeBattlefieldController.Arena.cs","Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs","Assets/Scripts/UI/SpecialEffectArt.cs","Assets/Resources/SpecialEffectArt.json"}.Select(p=>p+"\t"+Hash(p)))+"\n");var go=new GameObject("PlayerStatusValidation");go.AddComponent<Host>().StartCoroutine(Guard(Exercise(),go));}
    sealed class Host:MonoBehaviour{}
    static IEnumerator Guard(IEnumerator e,GameObject go){Exception error=null;while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){error=ex;break;}if(!more)break;if(current is IEnumerator nested){yield return Nested(nested,ex=>error=ex);if(error!=null)break;}else yield return current;}try{(e as IDisposable)?.Dispose();}catch(Exception ex){error??=ex;}File.WriteAllText(Output+"/result.txt",error==null?"PASS: actual native day/night and speed1/2/3/4/max/source-leave inputs, both-seat perspectives, three locales, exact helper artwork and native object IDs, visible fixed-size speed value, raycast hover localized details, unchanged render/view-return fonts, no selection submissions; unknown marker adapter preserves engine text. No new rule or network proof.":"FAIL\n"+error);Running=false;UnityEngine.Object.Destroy(go);}
    static IEnumerator Nested(IEnumerator e,Action<Exception> fail){while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){fail(ex);break;}if(!more)break;if(current is IEnumerator child)yield return Nested(child,fail);else yield return current;}}
    static IEnumerator Exercise(){
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("PlayerStatusBattlefield").AddComponent<ForgeBattlefieldController>();bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool active=module!=null&&module.enabled;if(module!=null)module.enabled=false;string locale=GamePreferences.Language;var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None).Select(n=>(n,n.enabled)).ToArray();foreach(var n in notices)n.n.enabled=false;
        try{foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}){
            int revision=GamePreferences.Revision;bool change=GamePreferences.Language!=lang;GamePreferences.SetLanguage(lang);float until=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||change&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Language not ready");yield return Frame();
            foreach(var sample in new[]{("day-natural","day"),("day-natural","night"),("day-natural","resolved"),("speed-max","started"),("speed-max","speed-1"),("speed-max","speed-2"),("speed-max","speed-3"),("speed-max","max"),("speed-source-leave","source-left"),("speed-source-leave","resolved")})foreach(bool other in new[]{false,true}){
                string metaPath=".tools/player-status-1006/native/"+sample.Item1+"-result.json";var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(metaPath));Check(meta.status=="PASS"&&meta.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Native proof stale");string path=meta.folder+"/"+sample.Item2+(other?"-other":"")+".json";string local=Output+"/inputs/"+sample.Item1+"-"+Path.GetFileName(path);File.Copy(path,local,true);File.AppendAllText(Output+"/input-hashes.tsv",path+"\t"+Hash(path)+"\n");var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(local));int submits=0;ui.Present(view,view.seat,false,i=>submits++,null,"");yield return Frame();foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}
                var markers=view.players.SelectMany(p=>p.effectMarkers??Array.Empty<ForgeCardView>()).Concat(view.globalEffects??Array.Empty<ForgeCardView>()).Where(c=>c.helperArtName is "Day" or "Night" or "Start Your Engines!" or "Max Speed" or "Max Speed!").ToArray();Check(markers.Length>0,"Missing native marker");foreach(var card in markers){
                    var tile=Root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Card_"+card.id);var image=tile.Find("Visual/Border/Face")?.GetComponent<Image>();Check(SpecialEffectArt.TryLoad(card.helperArtName,out var expected)&&image!=null&&image.sprite==expected&&image.preserveAspect,"Wrong helper face "+card.helperArtName);
                    bool speed=card.helperArtName is "Start Your Engines!" or "Max Speed" or "Max Speed!";Text number=null;if(speed){number=tile.Find("Visual/SpeedStatus/Value")?.GetComponent<Text>();Check(number!=null&&number.text==view.players.Single(p=>p.seat==card.ownerSeat).speed.ToString()&&!number.resizeTextForBestFit&&number.fontSize==Mathf.RoundToInt(20*GamePreferences.TextScale),"Speed value missing/wrong/font");var r=(RectTransform)number.transform;Check(r.rect.width>=number.preferredWidth&&r.rect.height>=number.preferredHeight,"Speed digit clipped");}
                    var pointer=tile.GetComponent<ForgeBattlefieldCardPointer>();pointer.enabled=true;var rect=(RectTransform)tile;var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));var data=new PointerEventData(EventSystem.current){position=point};var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);Check(hits.Any(h=>h.gameObject.GetComponentInParent<ForgeBattlefieldCardPointer>()==pointer),"Marker hover blocked");ExecuteEvents.Execute(pointer.gameObject,data,ExecuteEvents.pointerEnterHandler);yield return Frame();string rules=Root.Find("InspectPanel/Rules").GetComponent<Text>().text;Check(rules.Contains(speed?(lang=="en-US"?"Speed: ":"速度：")+view.players.Single(p=>p.seat==card.ownerSeat).speed:lang=="en-US"?"currently":lang=="zh-CN"?"当前":"現在"),"Wrong localized state detail "+rules);var name=Root.Find("InspectPanel/Name").GetComponent<Text>();int font=name.fontSize;ui.ViewBattlefield();yield return Frame();ui.RestoreBattlefieldWindows();yield return Frame();Check(name.fontSize==font&&submits==0,"View/return font or selection drift");pointer.ResetHover();pointer.enabled=false;
                    ui.Present(view,view.seat,false,i=>submits++,null,"");yield return Frame();if(speed)Check(number!=null&&number.text==view.players.Single(p=>p.seat==card.ownerSeat).speed.ToString(),"Unchanged render drift");
                    if(lang=="zh-CN"&&!other&&(sample.Item2=="max"||sample.Item2=="night")){ScreenCapture.CaptureScreenshot(Output+"/"+sample.Item2+".png");yield return Frame();}
                }File.AppendAllText(Output+"/steps.log",lang+" "+sample.Item1+"/"+sample.Item2+" seat="+view.seat+" ids="+string.Join(",",markers.Select(c=>c.id))+" PASS\n");
            }
        }
        var unknown=new ForgeCardView{effectMarker=true,markerKind="effect",helperArtName="Unknown",name="Unknown",markerStatus="native state",currentRules="native rule"};string unknownText=(string)typeof(ForgeBattlefieldController).GetMethod("MarkerRules",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{unknown});Check(unknownText=="native state\n\nnative rule","Unknown marker substituted");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(locale);if(module!=null)module.enabled=active;foreach(var n in notices)if(n.n!=null)n.n.enabled=n.enabled;}
    }
}
#endif
