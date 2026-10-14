#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Archived native requests check presentation only. Stack counts beyond the
// archive are explicit layout adapters; no new effect coverage is implied.
public static class StackFanFontPresentationValidation
{
    const string Output=".tools/stack-fan-review-1004/ui";
    const string Input=".tools/battlefield-view-review-1004/input/";
    static bool running;
    static readonly List<string> observations=new List<string>();
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static ForgeSeatView Read(string file)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(file));
    static IEnumerable<RectTransform> Windows(ForgeBattlefieldController ui)=>((IEnumerable<RectTransform>)typeof(ForgeBattlefieldController).GetMethod("BattlefieldWindows",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,null)).Where(w=>w!=null&&w.gameObject.activeInHierarchy).Distinct();
    sealed class FontSample
    {
        public Text legacy;public TMP_Text tmp;public float size;public UnityEngine.Object font;
        public void Verify(string phase){Check(legacy!=null||tmp!=null,"Label was recreated: "+phase);Check(Mathf.Abs((legacy!=null?legacy.fontSize:tmp.fontSize)-size)<.01f,"Font size changed "+(legacy!=null?legacy.name:tmp.name)+" "+phase+" expected "+size+" got "+(legacy!=null?legacy.fontSize:tmp.fontSize));Check((legacy!=null?(UnityEngine.Object)legacy.font:tmp.font)==font,"Font changed "+phase);}
    }
    static FontSample[] Capture(ForgeBattlefieldController ui)
    {
        var roots=Windows(ui).Select(w=>(Transform)w).ToList();
        foreach(string name in new[]{"ViewBattlefield","ReturnSelection"}){var b=GameObject.Find("Canvas").transform.Find(name);if(b!=null)roots.Add(b);}
        // Clear detaches/deactivates outgoing controls until end of frame.
        // They are not part of the newly visible window's first-frame sample.
        return roots.SelectMany(r=>r.GetComponentsInChildren<Text>(true)).Where(t=>t.gameObject.activeInHierarchy||t.transform.parent.name=="ViewBattlefield"||t.transform.parent.name=="ReturnSelection").Distinct().Select(t=>new FontSample{legacy=t,size=t.fontSize,font=t.font})
            .Concat(roots.SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)).Where(t=>t.gameObject.activeInHierarchy).Distinct().Select(t=>new FontSample{tmp=t,size=t.fontSize,font=t.font})).ToArray();
    }
    static void Verify(FontSample[] fonts,string phase){foreach(var f in fonts)f.Verify(phase);}
    public static void Run()
    {
        Check(Application.isPlaying,"Play Mode required");if(running)return;running=true;
        Directory.CreateDirectory(Output);observations.Clear();File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("StackFanFontValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(ui));
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        string language=GamePreferences.Language;float scale=GamePreferences.TextScale;
        var notice=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=notice!=null&&notice.enabled;if(notice!=null)notice.enabled=false;
        var it=Validate(ui);
        try{
            while(true){object next;try{if(!it.MoveNext())break;next=it.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS: first construction, next frame, forced preference scan, 0.6s periodic scan, two View/Return cycles per request; three languages x three scales x fifteen archived choice consumers; legacy/TMP size and font identity retained. Fixed label factory applies requested size immediately. Compact 1/3/7/12 fan preserves full 240x335 cards, ordering, bounded angles, and all card corners inside viewport. Extended counts are display adapters, not new engine effect tests.");
        }finally{File.WriteAllLines(Output+"/font-observations.tsv",observations);running=false;ui.RestoreBattlefieldWindows();GamePreferences.SetScale(scale);GamePreferences.SetLanguage(language);if(notice!=null)notice.enabled=enabled;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){
            GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale readiness");
            foreach(float scale in new[]{.85f,1f,1.3f}){
                GamePreferences.SetScale(scale);yield return null;
                var probe=new GameObject("FontFactoryProbe",typeof(RectTransform));probe.transform.SetParent(GameObject.Find("Canvas").transform,false);
                var label=IntegratedUiUtility.Text(probe.transform,"FactoryText","字体 / Font / 文字",18,Color.white);
                IntegratedUiUtility.Place(label.rectTransform,Vector2.zero,new Vector2(400,60));
                Check(label.fontSize==Mathf.RoundToInt(18*scale),"First-frame legacy factory scale");PreferenceText.SetSize(label,12);Check(label.fontSize==Mathf.RoundToInt(12*scale),"Intentional base size change");
                var symbols=CardSymbolText.Create(probe.transform,"FactorySymbols",20,Color.white);symbols.text=CardSymbolText.Format("{2}{U} 字体 Font 文字");symbols.rectTransform.sizeDelta=new Vector2(400,70);
                Check(Mathf.Abs(symbols.fontSize-20*scale)<.01f&&symbols.font==GamePreferences.TmpFont,"First-frame symbol factory locale/scale");GamePreferences.BindScene();symbols.ForceMeshUpdate();Check(symbols.textInfo.characterInfo.Count(c=>c.elementType==TMP_TextElementType.Sprite)>=2,"Symbol factory lost mana");
                Check(label.fontSize==Mathf.RoundToInt(12*scale)&&Mathf.Abs(symbols.fontSize-20*scale)<.01f,"Factory scan rebased size");UnityEngine.Object.Destroy(probe);
                foreach(string name in new[]{"modes","selected-modes","mandatory-modes","kicker","evoke","cards","piles","allocation","order","names","generic","number","dungeon","value","scry"}){
                    // Flush the previous request's deferred Destroy before
                    // sampling the new window's very first frame.
                    var v=Read(Input+name+".json");ui.Present(null,v.seat,false,null,null,"");yield return null;ui.Present(v,v.seat,true,_=>{},null,"");
                    var fonts=Capture(ui);Check(fonts.Length>0,"No window fonts "+name);Verify(fonts,"construction "+name);
                    GamePreferences.BindScene();Verify(fonts,"forced scan "+name);
                    yield return null;Verify(fonts,"next frame "+name);
                    float end=Time.realtimeSinceStartup+.6f;while(Time.realtimeSinceStartup<end){Verify(fonts,"periodic scan "+name);yield return null;}
                    for(int toggle=0;toggle<2;toggle++){
                        ui.ViewBattlefield();Verify(fonts,"hide immediate "+name);yield return null;Verify(fonts,"hide next frame "+name);
                        GamePreferences.BindScene();Verify(fonts,"hidden scan "+name);ui.RestoreBattlefieldWindows();Verify(fonts,"return immediate "+name);yield return null;Verify(fonts,"return next frame "+name);
                    }
                    observations.Add(locale+"\t"+scale+"\t"+name+"\t"+fonts.Length+"\tconstruction/next-frame/scan/hide/return stable");
                }
            }
        }
        GamePreferences.SetLanguage("zh-CN");while(!GamePreferences.Ready)yield return null;GamePreferences.SetScale(1.3f);
        var native=Read("Tools/Validation/Reports/StackInteractions/spells-three.json");
        foreach(int count in new[]{1,3,7,12}){
            var v=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(native));
            v.stackItems=Enumerable.Range(0,count).Select(i=>{var e=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(native.stackItems[i%native.stackItems.Length]));e.id=70000+i;return e;}).ToArray();v.stack=v.stackItems.Select(e=>e.description).ToArray();v.stackCount=count;
            ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,false,null,null,"");yield return new WaitForSecondsRealtime(.9f);Canvas.ForceUpdateCanvases();
            var viewport=(RectTransform)GameObject.Find("Canvas").transform.Find("StackColumn/Viewport");var content=viewport.GetComponent<ScrollRect>().content;
            Check(viewport.rect.width<=428.1f,"Fan too wide");Check(content.rect.height<=428.1f,"Fan grows vertically");
            float previous=-1;
            for(int i=0;i<count;i++){
                var row=(RectTransform)content.Find("StackEntry_"+i);var tile=(RectTransform)row.Find("StackCard_"+i);float angle=row.localEulerAngles.z;
                Check(Vector2.Distance(tile.sizeDelta,new Vector2(240,335))<.1f,"Fan shrinks cards");Check(angle>=previous-.01f&&angle<=15.01f,"Invalid fan angle");previous=angle;
                Check(i!=0||angle<.01f&&row.GetSiblingIndex()==content.childCount-1,"Front/order changed");
                var corners=new Vector3[4];tile.GetWorldCorners(corners);foreach(var corner in corners){var p=(Vector2)viewport.InverseTransformPoint(corner);Check(viewport.rect.Contains(p),"Fan corner clipped "+count+"/"+i+" "+p+" viewport "+viewport.rect);}
            }
            ScreenCapture.CaptureScreenshot(Output+"/zh-CN-fan-"+count+".png");yield return new WaitForEndOfFrame();observations.Add("fan\t"+count+"\tall corners visible; native order retained");
        }
    }
}
#endif
