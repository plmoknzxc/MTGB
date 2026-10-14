#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class SnapshotWireCompatibilityValidation
{
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static Type Codec=>typeof(OnlinePlayerViewState).Assembly.GetType("ServerSnapshotJson");
    static string Serialize(object value)=>(string)Codec.GetMethod("Serialize").Invoke(null,new[]{value});
    static OnlineStateUpdateMessage Parse(string json)=>(OnlineStateUpdateMessage)Codec.GetMethod("Deserialize").MakeGenericMethod(typeof(OnlineStateUpdateMessage)).Invoke(null,new object[]{json});
    static string Hash(JObject view){view["viewHash"]="";using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(view.ToString(Newtonsoft.Json.Formatting.None)))).Replace("-","");}
    public static string Run(string phase)
    {
        string dir=".tools/opening-sync-fix-1013";Directory.CreateDirectory(dir);
        var checks=new System.Collections.Generic.List<string>();var go=new GameObject("SnapshotWireCompatibilityProbe");var service=go.AddComponent<OnlineMatchService>();
        try {
            for(int seat=1;seat<=2;seat++) {
                typeof(OnlineMatchService).GetProperty("LocalSeat").SetValue(service,seat);
                typeof(OnlineMatchService).GetField("matchId",Hidden).SetValue(service,"wire-opening");
                typeof(OnlineMatchService).GetProperty("ForgeView").SetValue(service,null);
                var cards=Enumerable.Range(1,7).Select(id=>new ForgeCardView{id=id+seat*100,name="Island",zone="Hand",ownerSeat=seat}).ToArray();
                var view=new OnlinePlayerViewState{matchId="wire-opening",viewerSeat=seat,appliedSequence=1,forge=new ForgeSeatView{seat=seat,turn=0,players=new[]{new ForgePlayerView{seat=seat,hand=cards,handCount=7,libraryCount=53},new ForgePlayerView{seat=3-seat,handCount=7,libraryCount=53}},awaitingInput=true,request=new ForgeRequest{seat=seat,kind="InputConfirmMulligan",requestId=1,options=new[]{new ForgeOption{id="keep",label="Keep"}}}}};
                view.SealHash();var legacy=JObject.Parse(Serialize(view));
                foreach(var option in legacy.SelectTokens("$.forge.request.options[*]").Cast<JObject>())option.Remove("rulesText");
                legacy["viewHash"]=Hash(legacy);
                var wire=new JObject{["view"]=legacy,["requestId"]=0};File.WriteAllText(dir+"/legacy-seat"+seat+".json",wire.ToString());
                var parsed=Parse(wire.ToString());object[] args={parsed.view,""};
                bool accepted=(bool)typeof(OnlineMatchService).GetMethod("ApplyPlayerView",Hidden).Invoke(service,args);
                checks.Add((accepted?"PASS ":"FAIL ")+"Old-server opening seat "+seat+" accepted with all seven private hand cards; "+args[1]);
                if(accepted&&service.ForgeView.forge.players[0].hand.Length!=7)checks.Add("FAIL Hand cards lost");
                bool Probe(JObject candidate,bool expected,string label,Action<OnlinePlayerViewState> mutate=null) {
                    var message=Parse(new JObject{["view"]=candidate}.ToString());mutate?.Invoke(message.view);
                    object[] input={message.view,""};bool answer=(bool)typeof(OnlineMatchService).GetMethod("ApplyPlayerView",Hidden).Invoke(service,input);
                    checks.Add((answer==expected?"PASS ":"FAIL ")+label+" seat "+seat+"; "+input[1]);return answer;
                }
                var tampered=(JObject)legacy.DeepClone();tampered["forge"]["players"][0]["hand"][0]["name"]="Secret altered card";
                Probe(tampered,false,"Altered transmitted hand rejected");
                Probe((JObject)legacy.DeepClone(),false,"DTO mutation after parsing rejected",v=>v.forge.players[0].life=123);
                var future=(JObject)legacy.DeepClone();future["futureOptionalField"]="quoted \" value 中文";future["viewHash"]=Hash(future);Probe(future,true,"Future sender field set supported");
                var wrongSeat=(JObject)legacy.DeepClone();wrongSeat["viewerSeat"]=3-seat;wrongSeat["viewHash"]=Hash(wrongSeat);Probe(wrongSeat,false,"Wrong private viewer rejected");
                var wrongMatch=(JObject)legacy.DeepClone();wrongMatch["matchId"]="wrong-match";wrongMatch["viewHash"]=Hash(wrongMatch);Probe(wrongMatch,false,"Wrong match rejected");
                var stale=(JObject)legacy.DeepClone();stale["appliedSequence"]=0;stale["viewHash"]=Hash(stale);Probe(stale,false,"Older authoritative sequence rejected");
                view.forge.request.options[0].rulesText="保留起手牌";view.SealHash();Probe(JObject.Parse(Serialize(view)),true,"Current sender with optional fields supported");
                checks.Add((view.HasValidHash()?"PASS ":"FAIL ")+"Current SealHash remains valid seat "+seat);
            }
        }finally{UnityEngine.Object.DestroyImmediate(go);}
        File.WriteAllLines(dir+"/"+phase+"-result.txt",checks);return string.Join("\n",checks);
    }
}
#endif
