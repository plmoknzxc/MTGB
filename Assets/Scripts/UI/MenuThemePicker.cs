using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Both settings surfaces use the same catalog, localized labels and saved selection.
public sealed class MenuThemePicker : MonoBehaviour
{
    const float HeaderInset = 8f;
    const float HeaderHeight = 34f;
    const float DropdownTop = HeaderInset + HeaderHeight + 18f;
    const float DropdownHeight = 44f;
    const float MotionHeight = 42f;
    internal const float Height = DropdownTop + DropdownHeight + 12f + MotionHeight;
    Text heading;
    Text folderLabel;
    Dropdown dropdown;
    Toggle motion;
    Text motionLabel;
    MenuThemeStyle styledTheme;
    string optionLanguage;

    internal static RectTransform Create(Transform parent)
    {
        var go = new GameObject("MenuThemePicker", typeof(RectTransform));
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        var picker = go.AddComponent<MenuThemePicker>();
        var heading = new GameObject("ThemeHeading", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        heading.transform.SetParent(go.transform, false);
        heading.font = GamePreferences.Font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        heading.color = MenuVisualTheme.ArchivePaper;
        heading.raycastTarget = false;
        heading.alignment = TextAnchor.MiddleLeft;
        heading.rectTransform.anchorMin = Vector2.up;
        heading.rectTransform.anchorMax = Vector2.one;
        heading.rectTransform.offsetMin = new Vector2(0, -HeaderInset-HeaderHeight);
        heading.rectTransform.offsetMax = new Vector2(-150,-HeaderInset);
        PreferenceText.SetSize(heading, 18);
        heading.GetComponent<PreferenceText>().Literal = true;
        picker.heading = heading;
        var folder=ReplayWidgets.Button(go.transform,"主题文件夹",0,0,144,()=>{
            var directory=System.IO.Directory.CreateDirectory(MenuThemePackage.Root);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(directory.FullName){UseShellExecute=true});
        });
        var folderRect=(RectTransform)folder.transform;
        folderRect.anchorMin=folderRect.anchorMax=folderRect.pivot=Vector2.one;
        folderRect.anchoredPosition = new Vector2(0,-HeaderInset);
        folderRect.sizeDelta = new Vector2(144,HeaderHeight);
        MenuVisualTheme.StyleDeckTool(folder);
        picker.folderLabel=folder.GetComponentInChildren<Text>();
        picker.folderLabel.GetComponent<PreferenceText>().Literal=true;
        PreferenceText.SetSize(picker.folderLabel,15);

        var field = DefaultControls.CreateDropdown(new DefaultControls.Resources()).GetComponent<Dropdown>();
        field.name = "MenuThemeDropdown";
        field.transform.SetParent(go.transform, false);
        var rect = (RectTransform)field.transform;
        rect.anchorMin = Vector2.up;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(0, -DropdownTop-DropdownHeight);
        rect.offsetMax = new Vector2(0, -DropdownTop);
        var row = (RectTransform)field.itemText.GetComponentInParent<Toggle>(true).transform;
        row.sizeDelta = new Vector2(row.sizeDelta.x, 44);
        field.template.sizeDelta = new Vector2(0, 220);
        var mark = (RectTransform)row.GetComponent<Toggle>().graphic.transform;
        mark.sizeDelta = new Vector2(3, 30);
        PreferenceText.SetSize(field.captionText, 17);
        PreferenceText.SetSize(field.itemText, 17);
        DropdownChevron.Apply(field);
        picker.dropdown = field;
        field.onValueChanged.AddListener(picker.Select);
        picker.motion = ReplayWidgets.PreferenceToggle(go.transform, "MenuMotionToggle", "",
            GamePreferences.MenuMotionEnabled, GamePreferences.SetMenuMotionEnabled);
        var motionRect = (RectTransform)picker.motion.transform;
        motionRect.anchorMin = Vector2.zero;
        motionRect.anchorMax = Vector2.right;
        motionRect.offsetMin = Vector2.zero;
        motionRect.offsetMax = new Vector2(0, MotionHeight);
        picker.motionLabel = picker.motion.GetComponentInChildren<Text>(true);
        picker.motionLabel.GetComponent<PreferenceText>().Literal = true;
        go.SetActive(true);
        return (RectTransform)go.transform;
    }

    void OnEnable()
    {
        GamePreferences.Changed += Refresh;
        GamePreferences.MenuMotionChanged += RefreshMotion;
        Refresh();
    }

    void OnDisable()
    {
        GamePreferences.Changed -= Refresh;
        GamePreferences.MenuMotionChanged -= RefreshMotion;
    }

    void RefreshMotion() => motion.SetIsOnWithoutNotify(motion.interactable && GamePreferences.MenuMotionEnabled);

    void Refresh()
    {
        if (styledTheme != MenuThemeCatalog.Current)
        {
            styledTheme = MenuThemeCatalog.Current;
            heading.color = MenuVisualTheme.ArchivePaper;
            MenuVisualTheme.StyleArchiveDropdown(dropdown);
            MenuVisualTheme.StyleDeckScrollThumb(dropdown.template.GetComponent<ScrollRect>().verticalScrollbar);
            motion.interactable = TidelightsMenuMotion.SupportsArtwork(styledTheme);
            MenuVisualTheme.StylePreferenceToggle(motion);
        }
        var themes = MenuThemeCatalog.All;
        if (optionLanguage != GamePreferences.Language || dropdown.options.Count != themes.Count)
        {
            optionLanguage = GamePreferences.Language;
            heading.text = DeckCompanion.Text("界面主题", "Interface theme", "画面テーマ");
            folderLabel.text = DeckCompanion.Text("主题文件夹","Themes folder","テーマフォルダー");
            motionLabel.text = DeckCompanion.Text("主界面动态", "Main menu animation", "メインメニューのアニメーション");
            var names = new List<string>(themes.Count);
            foreach (var theme in themes) names.Add(theme.DisplayName);
            dropdown.ClearOptions();
            dropdown.AddOptions(names);
        }
        var selected = GamePreferences.MenuTheme;
        for (int i = 0; i < themes.Count; i++)
        {
            if (themes[i] != selected) continue;
            dropdown.SetValueWithoutNotify(i);
            break;
        }
        RefreshMotion();
    }

    void Select(int index)
    {
        if (index >= 0 && index < MenuThemeCatalog.All.Count)
            GamePreferences.SetMenuTheme(MenuThemeCatalog.All[index].Id);
    }
}
