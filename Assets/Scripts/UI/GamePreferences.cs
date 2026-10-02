using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

// Locale is owned by Unity Localization. Card identities always remain canonical.
public sealed class GamePreferences : MonoBehaviour
{
    public static event Action Changed;
    public static int Revision { get; private set; }
    public static bool Ready { get; private set; }
    public static Font Font { get; private set; }
    public static TMP_FontAsset TmpFont { get; private set; }
    public static float TextScale => Mathf.Clamp(PlayerPrefs.GetFloat("UI.TextScale", 1), .85f, 1.3f);
    public static bool AutoSaveReplays => PlayerPrefs.GetInt("Replay.AutoSave", 0) == 1;
    public static void SetAutoSaveReplays(bool value)
    {
        PlayerPrefs.SetInt("Replay.AutoSave", value ? 1 : 0); PlayerPrefs.Save(); Notify();
    }
    public static string DeckFormat
    {
        get { string value = PlayerPrefs.GetString("Deck.Format", "standard"); return value == "none" ? "standard" : value; }
    }
    public static string DeckBanlist
    {
        get
        {
            string value = PlayerPrefs.GetString("Deck.Banlist", "current");
            return value == DeckFormatLegality.Current ? DeckFormatLegality.Latest(DeckFormat) : value;
        }
    }
    public static string Language => LocalizationSettings.SelectedLocale?.Identifier.Code ?? "zh-CN";
    static GamePreferences instance;
    static StringTable table;
    static readonly List<Tuple<Regex,string>> formats=new List<Tuple<Regex,string>>();
    static Dictionary<string, string[]> cards = new Dictionary<string, string[]>(StringComparer.Ordinal);
    static Dictionary<string, string[]>[] searchCatalogs;
    readonly Dictionary<string, Dictionary<string, string[]>> catalogs = new Dictionary<string, Dictionary<string, string[]>>();
    int generation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { instance=null; Ready=false; table=null; formats.Clear(); Font=null; TmpFont=null; Changed=null; Revision=0; cards=new Dictionary<string,string[]>(); searchCatalogs=null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if(instance != null) return;
        var go=new GameObject("GamePreferences"); DontDestroyOnLoad(go); instance=go.AddComponent<GamePreferences>();
    }

    IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;
        var locale=LocalizationSettings.AvailableLocales.GetLocale(PlayerPrefs.GetString("UI.Language", "zh-CN"));
        if(locale!=null) LocalizationSettings.SelectedLocale=locale;
        LocalizationSettings.SelectedLocaleChanged += OnLocale;
        OnLocale(LocalizationSettings.SelectedLocale);
        while(true) { BindScene(); yield return new WaitForSecondsRealtime(.5f); }
    }

    void OnDestroy() { LocalizationSettings.SelectedLocaleChanged-=OnLocale; }

    async void OnLocale(Locale locale)
    {
        int request=++generation;
        var held=new List<UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle>();
        try
        {
            var strings=LocalizationSettings.StringDatabase.GetTableAsync("Interface",locale);
            held.Add(UnityEngine.AddressableAssets.Addressables.ResourceManager.Acquire(strings));
            var font=LocalizationSettings.AssetDatabase.GetLocalizedAssetAsync<Font>("Presentation", "Font", locale);
            held.Add(UnityEngine.AddressableAssets.Addressables.ResourceManager.Acquire(font));
            var tmp=LocalizationSettings.AssetDatabase.GetLocalizedAssetAsync<TMP_FontAsset>("Presentation", "TmpFont", locale);
            held.Add(UnityEngine.AddressableAssets.Addressables.ResourceManager.Acquire(tmp));
            await Task.WhenAll(strings.Task,font.Task,tmp.Task);
            string code=locale.Identifier.Code;
            if(this==null || request!=generation) return;
            table=strings.Result; Font=font.Result; TmpFont=tmp.Result;
            formats.Clear();
            foreach(var entry in table.Values)
            {
                if(entry==null || string.IsNullOrEmpty(entry.Key) || !entry.Key.Contains("{0}")) continue;
                string pattern=Regex.Escape(entry.Key);
                for(int i=0;i<8;i++) pattern=pattern.Replace(Regex.Escape("{"+i+"}"),"(?<p"+i+">.*?)");
                formats.Add(Tuple.Create(new Regex("^"+pattern+"$",RegexOptions.Singleline),entry.LocalizedValue));
            }
            PlayerPrefs.SetString("UI.Language",code); PlayerPrefs.Save();
            // Startup exposes deck editing immediately, so its translated card text must be ready too.
            if(!catalogs.TryGetValue(code,out var translated))
            {
                string path=Path.Combine(Application.streamingAssetsPath,"CardTranslations",code+".txt");
                translated=await Task.Run(()=>ReadCards(path));
                if(this==null || request!=generation) return;
                catalogs[code]=translated;
            }
            cards=translated; Ready=true; Notify(); BindScene();
        }
        catch(Exception error)
        {
            Debug.LogError("Language resources: "+error);
            // A missing localization asset must not leave the menu permanently hidden.
            if(this!=null && request==generation) { Ready=true; Notify(); BindScene(); }
        }
        finally {foreach(var operation in held) if(operation.IsValid()) UnityEngine.AddressableAssets.Addressables.Release(operation);}
    }

    [Serializable] private sealed class CardTextCorrection { public string name,expectedRules; }
    [Serializable] private sealed class CardTextCorrections { public CardTextCorrection[] entries; }
    static Dictionary<string,string[]> ReadCards(string path)
    {
        var result=new Dictionary<string,string[]>(StringComparer.Ordinal);
        if(!File.Exists(path)) return result;
        var corrections=new Dictionary<string,string>(StringComparer.Ordinal);
        Load(path,false);
        string correctionPath=Path.ChangeExtension(path,"corrections.json");
        if(File.Exists(correctionPath))
        {
            var manifest=JsonUtility.FromJson<CardTextCorrections>(File.ReadAllText(correctionPath));
            foreach(var correction in manifest?.entries??Array.Empty<CardTextCorrection>())
                if(!string.IsNullOrEmpty(correction.name))corrections[correction.name]=correction.expectedRules;
        }
        string supplement=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+".supplement.txt");
        if(File.Exists(supplement))Load(supplement,true);
        string arena=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+".arena.txt");
        if(File.Exists(arena))Load(arena,true);
        string notes=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+".notes.txt");
        if(File.Exists(notes))Load(notes,true);
        string printed=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+".printed.txt");
        if(File.Exists(printed))Load(printed,true);
        string gatherer=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+".gatherer.txt");
        if(File.Exists(gatherer))Load(gatherer,true);
        string gathererPrints=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+".gatherer-prints.txt");
        if(File.Exists(gathererPrints))Load(gathererPrints,true);
        string whisper=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+".whisper.txt");
        if(File.Exists(whisper))Load(whisper,true);
        string remainingReviewed=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+".remaining-reviewed.txt");
        if(File.Exists(remainingReviewed))Load(remainingReviewed,true);
        string reviewed=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+".review.txt");
        if(File.Exists(reviewed))Load(reviewed,true);
        return result;

        void Load(string file,bool fillMissing)
        {
            foreach(string line in File.ReadLines(file))
            {
                var parts=line.Split(new[]{'|'},4);
                if(parts.Length!=4)continue;
                for(int i=1;i<4;i++)parts[i]=parts[i].Replace("\\n","\n");
                if(fillMissing&&result.TryGetValue(parts[0],out var existing))
                {
                    bool corrected=corrections.TryGetValue(parts[0],out var expected)&&existing[3]==expected;
                    string locale=Path.GetFileNameWithoutExtension(path);
                    bool chinese=locale=="zh-CN",japanese=locale=="ja-JP";
                    for(int i=1;i<4;i++)
                    {
                        bool missing=corrected||string.IsNullOrWhiteSpace(existing[i])||chinese&&!Regex.IsMatch(existing[i],@"[\u3400-\u9fff]");
                        if(japanese&&!Regex.IsMatch(existing[i],@"[\u3040-\u30ff\u3400-\u9fff]"))missing=true;
                        // Some upstream rules translate only the introductory phrase.
                        // A published local supplement can replace the remaining English prose.
                        if((chinese||japanese)&&i==3&&Regex.IsMatch(existing[i],@"[A-Za-z][A-Za-z'’-]*(?: +[A-Za-z][A-Za-z'’-]*){3,}"))missing=true;
                        if(missing&&!string.IsNullOrWhiteSpace(parts[i]))existing[i]=parts[i];
                    }
                }
                else result[parts[0]]=parts;
            }
        }
    }

    public static void SetLanguage(string code)
    {
        var locale=LocalizationSettings.AvailableLocales.GetLocale(code);
        if(locale!=null) LocalizationSettings.SelectedLocale=locale;
    }
    public static void SetScale(float value) { PlayerPrefs.SetFloat("UI.TextScale",Mathf.Clamp(value,.85f,1.3f)); PlayerPrefs.Save(); Notify(); }
    public static void SetDeckFormat(string value)
    {
        if (string.IsNullOrEmpty(value) || DeckFormat == value) return;
        PlayerPrefs.SetString("Deck.Format", value);
        PlayerPrefs.SetString("Deck.Banlist", DeckFormatLegality.Latest(value));
        PlayerPrefs.Save();
    }
    public static void SetDeckBanlist(string value)
    {
        if (string.IsNullOrEmpty(value) || DeckBanlist == value) return;
        PlayerPrefs.SetString("Deck.Banlist", value);
        PlayerPrefs.Save();
    }
    static void Notify() { Revision++; Changed?.Invoke(); }
    public static string T(string source)
    {
        if(string.IsNullOrEmpty(source)) return source;
        var exact=table?.GetEntry(source); if(exact!=null) return exact.LocalizedValue;
        foreach(var format in formats)
        {
            var match=format.Item1.Match(source); if(!match.Success) continue;
            string result=format.Item2;
            for(int i=0;i<8;i++) result=result.Replace("{"+i+"}",match.Groups["p"+i].Value);
            return result;
        }
        return source;
    }
    public static string CardName(CardDefinition card) => Field(card,1,card?.cardName ?? "");
    public static string CardType(CardDefinition card) => Field(card,2,card?.GetTypeLine() ?? "");
    public static string CardRules(CardDefinition card) => DisplayRules(Field(card,3,card?.description ?? ""),card!=null&&card.cardTypes.Has(global::CardType.Planeswalker));
    public static string FeatureName(CardFeatureDefinition feature) => Field(feature?.cardName, 1, feature?.cardName ?? "");
    public static string FeatureType(CardFeatureDefinition feature, string fallback) => Field(feature?.cardName, 2, fallback);
    public static string FeatureRules(CardFeatureDefinition feature) => DisplayRules(Field(feature?.cardName, 3, feature?.description ?? ""),feature!=null&&feature.cardTypes.Has(global::CardType.Planeswalker));
    static string DisplayRules(string rules,bool planeswalker) => !planeswalker ? rules :
        Regex.Replace(rules,@"(?:^|[\r\n\u3000])(?:忠誠度|忠诚值|Loyalty)\s*[:：]\s*[0-9０-９]+\s*$",string.Empty,RegexOptions.IgnoreCase);
    static string Field(CardDefinition card,int column,string fallback) => Field(card?.cardName,column,fallback);
    static string Field(string name,int column,string fallback) => !string.IsNullOrEmpty(name) && cards.TryGetValue(name,out var value) && !string.IsNullOrWhiteSpace(value[column]) ? value[column] : fallback;

    // Search is independent of the display language. Load each translation file only on the
    // first query that needs it, then reuse the dictionaries for subsequent card checks.
    public static bool MatchesAnyCardTranslation(CardDefinition card, string query, int field = 0)
    {
        if (card == null || string.IsNullOrEmpty(query)) return false;
        if (searchCatalogs == null)
        {
            string root = Path.Combine(Application.streamingAssetsPath, "CardTranslations");
            searchCatalogs = new[]
            {
                Language == "zh-CN" && cards.Count > 0 ? cards : ReadCards(Path.Combine(root, "zh-CN.txt")),
                Language == "ja-JP" && cards.Count > 0 ? cards : ReadCards(Path.Combine(root, "ja-JP.txt"))
            };
        }
        foreach (var catalog in searchCatalogs)
        {
            if (MatchesTranslation(catalog, card.cardName, query, field)) return true;
            if (card.features == null) continue;
            foreach (CardFeatureDefinition feature in card.features)
                if (feature != null && MatchesTranslation(catalog, feature.cardName, query, field)) return true;
        }
        return false;
    }

    static bool MatchesTranslation(Dictionary<string, string[]> catalog, string cardName, string query, int field)
    {
        if (string.IsNullOrEmpty(cardName) || !catalog.TryGetValue(cardName, out var fields)) return false;
        if (field > 0) return fields[field].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        return fields[1].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || fields[2].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || fields[3].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Runtime-created labels join the same preferences as authored labels. Inputs and deck names
    // are never translated. Existing bindings retain their original font size and source text.
    public static void BindScene()
    {
        foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            foreach(var dropdown in canvas.GetComponentsInChildren<Dropdown>(true))DropdownChevron.Apply(dropdown);
            foreach(var button in canvas.GetComponentsInChildren<Button>(true)) DisabledButtonVisual.Bind(button);
            foreach(var text in canvas.GetComponentsInChildren<Text>(true))
            {
                // The marquee copies its source label's preferences; applying them twice scales it again.
                if(text.transform.parent!=null && text.transform.parent.GetComponent<PlayerNameMarquee>()!=null)continue;
                (text.GetComponent<PreferenceText>() ?? text.gameObject.AddComponent<PreferenceText>()).Apply();
            }
            foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true))
                (text.GetComponent<PreferenceText>() ?? text.gameObject.AddComponent<PreferenceText>()).Apply();
        }
    }
}
