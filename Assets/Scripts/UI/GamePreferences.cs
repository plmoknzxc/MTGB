using System;
using System.Linq;
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
    // Developer switch: set true to restore both language pickers and the saved
    // startup locale. Keep localization resources and SetLanguage for code/tools.
    public static bool LanguageSelectionEnabled => false;
    // Developer-only rendering switch. Keep the component renderer available,
    // but ship scans by default; old saved UI preferences must not re-enable it.
    public const bool DefaultComponentCardFaces = false;
    public static bool UseComponentCardFaces { get; private set; } = DefaultComponentCardFaces;
    public static void SetComponentCardFaces(bool value)
    {
        if(UseComponentCardFaces==value)return;
        UseComponentCardFaces=value;Notify();
    }
    public static void SetAutoSaveReplays(bool value)
    {
        PlayerPrefs.SetInt("Replay.AutoSave", value ? 1 : 0); PlayerPrefs.Save(); Notify();
    }
    public static string DeckFormat
    {
        get { return PlayerPrefs.GetString("Deck.Format", "standard"); }
    }
    public static string DeckBanlist
    {
        get
        {
            string value = PlayerPrefs.GetString("Deck.Banlist", "current");
            return value == DeckFormatLegality.Current ? DeckFormatLegality.Latest(DeckFormat) : value;
        }
    }
#if UNITY_SERVER
    public static string Language => "zh-CN";
#else
    public static string Language => LocalizationSettings.SelectedLocale?.Identifier.Code ?? "zh-CN";
#endif
    static GamePreferences instance;
    static StringTable table;
    static readonly List<Tuple<Regex,string>> formats=new List<Tuple<Regex,string>>();
    static readonly Dictionary<string,string> aliases=new Dictionary<string,string>(StringComparer.Ordinal);
    static readonly Dictionary<string,string> textCache=new Dictionary<string,string>(StringComparer.Ordinal);
    static readonly HashSet<string> untranslatedNative=new HashSet<string>(StringComparer.Ordinal);
    static readonly HashSet<string> untranslatedInterface=new HashSet<string>(StringComparer.Ordinal);
    public static IEnumerable<string> UntranslatedNativeMessages => untranslatedNative;
    public static IEnumerable<string> UntranslatedInterfaceMessages => untranslatedInterface;
    public static void ClearLanguageDiagnostics() {untranslatedNative.Clear();untranslatedInterface.Clear();}
    static Dictionary<string, string[]> cards = new Dictionary<string, string[]>(StringComparer.Ordinal);
    readonly Dictionary<string, Dictionary<string, string[]>> catalogs = new Dictionary<string, Dictionary<string, string[]>>();
    int generation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { instance=null; Ready=false; table=null; formats.Clear(); aliases.Clear(); textCache.Clear(); untranslatedNative.Clear();untranslatedInterface.Clear(); Font=null; TmpFont=null; Changed=null; Revision=0; cards=new Dictionary<string,string[]>(); UseComponentCardFaces=DefaultComponentCardFaces; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
#if UNITY_SERVER
        // The authority sends canonical card identities; clients localize and render them.
        Ready = true;
        return;
#else
        if(instance != null) return;
        var go=new GameObject("GamePreferences"); DontDestroyOnLoad(go); instance=go.AddComponent<GamePreferences>();
#endif
    }

    IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;
        string startupLanguage=LanguageSelectionEnabled ? PlayerPrefs.GetString("UI.Language", "zh-CN") : "zh-CN";
        var locale=LocalizationSettings.AvailableLocales.GetLocale(startupLanguage);
        if(locale!=null) LocalizationSettings.SelectedLocale=locale;
        LocalizationSettings.SelectedLocaleChanged += OnLocale;
        OnLocale(LocalizationSettings.SelectedLocale);
        var interval=new WaitForSecondsRealtime(.5f);
        while(true) { BindNewUi(); yield return interval; }
    }

    void OnDestroy() { LocalizationSettings.SelectedLocaleChanged-=OnLocale; BattleTextDiagnostics.FlushOnExit(); }
    void OnApplicationQuit() { BattleTextDiagnostics.FlushOnExit(); }
    void LateUpdate(){CardImageLibrary.TrimCache();BattleTextDiagnostics.Tick();}

    async void OnLocale(Locale locale)
    {
        Ready=false;
        int request=++generation;
        var held=new List<UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle>();
        try
        {
            var strings=LocalizationSettings.StringDatabase.GetTableAsync("Interface",locale);
            held.Add(UnityEngine.AddressableAssets.Addressables.ResourceManager.Acquire(strings));
            // Keep source text recoverable even when a label was constructed in
            // another locale (including labels inside a currently open dialog).
            var localeTables=new List<UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<StringTable>>();
            foreach(string supported in new[]{"zh-CN","en-US","ja-JP"}) {
                var operation=LocalizationSettings.StringDatabase.GetTableAsync("Interface",LocalizationSettings.AvailableLocales.GetLocale(supported));
                localeTables.Add(operation);held.Add(UnityEngine.AddressableAssets.Addressables.ResourceManager.Acquire(operation));
            }
            var font=LocalizationSettings.AssetDatabase.GetLocalizedAssetAsync<Font>("Presentation", "Font", locale);
            held.Add(UnityEngine.AddressableAssets.Addressables.ResourceManager.Acquire(font));
            var tmp=LocalizationSettings.AssetDatabase.GetLocalizedAssetAsync<TMP_FontAsset>("Presentation", "TmpFont", locale);
            held.Add(UnityEngine.AddressableAssets.Addressables.ResourceManager.Acquire(tmp));
            await Task.WhenAll(strings.Task,font.Task,tmp.Task);
            await Task.WhenAll(localeTables.ConvertAll(operation=>operation.Task));
            string code=locale.Identifier.Code;
            if(this==null || request!=generation) return;
            var nextTable=strings.Result;
            var nextFormats=new List<Tuple<Regex,string>>();
            var nextAliases=new Dictionary<string,string>(StringComparer.Ordinal);
            var conflicts=new HashSet<string>(StringComparer.Ordinal);
            void Alias(string source,string target) {
                if(string.IsNullOrEmpty(source)||string.IsNullOrEmpty(target)||conflicts.Contains(source))return;
                if(nextAliases.TryGetValue(source,out var existing)&&existing!=target){nextAliases.Remove(source);conflicts.Add(source);return;}
                nextAliases[source]=target;
            }
            foreach(var entry in nextTable.Values) {
                if(entry==null||string.IsNullOrEmpty(entry.Key)||string.IsNullOrEmpty(entry.LocalizedValue))continue;
                Alias(entry.Key,entry.LocalizedValue);
                foreach(var operation in localeTables)Alias(operation.Result.GetEntry(entry.Key)?.LocalizedValue,entry.LocalizedValue);
            }
            foreach(var pair in nextAliases) {
                if(!Regex.IsMatch(pair.Key,@"\{[0-7]\}"))continue;
                var seen=new HashSet<string>();int offset=0;
                var pattern=new System.Text.StringBuilder("^",pair.Key.Length+40);
                foreach(Match token in Regex.Matches(pair.Key,@"\{([0-7])\}")) {
                    pattern.Append(Regex.Escape(pair.Key.Substring(offset,token.Index-offset)));
                    string name="p"+token.Groups[1].Value;
                    pattern.Append(seen.Add(name)?"(?<"+name+">.+?)":"\\k<"+name+">");
                    offset=token.Index+token.Length;
                }
                pattern.Append(Regex.Escape(pair.Key.Substring(offset))).Append("$");
                nextFormats.Add(Tuple.Create(new Regex(pattern.ToString(),RegexOptions.Singleline,System.TimeSpan.FromMilliseconds(30)),pair.Value));
            }
            // Specific messages win over a shorter, more general template.
            nextFormats.Sort((a,b)=>b.Item1.ToString().Length.CompareTo(a.Item1.ToString().Length));
            if(LanguageSelectionEnabled) {PlayerPrefs.SetString("UI.Language",code); PlayerPrefs.Save();}
            // Startup exposes deck editing immediately, so its translated card text must be ready too.
            if(!catalogs.TryGetValue(code,out var translated))
            {
                string path=Path.Combine(Application.streamingAssetsPath,"CardTranslations",code+".txt");
                translated=await Task.Run(()=>ReadCards(path));
                if(this==null || request!=generation) return;
                catalogs[code]=translated;
            }
            // Publish one complete locale, rather than mixing its interface with
            // the previous locale's card catalog while a read is still pending.
            table=nextTable; Font=font.Result; TmpFont=tmp.Result;
            formats.Clear(); formats.AddRange(nextFormats);
            aliases.Clear();foreach(var pair in nextAliases)aliases[pair.Key]=pair.Value;
            textCache.Clear();ClearLanguageDiagnostics();
            cards=translated;
            Ready=true; Notify(); BindScene();
        }
        catch(Exception error)
        {
            Debug.LogError("Language resources: "+error);
            // A missing localization asset must not leave the menu permanently hidden.
            if(this!=null && request==generation)
            {
                string code=locale?.Identifier.Code ?? Language;
                // Keep usable glyph resources, but never carry another locale's
                // strings into this request. Missing card text falls back to
                // the canonical definition until the requested catalog reloads.
                table=null; formats.Clear();aliases.Clear();textCache.Clear();
                cards=catalogs.TryGetValue(code,out var cached) ? cached : new Dictionary<string,string[]>(StringComparer.Ordinal);
                if(LanguageSelectionEnabled) {PlayerPrefs.SetString("UI.Language",code); PlayerPrefs.Save();}
                Ready=true; Notify(); BindScene();
            }
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
        if(textCache.TryGetValue(source,out var cached))return cached;
        string result=DisplayCardIdentities(TranslateText(source,0));
        if(Ready&&Language=="en-US"&&result==source&&Regex.IsMatch(source,@"[\u3400-\u9fff]"))untranslatedInterface.Add(source);
        if(textCache.Count>=2048)textCache.Clear();
        textCache[source]=result;return result;
    }
    static string TranslateText(string source,int depth,bool native=false)
    {
        var exact=table?.GetEntry(source); if(!string.IsNullOrEmpty(exact?.LocalizedValue)) return exact.LocalizedValue;
        if(aliases.TryGetValue(source,out var localized))return localized;
        if(native&&NativeMessageText.TryTranslate(source,out var structured))return structured;
        foreach(var format in formats)
        {
            Match match;
            try {match=format.Item1.Match(source);}catch(RegexMatchTimeoutException){continue;}
            if(!match.Success)continue;
            if(native&&NativeMessageText.UnsafeArguments(match))continue;
            // A single replacement pass preserves literal mana braces in args;
            // repeated placeholders must refer to the same captured value.
            return Regex.Replace(format.Item2,@"\{([0-7])\}",token=>{
                var argument=match.Groups["p"+token.Groups[1].Value];
                if(!argument.Success)return token.Value;
                return native&&depth<2?TranslateText(argument.Value,depth+1,true):argument.Value;
            });
        }
        if(!native)return source;
        // Fixed-symbol productions from basic land subtypes have no printed
        // paragraph to bind. Preserve costs and symbols verbatim.
        var mana=Regex.Match(source,@"^(?<cost>(?:\{[^{}]+\}\s*[,，]?\s*)*:\s*)?Add (?<symbols>(?:\{[^{}]+\}\s*)+)\.?$");
        if(mana.Success && Language!="en-US")
            return mana.Groups["cost"].Value+(Language=="ja-JP"?mana.Groups["symbols"].Value.Trim()+"を加える。":"加"+mana.Groups["symbols"].Value.Trim()+"。");
        var identity=Regex.Match(source,@"^(.+) \(([0-9]+)\)$");
        if(identity.Success)return CardFaceName(identity.Groups[1].Value)+" ("+identity.Groups[2].Value+")";
        string name=CardFaceName(source);
        if(Ready&&Language!="en-US"&&name==source&&Regex.IsMatch(Regex.Replace(source,@"\{[^}]*\}|<[^>]*>",""),@"[A-Za-z]{2,}"))RememberUntranslated(source);
        return name;
    }
    // Display boundary only: request predicates and response IDs remain native.
    // Unknown prose is retained with diagnostics, never silently replaced by a
    // generic instruction that could change the meaning of a required choice.
    public static string NativeText(string source)
    {
        if(string.IsNullOrEmpty(source))return source;
        string value=TranslateText(source,0,true);
        if(value==source || value.Contains('\n')) {
            // A format can translate the first line while leaving an appended
            // native instruction untouched. Translate every remaining line too.
            var lines=value.Replace("\r","").Split('\n');
            for(int i=0;i<lines.Length;i++) {
                string line=lines[i].TrimEnd();
                if(line.StartsWith("one: ",StringComparison.Ordinal))line=line.Substring(5);
                else if(line.StartsWith("oneOrNone: ",StringComparison.Ordinal))line=line.Substring(11);
                string translated=TranslateText(line,0,true);
                if(translated==line) {
                    var identity=Regex.Match(line,@"^(.+) \(([0-9]+)\)$");
                    translated=identity.Success?CardFaceName(identity.Groups[1].Value)+" ("+identity.Groups[2].Value+")":CardFaceName(line);
                    var zone=Regex.Match(line,@"^(.+) \[(Hand|Graveyard|Exile|Library|Battlefield|Command)\]$");
                    if(zone.Success)translated=CardFaceName(zone.Groups[1].Value)+" ["+T(zone.Groups[2].Value)+"]";
                }
                lines[i]=translated;
                if(Ready&&Language!="en-US"&&translated==line&&Regex.IsMatch(line,@"[A-Za-z]{2,}"))
                    RememberUntranslated(line);
            }
            value=string.Join("\n",lines);
        }
        value=DisplayCardIdentities(value);
        BattleTextDiagnostics.Record("native",source,value);
        return value;
    }
    static void RememberUntranslated(string source)
    {
        if(untranslatedNative.Count>=512)untranslatedNative.Clear();
        if(source.Length>2048)source=source.Substring(0,2048);
        untranslatedNative.Add(source);
    }
    internal static bool IsCardIdentity(string value)
    {
        string name=Regex.Replace(value??"",@" \([0-9]+\)$","");
        return cards.ContainsKey(name);
    }
    static Dictionary<string,string[]> identityCatalog;
    static HashSet<string> identityNames;
    // Native object numbers remain on the wire. Hide them only when they are
    // attached to a known card name; mana, X, stats and ordinary counts survive.
    public static string DisplayCardIdentities(string value)
    {
        if(string.IsNullOrEmpty(value)||value.IndexOf(" (",StringComparison.Ordinal)<0)return value;
        EnsureIdentityNames();
        return Regex.Replace(value,@" \([0-9]+\)",match=> {
            int first=Math.Max(0,match.Index-200);
            for(int start=first;start<match.Index;start++) {
                if(start>0&&value[start-1]<128&&char.IsLetterOrDigit(value[start-1]))continue;
                if(identityNames.Contains(value.Substring(start,match.Index-start)))return "";
            }
            return match.Value;
        });
    }
    static void EnsureIdentityNames()
    {
        if(identityCatalog!=cards) {
            identityCatalog=cards;identityNames=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in cards) {
                identityNames.Add(entry.Key);
                if(entry.Value.Length>1&&!string.IsNullOrEmpty(entry.Value[1]))identityNames.Add(entry.Value[1]);
            }
        }
    }
    public static string CardName(CardDefinition card) => Field(card,1,card?.cardName ?? "");
    public static string CardType(CardDefinition card) => CardTypeText.Line(Field(card,2,card?.GetTypeLine() ?? ""));
    public static string CardRules(CardDefinition card) => LocalizedRules(card?.cardName,card?.description,card?.PrintingKey,
        DisplayRules(Field(card,3,card?.description ?? ""),card!=null&&card.cardTypes.Has(global::CardType.Planeswalker)));
    public static string FeatureName(CardFeatureDefinition feature) => Field(feature?.cardName, 1, feature?.cardName ?? "");
    public static string CardFaceName(string canonicalName)
    {
        string name=canonicalName??"";
        if(!cards.ContainsKey(name))name=Regex.Replace(name,@" \([0-9]+\)$","");
        string localized=Field(name,1,name);
        if(localized==name&&!cards.ContainsKey(name))localized=DeckTokenLinks.GeneratedTokenName(name)??localized;
        EnsureIdentityNames();identityNames.Add(name);identityNames.Add(localized);
        string display=DisplayCardIdentities(localized);identityNames.Add(display);return display;
    }
    public static string FeatureType(CardFeatureDefinition feature, string fallback) => CardTypeText.Line(Field(feature?.cardName, 2, fallback));
    public static string FeatureRules(CardFeatureDefinition feature) => LocalizedRules(feature?.cardName,feature?.description,null,
        DisplayRules(Field(feature?.cardName, 3, feature?.description ?? ""),feature!=null&&feature.cardTypes.Has(global::CardType.Planeswalker)));
    static string LocalizedRules(string name,string source,string printing,string display)
    {
        if(Ready&&Language!="en-US"&&BattleTextDiagnostics.HasEnglishProse(display)) {
            display=string.Join("\n",display.Replace("\\n","\n").Split('\n').Select(line=>NativeMessageText.TryTranslate(line,out var text)?text:line));
            if(BattleTextDiagnostics.HasEnglishProse(display))
                BattleTextDiagnostics.Record("catalog-rules",source,display,new ForgeCardView{name=name,printingKey=printing});
        }
        return display;
    }
    static string DisplayRules(string rules,bool planeswalker) => !planeswalker ? rules :
        Regex.Replace(rules,@"(?:^|[\r\n\u3000])(?:忠誠度|忠诚值|Loyalty)\s*[:：]\s*[0-9０-９]+\s*$",string.Empty,RegexOptions.IgnoreCase);
    static string Field(CardDefinition card,int column,string fallback) => Field(card?.cardName,column,fallback);
    static string Field(string name,int column,string fallback) => !string.IsNullOrEmpty(name) && cards.TryGetValue(name,out var value) && !string.IsNullOrWhiteSpace(value[column]) ? value[column] : fallback;

    // Search the same published locale as the displayed card text. Shared Chinese/Japanese
    // characters must not match another locale's names, types, rules, or secondary faces.
    public static bool MatchesCurrentCardTranslation(CardDefinition card, string query, int field = 0)
    {
        if (!Ready || card == null || string.IsNullOrEmpty(query)) return false;
        if (MatchesTranslation(cards, card.cardName, query, field)) return true;
        if (card.features == null) return false;
        foreach (CardFeatureDefinition feature in card.features)
            if (feature != null && MatchesTranslation(cards, feature.cardName, query, field)) return true;
        return false;
    }

    static bool MatchesTranslation(Dictionary<string, string[]> catalog, string cardName, string query, int field)
    {
        if (catalog==null || string.IsNullOrEmpty(cardName) || !catalog.TryGetValue(cardName, out var fields)) return false;
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
    private static void BindNewUi()
    {
        // Registered graphics belong to their nearest canvas. Discover new
        // visible labels without rescanning hidden hierarchies or reapplying
        // every unchanged font, dropdown layout and button twice each second.
        foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            var graphics=GraphicRegistry.GetGraphicsForCanvas(canvas);
            for(int i=0;i<graphics.Count;i++)
            {
                var graphic=graphics[i];
                if(graphic==null||graphic is not Text&&graphic is not TMP_Text
                    ||graphic.GetComponent<PreferenceText>()!=null)continue;
                if(graphic.transform.parent!=null&&graphic.transform.parent.GetComponent<PlayerNameMarquee>()!=null)continue;
                graphic.gameObject.AddComponent<PreferenceText>().Apply();
                var dropdown=graphic.GetComponentInParent<Dropdown>();if(dropdown!=null)DropdownChevron.Apply(dropdown);
                var button=graphic.GetComponentInParent<Button>();if(button!=null)DisabledButtonVisual.Bind(button);
            }
        }
    }
}
