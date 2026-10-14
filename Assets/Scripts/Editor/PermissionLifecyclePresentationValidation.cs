#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class PermissionLifecyclePresentationValidation
{
    const string Output=".tools/permission-lifecycle-1007/ui";
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Play mode required");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("PermissionValidation").AddComponent<ForgeBattlefieldController>();
        bool previous=ui.enabled;ui.enabled=false;string language=GamePreferences.Language;
        var modules=UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.BaseInputModule>(FindObjectsSortMode.None);
        var enabled=modules.Select(m=>m.enabled).ToArray();foreach(var m in modules)m.enabled=false;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/steps.log","");
        void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        void Pass(string message)=>File.AppendAllText(Output+"/steps.log",message+" PASS\n");
        ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(".tools/permission-lifecycle-1007/inputs/"+name+".json"));
        IEnumerator Frame(){yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
        void Click(GameObject root) {
            Check(root!=null,"Missing click root");var rect=(RectTransform)root.transform;
            var position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var e=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=position,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
            var hits=new List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(e,hits);
            Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(root.transform),"Blocked ray "+root.name);
            UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }
        IEnumerator Validate() {
            foreach(string lang in new[]{"zh-CN","en-US","ja-JP"}) {
                GamePreferences.SetLanguage(lang);float until=Time.realtimeSinceStartup+15;while(!GamePreferences.Ready&&Time.realtimeSinceStartup<until)yield return null;Check(GamePreferences.Ready,"Language ready");
                ui.Present(null,1,false,null,null,"");yield return Frame();
                var own=Read("gonti-control-source-after-0");var card=own.playableExile.Single();int sent=-1;
                ui.Present(own,1,true,i=>sent=i,null,"");yield return Frame();
                var tile=GameObject.Find("Card_"+card.id);Check(tile!=null,"Known exile is absent from casting shelf");
                var image=tile.transform.Find("Visual/Border/Face").GetComponent<UnityEngine.UI.Image>();
                var catalog=(Dictionary<string,CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(ui);
                Check(catalog.TryGetValue(card.knownFacePrintingKey,out var definition),"Known print absent from catalog");
                Check(CardImageLibrary.TryLoadFullCard(definition,out var expected)&&image.sprite==expected,"Known exile shows wrong localized face");
                Check(card.faceDown&&card.name=="Face-down card","UI mutated actual zone card");
                UnityEngine.EventSystems.ExecuteEvents.Execute(tile,new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);yield return Frame();
                var casting=((ForgeCardView[])typeof(ForgeBattlefieldController).GetMethod("AvailableExileCards",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(ui,null)).Single(c=>c.id==card.id);
                Check(casting.name=="Air Elemental"&&casting.power==4&&casting.toughness==4,"Known casting preview uses facedown 2/2");
                Check(image.sprite==expected&&tile.transform.Find("Visual").localScale.x>1.2f,"Known hand hover loses art or lift");
                Pass(lang+" private localized casting artwork and hover; preview original stats; native zone object unchanged");
                UnityEngine.EventSystems.ExecuteEvents.Execute(tile,new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);yield return Frame();
                Click(tile);yield return Frame();Check(sent>=0&&own.request.options[sent].id=="card:"+card.id,"Wrong exile identity submitted");
                ui.Present(Read("gonti-control-source-payment-3"),1,true,i=>{},null,"");yield return Frame();
                var origin=GameObject.Find("TargetOrigin");Check(origin!=null&&origin.GetComponent<UnityEngine.UI.Image>().sprite==expected,"Known cast payment lost artwork");
                Pass(lang+" actual casting shelf ray submits exact native ID and normal payment keeps face");
                if(lang=="zh-CN"){ScreenCapture.CaptureScreenshot(Output+"/known-exile-payment.png");yield return Frame();}
                ui.Present(Read("gonti-control-source-after-3"),1,true,i=>{},null,"");yield return Frame();
                Check(GameObject.Find("Card_"+card.id)!=null&&Read("gonti-control-source-after-3").players[0].battlefield.Any(c=>c.id==card.id),"Cast resolution missing");
                Pass(lang+" consumed exile entry becomes same battlefield identity");
                ui.Present(null,2,false,null,null,"");yield return Frame();var other=Read("gonti-control-source-after-0-other");ui.Present(other,2,false,i=>sent=i,null,"");yield return Frame();
                Check(GameObject.Find("Card_"+card.id)==null&&!other.playableExile.Any(),"Unauthorized casting shelf leaks card");
                Pass(lang+" other seat has no private casting face or shelf entry");
            }
            GamePreferences.SetLanguage("zh-CN");yield return Frame();
            foreach(var state in new[]{"after-0","next-turn","cancelled-retry","after-1","expired"}) {
                ui.Present(null,1,false,null,null,"");yield return Frame();var data=Read("impulse-next-expiry-"+state);ui.Present(data,1,data.awaitingInput,i=>{},null,"");yield return Frame();
                foreach(var c in data.playableExile)Check(GameObject.Find("Card_"+c.id)!=null,"Impulse permission missing "+state);
                if(state=="expired")foreach(var c in data.players[0].exileCards)Check(GameObject.Find("Card_"+c.id)==null,"Expired impulse remains in casting shelf");
                Pass("actual impulse "+state+" casting shelf matches native permissions");
            }
            int preparedId=Read("prepared-control-source-after-0").players[0].battlefield.Single(c=>c.preparedSpell!=null&&c.preparedSpell.id>0).preparedSpell.id;
            foreach(var state in new[]{"after-0","after-1","after-2"})foreach(int local in new[]{1,2}) {
                ui.Present(null,local,false,null,null,"");yield return Frame();var data=Read("prepared-control-source-"+state+(local==2?"-other":""));ui.Present(data,local,data.awaitingInput,i=>{},null,"");yield return Frame();
                foreach(var c in data.playableExile)Check(GameObject.Find("Card_"+c.id)!=null,"Prepared permission missing");
                var prepared=data.players.SelectMany(p=>p.battlefield).Select(p=>p.preparedSpell).FirstOrDefault(p=>p!=null&&p.id>0);
                if(prepared!=null&&!data.playableExile.Any(c=>c.id==prepared.id))Check(GameObject.Find("Card_"+prepared.id)==null,"Unauthorized prepared shelf");
                Check((GameObject.Find("Card_"+preparedId)!=null)==data.playableExile.Any(c=>c.id==preparedId),"Prepared shelf persists after source leaves or changes control");
                Pass("actual prepared "+state+" seat "+local+" source control/leaving projection");
            }
        }
        IEnumerator Guard() {
            var frames=new Stack<IEnumerator>();frames.Push(Validate());
            try {
                while(frames.Count>0) {
                    object next;try{var f=frames.Peek();if(!f.MoveNext()){(f as IDisposable)?.Dispose();frames.Pop();continue;}next=f.Current;if(next is IEnumerator child){frames.Push(child);continue;}}
                    catch(Exception e){File.WriteAllText(Output+"/result.txt","FAIL "+e);throw;}
                    yield return next;
                }
                File.WriteAllText(Output+"/result.txt","PASS bounded permission casting shelf: private known face, exact native click, payment and consumed entry; impulse next-turn/expiry; prepared controller/source lifecycle. Actual inputs, no live network proof.");
            } finally {ui.Present(null,1,false,null,null,"");ui.enabled=previous;GamePreferences.SetLanguage(language);for(int i=0;i<modules.Length;i++)if(modules[i]!=null)modules[i].enabled=enabled[i];}
        }
        ui.StartCoroutine(Guard());
    }
}
#endif
