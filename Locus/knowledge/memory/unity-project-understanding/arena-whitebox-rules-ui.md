---
id: kd_29d2f05f-2aa8-4419-aac6-c93868e7420b
injectMode: inherit
summary: 'Current scan: project has evolved into a fuller MTGA-style prototype with CardDefinition/RuntimeCard, GameRules priority/stack/mana/combat, SQLite card database loading, C#+Lua effects, dynamic UI hand cards, and runtime arena scene generation.'
aiEditMode: inherit
---

- Current card model is `CardDefinition` (not legacy `CardData`) with fields for card identity, type flags, mana cost, target mode, artwork, `luaScript`, and `effectKey`.
- `RuntimeCard` wraps `CardDefinition` and owns per-instance state: tapped, damage, temporary P/T modifiers, targets, keywords, activated abilities, combat state, and script instance.
- `GameRules` is now a large central rules manager: hands, battlefield, graveyard/exile, life, turn/priority, stack, mana pools/auto mana abilities, targeting checks, attacker/blocker selection, combat damage, cleanup/discard, and card play/resolve.
- `CardEffectExecutor` dispatches `CardEffectRegistry` C# effects by `CardDefinition.effectKey`, then invokes Lua hooks via `CardScriptRuntimeManager` and notifies battlefield cards.
- Sample C# effect keys currently include `Deal4ToOpponent`, `Draw1`, and `SelfPlus1Plus1`.
- `PrototypeMatchSetup` supplies fallback sample cards/decks and a Lua mana ability for Training Island (`Game:RegisterManaAbility(self.Card, "{U}")`). It also seeds opponent starting battlefield cards.
- `CardDatabaseRepository` loads external SQLite card databases from `CardDatabases`, reads card scripts from `CardScripts`, supports schema versions 1-2, and maps database `card_effects` rows into `effectKey`.
- UI has moved beyond only fixed 3D whitebox slots: `MTGAHandUiController` dynamically creates UI hand cards, lays them out in a fan, supports pointer drag/drop, cast confirmation, discard-to-hand-size, and target-selection handoff to `ArenaWhiteboxInteraction`.
- `MTGAHandUiCard` is a per-card UI GameObject component for hand interaction and display. `CardView` also exists for card prefab binding/hover display, but the current arena hand path uses `MTGAHandUiCard`.
- `ArenaWhiteboxInteraction` remains the bridge for 3D battlefield object interaction: stack/priority UI, targeting, clicking/tapping lands, combat selection, and fallback 3D hand/card slot interaction.
- `Assets/Scenes/MTGAPrototype.unity` is the persisted match scene; its `Systems` object hosts `GameRules`, `CardManager`, `ArenaWhiteboxInteraction`, `ArenaWhiteboxCardSync`, `MTGAHandUiController`, and `ArenaMatchLayoutController`.
- `ArenaMatchLayoutController` applies the shared `ArenaMatchLayoutSettings` to hands, battlefield cards/drop zones, stack/cast UI, player/zone UI, and `ArenaHudController`. It references persistent profile `Assets/Settings/ArenaMatchLayoutProfile.asset`.
- Locus View `arena-ui-layout-tuner` under `Locus/View/MTGB/arena-ui-layout-tuner` edits the full match layout in Chinese and saves directly to the persistent profile, including during Play Mode.
- `ArenaHudController` builds MTGA-style bottom-right phase/full-control/skip-opponent/mana HUD controls and binds to `GameRules`.
- Known architectural risk: there are two partially overlapping interaction/display paths (`ArenaWhiteboxInteraction` 3D slot path and `MTGAHandUiController` UI hand-card path). Future work should choose one canonical gameplay path and reduce duplicate drag/cast/drop logic.
- Known limitation: battlefield visual generation still appears slot/placeholder based (`ArenaBattlefieldCardVisual` bindings and generated battlefield card slots), while hand UI is closer to true per-card instances.
