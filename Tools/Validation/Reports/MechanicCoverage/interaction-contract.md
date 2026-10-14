# Forge 交互接口核对

固定版本 `4ec5f1a2c32fa90ecb983a72b9eb47aa5c5d7676`：105 个接口声明（含重载），259 个直接调用位置。

**接入分类不等于运行验证。** 规则断言、Unity 点击与继续结算必须分别有证据。

完整接口源文件（包括默认方法体）复核哈希：与已复核版本一致。

| 方法 | 分类 | 共用界面／处理 |
|---|---|---|
| `default boolean isLibgdxPort()` | state | 已有精确处理或默认转发 |
| `default void setGameView(GameView gameView, long sequenceNumber)` | snapshot | 默认转发／快照展示回调 |
| `void setGameView(GameView gameView)` | snapshot | 默认转发／快照展示回调 |
| `GameView getGameView()` | state | 已有精确处理或默认转发 |
| `void setOriginalGameController(PlayerView view, IGameController gameController)` | snapshot | 默认转发／快照展示回调 |
| `void setGameController(PlayerView player, IGameController gameController)` | snapshot | 默认转发／快照展示回调 |
| `void setSpectator(IGameController spectator)` | snapshot | 默认转发／快照展示回调 |
| `void openView(TrackableCollection<PlayerView> myPlayers)` | snapshot | 默认转发／快照展示回调 |
| `void afterGameEnd()` | snapshot | 默认转发／快照展示回调 |
| `void showCombat()` | snapshot | 默认转发／快照展示回调 |
| `default void showPromptMessage(PlayerView playerView, String message)` | state | 已有精确处理或默认转发 |
| `void showPromptMessage(PlayerView playerView, String message, CardView card)` | state | 已有精确处理或默认转发 |
| `default void updateDrawOffer(DrawOfferMessage.Status update)` | snapshot | 默认转发／快照展示回调 |
| `default void updateButtons(final PlayerView owner, final boolean okEnabled, final boolean cancelEnabled, final boolean focusOk)` | state | 已有精确处理或默认转发 |
| `void updateButtons(PlayerView owner, String label1, String label2, boolean enable1, boolean enable2, boolean focus1)` | state | 已有精确处理或默认转发 |
| `void flashIncorrectAction()` | snapshot | 默认转发／快照展示回调 |
| `void alertUser()` | snapshot | 默认转发／快照展示回调 |
| `default void updatePhase(boolean saveState)` | snapshot | 默认转发／快照展示回调 |
| `default void updateTurn(PlayerView player)` | snapshot | 默认转发／快照展示回调 |
| `default void updatePlayerControl()` | snapshot | 默认转发／快照展示回调 |
| `default void enableOverlay()` | snapshot | 默认转发／快照展示回调 |
| `default void disableOverlay()` | snapshot | 默认转发／快照展示回调 |
| `void finishGame()` | snapshot | 默认转发／快照展示回调 |
| `default void showManaPool(PlayerView player)` | snapshot | 默认转发／快照展示回调 |
| `default void hideManaPool(PlayerView player)` | snapshot | 默认转发／快照展示回调 |
| `default void updateStack()` | snapshot | 默认转发／快照展示回调 |
| `default void notifyStackAddition(final GameEventSpellAbilityCast event)` | snapshot | 默认转发／快照展示回调 |
| `default void notifyStackRemoval(final GameEventSpellRemovedFromStack event)` | snapshot | 默认转发／快照展示回调 |
| `default void handleLandPlayed(CardView land)` | snapshot | 默认转发／快照展示回调 |
| `void handleGameEvent(GameEvent event)` | snapshot | 默认转发／快照展示回调 |
| `default void handleGameEvents(List<GameEvent> events)` | snapshot | 默认转发／快照展示回调 |
| `default void updateZones(Iterable<PlayerZoneUpdate> zonesToUpdate)` | snapshot | 默认转发／快照展示回调 |
| `default void updateCard(final CardView card)` | snapshot | 默认转发／快照展示回调 |
| `default void updateCards(Iterable<CardView> cards)` | snapshot | 默认转发／快照展示回调 |
| `default void updateCardsNetSafe(FCollectionView<CardView> cards)` | snapshot | 默认转发／快照展示回调 |
| `void updateRevealedCards(TrackableCollection<CardView> collection)` | state | 已有精确处理或默认转发 |
| `default void refreshCardDetails(Iterable<CardView> cards)` | snapshot | 默认转发／快照展示回调 |
| `default void refreshField()` | snapshot | 默认转发／快照展示回调 |
| `GameState getGamestate()` | state | 已有精确处理或默认转发 |
| `default void updateManaPool(Iterable<PlayerView> manaPoolUpdate)` | snapshot | 默认转发／快照展示回调 |
| `default void updateLives(Iterable<PlayerView> livesUpdate)` | snapshot | 默认转发／快照展示回调 |
| `default void updateShards(Iterable<PlayerView> shardsUpdate)` | snapshot | 默认转发／快照展示回调 |
| `default void updateDependencies()` | snapshot | 默认转发／快照展示回调 |
| `void setPanelSelection(CardView hostCard)` | snapshot | 默认转发／快照展示回调 |
| `SpellAbilityView getAbilityToPlay(CardView hostCard, List<SpellAbilityView> abilities, ITriggerEvent triggerEvent)` | decision | 能力／施放方式 |
| `Map<CardView, Integer> assignCombatDamage(CardView attacker, List<CardView> blockers, int damage, GameEntityView defender, boolean overrideOrder, boolean maySkip)` | decision | 战斗伤害分配 |
| `Map<Object, Integer> assignGenericAmount(CardView effectSource, Map<Object, Integer> target, int amount, final boolean atLeastOne, final String amountLabel)` | decision | 分配数量 |
| `default void message(final String message)` | state | 原生接收玩家专属的有界通知；固定骰子／硬币调用点单独允许公开投影 |
| `void message(String message, String title)` | state | 原生接收玩家专属的有界通知；固定骰子／硬币调用点单独允许公开投影 |
| `default void showErrorDialog(final String message)` | fatal | 错误终止且通知客户端 |
| `void showErrorDialog(String message, String title)` | fatal | 错误终止且通知客户端 |
| `default boolean showConfirmDialog(final String message, final String title)` | decision | 转发到已登记的 showConfirmDialog 重载 |
| `default boolean showConfirmDialog(final String message, final String title, final String yesButtonText, final String noButtonText)` | decision | 转发到已登记的 showConfirmDialog 重载 |
| `boolean showConfirmDialog(String message, String title, String yesButtonText, String noButtonText, boolean defaultYes)` | decision | 确认 |
| `int showOptionDialog(String message, String title, FSkinProp icon, List<String> options, int defaultOption)` | decision | 单选 |
| `String showInputDialog(String message, String title, FSkinProp icon, String initialInput, List<String> inputOptions, boolean isNumeric)` | unsupported | 仅宏编辑直接调用；自由输入未接入，不能误报成所有牌名选择未接入 |
| `default boolean confirm(final CardView c, final String question)` | decision | 转发到已登记的 confirm 重载 |
| `boolean confirm(CardView c, String question, boolean defaultIsYes, List<String> options)` | decision | 确认 |
| `default <T> List<T> getChoices(final String message, final int min, final int max, final List<T> choices)` | decision | 转发到已登记的 getChoices 重载 |
| `<T> List<T> getChoices(String message, int min, int max, List<T> choices, List<T> selected, FSerializableFunction<T, String> display)` | decision | 多选卡牌或通用选项 |
| `Integer getInteger(String message, int min, int max, boolean sortDesc)` | decision | 有界数字 |
| `Integer getInteger(String message, int min, int max, int cutoff)` | decision | 有界数字 |
| `<T> T oneOrNone(String message, List<T> choices)` | decision | 可选单选 |
| `default <T> T one(final String message, final List<T> choices)` | decision | 转发到已登记的 one 重载 |
| `<T> T one(String message, List<T> choices, FSerializableFunction<T, String> display)` | decision | 单选 |
| `<T> void reveal(String message, List<T> items)` | decision | 确认展示 |
| `default <T> List<T> many(final String title, final String topCaption, final int cnt, final List<T> sourceChoices, final CardView c)` | decision | 转发到已登记的 many 重载 |
| `default <T> List<T> many(final String title, final String topCaption, final int min, final int max, final List<T> sourceChoices, final CardView c)` | decision | 转发到已登记的 many 重载 |
| `<T> List<T> many(String title, String topCaption, int min, int max, List<T> sourceChoices, List<T> destChoices, CardView c)` | decision | 卡牌多选 |
| `default <T> List<T> order(String title, String top, List<T> sourceChoices, CardView c)` | decision | 转发到已登记的 order 重载 |
| `default <T> List<T> order(String title, String top, int remainingObjectsMin, int remainingObjectsMax, List<T> sourceChoices, List<T> destChoices, CardView referenceCard, boolean sideboardingMode)` | decision | 转发到已登记的 order 重载 |
| `<T> OrderResult<T> order(String title, String top, int remainingObjectsMin, int remainingObjectsMax, List<T> sourceChoices, List<T> destChoices, CardView referenceCard, boolean sideboardingMode, boolean showRememberCheckbox)` | decision | 顺序选择；局间备牌分支未接入 |
| `<T> List<T> insertInList(String title, T newItem, List<T> oldItems)` | decision | 保留原顺序并插入 |
| `List<PaperCard> sideboard(CardPool sideboard, CardPool main, String message)` | unsupported | 原生 Forge 局间换备牌未接入 |
| `GameEntityView chooseSingleEntityForEffect(String title, List<? extends GameEntityView> optionList, DelayedReveal delayedReveal, boolean isOptional)` | decision | 单选实体 |
| `List<GameEntityView> chooseEntitiesForEffect(String title, List<? extends GameEntityView> optionList, int min, int max, DelayedReveal delayedReveal)` | decision | 多选实体 |
| `List<CardView> manipulateCardList(String title, final Iterable<CardView> cards, final Iterable<CardView> manipulable, boolean toTop, boolean toBottom, boolean toAnywhere)` | decision | 区域及插入位置 |
| `void setCard(CardView card)` | snapshot | 默认转发／快照展示回调 |
| `void setPlayerAvatar(LobbyPlayer player, IHasIcon ihi)` | snapshot | 默认转发／快照展示回调 |
| `default void openZones(PlayerView controller, Collection<ZoneType> zones, Map<PlayerView, Object> players)` | state | 已有精确处理或默认转发 |
| `void setHighlighted(Iterable<GameEntityView> entities, boolean b)` | snapshot | 默认转发／快照展示回调 |
| `void setSelectables(Iterable<CardView> cards, int min, int max)` | state | 已有精确处理或默认转发 |
| `void clearSelectables()` | state | 已有精确处理或默认转发 |
| `boolean isSelecting()` | state | 已有精确处理或默认转发 |
| `void setWeaklySelectable(final Iterable<CardView> cards)` | state | 已有精确处理或默认转发 |
| `void clearWeaklySelectable()` | state | 已有精确处理或默认转发 |
| `default void showRevealedCards(Iterable<CardView> cards)` | snapshot | 默认转发／快照展示回调 |
| `default void hideRevealedCards()` | snapshot | 默认转发／快照展示回调 |
| `boolean isGamePaused()` | state | 已有精确处理或默认转发 |
| `void setGamePause(boolean pause)` | snapshot | 默认转发／快照展示回调 |
| `PlaybackSpeed getGameSpeed()` | state | 已有精确处理或默认转发 |
| `void setGameSpeed(PlaybackSpeed gameSpeed)` | snapshot | 默认转发／快照展示回调 |
| `String getDayTime()` | state | 已有精确处理或默认转发 |
| `void updateDayTime(String daytime)` | snapshot | 默认转发／快照展示回调 |
| `void awaitNextInput()` | snapshot | 默认转发／快照展示回调 |
| `void cancelAwaitNextInput()` | snapshot | 默认转发／快照展示回调 |
| `void showWaitingTimer(PlayerView forPlayer, String waitingForPlayerName)` | snapshot | 默认转发／快照展示回调 |
| `void updateAutoPassPrompt()` | snapshot | 默认转发／快照展示回调 |
| `void setCurrentPlayer(PlayerView player)` | snapshot | 默认转发／快照展示回调 |
| `boolean isUiSetToSkipPhase(PlayerView playerTurn, PhaseType phase)` | state | 已有精确处理或默认转发 |
| `default void refreshYieldUi(PlayerView player)` | snapshot | 默认转发／快照展示回调 |
| `void applyYieldUpdate(YieldUpdate update)` | unsupported | 原生 Forge yield 同步未接入 |
| `default void applyDelta(DeltaPacket packet)` | unsupported | 项目采用自有快照协议；原生 Forge delta 未接入 |
| `boolean isNetGame()` | state | 已有精确处理或默认转发 |
| `void setNetGame()` | snapshot | 默认转发／快照展示回调 |

## 必须继续处理的缺口

- **showInputDialog**：仅宏编辑直接调用；自由输入未接入，不能误报成所有牌名选择未接入
- **sideboard**：原生 Forge 局间换备牌未接入
- **applyDelta**：项目采用自有快照协议；原生 Forge delta 未接入
- **applyYieldUpdate**：原生 Forge yield 同步未接入
- **player-control**：已适配并用真实控制咒语验证双方座位整回合及仅战斗控制；嵌套/相互控制、控制者离场与网络重连仍须专项回归。
- **whole-boundary**：IGuiBase 48 声明、基础静态辅助调用和继承源文件已加入守卫；运行反射表解析145个PlayerController声明到实现。这是结构分类，不能证明全卡池无阻断，别名/间接调用、其他模式及组合仍未全审计。

## 新增／变更声明


## 基础界面边界

48 个 IGuiBase 声明；玩家／输入／静态辅助类范围 25 个基础界面／辅助调用位置。

| 方法 | 分类 | 处理 |
|---|---|---|
| `boolean isRunningOnDesktop()` | environment | Desktop true |
| `boolean isLibgdxPort()` | environment | False; native desktop input branches |
| `String getCurrentVersion()` | environment | Bridge version |
| `void invokeInEdtNow(Runnable runnable)` | thread | Synchronous dedicated UI executor |
| `void invokeInEdtLater(Runnable runnable)` | thread | Dedicated UI executor; exceptions fatal |
| `void invokeInEdtAndWait(Runnable proc)` | thread | Synchronous dedicated UI executor |
| `void runBackgroundTask(String message, Runnable task)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `boolean isGuiThread()` | thread | Exact mtgb-ui thread |
| `String getAssetsDir()` | environment | Pinned runtime assets |
| `ImageFetcher getImageFetcher()` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `ISkinImage getSkinIcon(FSkinProp skinProp)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `ISkinImage getUnskinnedIcon(String path)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `default ISkinImage getCardArt(PaperCard card)` | default | Native default body; forwarded methods keep their contract |
| `ISkinImage getCardArt(PaperCard card, boolean backFace)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `ISkinImage createLayeredImage(PaperCard card, FSkinProp background, String overlayFilename, float opacity)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `void clearImageCache()` | presentation | Unity owns local images |
| `String encodeSymbols(String str, boolean formatReminderText)` | presentation | Identity; Unity renders text |
| `int getAvatarCount()` | environment | One native avatar; Unity artwork |
| `int getSleevesCount()` | environment | One native sleeve; Unity artwork |
| `float getScreenScale()` | environment | Native scale one; Unity preferences |
| `void preventSystemSleep(boolean preventSleep)` | presentation | No native sleep control |
| `void download(GuiDownloadService service, Consumer<Boolean> callback)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `void copyToClipboard(String text)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `void browseToUrl(String url) throws IOException, URISyntaxException` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `void showCardList(String title, String message, List<PaperCard> list)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `boolean showBoxedProduct(String title, String message, List<PaperCard> list)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `void showBugReportDialog(String title, String text, boolean showExitAppBtn)` | fatal | Surface exception; no automatic dismissal |
| `void showImageDialog(ISkinImage image, String message, String title)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `int showOptionDialog(String message, String title, FSkinProp icon, List<String> options, int defaultOption)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `String showInputDialog(String message, String title, FSkinProp icon, String initialInput, List<String> inputOptions, boolean isNumeric)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `String showFileDialog(String title, String defaultDir)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `File getSaveFile(File defaultFile)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `<T> List<T> order(String title, String top, int remainingObjectsMin, int remainingObjectsMax, List<T> sourceChoices, List<T> destChoices)` | unsupported | No actor on global interface; explicit exception. Current Jotun upkeep uses native entity/card input instead. |
| `<T> List<T> getChoices(String message, int min, int max, Collection<T> choices, Collection<T> selected, FSerializableFunction<T, String> display)` | unsupported | No actor on global interface; explicit exception. Current Jotun upkeep uses native entity/card input instead. |
| `PaperCard chooseCard(String title, String message, List<PaperCard> list)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `boolean isSupportedAudioFormat(File file)` | environment | Native audio disabled |
| `IAudioClip createAudioClip(String filename)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `IAudioMusic createAudioMusic(String filename)` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `void startAltSoundSystem(String filename, boolean isSynchronized)` | presentation | Unity owns audio |
| `void showSpellShop()` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `void showBazaar()` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `IGuiGame getNewGuiGame()` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `HostedMatch hostMatch()` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `UpnpServiceConfiguration getUpnpPlatformService()` | unsupported | Explicit UnsupportedOperationException; no fabricated choice |
| `boolean hasNetGame()` | environment | Forge native network host disabled; bridge transport separate |
| `default void vibrate(int milliseconds, int amplitude)` | default | Native default body; forwarded methods keep their contract |
| `default void vibrateController(int milliseconds, float amplitude)` | default | Native default body; forwarded methods keep their contract |
| `default boolean useControllerForHaptics()` | default | Native default body; forwarded methods keep their contract |

基础接口、继承源文件或费用辅助源文件的守卫变化会阻止构建；分类与哈希一致仍不代表全部效果验证。

- **legacy-cost-helper**：HumanCostDecision has a global same-graveyard helper; current Jotun cumulative upkeep resolves through PlaySpellAbility native entity/card input instead. The global helper remains unsupported; do not infer it is reached by this card.
- **base-input**：Global free text, file, product/order and native lobby/dialog features stay explicitly unsupported; current player/input static calls include cheat/profile/lobby paths outside ordinary bridge matches.
- **inheritance**：Reflection resolves declared PlayerController signatures to exact inherited/bridge implementations. It does not prove every implementation, cost or rule branch behaves correctly.
- **helper-aliases**：Bounded player/input/gui-util scan detects direct GuiBase, SGuiChoose and SOptionPane calls only; aliased interfaces, external modules and arbitrary call chains not exhaustively analyzed.

范围：不覆盖所有游戏模式、间接／别名调用及任意机制组合；具体限制见 JSON。
