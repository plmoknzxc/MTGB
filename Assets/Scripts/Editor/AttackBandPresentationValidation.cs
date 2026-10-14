#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class AttackBandPresentationValidation
{
    const string Root=".tools/attack-bands-1008/";
    const string Output=Root+"ui/";
    static bool running;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Step(string name)=>File.AppendAllText(Output+"steps.log",name+" PASS\n");
    static ForgeSeatView Read(string path)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));
    static IEnumerator Frame(){yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
    static IEnumerator Shot(string name){ScreenCapture.CaptureScreenshot(Output+name+".png");yield return new WaitForEndOfFrame();}
    static void Click(Button button)
    {
        Check(button!=null&&button.interactable&&button.gameObject.activeInHierarchy,"Unavailable button");
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Blocked ray "+button.name+": "+string.Join(",",hits.Take(3).Select(h=>h.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static void TextFits(Text text)
    {
        Check(text.cachedTextGenerator.vertexCount>0,"No glyphs "+text.name);
        Check(text.preferredHeight<=text.rectTransform.rect.height+2,"Clipped text "+text.text+" "+text.preferredHeight+"/"+text.rectTransform.rect.height);
    }
    static void Groups(ForgeSeatView view)
    {
        foreach(var card in view.players.SelectMany(p=>p.battlefield)) {
            var root=GameObject.Find("Card_"+card.id);Check(root!=null,"Missing native card "+card.id);
            var tag=root.GetComponentsInChildren<Text>().SingleOrDefault(t=>t.name=="AttackBand");
            bool grouped=card.attacking&&(card.attackingBandIds?.Length??0)>1;
            Check((tag!=null)==grouped,"Wrong visible group "+card.id);
            if(grouped){Check(tag.text.EndsWith(card.attackingBandIds.Min().ToString()),"Group identity");TextFits(tag);}
        }
    }
    public static void Run()
    {
        if(running)return;Check(Application.isPlaying,"Enter Play Mode");running=true;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"result.txt","RUNNING");File.WriteAllText(Output+"steps.log","");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("AttackBandValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    public static void RunGroupLabels()
    {
        Check(Application.isPlaying,"Enter Play Mode");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        Check(ui!=null,"No existing controller");ui.StartCoroutine(GroupLabels(ui));
    }
    static IEnumerator GroupLabels(ForgeBattlefieldController ui)
    {
        string language=GamePreferences.Language;File.WriteAllText(Output+"group-badge-result.txt","RUNNING");
        var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool bannerEnabled=banner!=null&&banner.enabled;if(banner!=null)banner.enabled=false;
        foreach(string locale in new[]{"zh-CN","en-US","ja-JP"}) {
            GamePreferences.SetLanguage(locale);
            Check(GamePreferences.Language==locale,"Language silently fell back");
            foreach(int local in new[]{1,2}) {
                var v=Read(Root+"inputs/join/attack-4-seat"+local+".json");ui.Present(null,local,false,null,null,"");ui.Present(v,local,false,null,null,"");yield return Frame();Groups(v);
                foreach(var tag in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Where(t=>t.name=="AttackBand")) {
                    Check(tag.text.StartsWith(locale=="en-US"?"Band ":locale=="ja-JP"?"バンド ":"联合 "),"Wrong group language");
                    var badge=tag.transform.parent.GetComponent<RectTransform>();var face=badge.parent.GetComponent<RectTransform>();
                    Check(badge.GetComponent<Image>().color.a>.9f,"Group badge needs opaque background");
                    Check(badge.anchoredPosition.y+badge.rect.height*.5f<=face.rect.height*.5f-14,"Group overlaps printed title");
                }
                Step("group-badge-"+locale+"-seat"+local);yield return Shot("joined-"+locale+"-seat"+local);
            }
            var selected=Read(Root+"inputs/join/attack-3-seat1.json");ui.Present(null,1,false,null,null,"");ui.Present(selected,1,true,_=>{},null,"");yield return Frame();
            Click(GameObject.Find("Card_"+selected.players[0].battlefield.Single(c=>c.name=="Benalish Hero").id).GetComponent<Button>());yield return Frame();
            var hint=GameObject.Find("AttackTargetHint");foreach(var text in hint.GetComponentsInChildren<Text>())TextFits(text);
            Check(hint.transform.Find("BandAttacker").GetComponentInChildren<Text>().text==(locale=="en-US"?"Deselect":locale=="ja-JP"?"選択解除":"取消选组"),"Wrong control language");
            Step("new-controls-"+locale);yield return Shot("controls-"+locale);
        }
        GamePreferences.SetLanguage(language);if(banner!=null)banner.enabled=bannerEnabled;File.WriteAllText(Output+"group-badge-result.txt","PASS six exact native group frames and three selected-band controls: both perspectives; exact zh-CN/en-US/ja-JP labels and full glyphs, opaque badge below printed title. Original full ray suite retained; replaces invalid en/ja alias checks and old group position.");
    }
    static IEnumerator Guard(IEnumerator test)
    {
        string language=GamePreferences.Language;
        var banner=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldMigrationNotice>();bool enabled=banner!=null&&banner.enabled;if(banner!=null)banner.enabled=false;
        try {
            while(true){object next;try{if(!test.MoveNext())break;next=test.Current;}catch(Exception e){File.WriteAllText(Output+"result.txt","FAIL "+e);throw;}yield return next;}
            File.WriteAllText(Output+"result.txt","PASS: exact native two-recipient requests; real card/defender and band/recall button rays, legal availability, group identity on both perspectives, zh/en/ja new label fit, stale/duplicate/nonacting/disabled guards, attacker-owned damage allocation and resolved cleanup. Six native starts reused; no full or live-network claim.");
        } finally {running=false;GamePreferences.SetLanguage(language);if(banner!=null)banner.enabled=enabled;}
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        GamePreferences.SetLanguage("zh-CN");float deadline=Time.realtimeSinceStartup+8;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<deadline)yield return null;Check(GamePreferences.Ready,"Language unavailable");
        var plans=new Dictionary<string,string[]> {
            ["join"]=new[]{"attack:Benalish Hero","attack:Grizzly Bears","band:Benalish Hero","band:Grizzly Bears"},
            ["recall"]=new[]{"attack:Benalish Hero","band:Benalish Hero","attack:Grizzly Bears","band:Benalish Hero","band:Grizzly Bears","remove:Benalish Hero","attack:Benalish Hero","band:Benalish Hero","band:Grizzly Bears"},
            ["limit"]=new[]{"attack:Benalish Hero","band:Benalish Hero","attack:Grizzly Bears","attack:Llanowar Elves","attack:Llanowar Elves"},
            ["bandswith"]=new[]{"attack:Isao, Enlightened Bushi","band:Isao, Enlightened Bushi","attack:Silvos, Rogue Elemental","attack:Grizzly Bears","attack:Grizzly Bears"},
            ["ordinary-repeat"]=new[]{"attack:Grizzly Bears","attack:Grizzly Bears"},
            ["join-seat2"]=new[]{"attack:Benalish Hero","attack:Grizzly Bears","band:Benalish Hero","band:Grizzly Bears"}
        };
        foreach(var pair in plans) {
            int local=pair.Key=="join-seat2"?2:1;
            for(int n=0;n<=pair.Value.Length;n++) {
                var v=Read(Root+"inputs/"+pair.Key+"/attack-"+n+"-seat"+local+".json");var r=v.request;int calls=0,last=-1;
                ui.Present(null,local,false,null,null,"");ui.Present(v,local,true,i=>{calls++;last=i;},null,"");yield return Frame();Groups(v);
                if(n==pair.Value.Length){Step(pair.Key+"-final-native-groups");continue;}
                string[] action=pair.Value[n].Split(new[]{':'},2);var card=v.players[local-1].battlefield.Single(c=>c.name==action[1]);
                Click(GameObject.Find("Card_"+card.id).GetComponent<Button>());yield return Frame();Check(calls==0,"Selecting attacker submitted");
                var hint=GameObject.Find("AttackTargetHint");Check(hint!=null,"Missing selected attacker controls");
                var band=hint.transform.Find("BandAttacker").GetComponent<Button>();int bandIndex=Array.FindIndex(r.options,o=>o.id=="attack-band:"+card.id);
                Check(band.gameObject.activeSelf==(bandIndex>=0),"Band button availability");
                if(bandIndex>=0){TextFits(hint.transform.Find("Hint").GetComponent<Text>());TextFits(band.GetComponentInChildren<Text>());TextFits(hint.transform.Find("RecallAttacker").GetComponentInChildren<Text>());}
                string id=action[0]=="attack"?r.options.First(o=>o.id.StartsWith("attack:")&&o.attackerId==card.id).id:(action[0]=="band"?"attack-band:":"attack-remove:")+card.id;
                int option=Array.FindIndex(r.options,o=>o.id==id);Check(option>=0,"Missing expected native option");Button button;
                if(action[0]=="attack")button=GameObject.Find("OpponentPlate").GetComponent<Button>();
                else button=hint.transform.Find(action[0]=="band"?"BandAttacker":"RecallAttacker").GetComponent<Button>();
                ui.Choose(option,r.requestId-1);Check(calls==0,"Stale accepted");
                if(pair.Key=="join"&&n==2){yield return Shot("select-band");r.options[option].disabled=true;Click(button);Check(calls==0,"Disabled accepted");r.options[option].disabled=false;}
                Click(button);button.onClick.Invoke();Check(calls==1&&last==option,"Exact ray/duplicate option "+pair.Key+"/"+n);
                Step(pair.Key+"-ray-"+n);
            }
            foreach(int recipient in new[]{1,2}) {
                var v=Read(Root+"inputs/"+pair.Key+"/attack-"+pair.Value.Length+"-seat"+recipient+".json");int calls=0;
                ui.Present(null,recipient,false,null,null,"");ui.Present(v,recipient,false,i=>calls++,null,"");yield return Frame();Groups(v);ui.Choose(0,v.request.requestId);Check(calls==0,"Observer action accepted");
                Check(GameObject.Find("AttackTargetHint")==null,"Observer actionable band controls");Step(pair.Key+"-public-seat"+recipient);
                if(pair.Key=="join")yield return Shot("joined-seat"+recipient);
            }
        }
        foreach(string language in new[]{"en-US","ja-JP"}) {
            GamePreferences.SetLanguage(language);yield return Frame();var v=Read(Root+"inputs/join/attack-3-seat1.json");ui.Present(null,1,false,null,null,"");ui.Present(v,1,true,_=>{},null,"");yield return Frame();
            Click(GameObject.Find("Card_"+v.players[0].battlefield.Single(c=>c.name=="Benalish Hero").id).GetComponent<Button>());yield return Frame();
            foreach(var text in GameObject.Find("AttackTargetHint").GetComponentsInChildren<Text>())TextFits(text);Step(language+"-new-band-label-fit");yield return Shot(language+"-band-controls");
        }
        GamePreferences.SetLanguage("zh-CN");
        var allocations=Directory.GetFiles(Root+"inputs/join","allocation-*-seat1.json").Select(Read).OrderBy(v=>v.request.requestId).ToArray();Check(allocations.Length==7,"Native six increments and confirm");
        foreach(var v in allocations) {
            int calls=0,last=-1;var r=v.request;Check(r.seat==1&&r.selectionPurpose=="combatDamage","Wrong native damage owner");
            ui.Present(null,1,false,null,null,"");ui.Present(v,1,true,i=>{calls++;last=i;},null,"");yield return Frame();var panel=GameObject.Find("DamageAllocation");Check(panel!=null,"Missing attacker allocation");
            var hero=r.allocations.Single(a=>a.card.name=="Benalish Hero");bool complete=hero.amount==6;
            Check(panel.transform.Find("Confirm").GetComponent<Button>().interactable==complete,"Premature confirm");
            var button=complete?panel.transform.Find("Confirm").GetComponent<Button>():panel.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Recipient_"+hero.id).Find("Plus").GetComponent<Button>();
            int index=Array.FindIndex(r.options,o=>o.id==(complete?"confirm-amount":"amount-add:"+hero.id));Click(button);Check(calls==1&&last==index,"Wrong native allocation action");Step("attacker-damage-"+hero.amount);if(complete)yield return Shot("attacker-allocation");
        }
        var resolved=Read(Root+"inputs/join/resolved-seat1.json");ui.Present(resolved,1,false,null,null,"");yield return Frame();Check(GameObject.Find("DamageAllocation")==null&&GameObject.Find("AttackTargetHint")==null,"Combat control cleanup");Groups(resolved);Step("MAIN2-cleanup");yield return Shot("resolved");
    }
}
#endif
