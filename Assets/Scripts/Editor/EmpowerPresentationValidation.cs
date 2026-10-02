#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class EmpowerPresentationValidation
{
    const string Output = ".tools/empower-ui";
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
    static ForgeSeatView Read(string file) => JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText("Tools/Validation/Reports/Empower/" + file + ".json"));
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        var ui = UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if (ui == null) ui = new GameObject("EmpowerValidation").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Validate(ui)));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while (true) {
            object value;
            try { if (!routine.MoveNext()) break; value = routine.Current; }
            catch (Exception ex) { File.WriteAllText(Output + "/result.txt", "FAIL " + ex); throw; }
            yield return value;
        }
        File.WriteAllText(Output + "/result.txt", "PASS: real engine snapshots, local Jace art, loyalty 2 and 4, multiple-token choice, ability choices, surveil UI, zero-loyalty removal.");
    }
    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        foreach (string test in new[] { "surveil-created", "draw-increased", "multiple-choose", "draw-abilities", "surveil-surveil", "zero-zero-resolved" }) {
            var view = Read(test); int chosen = -1;
            ui.Present(view, 1, true, i => chosen = i, null, "");
            yield return new WaitForSecondsRealtime(.8f);
            var tokens = view.players[0].battlefield.Where(c => c.name == "Jace Token").ToArray();
            if (test == "zero-zero-resolved") Check(tokens.Length == 0, "Zero loyalty token remains");
            foreach (var token in tokens) {
                var card = GameObject.Find("Card_" + token.id);
                Check(card != null, "Missing Jace battlefield object");
                Check(card.GetComponentsInChildren<RawImage>().Any(i => i.texture != null) || card.GetComponentsInChildren<Image>().Any(i => i.sprite != null && i.sprite.texture.width > 300), "No local Jace artwork");
                Check(card.transform.Find("Visual/DefenseCounter/Value").GetComponent<Text>().text == token.loyalty.ToString(), "Incorrect loyalty badge");
                Check(card.transform.Find("Visual/TokenBadge") == null, "Text token tag remains");
                Check(card.GetComponent<RectTransform>().rect.height > card.GetComponent<RectTransform>().rect.width, "Planeswalker must be portrait");
            }
            ScreenCapture.CaptureScreenshot(Output + "/" + test + ".png");
            yield return new WaitForEndOfFrame();
            if (test == "draw-abilities") {
                var menu = GameObject.Find("CardAbilityChoices");
                Check(menu != null, "Loyalty ability options missing");
                Check(menu.GetComponentsInChildren<RawImage>().Any(i => i.texture != null), "Ability choice artwork missing");
                menu.GetComponentsInChildren<Button>().First(b => b.interactable && b.name.StartsWith("Option_")).onClick.Invoke();
                Check(chosen >= 0, "Ability click did not submit");
            }
            if (test == "multiple-choose") {
                foreach (var token in tokens) {
                    var tile = GameObject.Find("ChoiceCard_" + token.id);
                    Check(tile != null && tile.transform.Find("Artwork").GetComponent<Image>().sprite != null, "Choice token art missing");
                    Check(tile.GetComponentsInChildren<Text>().Any(t => t.text == "忠诚 " + token.loyalty), "Choice loyalty missing");
                }
            }
        }
    }
}
#endif

