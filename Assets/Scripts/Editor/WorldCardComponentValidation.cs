#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A bounded scene/presentation check. The sample and its 7/7 variant are visual
// fixtures, not evidence that a Forge spell or ability resolved.
public static class WorldCardComponentValidation
{
    const string Output = ".tools/world-components-1105";
    const string Fixture = "Assets/Scenes/PreviewData/BattlefieldRedesignSample.json";
    static readonly List<string> results = new();
    static readonly string[] bindings = {
        "Contact shadow", "Paper edge", "State rim", "Printed face", "Current stats",
        "Group count", "State", "Current costs", "Unavailable art", "Stats backing",
        "Count backing", "State backing", "Grouped card 2", "Grouped card 3",
        "Printed cost cover", "Secondary cost cover", "Compact inset", "Name vignette",
        "Name edge inlay", "Compact footer", "Permanent type"
    };
    static readonly string[] compactBindings = { "Compact inset", "Name vignette", "Name edge inlay", "Compact footer" };
    static bool running;

    public static void Run()
    {
        if (running) throw new InvalidOperationException("Component validation is already running.");
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "IntegratedBattlefield")
            throw new InvalidOperationException("Requires IntegratedBattlefield in Play mode.");
        var ui = UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>();
        if (ui == null) throw new InvalidOperationException("No battlefield controller.");
        Directory.CreateDirectory(Output);
        results.Clear();
        results.Add("RUN " + DateTime.UtcNow.ToString("O"));
        results.Add("SCOPE component reuse, local printing/crop identity and Unity screen presentation only; no Forge effect claim.");
        File.WriteAllLines(Output + "/component-validation.txt", results);
        running = true;
        ui.StartCoroutine(Guard(ui));
    }

    static IEnumerator Guard(ForgeBattlefieldController ui)
    {
        var input = EventSystem.current != null ? EventSystem.current.GetComponent<StandaloneInputModule>() : null;
        bool inputWasEnabled = input != null && input.enabled;
        if (input != null) input.enabled = false;
        var pending = new Stack<IEnumerator>();
        pending.Push(Validate(ui));
        try {
            while (pending.Count > 0) {
                object next;
                try {
                    var run = pending.Peek();
                    if (!run.MoveNext()) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
                    next = run.Current;
                }
                catch (Exception exception) { results.Add("ERROR " + exception); throw; }
                if (next is IEnumerator nested) pending.Push(nested);
                else yield return next;
            }
        }
        finally {
            while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose();
            if (input != null) input.enabled = inputWasEnabled;
            running = false;
            File.WriteAllLines(Output + "/component-validation.txt", results);
        }
    }

    static IEnumerator Settle()
    {
        yield return new WaitForSecondsRealtime(.6f);
        yield return new WaitForEndOfFrame();
    }

    static void Check(bool passed, string message)
    {
        results.Add((passed ? "PASS " : "FAIL ") + message);
        if (!passed) throw new InvalidOperationException(message);
    }

    static BattlefieldCardPart[] Parts(BattlefieldWorldCard card) => card.GetComponentsInChildren<BattlefieldCardPart>(true);
    static BattlefieldCardPart Part(BattlefieldWorldCard card, string binding) => Parts(card).Single(p => p.Binding == binding);
    static TextMeshPro Label(BattlefieldWorldCard card, string binding) => Part(card, binding).GetComponent<TextMeshPro>();
    static MeshRenderer Surface(BattlefieldWorldCard card, string binding) => Part(card, binding).GetComponent<MeshRenderer>();
    static ForgeCardView Copy(ForgeCardView value) => JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(value));

    static void Bind(BattlefieldWorldCard card, ForgeCardView value, Sprite art, int slot)
    {
        card.Bind(value, art, false, 1, slot, new Color(.26f, .31f, .32f), value.manaCost, null,
            value.faceDown ? "" : value.summoningSick ? "Zzz" : "");
        card.SyncPose();
    }

    // TMP text meshes legitimately change with the text. Snapshot the authored
    // parts and physical surfaces, excluding those generated text meshes.
    sealed class Identity
    {
        readonly Dictionary<string, int> parts;
        readonly Dictionary<string, int> meshes;
        readonly int collider;
        public Identity(BattlefieldWorldCard card)
        {
            parts = Parts(card).ToDictionary(p => p.Binding, p => p.GetInstanceID());
            meshes = Parts(card).Where(p => p.GetComponent<TextMeshPro>() == null)
                .ToDictionary(p => p.Binding, p => p.GetComponent<MeshFilter>().sharedMesh.GetInstanceID());
            collider = card.GetComponent<BoxCollider>().GetInstanceID();
        }
        public bool Matches(BattlefieldWorldCard card)
        {
            var current = Parts(card);
            return current.Length == parts.Count && current.All(p => parts.TryGetValue(p.Binding, out int id) && id == p.GetInstanceID())
                && meshes.All(p => Part(card, p.Key).GetComponent<MeshFilter>().sharedMesh.GetInstanceID() == p.Value)
                && card.GetComponentsInChildren<BoxCollider>(true).Length == 1 && card.GetComponent<BoxCollider>().GetInstanceID() == collider;
        }
    }

    static bool ExactSource(Sprite art, string printingKey, out string path)
    {
        if (!CardImageLibrary.TryGetImageSource(art, out path)) return false;
        return string.Equals(new DirectoryInfo(Path.GetDirectoryName(path)).Name + "/" + Path.GetFileNameWithoutExtension(path),
            printingKey, StringComparison.OrdinalIgnoreCase);
    }

    static Vector4 Crop(BattlefieldWorldCard card)
    {
        var properties = new MaterialPropertyBlock();
        Surface(card, "Printed face").GetPropertyBlock(properties);
        return properties.GetVector("_UVRect");
    }

    static bool HasChinese(string text) => !string.IsNullOrEmpty(text) && text.Any(c => c >= '\u3400' && c <= '\u9fff');

    static IEnumerator Validate(ForgeBattlefieldController ui)
    {
        var table = UnityEngine.Object.FindFirstObjectByType<BattlefieldWorldTable>();
        Check(table != null && table.cardPrefab != null && table.artLayouts != null && table.compactBattlefieldCards,
            "Scene has the component prefab and printing-specific compact art layouts.");
        var camera = table.TableCamera;
        Check(camera != null && !camera.orthographic && Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.x, 67.4f)) < .05f,
            "Original perspective table camera retains its 67.4 degree inclination.");
        Vector3 cameraPosition = camera.transform.position;
        Quaternion cameraRotation = camera.transform.rotation;
        float cameraFov = camera.fieldOfView;
        var sample = JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(Fixture));
        ui.Present(sample, sample.seat, false, null, null, "");
        yield return Settle();
        var cards = UnityEngine.Object.FindObjectsByType<BattlefieldWorldCard>(FindObjectsSortMode.None);
        Check(cards.Length > 20, "Fixture produces battlefield and hand world cards (" + cards.Length + ").");
        Check(cards.All(c => Parts(c).Length == bindings.Length
            && Parts(c).Select(p => p.Binding).OrderBy(x => x).SequenceEqual(bindings.OrderBy(x => x))),
            "Every fixture card has exactly 21 unique expected part bindings.");
        Check(cards.All(c => c.GetComponentsInChildren<BoxCollider>(true).Length == 1 && c.GetComponent<BoxCollider>() != null),
            "Every fixture card has one root BoxCollider.");
        foreach (var card in cards) {
            if (card.Anchor != null && card.Anchor.TryGetComponent<ForgeBattlefieldCardPointer>(out var pointer)) pointer.ResetHover();
        }
        Check(cards.All(c => c.GetComponentInParent<Canvas>() == null && c.GetComponentsInChildren<Canvas>(true).Length == 0
            && c.GetComponentsInChildren<Graphic>(true).All(g => g is TextMeshPro && g.GetComponent<MeshRenderer>() != null)),
            "All world card hierarchies use meshes and 3D TMP, with no Canvas or uGUI card rendering.");
        var inspect = GameObject.Find("Canvas")?.transform.Find("InspectPanel");
        if (inspect != null) inspect.gameObject.SetActive(false);
        yield return Settle();

        var source = sample.players.Single(p => p.seat == sample.seat).battlefield;
        var firstValue = Copy(source.Single(c => c.id == 20024));
        var secondValue = Copy(source.Single(c => c.id == 20026));
        var first = cards.Single(c => c.NativeId == firstValue.id);
        var second = cards.Single(c => c.NativeId == secondValue.id);
        Check(CardImageLibrary.TryLoadPrintingKey("MH2/337", 0, out var normalArt) && ExactSource(normalArt, "MH2/337", out _),
            "MH2/337 loads from its exact local printing/face.");
        Check(CardImageLibrary.TryLoadPrintingKey("SLD/1878", 0, out var alternateArt) && ExactSource(alternateArt, "SLD/1878", out _),
            "SLD/1878 loads from its exact local printing/face without changing art preferences.");
        ExactSource(normalArt, "MH2/337", out string normalPath);
        ExactSource(alternateArt, "SLD/1878", out string alternatePath);
        results.Add("SOURCE MH2/337 " + normalPath);
        results.Add("SOURCE SLD/1878 " + alternatePath);
        Check(table.artLayouts.Find(normalArt)?.printingKey == "MH2/337" && table.artLayouts.Find(alternateArt)?.printingKey == "SLD/1878",
            "Art layout lookup follows both actual loaded image sources.");

        Bind(first, firstValue, normalArt, 0);
        Bind(second, secondValue, normalArt, 1);
        var identity = new Identity(second);
        for (int i = 0; i < 3; i++) Bind(second, secondValue, normalArt, 1);
        Check(identity.Matches(second), "Three repeated Bind calls reuse all 21 part instances, surface meshes and collider on card 20026.");
        var originalTexture = second.ArtTexture;
        var originalCrop = Crop(second);
        Vector3 artScale = Surface(second, "Printed face").transform.localScale;
        secondValue.power = secondValue.toughness = 7;
        Bind(second, secondValue, normalArt, 1);
        Check(Label(second, "Current stats").text == "7/7" && second.ArtTexture == originalTexture && Crop(second) == originalCrop
            && Surface(second, "Printed face").transform.localScale == artScale && identity.Matches(second),
            "Fixture stats change 3/3 to 7/7 without replacing art, surface geometry or part instances.");

        var concealed = new ForgeCardView { id = secondValue.id, ownerSeat = secondValue.ownerSeat, zone = "Battlefield", faceDown = true };
        Bind(second, concealed, null, 1);
        Check(second.Concealed && Label(second, "Unavailable art").text == "?" && !Label(second, "Unavailable art").enableAutoSizing
            && Label(second, "Unavailable art").alignment == TextAlignmentOptions.Center
            && Label(second, "Permanent type").text == "" && Label(second, "Current stats").text == ""
            && Label(second, "Current costs").text == "" && compactBindings.All(b => !Surface(second, b).enabled),
            "Concealed rebind clears compact name/type/stats/costs and resets title layout.");
        Bind(second, secondValue, normalArt, 1);
        Check(!second.Concealed && compactBindings.All(b => Surface(second, b).enabled) && identity.Matches(second)
            && Label(second, "Current stats").text == "7/7" && HasChinese(Label(second, "Unavailable art").text),
            "Face-up rebind restores Chinese compact presentation using the same parts.");
        yield return Settle();
        yield return Capture("overview.png");

        secondValue.currentArtPrintingKey = "SLD/1878";
        Bind(second, secondValue, alternateArt, 1);
        yield return Settle();
        var alternateProperties = new MaterialPropertyBlock();
        Surface(second,"Printed face").GetPropertyBlock(alternateProperties);
        var alternateBounds=table.artLayouts.Find(alternateArt).illustration;
        var actualCrop=Crop(second);
        Check(second.ArtTexture == alternateArt.texture && first.ArtTexture == normalArt.texture && first.ArtTexture != second.ArtTexture
            && alternateProperties.GetTexture("_MainTex")==alternateArt.texture
            && actualCrop.x>=alternateBounds.xMin-.0001f && actualCrop.y>=alternateBounds.yMin-.0001f
            && actualCrop.x+actualCrop.z<=alternateBounds.xMax+.0001f && actualCrop.y+actualCrop.w<=alternateBounds.yMax+.0001f
            && identity.Matches(second),
            "Card 20026 displays SLD/1878 beside card 20024 MH2/337 with independent crop/art and stable parts.");
        Check(HasChinese(Label(first, "Unavailable art").text) && Label(first, "Unavailable art").text == Label(second, "Unavailable art").text
            && Label(first, "Current stats").text == "3/3" && Label(second, "Current stats").text == "7/7",
            "Both Murktide names remain Chinese while the fixture independently displays 3/3 and 7/7.");
        Check(camera.transform.position == cameraPosition && Quaternion.Angle(camera.transform.rotation, cameraRotation) < .001f
            && Mathf.Approximately(camera.fieldOfView, cameraFov) && !camera.orthographic,
            "Perspective camera position, inclination and field of view remain unchanged throughout validation.");
        yield return Capture("variant.png");
        results.Add("COMPLETE Unity component/printing/fixture checks; showcase left visible. Forge rules, clicks and full-card pool coverage were not tested.");
    }

    static IEnumerator Capture(string name)
    {
        string path = Path.GetFullPath(Output + "/" + name);
        DateTime previous = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(path);
        float deadline = Time.realtimeSinceStartup + 5;
        while ((!File.Exists(path) || File.GetLastWriteTimeUtc(path) <= previous || new FileInfo(path).Length == 0)
            && Time.realtimeSinceStartup < deadline) yield return null;
        Check(File.Exists(path) && File.GetLastWriteTimeUtc(path) > previous && new FileInfo(path).Length > 0,
            "Actual Game-view ScreenCapture saved " + name + " (" + Screen.width + "x" + Screen.height + ").");
    }
}
#endif
