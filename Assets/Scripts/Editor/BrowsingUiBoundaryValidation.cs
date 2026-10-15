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

// Local UI boundary replay, not new Forge rule or live-server coverage.
public static class BrowsingUiBoundaryValidation
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Flags).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Flags).SetValue(owner, value);
    static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Flags).Invoke(owner, args);
    static IEnumerator Frame() { yield return null; yield return new WaitForEndOfFrame(); Canvas.ForceUpdateCanvases(); }
    static void Click(GameObject root)
    {
        var rect = (RectTransform)root.transform;
        var e = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(e, hits);
        if (hits.Count == 0 || !hits[0].gameObject.transform.IsChildOf(root.transform)) throw new Exception("Blocked ray: " + root.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, e, ExecuteEvents.pointerClickHandler);
    }
    public static void RunMenu(string output)
    {
        if (!Application.isPlaying) throw new Exception("Run in Main Play mode");
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        if (menu == null) throw new Exception("Main menu missing");
        Directory.CreateDirectory(output);
        menu.StartCoroutine(Guard(Menu(menu, output), output));
    }
    static IEnumerator Menu(MainMenuController menu, string output)
    {
        while (!GamePreferences.Ready || Field<List<CardDefinition>>(menu, "cards").Count == 0) yield return null;
        bool enabled = menu.enabled; menu.enabled = false;
        var originalDeck = Field<DeckDefinition>(menu, "currentDeck");
        var decks = Field<List<DeckDefinition>>(menu, "decks"); var originals = decks.ToArray();
        string language = GamePreferences.Language; float scale = GamePreferences.TextScale;
        var failures = new List<string>(); var steps = new List<string>();
        void Check(bool ok, string why) { steps.Add((ok ? "PASS " : "FAIL ") + why); if (!ok) failures.Add(why); }
        try
        {
            GamePreferences.SetScale(1.3f);
            Call(menu, "ShowDeckBuilder"); yield return Frame();
            foreach (string locale in new[] { "zh-CN", "ja-JP", "en-US" })
            {
                GamePreferences.SetLanguage(locale); while (!GamePreferences.Ready) yield return null;
                Set(menu, "currentDeck", new DeckDefinition { name = "UI-only auxiliary fixture" });
                Call(menu, "ShowAuxiliaryDeck", true);
                var dialog = Field<GameObject>(menu, "exchangeDialog");
                int initial = dialog.transform.Find("Panel/Attractions/Text").GetComponent<Text>().fontSize;
                Check(initial == Mathf.RoundToInt(20 * GamePreferences.TextScale), locale + " auxiliary first frame font=" + initial);
                yield return Frame(); GamePreferences.BindScene(); yield return Frame();
                int settled = dialog.transform.Find("Panel/Attractions/Text").GetComponent<Text>().fontSize;
                Check(initial == settled, locale + " auxiliary font stable before/after preference binding");
                foreach (string tab in new[] { "Contraptions", "Attractions", "ToggleRegistered" })
                {
                    Click(Field<GameObject>(menu, "exchangeDialog").transform.Find("Panel/" + tab).gameObject);
                    dialog = Field<GameObject>(menu, "exchangeDialog");
                    Check(dialog.transform.Find("Panel/Attractions/Text").GetComponent<Text>().fontSize == settled, locale + " switch " + tab + " keeps font");
                    yield return Frame();
                }
                ScreenCapture.CaptureScreenshot(output + "/auxiliary-" + locale + ".png"); yield return Frame();
                Call(menu, "CloseDeckExchange"); Set(menu, "auxiliaryDialogOpen", false); yield return Frame();
            }
            var dropdown = Field<Dropdown>(menu, "deckDropdown");
            decks.Clear();
            foreach (string name in new[] { "Affinity", "Imported Deck (1)", "这是一个很长的牌组名称用于验证下拉选项不会挤到下一行而且保留完整名称", "A deliberately long deck name with spaces to verify single line list clipping", "living end" }) decks.Add(new DeckDefinition { name = name });
            Call(menu, "RefreshDeckDropdown", new object[] { null }); yield return Frame();
            dropdown.Show(); yield return Frame(); GamePreferences.BindScene(); yield return Frame();
            foreach (var toggle in dropdown.GetComponentsInChildren<Toggle>())
            {
                var text = toggle.GetComponentInChildren<Text>(); if (text == null) continue;
                Check(text.horizontalOverflow == HorizontalWrapMode.Overflow && text.verticalOverflow == VerticalWrapMode.Truncate, "single-line dropdown: " + text.text);
                Check(text.cachedTextGenerator.lineCount <= 1, "one rendered line: " + text.text);
            }
            Check(dropdown.options[2].text == decks[2].name, "full dropdown name retained");
            ScreenCapture.CaptureScreenshot(output + "/long-deck-dropdown.png"); yield return Frame(); dropdown.Hide();
        }
        finally
        {
            Call(menu, "CloseDeckExchange"); Set(menu, "auxiliaryDialogOpen", false);
            Set(menu, "currentDeck", originalDeck); decks.Clear(); decks.AddRange(originals);
            Call(menu, "RefreshDeckDropdown", originalDeck?.name); menu.enabled = enabled;
            GamePreferences.SetLanguage(language); GamePreferences.SetScale(scale);
            File.WriteAllLines(output + "/steps.txt", steps);
        }
        if (failures.Count > 0) throw new Exception(string.Join("\n", failures));
    }
    public static void RunBattlefield(string output, string paymentInput = null)
    {
        if (!Application.isPlaying) throw new Exception("Run in battlefield Play mode");
        Directory.CreateDirectory(output);
        var ui = UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>() ?? new GameObject("BrowseBoundaryUI").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(Battlefield(ui, output, paymentInput), output));
    }
    static IEnumerator Battlefield(ForgeBattlefieldController ui, string output, string paymentInput)
    {
        bool enabled = ui.enabled; ui.enabled = false;
        var failures = new List<string>(); var steps = new List<string>(); int sent = 0;
        void Check(bool ok, string why) { steps.Add((ok ? "PASS " : "FAIL ") + why); if (!ok) failures.Add(why); }
        ForgeSeatView Read(string path) => JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(path));
        try
        {
            while (!GamePreferences.Ready) yield return null;
            foreach (string zone in new[] { "坟场", "放逐", "牌库", "手牌" })
            {
                var state = Read(paymentInput ?? ".tools/cost-unification-1014/multiple-payment.json");
                if (paymentInput == null)
                    state.players = Read(".tools/zone-browser-1007/inputs/grave.json").players; // Public browser content adapter.
                int local = state.seat;
                ui.Present(null, local, false, null, null, ""); ui.Present(state, local, true, _ => sent++, null, ""); yield return Frame();
                Call(ui, "OpenZoneBrowser", local, zone); yield return Frame();
                var close = Field<RectTransform>(ui, "selectionFooter").Find("CloseReviewCards").GetComponent<Button>();
                Check(close.interactable, zone + " initially closable during mana payment");
                var pending = JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(state)); pending.inputPending = true;
                ui.Present(pending, local, false, _ => sent++, null, ""); yield return Frame();
                Check(close.interactable, zone + " close remains enabled during server acknowledgement");
                ui.Present(state, local, true, _ => sent++, null, ""); yield return Frame();
                Check(close.interactable, zone + " same browser refresh restores/retains close");
                if (zone == "坟场") { ScreenCapture.CaptureScreenshot(output + "/payment-grave-browser.png"); yield return Frame(); }
                if (close.interactable) { Click(close.gameObject); yield return Frame(); Check(!Field<RectTransform>(ui, "cardSelectionOverlay").gameObject.activeSelf, zone + " actual close ray exits"); }
                Check(sent == 0 && Field<ForgeSeatView>(ui, "view").request.requestId == state.request.requestId, zone + " browsing never submits or changes payment request");
            }
            ui.Present(null, 1, false, null, null, "");
            var choice = Read(".tools/zone-browser-1007/inputs/mixed-grave.json");
            ui.Present(choice, choice.request.seat, true, _ => sent++, null, ""); yield return Frame();
            var inFlight = JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(choice)); inFlight.inputPending = true;
            ui.Present(inFlight, choice.request.seat, false, _ => sent++, null, ""); yield return Frame();
            Check(Field<RectTransform>(ui, "cardSelectionOverlay").GetComponentsInChildren<Button>().All(b => !b.interactable), "actual engine picker remains locked in flight");
            Check(!ui.TrySubmitPresentedOption(0, choice.request.requestId) && sent == 0, "in-flight engine choice cannot be submitted again");
        }
        finally { ui.Present(null, 1, false, null, null, ""); ui.enabled = enabled; File.WriteAllLines(output + "/steps.txt", steps); }
        if (failures.Count > 0) throw new Exception(string.Join("\n", failures));
    }
    public static void RunBrowserControls(string output)
    {
        if(!Application.isPlaying)throw new Exception("Run in battlefield Play mode");
        Directory.CreateDirectory(output);
        var ui=UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>()??new GameObject("BrowserControlsBoundary").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(BrowserControls(ui,output),output));
    }
    static IEnumerator BrowserControls(ForgeBattlefieldController ui,string output)
    {
        bool enabled=ui.enabled;ui.enabled=false;int sent=0;var steps=new List<string>();
        void Check(bool ok,string why){steps.Add((ok?"PASS ":"FAIL ")+why);if(!ok)throw new Exception(why);}
        try {
            while(!GamePreferences.Ready)yield return null;
            var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(".tools/ui-browsing-boundary-1014/actual-server-payment.json"));
            ui.Present(null,v.seat,false,null,null,"");ui.Present(v,v.seat,true,_=>sent++,null,"");yield return Frame();
            Call(ui,"OpenZoneBrowser",v.seat,"坟场");yield return Frame();
            int cancel=Array.FindIndex(v.request.options,o=>o.id=="cancel");
            Check(cancel>=0&&ui.TrySubmitPresentedOption(cancel,v.request.requestId),"native payment cancel submits once while browser open");
            var close=Field<RectTransform>(ui,"selectionFooter").Find("CloseReviewCards").GetComponent<Button>();
            Check(close.interactable&&sent==1,"ChooseCore locks rule actions and retains browser close");Click(close.gameObject);yield return Frame();
            Check(!Field<RectTransform>(ui,"cardSelectionOverlay").gameObject.activeSelf,"close ray exits locally submitted payment browser");
            ui.Present(v,v.seat,true,_=>sent++,null,"");
            var card=v.players.SelectMany(p=>p.graveyardCards??Array.Empty<ForgeCardView>()).First();
            // More than one page is a display adapter, not new game objects.
            var cards=Enumerable.Range(0,13).Select(i=>{var c=JsonUtility.FromJson<ForgeCardView>(JsonUtility.ToJson(card));c.id=10000+i;return c;}).ToArray();
            Call(ui,"ShowReviewCards","只读分页边界",cards);yield return Frame();
            var panel=Field<RectTransform>(ui,"selectionPanel");Click(panel.Find("CardSelectionScroll/Content/ChoiceCard_10000/Artwork").gameObject);yield return Frame();
            var pending=JsonUtility.FromJson<ForgeSeatView>(JsonUtility.ToJson(v));pending.inputPending=true;ui.Present(pending,v.seat,false,_=>sent++,null,"");yield return Frame();
            var detail=Field<RectTransform>(ui,"endDetail");var detailClose=detail.GetComponentsInChildren<Button>().Single();
            Check(detailClose.interactable,"detail close remains enabled in flight");Click(detailClose.gameObject);yield return Frame();
            Check(detail==null,"detail close ray destroys local window");
            Check(panel.Find("NextPage").GetComponent<Button>().interactable,"next page remains enabled in flight");Click(panel.Find("NextPage").gameObject);yield return Frame();
            panel=Field<RectTransform>(ui,"selectionPanel");Check(Field<int>(ui,"selectionPage")==1,"next page ray changes only local page");
            Check(panel.Find("PreviousPage").GetComponent<Button>().interactable,"previous page remains enabled in flight");Click(panel.Find("PreviousPage").gameObject);yield return Frame();
            Check(Field<int>(ui,"selectionPage")==0&&sent==1,"previous page and detail never submit native input");
            var picker=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(".tools/zone-browser-1007/inputs/mixed-grave.json"));ui.Present(picker,picker.seat,true,_=>sent++,null,"");yield return Frame();
            Check(!Field<bool>(ui,"cardSelectionReadOnly"),"real picker replaces readonly state");
            picker.inputPending=true;ui.Present(picker,picker.seat,false,_=>sent++,null,"");yield return Frame();
            Check(Field<RectTransform>(ui,"cardSelectionOverlay").GetComponentsInChildren<Button>().All(b=>!b.interactable),"replacement native picker freezes every action");
        } finally {ui.Present(null,1,false,null,null,"");ui.enabled=enabled;File.WriteAllLines(output+"/steps.txt",steps);}
    }
    public static void RunStartupAndLand(string output)
    {
        if (!Application.isPlaying) throw new Exception("Run in fresh battlefield Play mode");
        Directory.CreateDirectory(output);
        var ui = UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>() ?? new GameObject("StartupLandBoundary").AddComponent<ForgeBattlefieldController>();
        ui.StartCoroutine(Guard(StartupAndLand(ui, output), output));
    }
    static IEnumerator StartupAndLand(ForgeBattlefieldController ui, string output)
    {
        bool enabled=ui.enabled;ui.enabled=false;string language=GamePreferences.Language;
        var steps=new List<string>();var failures=new List<string>();
        void Check(bool ok,string why){steps.Add((ok?"PASS ":"FAIL ")+why);if(!ok)failures.Add(why);}
        try {
            Call(ui,"Build");var authored=Field<Transform>(ui,"authored");
            Check(!authored.Find("ForgeChoices").gameObject.activeSelf,"no empty choices panel before opening projection");
            ScreenCapture.CaptureScreenshot(output+"/startup.png");yield return Frame();
            foreach(string locale in new[]{"zh-CN","ja-JP","en-US"}) {
                GamePreferences.SetLanguage(locale);while(!GamePreferences.Ready)yield return null;
                foreach(string state in new[]{"unchanged","moon","restored","omen","omen-restored"}) {
                    var v=JsonUtility.FromJson<ForgeSeatView>(File.ReadAllText(".tools/ui-browsing-boundary-1014/land-native/"+state+"-view.json"));
                    ui.Present(v,1,false,null,null,"");yield return Frame();
                    var cards=state is "moon" or "omen"?v.players[0].battlefield.Where(c=>c.name=="Wooded Foothills"):v.players[0].battlefield.Where(c=>c.land);
                    foreach(var card in cards) {
                        Call(ui,"InspectIntegrated",card);yield return Frame();
                        var label=authored.Find("InspectPanel/Rules").GetComponent<Text>();string text=label.text;
                        bool changed=state is "moon" or "omen";
                        Check(text.Contains("#70BFFF")==changed,locale+"/"+state+"/"+card.name+" real ability tint");
                        Check(text.Contains("#565C65")== (state=="moon"),locale+"/"+state+"/"+card.name+" real lost ability strike");
                        if(locale!="en-US")Check(!text.Contains("Add ")&&!text.Contains("Search your library")&&!text.Contains("pay 2 life"),locale+"/"+state+"/"+card.name+" printed and generated rules localized");
                        if(changed){
                            var original=authored.Find("InspectPanel/OriginalState").GetComponent<Button>();Click(original.gameObject);yield return Frame();
                            Check(!label.text.Contains("<color="),locale+"/"+state+" original state has no change tint");
                            Click(original.gameObject);yield return Frame();
                        }
                        if(locale=="zh-CN"&&(card.name=="Godless Shrine"&&state=="unchanged"||changed)) {ScreenCapture.CaptureScreenshot(output+"/"+state+"-land.png");yield return Frame();}
                    }
                }
                var mana=GamePreferences.NativeText("{T}: Add {W}.");
                Check(mana.Contains("{T}")&&mana.Contains("{W}")&&(locale=="en-US"?mana=="{T}: Add {W}.":!mana.Contains("Add")),locale+" fixed mana production preserves cost/symbols");
                string restricted=GamePreferences.NativeText("Add {W}. Spend this mana only to cast creature spells.");
                Check(restricted.Contains("{W}")&&(restricted.Contains("Spend")||restricted.Contains("仅")||restricted.Contains("のみ")),locale+" restricted mana prose not erased");
            }
        } finally {ui.Present(null,1,false,null,null,"");ui.enabled=enabled;GamePreferences.SetLanguage(language);File.WriteAllLines(output+"/steps.txt",steps);}
        if(failures.Count>0)throw new Exception(string.Join("\n",failures));
    }
    static IEnumerator Guard(IEnumerator work, string output)
    {
        File.WriteAllText(output + "/result.txt", "RUNNING");
        var stack = new Stack<IEnumerator>(); stack.Push(work);
        while (stack.Count > 0)
        {
            object next;
            try { var current = stack.Peek(); if (!current.MoveNext()) { (current as IDisposable)?.Dispose(); stack.Pop(); continue; } next = current.Current; }
            catch (Exception e) { File.WriteAllText(output + "/result.txt", "FAIL " + e); throw; }
            if (next is IEnumerator nested) { stack.Push(nested); continue; }
            yield return next;
        }
        File.WriteAllText(output + "/result.txt", "PASS bounded UI checks; no new native rule or live server proof");
    }
}
#endif
