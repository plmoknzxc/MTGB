# Open card-template fonts

The reviewed font binaries, import metadata and license notices are included in source control. `open-fonts.json` records pinned upstream sources, SHA256, embedded copyright and derivative provenance. Unity loads these fixed resource names:

| Resource | Font | License |
|---|---|---|
| NameCN / NameJP | Source Han Serif CN / JP Bold | OFL 1.1 |
| RulesCN / RulesJP | Source Han Sans CN / JP Regular | OFL 1.1 |
| NameEN / RulesEN / StatsEN | EB Garamond SemiBold / Regular / Bold | OFL 1.1 |
| GlyphSymbols | Renamed LXGW WenKai subset: U+2610, space | OFL 1.1 |
| GlyphCJK | Renamed BabelStone Han 16.0.3 subset: U+2B689, U+2B812, space | Original Arphic Public License |

Retain all font copyright notices and license texts when distributing. Font copyright statements are also preserved in the binaries and the manifest. Source Han Serif: Copyright 2017–2024 Adobe; Source Han Sans: Copyright 2014–2025 Adobe. EB Garamond: Copyright 2017 The EB Garamond Project Authors. LXGW/Klee and Arphic/Andrew West notices are preserved in the subset font name tables. Fonts retain their own licenses, not the root MIT license.

The two fallback fonts were subsetted and renamed by MTGB contributors on 2026-10-14; glyph outlines were unchanged. Their modification notices are embedded in name ID 10. Reproduce them with `Tools/CardRendering/BuildCardFontFallbacks.py` and fonttools 4.66.1. Preserve `ARPHICPL.TXT` unaltered with the CJK subset; the pinned original download and modified font are available through the manifest and repository.

Previous DFWeiBeiGB/DFHeiGB/Beleren/Plantin/Times New Roman binaries are prohibited. Their local backups, obsolete client payloads, import artifacts and unreachable Git objects were removed on 2026-10-14; hashes and family names alone remain in `Tools/CardRendering/banned-fonts.json` for identification. The published Git history did not contain these five binaries.

`CardFontBuildGuard` checks every Unity player build, rejects the retired binaries even if renamed, checks imported family names and requires template fonts and notices to match `open-fonts.json`. Its import callback also rejects the exact retired binaries. `python Tools/CardRendering/ValidateCardFonts.py` checks source font family metadata and approved hashes, and runs on GitHub pushes and pull requests. Install `fonttools==4.66.1` for this check. A font upgrade requires reviewing its provenance/license and updating the approved manifest; removing the banned list is not an upgrade procedure. Independently included Noto CJK fonts under `Assets/Fonts` retain OFL.
