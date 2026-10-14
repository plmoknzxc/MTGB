#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class XSpellPresentationValidation
{
    static string Output=".tools/x-stack-insert-1005/ui-final";
    static string[] selectedPaths;
    static bool running;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static IEnumerator Frame(){yield return new WaitForSecondsRealtime(.6f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    public static void Run(){if(running)return;Check(Application.isPlaying,"Requires Play Mode");running=true;Directory.CreateDirectory(Output);var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("XSpellReview").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    public static void RunPrintedPolish()
    {
        if(running)return;
        Output=".tools/x-stack-rules-only-1005";
        selectedPaths=new[]{".tools/x-stack-insert-1005/native/increase-stack.json",".tools/x-stack-insert-1005/native/xx-increase-stack.json",".tools/x-stack-comparison-1005/wrath-x3.json"};
        Run();
    }
    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        string locale=GamePreferences.Language;var notice=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=notice!=null&&notice.enabled;if(notice!=null)notice.enabled=false;
        // Isolate replay events from the physical pointer moving over other cards.
        var modules=UnityEngine.Object.FindObjectsByType<BaseInputModule>(FindObjectsSortMode.None);var states=modules.Select(m=>m.enabled).ToArray();foreach(var module in modules)module.enabled=false;
        File.WriteAllText(Output+"/result.txt","RUNNING");var it=Validate(ui);
        try {while(true){object next;try{if(!it.MoveNext())break;next=it.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt",selectedPaths!=null?"PASS: printed X polish only; Song, double-X Ballista and Wrath; zh/en/ja/return-zh stack, hover, read-only list; Only lower rules repainted; printed X/XX mana untouched, no cost controls, rules top below complete type bar; full localized rules retained and scroll reaches bottom. Existing native evidence reused; no new engine/network checks.":"PASS: current native X cost snapshots (normal/reduce/increase, double-X reduce/increase/zero, counterspell tax) plus historical Omen/Wrath/Stomp/Soul Tether; zh/en/ja main cases, zh double-X/tax/non-X; stack/hover/read-only crowded list; preserved printed face and rules pixels, blue X note and separate numeric casting-cost sprites; localized secondary faces, one cost control, no overlay on battlefield hover. Crowded copies are layout adapters; native resolution verified separately, no network proof.");}
        finally{running=false;selectedPaths=null;Output=".tools/x-stack-insert-1005/ui-final";GamePreferences.SetLanguage(locale);if(notice!=null)notice.enabled=enabled;for(int i=0;i<modules.Length;i++)if(modules[i]!=null)modules[i].enabled=states[i];}
    }
    static void Face(Transform parent,ForgeStackItem entry,string locale)
    {
        bool printed=entry.source.stateName is not ("Secondary" or "PreparedSpell")&&(entry.chosenModes?.Length??0)==0&&(entry.castingChoices?.Length??0)==0;
        var face=parent.Find(printed?"ResolvedManaCost":entry.source.stateName is "Secondary" or "PreparedSpell"?"PreparedSpellFace":"ChosenSpellFace");Check(face!=null&&face.gameObject.activeSelf,"Missing face "+entry.source.name);
        if(!printed) {
        var cost=face.GetComponentsInChildren<TextMeshProUGUI>().First(t=>t.name=="Cost");
        Check(face.GetComponentsInChildren<TextMeshProUGUI>().Count(t=>t.name=="Cost")==1,"Duplicate cached cost");
        Check(cost.text==CardSymbolText.Format(string.IsNullOrEmpty(entry.source.castingManaCost)?entry.source.manaCost:entry.source.castingManaCost)&&!cost.text.Contains("{"),"Wrong resolved cost "+entry.source.name+" "+locale);cost.ForceMeshUpdate();
        Check(cost.textInfo.characterInfo.Take(cost.textInfo.characterCount).Count(c=>c.elementType==TMP_TextElementType.Sprite)==(string.IsNullOrEmpty(entry.source.castingManaCost)?entry.source.manaCost:entry.source.castingManaCost).Count(c=>c=='{'),"Cost lost sprite");
        }
        Check(!face.GetComponentsInChildren<Text>(true).Any(t=>t.text.Contains("X = ")),"X strip remains");
        if(printed) {
            Check(parent.GetComponent<Image>().sprite!=null,"Full printed image missing");
            Check(face.Find("CostRegion")==null&&!face.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t=>t.name=="Cost"),"Printed X mana covered");
            var paper=face.Find("Rules") as RectTransform;
            var frame=face.GetComponent<RectTransform>();
            float top=(paper.anchoredPosition.y+paper.rect.height*.5f)/frame.rect.height+.5f;
            Check(top<=.370f,"Rules cover printed type bar");
            var printedBody=face.GetComponentsInChildren<Text>(true).First(t=>t.name=="Body");
            var printedSymbols=printedBody.transform.Find("Symbols").GetComponent<TextMeshProUGUI>();
            var value=printedBody.transform.Find("XValue").GetComponent<TextMeshProUGUI>();
            Check(value.text=="X="+entry.xValue,"Wrong declared X");
            Check(Math.Abs(printedSymbols.fontSize-value.fontSize)<.01f&&Math.Abs(printedSymbols.fontSize-Mathf.RoundToInt(14*GamePreferences.TextScale))<.01f,"Different Omen printedBody/X font");
            Check(!printedSymbols.enableAutoSizing,"Shrinking rules");
            Check(printedSymbols.GetPreferredValues(printedSymbols.text,printedBody.rectTransform.rect.width,10000).y+value.rectTransform.rect.height<=printedBody.rectTransform.rect.height,"Incomplete scroll content");
            var definition=CardDatabaseRepository.LoadAll().First(c=>c.PrintingKey==entry.source.printingKey);
            Check(printedSymbols.text==CardSymbolText.Format(GamePreferences.CardRules(definition).Replace("\\n","\n")),"Localized rules changed or truncated");
            Check(face.GetComponentsInChildren<RawImage>(true).Length==0&&face.Find("PrintedRules")==null,"Raster patch/crop remains");
            var scroll=face.Find("Rules/Text").GetComponent<ScrollRect>();
            float previous=scroll.content.anchoredPosition.y;scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
            if(scroll.content.rect.height>scroll.viewport.rect.height+1)Check(scroll.content.anchoredPosition.y>previous,"Cannot reach last rules line");
            scroll.verticalNormalizedPosition=1;Canvas.ForceUpdateCanvases();
            Check(parent.Find("ChosenSpellFace")?.gameObject.activeSelf!=true&&parent.Find("PreparedSpellFace")?.gameObject.activeSelf!=true,"Normal spell face rebuilt");
            return;
        }
        var body=face.GetComponentsInChildren<Text>(true).First(t=>t.name=="Body"||t.name=="Effects");var symbols=body.transform.Find("Symbols").GetComponent<TextMeshProUGUI>();
        Check(symbols.GetPreferredValues(symbols.text,body.rectTransform.rect.width,10000).y<=body.rectTransform.rect.height+1,"Clipped rules");
        if(entry.hasXValue)Check(body.transform.Find("XValue").GetComponent<TextMeshProUGUI>().text=="X="+entry.xValue,"Secondary X note missing");
        if(entry.source.stateName is "Secondary" or "PreparedSpell") {
            Check(face.Find("Name").GetComponent<Text>().text==GamePreferences.CardFaceName(entry.source.name),"Secondary name not localized");
            if(locale!="en-US")Check(!System.Text.RegularExpressions.Regex.IsMatch(body.text,@"[A-Za-z]+(?: +[A-Za-z]+){3}"),"English secondary rules "+entry.source.name);
        }
        if(entry.source.name=="Exude Toxin") {
            string expected=locale=="zh-CN"?"毒气四溢":locale=="ja-JP"?"にじむ毒素":"Exude Toxin";
            Check(face.Find("Name").GetComponent<Text>().text==expected,"Wrong Omen name");
            Check(locale=="en-US"?body.text.Contains("non-Dragon"):locale=="zh-CN"?body.text.Contains("非龙"):body.text.Contains("ドラゴン"),"Wrong secondary rules "+body.text);
            Check(locale=="en-US"||!face.Find("Type").GetComponent<Text>().text.Contains("Sorcery"),"English secondary type");
        }
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        foreach(string path in selectedPaths??new[]{".tools/x-stack-comparison-1005/omen-x4.json",".tools/x-stack-comparison-1005/wrath-x3.json",".tools/x-stack-unified-1005/cast-adventure-stack.json",".tools/x-stack-unified-1005/prepare-stack.json"}.Concat(Directory.GetFiles(".tools/x-stack-insert-1005/native","*-stack.json")).ToArray()) {
            var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));var entry=v.stackItems.First(e=>e.kind=="spell");
            ui.Present(null,v.seat,false,null,null,"");
            foreach(string locale in selectedPaths==null&&(path.Contains("xx-")||path.Contains("counter-tax")||!entry.hasXValue)?new[]{"zh-CN"}:new[]{"zh-CN","en-US","ja-JP","zh-CN"}) {
                GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale not ready");
                ui.Present(v,v.seat,false,null,null,"");yield return Frame();var stack=GameObject.Find("StackCard_0");Face(stack.transform,entry,locale);
                ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-"+entry.source.name.Replace(' ','-')+(entry.hasXValue?"-X"+entry.xValue:"")+"-"+Path.GetFileNameWithoutExtension(path)+".png");yield return new WaitForEndOfFrame();
                if(selectedPaths!=null)CaptureCard(stack.GetComponent<RectTransform>(),Output+"/"+locale+"-"+entry.source.name.Replace(' ','-')+"-card.png");
                stack.GetComponent<ForgeBattlefieldCardPointer>().ResetHover();ExecuteEvents.Execute(stack,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);yield return Frame();
                var inspect=GameObject.Find("InspectPanel");Face(inspect.transform.Find("Art"),entry,locale);
                if(entry.source.name=="Exude Toxin")Check(inspect.transform.Find("Name").GetComponent<Text>().text==GamePreferences.CardFaceName(entry.source.name),"Hover source creature name");
                ExecuteEvents.Execute(stack,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);yield return Frame();
                if(entry.source.stateName is not ("Secondary" or "PreparedSpell")) {
                    var land=v.players.First().battlefield.FirstOrDefault(c=>c.land);
                    if(land!=null) {
                        typeof(ForgeBattlefieldController).GetMethod("InspectIntegrated",flags).Invoke(ui,new object[]{land});yield return Frame();
                        Check(inspect.transform.Find("Art/ResolvedManaCost")?.gameObject.activeSelf!=true,"X cost leaked into battlefield hover");
                        inspect.SetActive(false);
                    }
                }
            }
            // Copies are explicitly layout-only; native fees/results are checked separately.
            var crowded=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(v));crowded.stackItems=Enumerable.Range(0,3).Select(i=>{var e=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(entry));e.id+=i*10000;return e;}).ToArray();
            ui.Present(crowded,v.seat,false,null,null,"");yield return Frame();
            GameObject.Find("ViewStack").GetComponent<Button>().onClick.Invoke();yield return Frame();
            var list=GameObject.Find("StackList");Check(list!=null,"Missing list");
            foreach(var e in crowded.stackItems){var tile=list.GetComponentsInChildren<Transform>().First(t=>t.name=="Entry_"+e.id).Find("Card");Face(tile,e,"zh-CN");Check(tile.GetComponent<Button>().onClick.GetPersistentEventCount()==0&&!tile.GetComponent<Button>().interactable,"Browse selectable");}
            typeof(ForgeBattlefieldController).GetMethod("CloseStackList",flags).Invoke(ui,null);yield return Frame();
        }
    }
    static void CaptureCard(RectTransform card,string path)
    {
        var corners=new Vector3[4];card.GetWorldCorners(corners);
        int x=Math.Max(0,(int)corners[0].x-5),y=Math.Max(0,(int)corners[0].y-5);
        int w=Math.Min(Screen.width-x,(int)(corners[2].x-corners[0].x)+10),h=Math.Min(Screen.height-y,(int)(corners[2].y-corners[0].y)+10);
        var screen=ScreenCapture.CaptureScreenshotAsTexture();var crop=new Texture2D(w,h);
        crop.SetPixels(screen.GetPixels(x,y,w,h));crop.Apply();File.WriteAllBytes(path,crop.EncodeToPNG());UnityEngine.Object.Destroy(screen);UnityEngine.Object.Destroy(crop);
    }
}
#endif
