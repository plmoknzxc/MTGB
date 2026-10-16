#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Replays native descriptions at the display boundary; no game-rule result claim.
public static class BattleTextLocalizationValidation
{
    [Serializable] sealed class Fixture { public string runtimeHash; public Sample[] samples; }
    [Serializable] sealed class Sample { public string name, inputPath, inputSha256; public ForgeStackItem item; }
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string RunCurrentLocale()
    {
        if (!Application.isPlaying || !GamePreferences.Ready) throw new InvalidOperationException("Ready locale in Play Mode required");
        var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText("Tools/Validation/Fixtures/BattleTextLocalization1016.json"));
        var go = new GameObject("BattleTextLocalizationValidation");
        var ui = go.AddComponent<ForgeBattlefieldController>();
        ui.enabled = false;
        var catalog = (Dictionary<string, CardDefinition>)typeof(ForgeBattlefieldController).GetField("catalog", Private).GetValue(ui);
        foreach (var card in CardDatabaseRepository.LoadAll())
            if (!card.forgeAmbiguous) catalog.TryAdd(card.PrintingKey, card);
        var ability = typeof(ForgeBattlefieldController).GetMethod("AbilityEffectText", Private);
        var printed = typeof(ForgeBattlefieldController).GetMethod("PrintedRuleText", Private);
        var lines = new List<string>();
        int failures = 0;
        void Check(bool ok, string name) { lines.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) failures++; }
        try
        {
            foreach (var sample in fixture.samples)
            {
                var item = sample.item;
                var view = new ForgeSeatView { stackItems = new[] { item } };
                typeof(ForgeBattlefieldController).GetField("view", Private).SetValue(ui, view);
                var definition = catalog[item.source.printingKey];
                string expected = GamePreferences.CardRules(definition).Split('\n')[0];
                string nativeInput = JsonUtility.ToJson(item);
                string actual = (string)ability.Invoke(ui, new object[] { item.source, item.description });
                lines.Add(sample.name + " => " + actual);
                if (GamePreferences.Language != "en-US")
                    Check(actual == expected, sample.name + " exact localized printed paragraph");
                else
                    Check(sample.name == "Brainstorm" ? actual.Contains("three cards") && actual.Contains("two cards") : actual.Contains(sample.name == "White Plume Adventurer" ? "take the initiative" : "deals 2 damage"), sample.name + " English meaning retained");
                Check(JsonUtility.ToJson(item) == nativeInput, sample.name + " native input retained");
                if (sample.name != "White Plume Adventurer") continue;
                foreach (string native in new[] { "When White Plume Adventurer enters, you take the initiative.", "When White Plume Adventurer enters the battlefield, you take the initiative." })
                {
                    string localized = (string)printed.Invoke(ui, new object[] { item.source, native });
                    Check(localized == expected, "Equivalent enters wording: " + native);
                }
                foreach (string unknown in new[] { "When White Plume Adventurer enters tapped, you take the initiative.", "When White Plume Adventurer enters, draw 123 cards.", "When White Plume Adventurer leaves the battlefield, you take the initiative." })
                {
                    string unchanged = (string)printed.Invoke(ui, new object[] { item.source, unknown });
                    Check(unchanged == unknown, "Different rule is not substituted: " + unknown);
                }
            }
        }
        finally { UnityEngine.Object.Destroy(go); }
        string output = ".tools/battle-text-audit-1016/regression";
        Directory.CreateDirectory(output);
        lines.Add("Failures=" + failures + "; locale=" + GamePreferences.Language + "; native=" + fixture.runtimeHash);
        File.WriteAllLines(output + "/" + GamePreferences.Language + ".txt", lines);
        if (failures != 0) throw new InvalidOperationException(string.Join("\n", lines.Where(l => l.StartsWith("FAIL", StringComparison.Ordinal))));
        return string.Join("\n", lines);
    }
}
#endif
