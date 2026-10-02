using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Offline Scryfall set relationships and symbols. Selection is per printing set.
public sealed class SetBrowserPanel : MonoBehaviour
{
    [Serializable] public sealed class Entry { public string code, name, zh, ja, parent, date, icon; }
    [Serializable] public sealed class Catalog { public Entry[] entries; }
    Entry[] entries;
    readonly HashSet<string> expanded = new HashSet<string>();
    readonly HashSet<string> collapsedSearch = new HashSet<string>();
    HashSet<string> selected;
    RectTransform rows;
    ScrollRect scroll;
    Text status;
    InputField search;
    int page;
    const int PageSize = 8;
    Button previous, next;
    Text pageLabel;
    Dictionary<string, List<Entry>> children;
    readonly Dictionary<string, Sprite> symbols = new Dictionary<string, Sprite>();
    static Entry[] catalog;
    static Entry[] LoadCatalog() => catalog ?? (catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("SetBrowser/Catalog").text).entries);
    public static string DisplayName(Entry e) => GamePreferences.Language=="zh-CN"&&!string.IsNullOrEmpty(e.zh)?e.zh:
        GamePreferences.Language=="ja-JP"&&!string.IsNullOrEmpty(e.ja)?e.ja:e.name;
    static bool Contains(string text,string query) => (text??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
    public static IEnumerable<string> MatchingCodes(string query) => LoadCatalog().Where(e=>Contains(e.name,query)||Contains(e.zh,query)||Contains(e.ja,query)).Select(e=>e.code);

    public static GameObject Show(Transform parent, InputField target)
    {
        var root = ReplayWidgets.Panel(parent, "SeriesPicker", 0, 0, 0, 0, new Color(0, 0, 0, .8f));
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.gameObject.AddComponent<SetBrowserPanel>().Build(target);
        return root.gameObject;
    }

    void Build(InputField target)
    {
        entries = LoadCatalog();
        var known = new HashSet<string>(entries.Select(e => e.code));
        children = entries.Where(e => !string.IsNullOrEmpty(e.parent) && known.Contains(e.parent))
            .GroupBy(e => e.parent).ToDictionary(g => g.Key, g => g.OrderBy(e => e.code).ToList());
        selected = new HashSet<string>(target.text.Split(new[] { ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
        var panel = ReplayWidgets.Panel(transform, "SeriesPanel", 0, 0, 1000, 850, MenuVisualTheme.Panel);
        ReplayWidgets.Label(panel, "所有系列", 0, 376, 920, 44, 28);
        search = ReplayWidgets.Input(panel, "", 0, 310, 920);
        ReplayWidgets.Label(panel, "搜索代码或名称 · 箭头展开附属系列 · 点击行选择", 0, 260, 920, 32, 17);
        var viewport = ReplayWidgets.Panel(panel, "Viewport", 0, -12, 920, 492, MenuVisualTheme.Ink);
        viewport.gameObject.AddComponent<RectMask2D>();
        rows = ReplayWidgets.Rect(viewport, "Rows", 0, 0, 904, 492);
        rows.anchorMin = rows.anchorMax = rows.pivot = new Vector2(.5f, 1);
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = rows; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 42;
        var track = ReplayWidgets.Panel(viewport, "ScrollTrack", 452, 0, 8, 492, MenuVisualTheme.Panel);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        var handle = ReplayWidgets.Panel(track, "Handle", 0, 0, 8, 0, MenuVisualTheme.Muted);
        bar.handleRect = handle; bar.targetGraphic = handle.GetComponent<Image>(); bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar;
        status = ReplayWidgets.Label(panel, "", -320, -292, 280, 34, 18);
        previous = ReplayWidgets.Button(panel, "上一页", 30, -292, 130, () => { page--; Render(); scroll.verticalNormalizedPosition = 1; });
        pageLabel = ReplayWidgets.Label(panel, "", 210, -292, 200, 34, 18, TextAnchor.MiddleCenter);
        next = ReplayWidgets.Button(panel, "下一页", 395, -292, 130, () => { page++; Render(); scroll.verticalNormalizedPosition = 1; });
        ReplayWidgets.Panel(panel, "Divider", 0, -327, 920, 1, MenuVisualTheme.Muted);
        ReplayWidgets.Button(panel, "清空选择", -365, -376, 190, () => { selected.Clear(); Render(); });
        ReplayWidgets.Button(panel, "取消", 155, -376, 190, () => Destroy(gameObject));
        ReplayWidgets.Button(panel, "确定", 365, -376, 190, () =>
        {
            if (target != null) { target.characterLimit = 0; target.text = string.Join(", ", selected.OrderBy(s => s)); }
            Destroy(gameObject);
        }, true);
        foreach (var button in panel.GetComponentsInChildren<Button>()) FixedButtonText(button);
        search.onValueChanged.AddListener(_ => { page = 0; collapsedSearch.Clear(); Render(); scroll.verticalNormalizedPosition = 1; });
        Render();
    }

    void Render()
    {
        foreach (Transform child in rows) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        string query = search.text.Trim();
        bool Match(Entry e) => Contains(e.code,query)||Contains(e.name,query)||Contains(e.zh,query)||Contains(e.ja,query);
        bool FamilyMatches(Entry e) => Match(e) || (children.TryGetValue(e.code, out var list) && list.Any(FamilyMatches));
        int count = 0;
        float rowOffset=0;
        void Add(Entry e, int depth)
        {
            bool hasChildren = children.TryGetValue(e.code, out var nested);
            bool open = query.Length > 0 ? !collapsedSearch.Contains(e.code) : expanded.Contains(e.code);
            Button row = null;
            count++;
            row = ReplayWidgets.Button(rows, "", -4, 0, 884, () =>
            {
                if (!selected.Add(e.code)) selected.Remove(e.code);
                row.GetComponent<Image>().color = selected.Contains(e.code) ? MenuVisualTheme.Primary : MenuVisualTheme.Raised;
                status.text = $"已选 {selected.Count} 个系列";
            });
            FixedButtonText(row);
            var rect = (RectTransform)row.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            row.name = "Set_" + e.code;
            row.GetComponent<Image>().color = selected.Contains(e.code) ? MenuVisualTheme.Primary : MenuVisualTheme.Raised;
            int indent = Math.Min(depth, 4) * 24;
            if (hasChildren)
            {
                var arrow = ReplayWidgets.Button(rect, open ? "▾" : "▸", -414 + indent, 0, 38, () =>
                {
                    if (query.Length > 0) { if (!collapsedSearch.Add(e.code)) collapsedSearch.Remove(e.code); }
                    else if (!expanded.Add(e.code)) expanded.Remove(e.code);
                    Render();
                });
                arrow.name = "Expand_" + e.code;
                FixedButtonText(arrow);
            }
            if (!string.IsNullOrEmpty(e.icon))
            {
                if (!symbols.TryGetValue(e.icon, out var sprite))
                {
                    var texture = Resources.Load<Texture2D>("SetBrowser/" + e.icon);
                    sprite = texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                    symbols[e.icon] = sprite;
                }
                var image = ReplayWidgets.Rect(rect, "Symbol", -366 + indent, 0, 30, 30).gameObject.AddComponent<Image>();
                image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
                image.enabled = sprite != null;
            }
            float left = -338 + indent;
            var title = ReplayWidgets.Label(rect, RowName(e), (left + 286) / 2, 0, 286 - left, 46, 18);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            float height=Mathf.Max(52,Mathf.Ceil(title.preferredHeight)+14);
            rect.sizeDelta=new Vector2(884,height);
            rect.anchoredPosition=new Vector2(-4,-rowOffset-height/2);
            title.rectTransform.sizeDelta=new Vector2(286-left,height-12);
            ReplayWidgets.Label(rect, e.code, 359, 0, 128, height-12, 16, TextAnchor.MiddleRight);
            rowOffset+=height+6;
            if (hasChildren && open) foreach (var child in nested) Add(child, depth + 1);
        }
        var codes = new HashSet<string>(entries.Select(e => e.code));
        var roots = entries.Where(e => string.IsNullOrEmpty(e.parent) || !codes.Contains(e.parent))
            .Where(e => query.Length == 0 || FamilyMatches(e))
            .OrderByDescending(e => e.date).ThenBy(e => e.code).ToArray();
        int pages = Math.Max(1, (roots.Length + PageSize - 1) / PageSize);
        page = Mathf.Clamp(page, 0, pages - 1);
        foreach (var root in roots.Skip(page * PageSize).Take(PageSize)) Add(root, 0);
        previous.interactable = page > 0; next.interactable = page + 1 < pages;
        pageLabel.text = $"第 {page + 1} / {pages} 页";
        rows.sizeDelta = new Vector2(904, Math.Max(492, rowOffset));
        status.text = count == 0 ? $"没有匹配的系列 · 已选 {selected.Count} 项" : $"已选 {selected.Count} 个系列";
    }
    string RowName(Entry entry)
    {
        string name=DisplayName(entry);
        var parent=entries.FirstOrDefault(e=>e.code==entry.parent);
        if(parent==null)return name;
        string parentName=DisplayName(parent);
        string suffix=name;
        if(name.StartsWith(parentName,StringComparison.Ordinal)&&name.Length>parentName.Length)
            suffix=name.Substring(parentName.Length).Trim();
        else if(entry.name.StartsWith(parent.name+" ",StringComparison.OrdinalIgnoreCase))
            suffix=entry.name.Substring(parent.name.Length).Trim();
        else return name;
        string[] translated=null;
        switch(suffix)
        {
            case "Art Series": translated=new[]{"美术系列","アート・シリーズ"};break;
            case "Promos": translated=new[]{"推广牌","プロモ"};break;
            case "Tokens": translated=new[]{"衍生物","トークン"};break;
            case "Commander": translated=new[]{"指挥官","コマンダー"};break;
            case "Beginner Box Front Cards":translated=new[]{"入门盒正面参考牌","ビギナー・ボックス 表面カード"};break;
            case "Jumpstart Front Cards":translated=new[]{"Jumpstart 正面参考牌","ジャンプスタート 表面カード"};break;
        }
        return translated==null?suffix:GamePreferences.Language=="ja-JP"?translated[1]:GamePreferences.Language=="zh-CN"?translated[0]:suffix;
    }
    void OnEnable(){GamePreferences.Changed+=RefreshLayout;}
    void OnDisable(){GamePreferences.Changed-=RefreshLayout;}
    void RefreshLayout(){if(rows!=null)Render();}
    static void FixedButtonText(Button button)
    {
        var text = button.GetComponentInChildren<Text>();
        text.resizeTextForBestFit = false;
        text.GetComponent<PreferenceText>().SetBaseSize(18);
    }
    void OnDestroy() { foreach (var sprite in symbols.Values) if (sprite != null) Destroy(sprite); }
}
