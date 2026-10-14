# 特殊费用统一：逐项清单（2026-10-14）

## 具体数量

当前锁定 Forge 版本：**41 种特殊费用具体类**，另有普通法术力类 1 种、抽象基类 2 种。替代施放枚举 26 个、可选费用枚举 18 个属于入口标签，与费用类重叠，不能相加当作待测试数量。

按用户决定，固定费用保留直接支付；只统一实际需要选择的步骤。已知费用基础结果沿用旧批次身份，本表是逐项源码与共用合同审查，**不是 41 种牌各自新对局 PASS**。

## 41 种费用逐项

| 类别 | Forge 类型 | 处理 |
|---|---|---|
| 产生法术力 | CostAddMana | 固定／自动，无新增窗口 |
| 展示符合条件的牌 | CostBehold | 原生条件决定固定支付或选择；选择使用共用确认 |
| 展示并放逐 | CostBeholdExile | 原生条件决定固定支付或选择；选择使用共用确认 |
| 凋零 | CostBlight | 原生条件决定固定支付或选择；选择使用共用确认 |
| 选择颜色 | CostChooseColor | 原生条件决定固定支付或选择；选择使用共用确认 |
| 选择生物类别 | CostChooseCreatureType | 类别草稿＋右下下一步；原生没有取消时不伪造取消 |
| 搜证 | CostCollectEvidence | 原生条件决定固定支付或选择；选择使用共用确认 |
| 承受伤害 | CostDamage | 原生条件决定固定支付或选择；选择使用共用确认 |
| 弃牌 | CostDiscard | 原生条件决定固定支付或选择；选择使用共用确认 |
| 抓牌 | CostDraw | 原生条件决定固定支付或选择；选择使用共用确认 |
| 征召 | CostEnlist | 原生条件决定固定支付或选择；选择使用共用确认 |
| 耗竭 | CostExert | 原生条件决定固定支付或选择；选择使用共用确认 |
| 放逐牌 | CostExile | 原生条件决定固定支付或选择；选择使用共用确认 |
| 放逐堆叠咒语 | CostExileFromStack | 原生条件决定固定支付或选择；选择使用共用确认 |
| 将放逐牌移入坟墓场 | CostExiledMoveToGrave | 原生条件决定固定支付或选择；选择使用共用确认 |
| 掷硬币 | CostFlipCoin | 原生条件决定固定支付或选择；选择使用共用确认 |
| 觅食 | CostForage | 食品／坟场明确显示付款方式；仅食品可用时下一步／取消；之后选牌确认 |
| 获得操控权 | CostGainControl | 原生条件决定固定支付或选择；选择使用共用确认 |
| 让牌手获得生命 | CostGainLife | 原生条件决定固定支付或选择；选择使用共用确认 |
| 磨牌 | CostMill | 原生条件决定固定支付或选择；选择使用共用确认 |
| 支付能量 | CostPayEnergy | 原生条件决定固定支付或选择；选择使用共用确认 |
| 支付生命 | CostPayLife | 原生条件决定固定支付或选择；选择使用共用确认 |
| 支付魔法碎片 | CostPayShards | 原生条件决定固定支付或选择；选择使用共用确认 |
| 承诺赠礼 | CostPromiseGift | 原生条件决定固定支付或选择；选择使用共用确认 |
| 将牌放入牌库 | CostPutCardToLib | 原生条件决定固定支付或选择；选择使用共用确认 |
| 在牌上放指示物 | CostPutCounter | 原生条件决定固定支付或选择；选择使用共用确认 |
| 在牌手上放指示物 | CostPutCounterYou | 原生条件决定固定支付或选择；选择使用共用确认 |
| 跨牌选择移去指示物 | CostRemoveAnyCounter | 逐枚累计指示物、下一步／重选／原生取消 |
| 移去指示物 | CostRemoveCounter | 原生条件决定固定支付或选择；选择使用共用确认 |
| 回手 | CostReturn | 原生条件决定固定支付或选择；选择使用共用确认 |
| 展示牌 | CostReveal | 原生条件决定固定支付或选择；选择使用共用确认 |
| 展示已选择的牌 | CostRevealChosen | 固定／自动，无新增窗口 |
| 掷骰 | CostRollDice | 原生条件决定固定支付或选择；选择使用共用确认 |
| 牺牲 | CostSacrifice | 原生条件决定固定支付或选择；选择使用共用确认 |
| 横置来源 | CostTap | 固定／自动，无新增窗口 |
| 选择横置对象 | CostTapType | 原生条件决定固定支付或选择；选择使用共用确认 |
| 组队横置 | CostTeamwork | 原生条件决定固定支付或选择；选择使用共用确认 |
| 卸装 | CostUnattach | 原生条件决定固定支付或选择；选择使用共用确认 |
| 重置来源 | CostUntap | 固定／自动，无新增窗口 |
| 选择重置对象 | CostUntapType | 原生条件决定固定支付或选择；选择使用共用确认 |
| 御水 | CostWaterbend | 御水横置付款，共用下一步／取消 |

## 费用类外的入口

还核对：Convoke、Improvise、Delve、Offering、Emerge、Assist、Phyrexian / PayLifeInsteadOf、Harmonize、TapCreaturesForMana、Variable cost reduction / cost ordering、Spree / multikicker / X announcement。召集／拼造／御水走横置付款输入；掘穴数量和逐张放逐、献祭／化生选择、协力操作者与付款数值分别保留原生合同，均统一推进按钮。Phyrexian 的生命付款选项仍由原生合法性决定，不能用不可支付的替代费用绕过必须支付的费用。

替代施放标签：Awaken, BeamMeUp, Bestow, Blitz, Dash, Disturb, Emerge, Escape, Evoke, Flashback, Harmonize, Foretold, Freerunning, Impending, Madness, Mayhem, MTMtE, Mutate, Overload, Prowl, Plotted, Sneak, Spectacle, Surge, Warp, WebSlinging。

可选费用标签：Bargain, Buyback, Entwine, Kicker1, Kicker2, PromiseGift, Retrace, Jumpstart, Offering, Teamwork, ReduceW, ReduceU, ReduceB, ReduceR, ReduceG, AltCost, Flash, Generic。

## 证据口径

本次新增效果断言、Unity 操作结果见同目录批次记录和 `.tools/cost-unification-1014/audit.json`。原解析／付款审计只按其有界范围复用；不存在把类名匹配、卡面中文或截图当作整牌效果通过的结论。逐项构造入口、继承的 visitor、输入类和源码指纹见 `.tools/cost-unification-1014/inventory.json`。

食品／坟场、蓝／绿、顶／底的 `cancel` 编号可能是另一个合法模式，只有原生真实取消可触发右键。施放者在掘穴／协力等自己的未完成付款选择中取消，会中止本次施放；协力的另一位付款者可以拒绝协力，不撤销已经完成的游戏操作。

本批不打包、不覆盖桌面、不部署服务端，不恢复旧 goal。

## 本批结果与边界

- 24 个有界原生流程完成：多目标的部分／零／满选、撤选及取消、分配伤害与牌手目标；多张弃牌／放逐、献祭／化生、掘穴及其草稿取消、生命／能量／非瑞、固定横置，以及指示物跨牌／类型／重选／取消恢复、觅食食品与坟场和仅食品分支。
- 所有流程仍由原生引擎选项、真实支付与结算产生结果；选择前的安排夹具不计效果。旧成本证明按具体范围保留，不因为运行包总指纹改变重做。
- 固定费用不新增确认；需要选择的步骤等待明确推进。多目标达到上限不自动提交，保留撤选与原生取消。所选数量不足时推进按钮由原生门槛禁用。
- 列表／数值／类型选择只有原生存在实际取消入口时才提供取消并响应右键；必选输入不伪造取消。顶／底、蓝／绿、觅食的坟场方式等序数不会被误当取消。
- Unity 的原生请求重放、操作、数值布局适配器与食品单选重试分开记录；不是所有卡牌费用组合、所有参数数值和所有语言的穷举运行。
- 本地仅更新桥接与三个原生界面类，保留已有补丁。桌面客户端和服务器保持原包；未打包／覆盖／部署。覆盖清单只分类刷新，没有 `--run`，旧 goal 与核销统计不改。

最终 Unity 结果：58 个去重断言标签，保留原前缀、控制尾段、实际操作者／排版补段、类型／数值补段与仅食品接口检查；不同标签不代表不同效果或卡牌。当前图集见 `.tools/cost-unification-1014/multiple-targets-2.png`、`multiple-many-targets-6.png` 和付款提示图。
