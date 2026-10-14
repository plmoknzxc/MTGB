# 共用交互与结算表现验证

按共用交互找缺口：一张牌可能经过多个流程；一个流程对应很多机制。关联不等于整类已经验证。

[录像时间点与观察范围](../../../../Docs/ArenaReferenceAudit-2026-10-03.zh-CN.md) · [机制结构清单](README.md)

补充录像：[地城、资格和土地变化](../../../../Docs/ArenaReferenceAudit-2026-10-03-0816.zh-CN.md) · [地城、资格和土地变化](../../../../Docs/ArenaReferenceAudit-2026-10-04-1016.zh-CN.md)

规则断言和 Unity 展示断言分别记录，PASS 只限列出的场景和检查。录像观察不是测试结果。

| 共用流程 | 录像时间点 | 本轮规则场景 | Unity 检查 |
|---|---|---|---|
| 起手与调度 | 01:40、43:40 | 0/12 PASS | starting-hands: 需重测；opening-boundaries: 需重测 |
| 优先权、响应与等待 | 36:40、41:40 | 0/12 PASS | choices: 需重测；stack: 需重测；digital: 需重测；mana-source-refunds: 需重测 |
| 施放面、模式与异能选择 | 08:00、51:00 | 0/88 PASS | choices: 需重测；generic-choices: 需重测；starting-hands: 需重测；card-names: 需重测；replacements: 需重测；damage-draw: 需重测；mode-cards: 需重测；stack-choices: 需重测；cast-choice-families: 需重测；stack-list: 需重测；variable-branches: 需重测；dungeon-paths: 需重测；dungeon-restrictions: 需重测；dungeon-completion: 需重测；convoke-generic-icon: 需重测；stack-exile-cost: 需重测；stack-restore-list-increment: 需重测；reveal-cost-rollback-increment: 需重测；behold-exile-increment: 需重测 |
| X／数量／分配 | 04:40 | 0/39 PASS | selection: 需重测；advanced: 需重测；prepare: 需重测；combat-allocation: 需重测；variable-branches: 需重测；target-branches: 需重测；number-auction: 需重测；quantity-allocation: 需重测 |
| 法术力来源与支付 | 49:20 | 0/273 PASS | choices: 需重测；prepare: 需重测；mana-results: 需重测；payment-dock: 需重测；mana-variants: 需重测；phyrexian-payments: 需重测；phyrexian-modifiers: 需重测；snow-pool: 需重测；cost-interactions: 需重测；exert-branches: 需重测；quantity-allocation: 需重测；arena-mana-confirm: 需重测；mana-lifecycle-confirm: 需重测；mana-spent-effects: 需重测；multipart-cost-boundaries: 需重测；mixed-tap-costs: 需重测；mana-source-refunds: 需重测；convoke-generic-icon: 需重测；component-card-face: 需重测；template-card-faces: 需重测；stack-exile-cost: 需重测 |
| 非普通法术力费用 | 未在本次片段确认 | 0/203 PASS | selection: 需重测；tap-payment: 需重测；library-costs: 需重测；cost-interactions: 需重测；variable-branches: 需重测；exert-branches: 需重测；nested-player-control: 需重测；multipart-cost-boundaries: 需重测；mixed-tap-costs: 需重测；convoke-generic-icon: 需重测；stack-exile-cost: 需重测；stack-restore-list-increment: 需重测；reveal-cost-rollback-increment: 需重测；behold-exile-increment: 需重测 |
| 选择、重选与确认目标 | 04:42、36:40、40:20 | 0/41 PASS | target: 需重测；stack: 需重测；advanced: 需重测；stack-list: 需重测；target-branches: 需重测；ability-text: 需重测；trigger-order: 需重测；storm-copy: 需重测；royal-state: 需重测；royal-ability: 需重测；ring-combat: 需重测；stack-restore-list-increment: 需重测 |
| 跨区域选牌与区域检视 | 50:56 | 0/133 PASS | selection: 需重测；prepare: 需重测；digital: 需重测；library-costs: 需重测；damage-draw: 需重测；discover: 需重测；state-branches: 需重测；saga-chapters: 需重测；royal-state: 需重测；royal-ability: 需重测；ring-combat: 需重测；stack-count: 需重测；dungeon-paths: 需重测；dungeon-restrictions: 需重测；dungeon-completion: 需重测；multi-emblems: 需重测；nested-player-control: 需重测；search-boundaries: 需重测；multipart-cost-boundaries: 需重测；mixed-tap-costs: 需重测；zone-browser-boundaries: 需重测；component-card-face: 需重测；template-card-faces: 需重测；reveal-cost-rollback-increment: 需重测；behold-exile-increment: 需重测 |
| 展示、揭示与隐藏信息 | 未在本次片段确认 | 0/21 PASS | choices: 需重测；fra: 需重测；discover: 需重测；knowledge-lifecycle: 需重测；reveal-cost-rollback-increment: 需重测；behold-exile-increment: 需重测 |
| 检视、去向与顺序 | 未在本次片段确认 | 0/43 PASS | selection: 需重测；library-costs: 需重测；replacements: 需重测；damage-draw: 需重测；nested-player-control: 需重测；library-order-boundaries: 需重测；multipart-cost-boundaries: 需重测 |
| 触发堆叠与结算顺序 | 07:00、35:00、50:00 | 0/48 PASS | stack: 需重测；advanced: 需重测；ability-text: 需重测；trigger-order: 需重测；saga-chapters: 需重测；delayed-expiry: 需重测；storm-copy: 需重测；royal-state: 需重测；royal-ability: 需重测；ring-combat: 需重测；dungeon-restrictions: 需重测 |
| 咒语／异能堆叠、复制与反击 | 08:00、36:40 | 0/36 PASS | stack: 需重测；ability-text: 需重测；effect-source: 需重测；storm-copy: 需重测；royal-ability: 需重测；ring-combat: 需重测；dungeon-restrictions: 需重测；stack-exile-cost: 需重测；stack-restore-list-increment: 需重测 |
| 永久物／衍生物／复制的进入与叠放 | 03:20、24:00、38:40、47:40 | 0/24 PASS | fra: 需重测；empower: 需重测；replacements: 需重测；damage-draw: 需重测；combat-allocation: 需重测；delayed-expiry: 需重测；storm-copy: 需重测；group-safety: 需重测；noncreature-copies: 需重测；attachment-branches: 需重测 |
| 身材、关键词、指示物与持续状态 | 12:00、37:20、40:40、10-03 17:51、10-03 23:04 | 0/71 PASS | selection: 需重测；empower: 需重测；common: 需重测；replacements: 需重测；damage-draw: 需重测；state-branches: 需重测；exert-branches: 需重测；quantity-allocation: 需重测；player-status: 需重测；stack-count: 需重测；dungeon-restrictions: 需重测；multi-emblems: 需重测；continuous-state: 需重测；combat-keywords: 需重测 |
| 变形、背面、牌面变化与暗置 | 11:00 | 0/21 PASS | advanced: 需重测；selection: 需重测；opponent-hand: 需重测；saga-chapters: 需重测；facedown-lifecycle: 需重测；bending-lifecycle: 需重测 |
| 备法、跨区域许可与消耗 | 未在本次片段确认 | 0/16 PASS | prepare: 需重测；bending-lifecycle: 需重测；permission-lifecycle: 需重测 |
| 宣攻、防守者、阻挡与伤害 | 52:00、52:20 | 0/71 PASS | advanced: 需重测；combat-allocation: 需重测；exert-branches: 需重测；royal-state: 需重测；royal-ability: 需重测；ring-combat: 需重测；combat-keywords: 需重测；attack-bands: 需重测；combat-damage-boundaries: 需重测；combat-phase-boundaries: 需重测 |
| 玩家状态、徽记、延迟效果与辅助牌面 | 10-03 05:25、10-03 16:00、10-03 18:09、10-03 20:10 | 0/54 PASS | common: 需重测；player-status: 需重测；effect-source: 需重测；delayed-expiry: 需重测；royal-state: 需重测；royal-ability: 需重测；ring-combat: 需重测；dungeon-paths: 需重测；dungeon-completion: 需重测；multi-emblems: 需重测 |
| 控制权、玩家控制和对局结构 | 未在本次片段确认 | 0/8 PASS | player-control: 需重测；number-auction: 需重测；nested-player-control: 需重测 |
| 数字专属生成、寻找与永久修改 | 未在本次片段确认 | 0/10 PASS | digital: 需重测；state-branches: 需重测 |
| 区域转移、抽弃牌与生命变化 | 35:00、35:20、50:00 | 0/11 PASS | replacements: 需重测；damage-draw: 需重测；combat-allocation: 需重测；draw-boundaries: 需重测 |
| 灵气、武具与附着状态 | 36:40、37:20 | 0/16 PASS | combat-allocation: 需重测；group-safety: 需重测；attachment-branches: 需重测 |
| 掷骰、抛硬币与随机决定 | 未在本次片段确认 | 0/29 PASS | random: 需重测；random-boundaries: 需重测；mana-results: 需重测；dice-combinations: 需重测 |
| 由另一位玩家分堆并选择整堆 | 未在本次片段确认 | 0/25 PASS | piles: 需重测；pile-boundaries: 需重测 |
| 替代效果、来源与应用顺序 | 未在本次片段确认 | 0/70 PASS | replacements: 需重测；damage-draw: 需重测；quantity-allocation: 需重测；ability-text: 需重测；effect-source: 需重测；mana-spent-effects: 需重测；draw-boundaries: 需重测；multipart-cost-boundaries: 需重测；combat-damage-boundaries: 需重测 |

## 登记完整性

无失效场景引用。

## 已知接入限制

- 控制另一位玩家：已适配独立操作座位、私有手牌与控制归还；双方座位整回合、仅战斗控制的本轮结算与界面状态见上表。嵌套／相互控制、控制者离场及网络重连仍须专项回归。
- 召集／拼造／截水共用的输入已补适配；曾在真实施放中抛出未适配异常。当前场景与界面检查状态见上表，不能把修复本身当作所有费用边界通过。
- `order(... sideboardingMode=true ...)` 未适配；这项是局间备牌排序，不能误报成所有游戏内排序都未支持。
- 原生 Forge 联机的 `applyDelta/applyYieldUpdate` 未适配；项目自有快照/操作协议须另行验证，不能据此判定项目联机完全不支持。

其他异常分支的路径、行号和原文也保存在 families.json，新增分支会进入报告。

## 尚缺的边界

### 起手与调度

- 当前未完成事项由remaining-work.json中R02/S08唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 优先权、响应与等待

- 当前未完成事项由remaining-work.json中R02/R04唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 施放面、模式与异能选择

- 当前未完成事项由remaining-work.json中R03/R04/R47/R51/S02/S06/S07唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### X／数量／分配

- 当前未完成事项由remaining-work.json中R03/R47唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 法术力来源与支付

- 当前未完成事项由remaining-work.json中R09/R51唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 非普通法术力费用

- 当前未完成事项由remaining-work.json中R12/R51唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 选择、重选与确认目标

- 当前未完成事项由remaining-work.json中R02唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 跨区域选牌与区域检视

- 当前未完成事项由remaining-work.json中R12/S02/S03唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 展示、揭示与隐藏信息

- 当前未完成事项由remaining-work.json中R47唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 检视、去向与顺序

- 已完成的R12费用选择、R16排序与R36当前1v1终局合同保存原证据；当前旧生产者依赖合并仍由A06.01负责。原生modal真实网络时序归R02.02，存活多人归还/席位身份与模式支持归R37，不新增排序机制或重复语言矩阵。

### 触发堆叠与结算顺序

- 当前未完成事项由remaining-work.json中R18/R19/R49唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 咒语／异能堆叠、复制与反击

- R20/R41原复制身份、例外与附着消费者门槛已保存原编号及原证据归档；独立原生payload和Vanguard生产入口保留A01.01，已登记历史依赖与全局指纹审查保留A06.01。其他R02/R37/R47准确待办按remaining-work.json追踪，不重开已完成复制/附着游戏。

### 永久物／衍生物／复制的进入与叠放

- R19/R20/R40原具名对象生命周期消费者已按原编号及证据归档；S05/S07的齿轮/随机反馈支持决定仍原owner，独立payload/入口A01.01和当前历史依赖A06.01保持开放，不重开旧复制/移区游戏。

### 身材、关键词、指示物与持续状态

- R23/R39/R40/R49/S03已核销的原具名字段/效果门槛保留原编号与原证据；尚未完成的独立生成payload、声明与生产入口原A01–A05条目保持，各当前依赖审查原A06.01，不把即时数值刷新宣称通用数值动画。

### 变形、背面、牌面变化与暗置

- 当前未完成事项由remaining-work.json中R49/R51/S01唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 备法、跨区域许可与消耗

- 当前未完成事项由remaining-work.json中R49唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 宣攻、防守者、阻挡与伤害

- 当前未完成事项由remaining-work.json中R02/R23/R49唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 玩家状态、徽记、延迟效果与辅助牌面

- 当前未完成事项由remaining-work.json中R19/R32/R35/S04/S05唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 控制权、玩家控制和对局结构

- 当前未完成事项由remaining-work.json中R02/R03/R37/S08唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 数字专属生成、寻找与永久修改

- 当前未完成事项由remaining-work.json中R03/R38/R39/S02/S03唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 区域转移、抽弃牌与生命变化

- R40原同时移区、当前生命/数值、ghost中断恢复门槛已按原编号归档；既有结果通知的当前依赖审查保留A06.01，原生last16上限明确，R02真实网络/恢复及R47其他资源边界原owner保持，不新增未观察的任意突发测试。

### 灵气、武具与附着状态

- R20/R41原复制身份、例外与附着消费者门槛已保存原编号及原证据归档；独立原生payload和Vanguard生产入口保留A01.01，已登记历史依赖与全局指纹审查保留A06.01。其他R02/R37/R47准确待办按remaining-work.json追踪，不重开已完成复制/附着游戏。

### 掷骰、抛硬币与随机决定

- 当前未完成事项由remaining-work.json中R02/R03/R47/S04唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 由另一位玩家分堆并选择整堆

- 当前未完成事项由remaining-work.json中R02/R03唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

### 替代效果、来源与应用顺序

- 当前未完成事项由remaining-work.json中R03/R12/R18/R39/R47唯一追踪；逐项完成门槛与原证据复用见该表。早期范围段落仅保留在goal-scope整理-1008/before，不重新生成任务。

## 尚未归入共用流程的引擎操作

新增 API 会自动进入此表，避免只从已知关键词列表找缺口。下列引用数量不是机制总数。

| 操作 | 直接引用脚本数 | 代表牌 |
|---|---:|---|

每项规则/界面断言、证据状态、请求族与未适配分支见 [families.json](families.json)。
