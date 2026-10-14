#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class IntensityCopyPresentationValidation
{
    const string Base=".tools/intensity-copy-1009";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable] class Meta { public string status,directory,runtimeHash; public int sourceId,copyHostId,tokenId; }
    public static bool Running {get;private set;}
    public static string Output {get;private set;}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static Transform Panel=>Root.Find("InspectPanel");
    static TMPro.TextMeshProUGUI Rules=>Panel.Find("Rules").GetComponentInChildren<TMPro.TextMeshProUGUI>();
    static void Check(bool value,string label){if(!value)throw new Exception(label);File.AppendAllText(Output+"/checks.txt",label+"\n");}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();foreach(var p in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldCardPointer>(FindObjectsSortMode.None)){p.ResetHover();p.enabled=false;}}
    static void Inspect(ForgeBattlefieldController ui,ForgeCardView c){typeof(ForgeBattlefieldController).GetField("inspectOriginalState",Hidden).SetValue(ui,false);typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",Hidden).Invoke(ui,new object[]{c});Canvas.ForceUpdateCanvases();Rules.ForceMeshUpdate();}
    static void Click(Button button){Check(button!=null&&button.interactable,"Original-state button available");var r=(RectTransform)button.transform;foreach(float x in new[]{.5f,.2f,.8f}){var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(new Vector2(Mathf.Lerp(r.rect.xMin,r.rect.xMax,x),r.rect.center.y))),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);if(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button){ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);Canvas.ForceUpdateCanvases();Rules.ForceMeshUpdate();return;}}throw new Exception("Original-state ray blocked");}
    public static void Run(){if(!Application.isPlaying||Running)throw new Exception("Play required/already running");Running=true;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output+"/inputs");File.WriteAllText(Base+"/ui-latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");foreach(var p in new[]{"Assets/Scripts/Editor/IntensityCopyPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs","Assets/Scripts/UI/ForgeBattlefieldController.ModifiedCharacteristics.cs"})File.AppendAllText(Output+"/sources.tsv",p+"\t"+Hash(p)+"\n");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("IntensityCopyValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var it=Validate(ui);try{while(true){object next;try{if(!it.MoveNext())break;next=it.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS_BOUNDED: copied starting/added intensity and same-ID recopy, original physical no-false-intensity, normal original/start and copy-token printed start, actual original/return rays, three languages and two seat views. Unknown-original fallback and facedown filtering separately marked adapters.");}finally{(it as IDisposable)?.Dispose();Running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        string locale=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var notices=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=notices.Select(x=>x.enabled).ToArray();foreach(var n in notices)n.enabled=false;
        try{
            var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(Base+"/latest.json"));Check(meta.status=="PASS","Native producer passed");Check(meta.runtimeHash==JsonUtility.FromJson<Meta>(File.ReadAllText("ForgeRuntime/runtime.json")).runtimeHash,"Exact unchanged native runtime");
            foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}){
                GamePreferences.SetLanguage(lang);float end=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<end)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language==lang,"Actual locale "+lang);
                string prefix=lang=="zh-CN"?"强度：":lang=="ja-JP"?"強度：":"Intensity: ";
                foreach(int seat in new[]{1,2})foreach(string label in new[]{"cloned-start","intensified","token-start","finished"}){
                    string p=meta.directory+"/"+label+"-seat"+seat+".json";File.Copy(p,Output+"/inputs/"+label+"-seat"+seat+".json",true);File.AppendAllText(Output+"/input-hashes.tsv",p+"\t"+Hash(p)+"\n");
                    var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p));ui.Present(v,seat,false,null,null,"");yield return Frame();
                    int[] ids=label=="cloned-start"?new[]{meta.copyHostId}:label=="intensified"?new[]{meta.copyHostId,meta.sourceId}:label=="token-start"?new[]{meta.tokenId}:new[]{meta.copyHostId,meta.tokenId};
                    foreach(int id in ids){
                        var c=v.players[0].battlefield.Single(x=>x.id==id);Inspect(ui,c);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();Rules.ForceMeshUpdate();string tag=lang+" seat"+seat+" "+label+" ID"+id;string expected=c.intensity==c.originalIntensity?c.intensity.ToString():"<color=#70BFFF>"+c.intensity+"</color>";
                        Check(Rules.text.Contains(prefix+expected),tag+" exact effective total and increment tint");Check(Rules.gameObject.activeInHierarchy,tag+" actual visible rules");
                        if(c.intensity!=c.originalIntensity)Check(Rules.textInfo.characterInfo.Take(Rules.textInfo.characterCount).Any(x=>x.isVisible&&x.character==('0'+c.intensity)&&x.color.r==112&&x.color.g==191&&x.color.b==255),tag+" changed number actually drawn blue");
                        float size=Rules.fontSize;Click(Panel.Find("OriginalState").GetComponent<Button>());
                        if(id==meta.copyHostId)Check(!Rules.text.Contains(prefix)&&!Rules.text.Contains("Starting intensity"),tag+" physical Vesuvan original has no false copied intensity");
                        else if(id==meta.sourceId)Check(Rules.text.Contains(prefix+"1")&&!Rules.text.Contains(prefix+"<color="),tag+" normal original starting1 without increment");
                        else {var catalog=(Dictionary<string,CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog",Hidden).GetValue(ui);string printed=GamePreferences.CardRules(catalog[c.printingKey]);Check(!string.IsNullOrWhiteSpace(printed)&&Rules.text==printed,tag+" copy-token retains exact complete physical printed rules");}
                        Check(Rules.fontSize==size,tag+" original toggle fixed font");yield return new WaitForEndOfFrame();Click(Panel.Find("OriginalState").GetComponent<Button>());Check(Rules.text.Contains(prefix+expected),tag+" return restores same native total");
                        File.AppendAllText(Output+"/rendered.txt",tag+"\n"+Rules.text+"\n");Panel.gameObject.SetActive(false);
                    }
                }
            }
            GamePreferences.SetLanguage("zh-CN");float ready=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<ready)yield return null;
            var sample=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(meta.directory+"/cloned-start-seat1.json"));var unknown=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(sample.players[0].battlefield.Single(c=>c.id==meta.copyHostId)));unknown.printingKey="MTGB/unknown";Inspect(ui,unknown);yield return new WaitForEndOfFrame();Click(Panel.Find("OriginalState").GetComponent<Button>());Check(Rules.text.Contains("强度：1"),"Adapter unavailable physical definition retains known native baseline fallback");
            unknown.faceDown=true;var method=typeof(ForgeBattlefieldController).GetMethod("IntensityDetail",BindingFlags.Static|BindingFlags.NonPublic);Check((string)method.Invoke(null,new object[]{unknown,false})=="","Adapter facedown intensity remains hidden");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(locale);if(module!=null)module.enabled=input;for(int i=0;i<notices.Length;i++)if(notices[i]!=null)notices[i].enabled=flags[i];}
    }
}
#endif
