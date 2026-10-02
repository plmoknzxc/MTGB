#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class CastingChoicePresentationValidation
{
    const string Output=".tools/casting-choices";
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static ForgeSeatView Read(string name)=>JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/CastingChoices/"+name+".json"));
    public static void Run(){Check(Application.isPlaying,"Enter test scene Play Mode first");var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();if(ui==null)ui=new GameObject("CastingChoiceValidation").AddComponent<ForgeBattlefieldController>();Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");ui.StartCoroutine(Validate(ui));}
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach(string test in new[]{"cast-adventure","cast-mdfc-front","cast-mdfc-back","cast-omen"}) {
            var view=Read(test+"-menu");int selected=-1;ui.Present(view,1,true,i=>selected=i,null,"");yield return new WaitForSecondsRealtime(.4f);
            var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null&&GameObject.Find("CardSelection")==null,"Casting choices became a zone picker");
            var faces=view.request.options.Where(o=>!string.IsNullOrEmpty(o.faceKind)).ToArray();Check(faces.Length==2,"Missing casting faces");
            foreach(var face in faces)Check(menu.GetComponentsInChildren<Text>().Any(t=>t.name=="FaceName"&&t.text==face.castingFace.name),"Wrong face name "+face.faceKind);
            if(test.Contains("mdfc")) {
                var arts=menu.GetComponentsInChildren<Image>().Where(i=>i.name=="CastingFace").ToArray();
                Check(arts.Length==2&&arts.All(a=>a.sprite!=null)&&arts[0].sprite!=arts[1].sprite,"MDFC front and back use same or missing art");
            } else Check(menu.GetComponentsInChildren<Text>().Any(t=>t.name=="Effect"&&t.text.Contains(test=="cast-omen"?"Each non-Dragon":"Damage can't be prevented")),"Secondary face rules absent");
            ScreenCapture.CaptureScreenshot(Output+"/"+test+".png");yield return new WaitForEndOfFrame();
            ExecuteEvents.Execute(GameObject.Find("DismissAbilityChoices"),new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right},ExecuteEvents.pointerClickHandler);
            Check(selected>=0&&view.request.options[selected].id=="cancel","Casting choice cancellation not mapped");
        }
        for(int step=0;step<=7;step++) {
            var view=Read("cast-spree-modes-"+step);int sent=-1;ui.Present(view,1,true,i=>sent=i,null,"");yield return new WaitForSecondsRealtime(.15f);
            var menu=GameObject.Find("CardAbilityChoices");Check(menu!=null,"Spree menu missing");
            Check(menu.GetComponent<RectTransform>().rect.width<=430&&menu.GetComponent<RectTransform>().rect.height<=342,"Spree menu oversized");
            Check(menu.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Mode_"))==3,"Spree modes missing");
            Check(menu.GetComponentsInChildren<Text>().Count(t=>t.name=="Selection"&&t.text=="✓")==view.request.selectedCount,"Spree selected state incorrect");
            var confirm=menu.transform.Find("confirm-modes").GetComponent<Button>();Check(confirm.interactable==(view.request.selectedCount>0),"Spree permits zero modes");
            if(step==6||step==7){ScreenCapture.CaptureScreenshot(Output+"/spree-"+step+".png");yield return new WaitForEndOfFrame();}
            if(step==7){confirm.onClick.Invoke();Check(sent>=0&&view.request.options[sent].id=="confirm-modes","Spree confirmation mapping");}
        }
        File.WriteAllText(Output+"/result.txt","PASS: real Adventure/MDFC face names/rules/art, separate front/back sprites, outside cancellation; real Spree selected states, minimum gate, compact layout and confirmation.");Debug.Log("CastingChoicePresentationValidation PASS");
    }
}
#endif
