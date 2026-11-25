#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

public static class CardNameFontComparisonValidation
{
    const string Output = ".tools/previews/card-name-font-1111";
    const string Sample = "Assets/Scenes/PreviewData/BattlefieldRedesignSample.json";

    public static void Run()
    {
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "IntegratedBattlefield")
            throw new InvalidOperationException("Open IntegratedBattlefield in Play mode before comparing card names.");
        var controller = UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if (controller == null) throw new InvalidOperationException("Battlefield controller is missing.");
        Directory.CreateDirectory(Output);
        controller.StartCoroutine(Capture(controller));
    }

    static IEnumerator Capture(ForgeBattlefieldController controller)
    {
        var sample = JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Sample));
        GamePreferences.SetLanguage("zh-CN");
        string originalFontKey=TemplateCardFace.ChineseNameFontKey;
        foreach (var variant in new[] { ("NameWeiCN", "mashan"), ("NameWenKaiCN", "wenkai") })
        {
            TemplateCardFace.ChineseNameFontKey = variant.Item1;
            controller.Present(null, sample.seat, false, null, null, "");
            yield return null;
            controller.Present(sample, sample.seat, false, null, null, "");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ForgeBattlefieldController).GetField("battlefieldZoom", flags)?.SetValue(controller, 1f);
            typeof(ForgeBattlefieldController).GetField("battlefieldPan", flags)?.SetValue(controller, Vector2.zero);
            typeof(ForgeBattlefieldController).GetMethod("ApplyBattlefieldView", flags)?.Invoke(controller, null);
            var inspect = GameObject.Find("InspectPanel");
            if (inspect != null) inspect.SetActive(false);
            yield return new WaitForSecondsRealtime(1f);
            foreach (var card in UnityEngine.Object.FindObjectsByType<BattlefieldWorldCard>(FindObjectsSortMode.None))
                if (card.Anchor != null) card.Anchor.GetComponent<ForgeBattlefieldCardPointer>()?.ResetHover();
            inspect = GameObject.Find("InspectPanel");
            if (inspect != null) inspect.SetActive(false);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Output + "/" + variant.Item2 + ".png");
            yield return new WaitForSecondsRealtime(.8f);
        }
        TemplateCardFace.ChineseNameFontKey = originalFontKey;
        Debug.Log("Card name font comparison saved to " + Output);
    }
}
#endif
