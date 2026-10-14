#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public static class DelayedExpiryPresentationValidation
{
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    const string Base=".tools/delayed-expiry-1006/ui";
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable]sealed class Meta{public string status,folder,runtimeHash;}
    sealed class Host:MonoBehaviour{}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Clean(ForgeBattlefieldController ui){foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}var revision=typeof(ForgeBattlefieldController).GetField("inspectHoverRevision",Hidden);revision.SetValue(ui,(int)revision.GetValue(ui)+1);Root.Find("InspectPanel").gameObject.SetActive(false);}
    public static void Run(){Check(Application.isPlaying&&!Running&&GamePreferences.Language=="zh-CN","Chinese Play required");Running=true;Output=Base+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(Base+"/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/DelayedExpiryPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Arena.cs","Assets/Scripts/UI/ForgeBattlefieldController.Emblems.cs","Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs","Assets/Scripts/UI/TemporaryCopyGlow.cs"}.Select(p=>p+"\t"+Hash(p)))+"\n");var go=new GameObject("DelayedExpiryValidation");go.AddComponent<Host>().StartCoroutine(Guard(Exercise(),go));}
    static IEnumerator Guard(IEnumerator e,GameObject go){Exception error=null;while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){error=ex;break;}if(!more)break;if(current is IEnumerator child){yield return Nested(child,ex=>error=ex);if(error!=null)break;}else yield return current;}try{(e as IDisposable)?.Dispose();}catch(Exception ex){error??=ex;}File.WriteAllText(Output+"/result.txt",error==null?"PASS: Chinese incremental replay of actual self-exile counter/repeat, source-killed sacrifice and donated-token expiry; both display perspectives, exact token ID and controller region, silver mesh/no ray blocking and blue instruction while native obligation exists, same-ID clear after control-changed expiry, disappearing tokens cleared. Existing three-locale shape/text evidence reused; no new first UI suite/transport or hidden-information proof.":"FAIL\n"+error);Running=false;UnityEngine.Object.Destroy(go);}
    static IEnumerator Nested(IEnumerator e,Action<Exception> fail){while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){fail(ex);break;}if(!more)break;if(current is IEnumerator child)yield return Nested(child,fail);else yield return current;}}
    static IEnumerator Exercise(){
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("DelayedExpiryBattlefield").AddComponent<ForgeBattlefieldController>();bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool active=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        try{
            float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale unavailable");
            foreach(var scenario in new[]{("self-exile-counter",new[]{"copy-created","countered-survives","expiry-2","next-endstep-exiled"}),("external-source-leave",new[]{"copy-created","source-left","expiry-1","sacrificed-source-gone"}),("external-control-change",new[]{"copy-created","control-changed","expiry-1","survives-expiry"})}){
                var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/delayed-expiry-1006/native/"+scenario.Item1+"-result.json"));Check(meta.status=="PASS"&&meta.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Native proof stale");var initial=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(meta.folder+"/copy-created.json"));int token=initial.players[0].battlefield.Single(c=>c.token&&c.name=="Grizzly Bears").id;
                foreach(int seat in new[]{1,2}){ui.Present(null,seat,false,null,null,"");float initialY=0;int selfObject=0;
                    foreach(string label in scenario.Item2){string path=meta.folder+"/"+label+(seat==1?"":"-other")+".json";string copy=Output+"/inputs/"+scenario.Item1+"-"+seat+"-"+label+".json";File.Copy(path,copy);File.AppendAllText(Output+"/input-hashes.tsv",path+"\t"+Hash(path)+"\n");var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));ui.Present(v,seat,false,null,null,"");yield return Frame();Clean(ui);var card=v.players.SelectMany(p=>p.battlefield).SingleOrDefault(c=>c.id==token);var obj=GameObject.Find("Card_"+token);
                        if(card==null){Check(obj==null,"Departed token UI survived");Check(UnityEngine.Object.FindObjectsByType<TemporaryCopyGlow>(FindObjectsSortMode.None).Length==0,"Departed token border survived");}
                        else{Check(obj!=null,"Exact token object missing");var glow=obj.GetComponentInChildren<TemporaryCopyGlow>();bool timed=(card.pendingDepartures?.Length??0)>0;Check((glow!=null)==timed,"Native expiry/frame disagreement");if(glow!=null)Check(!glow.raycastTarget&&glow.canvasRenderer.GetMesh().vertexCount==480,"Expiry mesh/ray target");float y=RectTransformUtility.WorldToScreenPoint(null,obj.transform.position).y;if(label=="copy-created"){initialY=y;selfObject=obj.GetInstanceID();}if(label=="control-changed")Check((seat==1?y-initialY:initialY-y)>50,"Token stayed in former controller region");if(label=="countered-survives")Check(obj.GetInstanceID()==selfObject,"Unchanged self-trigger rebuilt token");
                            typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{card});yield return Frame();var rules=Root.Find("InspectPanel/Rules").GetComponentInChildren<TMPro.TextMeshProUGUI>();string text=ForgeBattlefieldController.PendingDepartureText(new ForgePendingDeparture{action=scenario.Item1=="self-exile-counter"?"exile":"sacrifice",phase="End of turn"});Check(timed?rules.text.Contains("<color=#70BFFF>"+text+"</color>"):!rules.text.Contains(text),"Blue expiry text stale/missing");Clean(ui);
                        }
                        File.AppendAllText(Output+"/steps.log",scenario.Item1+" seat="+seat+" "+label+" PASS\n");if(seat==1&&label=="control-changed"){ScreenCapture.CaptureScreenshot(Output+"/control-changed.png");yield return Frame();}
                    }
                }
            }
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=active;}
    }
}
#endif
