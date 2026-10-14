# 开源许可与发行源码

## 许可范围

MTGB 自有 Unity／客户端源码与文档采用 MIT。`Tools/ForgeBridge`、`Tools/ForgeProof` 和 `ThirdParty/Forge` 的 Java 桥接、原生修改与 Forge 衍生材料采用 GNU GPL v3，并保留上游许可／版权。根 MIT 许可不覆盖这些例外，也不覆盖第三方字体、卡牌资料、卡图、商标或 Unity 运行库。

Forge 原仓库：<https://github.com/Card-Forge/forge>；固定提交：`4ec5f1a2c32fa90ecb983a72b9eb47aa5c5d7676`。本地构建入口先取得该提交，再按 `Tools/ForgeBridge/NativePatches.json` 校验、应用修改和编译；桥接源码、补丁定义、验证夹具和构建脚本均在公共仓库中。

## 当前版本对应源码

[Forge 对应源码下载](https://github.com/plmoknzxc/MTGB/releases/tag/forge-source-20261014) 提供：

- `forge-4ec5f1a2c32fa90ecb983a72b9eb47aa5c5d7676.zip`：固定上游提交的完整源码，SHA256 `2f060fbe00f7e28c052aeb935ad9723eeafda71c4ef7eeb2619cdde496810190`。
- `mtgb-forge-source-overlay-20261014.zip`：当前 Java 桥接、实际修改的 Java 源码、补丁定义、构建脚本和来源记录。精确清单在 `ThirdParty/Forge/source-manifest.json`。
- 自有 Unity 源码在本仓库。原始本机验证日志和大体积输入位于未上传的 `.tools`；公开测试脚本、夹具、报告及文档保留各自的实际验证范围，缺少本机附件不视为新增测试通过。

从干净仓库重建：安装 Unity 6000.3.22f1、Java 17、Maven、Python 3.11+、PowerShell 7，执行 `pwsh -File Tools/ForgeBridge/Build-Bridge.ps1 -BuildUpstream`。该流程取得固定 Forge 源码／Maven 依赖并应用公开补丁。原生 Java 的源码哈希校验允许 Git 的 LF／CRLF 检出转换，任何实际内容修改仍被拒绝；公开上游归档的 45 份 Java 与 9 份原卡脚本分别核对。Unity 卡牌重绘需要自行准备有重分发权利的模板字体，见字体目录说明。

## 分发程序时

当前精简客户端／服务端没有内嵌完整 Forge 源码 ZIP。分发这些二进制时，应一并提供 Forge 的 GPL 文本／原通知、明确的对应源码位置，以及与该构建一致的 Java 修改、构建脚本和依赖许可。修改 Forge 后应刷新源码清单与对应源码下载，不能继续把旧源码包指认为新二进制的源码。

Temurin JRE 和 Maven／Unity 依赖继续遵循各自的许可证；JRE 原许可及 Classpath Exception 位于运行包 `jre/legal`，二进制来源记录在 `ForgeRuntime/source/origins.json`。重分发者需按实际 JRE／依赖版本提供其要求的对应源码和通知；本页不将上游网页链接等同于所有二进制发行要求已自动满足。

公开源码加明确许可后，自有代码具备开源许可；“上传到公开 GitHub”本身不等于 GPL 合规。Unity 与 Forge 使用独立进程和 JSON 通信，但 GPL 对独立作品／组合程序的区分还看实际结合方式。本文记录源码交付与许可事实，不保证所有发行形态均完成法律合规。

## 素材边界

Noto CJK 保持 SIL OFL。本地模板里的 DFWeiBeiGB、DFHeiGB 禁止转赠；Beleren、Plantin 等未核实重分发许可，已从公共 Git 排除。公开客户端构建需替换成可重分发字体或取得相应授权。卡图／卡文／符号等第三方权利也不因软件开源而改变。

依据：[Forge GPL](https://github.com/Card-Forge/forge/blob/4ec5f1a2c32fa90ecb983a72b9eb47aa5c5d7676/LICENSE)、[GNU GPL v3（对应源码与分发）](https://www.gnu.org/licenses/gpl-3.0.html)、[GNU FAQ（作品组合）](https://www.gnu.org/licenses/gpl-faq.html#MereAggregation)、[GitHub 许可说明](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository)。
