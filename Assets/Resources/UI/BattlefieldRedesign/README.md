# Hand tray artwork

Generated with the built-in imagegen tool on 2026-10-25 for the copied battlefield scene. The original generated PNG is preserved, including its alpha. Unity imports a single sub-sprite from pixel rectangle (0,172,2172,375), excluding transparent vertical margins. Borders (left,bottom,right,top): (180,48,180,48). Runtime Image uses Sliced and pixelsPerUnitMultiplier=2; no mipmaps or lossy texture compression. UI text, source tabs and card art remain independent.

Asset: `HandTrayMetal.png`. Used by both hand docks via `BattlefieldHandPanelTint.ApplyArtwork`; the previous gradient remains a missing-asset fallback. Opponent background does not intercept input. Own background retains its reveal and drag handlers.

## Final generation prompt

Use case: stylized-concept. Asset type: production 2D fantasy card game HUD hand tray, transparent PNG, ONE wide horizontal panel viewed perfectly front-on. Canvas wide 3:1, panel fills canvas almost edge to edge. A restrained dark blue-black brushed/etched metal hand tray with thin antique bronze bevel trim, elegant tiny angular corner details, softly textured dark quiet inset center. Thin top rail, gently chamfered corners, symmetrical left/right. Center has no decoration, intended to sit behind real cards and UI tabs. Premium crisp high-resolution game UI material, subtle realistic metal detail, readable silhouette, low contrast center. Genuine transparent exterior outside the single panel, no checkerboard baked in. No text, no labels, no icons, no cards, no buttons, no scene background, no props, no additional panels, no drop shadow outside. The frame should be understated and slender, not ornate, not neon. Entire panel fully in image, no clipping. Flat orthographic sprite suitable for Unity nine-slicing.
