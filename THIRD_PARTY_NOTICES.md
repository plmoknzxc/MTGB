# Licenses and third-party material

The root MIT license covers MTGB-authored Unity/client source code and documentation. It does not relicense third-party software, Forge-derived code, card data, fonts, artwork, trademarks or Unity itself.

| Component | License / source |
|---|---|
| MTGB-authored Unity/client source | [MIT](LICENSE) |
| Forge, MTGB Java bridge/proof tools, native modifications | GNU GPL v3, see the local LICENSE files in `Tools/ForgeBridge`, `Tools/ForgeProof` and `ThirdParty/Forge` |
| Noto CJK fonts and derived SDF assets | SIL Open Font License 1.1; [bundled notice](Assets/Fonts/Noto-OFL.txt) |
| Unity Editor, Unity runtime and Unity packages | Their respective Unity/package licenses; not relicensed by this repository |
| Other Maven/UPM libraries | Their original licenses and copyright notices; consult the dependency POM/package metadata and distributed notices |
| Magic card names, rules, translations, artwork and symbols | Rights remain with their respective owners, including Wizards of the Coast; software licenses do not grant ownership of these materials |

Forge is pinned to `4ec5f1a2c32fa90ecb983a72b9eb47aa5c5d7676`. Original notices are retained. Native changes are described by `Tools/ForgeBridge/NativePatches.json`; the published patched Java source snapshot is in `ThirdParty/Forge/native-patches`.

The complete pinned Forge archive and an MTGB source overlay are published separately because the full archive exceeds GitHub's regular Git file limit. See [source availability and distribution](Docs/OpenSourceAndDistribution.zh-CN.md).

Local card-template fonts with restricted or unverified redistribution permission are excluded from public Git. This includes the DFWeiBeiGB/DFHeiGB fonts whose embedded licenses prohibit giving the font files to others. A public binary distribution also needs a font/asset rights review; excluding them from Git alone does not authorize their presence in an application build.

This is an independent fan/development project and is not an official Forge, Unity or Wizards of the Coast product. An out-of-process Java bridge describes the technical architecture; it does not by itself settle whether a particular combined distribution is a single GPL-covered work.
