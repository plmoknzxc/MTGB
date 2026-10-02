# Forge ↔ Unity 最小适配验证

这是独立的技术验证工程。主 MTGB 工程、Lua、正式卡库、组卡和联机入口均不依赖这些验证代码。

## 运行

需要 Windows、JDK 17、Maven 3.8.1+、Unity 6000.3.22f1，以及 Unity Windows Mono 构建模块。脚本使用 PowerShell 7。

在 MTGB 根目录执行：

```powershell
pwsh -File Tools/ForgeProof/Build-Bridge.ps1
pwsh -File Tools/ForgeProof/Run-Proof.ps1
```

首次准备会下载固定版本 Forge 并构建其 core/game/ai/gui 模块。Forge 真人控制依赖 gui 模块，代码中不创建 AI 玩家。模块存在不代表使用了 AI 决策。

`Run-Proof.ps1` 依次运行三个 Play Mode 对局、构建 Windows Player、运行三个 Player 对局。也可指定 `-Mode Play`、`Build` 或 `Player`。修改桥接代码后可用 `Build-Bridge.ps1 -SkipForgeBuild` 单独重新编译桥接。

### 手动操作

用 Unity Hub 打开 `Tools/ForgeProof/UnityProject`，打开 `Assets/ForgeProof.unity`，进入 Play Mode，点击 **Start Forge**。若尚未生成场景，先运行一次自动验证。

1. 切换到当前请求所属的 Player 1 / Player 2 视图，确认先手与保留起手。
2. 在前面的优先权请求中点击 OK，直到 Player 1 的 MAIN1。
3. 点击手里的 Mountain，再确认 Play land。
4. 点击战场 Mountain，选择其产红能力；若再次出现能力确认，继续确认。
5. 点击手里的 Lightning Bolt，确认其能力。
6. 在 Forge 的目标请求中点击 Player 2。
7. 点击 **Pay {R} from mana pool**。
8. 堆叠有一个咒语时，分别在两名玩家的视图点击 OK。
9. 显示 PASS、Player 2 生命为 17；山脉横置、红色法术力归零、闪电击在坟墓场、堆叠为空。

关闭 Play Mode 或窗口会退出所属 JVM。验证中的模态能力确认最多等待 120 秒；这只是诊断超时，并非正式游戏计时规则。

Windows 可执行文件位于 `.tools/forge-proof/player/ForgeProof.exe`。手动启动时传入配置路径：

```powershell
& .tools/forge-proof/player/ForgeProof.exe -proof-config "$PWD/.tools/forge-proof/runtime/bridge-config.json"
```

该构建仍引用本机配置中的 JDK、JAR 和 Forge 资源目录，并非可直接分发到另一台电脑的安装包。

## 实现和验证边界

- 固定版本写在 `forge-version.txt`；桥接不修改 Forge 的规则实现或卡牌脚本。
- 使用 Forge `Match`、`LobbyPlayerHuman`、`PlayerControllerHuman`、`InputProxy`，以 `IGuiBase` / `IGuiGame` 代理连接独立调度线程。规则线程仍由 Forge 管理，等待玩家响应时 Unity 继续更新。
- 测试夹具固定随机种子，在第一回合开始钩子里为玩家 1 补齐闪电击，并换回一张手牌以保持七张；这属于起局布置。下地、产费、付费、指定目标、双方让过和伤害结算均走 Forge 原有输入及规则路径，不直接修改生命、法术力或堆叠。
- JSON 协议只服务本机诊断。一次消息含两份分别脱敏的席位视图，面板模拟两名玩家并切换展示；测试宿主能看到两份视图。正式联网时必须将两份视图分发给各自认证连接，不能把该诊断消息整体发给任一玩家。
- 字段化投影仅覆盖本测试使用的公开区域和自身手牌。面朝下、临时查看、复制、观战等不在验证范围。
- 目标候选仅实现本测试闪电击的两名玩家；非法山脉目标由桥接的候选白名单拒绝。合法目标仍交给 Forge `InputSelectTargets` 执行。本次没有证明通用目标候选导出完成。
- 非展示性的未实现返回接口抛出错误；绘制、动画、提示高亮等展示回调不做渲染。这是有意限定的原型，不是完整 `IGuiGame` 实现。
- 自动输入是测试驱动器，通过与调试按钮相同的 `Respond` 路径提交，不能作为真人点击操作的独立验收。

## 验收与输出

每次运行检查：费用真实消耗、双方堆叠优先权、最终生命及区域、非法目标不改变视图、同一目标响应只执行一次、对手手牌不出现在当前席位视图、等待选择时帧继续更新，以及 JVM 正常退出。

报告在 `.tools/forge-proof/play-report.json` 与 `player-report.json`，对应 `.wire.jsonl` 保存输入输出，`.java.log` 保存 Forge 诊断。报告记录实际 JVM PID、启动至第一个请求的时间、输入确认往返、下一请求延迟、每 100ms 采样的驻留内存峰值及每轮证据。Windows 使用 `Run-Proof.ps1 -Mode Player -ShowPanel` 可见窗口运行时还保存目标选择与成功截图；隐藏窗口运行可能不产生截图。

`roundTripMs` 是 Unity 发送至读取桥接确认的耗时，含 Unity 帧调度；`nextRequestMs` 是发送至读取后续请求的耗时，不等于所有复杂规则的运算上限。内存是本机进程驻留内存，不能外推为生产多房间容量。

`Probe-Bridge.py` 是开发阶段的协议诊断脚本；正式验收以 Unity 的报告为准。

## 版本与许可证

Forge 来源：https://github.com/Card-Forge/forge ，固定提交见版本文件。其许可证是 GPL-3.0；原始 LICENSE 随源码保留在 `.tools/forge-proof/upstream/LICENSE`。本目录 Java 桥接代码以 GPL-3.0-or-later 提供。正式分发时须连同适用许可证和对应源码落实发布要求。
