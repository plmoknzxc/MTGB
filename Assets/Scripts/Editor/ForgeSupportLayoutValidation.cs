#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public static class ForgeSupportLayoutValidation
{
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play mode required");
        var state=JsonUtility.FromJson<ForgeSeatView>(System.IO.File.ReadAllText("Tools/Validation/Reports/CardSelection/multi-library-context.json"));
        var definitions=CardDatabaseRepository.LoadAll();
        string[] names={"Grizzly Bears","Mountain","Tormod's Crypt","Blood Moon","Jace Beleren","Invasion of Zendikar","Dryad Arbor"};
        for(int seat=1;seat<=2;seat++)state.players[seat-1].battlefield=names.Select((name,i)=>{
            var definition=definitions.First(c=>c.cardName==name && !c.forgeAmbiguous);
            return new ForgeCardView{id=seat*1000+i,name=name,printingKey=definition.PrintingKey,creature=i==0||i==6,land=i==1||i==6,planeswalker=i==4,battle=i==5,loyalty=i==4?3:0,defense=i==5?3:0};
        }).ToArray();
        state.request=new ForgeRequest{requestId=9001,seat=1,kind="InputPassPriority",options=state.players[0].battlefield.Select(c=>new ForgeOption{id="card:"+c.id,label=c.name}).ToArray()};state.awaitingInput=true;
        var ui=new GameObject("SupportLayoutValidation").AddComponent<ForgeBattlefieldController>();
        ui.Present(state,1,true,_=>{},()=>{},"布局验证");Canvas.ForceUpdateCanvases();
    }
    // Call after the presentation animation has settled (at least 0.4 seconds).
    public static void Verify()
    {
        var root=GameObject.Find("ForgePermanents").transform;
        for(int seat=1;seat<=2;seat++)
        {
            var creature=(RectTransform)root.Find("Card_"+(seat*1000));
            var animatedLand=(RectTransform)root.Find("Card_"+(seat*1000+6));
            if(Math.Abs(creature.anchoredPosition.y-animatedLand.anchoredPosition.y)>1)throw new Exception("Animated land outside creature row");
            for(int i=2;i<=5;i++)
            {
                var card=(RectTransform)root.Find("Card_"+(seat*1000+i));
                if(card.anchoredPosition.x<=creature.anchoredPosition.x)throw new Exception("Support permanent in creature row");
                if(seat==1)
                {
                    var hits=new System.Collections.Generic.List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,card.position)},hits);
                    if(hits.Count==0 || hits[0].gameObject.GetComponentInParent<Button>()!=card.GetComponent<Button>())throw new Exception("Support card obstructed");
                }
            }
        }
        ScreenCapture.CaptureScreenshot("Tools/Validation/Reports/CardSelection/support-layout.png");
        System.IO.File.WriteAllText("Tools/Validation/Reports/CardSelection/support-layout.txt","PASS: both seats' artifacts, enchantments, planeswalkers, battles on side; animated land in creature row; own side cards raycast correctly.");
    }
}
#endif
