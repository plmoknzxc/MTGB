# Unity 战场接入

**状态：功能验证阶段，场景尚未完成。** 当前接入用于验证规则状态与 Unity 界面的连接，不作为最终战场的视觉或完整交互验收。

联机开局后自动进入新场景 `IntegratedBattlefield`。旧场景为 `MTGAPrototype`，不再作为默认联机入口。新场景复用已有 Canvas、牌桌、生命面板、手牌锚点、检视区、堆叠区和回合按钮，显示服务器下发的真实状态。

## 操作

- 自己的手牌始终在下方，对手手牌只显示牌背和数量。
- 金色边框表示当前可以选择的牌；点击出牌或激活，也可以把高亮手牌拖到战场。
- 目标选择时点击高亮的卡牌或玩家生命面板。
- 费用选择时点击高亮地牌产费，再按右侧选项确认；右下角显示各色法术力池。
- 右侧面板处理起手、模式、确认、取消和优先权。卡牌文字保留目录原语言。
- 攻击／阻挡选择时点击可选生物，再按提示完成选择；边框和状态文字显示攻击、阻挡及横置。
- 悬停查看卡牌详情；点击坟墓场查看公开列表，右侧原堆叠区显示公开咒语/异能。牌库仅显示数量；放逐区目前没有桥接数据，显示“—”。
- 卡牌数量超过一页时使用所在行两端的箭头；长选择列表可以滚动。
- 认输需要二次确认；每局结束返回大厅处理比分、换备及下一局。

## 数据与限制

所有操作仍通过认证网络席位提交当前请求的候选编号，界面不推算合法动作。旧请求和重复点击被阻止；连接中断或等待服务器确认时不允许再次提交。

卡牌按精确印刷编号寻找已有卡图，缺图时显示文字卡面。场上的攻防、伤害、横置和战斗状态来自规则引擎。没有批量下载官方卡图，也未实现全部复杂交互、特效、战斗连线及所有双面/复制效果的视觉表现。

## 验证入口

在 Unity 打开 IntegratedBattlefield 场景后：

- `MTGB/Forge/Validate Battlefield UI`：实际 Starter 起手，通过 uGUI 按钮下地、施放闪电击、选择玩家、支付费用、让过优先权，检查 20 → 17；检查重复点击只提交一次。
- `MTGB/Forge/Validate Battlefield Combat`：实际 Starter 起手，施放怒火鬼怪并宣告敏捷攻击，检查战斗伤害 20 → 19。
- `MTGB/Forge/Validate Battlefield Blocking`：在内存中建立山脉/怒火鬼怪测试牌组，双方正常下地、支付费用及施放生物；检查攻击、阻挡、取消并重新指定阻挡，最终双方生物进入坟墓场且生命保持 20/20。该牌组仅用于交互测试，不验证构筑赛制合法性，不写入玩家牌组文件。

2026-09-22 在 IntegratedBattlefield Play Mode 的最新实测与范围见 [交互验证记录](BattlefieldValidation-2026-09-22.zh-CN.md)。

结果与截图写入 `.tools/forge-migration/battlefield`。这两项测试运行真实 Java 会话；联机回归另用 `Tools/ForgeBridge/Run-NetworkSmoke.py`。回归客户端现在经过战场控制器提交选择，验证场景切换后的 BO3、重连与返回大厅。

## 继续制作场景

适配代码位于 `Assets/Scripts/UI/ForgeBattlefieldController.Integrated.cs`，公共请求校验位于 `ForgeBattlefieldController.cs`。不重新生成场景 Canvas，不覆盖磁盘上的美术布局。

保留 Canvas 下节点名称与层级：`PlayerHandRoot`、`OpponentHandRoot`、`PlayerPlate`、`OpponentPlate`、`InspectPanel`、`StackColumn`、两组 `ZonePiles_*`、`CornerCluster`、`GearBox`。可以继续调整它们的位置、大小及美术；更名或改变层级时同步更新绑定路径。手牌跟随原手牌锚点，运行时选择面板跟随 CornerCluster。永久物行的默认布局在 `IntegratedRow` 调用处。

没有规则数据支撑的倒计时暂时隐藏，设置按钮暂时禁用。适配完成不表示这些功能、美术或全部复杂卡牌交互完成。
