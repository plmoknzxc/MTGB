#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class LandTypePresentationValidation
{
    const string Output=".tools/land-type-ui";
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/LandTypes/"+name+"-view.json"));
    public static void Run(){
        Check(Application.isPlaying,"Play Mode required");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("LandTypeValidation").AddComponent<ForgeBattlefieldController>();
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator routine){
        while(true){object next;try{if(!routine.MoveNext())yield break;next=routine.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL: "+e);throw;}yield return next;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui){
        var eventSystem=EventSystem.current;eventSystem.enabled=false;
        foreach(var test in new[]{("blood","山脉","{R}"),("blue","海岛","{U}"),("omen","五种基本地","{G}")}){
            var view=Read(test.Item1+"-active");var card=view.players[0].battlefield.Single(c=>c.name=="Wooded Foothills");
            ui.Present(view,1,false,null,null,"");yield return new WaitForSecondsRealtime(.4f);
            var root=GameObject.Find("Card_"+card.id);Check(root!=null,"Missing land");
            var badge=root.transform.Find("Visual/CurrentLandTypes/Label");Check(badge==null,"Unrequested battlefield type badge remains");
            ExecuteEvents.Execute(root,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
            var canvas=GameObject.Find("Canvas").transform;
            string rules=canvas.Find("InspectPanel/Rules").GetComponent<Text>().text;
            var symbolRules=canvas.Find("InspectPanel/Rules").GetComponentInChildren<TMPro.TextMeshProUGUI>();
            symbolRules.ForceMeshUpdate();
            Check(!symbolRules.text.Contains("{T}")&&!symbolRules.text.Contains(test.Item3),"Raw mana letters remain visible");
            Check(symbolRules.textInfo.characterInfo.Take(symbolRules.textInfo.characterCount).Count(c=>c.elementType==TMPro.TMP_TextElementType.Sprite)>=(test.Item1=="omen"?11:3),"Mana/tap icons missing from rendered mesh");
            Check(!rules.Contains("当前能力")&&rules.Contains(test.Item3)&&rules.Contains("#70BFFF"),"Changed ability color/heading incorrect");
            Check(rules.Contains("Search your library") && rules.Contains("#565C65")== (test.Item1!="omen"),"Original ability not preserved with correct lost color");
            Check(symbolRules.textInfo.characterInfo.Take(symbolRules.textInfo.characterCount).Any(c=>(c.style&TMPro.FontStyles.Strikethrough)!=0)==(test.Item1!="omen"),"Lost rules strikethrough missing or applied to retained rules");
            if(test.Item1!="omen")Check(rules.IndexOf("#70BFFF",StringComparison.Ordinal)<rules.IndexOf("#565C65",StringComparison.Ordinal),"Gained ability must precede lost ability");
            string changedType=ForgeBattlefieldController.ChangedTypeText("Land - Forest","Land - Mountain");
            Check(changedType.Contains("<s color=#00000000>树林</s>"),"Lost type strikethrough missing");
            Check(changedType.IndexOf("山脉",StringComparison.Ordinal)<changedType.IndexOf('\n')&&changedType.IndexOf("树林",StringComparison.Ordinal)>changedType.IndexOf('\n'),"Lost type must appear below current type");
            Check(canvas.Find("InspectPanel/TypeLine").GetComponent<Text>().text.Contains(test.Item1=="blue"?"海岛":"山脉"),"Wrong effective type");
            yield return new WaitForSecondsRealtime(.3f);ScreenCapture.CaptureScreenshot(Output+"/"+test.Item1+".png");yield return new WaitForEndOfFrame();
            var originalToggle=canvas.Find("InspectPanel/OriginalState").GetComponent<Button>();
            originalToggle.onClick.Invoke();
            Check(!canvas.Find("InspectPanel/Rules").GetComponent<Text>().text.Contains("<color="),"Original view contains change colors");
            Check(!canvas.Find("InspectPanel/TypeLine").GetComponent<Text>().text.Contains("山脉"),"Original fetch land still has Mountain");
            originalToggle.onClick.Invoke();
            var restored=Read(test.Item1+"-restored");var original=restored.players[0].battlefield.Single(c=>c.id==card.id);
            Check(ForgeBattlefieldController.ArenaGroupKey(card)!=ForgeBattlefieldController.ArenaGroupKey(original),"Type change not part of group/render key");
            ui.Present(restored,1,false,null,null,"");yield return null;
            Check(GameObject.Find("Card_"+card.id).transform.Find("Visual/CurrentLandTypes")==null,"Stale badge after source removed");
            Check(canvas.Find("InspectPanel/Rules").GetComponent<Text>().text.Contains("Search your library")&&!canvas.Find("InspectPanel/Rules").GetComponent<Text>().text.Contains("<color="),"Open inspector did not restore original colors");
            if(test.Item1=="blood"){ScreenCapture.CaptureScreenshot(Output+"/restored.png");yield return new WaitForEndOfFrame();}
            var projected=OnlineSpectatorView.Create(new OnlinePlayerViewState{forge=view},0);
            Check(projected.forge.players[0].battlefield.Single(c=>c.id==card.id).currentType==card.currentType,"Spectator lost public current type");
            card.faceDown=true;projected=OnlineSpectatorView.Create(new OnlinePlayerViewState{forge=view},0);
            var hidden=projected.forge.players[0].battlefield.Single(c=>c.id==card.id);
            Check(!hidden.hasCurrentLandState&&hidden.currentRules==null&&hidden.basicLandTypes==null,"Face-down private fields leaked");
        }
        eventSystem.enabled=true;
        File.WriteAllText(Output+"/result.txt","PASS: real engine snapshots; red/blue moon and five land types; effective rules and type; restoration while inspecting; grouping refresh; spectator and face-down privacy.");
    }
}
#endif
