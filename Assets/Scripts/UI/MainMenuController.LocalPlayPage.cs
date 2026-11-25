using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class MainMenuController
{
    private Canvas localPlayCanvas;

    private void OpenLocalPlayPage()
    {
        if (AwaitCardData(OpenLocalPlayPage)) return;
        SetScreenVisible(startScreenGroup, false);
        SetScreenVisible(deckBuilderGroup, false);
        var root = ShowLocalPage("LocalModes", "LocalModesPage", AiText("本地模式","Local play","ローカルモード"),
            AiText("返回主菜单","Main menu","メインメニュー"), () => {
                CloseLocalPage();
                ShowStartScreenNow();
            });
        var heading = ReplayWidgets.Label(root, AiText("选择玩法","Choose a mode","モード選択"),
            -400, 274, 840, 56, 30);
        MenuTypography.Use(heading, MenuTypography.Face.Heading);
        var modes = ReplayWidgets.Rect(root, "Modes", 0, -80, 1640, 620);
        var grid = modes.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(520, 140);
        grid.spacing = new Vector2(34, 26);
        AddLocalMode(modes, "LocalDuelMode", LocalDuelTitle(),
            AiText("选择双方卡组与对局规则","Choose both decks and the match rules","双方のデッキと対戦ルールを選択"), OpenLocalDuelSetup);
        AddLocalMode(modes, "LocalPuzzleMode", AiText("残局挑战","Puzzles","詰めマジック"),
            AiText("挑战预设局面，或创作自己的残局","Play or create a starting position","局面に挑戦・オリジナル問題を作成"), () => OpenPuzzleLibrary());
    }

    private static string LocalDuelTitle() => AiText("1v1 人机对战","1v1 vs AI","1対1 AI対戦");

    private static void AddLocalMode(Transform modes, string name, string title, string description, Action choose)
    {
        var button = ReplayWidgets.Button(modes, title, 0, 0, 520, choose);
        button.name = name;
        MenuVisualTheme.StyleDeckTool(button);
        var label = button.GetComponentInChildren<Text>();
        label.alignment = TextAnchor.MiddleLeft;
        label.rectTransform.sizeDelta = new Vector2(456, 54);
        label.rectTransform.anchoredPosition = new Vector2(0, 24);
        label.resizeTextForBestFit = false;
        PreferenceText.SetSize(label, 29);
        var detail = ReplayWidgets.Label(button.transform, description, 0, -27, 456, 42, 20);
        detail.color = MenuVisualTheme.Silver;
    }

    private RectTransform ShowLocalPage(string canvasName, string pageName, string title, string back, Action onBack)
    {
        CloseLocalPage();
        localPlayCanvas = ReplayWidgets.Canvas(canvasName, 550);
        return MenuVisualTheme.ArchivePage(localPlayCanvas, pageName, title, back, onBack);
    }

    private void CloseLocalPage()
    {
        if (localPlayCanvas == null) return;
        localPlayCanvas.gameObject.SetActive(false);
        Destroy(localPlayCanvas.gameObject);
        localPlayCanvas = null;
    }

    private void OpenLocalDuelSetup()
    {
        var decks = DeckRepository.LoadAll().ToArray();
        var root = ShowLocalPage("LocalAiSetup", "LocalPlayPage", LocalDuelTitle(),
            AiText("返回玩法选择","Back to modes","モード選択に戻る"), OpenLocalPlayPage);
        root.GetComponentInChildren<Button>().name = "LocalPlayBack";
        var rules = MenuVisualTheme.ArchiveSection(root, "Rules", AiText("对局规则","Game rules","対戦ルール"),
            -484, -12, 672, 664, MenuVisualTheme.LibraryAccent);
        var players = MenuVisualTheme.ArchiveSection(root, "Decks", AiText("双方卡组","Players and decks","対戦デッキ"),
            348, -12, 944, 664, MenuVisualTheme.MainAccent);

        Dropdown Pick(Transform parent, string name, float x, float y, float width, string[] options)
        {
            var d = Instantiate(deckDropdown, parent);
            d.name = name;
            d.onValueChanged = new Dropdown.DropdownEvent();
            var rect = (RectTransform)d.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, 56);
            d.ClearOptions();
            d.AddOptions(options.ToList());
            d.RefreshShownValue();
            MenuVisualTheme.StyleArchiveDropdown(d);
            PreferenceText.SetSize(d.captionText, 22);
            return d;
        }
        void RuleLabel(string value, float y)
        {
            var label = ReplayWidgets.Label(rules, value, -206, y, 190, 56, 23);
            label.color = MenuVisualTheme.Silver;
        }

        RuleLabel(AiText("赛制","Format","フォーマット"), 182);
        var formats = DeckFormatLegality.OnlineKeys;
        var format = Pick(rules, "LocalFormat", 104, 182, 396, formats.Select(f => f == "none"
            ? AiText("不限赛制","Unrestricted","制限なし") : GamePreferences.T(DeckFormatLegality.FormatName(f))).ToArray());
        format.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(formats, currentDeck?.format)));
        format.RefreshShownValue();
        RuleLabel(AiText("初始生命","Starting life","初期ライフ"), 90);
        var life = ReplayWidgets.Input(rules, formats[format.value] == "brawl" ? "25" : "20", 104, 90, 396);
        life.name = "LocalStartingLife";
        life.contentType = InputField.ContentType.IntegerNumber;
        life.characterLimit = 3;
        MenuVisualTheme.StyleSearchInput(life);
        RuleLabel(AiText("先手","Play first","先攻"), -2);
        var first = Pick(rules, "LocalFirstPlayer", 104, -2, 396, new[] {
            AiText("随机","Random","ランダム"), AiText("你先手","You play first","自分が先攻"), AiText("AI 先手","AI plays first","AIが先攻") });
        RuleLabel(AiText("洗牌","Shuffling","シャッフル"), -94);
        var shuffle = Pick(rules, "LocalShuffle", 104, -94, 396, new[] {
            AiText("正常洗牌","Shuffle normally","通常のシャッフル"), AiText("整局不洗切卡组","Keep saved order all game","対戦中はシャッフルしない") });
        var hint = ReplayWidgets.Label(rules, AiText("初始生命对双方生效。","Starting life applies to both players.","初期ライフは両者に適用されます。"), 0, -191, 604, 58, 18);
        hint.color = MenuVisualTheme.Stone;

        var options = decks.Length == 0 ? new[] { AiText("没有已保存卡组","No saved decks","保存済みデッキなし") } : decks.Select(d => d.name).ToArray();
        Dropdown Seat(string name, string title, float y, int index, Color accent)
        {
            var seat = ReplayWidgets.Panel(players, name + "Seat", 0, y, 880, 210, MenuVisualTheme.ArchivePanel);
            var stripe = ReplayWidgets.Panel(seat, "SeatAccent", -438, 0, 3, 210, accent);
            stripe.GetComponent<Image>().raycastTarget = false;
            var badge = ReplayWidgets.Label(seat, title, -146, 65, 510, 48, 24);
            MenuTypography.Use(badge, MenuTypography.Face.Heading);
            badge.color = accent;
            var deck = Pick(seat, name, -146, 0, 510, options);
            deck.SetValueWithoutNotify(Mathf.Max(0, index));
            deck.RefreshShownValue();
            deck.interactable = decks.Length > 0;
            var info = ReplayWidgets.Label(seat, "", -146, -66, 510, 50, 19);
            info.color = MenuVisualTheme.Stone;
            var previews = new Image[3];
            for (int i = 0; i < previews.Length; i++)
            {
                var preview = ReplayWidgets.Rect(seat, "Card" + i, 192 + i * 65, i == 1 ? 5 : -2, 108, 154);
                preview.localRotation = Quaternion.Euler(0, 0, (1 - i) * 6);
                previews[i] = preview.gameObject.AddComponent<Image>();
                previews[i].preserveAspect = true;
                previews[i].raycastTarget = false;
            }
            void ShowDeck(int selection)
            {
                var chosen = decks.Length > 0 ? decks[selection] : null;
                if (chosen != null)
                    info.text = AiText("主牌 ","Main ","メイン ") + chosen.cards.Sum(c => c.count) + "   ·   "
                        + AiText("备牌 ","Sideboard ","サイド ") + chosen.sideboard.Sum(c => c.count)
                        + "   ·   " + GamePreferences.T(DeckFormatLegality.FormatName(chosen.format));
                int filled = 0;
                if (chosen != null)
                    foreach (var entry in chosen.commanders.Concat(chosen.cards).Where(e => e != null && e.count > 0).Take(3))
                    {
                        var card = DeckRepository.ResolveCard(entry, cards);
                        if (!CardImageLibrary.TryLoadThumbnail(card, out var sprite)) continue;
                        previews[filled++].sprite = sprite;
                    }
                for (int i = 0; i < previews.Length; i++) previews[i].enabled = i < filled;
            }
            deck.onValueChanged.AddListener(ShowDeck);
            ShowDeck(deck.value);
            return deck;
        }
        int selected = Mathf.Max(0, Array.FindIndex(decks, d => d.name == currentDeck?.name));
        var own = Seat("LocalPlayerDeck", AiText("你的卡组","Your deck","自分のデッキ"), 111, selected, MenuVisualTheme.MainAccent);
        var other = Seat("LocalAiDeck", AiText("AI 卡组","AI deck","AIのデッキ"), -119, decks.Length > 1 ? (selected + 1) % decks.Length : 0, MenuVisualTheme.SideAccent);
        var check = ReplayWidgets.Label(players, AiText("双方卡组按所选赛制检查。","Both decks are checked against the selected format.","両デッキを選択したフォーマットで検証します。"), 0, -276, 876, 58, 18);
        check.color = MenuVisualTheme.Stone;

        var status = ReplayWidgets.Label(root, "", -200, -402, 1240, 80, 21);
        status.name = "LocalPlayStatus";
        status.color = MenuVisualTheme.Warning;
        var begin = ReplayWidgets.Button(root, AiText("开始对局","Start match","対戦開始"), 665, -402, 310, () => {
            try {
                var config = new OnlineRoomConfig { formatId = formats[format.value], startingLife = int.Parse(life.text),
                    firstPlayerSeat = first.value, noShuffle = shuffle.value == 1 };
                OnlineMatchService.EnsureInstance().StartLocalAiMatch(decks[own.value], decks[other.value], settings:config);
                CloseLocalPage();
            } catch (Exception error) { status.text = error.Message; }
        }, true);
        begin.name = "LocalPlayStart";
        ((RectTransform)begin.transform).sizeDelta = new Vector2(310, 68);
        MenuVisualTheme.StyleDeckTool(begin, true);
        void Refresh()
        {
            bool valid = int.TryParse(life.text, out int value) && value >= 1 && value <= 200;
            begin.interactable = decks.Length > 0 && valid;
            status.text = decks.Length == 0 ? AiText("请先在卡组编辑中保存卡组。","Save a deck in the deck editor first.","先にデッキ編集で保存してください。")
                : !valid ? AiText("请输入1～200的初始生命。","Enter starting life from 1 to 200.","初期ライフを1～200で入力してください。") : "";
        }
        life.onValueChanged.AddListener(_ => Refresh());
        format.onValueChanged.AddListener(_ => { life.text = formats[format.value] == "brawl" ? "25" : "20"; Refresh(); });
        Refresh();
    }
}
