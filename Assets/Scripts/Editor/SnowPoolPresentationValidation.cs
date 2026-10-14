#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Native pool snapshots plus separately labelled twelve-count layout boundaries.
public static class SnowPoolPresentationValidation
{
    const string Base=".tools/snow-pool-1006/ui";
    public static string Output {get;private set;}
    [Serializable] class Result {public string folder;}
    static Transform Root=>GameObject.Find("Canvas").transform;
    static readonly string[] Symbols={"W","U","B","R","G","C"};
    static void Check(bool pass,string reason){if(!pass)throw new Exception(reason);}
    static ForgeSeatView Read(string name,string snapshot)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Path.Combine(JsonUtility.FromJson<Result>(File.ReadAllText(".tools/snow-pool-1006/native/"+name+"-result.json")).folder,snapshot+".json")));
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.16f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Click(Button b){Check(b.interactable,"Disabled "+b.name);var r=(RectTransform)b.transform;var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked ray "+b.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    static void Inspect(Transform row,int[] total,int[] snow)
    {
        for(int i=0;i<6;i++)foreach(bool s in new[]{false,true}) {
            var chip=row.Find("Pool_"+(s?"S_":"")+Symbols[i]);int expected=s?snow[i]:total[i]-snow[i];
            Check(chip.gameObject.activeSelf==(expected>0),"Visibility "+chip.name);
            if(expected==0)continue;
            Check(chip.Find("Count").GetComponent<Text>().text==expected.ToString(),"Double-counted "+chip.name);
            var glyph=chip.Find("Symbol").GetComponent<Text>();
            Check(glyph.font.HasCharacter(glyph.text[0])&&glyph.canvasRenderer.GetMesh()?.vertexCount>0,"No rendered glyph "+chip.name);
            Check(!glyph.resizeTextForBestFit&&glyph.font==Resources.Load<Font>("UI/ManaSymbols/ManaGlyphs"),"Font changed "+chip.name);
            Check(s?glyph.text=="\uE996":glyph.text!="\uE996","Wrong provenance symbol");
            Check(glyph.color==row.Find("Pool_"+Symbols[i]+"/Symbol").GetComponent<Text>().color,"Snow color lost");
            var r=(RectTransform)row;var p=(RectTransform)chip;
            Check(p.anchoredPosition.x-p.rect.width*.5f>=r.rect.xMin-.1f&&p.anchoredPosition.x+p.rect.width*.5f<=r.rect.xMax+.1f,"Horizontal overflow");
            Check(Mathf.Abs(p.anchoredPosition.y)+17<=r.rect.height*.5f+.1f,"Vertical overflow");
            Check(chip.GetComponent<Image>().color.a==0,"Mana acquired a tile");
        }
    }
    public static void Run(){Check(Application.isPlaying,"Play required");Output=Base+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);File.WriteAllText(Base+"/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("SnowPoolValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var e=Validate(ui);try{while(true){object next;try{if(!e.MoveNext())break;next=e.Current;}catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);File.Copy(Output+"/result.txt",Base+"/result.txt",true);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: three locales, four native scenarios, both seat viewpoints, 12 exact resource callbacks by ray; duplicate/stale/non-actor/pending guards; native split counts and cancel/refund snapshots; separate large-count and zero fixtures, fixed vector glyphs/no tile. Replay is not a live network assertion.");File.Copy(Output+"/result.txt",Base+"/result.txt",true);}finally{(e as IDisposable)?.Dispose();}}
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        string language=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;var module=EventSystem.current.currentInputModule;bool physical=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;
        try{
            foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}) {
                int revision=GamePreferences.Revision;bool change=GamePreferences.Language!=lang;GamePreferences.SetLanguage(lang);float until=Time.realtimeSinceStartup+15;while((!GamePreferences.Ready||change&&revision==GamePreferences.Revision)&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale unavailable");
                foreach(string name in new[]{"snow-green","snow-colorless","regular-colored","snow-colored"}) {
                    var v=Read(name,"payment-0");int actor=v.request.seat;var player=v.players.First(p=>p.seat==v.request.playerSeat);int calls=0,last=-1;
                    ui.Present(null,actor,false,null,null,"");ui.Present(v,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();
                    var pool=Root.Find("ManaPoolChips");Inspect(pool,player.manaPool,player.snowManaPool);
                    foreach(string symbol in Symbols)foreach(bool snow in new[]{false,true}) {
                        ui.Present(v,actor,true,i=>{calls++;last=i;},null,"");yield return Frame();
                        string id="mana:"+(snow?"S:":"")+symbol;var button=pool.Find("Pool_"+(snow?"S_":"")+symbol).GetComponent<Button>();
                        int count=calls;ui.Choose(ui.OptionIndex(id),v.request.requestId-1);Check(calls==count,"Stale payment");Click(button);button.onClick.Invoke();Check(calls==count+1&&last==ui.OptionIndex(id),"Wrong/duplicate callback "+id);
                    }
                    ui.Present(v,actor,false,i=>calls++,null,"");yield return Frame();Check(pool.GetComponentsInChildren<Button>(true).All(b=>!b.interactable),"Passive pool enabled");
                    v.inputPending=true;ui.Present(v,actor,true,i=>calls++,null,"");yield return Frame();Check(pool.GetComponentsInChildren<Button>(true).All(b=>!b.interactable),"Pending pool enabled");v.inputPending=false;
                    ui.Present(v,3-actor,false,i=>calls++,null,"");yield return Frame();var publicPool=Root.Find("PublicManaPool");Inspect(publicPool,player.manaPool,player.snowManaPool);Check(publicPool.GetComponentsInChildren<Button>(true).All(b=>!b.interactable),"Opponent pool enabled");
                    ui.Present(v,actor,true,null,null,"");yield return Frame();
                    if(lang=="zh-CN"&&name=="snow-green"){ScreenCapture.CaptureScreenshot(Output+"/native-twelve-pool.png");yield return Frame();}
                    var resolved=Read(name,"resolved");ui.Present(resolved,actor,true,null,null,"");yield return Frame();var paid=resolved.players.First(p=>p.seat==actor);Inspect(pool,paid.manaPool,paid.snowManaPool);
                }
                // Explicit layout-only fixture: both pools, three-digit counts.
                var full=Read("snow-green","payment-0");foreach(var p in full.players){p.manaPool=new[]{200,200,200,200,200,200};p.snowManaPool=new[]{100,100,100,100,100,100};}
                ui.Present(full,1,true,null,null,"");yield return Frame();foreach(var p in full.players)Inspect(Root.Find(p.seat==1?"ManaPoolChips":"PublicManaPool"),p.manaPool,p.snowManaPool);
                if(lang=="zh-CN"){ScreenCapture.CaptureScreenshot(Output+"/both-pools-large-counts.png");yield return Frame();}
                foreach(var p in full.players){p.manaPool=new int[6];p.snowManaPool=new int[6];}ui.Present(full,1,true,null,null,"");yield return Frame();Check(!Root.Find("ManaPoolChips").gameObject.activeSelf&&!Root.Find("PublicManaPool").gameObject.activeSelf,"Zero pool stayed visible");
            }
        }finally{ui.enabled=enabled;if(module!=null)module.enabled=physical;GamePreferences.SetLanguage(language);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=flags[i];}
    }
}
#endif
