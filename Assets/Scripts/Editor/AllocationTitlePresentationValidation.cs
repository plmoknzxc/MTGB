#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;

// Incremental presentation check: the original target suite retains its own producer identity.
public static class AllocationTitlePresentationValidation
{
    [Serializable] class Meta {public string folder,status;}
    public static string Output {get;private set;}
    public static bool Running {get;private set;}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    public static void Run(){Check(Application.isPlaying&&!Running,"Play/already running");Output=".tools/target-branches-1006/title/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);File.WriteAllText(".tools/target-branches-1006/title/latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/producer-hashes.tsv",string.Join("\n",new[]{"Assets/Scripts/Editor/AllocationTitlePresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.Allocation.cs"}.Select(p=>p+"\t"+Hash(p))));Running=true;var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("AllocationTitleValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){var e=Validate(ui);try{while(true){object c;try{if(!e.MoveNext())break;c=e.Current;}catch(Exception x){File.WriteAllText(Output+"/result.txt","FAIL "+x);throw;}yield return c;}File.WriteAllText(Output+"/result.txt","PASS: actual native ordinary-spell genericAmount damage title localized in zh-CN/en-US/ja-JP, fixed font and complete header. Only affected title verified; target/amount actions retain prior full-suite evidence, no new rules or first UI suite.");}finally{(e as IDisposable)?.Dispose();Running=false;}}
    static IEnumerator Validate(ForgeBattlefieldController ui){string lang=GamePreferences.Language;bool enabled=ui.enabled;ui.enabled=false;try{
        var meta=JsonUtility.FromJson<Meta>(File.ReadAllText(".tools/target-branches-1006/native/arc-partial-result.json"));Check(meta.status=="PASS","Native proof missing");string input=Directory.GetFiles(meta.folder,"step-*.json").Where(p=>!p.EndsWith("-other.json")).First(p=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(p)).request.kind=="AssignAmount");File.Copy(input,Output+"/native-input.json");File.WriteAllText(Output+"/input-sha256.txt",Hash(input));var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(input));
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}){int rev=GamePreferences.Revision;bool change=GamePreferences.Language!=locale;GamePreferences.SetLanguage(locale);float deadline=Time.realtimeSinceStartup+20;while((!GamePreferences.Ready||change&&GamePreferences.Revision==rev)&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready,"Locale unavailable");ui.Present(view,1,false,null,null,"");yield return new WaitForSecondsRealtime(.4f);var title=GameObject.Find("DamageAllocation").transform.Find("Title").GetComponent<Text>();string expected=locale=="zh-CN"?"分配伤害":locale=="en-US"?"Assign damage":"ダメージを割り振る";Check(title.text==expected&&!title.resizeTextForBestFit&&title.preferredHeight<=title.rectTransform.rect.height,"Allocation header language/size: "+locale+" "+title.text);File.AppendAllText(Output+"/titles.tsv",locale+"\t"+title.text+"\t"+title.fontSize+"\n");ScreenCapture.CaptureScreenshot(Output+"/"+locale+".png");yield return new WaitForEndOfFrame();}
        }finally{ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(lang);}}
}
#endif
