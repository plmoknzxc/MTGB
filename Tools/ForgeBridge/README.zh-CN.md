# Forge 构建与联机

当前使用官方印刷目录、持久对局会话和独立进程，联机对局在 IntegratedBattlefield 战场展示。操作与验证见 [战场说明](../../Docs/ForgeBattlefield.zh-CN.md)，运行结构见 [项目架构](../../Docs/ForgeArchitecture.zh-CN.md)。

当前固定版本：Forge 2.0.15（Reality Fracture），2026-09-30 同步。升级范围、验证和缺图见 [同步记录](../../Docs/ForgeUpdate-2.0.15.zh-CN.md)。

对战文本本地化：原生公开异能携带实时原文及来源牌面，客户端复用该牌面的译文。自动漏译日志位于客户端 `Application.persistentDataPath/Diagnostics/battle-text.jsonl`，当前及上一份各最多2 MiB，不含完整席位快照或隐藏牌。Unity编辑器的 `NativeMessageValidation.ReplaySavedMessages(path)` 在当前语言重放显示输入；未识别或截断的输入明确失败，不作为规则效果通过。`python -X utf8 Tools/ForgeBridge/Test-AbilityText.py` 验证真实施放、选目标、触发排序与独立的公开来源投影，`--cases` 可只补失败的尾段。诊断、验证范围与两端版本要求见 [对战英文残留记录](../../Docs/BattleTextLocalizationDiagnosis.2026-10-16.zh-CN.md)。

## 构建

需要 Unity 6000.3.22f1、Java 17 编译器、Maven、Python 3.11+ 和 PowerShell 7。

```powershell
pwsh -File Tools/ForgeBridge/Build-Bridge.ps1
```

首次使用可加 `-BuildUpstream`。脚本固定 Forge 提交、编译桥接程序、导出官方印刷目录，再下载经过 SHA-256 校验的 Eclipse Temurin Windows x64 JRE，复制官方资源。生成目录为项目根目录下的 `ForgeRuntime`。

构建会核对两层界面契约、玩家控制器及费用处理源文件守卫。`NativePatches.json` 保存少量已复核的原生修正：先核对完整源文件 SHA-256 和唯一替换上下文，生成完整修改源码，再编译到运行包 `classes`；上游检出不被改写。当前修正防止同一坟墓场费用取消选牌后继续对空结果排序。运行断言与展示证据见 [费用边界复核](../../Docs/LibraryCostBoundaryAudit-2026-10-04.zh-CN.md)。

全卡库加载的系列索引优化及准确身份/性能验证见 [内存与延迟优化记录](../../Docs/MemoryAndLatencyOptimization.2026-10-16.zh-CN.md#原生全卡库加载增量)。`python -X utf8 Tools/ForgeBridge/Test-CatalogLoading.py` 在 384 MiB Java 堆中核对延迟查询、完整印刷/卡面资料、系列索引和重复加载；`--runtime <运行目录>` 可验证打包结果，`--baseline <旧结果.json>` 增加资料不变与耗时改善断言。这些检查不代替卡牌效果回归。

`files.json` 登记 Java、桥接类、依赖和资源的逐文件哈希；`runtime.json` 保存清单及目录哈希。普通卡牌目录加载检查目录文件自身的哈希；显式重载和完整验证入口才逐文件验证运行资源，并在进程内缓存结果。入房只比较通信协议，权威端开局检查自己的桥接配置和必要路径。运行期间不要修改正在执行的引擎资源；升级后重启对应程序。

Java 与资源路径相对于 `ForgeRuntime`；每局使用独立的用户数据及缓存目录，退出会话时清理。

Unity 菜单入口：

- `MTGB/验证/卡牌/印刷目录`：完整印刷目录、歧义拒绝、已有牌组不改写。
- `MTGB/验证/对局/持续会话`：保存并重载官方牌组，真实 Java 会话、席位投影、非法输入及进程退出。
- `MTGB/验证/对局/异常终止`：异常终止不计分、不推进局数、不进入换备；正常 BO3 结束仍进入换备。
- `MTGB/验证/基础运行检查`：依次执行目录、持久会话和双进程故障隔离验证；结果写入 `Temp/project-validation-result.txt`。
- 机制效果与共用交互回归见 [验证说明](../../Docs/MechanicValidation.zh-CN.md)；`MTGB/验证/机制` 下可生成清单、运行登记场景和批量验证展示。不同机制共用界面仍需各自规则断言。
- `MTGB/构建/Windows 客户端（开发版）`：构建 `Builds/ForgeValidation/MTGB.exe`，复制运行资源与验证牌组。完成以 Editor.log 中的成功消息为准。
- `MTGB/构建/Windows 独立服务器`：构建原生 Windows Server 子目标 `Builds/Server/MTGBServer.exe`，包含相同的便携运行资源。

Linux 独立服务器构建会附带 `run-server.sh`，可执行 `sh run-server.sh -port 7777`。脚本保留站点已配置的 Java 预算，未配置时采用 32/384 MiB、两个处理器，并在没有显式堆空闲策略时启用经验证的紧凑堆回收参数。服务管理器若仍直接执行 MTGBServer，需改用此脚本或配置等价参数；回滚时也保留原启动命令。内存、时延与验证边界见 [服务端增量](../../Docs/MemoryAndLatencyOptimization.2026-10-16.zh-CN.md#服务端继续优化本轮)。

发布客户端可通过 Unity `-executeMethod ForgeBuildMenu.BuildReleaseFromCommandLine -clientOutput <新目录>` 构建 Windows x64 非开发版。发布包复用独立服务器的无界面 Forge 资源集合，保留规则、目录、JRE 与许可证；不携带 Forge 独立界面的冒险模式/皮肤资源和源码。开发构建入口保持原用途。

Windows 客户端默认以 1280×720 普通窗口启动，支持拖动边框缩放；启动时覆盖旧版保存的全屏设置，无界面批处理进程不调整窗口。

```powershell
python Tools/ForgeBridge/Package-Client.py --player <发布构建目录> --output <新ZIP路径>
```

仅打包 Unity 运行文件、Forge 运行资源、Starter 牌组和中文优先基础卡图，排除调试符号、验证牌组、报告、源代码、元文件和目录链接。需要附带其他牌组时显式追加 `--deck "Decks/living end.mdk"`（可重复指定），牌组会放入包内 `MTGB/Decks` 并记录文件校验值。卡图筛选规则见 [卡图说明](../../Docs/CardImages.zh-CN.md)；加 `--plan-only` 可先生成卡图清单和体积估算。打包校验与缺项写入 ZIP 内 `package-manifest.json` 及 ZIP 同级 `.manifest.json`，不等于全卡效果或外部联机验证。

```powershell
python -X utf8 Tools/ForgeBridge/Run-NetworkSmoke.py --mode dedicated --server-exe Builds/Server/MTGBServer.exe
python -X utf8 Tools/ForgeBridge/Run-NetworkSmoke.py --mode lan
python -X utf8 Tools/ForgeBridge/Run-NetworkSmoke.py --mode dedicated --best-of 1 --server-exe Builds/Server/MTGBServer.exe
python -X utf8 Tools/ForgeBridge/Run-NetworkSmoke.py --mode multiroom --server-exe Builds/Server/MTGBServer.exe
python -X utf8 Tools/ForgeBridge/Run-NetworkSmoke.py --mode mismatch --server-exe Builds/Server/MTGBServer.exe
python -X utf8 Tools/ForgeBridge/Run-RuleSmoke.py
python -X utf8 Tools/ForgeBridge/Run-RuleSmoke.py --mulligans 3
python -X utf8 Tools/ForgeBridge/Run-RuleSmoke.py --mulligans 2 --auto-bottom
```

验收启动真实 Windows 进程，经现有网络协议完成 BO3、注册卡池内换备、选择中自动重连、席位快照恢复及对手手牌脱敏。它通过认输结束每局，不能作为正常战斗结算、全卡效果或复杂隐藏信息的覆盖证明。结果在 `.tools/forge-migration/<模式>`。

## 交互边界

伦敦调度已适配 `InputLondonMulligan`：只列出当前玩家手牌，可以选择、取消选择或自动选牌，选中的牌标为「已选回牌库底」。牌数满足 Forge 要求后才允许确认。规则冒烟覆盖双方连续调度、取消选择、自动选牌、开局手牌数和对手手牌脱敏；网络冒烟每局双方各调度一次，再检查重连和 BO3 换备。

Unity 战场提交 Forge 当前请求的候选；已接入手牌、战场、玩家目标、费用、攻击与确认操作。排序、多选、部分费用与揭示信息等未实现回调必须明确异常终止，异常不计为玩家输局。目录规模不等于可玩性覆盖。

IntegratedBattlefield 在联机开局时自动加载，按服务器下发的状态显示卡牌并提交玩家选择。

## 来源

- Forge：`https://github.com/Card-Forge/forge`，提交 `4ec5f1a2c32fa90ecb983a72b9eb47aa5c5d7676`。许可证见运行资源 `assets/LICENSE.txt` 和上游源码；桥接 Java 文件标注 GPL-3.0-or-later。
- Eclipse Temurin JRE：版本、下载地址和 SHA-256 固定在 `Package-Runtime.py`，包内保留原有许可证文件。
- 包内 `source/forge-<revision>.zip` 是固定提交的上游源码；`source/bridge` 是对应桥接源码；`origins.json` 记录源档案及 JRE 校验值。`Build-Bridge.ps1`、`Package-Runtime.py` 是 MTGB 仓库布局下的构建脚本，不能直接在包内 `source` 根目录运行。
- `source/native-patches` 保存实际编译的完整原生修正源码，`NativePatches.json` 保存变更上下文与原始哈希，`origins.json` 记录变更清单与生成源码哈希。单独重建运行包时，也要用 JDK 17 将该目录中的 Java 源码编译到 `classes`，不能只编译桥接而漏掉修正。

包内源码的单独编译方法：安装 JDK 17 和 Maven，解压 Forge 源码到新目录；在源码根目录运行 `mvn -B -pl forge-gui -am package dependency:copy-dependencies -DskipTests -Dcheckstyle.skip=true`。然后在 `ForgeRuntime` 目录用 `javac -encoding UTF-8 -cp "lib/*" -d classes source/bridge/mtgb/bridge/*.java` 编译桥接。修改运行文件后需在完整 MTGB 仓库重新生成目录和哈希清单供分发及显式验证使用；只有通信协议发生不兼容变化时才要求双方同步升级协议。
