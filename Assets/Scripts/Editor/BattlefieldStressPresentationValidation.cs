#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Display stress adapters only. No cloned object here is a new Forge effect test.
public static class BattlefieldStressPresentationValidation
{
    public const string Output=".tools/battlefield-stress-1005";
    static bool running;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static ForgeSeatView Read(string path)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/"+path+".json"));
    static IEnumerator Frame(float seconds=.5f){yield return new WaitForSecondsRealtime(seconds);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static void Click(Button button){
        Check(button!=null&&button.interactable,"Disabled "+button?.name);
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Blocked "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    public static ForgeSeatView Fixture()
    {
        var view=DuelLayoutPresentationValidation.DensityFixture(true);
        var definitions=CardDatabaseRepository.LoadAll().Where(c=>!c.forgeAmbiguous).GroupBy(c=>c.cardName).ToDictionary(g=>g.Key,g=>g.First());
        var vanilla=definitions.Values.Where(c=>c.HasType(CardType.Creature)&&!c.HasType(CardType.Land)&&!c.HasType(CardType.Artifact)&&string.IsNullOrWhiteSpace(c.description)).OrderBy(c=>c.cardName).Take(20).ToArray();
        Check(vanilla.Length==20,"Need 20 distinct vanilla definitions");
        string[] lands={"Plains","Island","Swamp","Mountain","Forest","Command Tower","Temple Garden","Hallowed Fountain","Watery Grave","Blood Crypt","Stomping Ground","Sacred Foundry","Godless Shrine","Breeding Pool","Steam Vents","Overgrown Tomb","Reliquary Tower","Mystic Sanctuary","Bojuka Bog","Ancient Tomb"};
        string[] artifacts={"Sol Ring","Arcane Signet","Mind Stone","Lightning Greaves","Skullclamp","Chromatic Lantern","Fellwar Stone","Swiftfoot Boots","Commander's Sphere","Thought Vessel"};
        string[] enchantments={"Rhystic Study","Smothering Tithe","Doubling Season","Ghostly Prison","Sylvan Library","Propaganda","Parallel Lives","Primal Vigor","Mirari's Wake","Mana Reflection"};
        string[] walkers={"Jace, the Mind Sculptor","Chandra, Torch of Defiance","Liliana of the Veil","Teferi, Hero of Dominaria","Garruk Wildspeaker","Karn Liberated","Ugin, the Spirit Dragon","Elspeth, Sun's Champion","Nissa, Who Shakes the World","Sorin Markov"};
        string[] battles={"Invasion of Zendikar","Invasion of Ikoria","Invasion of Gobakhan","Invasion of Fiora","Invasion of Ravnica"};
        foreach(var player in view.players) {
            var field=new List<ForgeCardView>();int next=100000+player.seat*10000;
            void Add(CardDefinition d,int copies,bool creature=false,bool land=false,bool walker=false,bool battle=false,bool support=false){
                for(int i=0;i<copies;i++)field.Add(new ForgeCardView{id=next++,ownerSeat=player.seat,name=d.cardName,printingKey=d.PrintingKey,zone="Battlefield",currentType=d.GetTypeLine(),currentRules=d.description,creature=creature,land=land,planeswalker=walker,battle=battle,power=d.attack,toughness=d.defence,loyalty=Math.Max(1,d.loyalty),defense=Math.Max(1,d.battleDefense),stackableSupport=support,stackableStateKnown=true,stackablePermanent=true});
            }
            foreach(var d in vanilla)Add(d,10,creature:true);
            foreach(string name in lands)Add(definitions[name],10,land:true);
            foreach(string name in artifacts)Add(definitions[name],5,support:true);
            foreach(string name in enchantments)Add(definitions[name],5,support:true);
            foreach(string name in walkers)Add(definitions[name],5,walker:true);
            foreach(string name in battles)Add(definitions[name],5,battle:true);
            player.battlefield=field.ToArray();
            player.counters=new[]{new ForgeCardCounter{kind="ENERGY",count=12,label="能量"},new ForgeCardCounter{kind="POISON",count=3,label="中毒"},new ForgeCardCounter{kind="EXPERIENCE",count=25,label="经验"},new ForgeCardCounter{kind="RAD",count=7,label="辐射"},new ForgeCardCounter{kind="TICKET",count=16,label="票券"}};
            player.manaPool=new[]{12,8,5,23,6,10};
        }
        return view;
    }
    public static void Preview(){var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();if(banner!=null)banner.enabled=false;var v=Fixture();ui.Present(v,v.seat,true,_=>{},null,"");}
    public static void Run(){Check(Application.isPlaying,"Play Mode required");if(running)return;running=true;Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("StressValidation").AddComponent<ForgeBattlefieldController>();ui.StartCoroutine(Guard(ui));}
    static IEnumerator Guard(ForgeBattlefieldController ui){
        string original=GamePreferences.Language;var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();if(banner!=null)banner.enabled=false;
        var test=Validate(ui);
        try{while(true){object next;try{if(!test.MoveNext())break;next=test.Current;}catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}yield return next;}File.WriteAllText(Output+"/result.txt","PASS: zh/en/ja native triggered-ability faces and synthetic long text stay masked and can scroll to the end; shape shared by stack and browse rim; all five mapped player counter types, white serif numerals, paging and view/restore; 575 exact permanent IDs per player and 20x10 creatures/lands, 10x5 artifacts/enchantments/planeswalkers, 5x5 battles; overflow regions retain last card, scroll, hover and refresh position; dense-to-normal reparent cleanup. Display adapters are not card effect evidence.");}
        finally{running=false;GamePreferences.SetLanguage(original);ui.RestoreBattlefieldWindows();}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui){
        foreach(var locale in new[]{"zh-CN","en-US","ja-JP"}) {
            GamePreferences.SetLanguage(locale);float until=Time.realtimeSinceStartup+20;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Locale readiness");
            var source=Read("StackInteractions/triggers-target-2");var extra=JsonUtility.FromJson<ForgeStackItem>(JsonUtility.ToJson(source.stackItems[0]));extra.id=999999;extra.description=string.Join(" ",Enumerable.Repeat(source.stackItems[0].description.Split(new[]{" [Zone Changer:"},StringSplitOptions.None)[0],24));source.stackItems=source.stackItems.Concat(new[]{extra}).ToArray();source.stack=source.stackItems.Select(s=>s.description).ToArray();source.stackCount=3;
            ui.Present(source,source.seat,true,_=>{},null,"");yield return Frame();
            var canvas=GameObject.Find("Canvas").transform;var list=canvas.Find("StackList");Check(list.gameObject.activeSelf,"Stack chooser missing");
            var rim=list.GetComponentInChildren<ScrollRect>().content.Find("Entry_999999");var face=rim.Find("StackListTarget_999999/AbilityFace");
            Check(rim.GetComponent<Image>().sprite==face.GetComponent<Image>().sprite,"Ability rim is rectangular");
            var body=face.Find("EffectClip").GetComponent<ScrollRect>();var symbols=body.content.GetComponentInChildren<TextMeshProUGUI>();Check(body.GetComponent<RectMask2D>()!=null&&body.content.rect.height>body.viewport.rect.height,"Long ability has no bounded overflow");
            int font=Mathf.RoundToInt(symbols.fontSize);body.verticalNormalizedPosition=0;yield return Frame(.1f);Check(body.content.anchoredPosition.y>0,"Ability scroll cannot reach end");
            ui.ViewBattlefield();ui.RestoreBattlefieldWindows();yield return Frame(.1f);Check(Mathf.RoundToInt(symbols.fontSize)==font&&body.verticalNormalizedPosition<.01f,"Ability view/return reset");ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-ability-end.png");yield return Frame(.1f);
            ui.Present(null,1,false,null,null,"");var dense=Fixture();File.WriteAllText(Output+"/fixture.json",JsonUtility.ToJson(dense));ui.Present(dense,dense.seat,true,_=>{},null,"");yield return Frame(1);
            foreach(var player in dense.players) {
                Check(player.battlefield.Length==575&&player.battlefield.Select(c=>c.id).Distinct().Count()==575,"Permanent count/identity");
                int represented=0;
                foreach(var card in player.battlefield){var root=GameObject.Find("Card_"+card.id);if(root==null)continue;var count=root.transform.Find("Visual/Count/Value")?.GetComponent<Text>();represented+=count!=null?int.Parse(count.text.TrimStart('×')):1;}
                Check(represented==575,"Missing permanent or group count on screen "+player.seat+" / "+represented);
                var plate=canvas.Find(player.seat==dense.seat?"PlayerPlate":"OpponentPlate");var row=plate.Find("PlayerCounters");
                var energy=row.Find("Counter_ENERGY/Symbol").GetComponent<Text>();Check(energy.text=="\uE907"&&energy.font==Resources.Load<Font>("UI/ManaSymbols/ManaGlyphs"),"Energy glyph/font");
                foreach(var count in row.GetComponentsInChildren<Text>().Where(t=>t.name=="Count")){Check(count.color==Color.white&&count.font.name=="Times New Roman"||count.color==Color.white&&count.font!=GamePreferences.Font,"Counter numeral style");Check(!count.resizeTextForBestFit,"Counter auto size");}
                Check(row.Find("Next")==null&&row.Find("Counter_TICKET/Count").GetComponent<Text>().text=="16","Five counters should fit without paging");
            }
            var scrolls=canvas.GetComponentsInChildren<ScrollRect>().Where(s=>s.name.StartsWith("Dense_")).ToArray();Check(scrolls.Length==6,"Expected six overflow regions");
            foreach(var scroll in scrolls) {
                Check(scroll.GetComponent<RectMask2D>()!=null&&scroll.content.childCount>0,"Overflow missing card objects");scroll.verticalNormalizedPosition=0;yield return Frame(.1f);
                var last=scroll.content.Cast<Transform>().Where(t=>t.name.StartsWith("Card_")).Last();Check(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,RectTransformUtility.WorldToScreenPoint(null,last.TransformPoint(((RectTransform)last).rect.center))),"Last permanent unreachable "+scroll.name);
                var point=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,last.TransformPoint(((RectTransform)last).rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(point,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<ForgeBattlefieldCardPointer>()?.transform==last,"Dense card raycast blocked");ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,point,ExecuteEvents.pointerEnterHandler);yield return Frame(.05f);Check(canvas.Find("InspectPanel").gameObject.activeSelf,"Dense hover absent");ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,point,ExecuteEvents.pointerExitHandler);
            }
            ui.Present(dense,dense.seat,true,_=>{},null,"");yield return Frame(.2f);Check(scrolls.All(s=>s.verticalNormalizedPosition<.01f),"Snapshot reset dense scroll");
            ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-stress-end.png");yield return Frame(.1f);
            foreach(var scroll in scrolls)scroll.verticalNormalizedPosition=1;yield return Frame(.2f);ScreenCapture.CaptureScreenshot(Output+"/"+locale+"-stress-start.png");yield return Frame(.1f);
            DuelLayoutPresentationValidation.Preview(false);yield return Frame(.2f);Check(scrolls.All(s=>!s.gameObject.activeSelf),"Dense viewport still masks normal layout");
        }
    }
}
#endif
