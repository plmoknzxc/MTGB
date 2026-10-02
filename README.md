# MTGB

Unity 6 客户端与 Forge 规则引擎。当前通过组卡、大厅和 IntegratedBattlefield 战场运行双人对局，使用 Unity 卡牌与选择面板。

## 构建与运行

1. 用 Unity 6000.3.22f1 打开项目。
2. 运行 `pwsh -File Tools/ForgeBridge/Build-Bridge.ps1`；首次需要 Java 17、Maven、Python 3.11+ 和 PowerShell 7。
3. 在 Unity 运行 `MTGB/Validate Project`，验证目录、持久会话和进程隔离。
4. 运行 `MTGB/Forge/Build Validation Player`，产物位于 `Builds/ForgeValidation`。
5. 启动客户端，从大厅提交官方 `.mdk` 牌组并开局。用 `MTGB/Networking/Build Dedicated Server` 构建 `Builds/Server/MTGBServer.exe`，启动参数为 `-batchmode -nographics -mtgb-server -port 7777`。

Windows 包内包含 Java、固定版本 Forge、官方资源和对应 Forge/桥接源码；不依赖构建机器上的 JDK 或源码目录。客户端与服务器必须匹配协议及资源哈希。

## 当前范围

- 官方印刷目录、主备牌保存加载与明确的缺失/歧义报告。
- 局域网房主和独立服务器、BO1/BO3、局间换备、选择中重连。
- Forge 唯一执行规则；旧 Unity 规则和 Lua 运行时已退出。
- 排序、多选、揭示及控制其他玩家等交互尚有限制，未适配机制明确终止本局且不计败局；目录规模不是全部卡牌可玩性承诺。
- DIY 编辑器迁移、旧 DIY 转换和完整动画后续处理；已接入的战场操作见 [战场说明](Docs/ForgeBattlefield.zh-CN.md)。

构建命令、测试范围和剩余限制见 [ForgeBridge](Tools/ForgeBridge/README.zh-CN.md)、[验证记录](Tools/ForgeBridge/Reports/验证记录.zh-CN.md) 与 [迁移计划](Docs/ForgeMigration.zh-CN.md)。

旧卡包、数据库及 Lua 脚本目录已按要求移除；当前测试请选择 `Decks/Starter.mdk`，详见 [牌组说明](Decks/README.md)。[旧项目说明](Docs/LegacyProjectOverview.zh-CN.md) 和 [Lua SDK](Docs/LuaSDK.zh-CN.md) 仅供历史迁移参考。清理前的源码及原有未提交修改保存在本地 Git 引用 `codex/forge-legacy-reference-20260915`（`8754cdadc3bc17850d06233a76e9889131e3c22b`）。
