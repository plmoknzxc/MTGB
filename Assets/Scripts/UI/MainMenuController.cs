using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    private const string BattleSceneName = "MTGAPrototype";
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
    private CardDefinition selectedCard;
    private MainMenuCardTile hoveredTile;
    private MainMenuCardTile highlightedTile;
    private float hoverStartedAt;
    private bool previewVisible;
    private bool listenersRegistered;
    private bool hasUnsavedDeckChanges;
    private bool suppressDeckDropdownEvent;
    private string collectionQuery = string.Empty;
    private int collectionContentFilter;
    private GameObject collectionSearchDialog;
    private Text collectionFilterLabel;
    private int loadedDeckIndex = -1;
    private Action pendingUnsavedProceed;
    private Action pendingUnsavedCancel;
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
        RegisterListeners();
        ReloadData();
        ShowStartScreen();
        OnlineMatchService service = OnlineMatchService.Instance;
        if (service != null && service.IsConnected && !service.IsInMatch)
            OnlineLobbyPanel.Show(this, currentDeck);
    }

    private void Update()
    {
        if (hoveredTile == null || previewVisible) return;
        if (Time.unscaledTime - hoverStartedAt < HoverPreviewDelay) return;
        ShowPreview(hoveredTile.Card);
    }

    private void OnDestroy()
    {
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
            folderText.text = $"CardDatabases: {CardDatabaseRepository.ExternalRoot}\nCardImages: {CardImageLibrary.ExternalRoot}\nDecks: {DeckRepository.DeckRoot}";
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
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsPlaceholder);
        if (backToTitleButton != null) backToTitleButton.onClick.AddListener(ShowStartScreen);
        if (advancedSearchButton != null) advancedSearchButton.onClick.AddListener(OnAdvancedSearchPlaceholder);
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
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsPlaceholder);
        if (backToTitleButton != null) backToTitleButton.onClick.RemoveListener(ShowStartScreen);
        if (advancedSearchButton != null) advancedSearchButton.onClick.RemoveListener(OnAdvancedSearchPlaceholder);
        if (newDeckButton != null) newDeckButton.onClick.RemoveListener(OnNewDeck);
        if (saveDeckButton != null) saveDeckButton.onClick.RemoveListener(OnSaveDeck);
        if (reloadButton != null) reloadButton.onClick.RemoveListener(OnReloadClicked);
        if (addMainButton != null) addMainButton.onClick.RemoveListener(OnAddSelectedToMain);
        if (addSideboardButton != null) addSideboardButton.onClick.RemoveListener(OnAddSelectedToSideboard);
        if (startButton != null) startButton.onClick.RemoveListener(OnStartMatch);
    }

    private void ReloadData()
    {
        CardImageLibrary.ClearCache();

        cards.Clear();
        cards.AddRange(CardDatabaseRepository.LoadAll(true));

        decks.Clear();
        decks.AddRange(DeckRepository.LoadAll());
        bool savedCleanedDeck = false;
        for (int i = 0; i < decks.Count; i++)
        {
            if (DeckRepository.EnforceCopyLimits(decks[i], cards))
            {
                DeckRepository.Save(decks[i]);
                savedCleanedDeck = true;
            }
        }

        if (savedCleanedDeck)
        {
            decks.Clear();
            decks.AddRange(DeckRepository.LoadAll());
        }

        if (decks.Count == 0 && cards.Count > 0)
        {
            DeckDefinition starter = DeckRepository.CreateDefaultDeck(cards);
            DeckRepository.Save(starter);
            decks.AddRange(DeckRepository.LoadAll());
        }

        loadedDeckIndex = decks.Count > 0 ? 0 : -1;
        currentDeck = loadedDeckIndex >= 0 ? CloneDeck(decks[loadedDeckIndex]) : new DeckDefinition { name = "New Deck" };
        selectedCard = cards.Count > 0 ? cards[0] : null;
        hasUnsavedDeckChanges = false;
        ClearHighlightedTile();
        RefreshDeckDropdown();
        RefreshAllViews();
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
        SetScreenVisible(startScreenGroup, false);
        SetScreenVisible(deckBuilderGroup, true);
        RefreshAllViews();
    }

    public void SetOnlineLobbyVisible(bool visible)
    {
        SetScreenVisible(startScreenGroup, !visible);
        SetScreenVisible(deckBuilderGroup, false);
    }

    private void OnSettingsPlaceholder()
    {
        SetStatus("游戏设置还未接入。");
    }

    private void OnAdvancedSearchPlaceholder()
    {
        if (collectionSearchDialog != null) { collectionSearchDialog.SetActive(true); return; }
        Canvas canvas = GetRootCanvas();
        if (canvas == null) return;
        collectionSearchDialog = new GameObject("CollectionSearch", typeof(RectTransform), typeof(Image));
        collectionSearchDialog.transform.SetParent(canvas.transform, false);
        Stretch(collectionSearchDialog.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        collectionSearchDialog.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(collectionSearchDialog.transform, false);
        Place(panel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(560f, 320f));
        panel.GetComponent<Image>().color = ObsidianArenaTheme.SurfaceRaised;
        Text title = AddText(panel.transform, "Title", "搜索卡牌", 26, Color.white, TextAnchor.MiddleLeft);
        Place(title.rectTransform, new Vector2(0f, 122f), new Vector2(500f, 40f));
        var inputObject = new GameObject("SearchInput", typeof(RectTransform), typeof(Image), typeof(InputField));
        inputObject.transform.SetParent(panel.transform, false);
        Place(inputObject.GetComponent<RectTransform>(), new Vector2(0f, 68f), new Vector2(500f, 42f));
        inputObject.GetComponent<Image>().color = ObsidianArenaTheme.Surface;
        var input = inputObject.GetComponent<InputField>();
        input.textComponent = AddText(input.transform, "Text", string.Empty, 18, Color.white, TextAnchor.MiddleLeft);
        Stretch(input.textComponent.rectTransform, 12f, 4f, 12f, 4f);
        Text placeholder = AddText(input.transform, "Placeholder", "卡名、系列、编号或规则文字", 18, ObsidianArenaTheme.MutedText, TextAnchor.MiddleLeft);
        Stretch(placeholder.rectTransform, 12f, 4f, 12f, 4f);
        input.placeholder = placeholder;
        input.text = collectionQuery;
        input.onValueChanged.AddListener(value => { collectionQuery = value; RefreshCollectionGrid(); });
        string[] labels = { "全部内容", "官方卡牌", "DIY 卡牌" };
        for (int i = 0; i < labels.Length; i++)
        {
            int category = i;
            CreateDialogButton(panel.transform, "Category" + i, labels[i], new Vector2((i - 1) * 176f, 5f),
                ObsidianArenaTheme.Surface, () =>
                {
                    collectionContentFilter = category;
                    collectionFilterLabel.text = "当前筛选：" + labels[category];
                    RefreshCollectionGrid();
                });
        }
        collectionFilterLabel = AddText(panel.transform, "Filter", "当前筛选：" + labels[collectionContentFilter], 18, Color.white, TextAnchor.MiddleCenter);
        Place(collectionFilterLabel.rectTransform, new Vector2(0f, -48f), new Vector2(500f, 30f));
        CreateDialogButton(panel.transform, "Close", "查看结果", new Vector2(0f, -110f), ObsidianArenaTheme.BlueDeep,
            () => collectionSearchDialog.SetActive(false));
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
        ClearHighlightedTileInZone(MainMenuCardTileZone.Collection);
        ClearChildren(collectionContent);
        if (collectionContent == null) return;

        int visible = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (!MatchesCollectionFilter(cards[i], collectionQuery, collectionContentFilter)) continue;
            CreateCardTile(collectionContent, cards[i], 0, MainMenuCardTileZone.Collection);
            visible++;
        }

        SetText(collectionCountText, $"卡牌  {visible} / {cards.Count}");
    }

    public static bool MatchesCollectionFilter(CardDefinition card, string query, int category)
    {
        if (card == null || (category == 1 && card.customContent) || (category == 2 && !card.customContent)) return false;
        query = query?.Trim() ?? string.Empty;
        return query.Length == 0 || (card.cardName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || card.PrintingKey.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || (card.description ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void RefreshDeckGrids()
    {
        ClearHighlightedTileInZone(MainMenuCardTileZone.MainDeck);
        ClearHighlightedTileInZone(MainMenuCardTileZone.Sideboard);
        ClearChildren(mainDeckContent);
        ClearChildren(sideboardContent);

        int mainCount = DeckRepository.GetTotalCount(currentDeck, false);
        int sideboardCount = DeckRepository.GetTotalCount(currentDeck, true);
        SetText(mainDeckCountText, $"Main Deck  {mainCount}");
        SetText(sideboardCountText, $"Sideboard  {sideboardCount}");

        if (deckNameInput != null && currentDeck != null) deckNameInput.text = currentDeck.name;
        PopulateDeckGrid(mainDeckContent, currentDeck?.cards, MainMenuCardTileZone.MainDeck);
        PopulateDeckGrid(sideboardContent, currentDeck?.sideboard, MainMenuCardTileZone.Sideboard);
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
            ? ObsidianArenaTheme.SurfaceRaised
            : ObsidianArenaTheme.Surface;
        background.raycastTarget = true;

        Outline outline = tileObject.GetComponent<Outline>();
        outline.effectColor = ObsidianArenaTheme.LineSoft;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Image face = AddImage(tileObject.transform, "CardFace", Color.white);
        Stretch(face.rectTransform, 4f, 4f, 4f, 4f);
        face.sprite = LoadCardSprite(card);
        face.preserveAspect = false;

        if (face.sprite == null)
        {
            face.color = GetFallbackColor(card);
            Text fallback = AddText(tileObject.transform, "FallbackText", $"{card.cardName}\n{card.manaCost}", 13, Color.white, TextAnchor.MiddleCenter);
            Stretch(fallback.rectTransform, 8f, 8f, 8f, 8f);
        }
        else if (!CardImageLibrary.TryLoadFullCard(card, out _))
        {
            Text overlay = AddText(tileObject.transform, "FallbackOverlay", $"{card.cardName}\n{card.GetTypeLine()}", 11, new Color(0.04f, 0.04f, 0.04f, 0.95f), TextAnchor.UpperCenter);
            Stretch(overlay.rectTransform, 10f, 10f, 10f, size.y * 0.58f);
        }

        if (count > 0)
        {
            Image countBackground = AddImage(tileObject.transform, "CountBackground", new Color(0f, 0f, 0f, 0.76f));
            Place(countBackground.rectTransform, new Vector2(size.x * 0.34f, -size.y * 0.37f), new Vector2(size.x * 0.32f, 28f));

            Text countText = AddText(countBackground.transform, "CountText", $"x{count}", 18, Color.white, TextAnchor.MiddleCenter);
            Stretch(countText.rectTransform, 0f, 0f, 0f, 0f);
        }

        MainMenuCardTile tile = tileObject.GetComponent<MainMenuCardTile>();
        tile.Configure(this, card, zone);
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
        loadedDeckIndex = -1;
        MarkDeckDirty();
        SetDeckDropdownValue(-1);
        ClearHighlightedTile();
        RefreshDeckGrids();
        RefreshSelectedCardInfo();
        SetStatus("Created an empty deck.");
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

    public void OnCardTileClicked(MainMenuCardTile tile)
    {
        if (tile == null || tile.Card == null || currentDeck == null) return;

        selectedCard = tile.Card;
        RefreshSelectedCardInfo();

        switch (tile.Zone)
        {
            case MainMenuCardTileZone.Collection:
                int requested = GetCountInput();
                int added = DeckRepository.AddCard(currentDeck, tile.Card, requested, false);
                if (added > 0) MarkDeckDirty();
                SetStatus(BuildAddCardStatus(tile.Card, added, requested, false));
                break;
            case MainMenuCardTileZone.MainDeck:
                int mainBefore = CountCardCopies(tile.Card, false);
                DeckRepository.RemoveCard(currentDeck, tile.Card, GetCountInput(), false);
                if (CountCardCopies(tile.Card, false) < mainBefore) MarkDeckDirty();
                SetStatus($"{tile.Card.cardName} -{GetCountInput()} from main deck.");
                break;
            case MainMenuCardTileZone.Sideboard:
                int sideBefore = CountCardCopies(tile.Card, true);
                DeckRepository.RemoveCard(currentDeck, tile.Card, GetCountInput(), true);
                if (CountCardCopies(tile.Card, true) < sideBefore) MarkDeckDirty();
                SetStatus($"{tile.Card.cardName} -{GetCountInput()} from sideboard.");
                break;
        }

        RefreshAllViews();
    }

    public void OnCardTileSelected(MainMenuCardTile tile)
    {
        if (tile == null || tile.Card == null) return;

        selectedCard = tile.Card;
        SetHighlightedTile(tile);
        RefreshSelectedCardInfo();
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

        ApplyDraggedCard(sourceTile.Card, sourceTile.Zone, dropZone);
    }

    private void ApplyDraggedCard(CardDefinition card, MainMenuCardTileZone sourceZone, MainMenuDropZone targetZone)
    {
        if (card == null || currentDeck == null) return;

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
            RefreshAllViews();
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
        image.preserveAspect = false;
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
        SetContentDropHighlight(collectionContent, dropZone == MainMenuDropZone.Collection, ObsidianArenaTheme.MutedText);
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
                if (dropZone == MainMenuDropZone.Sideboard) return $"Release to move {count} {cardName} to sideboard.";
                if (dropZone == MainMenuDropZone.Collection) return $"Release to remove {count} {cardName} from main deck.";
                return $"Drag {cardName} to Sideboard or Collection.";
            case MainMenuCardTileZone.Sideboard:
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

        DeckRepository.EnforceCopyLimits(currentDeck, cards);
        path = DeckRepository.Save(currentDeck);
        decks.Clear();
        decks.AddRange(DeckRepository.LoadAll());
        loadedDeckIndex = FindDeckIndexByName(currentDeck.name);
        hasUnsavedDeckChanges = false;
        RefreshDeckDropdown(currentDeck.name);
        return true;
    }

    private void LoadDeckAt(int index)
    {
        if (index < 0 || index >= decks.Count) return;
        loadedDeckIndex = index;
        currentDeck = CloneDeck(decks[index]);
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

        hasUnsavedDeckChanges = false;
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
        SaveCurrentDeck(out _);
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
        panelImage.color = ObsidianArenaTheme.SurfaceRaised;
        ObsidianArenaTheme.EnsureOutline(panelImage, ObsidianArenaTheme.Line, new Vector2(1f, -1f));
        panelImage.raycastTarget = true;

        Text title = AddText(panel.transform, "Title", "牌组尚未保存", 26, ObsidianArenaTheme.Text, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        Place(title.rectTransform, new Vector2(0f, 82f), new Vector2(480f, 42f));

        unsavedChangesMessageText = AddText(panel.transform, "Message", string.Empty, 18, ObsidianArenaTheme.Text, TextAnchor.UpperLeft);
        Place(unsavedChangesMessageText.rectTransform, new Vector2(0f, 24f), new Vector2(480f, 54f));

        CreateDialogButton(panel.transform, "SaveButton", "保存", new Vector2(-176f, -78f), ObsidianArenaTheme.BlueDeep, ConfirmSaveAndContinue);
        CreateDialogButton(panel.transform, "DiscardButton", "放弃", new Vector2(0f, -78f), ObsidianArenaTheme.RedDeep, ConfirmDiscardAndContinue);
        CreateDialogButton(panel.transform, "CancelButton", "取消", new Vector2(176f, -78f), ObsidianArenaTheme.Surface, CancelUnsavedDialog);

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
        if (draggedTile != null || tile == null || tile.Card == null) return;
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
        RenderCardDetail(card);
        previewVisible = true;
    }

    private void HidePreview()
    {
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
        if (currentDeck == null)
        {
            SetStatus("No deck selected.");
            return;
        }

        List<CardDefinition> runtimeDeck = DeckRepository.BuildRuntimeDeck(currentDeck, cards, out string warning);
        if (runtimeDeck.Count == 0)
        {
            SetStatus(string.IsNullOrEmpty(warning) ? "Deck has no playable cards." : warning);
            return;
        }

        MatchStartConfig.SetDecks(currentDeck, currentDeck);
        MatchStartConfig.SetDeterministicSeed(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        try
        {
            SceneManager.LoadScene(BattleSceneName);
        }
        catch (Exception exception)
        {
            SetStatus($"Could not load scene '{BattleSceneName}': {exception.Message}");
        }
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
        if (cardPreviewGroup != null)
        {
            cardPreviewGroup.alpha = 1f;
            cardPreviewGroup.blocksRaycasts = true;
            cardPreviewGroup.interactable = true;
        }

        if (card == null)
        {
            SetDetailText("No card selected.");
            if (cardPreviewImage != null)
            {
                cardPreviewImage.sprite = null;
                cardPreviewImage.color = new Color(0.20f, 0.22f, 0.21f, 1f);
            }

            return;
        }

        Sprite sprite = LoadCardSprite(card);
        if (cardPreviewImage != null)
        {
            cardPreviewImage.sprite = sprite;
            cardPreviewImage.color = sprite == null ? GetFallbackColor(card) : Color.white;
            cardPreviewImage.preserveAspect = false;
        }

        SetDetailText(BuildCardDetailText(card));
    }

    private void SetDetailText(string value)
    {
        if (cardInfoText != null) cardInfoText.text = value;
        if (cardPreviewText != null && cardPreviewText != cardInfoText) cardPreviewText.text = value;
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
            commanders = CloneEntries(source.commanders),
            cards = CloneEntries(source.cards),
            sideboard = CloneEntries(source.sideboard)
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
