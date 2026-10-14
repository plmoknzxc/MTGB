#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;

public static class BlightStackPresentationValidation
{
    const string Base=".tools/recruit-blight-boundaries-1009";
    public static bool Running {get;private set;}
    public static string Output {get;private set;}
    [Serializable] class Meta {public string directory;}
    static string Hash(string p){using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);File.AppendAllText(Output+"/checks.txt",label+"\n");}
    public static void Run(){if(!Application.isPlaying||Running)throw new Exception("Play required");Running=true;Output=Base+"/stack-ui/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");Directory.CreateDirectory(Output);File.WriteAllText(Base+"/stack-ui-latest.txt",Output);File.WriteAllText(Output+"/result.txt","RUNNING");foreach(string p in new[]{"Assets/Scripts/Editor/BlightStackPresentationValidation.cs","Assets/Scripts/UI/ForgeBattlefieldController.AbilityFace.cs"})File.AppendAllText(Output+"/sources.tsv",p+"\t"+Hash(p)+"\n");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("BlightStackValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Validate());}
    static IEnumerator Validate()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();bool enabled=ui.enabled;ui.enabled=false;string old=GamePreferences.Language;
        try{
            string folder=JsonUtility.FromJson<Meta>(File.ReadAllText(Base+"/blight-latest.json")).directory;string input=folder+"/choice-2-seat2.json";File.WriteAllText(Output+"/input-hashes.tsv",input+"\t"+Hash(input)+"\n");var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(input));var item=v.stackItems.Single();var formatter=typeof(ForgeBattlefieldController).GetMethod("AbilityEffectText",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(string language in new[]{"zh-CN","en-US","ja-JP"}){
                GamePreferences.SetLanguage(language);float end=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<end)yield return null;Check(GamePreferences.Ready&&GamePreferences.Language==language,language+" locale ready");ui.Present(null,2,false,null,null,"");ui.Present(v,2,false,null,null,"");yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();
                string expected=language=="zh-CN"?"玩家 2 衰萎 2。":language=="ja-JP"?"プレイヤー2は、自分がコントロールするクリーチャー１体の上に-1/-1カウンターを2個置く。":"Player 2 blight 2. ";
                Check(item.source.id==122&&item.controllerSeat==1,language+" exact actual stack source/controller");Check((string)formatter.Invoke(ui,new object[]{item.source,item.description})==expected,language+" exact actual recipient and amount");var effect=GameObject.Find("Canvas").GetComponentsInChildren<Text>().Single(t=>t.name=="Effect"&&t.transform.parent.name=="EffectClip");Check(effect.text==expected,language+" actual ability face rendered");var tmp=effect.transform.Find("Symbols").GetComponent<TMPro.TMP_Text>();Check(tmp!=null&&tmp.text==expected,language+" symbolized effect rendered");tmp.ForceMeshUpdate();Check(!tmp.isTextOverflowing&&!effect.resizeTextForBestFit,language+" complete fixed font text");float size=tmp.fontSize;ui.ViewBattlefield();yield return null;ui.RestoreBattlefieldWindows();yield return new WaitForSecondsRealtime(.2f);Check(tmp.fontSize==size&&tmp.text==expected,language+" View Return stable");string unknown="Player 2 blight 2. Custom condition";Check((string)formatter.Invoke(ui,new object[]{item.source,unknown})==unknown,language+" unknown extension intact");
            }
            File.WriteAllText(Output+"/result.txt","PASS_BOUNDED: three-locale actual native Blight stack source/recipient/amount, face and TMP display, fixed text fit/View Return, unknown extension fallback. Only exact observed grammar; native outcomes reused.");
        }finally{ui.Present(null,2,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(old);Running=false;}
    }
}
#endif
