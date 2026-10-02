# 地类别持续性效应核查（2026-09-27）

## 范围和依据

- 依据官方 2026-09-25 CR：305.6–305.8、613.1d、613.7；蓝月按 Harbinger of the Seas 核查。
- 官方规则：https://media.wizards.com/2026/downloads/MagicCompRules%2020260925.txt
- 使用 ForgeRuntime 当前打包引擎、现有卡牌定义；没有修改卡牌数据；修复集中在桥接输出和 Unity 表现层。
- 命令：`python -X utf8 Tools/ForgeBridge/Test-LandTypes.py`
- 验证的是引擎计算后的类别、能力、法术力能力以及移除来源后的恢复；不是完整对战/UI 点击端到端验收。

## 已通过的 8 组

| 效应先后顺序 | 己方 Wooded Foothills 的产费能力 |
|---|---|
| Blood Moon | 红 |
| Harbinger of the Seas | 蓝 |
| Prismatic Omen | 白蓝黑红绿，保留找地能力 |
| Dryad of the Ilysian Grove | 白蓝黑红绿，保留找地能力 |
| Blood Moon → Harbinger of the Seas | 蓝 |
| Harbinger of the Seas → Blood Moon | 红 |
| Blood Moon → Prismatic Omen | 白蓝黑红绿，找地能力不恢复 |
| Prismatic Omen → Blood Moon | 红 |

额外断言：基本地不受红/蓝月影响；非基本地不会获得 Basic 超类别；树灵/预兆只影响控制者的地；Dryad Arbor 保留 Creature/Dryad；Urza’s Saga 保留 Enchantment/Saga；移除来源后找地地恢复。

## 已修复的数据和显示

- `currentType` 读取应用持续性效应后的 `Card.getType()`。
- 战场地的 `currentRules` 读取当前有效能力，并补充引擎当前法术力异能列表；移除的能力不再显示为当前能力。
- `hasCurrentLandState` 区分新数据与旧录像，旧录像不会被错误显示成“无能力”。
- 场上卡图不添加类别标签。悬停详情去掉“当前能力”标题：保留原始类别和能力，失去的部分变灰，新增的部分变蓝；可切换“原始状态”并返回变化视图。
- 类别和能力纳入合并/外观键；来源离场后颜色恢复，已打开的地详情同步恢复。堆叠异能详情和墓地区域浏览不被地详情刷新覆盖。
- 观战保留公开的有效地状态；面朝下牌不传出新增字段。协议升级到 v17；运行时已重新打包。

## 验证

- 8 组引擎测试全部通过，新增断言：wire 类别与引擎一致；红/蓝月没有旧找地文字；五色产费逐一出现；移除来源后类别标记及能力恢复。
- `LandTypePresentationValidation` 用实际引擎快照验证红月、蓝月、虹彩预兆的 Unity 无额外卡图标签、变化颜色及原始状态切换，来源离场后的悬停刷新、合并键，以及观战/面朝下隐私；通过。
- `Test-LandAbilities.py` 回归 Mountain 直接产红、Shivan Reef 多选产无色；通过。
- UI 截图：`.tools/land-type-ui/blood.png`、`blue.png`、`omen.png`、`restored.png`。

## 未覆盖

没有据此宣称所有 CR 交互通过：依赖关系（如 Urborg）、失去能力、地生物化、额外来源赋予的能力、进场替代效应，以及 Saga 状态动作等还需各自的专项测试。本测试中的 Saga 只核查类别，不检查其牺牲状态动作。

原始记录：`Tools/Validation/Reports/LandTypes/*.json`，汇总 `result.txt`。
