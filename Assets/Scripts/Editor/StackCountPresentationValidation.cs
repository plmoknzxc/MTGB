#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class StackCountPresentationValidation
{
    public static bool Running {get;private set;}
    public static string Output {get;private set;}
    [Serializable] sealed class Meta {public string folder,status,runtimeHash;}
    sealed class Host:MonoBehaviour{}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    public static void Run(){Check(Application.isPlaying&&!Running,"Play required");Running=true;Output=".tools/stack-count-1006/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(".tools/stack-count-1006/ui/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/StackCountPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Arena.cs","Assets/Scripts/UI/PreferenceText.cs"}.Select(p=>p+"\t"+Hash(p)))+"\n");var go=new GameObject("StackCountValidation");go.AddComponent<Host>().StartCoroutine(Guard(Exercise(),go));}
    static IEnumerator Guard(IEnumerator e,GameObject go){Exception error=null;while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){error=ex;break;}if(!more)break;if(current is IEnumerator nested){yield return Nested(nested,ex=>error=ex);if(error!=null)break;}else yield return current;}try{(e as IDisposable)?.Dispose();}catch(Exception ex){error??=ex;}File.WriteAllText(Output+"/result.txt",error==null?"PASS: actual grouped land count badges, first-frame and later-frame glyph mesh, preferred-height bounds, both public perspectives and three languages at .85/1/1.3 font scales. Local formatting regression only; no new native/UI first or full/network PASS.":"FAIL\n"+error);Running=false;UnityEngine.Object.Destroy(go);}
    static IEnumerator Nested(IEnumerator e,Action<Exception> fail){while(true){bool more=false;object current=null;try{more=e.MoveNext();if(more)current=e.Current;}catch(Exception ex){fail(ex);break;}if(!more)break;if(current is IEnumerator child)yield return Nested(child,fail);else yield return current;}}
    static void Click(Button b){var rect=(RectTransform)b.transform;var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);}
    static IEnumerator Exercise(){
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("CountBattlefield").AddComponent<ForgeBattlefieldController>();bool enabled=ui.enabled;ui.enabled=false;string language=GamePreferences.Language;float scale=GamePreferences.TextScale;var module=EventSystem.current.currentInputModule;bool active=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        const string folder=".tools/ring-combat-1006/change-native/ring-bearer-change/1791264493055884200";
        try{foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}){int rev=GamePreferences.Revision;bool changing=GamePreferences.Language!=lang;GamePreferences.SetLanguage(lang);float deadline=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||changing&&GamePreferences.Revision==rev)&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready,"Locale not ready");
            foreach(float value in new[]{.85f,1f,1.3f}){GamePreferences.SetScale(value);yield return Frame();foreach(string file in new[]{"bearer-changed.json","bearer-changed-other.json"}){var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(folder+"/"+file));File.AppendAllText(Output+"/input-hashes.tsv",folder+"/"+file+"\t"+Hash(folder+"/"+file)+"\n");ui.Present(null,view.seat,false,null,null,"");yield return Frame();ui.Present(view,view.seat,false,null,null,"");Canvas.ForceUpdateCanvases();
                var labels=Root.GetComponentsInChildren<Text>().Where(t=>t.name=="Value"&&t.transform.parent.name=="Count").ToArray();Check(labels.Length>0,"No actual grouped count");foreach(var t in labels)Check(t.preferredHeight<=t.rectTransform.rect.height+1&&!t.resizeTextForBestFit&&t.fontSize==Mathf.RoundToInt(19*value),"Initial count font/height");
                yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();foreach(var t in labels){var mesh=t.canvasRenderer.GetMesh();Check(mesh!=null&&mesh.vertexCount>=8&&!t.canvasRenderer.cull,"First-frame count mesh missing");File.AppendAllText(Output+"/measure.tsv",lang+"\t"+value+"\t"+view.seat+"\t"+t.text+"\t"+t.fontSize+"\t"+t.preferredHeight+"\t"+t.rectTransform.rect.height+"\t"+mesh.vertexCount+"\n");}if(value==1.3f&&file=="bearer-changed.json"){ScreenCapture.CaptureScreenshot(Output+"/"+lang+".png");yield return Frame();}
                yield return new WaitForSecondsRealtime(.6f);foreach(var t in labels)Check(t.preferredHeight<=t.rectTransform.rect.height+1&&t.fontSize==Mathf.RoundToInt(19*value)&&t.canvasRenderer.GetMesh().vertexCount>=8,"Later preference refresh clips count");
            }}
        }}finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetScale(scale);GamePreferences.SetLanguage(language);if(module!=null)module.enabled=active;}
    }
}
#endif
