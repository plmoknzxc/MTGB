using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Opt-in Play Mode check of the labels actually rendered, including labels
// whose creator bypasses NativeText. The normal player has no polling cost.
public static class BattlefieldLanguageSweep
{
    const string Menu = "MTGB/验证/扫描本次对局的英文";
    const string Report = "Temp/BattlefieldLanguageSweep.txt";
    static readonly HashSet<string> findings = new(StringComparer.Ordinal);
    static bool running;
    static double next;
    static int samples;

    [MenuItem(Menu)]
    public static void Toggle()
    {
        if (running) { Stop(); return; }
        if (!EditorApplication.isPlaying) { Debug.Log("请在运行游戏后开始扫描。"); return; }
        findings.Clear();
        samples = 0;
        next = 0;
        running = true;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnMode;
    }

    static void OnMode(PlayModeStateChange mode)
    {
        if (mode == PlayModeStateChange.ExitingPlayMode) Stop();
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + .25;
        if (!GamePreferences.Ready || GamePreferences.Language == "en-US" || !ForgeBattlefieldController.IsBattlefieldSceneActive) return;
        samples++;
        foreach (var label in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None)) Inspect(label, label.text);
        foreach (var label in UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None)) Inspect(label, label.text);
    }

    static void Inspect(Graphic label, string value)
    {
        if (!label.isActiveAndEnabled || label.canvas == null || label.canvasRenderer.cull ||
            label.canvasRenderer.GetInheritedAlpha() < .01f || string.IsNullOrEmpty(value)) return;
        string plain = Regex.Replace(value, @"</?(?:color|size|b|i|sprite|material|quad)\b[^>]*>|\{[^{}]+\}", " ").Trim();
        if (!BattleTextDiagnostics.IsUntranslatedDisplay(plain) && !plain.Contains("Forge AI")) return;
        string path = AnimationUtility.CalculateTransformPath(label.transform, label.canvas.rootCanvas.transform);
        findings.Add(path + "\t" + plain.Replace("\r", "").Replace("\n", " / "));
    }

    public static void Stop()
    {
        if (!running) return;
        running = false;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnMode;
        File.WriteAllLines(Report, new[] { $"samples={samples} candidates={findings.Count}" }.Concat(findings.OrderBy(s => s, StringComparer.Ordinal)));
        Debug.Log($"战场英文扫描完成，采样 {samples} 次，发现 {findings.Count} 条待核对文本。{Report}");
    }
}
