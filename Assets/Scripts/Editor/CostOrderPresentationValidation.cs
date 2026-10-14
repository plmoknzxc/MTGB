#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CostOrderPresentationValidation
{
    const string Base=".tools/cost-order-format-1009";
    public static bool Running{get;private set;}
    public static string Output{get;private set;}
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;File.AppendAllText(Output+"/checks.txt",label+"\n");}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Base+"/ui-inputs/"+name+".json"));
    public static void Run(){
        if(Running||!Application.isPlaying)throw new Exception("Play required/no duplicate run");
        Running=true;checks=0;Output=Base+"/ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");
        Directory.CreateDirectory(Output+"/sources");File.WriteAllText(Base+"/ui-current.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");
        foreach(var p in new[]{"Assets/Scripts/Editor/CostOrderPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.CostOrder.cs","Assets/Scripts/UI/ForgeBattlefieldController.LandChoices.cs"})File.Copy(p,Output+"/sources/"+Path.GetFileName(p));
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("CostOrderValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator work){var tasks=new Stack<IEnumerator>();tasks.Push(work);
        try{while(tasks.Count>0){object next;try{var task=tasks.Peek();if(!task.MoveNext()){tasks.Pop();continue;}next=task.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}if(next is IEnumerator child)tasks.Push(child);else yield return next;}
            File.WriteAllText(Output+"/result.txt","PASS_BOUNDED: "+checks+" checks; 9 actual native cost-order snapshots/three sources, zh-CN/en-US/ja-JP normal+1.3 text. Original option indices/order/confirm/reset gates, mana sprite, complete measured rows, real rays/stale/duplicate/read-only, view-return/font stability/priority cleanup. Synthetic long/custom scroll adapter separately labeled; no new native rules/network PASS.");}
        finally{while(tasks.Count>0)(tasks.Pop() as IDisposable)?.Dispose();Running=false;}
    }
    static IEnumerator Frame(){yield return new WaitForEndOfFrame();yield return null;Canvas.ForceUpdateCanvases();}
    static void Click(Button button){var rect=(RectTransform)button.transform;var p=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(button.interactable&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Actual topmost ray "+button.name);ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,p,ExecuteEvents.pointerClickHandler);}
    static IEnumerator Validate(ForgeBattlefieldController ui){
        var locale=GamePreferences.Language;var enabled=ui.enabled;ui.enabled=false;float scale=GamePreferences.TextScale;
        var module=EventSystem.current.currentInputModule;bool input=module!=null&&module.enabled;if(module!=null)module.enabled=false;
        var banners=UnityEngine.Object.FindObjectsByType<ForgeBattlefieldMigrationNotice>(FindObjectsSortMode.None);var flags=banners.Select(b=>b.enabled).ToArray();foreach(var b in banners)b.enabled=false;
        try{
            float deadline=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready,"Initial preferences ready");
            foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
                GamePreferences.SetLanguage(language);deadline=Time.realtimeSinceStartup+15;while((!GamePreferences.Ready||GamePreferences.Language!=language)&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language==language,"Exact locale "+language);
                foreach(float size in new[]{1f,1.3f}){
                    GamePreferences.SetScale(size);
                    foreach(string test in new[]{"exile-before-mana","sacrifice-before-mana","return-before-mana"})for(int state=0;state<=2;state++){
                        var v=Read(test+"-"+state);int calls=0,last=-1;
                        ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,true,i=>{calls++;last=i;},null,"");yield return Frame();
                        var panel=GameObject.Find("CardAbilityChoices");Check(panel!=null,"Native cost order panel "+test+state);
                        Check(((RectTransform)panel.transform).anchoredPosition==Vector2.zero,"Centered cost order");
                        Check(GameObject.Find("ForgeChoices")==null||!GameObject.Find("ForgeChoices").activeInHierarchy,"No duplicate fallback panel");
                        var title=panel.transform.Find("CostOrderTitle").GetComponent<Text>();Check(title.text==(language=="zh-CN"?"排列费用支付顺序":language=="en-US"?"Order cost payments":"コストの支払い順を選択"),"Localized exact title");
                        var rows=panel.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("CostOrderOption_")).ToArray();Check(rows.Length==v.request.options.Count(o=>o.id.StartsWith("select:")),"Exactly native option count");
                        foreach(var row in rows){int id=int.Parse(row.name.Substring("CostOrderOption_".Length));var option=v.request.options[id];var text=row.GetComponentInChildren<TextMeshProUGUI>();text.ForceMeshUpdate();
                            Check(!text.enableAutoSizing&&Math.Abs(text.fontSize-18*size)<.1f,"Fixed row font "+id);
                            Check(text.GetPreferredValues(text.text,text.rectTransform.rect.width,10000).y<=text.rectTransform.rect.height+1&&!text.isTextOverflowing,"Full row fits "+id);
                            Check(row.transform.Find("PaymentOrder").GetComponent<Text>().text==(option.selected?option.order.ToString():"—"),"Exact native payment rank "+id);
                            if(option.label=="{1}")Check(text.textInfo.characterInfo.Take(text.textInfo.characterCount).Count(c=>c.elementType==TMP_TextElementType.Sprite&&c.isVisible&&c.textElement is TMP_SpriteCharacter sprite&&sprite.name=="1")==1&&!text.text.Contains("{"),"Actual mana1 sprite");
                            else Check(text.text==(language=="zh-CN"?test=="exile-before-mana"?"从手牌中放逐两张绿色牌":test=="sacrifice-before-mana"?"牺牲两座山脉":"将你操控的两座海岛移回其拥有者手上":language=="ja-JP"?test=="exile-before-mana"?"手札から緑のカード２枚を追放する":test=="sacrifice-before-mana"?"山２つを生け贄に捧げる":"あなたがコントロールする島２つをオーナーの手札に戻す":option.label.Replace("Card.Green","green")),"Localized full original cost semantics");
                        }
                        var confirm=panel.transform.Find("CostOrderConfirm").GetComponent<Button>();var reset=panel.transform.Find("CostOrderReset").GetComponent<Button>();
                        Check(confirm.interactable==(ui.OptionIndex("confirm-cards")>=0)&&reset.interactable==(ui.OptionIndex("reset-cards")>=0),"Native confirm/reset presence gates");
                        var fonts=panel.GetComponentsInChildren<TMP_Text>().Select(t=>t.fontSize).ToArray();
                        if(state==0&&test=="return-before-mana"){
                            Click(GameObject.Find("ViewBattlefield").GetComponent<Button>());yield return Frame();var group=panel.GetComponent<CanvasGroup>();Check(group!=null&&group.alpha==0&&!group.blocksRaycasts,"View battlefield hides cost window");
                            Check(!ui.TrySubmitPresentedOption(0,v.request.requestId)&&calls==0,"Hidden window cannot submit");
                            Click(GameObject.Find("ReturnSelection").GetComponent<Button>());yield return Frame();Check(group.alpha==1&&group.blocksRaycasts,"Return restores cost window");
                            Check(fonts.SequenceEqual(panel.GetComponentsInChildren<TMP_Text>().Select(t=>t.fontSize)),"View return fixed fonts");
                            ScreenCapture.CaptureScreenshot(Output+"/"+language+"-"+size.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png");yield return Frame();
                        }
                        var target=state==2?confirm:state==1?reset:rows.Last();int index=state==2?ui.OptionIndex("confirm-cards"):state==1?ui.OptionIndex("reset-cards"):int.Parse(target.name.Substring("CostOrderOption_".Length));
                        Check(!ui.TrySubmitPresentedOption(index,v.request.requestId-1)&&calls==0,"Stale cost order rejected");Click(target);target.onClick.Invoke();Check(calls==1&&last==index,"Original cost index once");
                        ui.Present(v,v.seat,false,i=>calls++,null,"");yield return Frame();Check(panel.GetComponentsInChildren<Button>().All(b=>!b.interactable)&&!ui.TrySubmitPresentedOption(index,v.request.requestId)&&calls==1,"Read-only original choice guard");
                        ui.Present(Read(test+"-restored"),v.seat,true,i=>{},null,"");yield return Frame();Check(GameObject.Find("CostOrderTitle")==null,"Priority removes cost order");
                    }
                }
            }
            // Layout adapter only: unknown native text is preserved, long rows
            // scroll without hiding their end or the native confirmation.
            GamePreferences.SetScale(1.3f);var large=Read("return-before-mana-2");string raw=string.Concat(Enumerable.Repeat("Custom cost text retained in full. ",18));
            large.request.options[1].label=raw;int adapterCalls=0;ui.Present(null,large.seat,false,null,null,"");ui.Present(large,large.seat,true,_=>adapterCalls++,null,"");yield return Frame();
            var menu=GameObject.Find("CardAbilityChoices");var scroll=menu.GetComponentInChildren<ScrollRect>();var longRow=menu.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.text==raw);
            Check(scroll.content.rect.height>scroll.viewport.rect.height&&!longRow.isTextOverflowing,"ADAPTER long full text scrolls");scroll.verticalNormalizedPosition=0;yield return Frame();
            var corners=new Vector3[4];longRow.rectTransform.GetWorldCorners(corners);var viewport=scroll.viewport;var bottom=viewport.InverseTransformPoint(corners[0]);Check(bottom.y>=viewport.rect.yMin-1,"ADAPTER last text end reachable");
            ScreenCapture.CaptureScreenshot(Output+"/adapter-long.png");yield return Frame();Click(menu.transform.Find("CostOrderConfirm").GetComponent<Button>());Check(adapterCalls==1,"ADAPTER footer remains reachable");
            var ordinary=Read("return-before-mana-0");ordinary.request.message="Order cards";ui.Present(null,ordinary.seat,false,null,null,"");ui.Present(ordinary,ordinary.seat,true,_=>{},null,"");yield return Frame();Check(GameObject.Find("CostOrderTitle")==null,"Other string OrderCards retains previous route");
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;if(module!=null)module.enabled=input;GamePreferences.SetScale(scale);GamePreferences.SetLanguage(locale);for(int i=0;i<banners.Length;i++)if(banners[i]!=null)banners[i].enabled=flags[i];}
    }
}
#endif
