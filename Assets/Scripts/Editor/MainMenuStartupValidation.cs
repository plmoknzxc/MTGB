using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Observe actual rendered startup frames, including the cold localization window.
[InitializeOnLoad]
public static class MainMenuStartupValidation
{
    const string Key = "MTGB.MainMenuStartupValidation";
    static int visibleFrames;
    static bool clicked;
    static MainMenuStartupValidation() { EditorApplication.update += Observe; }

    public static void Arm()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Arm before entering Play Mode.");
        SessionState.SetString(Key, "running");
        SessionState.SetString(Key + ".locale", PlayerPrefs.GetString("UI.Language", "zh-CN"));
        SessionState.SetFloat(Key + ".start", (float)EditorApplication.timeSinceStartup);
        visibleFrames = 0; clicked = false;
    }

    static void Observe()
    {
        if (SessionState.GetString(Key, "") != "running" || !Application.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".start", 0) > 60)
                throw new Exception("Startup timed out.");
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            if (menu == null) return;
            var start = UnityEngine.Object.FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(x => x.name == "StartScreen");
            var content = start.transform.Find("StartMenuContent").GetComponent<CanvasGroup>();
            if (start.alpha > .99f && start.interactable && content.alpha > .99f && content.interactable)
            {
                if (!GamePreferences.Ready) throw new Exception("Menu visible before localization.");
                if (GamePreferences.Language != SessionState.GetString(Key + ".locale", ""))
                    throw new Exception("Menu did not use the saved locale.");
                foreach (var pair in new[] { ("OnlineMatchButton", "联机对战"), ("SettingsButton", "观看录像"), ("DeckEditorButton", "卡组编辑") })
                {
                    var button = start.GetComponentsInChildren<Button>(true).First(x => x.name == pair.Item1);
                    if (!button.interactable) throw new Exception(pair.Item1 + " disabled at startup.");
                    if (button.GetComponentInChildren<Text>(true).text != GamePreferences.T(pair.Item2))
                        throw new Exception(pair.Item1 + " displayed an incorrect locale.");
                }
                visibleFrames++;
                if (!clicked)
                {
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    bool ready = (bool)typeof(MainMenuController).GetField("dataReady", flags).GetValue(menu);
                    if (!ready) throw new Exception("Menu appeared before the card database was ready.");
                    SessionState.SetBool(Key + ".queued", !ready);
                    start.GetComponentsInChildren<Button>(true).First(x => x.name == "DeckEditorButton").onClick.Invoke();
                    clicked = true;
                }
            }
            var deck = UnityEngine.Object.FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(x => x.name == "DeckBuilderScreen");
            if (clicked && deck.gameObject.activeSelf && deck.alpha > .99f)
            {
                string result = "PASS: first visible menu localized (" + GamePreferences.Language + "), all entries clickable, deck editor opened immediately; queued="
                    + SessionState.GetBool(Key + ".queued", false) + "; visible samples=" + visibleFrames;
                SessionState.SetString(Key, result); Debug.Log(result);
            }
        }
        catch (Exception error) { SessionState.SetString(Key, "FAIL: " + error.Message); Debug.LogException(error); }
    }
}
