using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class MenuThemeStyle
{
    public string Id = "moonlit-sanctum";
    public string ChineseName = "月夜秘殿";
    public string EnglishName = "Moonlit Sanctum";
    public string JapaneseName = "月夜の聖堂";
    public string BattlefieldTrim;
    public string MenuMusic = "GameAudio/MoonlitHall";
    public string BattleMusic = "GameAudio/Duel";
    internal bool IsTidelights => BattlefieldTrim == "lacquer";

    [Header("Artwork and typography (Resources paths)")]
    public string Hall = "Menu/SummoningHall";
    public string DeckBackdrop = "Menu/ArchiveSanctum";
    public string ReplayBackdrop = "Menu/DeckPlaymat";
    public string BattlefieldBackdrop = "Menu/SanctumTable";
    public string DuelSeatBackdrop = "Menu/SanctumSeatPrint";
    public string BattlefieldFrameAtlas = "Menu/SanctumBattleFrames";
    public string HandFramePath, SeatJointPath, Motion;
    [NonSerialized] internal MenuThemePackage Package;
    internal Sprite HandFrame => Load<Sprite>(HandFramePath);
    internal Sprite SeatJoint => Load<Sprite>(SeatJointPath);
    internal T Load<T>(string key) where T : UnityEngine.Object =>
        string.IsNullOrEmpty(key) ? null : Package != null ? Package.Load<T>(key) : Resources.Load<T>(key);
    public string JournalTabStrip = "Menu/JournalTabs";
    public Color32 JournalTabText = new Color32(180,198,208,255);
    public Color32 JournalTabActiveText = new Color32(243,245,245,255);
    [NonSerialized] private Dictionary<string, Sprite> battlefieldFrames;
    internal Sprite BattlefieldFrame(string part)
    {
        if (battlefieldFrames == null)
        {
            battlefieldFrames = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(BattlefieldFrameAtlas))
                foreach (var sprite in Package != null && BattlefieldFrameAtlas.StartsWith("art/",StringComparison.Ordinal)
                    ? Package.Frames(BattlefieldFrameAtlas) : Resources.LoadAll<Sprite>(BattlefieldFrameAtlas))
                    battlefieldFrames[sprite.name] = sprite;
        }
        if (battlefieldFrames.TryGetValue(part, out var frame)) return frame;
        return this == MenuThemeCatalog.Default ? null : MenuThemeCatalog.Default.BattlefieldFrame(part);
    }
    internal void ReleaseArtwork()
    {
        battlefieldFrames=null;
        Package?.Release();
    }
    public string DeckMaterial = "Menu/ArchiveMoonlight";
    public string HeaderArt = "Menu/MoonlitFrieze";
    public string ButtonSprite = "Menu/ArchiveButton";
    public string ControlFont = "Menu/WenQuanYiMicroHei";

    [Header("Menu palette")]
    public Color32 DialogShade = new Color32(6, 13, 24, 214);
    public Color32 ArchivePaper = new Color32(233, 240, 244, 255);
    public Color32 ArchivePanel = new Color32(27, 43, 53, 255);
    public Color32 Recess = new Color32(14, 25, 33, 255);
    public Color32 Slate = new Color32(45, 68, 80, 255);
    public Color32 LitSlate = new Color32(65, 91, 103, 255);
    public Color32 MistBlue = new Color32(159, 194, 213, 255);
    public Color32 Silver = new Color32(212, 232, 243, 255);
    public Color32 Stone = new Color32(174, 192, 197, 255);
    public Color32 Etching = new Color32(126, 149, 156, 126);
    public Color32 ActionFill = new Color32(107, 81, 137, 255);
    public Color32 ActionHover = new Color32(135, 106, 166, 255);
    public Color32 ActionPressed = new Color32(78, 58, 101, 255);
    public Color32 ActionEdge = new Color32(201, 178, 223, 255);
    public Color32 ActionText = new Color32(249, 245, 255, 255);
    public Color32 Warning = new Color32(246, 158, 127, 255);
    public Color32 MainAccent = new Color32(149, 202, 250, 255);
    public Color32 SideAccent = new Color32(202, 182, 234, 255);
    public Color32 LibraryAccent = new Color32(132, 218, 209, 255);
    public Color32 SideSurface = new Color32(51, 43, 66, 255);
    public Color32 ControlEdge = new Color32(178, 205, 220, 218);
    public Color32 NavigationEdge = new Color32(226, 202, 247, 255);
    public Color32 ControlFill = new Color32(63, 86, 103, 255);
    public Color32 ControlHover = new Color32(85, 119, 143, 255);
    public Color32 ControlPressed = new Color32(38, 59, 71, 255);
    public Color32 ControlDisabled = new Color32(59, 72, 77, 255);
    public Color32 InputFocus = new Color32(29, 51, 66, 255);
    public Color32 SectionFill = new Color32(18, 31, 43, 245);
    public Color32 DialogTint = new Color32(218, 224, 250, 255);
    public Color32 SearchEdge = new Color32(229, 203, 251, 255);
    public Color32 SearchFill = new Color32(143, 91, 185, 255);
    public Color32 SearchHover = new Color32(165, 115, 204, 255);
    public Color32 Selection = new Color32(97, 158, 194, 165);
    public Color32 InputRim = new Color32(129, 158, 181, 255);
    public Color32 PreviewFill = new Color32(24, 28, 37, 251);
    public Color32 DetailFill = new Color32(24, 28, 37, 224);
    public Color32 MainSurface = new Color32(31, 53, 76, 250);
    public Color32 MainWell = new Color32(17, 31, 48, 250);
    public Color32 SideWell = new Color32(36, 30, 47, 250);
    public Color32 LibrarySurface = new Color32(25, 57, 61, 250);
    public Color32 LibraryWell = new Color32(14, 37, 40, 248);
    public Color32 ScrollThumb = new Color32(120, 148, 156, 255);

    public string DisplayName => GamePreferences.Language switch
    {
        "en-US" when !string.IsNullOrWhiteSpace(EnglishName) => EnglishName,
        "ja-JP" when !string.IsNullOrWhiteSpace(JapaneseName) => JapaneseName,
        _ => ChineseName
    };
}

internal static class MenuThemeCatalog
{
    internal static readonly MenuThemeStyle Default = new MenuThemeStyle();
    internal static MenuThemeStyle Current { get; private set; } = Default;
    internal static event Action Changed;
    internal static IReadOnlyList<MenuThemeStyle> All { get; private set; } = new[] { Default };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    internal static void Load() => ReadDirectory(MenuThemePackage.Root);

    internal static void ReadDirectory(string root)
    {
        var themes = new List<MenuThemeStyle> { Default };
        var ids = new HashSet<string>(StringComparer.Ordinal) { Default.Id };
        foreach (var style in MenuThemePackage.Discover(root))
        {
            if (!ids.Add(style.Id))
            {
                Debug.LogWarning("重复的主题标识：" + style.Id);
                continue;
            }
            themes.Add(style);
        }
        foreach (var old in All) old.ReleaseArtwork();
        All = themes;
        Current = GamePreferences.ResolveInstalledMenuTheme();
        Changed?.Invoke();
        Debug.Log($"[Themes] active={Current.Id}; installed={string.Join(",",themes.Select(t=>t.Id))}");
    }

    internal static MenuThemeStyle Find(string id) => All.FirstOrDefault(theme => theme.Id == id);

    internal static void Activate(MenuThemeStyle theme)
    {
        var previous = Current;
        Current = theme;
        Changed?.Invoke();
        MenuThemeRefresh.Apply(previous, theme);
        MenuThemeArtwork.RefreshAll();
        previous?.ReleaseArtwork();
        if(previous?.Package!=null && Application.isPlaying)Resources.UnloadUnusedAssets();
    }
}
