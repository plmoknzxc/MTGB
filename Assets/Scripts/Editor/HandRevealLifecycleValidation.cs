#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Replays native Thoughtseize views through the production redesigned scene.
// Added battlefield density is a UI fixture, not a new Forge rules assertion.
public static class HandRevealLifecycleValidation
{
    static string Output => UnityEngine.Object.FindFirstObjectByType<BattlefieldWorldTable>()!=null
        ? ".tools/world-table-1104/hand-reveal" : ".tools/hand-reveal-1104";
    static readonly List<string> results = new();
    static bool running;
    static int run;
    static ForgeSeatView Read(string stage) => JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/Thoughtseize/" + stage + ".json"));
    static void Check(bool value, string message) { results.Add((value ? "PASS " : "FAIL ") + message); if (!value) throw new Exception(message); }
    static IEnumerator Frame() { yield return new WaitForSecondsRealtime(.35f); yield return new WaitForEndOfFrame(); Canvas.ForceUpdateCanvases(); }
    static Rect Bounds(RectTransform rect) { var c = new Vector3[4]; rect.GetWorldCorners(c); return Rect.MinMaxRect(c.Min(p => p.x), c.Min(p => p.y), c.Max(p => p.x), c.Max(p => p.y)); }
    static RectTransform Other => GameObject.Find("OpponentHandRoot").GetComponent<RectTransform>();
    static ForgeBattlefieldCardPointer[] Cards => Other.GetComponentsInChildren<ForgeBattlefieldCardPointer>().Where(p => p.GetComponent<CanvasGroup>().blocksRaycasts).ToArray();
    static void FieldClick(ForgeBattlefieldController ui) => typeof(ForgeBattlefieldController).GetMethod("HandleRedesignBattlefieldPointer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, new object[] { new Vector2(Screen.width * .5f, Screen.height * .5f) });
    static void Press(Transform target)
    {
        var rect = (RectTransform)target;
        var bounds = Bounds(rect);
        for (int x = 1; x <= 9; x++) for (int y = 1; y <= 9; y++) {
            var point = new Vector2(Mathf.Lerp(bounds.xMin, bounds.xMax, x / 10f), Mathf.Lerp(Mathf.Max(1, bounds.yMin), Mathf.Min(Screen.height - 1, bounds.yMax), y / 10f));
            var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            if (hits.Count == 0 || !(hits[0].gameObject.transform == target || hits[0].gameObject.transform.IsChildOf(target))) continue;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
            return;
        }
        throw new Exception("No screen raycast reaches " + target.name);
    }
    static void AddField(ForgeSeatView v, int count)
    {
        var sample = Read("choice").players[1].hand.First(c => c.land);
        v.players[1].battlefield = Enumerable.Range(0, count).Select(i => {
            var c = JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(sample));
            c.id = 909000 + i; c.zone = "Battlefield"; c.tapped = i % 2 == 0; c.ownerSeat = 2;
            return c;
        }).ToArray();
    }
    public static void Run()
    {
        if (running) throw new Exception("Already running");
        if (!Application.isPlaying || !ForgeBattlefieldController.IsBattlefieldSceneActive) throw new Exception("Requires integrated battlefield in Play Mode");
        var ui = UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if (ui == null) ui = new GameObject("HandRevealValidation").AddComponent<ForgeBattlefieldController>();
        running = true; results.Clear(); run++; Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/result.txt", "RUNNING"); ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator test)
    {
        try {
            while (true) {
                object next;
                try { if (!test.MoveNext()) break; next = test.Current; }
                catch (Exception e) { results.Add(e.ToString()); File.WriteAllLines(Output + "/result.txt", results); File.WriteAllLines(Output + "/run-" + run + ".txt", results); throw; }
                yield return next;
            }
            results.Add("PASS ALL: UI projection/raycast lifecycle; no new native rules or network claim.");
            File.WriteAllLines(Output + "/result.txt", results); File.WriteAllLines(Output + "/run-" + run + ".txt", results);
        } finally { running = false; }
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        yield return Frame();
        ui.Present(null, 1, false, null, null, "");
        // Minimal regression: a single land must not push an active hand choice offscreen.
        var choice = Read("choice"); AddField(choice, 1);
        ui.Present(choice, 1, true, _ => { }, null, ""); yield return Frame();
        Check(Cards.Length == choice.players[1].handCount, "All revealed hand cards retained");
        Check(Cards.All(c => Bounds((RectTransform)c.transform).yMax <= Screen.height + 1), "Active opponent hand choice fully inside top edge");
        for (int cycle = 0; cycle < 2; cycle++) {
            var before = Read("before"); AddField(before, cycle == 0 ? 0 : 18);
            ui.Present(before, 1, true, _ => { }, null, ""); yield return Frame();
            FieldClick(ui); yield return Frame();
            float tucked = Other.anchoredPosition.y;
            Check(tucked > 40, "Cycle " + cycle + " idle hand tucks even without battlefield cards");
            Press(GameObject.Find("RedesignOtherHandBackplate").transform); yield return Frame();
            Check(Other.anchoredPosition.y < tucked - 30, "Cycle " + cycle + " top tray press expands");
            FieldClick(ui); yield return Frame();
            Check(Mathf.Abs(Other.anchoredPosition.y - tucked) < 1, "Cycle " + cycle + " battlefield press tucks again");
            var revealed = Read("revealed"); AddField(revealed, cycle == 0 ? 1 : 18);
            revealed.request.requestId += cycle * 1000;
            ui.Present(revealed, 1, true, _ => { }, null, ""); yield return Frame();
            Check(Cards.All(c => Bounds((RectTransform)c.transform).yMax <= Screen.height + 1), "Cycle " + cycle + " reveal reopens complete hand");
            choice = Read("choice"); AddField(choice, cycle == 0 ? 1 : 18); choice.request.requestId += cycle * 1000;
            int calls = 0, selected = -1;
            ui.Present(choice, 1, true, i => { calls++; selected = i; }, null, ""); yield return Frame();
            FieldClick(ui); yield return Frame();
            Check(Cards.All(c => Bounds((RectTransform)c.transform).yMax <= Screen.height + 1), "Cycle " + cycle + " choice cannot be tucked by a field click");
            ui.ViewBattlefield(); yield return Frame();
            Check(Other.anchoredPosition.y>40, "Cycle " + cycle + " battlefield review tucks forced hand");
            ui.RestoreBattlefieldWindows(); yield return Frame();
            Check(Cards.All(c => Bounds((RectTransform)c.transform).yMax <= Screen.height + 1), "Cycle " + cycle + " return to choice restores complete hand");
            int index = Array.FindIndex(choice.request.options, o => o.card != null && o.card.name == "Negate");
            var target = Cards.First(p => p.name == "Card_" + choice.request.options[index].card.id);
            string targetName = target.name;
            ScreenCapture.CaptureScreenshot(Output + "/choice-" + cycle + "-" + Screen.width + "x" + Screen.height + ".png");
            Press(target.transform); Check(calls == 1 && selected == index, "Cycle " + cycle + " physical raycast submits exact native option once");
            ui.Choose(index, choice.request.requestId); Check(calls == 1, "Cycle " + cycle + " duplicate click blocked");
            var resolved = Read("resolved"); AddField(resolved, 18);
            ui.Present(resolved, 1, true, _ => { }, null, ""); yield return Frame();
            Check(Other.anchoredPosition.y > 40, "Cycle " + cycle + " completion restores tucked state");
            Check(!Cards.Any(c => c.name == targetName), "Cycle " + cycle + " private face removed after reveal ends");
        }
        // Own tray must also reopen immediately after repeated battlefield presses.
        for (int i = 0; i < 3; i++) {
            FieldClick(ui); yield return Frame(); Press(GameObject.Find("RedesignHandBackplate").transform); yield return Frame();
            Check(GameObject.Find("PlayerHandRoot").GetComponentsInChildren<ForgeBattlefieldCardPointer>().Any(p => p.GetComponent<CanvasGroup>().alpha > .9f), "Own tray reopen " + i);
        }
        ScreenCapture.CaptureScreenshot(Output + "/idle-" + Screen.width + "x" + Screen.height + ".png");
    }
}
#endif
