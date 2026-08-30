---
id: kd_ac1ce521-748f-482c-bc40-c7826a748dec
injectMode: inherit
summary: Card prefab templates, CardView binding behavior, hover detail panel, and Main scene board layout.
aiEditMode: inherit
---

- `Assets/Cards/Prefabs/MTGStandardCardPrefab.prefab` is a Unity UI prefab built from built-in UI components. Root size is 300x420 RectTransform.
- It uses named child regions matching a modern MTG-like standard card face: `NameLine_NameAndManaCost`, `ManaCost_WUBRG`, `ArtworkBox`, `TypeLine_CardTypeAndSubtype`, `RulesTextBox`, `PowerToughnessBox`, `CollectorInfo_Footer`, plus `ColorLegend` for visible region color labels.
- `Assets/Scripts/UI/CardView.cs` is attached to the prefab root. It binds `CardData` fields to the visual labels: `cardName`, `cost`, `description`, `attack`, `defence`, `artwork`, and `cardType`.
- `ColorLegend` is used as the hover detail panel. `CardView` implements `IPointerEnterHandler`/`IPointerExitHandler` and toggles it only while the pointer is over the card.
- `CardView` includes serialized future hooks for fields not yet present on `CardData`: `manaCostOverride`, `typeLineOverride`, `collectorInfoOverride`, `rarityOverride`, and `setCodeOverride`.
- `Assets/Cards/Prefabs/HearthStyle3DCardPrefab.prefab` is the 3D battlefield card shell. It uses default primitives only: `3DOuterFrame` cube, `Embedded2DCardArt` quad, and `TopNamePlate` cube. It is intended for the Hearthstone-like board representation: 3D outer body with a 2D card art surface embedded inside.
- `Assets/Scenes/Main.unity` currently has a default-component board layout: `Tabletop`, `Battlefield/PlayerSide`, `Battlefield/OpponentSide`, `Battlefield/CenterLane`, `HandZone/*`, deck/graveyard cubes, `TextMesh` labels, and `CardShowcase` containing five sample instances of `HearthStyle3DCardPrefab`.
