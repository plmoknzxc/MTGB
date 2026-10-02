#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// UI fixtures exercise the same presentation/submission path as server dialogs.
// They verify presentation, not affordability or the Forge resolution rules.
public static class AbilityChoicePresentationValidation
{
    const string Folder=".tools/ability-choices";
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter IntegratedBattlefield Play Mode first.");
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if(ui==null)ui=new GameObject("ChoiceValidation").AddComponent<ForgeBattlefieldController>();
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/result.txt","RUNNING");
        ui.StartCoroutine(Validate(ui));
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static ForgeCardView Card(string name,int id)
    {
        var d=CardDatabaseRepository.LoadAll().First(c=>c.cardName==name && !c.forgeAmbiguous);
        return new ForgeCardView{id=id,name=name,printingKey=d.PrintingKey,creature=true,power=3,toughness=4};
    }
    static Button Option(int n)=>GameObject.Find("CardAbilityChoices").GetComponentsInChildren<Button>().First(b=>b.name=="Option_"+n);
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        var card=Card("Endurance",910);
        var state=new ForgeSeatView{seat=1,turn=3,turnSeat=1,phase="MAIN1",awaitingInput=true,
            players=new[]{new ForgePlayerView{seat=1,life=20,handCount=1,hand=new[]{card},battlefield=Array.Empty<ForgeCardView>(),graveyard=Array.Empty<string>()},
            new ForgePlayerView{seat=2,life=20,hand=Array.Empty<ForgeCardView>(),battlefield=Array.Empty<ForgeCardView>(),graveyard=Array.Empty<string>()}}};
        int calls=0,last=-1;long request=1800;
        Action<string[],bool> show=(labels,enabled)=>{
            state.request=new ForgeRequest{requestId=++request,seat=1,kind="dialog",sourceCard=card,sourceCardId=card.id,
                options=labels.Select((label,i)=>new ForgeOption{id=label=="Cancel"?"cancel":"choice:"+i,label=label}).ToArray()};
            ui.Present(state,1,enabled,i=>{calls++;last=i;},null,"选择界面 · 验证状态");
        };
        show(new[]{"Cast {1}{G}{G}: Endurance","Evoke—Exile a green card from your hand. (You may cast this spell for its evoke cost. If you do, it is sacrificed when it enters.)","Cancel"},true);
        yield return new WaitForSecondsRealtime(.6f);Canvas.ForceUpdateCanvases();
        var canvas=GameObject.Find("Canvas").GetComponent<RectTransform>();
        var menu=GameObject.Find("CardAbilityChoices").GetComponent<RectTransform>();
        var corners=new Vector3[4];menu.GetWorldCorners(corners);
        Check(corners.All(c=>canvas.rect.Contains(canvas.InverseTransformPoint(c))),"Choice menu leaves the canvas.");
        Check(Option(0).GetComponentInChildren<RawImage>().texture!=null,"Source art is missing.");
        Check(menu.position.y>GameObject.Find("Card_910").transform.position.y,"Choices are not above their source.");
        Check(menu.Find("Cancel")==null && menu.rect.height<=432,"Ability menu still contains a cancel button or is oversized.");
        ScreenCapture.CaptureScreenshot(Folder+"/evoke.png");yield return new WaitForEndOfFrame();
        var button=Option(1);var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.TransformPoint(((RectTransform)button.transform).rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<Button>()==button,"Card choice raycast blocked by its content.");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
        button.onClick.Invoke();Check(calls==1 && last==1,"Option ID changed or duplicate click submitted twice.");
        card=Card("Street Wraith",911);state.players[0].hand=new[]{card};
        show(new[]{"Cast {3}{B}{B}: Street Wraith","Cycling—Pay 2 life. Discard this card: Draw a card.","Cancel"},true);
        yield return new WaitForSecondsRealtime(.3f);ScreenCapture.CaptureScreenshot(Folder+"/cycling.png");yield return new WaitForEndOfFrame();
        var backdrop=GameObject.Find("DismissAbilityChoices");
        ExecuteEvents.Execute(backdrop,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Right},ExecuteEvents.pointerClickHandler);
        Check(calls==2 && last==2 && !menu.gameObject.activeSelf,"Outside right-click failed to cancel.");
        card=Card("City of Brass",912);card.land=true;card.creature=false;
        state.players[0].hand=Array.Empty<ForgeCardView>();state.players[0].battlefield=new[]{card};
        show(new[]{"WHITE","BLUE","BLACK","RED","GREEN","Cancel"},true);
        yield return new WaitForSecondsRealtime(.3f);
        Check(menu.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Length==5,"Mana colors are not symbol buttons.");
        Check(menu.Find("Cancel")==null && menu.rect.width<=244 && menu.rect.height<=96,"Mana menu is not compact or still has a cancel button.");
        Check(ForgeBattlefieldController.ChoiceManaSymbols("Add {R}.")=="{R}" && ForgeBattlefieldController.ChoiceManaSymbols("Add {G}. You gain 1 life.")==null,"Mana presentation hides extra rules.");
        ScreenCapture.CaptureScreenshot(Folder+"/mana.png");yield return new WaitForEndOfFrame();
        Option(3).onClick.Invoke();Check(calls==3 && last==3,"Mana choice selected wrong option.");
        show(new[]{"WHITE","BLUE","Cancel"},false);
        yield return null;Check(!Option(0).interactable,"Waiting player's choices are clickable.");
        ExecuteEvents.Execute(GameObject.Find("DismissAbilityChoices"),new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        Check(calls==3,"Waiting player can cancel somebody else's choice.");
        card=Card("Grist, the Hunger Tide",913);card.creature=false;card.planeswalker=true;card.loyalty=5;
        state.players[0].battlefield=new[]{card};
        show(new[]{"+1: Create a 1/1 black and green Insect creature token, then mill a card.","-2: You may sacrifice a creature. When you do, destroy target creature or planeswalker.","-5: Each opponent loses life equal to the number of creature cards in your graveyard.","Cancel"},true);
        yield return new WaitForSecondsRealtime(.3f);ScreenCapture.CaptureScreenshot(Folder+"/abilities.png");yield return new WaitForEndOfFrame();
        Check(menu.GetComponentsInChildren<Text>().All(t=>t.text!="是" && t.text!="否"),"Ability selection has an extra yes/no step.");
        ExecuteEvents.Execute(GameObject.Find("DismissAbilityChoices"),new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        Check(calls==4 && last==3 && !menu.gameObject.activeSelf,"Outside left-click did not dismiss the ability menu.");
        show(new[]{"WHITE","BLUE","Cancel"},true);yield return null;
        // A button handles pointer clicks itself. Verify the global right-click path too.
        var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("AbilityChoiceValidationMouse");
        var inputSettings=UnityEngine.InputSystem.InputSystem.settings;
        var oldEditorInput=inputSettings.editorInputBehaviorInPlayMode;var oldBackground=inputSettings.backgroundBehavior;
        try {
        // Automation runs while Codex has focus. Route only this synthetic input
        // through the player update, then restore the user's editor preferences.
        inputSettings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        inputSettings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState());
        UnityEngine.InputSystem.InputSystem.Update();
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right));
        UnityEngine.InputSystem.InputSystem.Update();
        GameObject.Find("DismissAbilityChoices").SendMessage("Update");
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState());
        UnityEngine.InputSystem.InputSystem.Update();
        Check(calls==5 && last==2 && !menu.gameObject.activeSelf,"Right-click over a choice did not dismiss the menu.");
        } finally {UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);inputSettings.editorInputBehaviorInPlayMode=oldEditorInput;inputSettings.backgroundBehavior=oldBackground;}
        show(new[]{string.Concat(Enumerable.Repeat("Choose a legal object. This long ability retains every condition and cost. ",16)),"Cancel"},true);
        yield return null;
        var textScroll=Option(0).GetComponentsInChildren<ScrollRect>().First();
        Check(textScroll.content.rect.height>textScroll.viewport.rect.height,"Long ability rules cannot be scrolled.");
        show(new[]{"Yes","No"},true);yield return null;
        Check(menu.rect.height<=140 && menu.rect.width<=300,"Confirmation panel is oversized.");
        // This is an optional-effect confirmation fixture, NOT a planeswalker activation.
        ExecuteEvents.Execute(GameObject.Find("DismissAbilityChoices"),new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        Check(calls==5 && menu.gameObject.activeSelf,"Mandatory dialog was dismissed without an offered cancel.");
        File.WriteAllText(Folder+"/result.txt","PASS: compact source-anchored choices without cancel buttons; direct ability options; outside left/right dismissal and global right-click; stable option ID; duplicate prevention; mandatory decisions; disabled viewer; mana symbols; long-text scrolling. Screenshots are UI fixtures, not engine end-to-end tests.");
        Debug.Log("AbilityChoicePresentationValidation PASS");
    }
}
#endif
