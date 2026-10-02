# 堆叠交互验证（2026-09-22）

## 结果

已补上堆叠实例目标选择和同时触发异能排序，并通过真实 Forge 引擎测试与 Unity Play Mode 界面回放。

### 界面行为

- 右侧堆叠保留完整卡图和固定纵横比；每项有序号、控制者、咒语/起动异能/诱发异能分类。
- 多项超过显示区域时使用滚动条，刷新同一堆叠不会重置滚动位置。
- 点卡牌或「详情」查看该堆叠实例的效果描述。长效果可以滚动。展示的是 Forge 的这一条效果，不能用来源卡牌的通用规则文字替代。
- 选择堆叠目标时，高亮 Forge 提供的合法候选项，支持卡牌点击和目标箭头拖放；「详情」独立查看，不提交目标。
- 同时触发排序在中央面板进行：按先结算顺序逐项选择，可重新排序，全部选完后明确确认。Forge 真人控制器将选择结果反向入堆叠，实际结算顺序与面板一致。
- 效果说明沿用 Forge 原文，目前通常为英文。

## 引擎验收

运行 `python -X utf8 Tools/ForgeBridge/Test-StackInteractions.py`。

测试入口 `tests/StackInteractionFixture.java` 仅安排官方卡牌的初始区域；施放、支付、目标、优先权、触发和结算均经过正常 Forge 真人输入。测试类不打包到生产运行时。

1. 玩家一 Lightning Bolt → 玩家二 Counterspell 指向 Bolt → 玩家一 Negate 指向 Counterspell。三张实际咒语同时在堆叠；双方让过后 Counterspell 被反击，Bolt 实际造成 3 点伤害，对手 20 → 17。
2. Soul Warden 与 Soul's Attendant 同时在场，正常施放 Goblin Piker 并让过。实际同时触发两个异能，玩家排序、重排并确认，检查堆叠顺序。施放 Stifle 指向先结算的那个异能，三个条目同时在堆叠，最终仅另一个加血异能生效，生命 20 → 21。
3. 反转上述两个异能的顺序，再次确认入堆叠顺序及 Stifle 的目标与实际结算。两个顺序均通过。
4. 无效堆叠实例标识被拒绝；重复旧请求被判定过期；回发状态一致，没有重复执行。
5. 每个案例关闭自己启动的 Java 子进程并记录退出状态。

原始请求与状态：`Tools/Validation/Reports/StackInteractions/*.jsonl`。
结果：`engine-result.txt`；子进程：`*-process.txt`。

## Unity 验收

打开 IntegratedBattlefield，进入 Play Mode，调用 `StackInteractionValidation.Run()`。

- 回放上面的真实 Forge 快照，逐项滚动查看三个条目的独立效果。
- 指向堆叠的箭头有合法端点；顶部及滚动后的下方条目均提交正确的堆叠实例 ID。
- 空白、过期、重复指目标均不提交；目标选择期间查看详情不提交。
- 初次选择排序项、显示确认列表、明确确认均有效；不占用右侧堆叠位置。
- 另有 **12 项界面压力样本**（不是 12 项真实引擎测试）：验证同一来源卡的不同异能实例不会合并，最后一项可到达，长效果文字可滚动。
- `ArenaPresentationValidation.RunStack()` 原有卡图尺寸、纵横比、滚动位置和施放预览避让检查通过。
- `CastingMotionValidation.Run()` 原有施放、确认取消后回手、快速入堆叠、去重及动画清理检查通过。

结果和截图：`ui-result.txt`、`multiple.png`、`order.png`。

## 版本及边界

Forge 提交：`746455d75515daabec62971e0544cf19c66356b3`。
运行时校验值：`db569872db815e9f2ad5171563dc44a9a0f551444934eca90401b09cdb094b3b`。
桥接源码副本、编译类与运行时清单已同步。

本次验证范围为上述咒语/诱发异能案例及 Unity 编辑器交互，不代表全卡、所有多目标组合、跨玩家同时触发组合、联网往返或 Windows 独立构建全部验证。非异能的排序接口（例如库顶卡排序、替代效应排序）仍明确报未适配，不会静默猜顺序。

排序依据为固定版本 `PlayerControllerHuman.orderSimultaneousSa` / `orderAndPlaySimultaneousSa`，堆叠目标依据为 `TargetSelection.chooseCardFromStack`。图索引工具本次连接不可用，使用了对应源码核验。
