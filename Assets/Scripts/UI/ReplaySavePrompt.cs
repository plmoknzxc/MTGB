using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ReplaySavePrompt : MonoBehaviour
{
    private static ReplaySavePrompt instance;
    private readonly Queue<MatchReplay> pending = new Queue<MatchReplay>();
    private RectTransform box;
    private Text message;
    private InputField nameInput;
    public static void Enqueue(MatchReplay replay)
    {
        if (GamePreferences.AutoSaveReplays)
        {
            try { ReplayRepository.Save(replay); return; }
            catch (Exception e) { Debug.LogWarning("录像自动保存失败：" + e.Message); }
        }
        if (instance == null)
        {
            var canvas = ReplayWidgets.Canvas("SaveReplay", 950);
            DontDestroyOnLoad(canvas.gameObject);
            instance = canvas.gameObject.AddComponent<ReplaySavePrompt>(); instance.Build();
        }
        instance.pending.Enqueue(replay);
        if (instance.pending.Count == 1) instance.ShowNext();
    }
    private void Build()
    {
        var shade = ReplayWidgets.Panel(transform, "Modal", 0, 0, 1920, 1080, new Color(0, 0, 0, .75f));
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
        box = ReplayWidgets.Panel(transform, "SaveReplayDialog", 0, 0, 760, 390, MenuVisualTheme.Panel);
        ReplayWidgets.Label(box, "保存对局录像？", 0, 133, 680, 52, 30);
        message = ReplayWidgets.Label(box, "", 0, 76, 680, 48, 19);
        nameInput = ReplayWidgets.Input(box, "", 0, 10, 680);
        var toggle = ReplayWidgets.AutoSaveToggle(box); ((RectTransform)toggle.transform).anchoredPosition = new Vector2(0, -58);
        ReplayWidgets.Button(box, "不保存", 150, -137, 160, () => Complete(false));
        ReplayWidgets.Button(box, "保存录像", -60, -137, 200, () => Complete(true), true);
    }
    private void ShowNext()
    {
        var replay = pending.Peek();
        nameInput.text = ReplayRepository.DefaultName(replay);
        message.text = replay.players[0] + "  vs  " + replay.players[1] + "   ·   " + ReplayWidgets.TimeText(replay.Duration);
    }
    private void Complete(bool save)
    {
        if (pending.Count == 0) return;
        if (save)
        {
            try { ReplayRepository.Save(pending.Peek(), nameInput.text); }
            catch (Exception e) { message.text = e.Message; return; }
        }
        pending.Dequeue();
        if (pending.Count == 0) { Destroy(gameObject); instance = null; }
        else ShowNext();
    }
}
