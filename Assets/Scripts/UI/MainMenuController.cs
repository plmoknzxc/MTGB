using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed partial class MainMenuController : MonoBehaviour
{
    private const float HoverPreviewDelay = 0.75f;

    private readonly List<CardDefinition> cards = new List<CardDefinition>();
    private readonly List<DeckDefinition> decks = new List<DeckDefinition>();

    [SerializeField] private Dropdown deckDropdown;
    [SerializeField] private InputField deckNameInput;
    [SerializeField] private InputField countInput;
    [SerializeField] private Text collectionCountText;
    [SerializeField] private Text mainDeckCountText;
    [SerializeField] private Text sideboardCountText;
    [SerializeField] private Text cardInfoText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text folderText;
    [SerializeField] private CanvasGroup startScreenGroup;
    [SerializeField] private CanvasGroup deckBuilderGroup;
    [SerializeField] private Button onlineMatchButton;
    [SerializeField] private Button deckEditorButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button backToTitleButton;
    [SerializeField] private Button advancedSearchButton;
    [SerializeField] private Button newDeckButton;
    [SerializeField] private Button saveDeckButton;
    [SerializeField] private Button reloadButton;
    [SerializeField] private Button addMainButton;
    [SerializeField] private Button addSideboardButton;
    [SerializeField] private Button startButton;
    [SerializeField] private RectTransform collectionContent;
    [SerializeField] private RectTransform mainDeckContent;
    [SerializeField] private RectTransform sideboardContent;
    [SerializeField] private CanvasGroup cardPreviewGroup;
    [SerializeField] private Image cardPreviewImage;
    [SerializeField] private Text cardPreviewText;

    private DeckDefinition currentDeck;
    private DeckCardDetailsView cardDetailsView;
    private string artworkLanguage;
    private CardDefinition detailTokenArtCard;
    private List<DeckRelatedTokenArt.Art> detailTokenArts;
    private Coroutine detailTokenLoad;
    private CardArtPanel cardArtPanel;
    private readonly CancellationTokenSource loadingCancellation = new CancellationTokenSource();
    private bool dataReady;
    private bool dataLoading;
    private UnityEngine.UI.Text loadingStatus;
    private UnityEngine.UI.Button retryLoadButton;
    private readonly System.Diagnostics.Stopwatch startupTimer = System.Diagnostics.Stopwatch.StartNew();
    private CardDefinition selectedCard;
    private MainMenuCardTile hoveredTile;
    private MainMenuCardTile highlightedTile;
    private MainMenuCardTile quantityTile;
    private CardDefinition quantityCard;
    private MainMenuCardTileZone quantityZone;
    private float hoverStartedAt;
    private bool previewVisible;
    private bool listenersRegistered;
    private bool hasUnsavedDeckChanges;
    private string lastDeckSaveError;
    private bool suppressDeckDropdownEvent;
    private string collectionQuery = string.Empty;
    private UnityEngine.UI.InputField collectionSearchInput;
    private AdvancedCardSearch advancedSearch = new AdvancedCardSearch();
    private Func<CardDefinition, bool> advancedPredicate;
    private int collectionPage;
    private Button previousCollectionPageButton;
    private Button nextCollectionPageButton;
    private Text collectionPageText;
    private const int CollectionPageSize = 18;
    private Coroutine collectionLoad;
    private readonly List<CardDefinition> filteredCollection = new List<CardDefinition>();
    private string cachedCollectionQuery;
    private int cachedCollectionFilter = -1;
    private int collectionContentFilter;


    private int loadedDeckIndex = -1;
    private string deckDirectoryRevision;
    private float nextDeckScan;

    private void RefreshDeckFiles(bool force=false)
    {
        if(!dataReady)return;
        string revision=DeckRepository.DirectoryRevision();
        if(!force&&revision==deckDirectoryRevision)return;
        deckDirectoryRevision=revision;
        string selectedPath=loadedDeckIndex>=0&&loadedDeckIndex<decks.Count?decks[loadedDeckIndex].sourcePath:currentDeck?.sourcePath;
        string selectedName=currentDeck?.name;
        decks.Clear();decks.AddRange(DeckRepository.LoadAll());
        loadedDeckIndex=decks.FindIndex(d=>!string.IsNullOrEmpty(selectedPath)?string.Equals(d.sourcePath,selectedPath,StringComparison.OrdinalIgnoreCase):string.Equals(d.name,selectedName,StringComparison.OrdinalIgnoreCase));
        if(!hasUnsavedDeckChanges){
            if(loadedDeckIndex<0)loadedDeckIndex=decks.Count>0?0:-1;
            currentDeck=loadedDeckIndex>=0?CloneDeck(decks[loadedDeckIndex]):new DeckDefinition{name="New Deck"};
            deckNameInput.SetTextWithoutNotify(currentDeck.name);
            ApplyDeckLegalitySelection();RefreshDeckGrids();RefreshSelectedCardInfo();
        }
        RefreshDeckDropdown(currentDeck?.name);UpdateDeleteDeckButton();
    }
    private void OnApplicationFocus(bool focused){if(focused){nextDeckScan=0;RefreshDeckFiles();}}
    private Action pendingUnsavedProceed;
    private Action pendingUnsavedCancel;
    private Action pendingDataEntry;
    private CanvasGroup startMenuContent;
    private MainMenuCardTile draggedTile;
    private GameObject dragGhost;
    private RectTransform dragGhostRect;
    private CanvasGroup draggedTileGroup;
    private CanvasGroup unsavedChangesDialogGroup;
    private Text unsavedChangesMessageText;
    private MainMenuDropZone highlightedDropZone = MainMenuDropZone.None;

    private enum MainMenuDropZone
    {
        None,
        Collection,
        MainDeck,
        Sideboard
    }

    private void Awake()
    {
        BindSceneReferences();
        CreateCollectionSearchToolbar();
        CreateDialogButton(deckDropdown.transform.parent, "RenameDeckButton", "重命名",
            new Vector2(-100f, 482f), MenuVisualTheme.Panel, BeginRenameDeck);
        MenuVisualTheme.ApplyMain(startScreenGroup.transform.parent);
        CreateCardPreviewChrome();
        CreateDeckCollapseControls();
        CreateDeckExchangeToolbar();
        CreateAuxiliaryDeckEntry();
        RegisterListeners();
        ShowStartScreen();
        loadingStatus = AddText(startScreenGroup.transform, "LoadingStatus", "正在准备卡牌…", 20,
            MenuVisualTheme.Muted, TextAnchor.MiddleLeft);
        Place(loadingStatus.rectTransform, new Vector2(-570f, -305f), new Vector2(570f, 62f));
        loadingStatus.gameObject.SetActive(false);
        retryLoadButton = CreateDialogButton(startScreenGroup.transform, "RetryLoadButton", "重试加载",
            new Vector2(-570f, -375f), MenuVisualTheme.Panel, ReloadData);
        retryLoadButton.gameObject.SetActive(false);
        EnableMenuEntrypoints();
        GamePreferences.Changed += RefreshLocalizedDetails;
        // Do not render authored Chinese labels before the saved locale is ready.
        startMenuContent = startScreenGroup.transform.Find("StartMenuContent").GetComponent<CanvasGroup>();
        SetScreenVisible(startMenuContent, false);
    }

    private System.Collections.IEnumerator Start()
    {
        _ = LoadDataAsync(false);
        SetScreenVisible(startMenuContent, false);
        while (!GamePreferences.Ready || dataLoading) yield return null;
        GamePreferences.BindScene();
        SetScreenVisible(startMenuContent, true);
        Debug.Log($"[Startup] Localized menu ready: {startupTimer.ElapsedMilliseconds} ms; locale={GamePreferences.Language}");
        if (ReplayBrowser.ReopenOnReturn) { ReplayBrowser.ReopenOnReturn = false; ReplayBrowser.Show(); }
    }

    private void EnableMenuEntrypoints()
    {
        // Navigation accepts clicks immediately; data-dependent entries finish after loading.
        if (deckEditorButton != null) deckEditorButton.interactable = true;
        if (onlineMatchButton != null) onlineMatchButton.interactable = true;
        if (settingsButton != null) settingsButton.interactable = true;
    }

    private bool AwaitCardData(Action enter)
    {
        if (dataReady) return false;
        pendingDataEntry = enter;
        if (dataLoading) loadingStatus.text = GamePreferences.T("正在准备卡牌…");
        loadingStatus.gameObject.SetActive(true);
        return true;
    }

    private void Update()
    {
        if(Time.unscaledTime>=nextDeckScan){nextDeckScan=Time.unscaledTime+1;RefreshDeckFiles();}
        DismissQuantityControlsFromPointer();
        if (hoveredTile == null || previewVisible) return;
        if (Time.unscaledTime - hoverStartedAt < (detailsCollapsed ? .55f : HoverPreviewDelay)) return;
        ShowPreview(hoveredTile.Card);
    }

    private void DismissQuantityControlsFromPointer()
    {
        if (quantityTile == null || !quantityTile.QuantityControlsVisible) return;

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;
        if (mouse.rightButton.wasPressedThisFrame)
        {
            CloseQuantityControls();
            return;
        }
        if (!mouse.leftButton.wasPressedThisFrame || EventSystem.current == null) return;

        var pointer = new PointerEventData(EventSystem.current)
        {
            position = mouse.position.ReadValue()
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        bool insideSelectedCard = false;
        foreach (var hit in hits)
        {
            if (hit.gameObject != null && hit.gameObject.transform.IsChildOf(quantityTile.transform))
            {
                insideSelectedCard = true;
                break;
            }
        }
        if (!insideSelectedCard) CloseQuantityControls();
    }

    private void CloseQuantityControls()
    {
        if (quantityTile != null) quantityTile.SetQuantityControls(false, 0, false);
        quantityTile = null;
        quantityCard = null;
    }

    private void OnDestroy()
    {
        DeckRelatedTokenArt.Clear();
        if(detailsArtHover!=null)detailsArtHover.Hide();
        GamePreferences.Changed -= RefreshLocalizedDetails;
        cardDetailsView?.Dispose();
        loadingCancellation.Cancel();
        loadingCancellation.Dispose();
        UnregisterListeners();
        DestroyDragGhost();
    }

    private void BindSceneReferences()
    {
        if (deckDropdown == null) deckDropdown = FindSceneComponent<Dropdown>("DeckDropdown");
        if (deckNameInput == null) deckNameInput = FindSceneComponent<InputField>("DeckNameInput");
        if (countInput == null) countInput = FindSceneComponent<InputField>("CountInput");
        if (collectionCountText == null) collectionCountText = FindSceneComponent<Text>("CollectionCountText");
        if (mainDeckCountText == null) mainDeckCountText = FindSceneComponent<Text>("MainDeckCountText");
        if (sideboardCountText == null) sideboardCountText = FindSceneComponent<Text>("SideboardCountText");
        if (cardInfoText == null) cardInfoText = FindSceneComponent<Text>("CardInfo");
        if (statusText == null) statusText = FindSceneComponent<Text>("StatusText");
        if (folderText == null) folderText = FindSceneComponent<Text>("FolderText");
        if (startScreenGroup == null) startScreenGroup = FindSceneComponent<CanvasGroup>("StartScreen");
        if (deckBuilderGroup == null) deckBuilderGroup = FindSceneComponent<CanvasGroup>("DeckBuilderScreen");
        if (onlineMatchButton == null) onlineMatchButton = FindSceneComponent<Button>("OnlineMatchButton");
        if (deckEditorButton == null) deckEditorButton = FindSceneComponent<Button>("DeckEditorButton");
        if (settingsButton == null) settingsButton = FindSceneComponent<Button>("SettingsButton");
        if (backToTitleButton == null) backToTitleButton = FindSceneComponent<Button>("BackToTitleButton");
        if (advancedSearchButton == null) advancedSearchButton = FindSceneComponent<Button>("AdvancedSearchButton");
        if (newDeckButton == null) newDeckButton = FindSceneComponent<Button>("NewDeckButton");
        if (saveDeckButton == null) saveDeckButton = FindSceneComponent<Button>("SaveDeckButton");
        if (reloadButton == null) reloadButton = FindSceneComponent<Button>("ReloadButton");
        if (addMainButton == null) addMainButton = FindSceneComponent<Button>("AddMainButton");
        if (addSideboardButton == null) addSideboardButton = FindSceneComponent<Button>("AddSideboardButton");
        if (startButton == null) startButton = FindSceneComponent<Button>("StartButton");
        if (collectionContent == null) collectionContent = FindSceneComponent<RectTransform>("CollectionContent");
        if (mainDeckContent == null) mainDeckContent = FindSceneComponent<RectTransform>("MainDeckContent");
        if (sideboardContent == null) sideboardContent = FindSceneComponent<RectTransform>("SideboardContent");
        if (cardPreviewGroup == null) cardPreviewGroup = FindSceneComponent<CanvasGroup>("CardPreviewPanel");
        if (cardPreviewImage == null) cardPreviewImage = FindSceneComponent<Image>("CardPreviewImage");
        if (cardPreviewText == null) cardPreviewText = FindSceneComponent<Text>("CardPreviewText");
        if (cardPreviewText == null) cardPreviewText = cardInfoText;

        if (folderText != null)
        {
            folderText.text = $"Forge: {ForgeCardCatalog.Root}\nDecks: {DeckRepository.DeckRoot}";
        }

        if (countInput != null && string.IsNullOrWhiteSpace(countInput.text))
        {
            countInput.text = "1";
        }

        NormalizeScrollViewport(collectionContent);
        NormalizeScrollViewport(mainDeckContent);
        NormalizeScrollViewport(sideboardContent);
        EnsureUnsavedChangesDialog();
    }

    private void RegisterListeners()
    {
        if (listenersRegistered) return;
        listenersRegistered = true;

        if (deckDropdown != null) deckDropdown.onValueChanged.AddListener(OnDeckSelected);
        if (deckNameInput != null) deckNameInput.onEndEdit.AddListener(OnDeckNameChanged);
        if (onlineMatchButton != null) onlineMatchButton.onClick.AddListener(OnOnlineMatch);
        if (deckEditorButton != null) deckEditorButton.onClick.AddListener(ShowDeckBuilder);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnReplays);
        if (backToTitleButton != null) backToTitleButton.onClick.AddListener(ShowStartScreen);
        if (advancedSearchButton != null) advancedSearchButton.onClick.AddListener(OnAdvancedSearch);
        if (newDeckButton != null) newDeckButton.onClick.AddListener(OnNewDeck);
        if (saveDeckButton != null) saveDeckButton.onClick.AddListener(OnSaveDeck);
        if (reloadButton != null) reloadButton.onClick.AddListener(OnReloadClicked);
        if (addMainButton != null) addMainButton.onClick.AddListener(OnAddSelectedToMain);
        if (addSideboardButton != null) addSideboardButton.onClick.AddListener(OnAddSelectedToSideboard);
        if (startButton != null) startButton.onClick.AddListener(OnStartMatch);
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered) return;
        listenersRegistered = false;

        if (deckDropdown != null) deckDropdown.onValueChanged.RemoveListener(OnDeckSelected);
        if (deckNameInput != null) deckNameInput.onEndEdit.RemoveListener(OnDeckNameChanged);
        if (onlineMatchButton != null) onlineMatchButton.onClick.RemoveListener(OnOnlineMatch);
        if (deckEditorButton != null) deckEditorButton.onClick.RemoveListener(ShowDeckBuilder);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnReplays);
        if (backToTitleButton != null) backToTitleButton.onClick.RemoveListener(ShowStartScreen);
        if (advancedSearchButton != null) advancedSearchButton.onClick.RemoveListener(OnAdvancedSearch);
        if (newDeckButton != null) newDeckButton.onClick.RemoveListener(OnNewDeck);
        if (saveDeckButton != null) saveDeckButton.onClick.RemoveListener(OnSaveDeck);
        if (reloadButton != null) reloadButton.onClick.RemoveListener(OnReloadClicked);
        if (addMainButton != null) addMainButton.onClick.RemoveListener(OnAddSelectedToMain);
        if (addSideboardButton != null) addSideboardButton.onClick.RemoveListener(OnAddSelectedToSideboard);
        if (startButton != null) startButton.onClick.RemoveListener(OnStartMatch);
    }

    private void ReloadData()
    {
        _ = LoadDataAsync(true);
    }

    private async Task LoadDataAsync(bool forceReload)
    {
        if (dataLoading) return;
        bool reopenDeck = IsDeckBuilderVisible();
        dataLoading = true;
        dataReady = false;
        retryLoadButton.gameObject.SetActive(false);
        loadingStatus.gameObject.SetActive(false);
        ShowStartScreenNow();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var loadedCards = await CardDatabaseRepository.LoadAllAsync(forceReload, loadingCancellation.Token,
                null);
            if (this == null) return;
            ApplyLoadedData(loadedCards);
            dataReady = true;

            loadingStatus.gameObject.SetActive(false);
            Debug.Log($"[Startup] Data ready: {timer.ElapsedMilliseconds} ms; cards={cards.Count}; catalog tiles={collectionContent.childCount}");
            if (reopenDeck) ShowDeckBuilder();
            OnlineMatchService service = OnlineMatchService.Instance;
            if (service != null && service.IsConnected && !service.IsInMatch)
                OnlineLobbyPanel.Show(this, currentDeck);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (this == null) return;
            loadingStatus.text = "加载失败：" + exception.Message;
            loadingStatus.gameObject.SetActive(true);
            retryLoadButton.gameObject.SetActive(true);
            Debug.LogWarning("Card loading failed: " + exception.Message);
        }
        finally { dataLoading = false; }
        var enter = pendingDataEntry;
        pendingDataEntry = null;
        if (dataReady) enter?.Invoke();
    }

    private void ApplyLoadedData(IReadOnlyList<CardDefinition> loadedCards)
    {
        deckExchange=null;
        CardImageLibrary.ClearCache();

        cards.Clear();
        cards.AddRange(loadedCards);
        cachedCollectionQuery = null;

        decks.Clear();
        decks.AddRange(DeckRepository.LoadAll());
        if (decks.Count == 0 && cards.Count > 0 && !DeckRepository.WasExplicitlyEmptied)
        {
            DeckDefinition starter = DeckRepository.CreateDefaultDeck(cards);
            DeckRepository.Save(starter);
            decks.AddRange(DeckRepository.LoadAll());
        }

        loadedDeckIndex = decks.Count > 0 ? 0 : -1;
        currentDeck = loadedDeckIndex >= 0 ? CloneDeck(decks[loadedDeckIndex]) : new DeckDefinition { name = "New Deck" };
        ApplyDeckLegalitySelection();
        UpdateDeleteDeckButton();
        selectedCard = cards.Count > 0 ? cards[0] : null;
        hasUnsavedDeckChanges = false;
        ClearHighlightedTile();
        RefreshDeckDropdown();
        SetStatus(cards.Count > 0
            ? $"Loaded {cards.Count} cards and {decks.Count} decks."
            : "No cards loaded. Put databases under CardDatabases before building a deck.");
        if (CardDatabaseRepository.LoadWarnings.Count > 0)
            SetStatus(string.Join("\n", CardDatabaseRepository.LoadWarnings));
    }

    private void ShowStartScreen()
    {
        if (IsDeckBuilderVisible() && hasUnsavedDeckChanges)
        {
            ShowUnsavedChangesDialog(ShowStartScreenNow);
            return;
        }

        ShowStartScreenNow();
    }

    private void ShowStartScreenNow()
    {
        SetOnlineLobbyVisible(false);
    }

    private void ShowDeckBuilder()
    {
        RefreshDeckFiles();
        if (AwaitCardData(ShowDeckBuilder)) return;
        SetScreenVisible(startScreenGroup, false);
        SetScreenVisible(deckBuilderGroup, true);
        RefreshAllViews();
    }

    public void SetOnlineLobbyVisible(bool visible)
    {
        CloseAdvancedSearch();
        SetScreenVisible(startScreenGroup, !visible);
        SetScreenVisible(deckBuilderGroup, false);
    }

    private void OnReplays()
    {
        pendingDataEntry = null;
        loadingStatus.gameObject.SetActive(false);
        ReplayBrowser.Show();
    }

    private void RefreshLocalizedDetails()
    {
        RefreshAuxiliaryDeckLocale();
        bool repaintArtwork=artworkLanguage!=GamePreferences.Language;
        artworkLanguage=GamePreferences.Language;
        if(repaintArtwork)detailTokenArtCard=null;
        RefreshSelectedCardInfo();
        if(deckBuilderGroup==null) return;
        foreach(var tile in deckBuilderGroup.GetComponentsInChildren<MainMenuCardTile>(true))
        {
            if(tile.Card==null) continue;
            if(repaintArtwork)
            {
                var face=tile.transform.Find("CardFace")?.GetComponent<Image>();
                bool hasArt=CardImageLibrary.TryLoadFullCard(tile.Card,out var localizedArt);
                if(face!=null)
                {
                    face.sprite=hasArt?localizedArt:CardVisualStyle.LoadFrame(tile.Card);
                    face.color=face.sprite==null?GetFallbackColor(tile.Card):Color.white;
                }
                foreach(string name in new[]{"FallbackText","FallbackOverlay"})
                {
                    var label=tile.transform.Find(name);
                    if(label!=null)label.gameObject.SetActive(!hasArt);
                }
                if(!hasArt && tile.transform.Find("FallbackText")==null && tile.transform.Find("FallbackOverlay")==null)
                {
                    Text missing=AddText(tile.transform,"FallbackOverlay",GamePreferences.CardName(tile.Card)+"\n"+GamePreferences.CardType(tile.Card),11,Color.white,TextAnchor.UpperCenter);
                    Stretch(missing.rectTransform,10f,10f,10f,10f);
                }
            }
            var fallback=tile.transform.Find("FallbackText")?.GetComponent<Text>();
            if(fallback!=null) fallback.text=GamePreferences.CardName(tile.Card)+"\n"+tile.Card.manaCost;
            var overlay=tile.transform.Find("FallbackOverlay")?.GetComponent<Text>();
            if(overlay!=null) overlay.text=GamePreferences.CardName(tile.Card)+"\n"+GamePreferences.CardType(tile.Card);
            if(tile.Zone!=MainMenuCardTileZone.Collection) ApplyLegality(tile);
        }
    }

    private void CreateCollectionSearchToolbar()
    {
        Transform parent = deckDropdown.transform.parent;
        var go = new GameObject("CollectionSearchInput", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.InputField));
        go.transform.SetParent(parent, false);
        collectionSearchInput = go.GetComponent<UnityEngine.UI.InputField>();
        collectionSearchInput.targetGraphic = go.GetComponent<UnityEngine.UI.Image>();
        collectionSearchInput.textComponent = AddText(go.transform, "Text", string.Empty, 18, Color.white, TextAnchor.MiddleLeft);
        collectionSearchInput.textComponent.supportRichText = false;
        collectionSearchInput.textComponent.verticalOverflow = VerticalWrapMode.Overflow;
        Stretch(collectionSearchInput.textComponent.rectTransform, 12f, 4f, 12f, 4f);
        Text placeholder = AddText(go.transform, "Placeholder", "搜索卡名 / 规则文字", 18, MenuVisualTheme.Muted, TextAnchor.MiddleLeft);
        placeholder.verticalOverflow = VerticalWrapMode.Overflow;
        Stretch(placeholder.rectTransform, 12f, 4f, 12f, 4f);
        collectionSearchInput.placeholder = placeholder;
        collectionSearchInput.lineType = UnityEngine.UI.InputField.LineType.SingleLine;
        collectionSearchInput.onSubmit.AddListener(ApplyCollectionQuery);
        CreateDialogButton(parent, "CollectionSearchButton", "搜索", Vector2.zero,
            MenuVisualTheme.Panel, () => ApplyCollectionQuery(collectionSearchInput.text));
        CreateDialogButton(parent, "CollectionSearchResetButton", "重置条件", Vector2.zero,
            MenuVisualTheme.Panel, ResetCollectionSearch);
    }

    private void ResetCollectionSearch()
    {
        advancedSearch = new AdvancedCardSearch();
        advancedPredicate = null;
        collectionContentFilter = 0;
        cachedCollectionQuery = null;
        ApplyCollectionQuery(string.Empty);
    }

    private void ApplyCollectionQuery(string value)
    {
        collectionQuery = (value ?? string.Empty).Trim();
        collectionSearchInput.SetTextWithoutNotify(collectionQuery);
        collectionPage = 0;
        RefreshCollectionGrid();
        var scroll = collectionContent.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    private void OnAdvancedSearch()
    {
        if (!Application.isPlaying || !IsDeckBuilderVisible()) return;
        if (GetRootCanvas().transform.Find("AdvancedSearch") != null) return;
        var draft = advancedSearch.Copy();
        draft.Query = collectionSearchInput != null ? collectionSearchInput.text.Trim() : collectionQuery;
        draft.Source = collectionContentFilter;
        AdvancedSearchPanel.Show(GetRootCanvas(), deckDropdown, draft, next =>
        {
            var predicate = next.Copy(); predicate.Query = ""; predicate.Source = 0;
            var compiled = predicate.Compile(cards);
            advancedSearch = next; advancedPredicate = compiled;
            collectionContentFilter = next.Source; cachedCollectionQuery = null;
            ApplyCollectionQuery(next.Query);
        });
    }

    private void CloseAdvancedSearch()
    {
        var canvas = GetRootCanvas();
        if (canvas == null) return;
        foreach (var panel in canvas.GetComponentsInChildren<AdvancedSearchPanel>(true))
        {
            panel.gameObject.SetActive(false);
            Destroy(panel.gameObject);
        }
    }

    private static void SetScreenVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
        group.gameObject.SetActive(visible);
    }

    private void RefreshAllViews()
    {
        RefreshCollectionGrid();
        RefreshDeckGrids();
        RefreshSelectedCardInfo();
    }

    private void RefreshCollectionGrid()
    {
        if (collectionLoad != null) { StopCoroutine(collectionLoad); collectionLoad = null; }
        ClearHighlightedTileInZone(MainMenuCardTileZone.Collection);
        ClearChildren(collectionContent);
        if (collectionContent == null) return;

        if (cachedCollectionQuery != collectionQuery || cachedCollectionFilter != collectionContentFilter)
        {
            filteredCollection.Clear();
            for (int i = 0; i < cards.Count; i++)
                if (MatchesCollectionFilter(cards[i], collectionQuery, collectionContentFilter) && (advancedPredicate == null || advancedPredicate(cards[i]))) filteredCollection.Add(cards[i]);
            advancedSearch.Order(filteredCollection);
            cachedCollectionQuery = collectionQuery;
            cachedCollectionFilter = collectionContentFilter;
        }
        var matches = filteredCollection;
        int pages = Mathf.Max(1, (matches.Count + CollectionPageSize - 1) / CollectionPageSize);
        collectionPage = Mathf.Clamp(collectionPage, 0, pages - 1);
        int start = collectionPage * CollectionPageSize;
        collectionLoad = StartCoroutine(LoadCollectionPage(matches.GetRange(start, Mathf.Min(CollectionPageSize, matches.Count - start))));
        RefreshCollectionPagination(pages);
        SetText(collectionCountText, $"卡牌 {matches.Count} / {cards.Count}");
        var scroll = collectionContent.GetComponentInParent<ScrollRect>(true);
        if (scroll != null)
        {
            scroll.StopMovement();
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
            if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.SetValueWithoutNotify(1f);
        }
    }

    private System.Collections.IEnumerator LoadCollectionPage(List<CardDefinition> page)
    {
        // Yield before decoding images so the click and page label render immediately.
        yield return null;
        for (int i = 0; i < page.Count; i++)
        {
            CreateCardTile(collectionContent, page[i], 0, MainMenuCardTileZone.Collection);
            if (i % 2 == 1) yield return null;
        }
        collectionLoad = null;
    }

    private void RefreshCollectionPagination(int pages)
    {
        if (collectionPageText == null)
        {
            var bar = new GameObject("CollectionPagination", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(collectionContent.GetComponentInParent<ScrollRect>(true).transform.parent, false);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(.755f, .01f);
            barRect.anchorMax = new Vector2(.99f, .07f);
            barRect.offsetMin = barRect.offsetMax = Vector2.zero;
            bar.GetComponent<Image>().color = MenuVisualTheme.Panel;
            previousCollectionPageButton = CreateDialogButton(bar.transform, "PreviousPage", "‹  上一页",
                Vector2.zero, MenuVisualTheme.Panel, () => ChangeCollectionPage(-1));
            nextCollectionPageButton = CreateDialogButton(bar.transform, "NextPage", "下一页  ›",
                Vector2.zero, MenuVisualTheme.Panel, () => ChangeCollectionPage(1));
            var previousRect = previousCollectionPageButton.GetComponent<RectTransform>();
            previousRect.anchorMin = previousRect.anchorMax = new Vector2(0, .5f);
            previousRect.anchoredPosition = new Vector2(52, 0);
            previousRect.sizeDelta = new Vector2(100, 42);
            var nextRect = nextCollectionPageButton.GetComponent<RectTransform>();
            nextRect.anchorMin = nextRect.anchorMax = new Vector2(1, .5f);
            nextRect.anchoredPosition = new Vector2(-52, 0);
            nextRect.sizeDelta = new Vector2(100, 42);
            collectionPageText = AddText(bar.transform, "PageNumber", string.Empty, 17,
                MenuVisualTheme.Paper, TextAnchor.MiddleCenter);
            Place(collectionPageText.rectTransform, Vector2.zero, new Vector2(190f, 42f));
            collectionPageText.raycastTarget = false;
        }
        previousCollectionPageButton.interactable = collectionPage > 0;
        previousCollectionPageButton.gameObject.SetActive(collectionPage > 0);
        nextCollectionPageButton.interactable = collectionPage + 1 < pages;
        nextCollectionPageButton.gameObject.SetActive(collectionPage + 1 < pages);
        collectionPageText.text = $"第 {collectionPage + 1} / {pages} 页";
    }

    private void ChangeCollectionPage(int direction)
    {
        collectionPage += direction;
        RefreshCollectionGrid();
    }

    public static bool MatchesCollectionFilter(CardDefinition card, string query, int category)
    {
        // Locale publication is atomic; do not search the previous text while a new locale loads.
        if (!GamePreferences.Ready) return false;
        if (card == null || (category == 1 && card.customContent) || (category == 2 && !card.customContent)) return false;
        query = query?.Trim() ?? string.Empty;
        return query.Length == 0 || (card.cardName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || GamePreferences.CardName(card).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || GamePreferences.CardRules(card).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || card.PrintingKey.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || (card.description ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || GamePreferences.MatchesCurrentCardTranslation(card, query);
    }

    private void RefreshDeckGrids()
    {
        ClearHighlightedTileInZone(MainMenuCardTileZone.MainDeck);
        ClearHighlightedTileInZone(MainMenuCardTileZone.Sideboard);
        ClearChildren(mainDeckContent);
        ClearChildren(sideboardContent);

        int mainCount = DeckRepository.GetTotalCount(currentDeck, false);
        int sideboardCount = DeckRepository.GetTotalCount(currentDeck, true);
        SetText(mainDeckCountText, $"主牌  {mainCount}");
        SetText(sideboardCountText, $"备牌  {sideboardCount}");

        if (deckNameInput != null && currentDeck != null) deckNameInput.text = currentDeck.name;
        PopulateDeckGrid(mainDeckContent, currentDeck?.cards, MainMenuCardTileZone.MainDeck);
        PopulateDeckGrid(sideboardContent, currentDeck?.sideboard, MainMenuCardTileZone.Sideboard);
        if (quantityTile != null && quantityZone == MainMenuCardTileZone.Collection && quantityTile.QuantityControlsVisible)
            quantityTile.SetQuantityControls(true, CountCardCopies(quantityCard, false),
                DeckRepository.GetAllowedCopiesToAdd(currentDeck, quantityCard) > 0);
        RefreshDeckLegality();
    }

    private void PopulateDeckGrid(RectTransform parent, List<DeckEntry> entries, MainMenuCardTileZone zone)
    {
        if (parent == null || entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            DeckEntry entry = entries[i];
            CardDefinition card = DeckRepository.ResolveCard(entry, cards);
            if (card == null) continue;
            CreateCardTile(parent, card, entry.count, zone);
        }
    }

    private void CreateCardTile(RectTransform parent, CardDefinition card, int count, MainMenuCardTileZone zone)
    {
        if (parent == null || card == null) return;

        GridLayoutGroup grid = parent.GetComponent<GridLayoutGroup>();
        Vector2 size = grid != null ? grid.cellSize : new Vector2(126f, 176f);

        GameObject tileObject = new GameObject(GetTileName(card, zone), typeof(RectTransform), typeof(Image), typeof(Outline), typeof(MainMenuCardTile));
        tileObject.transform.SetParent(parent, false);
        RectTransform rect = tileObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        Image background = tileObject.GetComponent<Image>();
        background.color = zone == MainMenuCardTileZone.Collection
            ? MenuVisualTheme.Raised
            : MenuVisualTheme.Panel;
        background.raycastTarget = true;

        Outline outline = tileObject.GetComponent<Outline>();
        outline.effectColor = MenuVisualTheme.Line;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Image face = AddImage(tileObject.transform, "CardFace", Color.white);
        Stretch(face.rectTransform, 4f, 4f, 4f, 4f);
        face.sprite = LoadCardSprite(card);
        face.preserveAspect = true;

        if (face.sprite == null)
        {
            face.color = GetFallbackColor(card);
            Text fallback = AddText(tileObject.transform, "FallbackText", $"{GamePreferences.CardName(card)}\n{card.manaCost}", 13, Color.white, TextAnchor.MiddleCenter);
            Stretch(fallback.rectTransform, 8f, 8f, 8f, 8f);
        }
        else if (!CardImageLibrary.TryLoadFullCard(card, out _))
        {
            Text overlay = AddText(tileObject.transform, "FallbackOverlay", $"{GamePreferences.CardName(card)}\n{GamePreferences.CardType(card)}", 11, new Color(0.04f, 0.04f, 0.04f, 0.95f), TextAnchor.UpperCenter);
            Stretch(overlay.rectTransform, 10f, 10f, 10f, size.y * 0.58f);
        }

        GameObject countBadge = null;
        if (count > 0)
        {
            Image countBackground = AddImage(tileObject.transform, "CountBackground", new Color(0f, 0f, 0f, 0.76f));
            countBadge = countBackground.gameObject;
            Place(countBackground.rectTransform, new Vector2(size.x * 0.34f, -size.y * 0.37f), new Vector2(size.x * 0.32f, 28f));

            Text countText = AddText(countBackground.transform, "CountText", $"x{count}", 18, Color.white, TextAnchor.MiddleCenter);
            Stretch(countText.rectTransform, 0f, 0f, 0f, 0f);
        }

        MainMenuCardTile tile = tileObject.GetComponent<MainMenuCardTile>();
        tile.Configure(this, card, zone);
        CreateQuantityControls(tile, size, countBadge);
        if (zone != MainMenuCardTileZone.Collection) ApplyLegality(tile);
        if (ReferenceEquals(quantityCard, card) && quantityZone == zone)
        {
            quantityTile = tile;
            tile.SetQuantityControls(true, CountCardCopies(card, zone == MainMenuCardTileZone.Sideboard),
                DeckRepository.GetAllowedCopiesToAdd(currentDeck, card) > 0);
            SetHighlightedTile(tile);
        }
    }

    private void CreateQuantityControls(MainMenuCardTile tile, Vector2 size, GameObject countBadge)
    {
        var controls = new GameObject("QuantityControls", typeof(RectTransform));
        controls.transform.SetParent(tile.transform, false);
        Place((RectTransform)controls.transform, new Vector2(0, size.y * .5f - 54f), new Vector2(size.x - 10f, 58f));
        Button minus = CreateQuantityButton(controls.transform, "Decrease", "−", -size.x * .30f);
        Button plus = CreateQuantityButton(controls.transform, "Increase", "+", size.x * .30f);
        Image countBackground = AddImage(controls.transform, "QuantityCountBackground", new Color(.015f, .045f, .065f, .83f));
        countBackground.sprite = IntegratedUiUtility.RoundedRect(54, 42, 9);
        Place(countBackground.rectTransform, Vector2.zero, new Vector2(52f, 42f));
        Text countText = AddText(countBackground.transform, "QuantityCount", "x0", 20, Color.white, TextAnchor.MiddleCenter);
        countText.fontStyle = FontStyle.Bold;
        Stretch(countText.rectTransform, 0f, 0f, 0f, 0f);
        tile.ConfigureQuantityControls(controls, countText, minus, plus, countBadge);
        minus.onClick.AddListener(() => ChangeCardQuantity(tile, -1));
        plus.onClick.AddListener(() => ChangeCardQuantity(tile, 1));
    }

    internal static Button CreateQuantityButton(Transform parent, string name, string symbol, float x)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, new Vector2(x, 0), new Vector2(52f, 52f));
        Image image = go.GetComponent<Image>();
        image.sprite = IntegratedUiUtility.RoundedRect(56, 56, 28);
        image.color = new Color(.015f, .055f, .075f, .9f);
        var outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(.7f, .85f, .95f, .75f);
        outline.effectDistance = new Vector2(1f, -1f);
        Text label = AddText(go.transform, "Symbol", symbol, 30, Color.white, TextAnchor.MiddleCenter);
        label.fontStyle = FontStyle.Bold;
        Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
        return go.GetComponent<Button>();
    }

    private void OnDeckSelected(int index)
    {
        if (suppressDeckDropdownEvent) return;
        if (index < 0 || index >= decks.Count) return;

        if (hasUnsavedDeckChanges)
        {
            int previousIndex = loadedDeckIndex;
            ShowUnsavedChangesDialog(
                () => LoadDeckAt(index),
                () => SetDeckDropdownValue(previousIndex));
            return;
        }

        LoadDeckAt(index);
    }

    private void OnDeckNameChanged(string value)
    {
        if (currentDeck == null) return;
        currentDeck.name = string.IsNullOrWhiteSpace(value) ? "New Deck" : value.Trim();
        MarkDeckDirty();
        RefreshDeckGrids();
        deckNameInput.gameObject.SetActive(false);
        deckDropdown.gameObject.SetActive(true);
        deckDropdown.captionText.text = currentDeck.name;
    }

    private void BeginRenameDeck()
    {
        deckDropdown.gameObject.SetActive(false);
        deckNameInput.gameObject.SetActive(true);
        deckNameInput.SetTextWithoutNotify(currentDeck?.name ?? "New Deck");
        deckNameInput.Select();
        deckNameInput.ActivateInputField();
    }

    private void OnNewDeck()
    {
        if (hasUnsavedDeckChanges)
        {
            ShowUnsavedChangesDialog(CreateNewDeck);
            return;
        }

        CreateNewDeck();
    }

    private void CreateNewDeck()
    {
        currentDeck = new DeckDefinition { name = "New Deck" };
        ApplyDeckLegalitySelection();
        loadedDeckIndex = -1;
        UpdateDeleteDeckButton();
        MarkDeckDirty();
        SetDeckDropdownValue(-1);
        ClearHighlightedTile();
        RefreshDeckGrids();
        RefreshSelectedCardInfo();
        SetStatus("Created an empty deck.");
        BeginRenameDeck();
    }

    private void OnSaveDeck()
    {
        if (SaveCurrentDeck(out string path))
        {
            SetStatus($"Saved deck: {path}");
        }
    }

    private void OnReloadClicked()
    {
        if (hasUnsavedDeckChanges)
        {
            ShowUnsavedChangesDialog(ReloadData);
            return;
        }

        ReloadData();
    }

    private void OnAddSelectedToMain()
    {
        AddSelectedToDeck(false);
    }

    private void OnAddSelectedToSideboard()
    {
        AddSelectedToDeck(true);
    }

    private void AddSelectedToDeck(bool sideboard)
    {
        if (selectedCard == null || currentDeck == null) return;
        if (DeckAuxiliary.IsAuxiliary(selectedCard)) { AddSelectedAuxiliary(); return; }
        int requested = GetCountInput();
        int added = DeckRepository.AddCard(currentDeck, selectedCard, requested, sideboard);
        if (added > 0) MarkDeckDirty();
        RefreshDeckGrids();
        SetStatus(BuildAddCardStatus(selectedCard, added, requested, sideboard));
    }

    private static string BuildAddCardStatus(CardDefinition card, int added, int requested, bool sideboard)
    {
        string destination = sideboard ? "sideboard" : "main deck";
        if (card == null) return string.Empty;
        if (added <= 0) return $"{card.cardName} is already at the copy limit.";
        if (added < requested) return $"{card.cardName} +{added} to {destination}. Copy limit reached.";
        return $"{card.cardName} +{added} to {destination}.";
    }

    public void ChangeCardQuantity(MainMenuCardTile tile, int delta)
    {
        if (tile == null || tile.Card == null || currentDeck == null || delta == 0) return;
        if (tile.Zone == MainMenuCardTileZone.Collection && DeckAuxiliary.IsAuxiliary(tile.Card))
        {
            selectedCard = tile.Card;
            if (delta > 0) AddSelectedAuxiliary();
            else { if (DeckAuxiliary.Remove(currentDeck, tile.Card)) MarkDeckDirty(); ShowAuxiliaryDeck(DeckAuxiliary.IsAttraction(tile.Card)); }
            return;
        }
        bool sideboard = tile.Zone == MainMenuCardTileZone.Sideboard;
        int before = CountCardCopies(tile.Card, sideboard);
        if (delta > 0) DeckRepository.AddCard(currentDeck, tile.Card, 1, sideboard);
        else DeckRepository.RemoveCard(currentDeck, tile.Card, 1, sideboard);
        int after = CountCardCopies(tile.Card, sideboard);
        if (after == before) return;
        MarkDeckDirty();
        selectedCard = tile.Card;
        SetStatus($"{tile.Card.cardName} {(delta > 0 ? "+1" : "-1")} {(sideboard ? "sideboard" : "main deck")}.");
        MainMenuCardTileZone deckZone = sideboard ? MainMenuCardTileZone.Sideboard : MainMenuCardTileZone.MainDeck;
        RectTransform deckContent = sideboard ? sideboardContent : mainDeckContent;
        if (after == 0 && tile.Zone != MainMenuCardTileZone.Collection)
        {
            quantityCard = null;
            quantityTile = null;
        }
        if (before == 0 && after > 0)
        {
            CreateCardTile(deckContent, tile.Card, after, deckZone);
            Transform added = deckContent.GetChild(deckContent.childCount - 1);
            List<DeckEntry> entries = sideboard ? currentDeck.sideboard : currentDeck.cards;
            int index = entries.FindIndex(entry => EntryMatchesCard(entry, tile.Card));
            if (index >= 0) added.SetSiblingIndex(index);
        }
        else if (after == 0)
        {
            MainMenuCardTile removed = FindDeckTile(deckContent, tile.Card);
            if (removed != null)
            {
                if (highlightedTile == removed) highlightedTile = null;
                removed.gameObject.SetActive(false);
                Destroy(removed.gameObject);
            }
        }
        RefreshVisibleCardQuantities(tile.Card);
        SetText(mainDeckCountText, $"主牌  {DeckRepository.GetTotalCount(currentDeck, false)}");
        SetText(sideboardCountText, $"备牌  {DeckRepository.GetTotalCount(currentDeck, true)}");
        RefreshDeckLegality();
    }

    private static MainMenuCardTile FindDeckTile(RectTransform parent, CardDefinition card)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            MainMenuCardTile tile = parent.GetChild(i).GetComponent<MainMenuCardTile>();
            if (tile != null && tile.gameObject.activeSelf && ReferenceEquals(tile.Card, card)) return tile;
        }
        return null;
    }

    private void RefreshVisibleCardQuantities(CardDefinition card)
    {
        bool canIncrease = DeckRepository.GetAllowedCopiesToAdd(currentDeck, card) > 0;
        foreach (RectTransform parent in new[] { mainDeckContent, sideboardContent, collectionContent })
        {
            if (parent == null) continue;
            for (int i = 0; i < parent.childCount; i++)
            {
                MainMenuCardTile tile = parent.GetChild(i).GetComponent<MainMenuCardTile>();
                if (tile == null || !tile.gameObject.activeSelf || !ReferenceEquals(tile.Card, card)) continue;
                int count = CountCardCopies(card, tile.Zone == MainMenuCardTileZone.Sideboard);
                tile.SetCount(count);
                tile.SetQuantityControls(tile.QuantityControlsVisible, count, canIncrease);
            }
        }
    }

    public void OnCardTileSelected(MainMenuCardTile tile)
    {
        if (tile == null || tile.Card == null) return;

        selectedCard = tile.Card;
        SetHighlightedTile(tile);
        RefreshSelectedCardInfo();
        bool closing = quantityTile == tile && tile.QuantityControlsVisible;
        if (quantityTile != null)
            quantityTile.SetQuantityControls(false, 0, false);
        quantityTile = closing ? null : tile;
        quantityCard = closing ? null : tile.Card;
        if (!closing)
        {
            quantityZone = tile.Zone;
            tile.SetQuantityControls(true, CountCardCopies(tile.Card, tile.Zone == MainMenuCardTileZone.Sideboard),
                DeckRepository.GetAllowedCopiesToAdd(currentDeck, tile.Card) > 0);
        }
        SetStatusTextOnly($"Selected {tile.Card.cardName}.");
    }

    public void BeginCardDrag(MainMenuCardTile tile, PointerEventData eventData)
    {
        if (tile == null || tile.Card == null || currentDeck == null) return;

        draggedTile = tile;
        selectedCard = tile.Card;
        RefreshSelectedCardInfo();
        EndCardHover(tile);
        HidePreview();

        draggedTileGroup = tile.GetComponent<CanvasGroup>();
        if (draggedTileGroup == null) draggedTileGroup = tile.gameObject.AddComponent<CanvasGroup>();
        draggedTileGroup.alpha = 0.44f;

        CreateDragGhost(tile, eventData);
        SetDropZoneHighlights(MainMenuDropZone.None);
        SetStatusTextOnly(BuildDragHint(tile, MainMenuDropZone.None));
    }

    public void UpdateCardDrag(PointerEventData eventData)
    {
        if (draggedTile == null) return;

        MoveDragGhost(eventData);
        MainMenuDropZone dropZone = GetDropZone(eventData);
        if (dropZone == highlightedDropZone) return;

        SetDropZoneHighlights(dropZone);
        SetStatusTextOnly(BuildDragHint(draggedTile, dropZone));
    }

    public void EndCardDrag(MainMenuCardTile tile, PointerEventData eventData)
    {
        if (draggedTile == null) return;

        MainMenuCardTile sourceTile = draggedTile;
        MainMenuDropZone dropZone = GetDropZone(eventData);
        draggedTile = null;
        ResetDragSourceVisuals();
        DestroyDragGhost();
        SetDropZoneHighlights(MainMenuDropZone.None);

        if ((sourceTile.Zone == MainMenuCardTileZone.MainDeck && dropZone == MainMenuDropZone.MainDeck)
            || (sourceTile.Zone == MainMenuCardTileZone.Sideboard && dropZone == MainMenuDropZone.Sideboard))
            ReorderDraggedCard(sourceTile, eventData);
        else ApplyDraggedCard(sourceTile.Card, sourceTile.Zone, dropZone);
    }

    private void ReorderDraggedCard(MainMenuCardTile source, PointerEventData pointer)
    {
        var entries = source.Zone == MainMenuCardTileZone.MainDeck ? currentDeck.cards : currentDeck.sideboard;
        var content = source.Zone == MainMenuCardTileZone.MainDeck ? mainDeckContent : sideboardContent;
        int from = entries.FindIndex(e => DeckRepository.ResolveCard(e, cards) == source.Card);
        if (from < 0) return;
        MainMenuCardTile nearest = null;
        Vector2 nearestPoint = Vector2.zero;
        float distance = float.MaxValue;
        foreach (var tile in content.GetComponentsInChildren<MainMenuCardTile>())
        {
            var rect = (RectTransform)tile.transform;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(pointer.pressEventCamera, rect.TransformPoint(rect.rect.center));
            float d = (center - pointer.position).sqrMagnitude;
            if (d < distance) { distance = d; nearest = tile; nearestPoint = center; }
        }
        if (nearest == null || nearest == source) return;
        int insertion = entries.FindIndex(e => DeckRepository.ResolveCard(e, cards) == nearest.Card);
        if (insertion < 0) return;
        if (pointer.position.x >= nearestPoint.x) insertion++;
        if (from < insertion) insertion--;
        if (from == insertion) return;
        var entry = entries[from]; entries.RemoveAt(from); entries.Insert(insertion, entry);
        // Reuse the existing tiles: only their order changes, avoiding image reload/flicker.
        source.transform.SetSiblingIndex(insertion);
        LayoutRebuilder.MarkLayoutForRebuild(content);
        MarkDeckDirty();
        SetStatusTextOnly("已调整卡牌顺序，保存后保留。");
    }

    private void ApplyDraggedCard(CardDefinition card, MainMenuCardTileZone sourceZone, MainMenuDropZone targetZone)
    {
        if (card == null || currentDeck == null) return;
        if (sourceZone == MainMenuCardTileZone.Collection && DeckAuxiliary.IsAuxiliary(card)
            && (targetZone == MainMenuDropZone.MainDeck || targetZone == MainMenuDropZone.Sideboard))
        { selectedCard = card; AddSelectedAuxiliary(); return; }

        int count = GetDragCopyCount(card, sourceZone);
        if (count <= 0)
        {
            SetStatus("Drag canceled.");
            return;
        }

        selectedCard = card;
        bool changed = false;
        string message = "Drag canceled.";

        switch (sourceZone)
        {
            case MainMenuCardTileZone.Collection:
                if (targetZone == MainMenuDropZone.MainDeck)
                {
                    int added = DeckRepository.AddCard(currentDeck, card, count, false);
                    changed = added > 0;
                    message = BuildAddCardStatus(card, added, count, false);
                }
                else if (targetZone == MainMenuDropZone.Sideboard)
                {
                    int added = DeckRepository.AddCard(currentDeck, card, count, true);
                    changed = added > 0;
                    message = BuildAddCardStatus(card, added, count, true);
                }
                break;
            case MainMenuCardTileZone.MainDeck:
                if (targetZone == MainMenuDropZone.Sideboard)
                {
                    DeckRepository.RemoveCard(currentDeck, card, count, false);
                    DeckRepository.AddCard(currentDeck, card, count, true);
                    changed = true;
                    message = $"Moved {count} {card.cardName} to sideboard.";
                }
                else if (targetZone == MainMenuDropZone.Collection)
                {
                    DeckRepository.RemoveCard(currentDeck, card, count, false);
                    changed = true;
                    message = $"{card.cardName} -{count} from main deck.";
                }
                break;
            case MainMenuCardTileZone.Sideboard:
                if (targetZone == MainMenuDropZone.MainDeck)
                {
                    DeckRepository.RemoveCard(currentDeck, card, count, true);
                    DeckRepository.AddCard(currentDeck, card, count, false);
                    changed = true;
                    message = $"Moved {count} {card.cardName} to main deck.";
                }
                else if (targetZone == MainMenuDropZone.Collection)
                {
                    DeckRepository.RemoveCard(currentDeck, card, count, true);
                    changed = true;
                    message = $"{card.cardName} -{count} from sideboard.";
                }
                break;
        }

        if (changed)
        {
            MarkDeckDirty();
            RefreshDeckGrids();
            RefreshSelectedCardInfo();
        }
        else RefreshSelectedCardInfo();

        SetStatus(message);
    }

    private int GetDragCopyCount(CardDefinition card, MainMenuCardTileZone sourceZone)
    {
        if (card == null) return 0;
        int requested = GetCountInput();
        if (sourceZone == MainMenuCardTileZone.Collection)
        {
            int allowed = DeckRepository.GetAllowedCopiesToAdd(currentDeck, card);
            return allowed == int.MaxValue ? requested : Mathf.Min(requested, allowed);
        }

        int available = CountCardCopies(card, sourceZone == MainMenuCardTileZone.Sideboard);
        if (available <= 0) return 0;
        return Mathf.Min(requested, available);
    }

    private int CountCardCopies(CardDefinition card, bool sideboard)
    {
        if (card == null || currentDeck == null) return 0;
        List<DeckEntry> entries = sideboard ? currentDeck.sideboard : currentDeck.cards;
        if (entries == null) return 0;

        for (int i = 0; i < entries.Count; i++)
        {
            DeckEntry entry = entries[i];
            if (EntryMatchesCard(entry, card)) return Mathf.Max(0, entry.count);
        }

        return 0;
    }

    private static bool EntryMatchesCard(DeckEntry entry, CardDefinition card)
    {
        return DeckRepository.EntryMatchesCard(entry, card);
    }

    private void CreateDragGhost(MainMenuCardTile tile, PointerEventData eventData)
    {
        DestroyDragGhost();

        Canvas canvas = GetRootCanvas();
        if (canvas == null) return;

        RectTransform sourceRect = tile.GetComponent<RectTransform>();
        Vector2 size = sourceRect != null ? sourceRect.rect.size * 1.08f : new Vector2(126f, 176f);

        dragGhost = new GameObject("CardDragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
        dragGhost.transform.SetParent(canvas.transform, false);
        dragGhost.transform.SetAsLastSibling();

        dragGhostRect = dragGhost.GetComponent<RectTransform>();
        dragGhostRect.anchorMin = dragGhostRect.anchorMax = new Vector2(0.5f, 0.5f);
        dragGhostRect.pivot = new Vector2(0.5f, 0.5f);
        dragGhostRect.sizeDelta = size;

        CanvasGroup group = dragGhost.GetComponent<CanvasGroup>();
        group.alpha = 0.92f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image image = dragGhost.GetComponent<Image>();
        image.sprite = LoadCardSprite(tile.Card);
        image.color = image.sprite == null ? GetFallbackColor(tile.Card) : Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        Outline outline = dragGhost.GetComponent<Outline>();
        outline.effectColor = ObsidianArenaTheme.Blue;
        outline.effectDistance = new Vector2(4f, -4f);

        Text label = AddText(dragGhost.transform, "DragLabel", tile.Card.cardName, 13, Color.white, TextAnchor.UpperCenter);
        Stretch(label.rectTransform, 8f, 8f, 8f, size.y * 0.70f);

        MoveDragGhost(eventData);
    }

    private void MoveDragGhost(PointerEventData eventData)
    {
        if (dragGhostRect == null) return;
        Canvas canvas = GetRootCanvas();
        RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        if (canvasRect == null)
        {
            dragGhostRect.position = eventData.position;
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPosition);
        dragGhostRect.anchoredPosition = localPosition;
    }

    private void ResetDragSourceVisuals()
    {
        if (draggedTileGroup != null)
        {
            draggedTileGroup.alpha = 1f;
        }

        draggedTileGroup = null;
    }

    private void DestroyDragGhost()
    {
        if (dragGhost != null)
        {
            if (Application.isPlaying) Destroy(dragGhost);
            else DestroyImmediate(dragGhost);
        }

        dragGhost = null;
        dragGhostRect = null;
        SetDropZoneHighlights(MainMenuDropZone.None);
    }

    private MainMenuDropZone GetDropZone(PointerEventData eventData)
    {
        if (IsPointerOverContentShell(mainDeckContent, eventData)) return MainMenuDropZone.MainDeck;
        if (IsPointerOverContentShell(sideboardContent, eventData)) return MainMenuDropZone.Sideboard;
        if (IsPointerOverContentShell(collectionContent, eventData)) return MainMenuDropZone.Collection;
        return MainMenuDropZone.None;
    }

    private static bool IsPointerOverContentShell(RectTransform content, PointerEventData eventData)
    {
        RectTransform target = GetScrollShell(content);
        return target != null && RectTransformUtility.RectangleContainsScreenPoint(target, eventData.position, eventData.pressEventCamera);
    }

    private void SetDropZoneHighlights(MainMenuDropZone dropZone)
    {
        highlightedDropZone = dropZone;
        SetContentDropHighlight(collectionContent, dropZone == MainMenuDropZone.Collection, MenuVisualTheme.Muted);
        SetContentDropHighlight(mainDeckContent, dropZone == MainMenuDropZone.MainDeck, ObsidianArenaTheme.Blue);
        SetContentDropHighlight(sideboardContent, dropZone == MainMenuDropZone.Sideboard, ObsidianArenaTheme.Red);
    }

    private static void SetContentDropHighlight(RectTransform content, bool active, Color color)
    {
        RectTransform shell = GetScrollShell(content);
        if (shell == null) return;

        Outline outline = shell.GetComponent<Outline>();
        if (outline == null) outline = shell.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(4f, -4f);
        outline.enabled = active;
    }

    private static RectTransform GetScrollShell(RectTransform content)
    {
        if (content == null || content.parent == null || content.parent.parent == null) return null;
        return content.parent.parent as RectTransform;
    }

    private Canvas GetRootCanvas()
    {
        if (collectionContent != null) return collectionContent.GetComponentInParent<Canvas>();
        if (mainDeckContent != null) return mainDeckContent.GetComponentInParent<Canvas>();
        return sideboardContent != null ? sideboardContent.GetComponentInParent<Canvas>() : null;
    }

    private string BuildDragHint(MainMenuCardTile tile, MainMenuDropZone dropZone)
    {
        if (tile == null || tile.Card == null) return string.Empty;

        int count = GetDragCopyCount(tile.Card, tile.Zone);
        string cardName = tile.Card.cardName;
        switch (tile.Zone)
        {
            case MainMenuCardTileZone.Collection:
                if (count <= 0) return $"{cardName} is already at the copy limit.";
                if (dropZone == MainMenuDropZone.MainDeck) return $"Release to add {count} {cardName} to main deck.";
                if (dropZone == MainMenuDropZone.Sideboard) return $"Release to add {count} {cardName} to sideboard.";
                return $"Drag {cardName} to Main Deck or Sideboard.";
            case MainMenuCardTileZone.MainDeck:
                if (dropZone == MainMenuDropZone.MainDeck) return "拖到卡牌左侧或右侧，调整整叠卡牌的顺序。";
                if (dropZone == MainMenuDropZone.Sideboard) return $"Release to move {count} {cardName} to sideboard.";
                if (dropZone == MainMenuDropZone.Collection) return $"Release to remove {count} {cardName} from main deck.";
                return $"Drag {cardName} to Sideboard or Collection.";
            case MainMenuCardTileZone.Sideboard:
                if (dropZone == MainMenuDropZone.Sideboard) return "拖到卡牌左侧或右侧，调整整叠卡牌的顺序。";
                if (dropZone == MainMenuDropZone.MainDeck) return $"Release to move {count} {cardName} to main deck.";
                if (dropZone == MainMenuDropZone.Collection) return $"Release to remove {count} {cardName} from sideboard.";
                return $"Drag {cardName} to Main Deck or Collection.";
            default:
                return string.Empty;
        }
    }

    private void SetHighlightedTile(MainMenuCardTile tile)
    {
        if (highlightedTile == tile) return;
        if (highlightedTile != null) highlightedTile.SetHighlighted(false);
        highlightedTile = tile;
        if (highlightedTile != null) highlightedTile.SetHighlighted(true);
    }

    private void ClearHighlightedTile()
    {
        if (highlightedTile != null) highlightedTile.SetHighlighted(false);
        highlightedTile = null;
    }

    private void ClearHighlightedTileInZone(MainMenuCardTileZone zone)
    {
        if (highlightedTile != null && highlightedTile.Zone == zone)
        {
            ClearHighlightedTile();
        }
    }

    private void MarkDeckDirty()
    {
        hasUnsavedDeckChanges = true;
    }

    private bool SaveCurrentDeck(out string path)
    {
        path = string.Empty;
        if (currentDeck == null) return false;

        currentDeck.name = deckNameInput == null || string.IsNullOrWhiteSpace(deckNameInput.text)
            ? "New Deck"
            : deckNameInput.text.Trim();

        try
        {
            path = loadedDeckIndex >= 0 && loadedDeckIndex < decks.Count
                ? DeckRepository.SaveReplacing(decks[loadedDeckIndex].name, currentDeck)
                : DeckRepository.SaveNew(currentDeck);
            lastDeckSaveError = null;
        }
        catch (Exception exception)
        {
            lastDeckSaveError = exception.Message;
            SetStatus("保存失败：" + lastDeckSaveError);
            return false;
        }
        deckNameInput.SetTextWithoutNotify(currentDeck.name);
        decks.Clear();
        decks.AddRange(DeckRepository.LoadAll());
        loadedDeckIndex = FindDeckIndexByName(currentDeck.name);
        UpdateDeleteDeckButton();
        hasUnsavedDeckChanges = false;
        RefreshDeckDropdown(currentDeck.name);
        return true;
    }

    private void LoadDeckAt(int index)
    {
        if (index < 0 || index >= decks.Count) return;
        loadedDeckIndex = index;
        UpdateDeleteDeckButton();
        currentDeck = CloneDeck(decks[index]);
        ApplyDeckLegalitySelection();
        hasUnsavedDeckChanges = false;
        ClearHighlightedTile();
        SetDeckDropdownValue(index);
        RefreshDeckGrids();
        RefreshSelectedCardInfo();
        SetStatus($"Loaded deck: {currentDeck.name}");
    }

    private void RevertCurrentDeckFromSaved()
    {
        if (loadedDeckIndex >= 0 && loadedDeckIndex < decks.Count)
        {
            currentDeck = CloneDeck(decks[loadedDeckIndex]);
            SetDeckDropdownValue(loadedDeckIndex);
        }
        else
        {
            loadedDeckIndex = decks.Count > 0 ? 0 : -1;
            currentDeck = loadedDeckIndex >= 0 ? CloneDeck(decks[loadedDeckIndex]) : new DeckDefinition { name = "New Deck" };
            SetDeckDropdownValue(loadedDeckIndex);
        }

        ApplyDeckLegalitySelection();

        hasUnsavedDeckChanges = false;
        UpdateDeleteDeckButton();
        ClearHighlightedTile();
        RefreshDeckGrids();
        RefreshSelectedCardInfo();
    }

    private int FindDeckIndexByName(string deckName)
    {
        if (string.IsNullOrWhiteSpace(deckName)) return -1;
        for (int i = 0; i < decks.Count; i++)
        {
            if (string.Equals(decks[i].name, deckName, StringComparison.OrdinalIgnoreCase)) return i;
        }

        return -1;
    }

    private void SetDeckDropdownValue(int index)
    {
        if (deckDropdown == null) return;
        suppressDeckDropdownEvent = true;
        int optionCount = deckDropdown.options != null ? deckDropdown.options.Count : 0;
        if (index >= 0 && optionCount > 0) deckDropdown.value = Mathf.Clamp(index, 0, optionCount - 1);
        deckDropdown.RefreshShownValue();
        suppressDeckDropdownEvent = false;
    }

    private bool IsDeckBuilderVisible()
    {
        return deckBuilderGroup != null && deckBuilderGroup.gameObject.activeSelf && deckBuilderGroup.alpha > 0.5f;
    }

    private void ShowUnsavedChangesDialog(Action proceed, Action cancel = null)
    {
        if (!hasUnsavedDeckChanges)
        {
            proceed?.Invoke();
            return;
        }

        pendingUnsavedProceed = proceed;
        pendingUnsavedCancel = cancel;
        EnsureUnsavedChangesDialog();
        if (unsavedChangesMessageText != null)
        {
            string deckName = currentDeck != null && !string.IsNullOrWhiteSpace(currentDeck.name) ? currentDeck.name : "current deck";
            unsavedChangesMessageText.text = $"Deck \"{deckName}\" has unsaved changes.";
        }

        SetUnsavedChangesDialogVisible(true);
    }

    private void ConfirmSaveAndContinue()
    {
        if (!SaveCurrentDeck(out _))
        {
            if (unsavedChangesMessageText != null) unsavedChangesMessageText.text = "保存失败：" + lastDeckSaveError;
            return;
        }
        Action proceed = pendingUnsavedProceed;
        ClearPendingUnsavedAction();
        SetUnsavedChangesDialogVisible(false);
        proceed?.Invoke();
    }

    private void ConfirmDiscardAndContinue()
    {
        RevertCurrentDeckFromSaved();
        Action proceed = pendingUnsavedProceed;
        ClearPendingUnsavedAction();
        SetUnsavedChangesDialogVisible(false);
        proceed?.Invoke();
    }

    private void CancelUnsavedDialog()
    {
        Action cancel = pendingUnsavedCancel;
        ClearPendingUnsavedAction();
        SetUnsavedChangesDialogVisible(false);
        cancel?.Invoke();
    }

    private void ClearPendingUnsavedAction()
    {
        pendingUnsavedProceed = null;
        pendingUnsavedCancel = null;
    }

    private void SetUnsavedChangesDialogVisible(bool visible)
    {
        if (unsavedChangesDialogGroup == null) return;
        unsavedChangesDialogGroup.alpha = visible ? 1f : 0f;
        unsavedChangesDialogGroup.interactable = visible;
        unsavedChangesDialogGroup.blocksRaycasts = visible;
        unsavedChangesDialogGroup.gameObject.SetActive(visible);
        if (visible) unsavedChangesDialogGroup.transform.SetAsLastSibling();
    }

    private void EnsureUnsavedChangesDialog()
    {
        if (unsavedChangesDialogGroup != null) return;

        Canvas canvas = GetRootCanvas();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject overlay = new GameObject("UnsavedChangesDialog", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        overlay.transform.SetParent(canvas.transform, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        Stretch(overlayRect, 0f, 0f, 0f, 0f);

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.55f);
        overlayImage.raycastTarget = true;

        unsavedChangesDialogGroup = overlay.GetComponent<CanvasGroup>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(overlay.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        Place(panelRect, Vector2.zero, new Vector2(560f, 260f));

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = MenuVisualTheme.Raised;
        ObsidianArenaTheme.EnsureOutline(panelImage, ObsidianArenaTheme.Line, new Vector2(1f, -1f));
        panelImage.raycastTarget = true;

        Text title = AddText(panel.transform, "Title", "牌组尚未保存", 26, MenuVisualTheme.Paper, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        Place(title.rectTransform, new Vector2(0f, 82f), new Vector2(480f, 42f));

        unsavedChangesMessageText = AddText(panel.transform, "Message", string.Empty, 18, MenuVisualTheme.Paper, TextAnchor.UpperLeft);
        Place(unsavedChangesMessageText.rectTransform, new Vector2(0f, 24f), new Vector2(480f, 54f));

        CreateDialogButton(panel.transform, "SaveButton", "保存", new Vector2(-176f, -78f), MenuVisualTheme.Primary, ConfirmSaveAndContinue);
        CreateDialogButton(panel.transform, "DiscardButton", "放弃", new Vector2(0f, -78f), ObsidianArenaTheme.RedDeep, ConfirmDiscardAndContinue);
        CreateDialogButton(panel.transform, "CancelButton", "取消", new Vector2(176f, -78f), MenuVisualTheme.Panel, CancelUnsavedDialog);

        SetUnsavedChangesDialogVisible(false);
    }

    private static Button CreateDialogButton(Transform parent, string name, string label, Vector2 position, Color color, Action callback)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        Place(rect, position, new Vector2(148f, 48f));

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ObsidianArenaTheme.StyleButton(button, color);
        button.onClick.AddListener(() => callback?.Invoke());

        Text text = AddText(go.transform, "Text", label, 20, Color.white, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        return button;
    }

    private void SetStatusTextOnly(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    public void BeginCardHover(MainMenuCardTile tile)
    {
        if (cardArtPanel != null && cardArtPanel.gameObject.activeInHierarchy) return;
        if (draggedTile != null || tile == null || tile.Card == null) return;
        if(detailsCollapsed)floatingDeckDetails=false;
        hoveredTile = tile;
        selectedCard = tile.Card;
        SetHighlightedTile(tile);
        hoverStartedAt = Time.unscaledTime;
        previewVisible = false;
        RefreshSelectedCardInfo();
    }

    public void EndCardHover(MainMenuCardTile tile)
    {
        if (hoveredTile != tile) return;
        hoveredTile = null;
        previewVisible = false;
        HidePreview();
    }

    private void ShowPreview(CardDefinition card)
    {
        if(detailsCollapsed)OpenFloatingDeckDetails();
        RenderCardDetail(card);
        previewVisible = true;
    }

    private void HidePreview()
    {
        if(detailsCollapsed){floatingCloseAt=Time.unscaledTime+.12f;return;}
        RenderCardDetail(selectedCard);
    }

    private void OnStartMatch()
    {
        if (IsDeckBuilderVisible() && hasUnsavedDeckChanges)
        {
            ShowUnsavedChangesDialog(StartMatchCore);
            return;
        }

        StartMatchCore();
    }

    private void OnOnlineMatch()
    {
        if (AwaitCardData(OnOnlineMatch)) return;
        SetOnlineLobbyVisible(true);
        try
        {
            OnlineLobbyPanel.Show(this, currentDeck);
        }
        catch (Exception exception)
        {
            SetOnlineLobbyVisible(false);
            SetStatus($"无法打开联机大厅：{exception.Message}");
        }
    }

    private void StartMatchCore()
    {
        SetStatus("Forge 迁移版请从联机大厅开局，使用调试面板操作。正式战场交互尚未适配。");
    }

    private void RefreshDeckDropdown(string selectedName = null)
    {
        if (deckDropdown == null) return;

        suppressDeckDropdownEvent = true;
        deckDropdown.ClearOptions();
        var options = new List<string>();
        int selected = 0;
        for (int i = 0; i < decks.Count; i++)
        {
            string name = string.IsNullOrWhiteSpace(decks[i].name) ? $"Deck {i + 1}" : decks[i].name;
            options.Add(name);
            if (!string.IsNullOrEmpty(selectedName) && string.Equals(name, selectedName, StringComparison.OrdinalIgnoreCase))
            {
                selected = i;
            }
        }

        if (options.Count == 0) options.Add("No decks");
        deckDropdown.AddOptions(options);
        deckDropdown.value = Mathf.Clamp(selected, 0, options.Count - 1);
        deckDropdown.RefreshShownValue();
        suppressDeckDropdownEvent = false;
    }

    private void RefreshSelectedCardInfo()
    {
        RenderCardDetail(selectedCard);
    }

    private void RenderCardDetail(CardDefinition card)
    {
        if (cardDetailsView == null && cardInfoText != null)
            cardDetailsView = new DeckCardDetailsView(cardInfoText, OpenCardArt, OnDeckLegalitySelectionChanged, deckDropdown,
                ShowDetailsRelatedHover);
        if (cardPreviewGroup != null)
        {
            bool visible=!detailsCollapsed;
            cardPreviewGroup.alpha = visible?1f:0f;
            cardPreviewGroup.blocksRaycasts = visible;
            cardPreviewGroup.interactable = visible;
        }

        if (!ReferenceEquals(previewFaceCard, card))
        {
            previewFaceCard = card;
            RefreshAvailablePreviewFaces(card);
        }
        cardDetailsView?.Show(card);
        if(!ReferenceEquals(detailTokenArtCard,card))
        {
            if(detailsArtHover!=null)detailsArtHover.Hide();
            if(detailTokenLoad!=null){StopCoroutine(detailTokenLoad);detailTokenLoad=null;}
            detailTokenArtCard=card;detailTokenArts=null;
            if(card!=null)detailTokenLoad=StartCoroutine(DeckRelatedTokenArt.Load(card,related=>
            {
                if(!ReferenceEquals(detailTokenArtCard,card))return;
                detailTokenArts=related;detailTokenLoad=null;
            },currentDeck?.format));
        }
        if (card == null)
        {
            SetDetailText("No card selected.");
            RefreshPreviewImages(null);
            return;
        }

        RefreshPreviewImages(card);

        SetDetailText(BuildCardDetailText(card));
    }

    private void SetDetailText(string value)
    {
        if (cardInfoText != null) cardInfoText.text = value;
        if (cardPreviewText != null && cardPreviewText != cardInfoText) cardPreviewText.text = value;
    }

    private void OpenCardArt(CardDefinition card, Transform host)
    {
        selectedCard = card;
        hoveredTile = null;
        if (cardArtPanel == null) cardArtPanel = CardArtPanel.CreateEmbedded(host);
        cardArtPanel.Show(card, RefreshArtwork);
    }

    private void RefreshArtwork()
    {
        // Repaint existing tiles instead of rebuilding the grids (which causes flicker).
        foreach (var tile in deckBuilderGroup.GetComponentsInChildren<MainMenuCardTile>(true))
        {
            var face = tile.transform.Find("CardFace")?.GetComponent<UnityEngine.UI.Image>();
            if (face == null) continue;
            face.sprite = LoadCardSprite(tile.Card);
            if (face.sprite != null) face.color = Color.white;
            foreach (string name in new[] { "FallbackText", "FallbackOverlay" })
            {
                var fallback = tile.transform.Find(name);
                if (fallback != null && CardImageLibrary.TryLoadFullCard(tile.Card, out _)) fallback.gameObject.SetActive(false);
            }
        }
        RefreshSelectedCardInfo();
    }

    private static string BuildCardDetailText(CardDefinition card)
    {
        string cost = string.IsNullOrWhiteSpace(card.manaCost) ? "-" : card.manaCost;
        string description = string.IsNullOrWhiteSpace(card.description) ? "-" : card.description;
        string typeLine = string.IsNullOrWhiteSpace(card.GetTypeLine()) ? card.cardTypes.ToDisplayString() : card.GetTypeLine();
        string stats = string.Empty;
        if (card.cardTypes.Has(CardType.Creature)) stats += $"\nPower/Toughness  {card.attack}/{card.defence}";
        if (card.cardTypes.Has(CardType.Planeswalker)) stats += $"\nLoyalty  {card.loyalty}";
        if (card.cardTypes.Has(CardType.Battle)) stats += $"\nDefense  {card.battleDefense}";

        return
            $"{card.cardName}\n" +
            $"{card.PrintingKey}  ID {card.cardID}\n\n" +
            $"Type\n{typeLine}\n\n" +
            $"Mana Cost\n{cost}{stats}\n\n" +
            $"Text\n{description}";
    }

    private int GetCountInput()
    {
        if (countInput == null || !int.TryParse(countInput.text, out int value)) return 1;
        return Mathf.Clamp(value, 1, 99);
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log(message);
    }

    private static Sprite LoadCardSprite(CardDefinition card)
    {
        if (CardImageLibrary.TryLoadFullCard(card, out Sprite fullCard)) return fullCard;
        return CardVisualStyle.LoadFrame(card);
    }

    private static Color GetFallbackColor(CardDefinition card)
    {
        if (card == null) return new Color(0.20f, 0.22f, 0.24f, 1f);
        if (card.HasType(CardType.Land)) return new Color(0.18f, 0.36f, 0.42f, 1f);
        if (card.HasType(CardType.Creature)) return new Color(0.14f, 0.28f, 0.40f, 1f);
        if (card.cardTypes.IsInstantOrSorcery()) return new Color(0.50f, 0.20f, 0.12f, 1f);
        return new Color(0.25f, 0.24f, 0.25f, 1f);
    }

    private static string GetTileName(CardDefinition card, MainMenuCardTileZone zone)
    {
        string name = string.IsNullOrWhiteSpace(card.cardName) ? card.PrintingKey : card.cardName;
        return $"{zone}_{name}";
    }

    private static T FindSceneComponent<T>(string objectName) where T : Component
    {
        GameObject go = GameObject.Find(objectName);
        return go != null ? go.GetComponent<T>() : null;
    }

    private static void NormalizeScrollViewport(RectTransform content)
    {
        if (content == null || content.parent == null) return;

        Transform viewport = content.parent;
        Mask stencilMask = viewport.GetComponent<Mask>();
        if (stencilMask != null) stencilMask.enabled = false;

        if (viewport.GetComponent<RectMask2D>() == null)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
        }

        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage != null && viewportImage.color.a <= 0f)
        {
            viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        }
    }

    private static void ClearChildren(RectTransform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (Application.isPlaying)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            else DestroyImmediate(child.gameObject);
        }
    }

    private static Image AddImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text AddText(Transform parent, string name, string value, int fontSize, Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetText(Text text, string value)
    {
        if (text != null) text.text = value;
    }

    private static DeckDefinition CloneDeck(DeckDefinition source)
    {
        if (source == null) return null;
        var clone = new DeckDefinition
        {
            name = source.name,
            sourcePath = source.sourcePath,
            format = source.format,
            banlist = source.banlist,
            commanders = CloneEntries(source.commanders),
            cards = CloneEntries(source.cards),
            sideboard = CloneEntries(source.sideboard),
            attractions = CloneEntries(source.attractions),
            contraptions = CloneEntries(source.contraptions)
        };

        return clone;
    }

    private static List<DeckEntry> CloneEntries(List<DeckEntry> source)
    {
        var clone = new List<DeckEntry>();
        if (source == null) return clone;

        for (int i = 0; i < source.Count; i++)
        {
            DeckEntry entry = source[i];
            if (entry == null) continue;
            clone.Add(new DeckEntry
            {
                setCode = entry.setCode,
                collectorNumber = entry.collectorNumber,
                cardId = entry.cardId,
                count = entry.count
            });
        }

        return clone;
    }
}
