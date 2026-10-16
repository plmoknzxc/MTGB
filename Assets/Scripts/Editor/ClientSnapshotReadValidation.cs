#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Frozen wire input and the legacy canonical hash are independent of the new reader.
public static class ClientSnapshotReadValidation
{
    static readonly Type JsonType = typeof(MatchReplay).Assembly.GetType("ServerSnapshotJson");
    static T Read<T>(string value) => (T)JsonType.GetMethod("Deserialize").MakeGenericMethod(typeof(T)).Invoke(null,new object[]{value});
    static string Json(object value) => (string)JsonType.GetMethod("Serialize").Invoke(null,new[]{value});
    static string Seal(JObject fields)
    {
        fields["viewHash"] = "";
        using var sha = SHA256.Create();
        fields["viewHash"] = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(fields.ToString(Formatting.None)))).Replace("-", "");
        return fields.ToString(Formatting.None);
    }
    public static void Run()
    {
        var checks = new List<string>();
        void Check(bool valid,string label) { if(!valid)throw new InvalidOperationException(label); checks.Add(label); }
        void Valid(string value,string label)
        {
            var parsed = Read<OnlinePlayerViewState>(value);
            Check(parsed.HasValidHash(),label);
            parsed.appliedSequence++;
            Check(!parsed.HasValidHash(),label+" detects typed mutation");
        }
        using var file = File.OpenRead("Tools/Validation/Fixtures/ReplayMemoryFrame.json.gz");
        using var zip = new GZipStream(file,CompressionMode.Decompress);
        using var text = new StreamReader(zip);
        var frame = Read<ReplayFrame>(text.ReadToEnd());
        var view = new OnlinePlayerViewState{matchId="client-read-regression",viewerSeat=1,forge=frame.views[0],outcome=MatchOutcome.InProgress()};
        view.SealHash(); string original=Json(view);
        Valid(original,"Existing canonical view");
        var fields=JObject.Parse(original);
        fields["future"]=new JObject{{"unicode","中文 😀 / \\ \""},{"items",new JArray(1,null,1.0,"2026-10-16T01:02:03Z")}};
        Valid(Seal(fields),"Unknown nested sender fields");
        fields["future"]["unicode"]="changed";
        Check(!Read<OnlinePlayerViewState>(fields.ToString()).HasValidHash(),"Unknown field tampering rejected");
        fields=JObject.Parse(original); fields.Remove("outcome"); fields.Remove("appliedSequence"); fields["forge"]["request"]=null;
        Valid(Seal(fields),"Old omitted defaults and explicit null");
        fields=new JObject(JObject.Parse(original).Properties().Reverse().Select(p=>new JProperty(p.Name,p.Value)));
        Valid(Seal(fields),"Sender field order preserved");
        foreach(string value in new[]{"1.0","1e0","-0.0","123456789012345678901234567890","\"2026-10-16T01:02:03Z\""})
        {fields=JObject.Parse(original);fields["futureScalar"]=JToken.Parse(value);Valid(Seal(fields),"Extra scalar "+value);}
        fields=JObject.Parse(original); fields["appliedSequence"]="00042";
        Valid(Seal(fields),"Wire string to numeric DTO conversion");
        fields=JObject.Parse(original); fields["forge"]["phase"]="2026-10-16T01:02:03Z";
        Valid(Seal(fields),"ISO-shaped string field");
        fields=JObject.Parse(original); fields["futureComment"]=1;
        string comment=Seal(fields).Replace("\"futureComment\":1","\"futureComment\":/*omitted by legacy canonical hash*/1");
        Valid(comment,"Canonical comments ignored");
        foreach(string value in new[]{original.Substring(0,original.Length-1),original+" trailing"})
        {
            try {Read<OnlinePlayerViewState>(value);throw new InvalidOperationException("Malformed JSON accepted");}
            catch(TargetInvocationException e) when(e.InnerException is JsonException) {checks.Add("Malformed/trailing input rejected");}
        }
        // Execute the actual client acceptance guard without starting transports.
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        var serviceType=typeof(OnlineMatchService);
        var apply=serviceType.GetMethod("ApplyPlayerView",hidden);
        var current=serviceType.GetField("<ForgeView>k__BackingField",hidden);
        for(int seat=1;seat<=2;seat++)
        {
            var service=(OnlineMatchService)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(serviceType);
            serviceType.GetField("<LocalSeat>k__BackingField",hidden).SetValue(service,seat);
            serviceType.GetField("matchId",hidden).SetValue(service,"wire-opening");
            bool Apply(OnlinePlayerViewState candidate,bool reset=true)
            {
                if(reset)current.SetValue(service,null);
                return (bool)apply.Invoke(service,new object[]{candidate,null});
            }
            string path="Tools/Validation/Fixtures/LegacyOpeningSeat"+seat+".json";
            var legacy=JObject.Parse(File.ReadAllText(path))["view"];
            OnlinePlayerViewState Legacy()=>Read<OnlinePlayerViewState>(legacy.ToString());
            var parsed=Legacy();
            int own=Array.FindIndex(parsed.forge.players,player=>player.seat==seat);
            Check(Apply(parsed)&&own>=0&&parsed.forge.players[own].hand.Length==7,"Old opening accepted with seven private cards, seat "+seat);
            var altered=(JObject)legacy.DeepClone();altered["forge"]["players"][own]["hand"][0]["name"]="tampered";
            Check(!Apply(Read<OnlinePlayerViewState>(altered.ToString())),"Transmitted card mutation rejected, seat "+seat);
            parsed=Legacy();parsed.forge.players[own].hand[0].name="tampered";
            Check(!Apply(parsed),"Post-read private card mutation rejected, seat "+seat);
            altered=(JObject)legacy.DeepClone();altered["future"]=new JObject{{"value",42}};
            Check(Apply(Read<OnlinePlayerViewState>(Seal(altered))),"Future field set accepted, seat "+seat);
            parsed=Legacy();parsed.viewerSeat=3-seat;
            Check(!Apply(parsed),"Wrong private viewer rejected, seat "+seat);
            parsed=Legacy();parsed.matchId="other-match";
            Check(!Apply(parsed),"Wrong match rejected, seat "+seat);
            parsed=Legacy();parsed.appliedSequence=10;parsed.SealHash();Check(Apply(parsed),"Current sender accepted, seat "+seat);
            parsed=Legacy();parsed.appliedSequence=9;parsed.SealHash();
            Check(!Apply(parsed,false),"Older authoritative sequence rejected, seat "+seat);
            Check(parsed.HasValidHash(),"Current SealHash valid, seat "+seat);
        }
        string root=Path.GetFullPath(".tools/client-memory-1016");Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root,"snapshot-boundaries.json"),JsonConvert.SerializeObject(new{success=true,checks},Formatting.Indented));
    }
}
#endif
