using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Remembers the selection before InputField clears its caret on deselection.
[DefaultExecutionOrder(100)]
public sealed class SearchInsertionMenu : MonoBehaviour
{
    public sealed class Group
    {
        public string Name;
        public string[] Values;
        public Group(string name, params string[] values) { Name = name; Values = values; }
    }
    InputField input;
    Button trigger;
    Transform host;
    bool mana;
    int anchor, focus, selected;
    int pendingCaret = -1;
    GameObject popup;
    RectTransform items;
    ScrollRect scroll;
    InputField search;
    Group[] groups;
    readonly List<Button> categories = new List<Button>();
    public bool IsOpen => popup != null;
    public void Initialize(InputField field, Button button, Transform root, bool symbols)
    {
        input = field; trigger = button; host = root; mana = symbols;
        anchor = focus = input.text.Length;
        input.onEndEdit.AddListener(_ => Capture());
        trigger.onClick.AddListener(Open);
    }
    void Capture() { anchor = input.selectionAnchorPosition; focus = input.selectionFocusPosition; }
    void LateUpdate()
    {
        // InputField activates/selects all in its LateUpdate (default order 0).
        if (pendingCaret >= 0 && input.isFocused)
        {
            input.selectionAnchorPosition = input.selectionFocusPosition = pendingCaret;
            pendingCaret = -1; input.ForceLabelUpdate();
        }
        if (input.isFocused) Capture();
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }
    public static Group[] ManaGroups() => new[]
    {
        new Group("五色法术力", "{W}", "{U}", "{B}", "{R}", "{G}"),
        new Group("通用费用", Enumerable.Range(0, 21).Select(i => "{" + i + "}").ToArray()),
        new Group("变量费用", "{X}", "{Y}", "{Z}"),
        new Group("无色与雪", "{C}", "{S}"),
        new Group("双色混血", "{W/U}", "{W/B}", "{U/B}", "{U/R}", "{B/R}", "{B/G}", "{R/G}", "{R/W}", "{G/W}", "{G/U}"),
        new Group("二费混血", "{2/W}", "{2/U}", "{2/B}", "{2/R}", "{2/G}"),
        new Group("非瑞克西亚", "{W/P}", "{U/P}", "{B/P}", "{R/P}", "{G/P}"),
        new Group("混血非瑞", "{W/U/P}", "{W/B/P}", "{U/B/P}", "{U/R/P}", "{B/R/P}", "{B/G/P}", "{R/G/P}", "{R/W/P}", "{G/W/P}", "{G/U/P}"),
        new Group("无色混血", "{C/W}", "{C/U}", "{C/B}", "{C/R}", "{C/G}")
    };
    static Group[] TypeGroups()
    {
        var creatures = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var lands = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var walkers = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var other = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(CardType type, string subtypes)
        {
            var target = (type & CardType.Creature) != 0 ? creatures : (type & CardType.Land) != 0 ? lands : (type & CardType.Planeswalker) != 0 ? walkers : other;
            // Supplementary card types (planes, dungeons, etc.) can have a
            // multiword proper subtype; it is not a list of creature types.
            if(type==CardType.None)
            { if(!string.IsNullOrWhiteSpace(subtypes))target.Add(subtypes.Trim());return; }
            foreach (var token in (subtypes ?? "").Replace("Time Lord", "Time_Lord").Split(new[] { ' ', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                target.Add(token.Replace("Time_Lord", "Time Lord"));
        }
        foreach (var card in CardDatabaseRepository.LoadAll())
        {
            Add(card.cardTypes, card.subtypes);
            if (card.features != null) foreach (var face in card.features) if (face != null) Add(face.cardTypes, face.subtypes);
        }
        return new[] {
            new Group("卡牌类别", Enum.GetNames(typeof(CardType)).Where(n => n != "None").ToArray()),
            new Group("超类别", Enum.GetNames(typeof(CardSupertype)).Where(n => n != "None").ToArray()),
            new Group("生物副类别", creatures.ToArray()), new Group("地副类别", lands.ToArray()),
            new Group("鹏洛客副类别", walkers.ToArray()), new Group("其他副类别", other.ToArray()) };
    }
    public void Open()
    {
        if (IsOpen) { Close(); return; }
        if (input.isFocused) Capture();
        groups = groups ?? (mana ? ManaGroups() : TypeGroups());
        var blocker = ReplayWidgets.Panel(host, "InsertionPopup", 0, 0, 0, 0, Color.clear);
        blocker.anchorMin = Vector2.zero; blocker.anchorMax = Vector2.one; blocker.offsetMin = blocker.offsetMax = Vector2.zero;
        popup = blocker.gameObject;
        // Dismiss is a sibling behind the menu, never an ancestor click handler.
        // Scrollbar releases can remain eligible for clicks after dragging.
        var dismiss = ReplayWidgets.Panel(blocker, "Dismiss", 0, 0, 0, 0, Color.clear);
        dismiss.anchorMin = Vector2.zero; dismiss.anchorMax = Vector2.one;
        dismiss.offsetMin = dismiss.offsetMax = Vector2.zero;
        dismiss.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        // Sibling of the scroll view: the menu is not clipped by its mask.
        var panel = ReplayWidgets.Panel(blocker, "Menu", 0, 0, 900, 600, MenuVisualTheme.Raised);
        var hostRect = (RectTransform)host;
        var corners = new Vector3[4]; ((RectTransform)trigger.transform).GetWorldCorners(corners);
        Vector2 bottomRight = hostRect.InverseTransformPoint(corners[3]);
        var bounds = hostRect.rect;
        float x = Mathf.Clamp(bottomRight.x - 450, bounds.xMin + 462, bounds.xMax - 462);
        float y = bottomRight.y - 304;
        if (y - 300 < bounds.yMin + 12) y = hostRect.InverseTransformPoint(corners[2]).y + 304;
        panel.anchoredPosition = new Vector2(x, Mathf.Clamp(y, bounds.yMin + 312, bounds.yMax - 312));
        ReplayWidgets.Label(panel, mana ? "插入法术力符号" : "插入类别", -28, 264, 820, 44, 24);
        ReplayWidgets.Button(panel, "×", 418, 264, 40, Close);
        categories.Clear();
        for (int i = 0; i < groups.Length; i++)
        {
            int index = i;
            float height=mana?46:72,step=mana?50:76;
            int size=mana?20:22;
            var button = ReplayWidgets.Button(panel, groups[i].Name, -298, (mana?220:200) - i * step, 272, () => { selected = index; Render(); });
            ((RectTransform)button.transform).sizeDelta = new Vector2(272, height);
            var label = button.GetComponentInChildren<Text>(); label.rectTransform.sizeDelta = new Vector2(256, height-2);
            label.resizeTextForBestFit=true;label.resizeTextMinSize=label.resizeTextMaxSize=size;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;
            label.GetComponent<PreferenceText>().SetBaseSize(size);
            categories.Add(button);
        }
        search = ReplayWidgets.Input(panel, "", 146, 218, 560);
        search.gameObject.name = "InsertionSearch";
        var placeholder = ReplayWidgets.Label(search.transform, "搜索选项", 0, 0, 536, 42, 22);
        placeholder.color = MenuVisualTheme.Muted; search.placeholder = placeholder;
        search.onValueChanged.AddListener(_ => Render());
        var viewport = ReplayWidgets.Panel(panel, "Options", 146, -36, 568, 436, MenuVisualTheme.Panel);
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        items = ReplayWidgets.Rect(viewport, "Items", 0, 0, 556, 436);
        items.anchorMin = items.anchorMax = new Vector2(.5f, 1); items.pivot = new Vector2(.5f, 1);
        scroll.content = items; scroll.viewport = viewport; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
        var track = ReplayWidgets.Panel(panel, "ScrollTrack", 434, -36, 8, 436, MenuVisualTheme.Ink);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        var handle = ReplayWidgets.Panel(track, "Handle", 0, 0, 0, 0, MenuVisualTheme.Muted);
        handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.offsetMin = handle.offsetMax = Vector2.zero;
        bar.handleRect = handle; bar.targetGraphic = handle.GetComponent<Image>(); bar.direction = Scrollbar.Direction.BottomToTop; scroll.verticalScrollbar = bar;
        Render(); GamePreferences.BindScene();
    }
    static readonly Dictionary<string, string> Labels = new Dictionary<string, string> {
        {"Artifact", "神器"}, {"Battle", "战役"}, {"Creature", "生物"}, {"Enchantment", "结界"}, {"Instant", "瞬间"},
        {"Kindred", "亲缘"}, {"Land", "地"}, {"Planeswalker", "鹏洛客"}, {"Sorcery", "法术"},
        {"Basic", "基本"}, {"Legendary", "传奇"}, {"Ongoing", "持续"}, {"Snow", "雪境"}, {"World", "世界"},
        {"Human", "人类"}, {"Elf", "妖精"}, {"Goblin", "鬼怪"}, {"Dragon", "龙"}, {"Zombie", "灵俑"},
        {"Vampire", "吸血鬼"}, {"Angel", "天使"}, {"Wizard", "法术师"}, {"Warrior", "战士"}, {"Soldier", "士兵"},
        {"Island", "海岛"}, {"Plains", "平原"}, {"Swamp", "沼泽"}, {"Mountain", "山脉"}, {"Forest", "树林"}
    };
    [Serializable] sealed class TypeLabel { public string en,zh,ja; }
    [Serializable] sealed class TypeLabels { public TypeLabel[] entries; }
    static Dictionary<string,TypeLabel> localizedTypes;
    static string Label(string value)
    {
        if(localizedTypes==null)
        {
            var asset=Resources.Load<TextAsset>("TokenTypeAliases");
            localizedTypes=asset==null?new Dictionary<string,TypeLabel>(StringComparer.OrdinalIgnoreCase):
                JsonUtility.FromJson<TypeLabels>(asset.text).entries.GroupBy(e=>e.en,StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
            var vocabulary=Resources.Load<TextAsset>("SearchTypeLabels");
            if(vocabulary!=null)foreach(var term in JsonUtility.FromJson<TypeLabels>(vocabulary.text).entries)
            {
                if(localizedTypes.TryGetValue(term.en,out var existing))
                { if(!string.IsNullOrEmpty(term.zh))existing.zh=term.zh; if(!string.IsNullOrEmpty(term.ja))existing.ja=term.ja; }
                else localizedTypes[term.en]=term;
            }
        }
        string translated=value;
        if(localizedTypes.TryGetValue(value,out var entry))
            translated=GamePreferences.Language=="ja-JP"?entry.ja:GamePreferences.Language=="zh-CN"?entry.zh:value;
        if(string.IsNullOrEmpty(translated)||translated==value)
            translated=Labels.TryGetValue(value,out string key)?GamePreferences.T(key):value;
        return translated.Equals(value, StringComparison.OrdinalIgnoreCase) ? value : translated + " · " + value;
    }
    void Render()
    {
        foreach (Transform child in items) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        for (int i = 0; i < categories.Count; i++) categories[i].GetComponent<Image>().color = i == selected ? MenuVisualTheme.Primary : MenuVisualTheme.Panel;
        string term = search.text.Trim();
        var values = (term.Length == 0 ? groups[selected].Values : groups.SelectMany(g => g.Values).Distinct()).Where(v => Label(v).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        int columns = mana ? 3 : 1;
        float offset=0,rowHeight=0;
        for (int i = 0; i < values.Length; i++)
        {
            if(i%columns==0&&i>0){offset+=rowHeight+6;rowHeight=0;}
            string value = values[i]; float width = mana ? 172 : 544;
            var button = ReplayWidgets.Button(items, Label(value), mana ? -184 + i % 3 * 184 : 0, 0, width, () => Insert(value));
            button.name = "Insert_" + value;
            var r = (RectTransform)button.transform; r.anchorMin = r.anchorMax = new Vector2(.5f, 1);
            var text = button.GetComponentInChildren<Text>(); text.rectTransform.sizeDelta = new Vector2(width - 16, 52);
            text.resizeTextForBestFit=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;
            text.GetComponent<PreferenceText>().SetBaseSize(24);
            float height=Mathf.Max(60,Mathf.Ceil(text.preferredHeight)+12);
            r.sizeDelta=new Vector2(width,height);r.anchoredPosition=new Vector2(r.anchoredPosition.x,-offset-height/2-4);
            text.rectTransform.sizeDelta=new Vector2(width-16,height-12);
            rowHeight=Mathf.Max(rowHeight,height);
        }
        items.sizeDelta=new Vector2(556,Mathf.Max(436,offset+rowHeight+8));
        if (values.Length == 0) { var label = ReplayWidgets.Label(items, "没有匹配的选项", 0, -30, 412, 48, 17); label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 1); }
        Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1;
    }
    public static string InsertText(string text, int anchor, int focus, string token, bool separateWords, out int caret)
    {
        int start = Mathf.Clamp(Math.Min(anchor, focus), 0, text.Length), end = Mathf.Clamp(Math.Max(anchor, focus), start, text.Length);
        string before = text.Substring(0, start), after = text.Substring(end);
        string insertion = (separateWords && before.Length > 0 && !char.IsWhiteSpace(before[before.Length - 1]) ? " " : "") + token
            + (separateWords && after.Length > 0 && !char.IsWhiteSpace(after[0]) ? " " : "");
        caret = before.Length + insertion.Length;
        return before + insertion + after;
    }
    public void Insert(string value)
    {
        string next = InsertText(input.text, anchor, focus, value, !mana, out int caret);
        if (next.Length > input.characterLimit && input.characterLimit > 0) return;
        input.text = next; anchor = focus = caret;
        Close(); pendingCaret = caret; input.ActivateInputField();
    }
    public void Close() { if (popup != null) { popup.SetActive(false); Destroy(popup); popup = null; } }
    void OnDestroy() { Close(); }
}
