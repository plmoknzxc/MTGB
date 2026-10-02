# Forge 构建与联机

当前使用官方印刷目录、持久对局会话和独立进程，联机对局在 IntegratedBattlefield 战场展示。操作与验证见 [战场说明](../../Docs/ForgeBattlefield.zh-CN.md)，运行结构见 [项目架构](../../Docs/ForgeArchitecture.zh-CN.md)。

当前固定版本：Forge 2.0.15（Reality Fracture），2026-09-30 同步。升级范围、验证和缺图见 [同步记录](../../Docs/ForgeUpdate-2.0.15.zh-CN.md)。

## 构建

需要 Unity 6000.3.22f1、Java 17 编译器、Maven、Python 3.11+ 和 PowerShell 7。

```powershell
pwsh -File Tools/ForgeBridge/Build-Bridge.ps1
```

首次使用可加 `-BuildUpstream`。脚本固定 Forge 提交、编译桥接程序、导出官方印刷目录，再下载经过 SHA-256 校验的 Eclipse Temurin Windows x64 JRE，复制官方资源。生成目录为项目根目录下的 `ForgeRuntime`。

`files.json` 登记 Java、桥接类、依赖和资源的逐文件哈希；`runtime.json` 保存清单及目录哈希。普通卡牌目录加载检查目录文件自身的哈希；显式重载和完整验证入口才逐文件验证运行资源，并在进程内缓存结果。入房只比较通信协议，权威端开局检查自己的桥接配置和必要路径。运行期间不要修改正在执行的引擎资源；升级后重启对应程序。

Java 与资源路径相对于 `ForgeRuntime`；每局使用独立的用户数据及缓存目录，退出会话时清理。

Unity 菜单入口：

- `MTGB/Forge/Validate Catalog`：完整印刷目录、歧义拒绝、已有牌组不改写。
- `MTGB/Forge/Validate Persistent Session`：保存并重载官方牌组，真实 Java 会话、席位投影、非法输入及进程退出。
- `MTGB/Forge/Validate Aborted Match`：异常终止不计分、不推进局数、不进入换备；正常 BO3 结束仍进入换备。
- `MTGB/Validate Project`：依次执行目录、持久会话和双进程故障隔离验证；结果写入 `Temp/project-validation-result.txt`。
- `MTGB/Forge/Build Validation Player`：构建 `Builds/ForgeValidation/MTGB.exe`，复制运行资源与验证牌组。完成以 Editor.log 中的成功消息为准。
- `MTGB/Networking/Build Dedicated Server`：构建原生 Windows Server 子目标 `Builds/Server/MTGBServer.exe`，包含相同的便携运行资源。

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

包内源码的单独编译方法：安装 JDK 17 和 Maven，解压 Forge 源码到新目录；在源码根目录运行 `mvn -B -pl forge-gui -am package dependency:copy-dependencies -DskipTests -Dcheckstyle.skip=true`。然后在 `ForgeRuntime` 目录用 `javac -encoding UTF-8 -cp "lib/*" -d classes source/bridge/mtgb/bridge/*.java` 编译桥接。修改运行文件后需在完整 MTGB 仓库重新生成目录和哈希清单供分发及显式验证使用；只有通信协议发生不兼容变化时才要求双方同步升级协议。
