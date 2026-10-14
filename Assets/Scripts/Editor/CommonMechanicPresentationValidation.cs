#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class CommonMechanicPresentationValidation
{
    const string Output=".tools/common-mechanics-ui";
    static void Check(bool condition,string message) {if(!condition)throw new Exception(message);}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/CommonMechanics/"+name+".json"));
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output+"/result.txt","RUNNING");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("CommonMechanicValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        string original=GamePreferences.Language;
        try {
            while(true) {
                object value;
                try {if(!routine.MoveNext())break;value=routine.Current;}
                catch(Exception error){File.WriteAllText(Output+"/result.txt","FAIL "+error);throw;}
                yield return value;
            }
            File.WriteAllText(Output+"/result.txt","PASS: real request replay in zh-CN/en-US/ja-JP; qualification count/color, persistent markers, three legal dungeon faces, full room map, legal branch click, duplicate/stale/viewer guards, Ring level and land-state wire. Full rules translation and room-map animation are outside these assertions.");
        } finally {GamePreferences.SetLanguage(original);}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string language in new[]{"zh-CN","en-US","ja-JP"}) {
            int previousRevision=GamePreferences.Revision;
            bool changing=GamePreferences.Language!=language;
            GamePreferences.SetLanguage(language);
            float deadline=Time.realtimeSinceStartup+8;
            while(changing && GamePreferences.Revision==previousRevision && Time.realtimeSinceStartup<deadline)yield return null;
            Check(GamePreferences.Language==language && (!changing || GamePreferences.Revision!=previousRevision),"Locale resources did not finish loading");
            foreach(string test in new[]{"story-unique-below-threshold","story-qualified","ascend-below-threshold","ascend-qualified","story-persistent","ascend-persistent","dungeon-dialog-2","dungeon-dialog-3","initiative-dialog-1","ring-select-5","ring-tempt-5","moon-red-blue-stage-2","moon-red-blue-stage-4"}) {
                var view=Read(test);int submissions=0,chosen=-1;
                Action<int> submit=i=>{submissions++;chosen=i;};
                ui.Present(view,1,true,submit,null,"");
                yield return new WaitForSecondsRealtime(.14f);
                Canvas.ForceUpdateCanvases();
                foreach(var card in view.players[0].battlefield.Where(c=>c.qualificationRequired>0)) {
                    var tile=GameObject.Find("Card_"+card.id);
                    var badge=tile.GetComponentsInChildren<Text>().Single(t=>t.name=="Value"&&t.transform.parent.name=="Qualification");
                    Check(badge.text==card.qualificationCount+"/"+card.qualificationRequired,"Wrong qualification tally");
                    Check(!badge.resizeTextForBestFit,"Qualification font resizes");
                    if(card.qualificationEarned)Check(badge.color.r>badge.color.b,"Earned qualification is not gold");
                }
                if(test.EndsWith("persistent")) {
                    var marker=view.players[0].effectMarkers.Single(c=>c.name==(test.StartsWith("story")?"An Enduring Story":"City's Blessing"));
                    Check(GameObject.Find("Card_"+marker.id)!=null,"Persistent qualification marker missing");
                }
                if(test=="dungeon-dialog-2") {
                    Check(view.request.selectionPurpose=="dungeon","Dungeon request lacks typed metadata");
                    Check(view.request.options.Length==3 && !view.request.options.Any(o=>o.card.name=="Undercity"),"Ordinary venture offers illegal dungeon");
                    foreach(var option in view.request.options) {
                        var tile=GameObject.Find("ChoiceCard_"+option.card.id);
                        Check(tile!=null && tile.transform.Find("Artwork").GetComponent<Image>().sprite!=null,"Dungeon helper face missing");
                    }
                    var choice=GameObject.Find("ChoiceCard_"+view.request.options[0].card.id).GetComponentInChildren<Button>();
                    choice.onClick.Invoke();choice.onClick.Invoke();
                    Check(submissions==1 && chosen==0,"Dungeon click/duplicate guard failed");
                    var stale=choice.onClick;
                    var next=Read("dungeon-dialog-3");ui.Present(next,1,true,submit,null,"");
                    stale.Invoke();Check(submissions==1,"Stale dungeon choice submitted");
                    // Restore the original snapshot for the screenshot.
                    ui.Present(view,1,true,submit,null,"");
                }
                if(view.request.selectionPurpose=="dungeonRoom") {
                    var map=GameObject.Find("DungeonMap").GetComponent<Image>();
                    Check(map.sprite!=null && map.preserveAspect,"Full dungeon map missing/cropped");
                    foreach(int index in Enumerable.Range(0,view.request.options.Length)) {
                        var button=GameObject.Find("DungeonRoom_"+index).GetComponent<Button>();
                        Check(button.interactable,"Legal room is not selectable");
                        Check(!button.GetComponentInChildren<Text>().resizeTextForBestFit,"Room text resizes");
                        if(test=="dungeon-dialog-3" && language!="en-US") {
                            string text=button.GetComponentInChildren<Text>().text;
                            Check(!text.Contains("Goblin Lair")&&!text.Contains("Mine Tunnels"),"Translated room text fell back to English");
                            Check(language=="zh-CN"?text.Contains(index==0?"哥布林":"矿坑隧道"):text.Contains(index==0?"ゴブリン":"鉱山"),"Wrong room locale");
                        }
                    }
                    var branch=GameObject.Find("DungeonRoom_1").GetComponent<Button>();
                    branch.onClick.Invoke();branch.onClick.Invoke();
                    Check(submissions==1 && chosen==1,"Room branch/duplicate mapping failed");
                    ui.Present(view,1,false,submit,null,"");
                    GameObject.Find("DungeonRoom_1").GetComponent<Button>().onClick.Invoke();
                    Check(submissions==1,"Viewer submitted a dungeon branch");
                    ui.Present(view,1,true,submit,null,"");
                }
                if(test=="ring-tempt-5") {
                    var player=view.players[0];
                    Check(player.ringTemptedCount==5 && player.ringLevel==4,"Ring temptation and unlocked levels conflated");
                    Check(player.effectMarkers.Count(c=>c.name=="The Ring")==1,"Duplicate Ring markers");
                    Check(GameObject.Find("Card_"+player.effectMarkers.Single(c=>c.name=="The Ring").id)!=null,"Ring marker missing");
                }
                ScreenCapture.CaptureScreenshot(Output+"/"+language+"-"+test+".png");
                yield return new WaitForEndOfFrame();
            }
        }
    }
}
#endif
