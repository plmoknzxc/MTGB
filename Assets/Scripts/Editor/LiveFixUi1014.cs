#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class LiveFixUi1014
{
    const string Out=".tools/live-fixes-1014";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    sealed class Host:MonoBehaviour{}
    public static bool Running;static List<string> checks=new();
    static object Field(object o,string name)=>o.GetType().GetField(name,Hidden).GetValue(o);
    static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Hidden).Invoke(o,args);
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Out+"/"+name+".json"));
    static void Capture(string name){var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Out+"/"+name+".png",texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);}
    public static void Run(){if(Running||!Application.isPlaying)throw new Exception("Idle Play mode required");checks.Clear();Running=true;new GameObject("LiveFixUiProbe").AddComponent<Host>().StartCoroutine(Guard(Exercise()));}
    static IEnumerator Guard(IEnumerator routine)
    {
        while(true){object next;bool more;try{more=routine.MoveNext();next=more?routine.Current:null;}catch(Exception e){checks.Add("FAIL "+e);more=false;next=null;}if(!more)break;yield return next;}
        GamePreferences.SetScale(1);File.WriteAllLines(Out+"/ui-result.txt",checks);Running=false;
    }
    static IEnumerator Exercise()
    {
        while(!GamePreferences.Ready)yield return null;
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("LiveFixBoard").AddComponent<ForgeBattlefieldController>();ui.enabled=false;ui.GetType().GetField("service",Hidden).SetValue(ui,null);
        foreach(float scale in new[]{1f,1.3f}){
            GamePreferences.SetScale(scale);var chosen=new List<int>();var color=Read("color-view");ui.Present(color,color.seat,true,i=>chosen.Add(i),null,"");yield return new WaitForSecondsRealtime(.4f);
            var picker=(RectTransform)Field(ui,"cardSelectionOverlay");var menu=(RectTransform)Field(ui,"landChoiceMenu");
            Check(picker==null||!picker.gameObject.activeSelf,"Mana production has no card confirmation modal "+scale);
            Check(menu!=null&&menu.rect.width<260&&menu.rect.height<150,"Mana production is a compact chip menu "+scale);
            var buttons=menu.GetComponentsInChildren<Button>();Check(buttons.Length==2&&menu.Find("Confirm")==null,"Exactly two color choices, no extra confirmation "+scale);
            Check(!ui.TryCancelPresentedChoice()&&chosen.Count==0,"Native Red ordinal cannot be cancelled/right-clicked "+scale);
            int blue=Array.FindIndex(color.request.options,o=>o.label=="Blue");var blueButton=buttons.First(b=>b.name=="Option_"+blue);blueButton.onClick.Invoke();Check(chosen.SequenceEqual(new[]{blue}),"Single color click replies with exact Blue option "+scale);
            ui.Present(color,color.seat,true,i=>chosen.Add(i),null,"");yield return new WaitForEndOfFrame();if(scale==1)Capture("mana-color");
            var payment=Read("payment-view");chosen.Clear();ui.Present(payment,payment.seat,true,i=>chosen.Add(i),null,"");yield return null;
            var pay=(RectTransform)Field(ui,"manaPaymentActions");var next=pay.Find("AutoPayment").GetComponent<Button>();var cancel=pay.Find("CancelPayment").GetComponent<Button>();
            Check(next.GetComponentInChildren<Text>().text=="下一步"&&next.transform.localPosition.x<cancel.transform.localPosition.x,"Payment Next is left and Cancel is right "+scale);
            Check(chosen.Count==0,"Payment presentation sends no automatic reply "+scale);
            var pool=(RectTransform)Field(ui,"manaPoolRow");Check(pool.anchoredPosition.x-pool.rect.width*.5f>=20&&pool.anchoredPosition.x+pool.rect.width*.5f<340,"Mana pool stays in player information column "+scale);
            Check(pool.anchoredPosition.y>=230,"Mana pool moved clear of previous land overlap "+scale);
            yield return new WaitForEndOfFrame();if(scale==1)Capture("payment-before-next");
            next.onClick.Invoke();Check(chosen.SequenceEqual(new[]{Array.FindIndex(payment.request.options,o=>o.id=="ok")}),"Next submits native mana payment once "+scale);
            chosen.Clear();ui.Present(payment,payment.seat,true,i=>chosen.Add(i),null,"");Check(ui.TryCancelPresentedChoice()&&chosen.SequenceEqual(new[]{Array.FindIndex(payment.request.options,o=>o.id=="cancel")}),"Payment right-click route submits real native cancel "+scale);
            var food=Read("food-view");ui.Present(food,food.seat,false,null,null,"");yield return null;
            var items=(IDictionary)Field(ui,"arenaCards");var roots=items.Values.Cast<object>().Select(x=>(RectTransform)x.GetType().GetField("root").GetValue(x)).ToArray();
            var foodIds=food.players.SelectMany(p=>p.battlefield??Array.Empty<ForgeCardView>()).Where(c=>c.name=="Food Token").Select(c=>"Card_"+c.id).ToArray();var visible=roots.Where(r=>foodIds.Contains(r.name)).ToArray();
            Check(visible.Length==1&&visible[0].GetComponentsInChildren<Text>().Any(t=>t.text=="×2"),"Actual two food arts render as one group with count two "+scale);
            Check(roots.All(r=>r.GetComponentsInChildren<Text>().All(t=>t.name!="PaymentHint")),"No fee-warning text overlays card art "+scale);
            yield return new WaitForEndOfFrame();if(scale==1)Capture("food-group");
            var wrapper=JsonUtility.FromJson<CardArray>("{\"cards\":"+File.ReadAllText(Out+"/sagas.json")+"}");Call(ui,"InspectIntegrated",wrapper.cards[0]);yield return null;
            var surface=(RectTransform)Field(ui,"surface");var rules=surface.Find("InspectPanel/Rules").GetComponent<Text>();
            Check(!System.Text.RegularExpressions.Regex.IsMatch(rules.text,@"\b(Add|Create|Construct|Chapter|Animate|Tutor)\b"),"Actual saga gained abilities and status have no raw English recipe "+scale);
            Check(rules.text.Contains("组构体")&&rules.text.Contains("章节数量"),"Saga keeps gained effect and chapter information "+scale);
            yield return new WaitForEndOfFrame();if(scale==1)Capture("saga-detail");
            string cost=(string)Call(ui,"NativeCostEffectText","As an additional cost to cast this spell, exile a green card from your hand.");Check(cost.Contains("绿色")&&!cost.Contains("exile"),"Generated single-green-card cost uses shared vocabulary "+scale);
            var before=food.phase;food.awaitingInput=false;food.request=null;food.phase="MAIN1";ui.Present(food,food.seat,false,null,null,"");yield return new WaitForSecondsRealtime(.3f);
            var bar=surface.Find("CornerCluster/ControlRow/PhaseTrack/Phase_2").GetComponent<Image>();var old=bar.color;food.phase="COMBAT_BEGIN";ui.Present(food,food.seat,false,null,null,"");yield return null;Check(bar.color!=old,"Combat phase bar starts a continuous color transition "+scale);var middle=bar.color;
            ui.Present(food,food.seat,false,null,null,"");yield return new WaitForSecondsRealtime(.08f);Check(bar.color!=middle,"Repeated phase snapshot does not reset transition "+scale);
        }
    }
    [Serializable] sealed class CardArray{public ForgeCardView[] cards;}
}
#endif
