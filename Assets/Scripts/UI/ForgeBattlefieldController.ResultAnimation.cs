using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ForgeBattlefieldController
{
    private RectTransform randomResultRoot;
    private CanvasGroup randomResultFade;
    private float randomResultStarted;
    private bool randomResultAwaitingFrame;
    private readonly List<RandomResultGraphic> randomResultObjects=new List<RandomResultGraphic>();
    private static bool IsRandomResult(ForgeNotice notice)=>notice?.kind=="coin"||DiceResultValues(notice).Length>0;

    private static string[] DiceResultValues(ForgeNotice notice)
    {
        if(notice?.kind!="dice")return Array.Empty<string>();
        string value=notice.value??notice.message??"";
        var rolled=Regex.Match(value,@"(?:^|\n).+ rolled (\[?[0-9, ]+\]?)");
        if(!rolled.Success)
            rolled=Regex.Match(value,@"(?:^|\n).+ rolled to visit their Attractions\. Result: ([0-9, ]+)\.");
        return rolled.Success?Regex.Matches(rolled.Groups[1].Value,@"\d+").Cast<Match>().Select(m=>m.Value).ToArray():Array.Empty<string>();
    }

    private void ShowRandomResult(ForgeNotice notice)
    {
        if(randomResultRoot==null) {
            randomResultRoot=Rect(surface,"RandomResultAnimation",new Vector2(0,40),new Vector2(600,220));
            SetOverlayLayer(randomResultRoot,NoticeLayerOffset);
            randomResultFade=randomResultRoot.gameObject.AddComponent<CanvasGroup>();
            randomResultFade.blocksRaycasts=false;randomResultFade.interactable=false;
        }
        Clear(randomResultRoot);randomResultObjects.Clear();
        bool coin=notice.kind=="coin";
        string outcome=notice.value??notice.message??"";
        // The native rolled line is the presented result (possibly modified).
        // Natural/ignored metadata stays in history; never re-roll or infer it.
        string[] values=coin?new[]{outcome}:DiceResultValues(notice);
        if(values.Length==0){randomResultRoot.gameObject.SetActive(false);return;}
        for(int i=0;i<values.Length;i++) {
            var rect=Rect(randomResultRoot,"ResultObject_"+i,new Vector2((i%6-(Mathf.Min(6,values.Length)-1)*.5f)*94,-(i/6)*100),new Vector2(coin?76:90,coin?76:90));
            var graphic=rect.gameObject.AddComponent<RandomResultGraphic>();graphic.raycastTarget=false;graphic.Coin=coin;graphic.Outcome=values[i];
            randomResultObjects.Add(graphic);
            if(coin&&(values[i]=="heads"||values[i]=="tails")) {
                bool heads=values[i]=="heads";
                var mark=Label(rect,"CoinFace",heads?TapPaymentText("正","H","表"):TapPaymentText("反","T","裏"),Vector2.zero,new Vector2(58,58),36,heads?new Color(.25f,.12f,.025f):new Color(.12f,.2f,.28f));
                mark.fontStyle=FontStyle.Bold;mark.supportRichText=false;mark.raycastTarget=false;
            }
        }
        randomResultStarted=Time.unscaledTime;
        randomResultAwaitingFrame=true;
        randomResultRoot.gameObject.SetActive(true);randomResultRoot.SetAsLastSibling();UpdateRandomResultAnimation();
    }
    private void UpdateRandomResultAnimation()
    {
        if(randomResultRoot==null||!randomResultRoot.gameObject.activeSelf)return;
        float elapsed=Time.unscaledTime-randomResultStarted;
        bool coin=activeNotice?.kind=="coin";
        float duration=coin?1.65f:1.85f,t=Mathf.Clamp01(elapsed/duration);
        randomResultFade.alpha=Mathf.Clamp01(elapsed/.12f)*Mathf.Clamp01((duration-elapsed)/.24f);
        foreach(var graphic in randomResultObjects) {
            if(graphic==null)continue;
            graphic.Animate(t);
            var rect=graphic.rectTransform;
            float flight=Mathf.Clamp01(t/.67f);
            // Rise/flip/drop for coins; a tumbling bounce for dice. Deterministic
            // visual motion, independent of native result probabilities.
            float height=Mathf.Sin(flight*Mathf.PI)*(coin?125:52);
            var p=rect.anchoredPosition;p.y=height-(randomResultObjects.IndexOf(graphic)/6)*100;rect.anchoredPosition=p;
            rect.localRotation=Quaternion.Euler(0,0,coin?Mathf.Sin(flight*Mathf.PI)*22:0);
            float scale=coin?1+Mathf.Sin(flight*Mathf.PI)*.16f:1;rect.localScale=Vector3.one*scale;
            var mark=rect.Find("CoinFace");if(mark!=null) {
                float flatten=Mathf.Abs(Mathf.Cos((1-Mathf.SmoothStep(0,1,flight))*Mathf.PI*8));
                mark.localScale=new Vector3(flatten,1,1);mark.gameObject.SetActive(flatten>=.16f);
            }
        }
    }
}
