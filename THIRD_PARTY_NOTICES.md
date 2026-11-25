# Licenses and third-party material

The root MIT license covers MTGB-authored Unity client/server source code and documentation. It does not relicense third-party software, Forge-derived code, card data, fonts, artwork, trademarks or Unity itself.

| Component | License / source |
|---|---|
| MTGB-authored Unity client/server source | [MIT](LICENSE) |
| Moonlit Sanctum and Tidelights menu music | [Lost Place4](https://peritune.com/blog/2017/01/11/lost_place4/) (2017) and [Oboro](https://peritune.com/blog/2019/04/08/oboro/) (2019) by PeriTune (Sei Mutsuki), [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). MTGB adjusted the volume of the official loops and re-encoded them as Ogg Vorbis. |
| Moonlit Sanctum battle music | [Heartfelt Battle](https://opengameart.org/content/heartfelt-battle-loopable-fantasy-stringspianohorn) by request. The unmodified loop recording is available under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/). |
| Battlefield action sounds | Kenney's [Casino Audio](https://opengameart.org/content/54-casino-sound-effects-cards-dice-chips) and [RPG Audio](https://opengameart.org/content/50-rpg-sound-effects), [CC0](https://creativecommons.org/publicdomain/zero/1.0/). MTGB cut and mixed card, cloth, metal and impact recordings for game actions. |
| Forge, MTGB Java bridge/proof tools, native modifications | GNU GPL v3, see the local LICENSE files in `Tools/ForgeBridge`, `Tools/ForgeProof` and `ThirdParty/Forge` |
| Noto CJK fonts and derived SDF assets | SIL Open Font License 1.1; [bundled notice](Assets/Fonts/Noto-OFL.txt) |
| Card-template Source Han Serif/Sans, EB Garamond, renamed LXGW symbol subset | OFL 1.1; [font sources, copyright and notices](Assets/Resources/UI/CardTemplates/Fonts/README.md) |
| Renamed BabelStone Han CJK fallback subset | Original Arphic Public License; preserve [ARPHICPL.TXT](Assets/Resources/UI/CardTemplates/Fonts/ARPHICPL.TXT) |
| Unity Editor, Unity runtime and Unity packages | Their respective Unity/package licenses; not relicensed by this repository |
| Other Maven/UPM libraries | Their original licenses and copyright notices; consult the dependency POM/package metadata and distributed notices |
| FFmpeg replay video encoder (separate executable) | GPL v3; distributed client includes `VideoTools/LICENSE`, original build README and [provenance](VideoTools/PROVENANCE.md) |
| Magic card names, rules, translations, artwork and symbols | Rights remain with their respective owners, including Wizards of the Coast; software licenses do not grant ownership of these materials |

Forge is pinned to `583714b8bfbd9cb04f8bc39ad95861190caf7691`. Original notices are retained. Native changes are described by `Tools/ForgeBridge/NativePatches.json`; the matching patched Java source snapshot is in `ThirdParty/Forge/native-patches`.

The complete pinned Forge archive is bundled locally with the engine. The matching overlay and archive are distributed separately from Git due to their size. See [source availability and distribution](Docs/OpenSourceAndDistribution.zh-CN.md).

Card-template fonts were replaced in project Assets with the reviewed open fonts on 2026-10-14. The restricted or unverified retired font files, local backup/client payloads containing them, obsolete import artifacts and unreachable Git copies have been removed. Their hashes and family names remain solely as a denylist; Unity import/build validation and repository CI reject reintroduction. Published Git history contained none of the five retired binaries. The current desktop client and open-font update use the reviewed replacements. Other asset distribution rights remain separate.

This is an independent fan/development project and is not an official Forge, Unity or Wizards of the Coast product. An out-of-process Java bridge describes the technical architecture; it does not by itself settle whether a particular combined distribution is a single GPL-covered work.
