#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Replays authenticated snapshots from the real Forge acceptance tests through the actual UI.
public static class AdvancedInteractionPresentationValidation
{
    const string Output=".tools/advanced-interactions";
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static ForgeSeatView Read(string relative)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/"+relative+".json"));
    public static void Run()
    {
        Check(Application.isPlaying,"Enter IntegratedBattlefield Play Mode first.");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("AdvancedPresentationValidation").AddComponent<ForgeBattlefieldController>();
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/presentation-result.txt","RUNNING");
        ui.StartCoroutine(Validate(ui));
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        int submitted=-1,calls=0;
        Action<ForgeSeatView> show=v=>ui.Present(v,v.seat,true,i=>{submitted=i;calls++;},null,"");
        var damage=Read("AdvancedInteractions/damage-assign");show(damage);
        yield return new WaitForSecondsRealtime(.5f);
        var panel=GameObject.Find("DamageAllocation");Check(panel!=null,"Allocation UI missing.");
        Check(panel.GetComponentsInChildren<Text>().Count(t=>t.name=="Amount"&&t.text=="2")==3,"Damage amounts not displayed.");
        var recipient=panel.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Recipient_0");
        Check(!recipient.Find("Plus").GetComponent<Button>().interactable,"Illegal damage increase is enabled.");
        ScreenCapture.CaptureScreenshot(Output+"/damage.png");yield return new WaitForEndOfFrame();
        recipient.Find("Minus").GetComponent<Button>().onClick.Invoke();
        Check(damage.request.options[submitted].id=="amount-sub:0"&&calls==1,"Allocation option mapping failed.");
        damage.request.requestId++;show(damage);yield return null;
        Check(GameObject.Find("DamageAllocation")==panel&&recipient!=null,"Allocation redraw replaced the current controls.");

        var blocking=Read("AdvancedInteractions/damage-blocking");show(blocking);yield return new WaitForSecondsRealtime(.4f);
        Check(GameObject.Find("ForgeChoices")==null,"Generic choice panel covers blocking.");
        var block=blocking.request.options.First(o=>o.id.StartsWith("block:")&&!o.selected);
        GameObject.Find("Card_"+block.defenderId).GetComponent<Button>().onClick.Invoke();yield return null;
        Check(GameObject.Find("BlockHint")!=null,"Blocker selection lacks destination hint.");
        ScreenCapture.CaptureScreenshot(Output+"/blocking.png");yield return new WaitForEndOfFrame();
        GameObject.Find("Card_"+block.attackerId).GetComponent<Button>().onClick.Invoke();
        Check(blocking.request.options[submitted].id==block.id&&calls==2,"Blocker-to-attacker click mapped to wrong pair.");

        var startOrder=Read("StackInteractions/triggers-order-start");show(startOrder);yield return new WaitForSecondsRealtime(.4f);
        var firstOrderOption=startOrder.request.options.First(o=>o.id.StartsWith("order:"));
        var firstOrderCard=GameObject.Find("OrderedAbility_"+firstOrderOption.ability.id);
        firstOrderCard.GetComponent<Button>().onClick.Invoke();
        Check(startOrder.request.options[submitted].id==firstOrderOption.id,"Order selection mapping failed.");calls--;
        var order=Read("StackInteractions/triggers-order");show(order);yield return new WaitForSecondsRealtime(.5f);
        panel=GameObject.Find("AbilityOrder");Check(panel!=null,"Ability ordering UI missing.");
        Check(GameObject.Find(firstOrderCard.name)==firstOrderCard,"Ordering replaces unchanged card objects.");
        Check(panel.GetComponentsInChildren<Text>().Count(t=>t.name=="Order"&&!string.IsNullOrEmpty(t.text))==2,"Ordered ability positions missing.");
        ScreenCapture.CaptureScreenshot(Output+"/order.png");yield return new WaitForEndOfFrame();
        panel.transform.Find("ResetOrder").GetComponent<Button>().onClick.Invoke();
        Check(order.request.options[submitted].id=="reset-order"&&calls==3,"Order reset mapping failed.");

        var target=Read("StackInteractions/triggers-target-2");show(target);yield return new WaitForSecondsRealtime(.5f);
        var origin=GameObject.Find("TargetOrigin");Check(origin!=null,"Stack target source missing.");
        var confirm=origin.transform.Find("ConfirmTarget").GetComponent<Button>();
        Check(confirm.gameObject.activeInHierarchy&&confirm.interactable,"Selected stack target cannot be confirmed.");
        Check(origin.transform.Find("ResetTarget").gameObject.activeInHierarchy,"Selected stack target cannot be reselected.");
        ScreenCapture.CaptureScreenshot(Output+"/stack-target.png");yield return new WaitForEndOfFrame();
        confirm.onClick.Invoke();Check(target.request.options[submitted].id=="ok"&&calls==4,"Stack target confirmation mapping failed.");

        show(Read("AdvancedInteractions/storm-copies"));yield return new WaitForSecondsRealtime(.5f);
        ScreenCapture.CaptureScreenshot(Output+"/storm.png");yield return new WaitForEndOfFrame();
        show(Read("AdvancedInteractions/battle-front-trigger"));yield return new WaitForSecondsRealtime(.5f);
        show(Read("AdvancedInteractions/battle-back-stack"));yield return new WaitForSecondsRealtime(.06f);
        var flip=GameObject.Find("StackArrival");Check(flip!=null&&flip.transform.localScale.x<1,"Transformed spell does not reveal its back face.");
        yield return new WaitForSecondsRealtime(.8f);
        ScreenCapture.CaptureScreenshot(Output+"/battle-back-stack.png");yield return new WaitForEndOfFrame();
        var back=Read("AdvancedInteractions/battle-backface");show(back);yield return new WaitForSecondsRealtime(.5f);
        var transformed=back.players.SelectMany(p=>p.battlefield).First(c=>c.backFace);
        var root=GameObject.Find("Card_"+transformed.id);
        Check(root!=null && root.GetComponentsInChildren<Text>().Any(t=>t.name=="Value"&&t.text==transformed.power+"/"+transformed.toughness),"Transformed battle lacks creature stats.");
        ScreenCapture.CaptureScreenshot(Output+"/battle-backface.png");yield return new WaitForEndOfFrame();
        File.WriteAllText(Output+"/presentation-result.txt","PASS: real-engine snapshots rendered; legal allocation controls and stable instances; blocker/destination clicks; ordered ability cards/reset; stack target confirm/reset; storm copies; transformed battle creature stats. Screenshots verify presentation, not video animation equivalence.");
        Debug.Log("AdvancedInteractionPresentationValidation PASS");
    }
}
#endif
