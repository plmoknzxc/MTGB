#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class FraMechanicsPresentationValidation
{
    const string Output=".tools/fra-mechanics-ui";
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("FraValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while(true) {
            object value;
            try { if(!routine.MoveNext())break;value=routine.Current; }
            catch(Exception ex){File.WriteAllText(Output+"/result.txt","FAIL "+ex);throw;}
            yield return value;
        }
        File.WriteAllText(Output+"/result.txt","PASS: 8 real-engine snapshots rendered; token art, count labels, and battlefield objects checked. Screenshots require visual review.");
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string test in new[]{"heartwood-resolved","prepare-prepared","flashback-resolved","surveil-surveil","cycling-select","modal-dialog","grave-empower-resolved","behold-hand-select"}) {
            var view=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/FraMechanics/"+test+".json"));
            ui.Present(view,1,true,i=>{},null,"");
            yield return new WaitForSecondsRealtime(.8f);
            foreach(var token in view.players[0].battlefield.Where(c=>c.token)) {
                var obj=GameObject.Find("Card_"+token.id);
                if(obj==null)throw new Exception(test+": token absent "+token.name);
                if(!obj.GetComponentsInChildren<RawImage>().Any(i=>i.texture!=null))throw new Exception(test+": token illustration absent "+token.name);
            }
            foreach(var t in Resources.FindObjectsOfTypeAll<Text>().Where(t=>t.gameObject.scene.IsValid()&&t.gameObject.activeInHierarchy&&t.text.StartsWith("×")))
                if(t.horizontalOverflow!=HorizontalWrapMode.Overflow)throw new Exception("Count label can wrap: "+t.text);
            ScreenCapture.CaptureScreenshot(Output+"/"+test+".png");
            yield return new WaitForEndOfFrame();
        }
    }
}
#endif
