using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed class AdvancedSearchPanel : MonoBehaviour
{
    AdvancedCardSearch draft;
    Dropdown template;
    RectTransform content;
    Text message;
    Button apply;
    GameObject setPicker;
    Action<AdvancedCardSearch> submit;
    bool commanderAvailable,companionAvailable;
    readonly List<Action> read = new List<Action>();
    readonly List<Action> colorLabels = new List<Action>();
    void OnEnable() { GamePreferences.Changed += RefreshColorLabels; }
    void OnDisable() { GamePreferences.Changed -= RefreshColorLabels; }
    void RefreshColorLabels() { foreach (var refresh in colorLabels) refresh(); }
    public static void Show(Canvas canvas, Dropdown dropdownTemplate, AdvancedCardSearch state, Action<AdvancedCardSearch> submit, bool commanderAvailable = false, bool companionAvailable = false)
    {
        if (!Application.isPlaying) return;
        var overlay = ReplayWidgets.Panel(canvas.transform, "AdvancedSearch", 0, 0, 0, 0, new Color(0, 0, 0, .8f));
        overlay.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one; overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        var view = overlay.gameObject.AddComponent<AdvancedSearchPanel>();
        view.template = dropdownTemplate; view.submit = submit;
        view.commanderAvailable=commanderAvailable;view.companionAvailable=companionAvailable;
        view.Build(state.Copy());
    }
    void Build(AdvancedCardSearch state)
    {
        colorLabels.Clear();
        draft = state;
        var panel = ReplayWidgets.Panel(transform, "SearchPanel", 0, 0, 1120, 940, MenuVisualTheme.Panel);
        ReplayWidgets.Label(panel, "高级搜索", 0, 424, 1032, 48, 28);
        ReplayWidgets.Label(panel, "条件同时生效；留空表示不限", 0, 378, 1032, 38, 17);
        var viewport = ReplayWidgets.Panel(panel, "Viewport", 0, 0, 1040, 700, MenuVisualTheme.Panel);
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        content = ReplayWidgets.Rect(viewport, "Fields", 0, 0, 1020, 1472);
        content.anchorMin = content.anchorMax = new Vector2(.5f, 1); content.pivot = new Vector2(.5f, 1);
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
        var track = ReplayWidgets.Panel(panel, "ScrollTrack", 534, 0, 10, 700, MenuVisualTheme.Ink);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        var handle = ReplayWidgets.Panel(track, "Handle", 0, 0, 0, 0, MenuVisualTheme.Muted);
        handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.offsetMin = handle.offsetMax = Vector2.zero;
        bar.handleRect = handle; bar.targetGraphic = handle.GetComponent<Image>(); bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar;
        Field(0, 0, "卡名", draft.Name, v => draft.Name = v);
        Field(1, 0, "效果文本", draft.Rules, v => draft.Rules = v);
        Field(0, 1, "类别与副类别", draft.Type, v => draft.Type = v, false);
        Heading(1, 1, DeckCompanion.Text("牌组适配（可关闭）","Deck fit (optional)","デッキ適合（任意）"));
        ConstructionToggle("CommanderFit",DeckCompanion.Text("主将适配","Commander fit","統率者適合"),130,draft.CommanderFit,commanderAvailable,v=>draft.CommanderFit=v);
        ConstructionToggle("CompanionFit",DeckCompanion.Text("行侣适配","Companion fit","相棒適合"),390,draft.CompanionFit,companionAvailable,v=>draft.CompanionFit=v);
        ColorRow(0, 2, "颜色", draft.Colors, v => draft.Colors = v);
        ColorRow(1, 2, "颜色标识", draft.Identity, v => draft.Identity = v);
        string[] modes = { "包含任意所选颜色", "包含全部所选颜色", "恰好这些颜色", "仅使用所选颜色（含无色）" };
        Choice(0, 3, "颜色匹配", modes, draft.ColorMode, v => draft.ColorMode = v);
        Choice(1, 3, "颜色标识匹配", modes, draft.IdentityMode, v => draft.IdentityMode = v);
        Field(0, 4, "法术力值（例如 <=3）", draft.Value, v => draft.Value = v);
        Field(1, 4, "费用符号（例如 {U}{U}）", draft.Mana, v => draft.Mana = v, true);
        Field(0, 5, "力量（例如 >=2）", draft.Power, v => draft.Power = v);
        Field(1, 5, "防御力（例如 >=2）", draft.Toughness, v => draft.Toughness = v);
        Field(0, 6, "忠诚度", draft.Loyalty, v => draft.Loyalty = v);
        Choice(1, 6, "稀有度", new[] { "不限", "普通", "非普通", "稀有", "秘稀", "特殊", "奖励" }, draft.Rarity, v => draft.Rarity = v);
        var sets = Field(0, 7, "系列（可多选）", draft.Sets, v => draft.Sets = v);
        var setsRect=(RectTransform)sets.transform;setsRect.sizeDelta=new Vector2(328,52);setsRect.anchoredPosition+=new Vector2(-82,0);
        sets.textComponent.rectTransform.sizeDelta=new Vector2(304,46);
        var browse=ReplayWidgets.Button(content,"系列一览",X(0)+170,-7*112-70,152,()=>OpenSets(sets));
        ((RectTransform)browse.transform).anchorMin=((RectTransform)browse.transform).anchorMax=new Vector2(.5f,1);
        Field(1, 7, "画师", draft.Artist, v => draft.Artist = v);
        Field(0, 8, "发行起日（YYYY-MM-DD）", draft.From, v => draft.From = v);
        Field(1, 8, "发行止日（YYYY-MM-DD）", draft.To, v => draft.To = v);
        var format = Choice(0, 9, "赛制（本地现行禁限表）", new[] { "不限" }.Concat(DeckFormatLegality.Keys.Select(DeckFormatLegality.FormatName)).ToArray(), draft.Format, v => draft.Format = v);
        Choice(1, 9, "合法性", new[] { "合法", "限一", "禁止" }, draft.Status, v => draft.Status = v);
        Choice(0, 10, "卡牌版式", new[] { "不限", "普通版式", "转化双面", "模式双面", "历险", "连体", "倒转", "融合", "传纪", "职业" }, draft.Layout, v => draft.Layout = v);
        Choice(1, 10, "卡牌来源", new[] { "全部内容", "官方卡牌", "DIY 卡牌" }, draft.Source, v => draft.Source = v);
        Choice(0, 11, "排序", new[] { "默认顺序", "卡名", "法术力值升序", "发行日期降序" }, draft.Sort, v => draft.Sort = v);
        Choice(1, 11, "重印版本", new[] { "显示全部版本", "每张卡只显示一个版本" }, draft.Unique ? 1 : 0, v => draft.Unique = v == 1);
        PlatformRow(12);
        message = ReplayWidgets.Label(panel, "正在读取筛选资料…", 0, -378, 1032, 42, 16);
        apply = ReplayWidgets.Button(panel, "查看结果", -358, -432, 240, Apply, true);
        ReplayWidgets.Button(panel, "重置条件", 0, -432, 240, () =>
        { Destroy(panel.gameObject); read.Clear(); Build(new AdvancedCardSearch()); });
        ReplayWidgets.Button(panel, "取消", 358, -432, 240, () => Destroy(gameObject));
        apply.interactable = false;
        GamePreferences.BindScene();
        Load();
    }
    async void Load()
    {
        var button = apply; var label = message;
        try
        {
            await CardSearchMetadata.LoadAsync();
            if (button == null) return;
            button.interactable = true;
            label.text = "未收录平台或合法性资料的版本不参与搜索";
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            if (label != null) label.text = "筛选资料读取失败，请关闭后重试";
        }
    }
    void Apply()
    {
        foreach (var get in read) get();
        try { submit(draft.Copy()); Destroy(gameObject); }
        catch (FormatException ex) { message.text = GamePreferences.T(ex.Message); }
    }
    void Update() { if (Input.GetKeyDown(KeyCode.Escape)) { if(setPicker!=null){Destroy(setPicker);return;} if(!GetComponentsInChildren<SearchInsertionMenu>().Any(m => m.IsOpen)) Destroy(gameObject); } }
    static float X(int col) => col == 0 ? -260 : 260;
    void ConstructionToggle(string name,string label,float x,bool selected,bool available,Action<bool> assign)
    {
        var rect=ReplayWidgets.Rect(content,name,x,-182,236,52);
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);
        rect.gameObject.AddComponent<Image>().color=Color.clear;
        var toggle=rect.gameObject.AddComponent<Toggle>();
        var box=ReplayWidgets.Panel(rect,"Box",-102,0,28,28,MenuVisualTheme.Ink);
        ObsidianArenaTheme.EnsureOutline(box.GetComponent<Image>(),MenuVisualTheme.Muted,Vector2.one);
        var check=ReplayWidgets.Panel(box,"Check",0,0,16,16,MenuVisualTheme.Accent);
        var text=ReplayWidgets.Label(rect,label,20,0,192,52,17);
        text.GetComponent<PreferenceText>().Literal=true;
        text.color=available?MenuVisualTheme.Paper:MenuVisualTheme.Muted;
        toggle.targetGraphic=box.GetComponent<Image>();toggle.graphic=check.GetComponent<Image>();
        toggle.SetIsOnWithoutNotify(selected&&available);toggle.interactable=available;
        read.Add(()=>assign(toggle.isOn&&available));
    }
    void PlatformRow(int row)
    {
        Heading(0,row,"平台");
        int mask=draft.Games;
        string[] names={"纸质","MTGA","MO"};
        for(int i=0;i<3;i++)
        {
            int bit=1<<i;
            var rect=ReplayWidgets.Rect(content,"Platform_"+names[i],-330+i*330,-row*112-70,300,44);
            rect.gameObject.AddComponent<Image>().color=Color.clear;
            var toggle=rect.gameObject.AddComponent<Toggle>();
            var box=ReplayWidgets.Panel(rect,"Box",-130,0,28,28,MenuVisualTheme.Raised);
            var check=ReplayWidgets.Panel(box,"Check",0,0,16,16,MenuVisualTheme.Accent);
            toggle.targetGraphic=box.GetComponent<Image>(); toggle.graphic=check.GetComponent<Image>();
            ReplayWidgets.Label(rect,names[i],32,0,230,44,19);
            toggle.gameObject.name="Platform_"+names[i];
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);
            rect.anchoredPosition=new Vector2(-330+i*330,-row*112-70);
            rect.sizeDelta=new Vector2(300,44);
            toggle.SetIsOnWithoutNotify((mask&bit)!=0);
            toggle.onValueChanged.AddListener(on=>mask=on?mask|bit:mask&~bit);
        }
        read.Add(()=>draft.Games=mask);
    }
    void Heading(int col, int row, string title)
    {
        var label = ReplayWidgets.Label(content, title, X(col), -row * 112 - 24, 492, 34, 17);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 1);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
    }
    InputField Field(int col, int row, string title, string value, Action<string> assign, bool? manaInsertion = null)
    {
        Heading(col, row, title);
        var input = ReplayWidgets.Input(content, value, X(col), -row * 112 - 70, 492);
        input.gameObject.name = "Search_" + title;
        var rect = (RectTransform)input.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
        if (manaInsertion.HasValue)
        {
            rect.sizeDelta = new Vector2(328, 52); rect.anchoredPosition += new Vector2(-82, 0);
            input.textComponent.rectTransform.sizeDelta = new Vector2(304, 46);
            var insert = ReplayWidgets.Button(content, "插入", X(col) + 170, -row * 112 - 70, 152, null);
            var insertRect = (RectTransform)insert.transform;
            insertRect.anchorMin = insertRect.anchorMax = new Vector2(.5f, 1);
            var chevron = ReplayWidgets.Rect(insertRect, "Chevron", 60, 0, 14, 14);
            chevron.gameObject.AddComponent<CanvasRenderer>();
            chevron.gameObject.AddComponent<DropdownChevron>().color = MenuVisualTheme.Paper;
            var menu = input.gameObject.AddComponent<SearchInsertionMenu>();
            menu.Initialize(input, insert, transform, manaInsertion.Value);
        }
        read.Add(() => assign(input.text.Trim()));
        return input;
    }
    void OpenSets(InputField target)
    {
        if (setPicker == null) setPicker = SetBrowserPanel.Show(transform, target);
    }
    Dropdown Choice(int col, int row, string title, string[] options, int selected, Action<int> assign)
    {
        Heading(col, row, title);
        var dropdown = Instantiate(template, content, false);
        dropdown.gameObject.name = "Search_" + title; dropdown.gameObject.SetActive(true);
        dropdown.onValueChanged = new Dropdown.DropdownEvent();
        var rect = (RectTransform)dropdown.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(X(col), -row * 112 - 44); rect.sizeDelta = new Vector2(492, 52);
        dropdown.ClearOptions(); dropdown.AddOptions(options.Select(GamePreferences.T).ToList());
        dropdown.SetValueWithoutNotify(selected); dropdown.RefreshShownValue();
        dropdown.captionText.resizeTextForBestFit = false;
        dropdown.captionText.GetComponent<PreferenceText>()?.SetBaseSize(18);
        dropdown.itemText.GetComponent<PreferenceText>()?.SetBaseSize(18);
        dropdown.captionText.horizontalOverflow = HorizontalWrapMode.Overflow;
        DropdownChevron.Apply(dropdown);
        read.Add(() => assign(dropdown.value));
        return dropdown;
    }
    void ColorRow(int col, int row, string title, int selected, Action<int> assign)
    {
        Heading(col, row, title);
        int mask = selected;
        string[] names = { "W", "U", "B", "R", "G", "C" };
        var buttons = new Button[6];
        for (int i = 0; i < 6; i++)
        {
            int bit = 1 << i;
            var button = ReplayWidgets.Button(content, names[i], X(col) - 205 + i * 82, -row * 112 - 70, 74, () =>
            {
                mask = (mask & bit) != 0 ? mask & ~bit : bit == 32 ? 32 : (mask & 31) | bit;
                Refresh();
            });
            ((RectTransform)button.transform).anchorMin = ((RectTransform)button.transform).anchorMax = new Vector2(.5f, 1);
            var label=button.GetComponentInChildren<Text>();
            label.resizeTextForBestFit=false;
            label.GetComponent<PreferenceText>().Literal=true;
            PreferenceText.SetSize(label,20);
            buttons[i] = button;
        }
        void RefreshLabels() {
            string[] captions=GamePreferences.Language=="ja-JP"?new[]{"白","青","黒","赤","緑","無色"}
                :GamePreferences.Language=="en-US"?names:new[]{"白","蓝","黑","红","绿","无色"};
            for(int i=0;i<6;i++)buttons[i].GetComponentInChildren<Text>().text=captions[i];
        }
        colorLabels.Add(RefreshLabels);RefreshLabels();
        void Refresh() { for (int i = 0; i < 6; i++) buttons[i].GetComponent<Image>().color = (mask & (1 << i)) != 0 ? MenuVisualTheme.Primary : MenuVisualTheme.Raised; }
        Refresh(); read.Add(() => assign(mask));
    }
}
