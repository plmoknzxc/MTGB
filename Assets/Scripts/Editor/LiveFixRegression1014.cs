#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

public static class LiveFixRegression1014
{
    const string Out=".tools/live-fixes-1014";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
    public static string Core(string phase)
    {
        var checks=new List<string>();
        void Check(bool ok,string text)=>checks.Add((ok?"PASS ":"FAIL ")+text);
        var request=new ForgeSeatView{awaitingInput=true,request=new ForgeRequest{seat=1,kind="InputPayManaOfCostPayment",options=new[]{new ForgeOption{id="ok"},new ForgeOption{id="cancel"}}}};
        foreach(var mode in new[]{ForgeControlMode.On,ForgeControlMode.Auto,ForgeControlMode.Off})Check(ForgeControlPolicy.AutomaticOption(request,1,mode,false)==-1,"Payment waits for click in "+mode);
        var foods=JsonUtility.FromJson<Cards>("{\"values\":"+File.ReadAllText(Out+"/foods.json")+"}").values;
        Check(ForgeBattlefieldController.ArenaGroupKey(foods[0])==ForgeBattlefieldController.ArenaGroupKey(foods[1]),"Actual same-state food ignores different art");
        var tapped=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(foods[1]));tapped.tapped=!foods[0].tapped;
        Check(ForgeBattlefieldController.ArenaGroupKey(foods[0])!=ForgeBattlefieldController.ArenaGroupKey(tapped),"Tapped food stays separate");
        var changed=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(foods[1]));changed.currentRules+=" Additional ability.";
        Check(ForgeBattlefieldController.ArenaGroupKey(foods[0])!=ForgeBattlefieldController.ArenaGroupKey(changed),"Different food ability stays separate");
        var status=(string)typeof(ForgeBattlefieldController).GetMethod("KeywordDetailLabel",Hidden).Invoke(null,new object[]{"Chapter:3:Animate1,Animate2,Tutor","Urza's Saga"});
        Check(!status.Contains("Animate")&&!status.Contains("Chapter:"),"Saga status hides native script identifiers");
        request.request.kind="InputPassPriority";request.turnSeat=1;request.phase="COMBAT_BEGIN";request.canRespond=true;request.attackStateKnown=true;request.hasLegalAttackers=false;
        Check(ForgeControlPolicy.AutomaticOption(request,1,ForgeControlMode.Auto,false)==0,"Auto skips empty own combat even with an instant available");
        Check(ForgeControlPolicy.AutomaticOption(request,1,ForgeControlMode.On,false)==-1,"Manual combat remains manual");
        request.hasLegalAttackers=true;Check(ForgeControlPolicy.AutomaticOption(request,1,ForgeControlMode.Auto,false)==-1,"Auto retains combat when native attacker exists");
        request.hasLegalAttackers=false;request.attackStateKnown=false;Check(ForgeControlPolicy.AutomaticOption(request,1,ForgeControlMode.Auto,false)==-1,"Legacy unknown attack availability is conservative");
        request.attackStateKnown=true;request.phase="MAIN1";Check(ForgeControlPolicy.AutomaticOption(request,1,ForgeControlMode.Auto,false)==-1,"Own first main phase stays manual");
        request.phase="MAIN2";Check(ForgeControlPolicy.AutomaticOption(request,1,ForgeControlMode.Off,false)==-1,"Own second main phase stays manual in skip mode");
        File.WriteAllLines(Out+"/"+phase+"-core.txt",checks);return string.Join("\n",checks);
    }
    [Serializable] sealed class Cards{public ForgeCardView[] values;}
}
#endif
