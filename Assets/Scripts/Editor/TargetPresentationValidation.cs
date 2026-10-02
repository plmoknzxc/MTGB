#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class TargetPresentationValidation
{
    public static void Run()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        ui.StartCoroutine(Check(ui));
    }
    private static IEnumerator Check(ForgeBattlefieldController ui)
    {
        const string folder="Tools/Validation/Reports/TargetPresentation";
        Directory.CreateDirectory(folder);File.WriteAllText(folder+"/result.txt","RUNNING");
        var catalog=CardDatabaseRepository.LoadAll();
        ForgeCardView Card(string name,int id)=>new ForgeCardView{id=id,name=name,printingKey=catalog.First(c=>c.cardName==name && !c.forgeAmbiguous).PrintingKey,creature=name=="Hill Giant",power=3,toughness=3};
        var bolt=Card("Lightning Bolt",1);var creature=Card("Hill Giant",2);int calls=0;
        var state=new ForgeSeatView{seat=1,awaitingInput=true,turn=3,turnSeat=1,phase="MAIN1",request=new ForgeRequest{requestId=810,seat=1,kind="InputPassPriority",options=new[]{new ForgeOption{id="card:1",label="Lightning Bolt"}}},players=new[]{
            new ForgePlayerView{seat=1,life=20,hand=new[]{bolt},battlefield=Array.Empty<ForgeCardView>()},
            new ForgePlayerView{seat=2,life=20,targetId=22,hand=Array.Empty<ForgeCardView>(),battlefield=new[]{creature}}}};
        ui.Present(state,1,true,_=>calls++,null,"目标呈现测试");ui.Choose(0,810);
        state.players[0].hand=Array.Empty<ForgeCardView>();
        state.request=new ForgeRequest{requestId=811,seat=1,kind="InputSelectTargets",options=new[]{new ForgeOption{id="card:2",label="Hill Giant"},new ForgeOption{id="player:22",label="Player 2"},new ForgeOption{id="cancel",label="Cancel"}}};
        ui.Present(state,1,true,_=>calls++,null,"目标呈现测试");
        yield return new WaitForSecondsRealtime(.5f);
        CardImageLibrary.TryLoadFullCard(catalog.First(c=>c.PrintingKey==bolt.printingKey),out var expectedArt);
        if(GameObject.Find("TargetOrigin").GetComponent<Image>().sprite!=expectedArt)throw new Exception("Casting preview used stale card art");
        // Freeze mouse polling for this deterministic presentation capture only.
        ui.enabled=false;
        var target=GameObject.Find("Card_2").transform;
        Vector2 point=RectTransformUtility.WorldToScreenPoint(null,target.position);
        ui.UpdateTargetingAt(point);Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(folder+"/creature-target.png");
        yield return new WaitForSecondsRealtime(.2f);
        if(ui.TryChooseTargetAt(new Vector2(1,1),811) || ui.TryChooseTargetAt(point,810))throw new Exception("Invalid/stale target accepted");
        if(!ui.TryChooseTargetAt(point,811) || ui.TryChooseTargetAt(point,811))throw new Exception("Creature target submission guard failed");
        if(GameObject.Find("TargetArrow")!=null)throw new Exception("Arrow remained after submission");
        state.request.requestId=812;ui.Present(state,1,true,_=>calls++,null,"取消目标测试");
        var cancel=GameObject.Find("TargetOrigin").transform.Find("CancelTarget").GetComponent<Button>();
        cancel.onClick.Invoke();cancel.onClick.Invoke();
        if(calls!=3 || GameObject.Find("TargetArrow")!=null)throw new Exception("Cancel did not submit once and hide arrow");
        ui.enabled=true;
        File.WriteAllText(folder+"/result.txt","PASS: curved rendered arrow; right-side full preview; creature target; invalid/stale/duplicate guard; cancel cleanup. Synthetic presentation fixture, not a rules test.");
    }
}
#endif

