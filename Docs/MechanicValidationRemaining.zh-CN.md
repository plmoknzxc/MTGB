# 机制验证剩余工作与增量回归计划

日期：2026-10-12。此报告是现有清单的去重规划；本次盘点没有运行规则或 Unity 效果测试。

目标范围、验收与用户继续授权/工具状态见 [goal 修订记录](MechanicValidationGoal.zh-CN.md)。接续任务时核对其更新日期；本报告不会修改系统 goal 状态。

## 当前逐项工作清单

当前 goal 有 **1 项待办**：0 项待补具体证据、1 项待源码/旧证据审查、0 项待展示/范围收尾。另有 3 整项由用户后置，未验证，不计当前待办。原 81 项中 77 项已按各自范围证据核销；9 个开放包用于归属追踪。

**这些是整理后的工作事项，不是测试次数或剩余轮数。** 完整事项编号、待补内容、已有证据和逐项结束条件见 [当前工作清单](MechanicValidationWorklist.zh-CN.md)，机器源为 skill 的 `references/remaining-work.json`。

已完成的冻结来源归类沿用原证明，当前只对齐受影响旧证据并列出缺失结果与UI合同差异。审查项不能未经分类直接转成新对局。新增可达分支必须记录源码或请求依据、旧证明不足的原因、唯一归属和事项净变化；数字、标签变体及任意组合不自动扩大目标。

## 工作包与排期归属

当前整理为 **9 个开放工作包**：3 个按共用流程补边界，0 个需要独立复核的特殊表现/模式，6 个清单及工具审查包。

审查后的真实缺口按上述变更规则更新具体事项；特殊包先检查复用可能性，尚未认定都需要新 UI 或用户设计。

同一取消恢复、操作玩家、网络时序、隐藏信息和文本边界在多个流程出现时合并排期；不同项目接入分支及本项目补丁影响保留独立结算断言；未修改Forge内部组合不自动追加验证。当前活动边界与历史完成范围分开保存，关联来源不代表整牌已验证。

计划来源状态：`SOURCE_SNAPSHOT_MATCHES`。

## 实际剩余工作量

已明确必须新增的运行：**0 条**；尚待集中核对的共用合同：**1 类**。零条已确认补测不表示零未知项或 goal 已完成。

逐卡关联数量仅作证据追溯，不计待办、不用于估时，也不要求归零才能完成。先集中比较旧证明；只有明确未覆盖的请求、权限、字段或生命周期分支进入补测表。

| 共用合同核对 | 状态 | 差异或取消依据 |
|---|---|---|
| 选择值与后续用途 | 已复用共同合同 | 只比较新读取字段、操作者／可见性及保存清理时点；名称、颜色、类别窗口和已验证下游结果共用。 |
| 生成对象与施放权限 | 取消新增验证，复用已有接入证据 | 逐卡比较生成异能的整链结果、重复次数与每个来源离场组合；因未逐卡施放而追加场景：已取消。Garth 实际生成并支付6施放；旧期限许可实际施放、取消、到期、来源独立/依赖与私有可见性；生成链复制/免费施放和清理。 |
| 事件与延迟生命周期 | 取消新增验证，复用已有接入证据 | 为每种延迟 Execute 重新验证牺牲→LKI→反身触发→伤害的完整组合：已取消。Kardur 实际先牺牲熊再选择反身3伤害目标；Forum 两个 Immediate 触发独立记忆及附着；批量离场后的真实力量记忆；13条延迟条件/时间戳/来源失效/自然到期流程及N61三条明确尾段。 |
| 剩余声明的接入差异筛查 | 待比较旧结果 | Screen the retained choice/generated inventory for project-facing request shape, actor/visibility, source/LKI identity, authorization and next-request recovery differences. Batch same contracts; do not inspect every full native effect chain or replay every card. |

旧实际结果及尚未逐卡证明的范围完整保留；本次修正规划，没有新增效果 PASS。

## 当前优先范围与后续记录

当前排期 **1 项**；排除在当前 goal 外的整项 **3 项**，编号：R37.03、R02.04、A06.01。另有 3 个模式专属分支及 7 个完整输入排除在当前 goal 外。原始记录保留；排除不等于关闭或验证通过。

当前goal完成普通1v1。指挥官/生产四人、完整子游戏、Planechase、Vanguard、Archenemy、征召笔记/密谋牌后置，原编号/断言/证据保留scope-exclusions.json；游乐设施和装置辅助牌组继续接入，普通共用规则和BO3仍保留。

选择一组可共享开局/消费者的多个缺失结果，在既有夹具集中补断言；先核对旧证据，仅缺失断言执行。驱动按结果边界保存检查点，失败仅续失败差异；共用UI/语言取代表，批末一次登记。只剩单组可执行或本组修复阻塞时记录原因。

A01有 88 条顶层引用记录、86 个不同顶层ID；展开为 131 条引用，去重后 125 个具体输入；已核销 117 个，当前尚需完成 **2 个**，后置且未验证 6 个；原始未核销合计 8 个。这是输入账数，包含补证据和支持门槛，不是净新增测试数或剩余轮数。

下表保留原执行块，状态按各块完成证明读取；不把已完成块重新排期。

| 执行块 | 内容 | 原输入／事项 |
|---|---|---|
| N01 | 普通横置／重置共享夹具（本轮完成） | api-flow-TapAll、api-flow-TapOrUntap、api-flow-TapOrUntapAll、api-flow-Untap |
| N02 | 指示物增减、移动与批量清除 | api-state-RemoveCounterAll、api-choice-RemoveCounter、api-choice-MoveCounter、api-choice-AddOrRemoveCounter、api-choice-TimeTravel |
| N03 | 双人真实联网时序与恢复合并准备 | R02.02、R02.03 |
| N04 | 再生与销毁共享同一战斗流程 | api-flow-Regenerate、api-flow-Regeneration |
| N05 | 批量消灭／牺牲与记忆子效果 | api-result-DestroyAll、api-result-SacrificeAll |
| N06 | 当前对象、战斗与附着状态共享夹具 | api-state-Detain、api-state-HealDamage、api-flow-RemoveFromCombat、api-flow-Unattach |
| N07 | 强制阻挡、成为被阻挡与战斗关系 | api-flow-MustBlock、api-flow-ChangeCombatants、api-flow-BecomesBlocked、api-flow-Block |
| N08 | 生命设定、生命与攻防交换 | api-result-SetLife、api-result-ExchangeLife、api-result-ExchangeLifeVariant、api-result-ExchangePower |
| N09 | 囤兵、续战、探查与探察 | api-result-Amass、api-result-Endure、api-result-Investigate、api-result-Explore |
| N10 | 刺探、筹谋、显化惧象与温习 | api-result-ManifestDread、api-result-Learn、api-result-Surveil、api-result-Connive |
| N11 | 条件分支、修改X与旧方向证明归并 | api-state-Branch、api-state-ChangeX、api-flow-ChooseDirection |
| N12 | 私有查看与跃离身份／返回／限制 | api-flow-LookAt、api-result-Phases |
| N13 | 奇偶选择／直接展示手牌／原来源记忆清理 | api-flow-ChooseEvenOdd、api-result-Reveal、api-result-Cleanup |
| N14 | 玩家选择／控制权交换与原来源字段清理 | api-flow-ChoosePlayer、api-result-ExchangeControl、api-result-Cleanup |
| N15 | 清理类别／延迟触发／清理前记录 | api-result-Cleanup、type-state-selector-deltas |
| N16 | 清理前结果日志接入现有通知 | api-result-Cleanup |
| N17 | 颜色与类别选择原结果和值选择接入 | color-candidate-count-deltas、type-state-selector-deltas |
| N18 | 类别选择剩余原结果及制作/空候选修复 | type-state-selector-deltas |
| N19 | 翻面与盖牌守卫及旧类别状态证明归并 | type-state-selector-deltas |
| N20 | 多组控制权交换、堆叠咒语控制与朋友敌人分组 | api-result-ExchangeControlVariant、api-result-ControlSpell、api-result-AssignGroup |
| N21 | 均衡舍弃牺牲、互斗原身份与比拼公开顶牌 | api-result-Balance、api-choice-Fight、api-choice-Clash |
| N22 | 分类检视、清空法术力与掷落结果 | digmultiple-category-results、digmultiple-rest-order、drain-clear-retained-and-all-players、drain-remembered-delayed-number、drain-transfer-source-and-spending、flip-empty-and-real-outcomes |
| N23 | 法术力限制、生命周期和产费替代尾段 | combat-pool-first-end-before-extra、conditional-no-counter-not-permanent、early-end-combat-pool-cleanup、lose-mana-color-conversion-provenance、mana-burn-cleared-only、nonartifact-ban-allows-ability-cost、piracy-extra-restriction-and-source-permission、production-replacement-type-amount-choice、restricted-mana-colored-two-hybrid-arm、restricted-static-operation-flags、static-retention-color-all-expiry |
| N24 | 特征基线、待机御火与取消费用 | cda-effective-baseline、cda-non-pt-baseline、other-public-hidden-restrictions、api-state-PermanentNoncreature、suspend-special-action-last-counter、firebending-own-attacks-and-live-amount、ordinary-snapshot-pending-spell-history、mana-trigger-versus-local-snapshot、restricted-snow-mana-snapshot-identity、queue-ownership-and-during-payment-writers |
| N25 | 重复保存值、秘密选择与卡名 | stored-minus-times-repeat-results、repeat-stop-and-reset-consumers、reveal-chosen-payloads、reveal-type-sacrifice-tax、reveal-player-tax、legacy-no-admittance-payload、name-set-and-additional-names |
| N26 | 层与文本、房间和咒语书、复制原证明对齐 | incubate-token-counter-transform、copy-spell-and-token-replacement-delta、shared-copy-layer-mapping、api-choice-AnimateAll、api-choice-ExchangeTextBox、change-text-word-flow、makecard-random-spellbook-exhaustion、room-effect-face-selection |
| N27 | 实际ChooseCard父链及双方选择约束 | api-choice-ChooseCard |
| N28 | 原属性生命周期及真实事件消费者 | api-choice-AlterAttribute、name-predicate-selector-deltas |
| N29 | 原名称条件/计算费用/列表耗尽 | name-predicate-selector-deltas |
| N30 | 原景点领奖阈值与辅助开牌增量 | claim-prize-threshold-payload、open-and-visit-attractions |
| N31 | 融合原件身份与复用原件预览 | meld-linked-native-objects |
| N32 | 真实AWOL当前物理对象移除 | api-state-RemoveFromGame |
| N33 | 原生翻牌公开结果与正确命中高亮 | native-flip-simulation-feedback |
| N34 | 卡恩真实同局重开与共享状态清理 | karn-same-game-reset、restart-and-child-shared-mutable-state |
| N35 | 现有BO3真实换备与双局绑定 | S08.02 |
| N36 | 特化死亡链与四原payload组牌面归类 | A01.01 |
| N37 | 特化11原牌面结果与任意颜色费用候选 | A01.01 |
| N38 | 特化13原根结果、蓝色后继修复及免费施放展示 | A01.01 |
| N39 | 特化剩余21原根结果和52根原组闭合 | A01.01 |
| N40 | 原生成链实际结果及赠礼条件修复 | A01.01 |
| N41 | 原21生成链剩11根结果与公开卡图闭合 | A01.01 |
| N42 | 两处原生成延迟消费者与准确旧声明复用 | A01.01 |
| N43 | 移出比赛两来源本局结果与跨局/通知实际缺口 | A01.01 |
| N44 | 独立选择三消费者与九旧命名父链增量 | A01.01 |
| N45 | 公开分区投影与战场标识 | S06.02 |
| N46 | 比赛内移除与BO3有效卡池 | A01.01 |
| N47 | 赌注模式与本场BO3归属交换 | A01.01 |
| N48-scope | 普通1v1范围明确与特殊模式后置 | A01.01 |
| N48-entry | 普通1v1辅助牌组正式注册 | A01.01、S04.02、S05.02 |
| N48-editor | 普通1v1辅助牌组编辑入口 | A01.01、S04.02、S05.02 |
| N49 | 装置生命周期与公开组别 | A01.01 |
| N50 | 普通游乐设施造访和精确灯号 | A01.01 |
| N62 | 集中合同核对，停止逐卡核销排期 | independent-choice-payloads、generated-and-event-payloads |
| N64 | 取消无项目接入差异的整链验证 | A01.01 |
| N65 | 有界接入差异筛查，保留已验证共同合同 | A01.01 |

推荐块先对齐旧证据；上述记录数不代表需要新运行的数量。

当前排除范围及卡牌提示见 [特殊模式与子游戏后续记录](MechanicValidationExcluded.zh-CN.md)。游乐设施与装置仍按普通1v1接入；后置模式的入口与专属效果不作为本次完成门槛。

## 合并后的执行批次

9 条开放边界统一排入 **2 个执行批次**，另指定 **6 类共用检查**的负责批次。每条边界恰有一个排期归属；批次数不是剩余轮数，合并不关闭任何未验证边界。

| 批次 | 内容 | 对应开放包 | 执行重点 |
|---|---|---|---|
| E01 | 消费者分类与证据依赖 | A01、A02、A03、A04、A05、A06、R49 | A01.01有界接入差异筛查：保留共享复用，确认是否仍有新请求/权限/隐藏信息/LKI身份/恢复分支；不逐卡重跑，补测数量未确定。 |
| E02 | 权限、请求恢复与轮序 | R02、R37 | 当前剩余R02.04与R37.03均由用户后置；本批无当前执行门槛。外部机器/容量交用户自行测试，旧本地UTP/权限/恢复证明和原编号保留；不自动重跑支付/轮序/语言UI。 |

共用检查按相同消费者/合同引用证据；不同支付资源、控制权、隐藏信息和生命周期继续单独取证。

| 共用检查 | 负责批次 | 可引用已有证据的条件 | 需要新增证明的条件 |
|---|---|---|---|
| 操作权限与请求版本 | E02 | 同一桥接处理分支、操作/可见席位与请求版本消费合同；记录具体消费者及证据身份。 | 新路由/字段/消费者，控制权切换或隐藏信息策略变化；真实网络延迟/乱序/重连须有联网证据。 |
| 支付取消与资源恢复 | E02 | 相同支付阶段、资源类型和撤销实现；引用已有实际部分支付后取消的证据。 | 弃牌/牺牲/放逐/横置/生命等原生资源或不可撤销节点不同、多段/嵌套选择；不同原生取消语义分别断言。 |
| 选项数量、身份与确认 | E01 | 同一控件和提交分支、操作/可见席位、身份类型、数量/顺序、确认/取消门槛及窗口生命周期相同；核对新原生输入，仅实例/数值变化可引用已有真实点击证据，无需每个规则case重放。 | 零候选/失效目标/分配/排序等合同不同、新直接提交路径或窗口生命周期；相同控件不代替不同规则结果。 |
| 私有与已知区域信息 | E01 | 相同原生可见性、知识记忆与浏览消费者；引用具体席位/区域/期限的证明。 | 暗置私有身份、窃取候选、多方控制或知识清除生命周期不同；只读预览不能证明选择权限。 |
| 语言、字号与图片布局 | E01 | 同一文字/图片消费者、控件与资源/格式路径；新规则仅改变数值/实例且实际请求合同相同，可引用已有三语言证据，不为每个case重复三语言点击。 | 新文案/格式、字体/卡图资源、布局尺寸或消费者；受影响语言及长文本/首次呈现/切换边界补验。 |
| 折叠、展开与只读浏览 | E01 | 相同对象分组/身份与共用浏览消费者；数量压力和布局样本保留适配器身份。 | 对象状态/控制者/附着/生命周期影响分组或新的选择路径；独立对象身份与实际规则结果分别证明。 |

## 已完成部分的工作量核销

相较暂停时的完整基线，已存档 **552 个新增实际规则场景**与 **41 套新增 Unity 定向检查**。这些列出的断言不再属于未做首次验证；后续相关改动仍可能需要回归。规则场景与 UI 套不能相加当成机制数量。

本次在 **6 个开放包**中核销相应边界，保留其余待办；大包仍开放不表示里面的工作全部没做，包数无法表示包内工作量下降。

另外有 **56 个有界工作包**已保存完整原始行及原生/UI证据后关闭；从开放队列移除不会删除其证据或扩大为所有卡牌组合通过。

原始边界、已完成范围、原生 case ID、证据 SHA-256 和剩余边界保存在 skill 的 `references/verified-progress.json`，同时输出到 remaining-plan.json。这是首次验证进度账，不是当前版本依赖账本；不改写需重测或合并全量 PASS。

| 包 | 已核销的范围 | 仍需完成 |
|---|---|---|
| A01 | 十个旧驱动源码分类：1服务器、4投影适配器、1原生输入合同、4历史效果；哈希变化自动重开复核，不增加规则/UI首验数；四历史效果驱动原证据/登记重叠已复核，原生末态独立核对；执行身份缺失保留历史未版本化证明，不新增当前PASS；Companion可选开局取消空对象阻塞已修复，接受/拒绝及普通开局3真实流程恢复首个维持优先权，原命令区辅助效果接受有/拒绝无；付费回手、其他限制和UI仍开放（companion-entry-1009/audit.json）。；Companion三费命令区回手/部分付款取消退款重试及无堆叠结果已核销，命令区无点击入口已修复，三语言真实请求射线与移区标记刷新、只读/过期/重复提交、接收方及观察者范围完成；现有放逐入口局部重放通过。其他限制/子对局/A01原结果保持开放（companion-payment-1009/audit.json）。；Assist他人分担/选择0/协助方部分付款取消/施放者取消的双方资源，以及只有协助方能支付的入口与原堆叠保留，5具体原生结果已核销；协助席位的数值/分组地精确选择/只读/取消和施放者恢复UI合同补验（assist-payment-1009/audit.json），未泛化所有支付互动。 两真实原生流程、8付款操作（5施放/3转化）、24法术力、938驱动检查及51离线精确断言：Season+Dawn产生四个40指示物正面；Solemnity+第二Dawn再产生四个零指示物正面，旧40不变；旧146转化40/40并无重复激活选项，新152转化0/0被状态动作删除，七个准确同伴保留。Incubob129造132，付2转化同132为Dark Confidant3/2，1指示物保留；自然下次维持触发、双方94/95分别只读确认原顶130 Air Elemental，入手并失去5生命，记忆对象清空；正常抓原次顶21，只有130继续对手公开，21仍私密。47去重当前UI合同检查，原失败有效14/13前缀加28尾段减8重复；原FAIL不改写。准确确认按钮射线提交原编号/重复拒绝/非接收者不能确认/旧预览清理/两视图最终手牌和生命核对，背面当前异能正文实际mesh。 A01原stored-loyalty-before-damage与soul-burn-black-x-subset补齐：同一真实对局两施放13mana，Drain Life X4全黑对原Jace138忠诚2，伤害过程中原Limit=2，原生damage history4、实际回血2、原138入坟墓场；Soul Burn136实际7mana（三黑二红二蓝），原X部分严格二黑二红、基础2U+B分开，目标原Player2受4伤害，Limit保存伤害前40，实际只回血2；双方最终44/36，正常返回优先权。340驱动检查、25冻结断言；主语言最大字号四真实费用阶段18UI检查，四个BR准确图标及自动支付实际射线原ordinal。 A01原joint-payment-different-payload补齐：Collective Voyage真实双方各选五张原Basic入场且横置；Minds Aglow两原副本分别2+取消3只抓2，以及2+3抓5，逐一核对原牌库顺序与手牌ID；Shared Trauma双方磨5张原ID。Mana-Charged Dragon真实宣攻2+3变10/5造成10伤害，原同一场上对象累加器归零；正常回合结束恢复5/5，下次双方0只造成5，无旧值残留。两局有效边界四咒语九已付贡献26mana，取消不扣资源；52冻结结果断言，龙尾段490驱动检查，找地两席位实际射线和选五张确认合计14去重UI检查。 A01原stored-first-cast-time完成：实际First Contact145第一张已付2，立即Static保存X1；响应Giant Growth147付1使原Bear148变5/5，游戏计数2而原145仍X1。四原牌21/56/18/11实际磨入坟墓场，选择原21回手并由对手公开确认，原其余手牌隐藏；继续结算生命40→42，记忆清空。第二原First Contact146付2保存X3，磨原42/34/49/17，拒绝取牌，全入坟墓场且生命仍42，真实优先权返回；3施放5mana、176驱动/32冻结断言。实际坟墓场+放逐区二选提示与列表指示漏翻译各补精确两行，三语言最大字号/新布局与原选牌、确认、公开确认、拒绝和清理43去重UI检查。 A01原stored-etb-life-payment补齐：Minion实际付6mana/7life成7/7；Nameless Race付4mana/4life且原对手两白色非衍生永久物+两白色坟墓牌仅许可0..4，成4/4；Processor付4mana/5life，随后真实4T生成一原5/5衍生物，保存值随后续生命变化不改写。原零支付驱动FAIL的完整付6mana/0life→0韧性SBA进入坟墓场→空栈优先权结果独立恢复，不重跑零分支。两真实流程5付费操作24mana，原失败保留；实际数字按钮射线/原ordinal/0下限/4上限/必选无取消、三语言最大字号确认与清理通过。 A01原stored-last-known-mana-value-and-life的Soulgorger Orgg半组完成：实际进场20→1保存19；Revitalize实际付2升至4并抓准确原顶；Unsummon付1将原对象回手，离场仍回复19至23，原同伴/私有手牌/双方牌库与空栈优先权核对。3完成施放8mana、27冻结准确断言；旧FAIL保留，无新交互合同/UI套/编号。 A01原stored-last-known-mana-value-and-life完整核销：保留Orgg实际保存/回复生命证明，Transmute四同局UU与独立无神器UU实际完成。牺牲MV3→找MV2/3免差费；MV2→4准确支付2入场且Disk横置；MV2→5拒绝差额3，原所选牌入坟墓场。清空牺牲记忆前保存LKI，搜索时记忆已空而保存值仍正确；无神器实际-1不搜索/不洗牌。5完成施放12mana、350驱动/70冻结准确断言。 A01原rhystic-target-controller-payment核销：同一真实对局4张Lightning+两灵气6施放17mana，五次实际控制者付款9mana。对原Player2支付2受2伤/拒绝受4伤；对原对手Wurm支付2受2伤、自有Wurm拒绝受4伤，付款座位准确，实际伤害事件与原SpellAbility RhysticPaid为1/0。Errant/Power Leak实际附着原对手生物/结界；三个原对手维持分别选1/2、2/3拒绝、0/0，原生命34→33→31→27，每次PaidAmount归零，原卡/私有抽牌/两方mana来源及空栈优先权核对。907驱动/103冻结准确断言。 A01原stored-noted-power-life-and-cost三卡组核销：Dragonborn实际+2再Growth到7、Fling牺牲LKI7/伤害7/boon7，原来源回手2不改变7，第一Bear9/2永久保留并回手重施一次、第二Bear2/2；保留原FAIL并冻结9操作15mana完成前缀。修复生成赐福的正数门槛后，实际正2/零0/负-2牺牲及下一生物7操作11mana通过。Splendor实际施放记录40，四个己方维持等40抓/低38不抓但更新38/等38再抓/升42抓，白色Revitalize触发+1再+3；三施放7mana及原手牌/牌库身份核对。重平衡Muse实际装备4、攻击并响应Growth到7、结算记录7，再移走装备变5而记录仍7，首BlazeX6仅R/下一BlazeX6为6R、一次效果移除，6操作12mana；修复Attacker取值及Calculate解析。窄桥接回归三操作4mana证实真实boon2/减费7向双方导出。合计5流程含1完成前缀、28成功付费操作49mana、564冻结断言及115UI展示检查。 珊瑚盔统领原等级组：实际UU施放前缀；同一场上对象四次真实升级1，等级0/1=2/2、2/3=3/3飞行、4=4/4飞行且仅己方其他人鱼+1/+1。实际GrowthG成为7/7，基攻防仍4/4。独立初始化4级尾段实际Hexmage牺牲移除全部等级，同ID恢复2/2、飞行和他人人鱼加成撤销，再真实Growth5/5。最后独立Growth尾段自然清理恢复2/2，对手回合不能升级；原占栈时SorcerySpeed不可激活检查保留。 四个原API最低消费者结果完成：两Turnabout各一次整组二选，Hidden Strings逐张相反决定并拒绝编码；真实Blinding/Trumpet记忆1伤和清理，Deeproot两新横置仅一聚合衍生物；两Cloud选0/2，White Plume对手维持必须1，Gond替代实际Gate进场无普通Untaps，Cloud+Orb正常两重置准确磨原两顶。 四个原输入最低消费者结果完成：Dust两区域TIME数量与真实延缓续问；Aether全部类型/实际衍生物放逐、Temporal只活动方HOURGLASS全部；任选类型/多牌/零停止与下游抓失生命、玩家、共享3预算、Ashling移除记忆伤害与Amy放逐自动单选；移动All/Any/零/来源汇集/多接收者/EachNotOn/NonZero与来源实际移除、目的倍增、Goldberry真实抓1；三次时间旅行重建/空选/最后TIME免费施放。 Original AddOrRemoveCounter minimum now complete: existing chosen type add/remove and EachExisting reuse; actual Guile empty auto-add; original optional Yes/No at both triggered players, later sacrifice/damage; forced receive-only/remove-only/neither and type-specific Melira restriction; Shape true/false RemoveCondition across real attack/upkeep; Guile removal remembered child real4 damage and cleanup. Five actual paid native flows: two shields/damaged attacker/heal/tap/remove combat and one-use; original zone/timestamp/unused EOT expiry; Mossbridge direct repeat and actual NoRegen; Debt additional child control/owner; shared mass shield consumes one object and forgets last moved object. Original producer/draft/source IDs and both public/private permissions preserved. Six actual native flows: shielded/indestructible versus actual destroyed memory and life; mass NoRegen active shield; exact optional equipment decline/accept with natural control restore; targeted player mass destroy remembers all original powers including survivor after original11loyalty parent leaves; opponent CantSacrifice filtering versus own sacrifice, exact4 owned4/4 tokens and cleanup; real Vault paid exile then Defined Self imprint gates discard/return/leave-trigger and cleanup. Original Detain3prohibitions/natural actor expiry and child draw; direct Disarm2 Equipment remain battlefield unattached and empty repeat. Wolverine old2/new3 damage versus Pyramids Destroy replacement clears2/one-shot and unused natural EOT expiry. Plain blocker removal retains blocked/no damage; False Orders explicit unblock real3; Reconnaissance removes attacker; Gambit exact remembered ID/untap/extra combat defender restriction/cleanup. Actual new-object detain reset on Unsummon and paid recast. MustBlock defender chooser, actual target, BlockAllDefined two original attacker IDs, rejected mandatory confirmation with same phase then valid pairs, natural UntilEndOfCombat expiry on surviving Wall. Portal reselect from actual player to Chandra with original queued Ulamog trigger retained/updated, original20 top IDs exiled and10 damage to planeswalker; Planetar per-attacker decline/accept and split actual damage. Fog first/repeat with no real blockers preserves one Bushido, exact4 trample damage; genuine unblock/reblock still produces second Bushido. Actual Noble Ox ETB creates blocked flag and original pair, one Bushido and actual2/3 combat damage; public current relations and continued operation. Original SetLife fixed plus optional0/2 redistribution, legal saved multiset under CantGainLife/CantLoseLife, duplicate40 values consumed once each. ExchangeLife unequal/two targets, one-target actual own-loss draw and cleanup, prohibitions no half exchange, Archive real doubled gain and post-exchange difference token. Actual LifeLostAll downstream Ob counter/exact top exile for fixed, exchange and variant. Life/PT power and toughness use modified net snapshot, set new base and retain ordinary bonuses, natural bonus expiry/repeated exchange, CantChangeLife guard and actual source bounce no-op. Serene Master actual blocked creature net exchange with different modifiers, exact shared end-combat expiry and real4/6 damage on surviving creatures. Original Prankster perpetual reciprocal base record and real recipient zone/recast reuse R39 proof without rerun. Amass actual no-Army token/counter doubling, exact one of two Armies, remembered power mills original eight IDs, Zombie retains type/adds Orc and invisible effect removed on leave. Endure actual counter and Spirit choices under Doubling Season, source-gone fallback produces exact two3/3 Spirits without confirmation. Investigate optional accepted/declined by original opponent, no declined token/event plus exact loss, repeated Num own events distinct from token doubling, original Clue real sacrifice/payment/draw and memory cleanup. Explore actual public land identity/known hand, empty top still counter and no invented reveal, original gone creature still explores without counter; historical kept/milled nonland/public top/draw lifetime proof retained. Learn original minimum complete: real Lesson/discard/decline/Phoenix replacement and real Mindslaver-controlled permission, exact IDs/resources/zone/order/private reveal/next request. Manifest distinct tops/private face and Cage prohibition completed partial evidence. Original Surveil split/order/first-repeat/empty/static+2 accept-decline; original Connive multiple order/nonland-land/current-object/third-draw timing/Leader replacement/no-discard; Manifest one-zero watcher and actual Priest replacement event/result, reused original distinct/private/Cage. Original Branch minimum: real Anticognition exact7/8 grave threshold, original creature stack target, extra2 accepted/refused and actual true-child Scry order. Original ChangeX minimum: real XX Hangarback announced2->effective4 via Unbound,0->0 natural SBA death,Glava2->5 and optional decline2;original mana4/0 remains unchanged and same stack ID updates. Original ChooseDirection minimum reconciled to existing five native four-seat ordered/simultaneous/empty left-right flows and three lifecycle results, original enum/recipient order and actor/view evidence retained; no reruns. Original LookAt and Phases minimums: actual receiver-specific hand/top/face-down, empty chain, repeated/reverse-seat look and old object leaving;original permanent/attachments retain IDs/zone/timestamps with no zone events, normal return, Oubliette held across real next untap then tapped return, CantPhaseOut actual expiry, CantPhaseIn blocked toggle and source leave, AllValid toggle and real AnyNumber0/2. Bridge-only authorized face projection fixed; current actual Poll/165 Unity checks prove exact localized original identity artwork, private receiver/waiter, original ray ordinal/duplicates, original phased tiles disappear/return. Native Odd/Even enum before real zone reset and exact both-player MV0/even/odd exile; direct RevealEffect AnyNumber2/0, Optional accept/decline, empty/filter skip, random opposing actor/two receivers, announcedX3 exact clamped2 opponent hand, RevealAllValid2 and real chosen perpetual children, RevealDefined land extra draw/nonland damage; exact IDs/paid mana/known hand/private remaining draw/next priority.196 current Poll/rays/duplicate/receiver and localized parity at1.3 checks independent. ChoosePlayer actual optional acceptance/decline, remembered two-player damage and source removal, secret native/view suppression and actual revealing exile, random previous-player exclusion/no-candidate tap cleanup, generated Siege protector and two real tokens. ExchangeControl original pair ownership/timestamps, missing member response, optional decline and active CantGainControl all-or-nothing guard. Selective remembered players repeat, chosen color repeat and reverse activator pool, preclear random named identities matching X0 twice/X1 once, real coin tails/damage and zero-damage repeated final heads. Actual payment/source identities and next priority; current Poll/receiver/original ordinal ray consumers and affected three-locale text independent. Actual same-source Bear/Cat both players and Land/Nonland repeated combat clear fields while source stays, random eighty original land type assignments, selective delayed trigger removal on source leave/control exchange retaining unrelated source and no stale trigger after real recast. Actual native preclear Faerie card log and eight-action Whimsy repeated remembered/card/player logs preserve identities after clearing; real payment observed before random untap. Old Knowledge Vault exact imprinted-condition return/clear result reused at original runtime. Current type selectors fix wrong ability-card routing, actual Poll/search/page/original ordinal/other-seat/duplicate checks. Complete original bounded api-result-Cleanup by combining N13/N14/N15 native field/child/repeat/imprint/delayed proofs with native preclear log delivery: Faerie one card result, Whimsy five remembered/card/player results; both public views same native IDs/order/source. Exact native results/request sequences unchanged, existing mixed result queue/deadline/dedup across both seats and three locales1.3; six actual native player-value nickname adapters and exact plural/empty formatter checks. No history button/new decision. N17颜色原输入完整：私有手牌零色静默、单色候选低于上限准确0..1、两/三色候选0..2选0/2、排蓝1..1、任选1/全部5、纯随机无UI及原生保存/防护结果；13真实完整/冻结前缀流程25末态，15完成付费28mana。类型部分：定义类别Note去重/空候选自动、两自然章节ChooseNoted排除旧类及实际counter、Volo准确原顶抓1、Type2双方基本地永久转换与Vision临时转换/自然到期。两确切地类别问句复用值列表/既有类别词汇，5基本类别完整显示与18类别最后项可达。 N18类别选择补证：Shared真实制作两原件/交集Artifact与Creature/进场横置/存类别/免费一次再正常4费；MostPrevalent只取对手牌库Bear/Cat并列、Note与清理、实际战斗死亡加1及可选零目标；AtRandom取对手真实全牌集合Bear/Giant、对应原牌消灭与另一原牌保留、纯地空类别无虚构问句且正常续行；Secretly真实三token、内部Human不进公共CardView/无提前通知、真实牺牲揭示后对手通知/三counter/4伤害/临时死触自然到期。6完整/准确旧FAIL结果流程19末态，11完成费用操作25mana、146独立检查；81局部UI检查。 原type-state-selector-deltas非特化SetState最低差异完成：真实复制翻面仅一次、两原SVar消费清理；Cloudshift同ID重入旧触发不改新对象；RevealFirst合法生物拒绝/接受、非生物过滤及公开原ID，直接/合变非永久组件翻正拒绝；Cyberman实际类型/攻防覆盖与翻正还原，含DFC合变不能盖牌；真实夜间入场ETB翻背面；Choices准确两原盖牌择一翻正。8真实完整/准确FAIL恢复流程24末态19费用38mana、524独立检查。普通Transform/Flip/Morph与Specialize支持/状态投影复用原R20/S02准确证明；N17/N18类别差异合并，不重跑旧成功。 原api-result-ExchangeControlVariant/ControlSpell/AssignGroup最低输入闭合：Cultural Exchange异大小2/3候选选2/1/0且第二组严格同数，原对象/控制权与真实共有控制时间戳；Aethersnatch窃取瞬间原stack/ability/card编号保留、实际操作者转移并改选目标产生准确3伤，窃取生物新控制者进场；Perplexing交换接受拒绝与真实响应先移走来源、原咒语仍由原操作者结算；Pirs Whim三组友敌组合准确原地横置/真实洗牌与原神器牺牲，每玩家一次分组、子记忆仅本组/不泄漏。11实际完整或raw FAIL独立恢复流程11末态14已付操作48mana、1207独立检查与207相关UI。 均衡四真实付费分支的原计数/舍弃牺牲/替代及触发时点；互斗真实非战斗同步伤害、替代变化/原双对象/可选拒绝/自然可达自身互斗及Fight事件；比点赢/平/对手赢/单空/双空、原公开顶牌/双方置顶置底/原事件与子结果。当前桥单牌留顶保留已有公开知识，私有/置底/洗牌不泄露；三语言1.3准确原控制提交及完整结果文字。 N22 closes only six existing A01 original minimum inputs: DigMultiple category overlap/empty retry/extra destinations/ChangeLater/sequential privacy and actual remaining-card bottom order; Drain retained and all-player pool removal, remembered delayed count, original mana provenance/restrictions/actual spending; Flip empty/nonflip/miss/exact hit/destroy/damage/tap/prevention child outcomes and restored next input.21 completed native flows/37 staged endpoints/27 paid78mana;2paid6mana original defect failures separate. No new goal IDs or all-producer/full-current-runtime PASS. N23原11个最低输入完成：静态操作限制、非神器限制对异能、Boseiju条件不可反击、双色单边和Piracy来源许可；提前结束/额外战斗的法术力清空、Horizon/Omnath/Ozai转换、Yurlok仅实际清空燃烧及保留效果到期；原七个命名产费替代来源的类型/数量/顺序/选择。23冻结完整流程或前缀、127完整端点、45成功付款86mana。旧已成功结果独立复用，驱动原失败不改写。 N24原10输入最低范围：CDA攻防/原生类型颜色基线、公开不能阻挡及非生物永久物替代入场；待机实际特殊动作/末指示物/可选施放/目标/费用/控制权与御火自身攻击/实时数量；普通费用取消的堆叠历史、法术力触发/隐藏状态、雪境限制单位及队列归属。成功旧结算与未变控件复用，原FAIL前缀与独立适配器各自保留。 N25原7输入：Chaos倍率筹码、Spark非零骰值保存/匹配禁用、Strategy有限额外重复；Eureka/Hypergenesis整轮拒绝及同宿主重置、Delina高低停止与临时复制期限、Helm真实磨入/生物/X/空库/替代去向；四个秘密选择独立结果及两种费用取消；No Admittance伤害/Jace、覆写/附加/选择卡名及原始状态。独立断言共享真实流程，原FAIL前缀/适配器/UI各自保留。 N26关闭7原A01输入：AnimateAll组筛选/继承产费/普通boost/实际到期；ExchangeWords与预先改词、后续Green→Red实际目标和触发、新对象/来源离场；MakeCard X0/X14随机咒语书重复抽取；房间0/1/2锁定候选、门身份与完整后续交互；复制替代真实非空Remembered及旧复制层原结果逐字段复用。保留原FAIL与其有效前缀，各阶段独立登记。 N27完成原api-choice-ChooseCard最低输入：力量总和含负值/零选择与停止、不同力量刷新及真实回合结束、逐力量分组、基本地类别、双方自己的冒险团、秘密控制/非控制选择及集体展示。复用R36原搜寻权限/取消证明。修正集体行动只选择自己控制的生物，并补选择提示三语翻译。 原AlterAttribute最低输入的普通1v1部分：驾驭、谋划、侦结、疑犯、乘驾真实付费/撤销/自然期限，三个真实事件消费者保留Card及Player身份；Prepared源牌/副本/效果三ID、施放一次消耗及清理，取消/控制权/离场沿用旧实际证据。补齐公开属性字段、状态分组、蓝色状态标记及详情。 原NameCard名称条件最低输入完成：保留34原分组，静态合法候选与旧13个名称效果/defined-split-empty原证明单列复用。新真实非基本地/AND/OR、传奇及计算MV、实际Equipped/Imprinted费用、固定随机逗号牌名、M1/2/3与X3、Nissa X12全11候选排除/耗尽、Garth同宿主六次选择及最后选择/重置/空列表。13完整或准确冻结前缀、29末态、18实付80mana及10零mana费用操作；204相关UI/27状态，三正式语言1.3、原ordinal/射线/等待/过期/重复/清除。修原Garth耗尽后继续复制旧名字的Play子分支，新增唯一39th native override；原55856运行文件字节不变。 原景点领奖阈值输入完成：4真实历史14末态、14实付共56mana；同来源低于/达到6 Luck、正常1及奖励2 Treasure、准确Junkyard/补开一个/空辅助牌库、Gamer事件Card与Player及目标消灭、静态奖励先于普通触发队列。开景点2/不足1/空0为另一原输入部分证据。原FAIL及有效前缀保留，未支付后续操作不计。262相关UI检查含169原交互沿用与93展示差异；修英文确认及长名/提示布局。 N31完成原meld-linked-native-objects普通1v1最低输入与S01.02融合展示：真实原件链接/单场上对象、原件选择、正常离场/重定向/库顶库底及坟墓场排序、攻击米斯拉、克撒7→9忠诚、缺伙伴/非自身所有/衍生物/实际Clone/禁止部分放逐、合成特征LKI及单次死亡触发；现有详情按钮打开两原件只读列表、完整融合图和真实拆分清理。 N32原api-state-RemoveFromGame最低输入：真实AWOL对准确攻击生物付3，先放逐后从所有原生区域移除；真实Unsummon付1响应使目标回手，AWOL不再放逐/移除，双方公开数量/身份及下一优先权一致。修复只在RemoveFromGameEffect读取当前物理对象后ceaseToExist。 N34完成原卡恩同局重开及共享可变状态最低输入的普通1v1部分：真实三类放逐/忠诚支付、临时复制与延迟牺牲、真实1/1蓝色蛙修改恢复；同Game/同session重新调度、发动者先手、双方隐藏手牌与生命/指示物/日夜/君主/魔戒/Command清理。原生重开事件递增视图revision、清知识/旧选择缓存，Unity清旧详情/区域窗口/分页/通知并提供双方短通知和原下一输入。 | A01.01有界接入差异筛查：保留共享复用，确认是否仍有新请求/权限/隐藏信息/LKI身份/恢复分支；不逐卡重跑，补测数量未确定。 |
| A02 | Discover关联揭示/确认/施放消费者及九条原生/一套UI；Empower关联衍生物/忠诚选择并补四场原生回归，历史UI保持原身份；BlankLine源码确认仅返回换行且继承空resolve，分类不授予效果PASS。 Recruit/Blight/Intensify原生消费者、原请求及十条规则/一套UI已登记，保留不同控制/替代/复制等组合。 余下StoreSVar/ChooseSector/ClaimThePrize/FlipOntoBattlefield按准确源码/调用/脚本链初步归族，零新效果或UI PASS；空间字段及原生模拟差异具体落入S06/S07。 七类StoreSVar代表实际结果九场：躁狂一/双献祭，X黑色伤害/生命上限，放逐MV记录和跨回合重置，双方秘密选择与实际新异能，CountSVar动态2+3付款与双方衍生物，EachPlayer三轮累计/停止/清除。三语言实际原请求UI和服务器投影适配器分别保存。 | A02.01原112API入口/消费者/证据定位审查关闭。实际原生结果/权限/恢复原输入保留并转A01共用核销；支持/特殊展示继续R49/S，字段与依赖继续A03/A06。A02.02原StoreSVar分类及历史证明身份不变；不代表112API效果/UI/联网通过。 |
| A06 | DiscoverConsumerAudit记录本轮两个UI生产分支、实际消费者、源/输入哈希、运行包、原生与UI/历史/回归身份、原失败与中断、选择复用与未覆盖范围；仅本批记录，不宣称全项目自动依赖合并。 StateBranchConsumerAudit记录新增强度协议/渲染/分组和两种固定提示的影响；十场新旧运行包20完整投影比较，保留历史与新UI及中断批次。 VariableBranchConsumerAudit区分九场新原生、三语言UI/生产Poll投影适配器、JsonUtility空DTO及嵌套协程驱动误报、结算值与换区重置；没有产品或运行包变化，其他任务不重跑。 TargetBranchConsumerAudit九场原生及一套UI，窗口枚举修复范围为targetOrigin/arrow；生产者版本、失败与真实输入冻结，规则/卡资料/运行包未变，仅补新增分支及失败UI。 ExertBranchConsumerAudit六原生/一完整UI/双攻击者增量，新增战场卡字段与已知集合用途、原生门槛/隐藏继承、构建结束及不同生产者哈希已审计。 NumberAllocationConsumerAudit记录11新原生/2三语言UI、运行包ChooseAnyNumber补丁及共同付款关联回归；数字/分配/竞价消费者、失败生产者及Int32适配器单独审计。 AbilityTextConsumerAudit记录共用卡面格式路径、实际目标/排序输入、未知文本适配器和ScrollRect常驻反例；没有新原生首验或运行包变化。 PlayerStatusConsumerAudit记录9原生/一套双方三语言状态重放、只读原生观察与已知状态文本／数值显示分支；失败生产者和辅助图键标点差异保留。 EffectSourceConsumerAudit记录四投影字段、publicMarkerSource/图片一致守卫、两条实际预防回归与一项八条件直接适配器；相关目标/旧排序中文增量和生产者哈希独立保存。 TriggerChapterConsumerAudit记录七条实际规则、两套三语言点击及排序相关两个UI回归；预览缺控制字段、JsonUtility空对象、图片偏好和手牌可查看/可提交合同差异保留。 DelayedExpiryConsumerAudit保存原生三路径与UI增量及不变消费者哈希，无新UI首验。 StormCopyConsumerAudit保存原生三路径与UI增量及不变消费者哈希，无新UI首验。 RoyalStateConsumerAudit保存六条实际规则与一套三语言状态首验；相邻白昼/极速回归抽样，多个真实驱动版本保留。 RoyalBoundaryConsumerAudit records original runtime, affected typed EFFECT projection, two current real regressions, one current old-draw first, direct adapter and actual three-language shared consumers. RingCombatConsumerAudit: seven actual first identities, three current regressions and new UI consumer; two historical runtimes and failed/OOM drivers retained. DungeonPath/StackCount audits:13 actual first/one current native regression/four UI increments, 48 projection comparisons, exact source/driver/fixture/producer hashes and old/failed attempts retained; no full/network PASS. MultiEmblem audit: three actual native first/two preserved drivers, existing UI increment, two exact resource links/decoded images and subtype importer regression, producer/input/source hashes, failures preserved; no unrelated native repeat/full/network PASS. NestedPlayerControl audit records two frozen native driver identities, original failed single-choice assertion/empty scene/wrong projection UI attempts, real request/operator contracts and unchanged production consumers; exact A06 hashes, no full/network PASS. Network recovery frozen three-process BO1 proof, exact source/build/runtime/driver/log hashes and four queue saturation observations; no whole-network claim. Network queue exact one production configuration delta checked against frozen old source, build/runtime/package/driver hashes and dedicated+LAN evidence retained; no unrelated effects rerun. R36 controlled-concession audit retains historical partial/normal producers and new terminal seat2 projection proof. Full old native/lib inventory absent; old aggregate reconstruction mismatch explicitly retained in A06, not claimed byte-equivalent/global PASS. Controlled-search audit: exact3 changed effect classes, actual own/other-library semantic and UI deltas; portable manifest identity synchronized, resource/JRE entries retained without fresh audit. Producer classes/lib and portable manifest mapping preserved; global needs-retest remains. Player-counter increment: exact main bridge delta plus instruction-identical synthetic helper, C# mixed-entity footer only; all other classes/jars same as controlled-search inventory. Historical successful producers preserved, no global-current PASS. Multi-object replacement increment: test-only, all current classes/lib byte hashes equal every native producer, no runtime/UI/native production delta; earlier successful producers unchanged. Global-current stale state retained. R03 final increment:test-only four actual native flows/current321UI; unchanged production class/lib producer identities checked; exact old source guards and field dispatch kept. Global current certification remains open. R38 test-only three actual native results/current246UI, unchanged actual/current class/lib identities; early UI Play timing failures preserved with bounded steady-state proof, global dependency scope not closed. R38 final exact source-only test additions and six original actual producer identities/current331UI, source audit full363/560 retained; native conditional commander not product-mode certification. Closure validator incremental proof checks preserve first-validation counts. R39 original self-removal exact nine-paid/one-free native unchanged-runtime and current202UI input/action/derived-field refresh audit; global dependency scope remains. R39 current cost/color exact seven paid/actual resource and original reveal-private lifetime native unchanged-runtime plus current150UI cost-glyph/toggle audit; global dependencies remain. R39 current eight-family named field gate/actual11paid NewPT/null/types/keywords/cost-static native unchanged-runtime plus243UI original/current ray audit; global dependencies remain. R39 two original native copy/Moved producers20paid/402frozen407currentUI, actual engine LKI and independent recipient/delayed identity; previous three increments reused. First Play UI nonactor gate failure kept as unresolved startup risk despite stable retry, no global current/network PASS. Current locale load/search consumer fault/reload/stale generation114UI checks; source GamePreferences only changed, no global PASS. Current compact mana title/source measured layout181UI+7adapter, actual font first frame/view-return; native payment unchanged, no global PASS. Actual auxiliary numeric result/fallback increment78UI +2native flows/3paid operations, changed two result consumers bounded; no global PASS. Current captured vote/order/numeric purpose text increment with exact old native inputs; source-scoped text/indices/fonts/geometry and unknown fallback reviewed, no global PASS; interrupted Play startup risk retained. Long ResultToast scoped layout/scroll/read lifecycle increment reviewed; nested body reader tests current, unbuilt-controller driver failure0checks preserved; no global current PASS. Raw-picker header only271 current checks reviewed; known/readOnly branches retained equivalent prior geometry; original effect proof hashes unchanged. Editorhotreload ManaPool array/row failure and0-check Play exit retained; new driver requires Play/creates runtime controller; no global/startup PASS. Captured source/rule/header102 checks and actual TMP child verified; no native changes; Incubate/mode differences retained. 实际两流程/四施放/两次付2转化/18法术力/450驱动检查：Dawn已知20地造两个独立抚育20，转化同145为20/20且147仍正面；Exarch实际双X=0不造衍生物，双X=2费用5造两个抚育3。零地Dawn造两个零指示物非生物正面，付2转化其中一个后0/0状态动作删除，仅另一正面仍在、准确归还优先权。实际反面TMOM17无图已复现；按准确来源关联Incubator // Phyrexian双面，优先准确key，唯一来源配对才回退本地18/18_2，不在歧义来源猜图。171当前执行UI/图路径检查（非独立机制数）：双方原生封包经production Poll、同ID缓存转化、实际缩略图/详情mesh/攻防、零SBA清理、真实射线原145与重复门槛。旧失败驾驶器把富文本20/20和对手响应权限误判，冻结有效21/70前缀，只补缺失尾段75和图路径守卫5；重复21不计。 Current shared TokenArt route review frozen; full current fingerprint merge remains. 两真实原生流程、8付款操作（5施放/3转化）、24法术力、938驱动检查及51离线精确断言：Season+Dawn产生四个40指示物正面；Solemnity+第二Dawn再产生四个零指示物正面，旧40不变；旧146转化40/40并无重复激活选项，新152转化0/0被状态动作删除，七个准确同伴保留。Incubob129造132，付2转化同132为Dark Confidant3/2，1指示物保留；自然下次维持触发、双方94/95分别只读确认原顶130 Air Elemental，入手并失去5生命，记忆对象清空；正常抓原次顶21，只有130继续对手公开，21仍私密。47去重当前UI合同检查，原失败有效14/13前缀加28尾段减8重复；原FAIL不改写。准确确认按钮射线提交原编号/重复拒绝/非接收者不能确认/旧预览清理/两视图最终手牌和生命核对，背面当前异能正文实际mesh。 Actual result/evidence hash identity added; global current dependency merge not PASS. 分区选择严格限定分支与实际接收者/原ordinal/准确来源/首帧字号已核对，证据sector-native-1010；全局当前依赖合并未通过。 当前Bridge退出队列/已离场输入/注册playerSeat/终局canRespond差异及规范portable manifest登记，证据royal-multiplayer-1010；普通存活输入不变，四人展示与剩余权限投影未证明，全局需重测未改写。 原LosesGame/防输和GameDrawn的真实操作/请求/原ID及零胜者协议头/BO3组件差异证据loss-draw-native-1010；生产源码/运行包无变更，实际终局绑定与全局依赖不推定通过。 当前Bridge准确6字段注册身份差异与两个compiled classes/规范portable manifest闭包seat-projection-1010；旧真实规则/UI证明按字段结构/权限未改复用，实际多方控制归还/终局绑定保留；不伪造当前全局PASS。 control-return-1010对Bridge/Gui真实共享输入归还变更、保留原队列/future、当前2终局释放和资格guard/取消差异补证；4辅助class仅debug line变化，native jars/resources保持；旧付款/Order/语言合同复用，实际全会话终局绑定保持；不授当前全局PASS。 terminal-binding-1010当前双人终局源码、原生完整draw/win视图与EndRoomMatch/标准session/NGO本机个性化消息/客户端BO3最终绑定分层验收；0生产改动，不因旧成功证据标签或测试尾段重新付款，不授全局当前依赖/真实远程网络PASS。 turn-wheel-1010当前无生产变更原生轮序6真实流程分层验收、原input/paid/对象/阶段/继任身份与historical UI合同准确复用；2驱动FAIL冻结，只修失败尾段。四native4席位夹具只改3人数bounds，不授生产4或当前全局UI/依赖通过。 cast-cost-tail-1010无生产变更，4个原精确费用结果最小差异与当前不变选择器合同分层核对；保留原FAIL和accepted Harm成功前缀，仅补拒绝结果尾段。0登记新job/UI，原生4gift只改3bounds，不授生产4/全部语言布局/当前全局依赖。 本次只有TargetRestrictions新override，正向OptionalCost True target gate受影响；当前资源/bridge/lib和其他旧补丁不变。真实Hulk paid/unpaid及无神器控制完成，其他普通目标与负向共享根目标源码门槛保持。UI合同不变复用旧真实点击；全局旧2890状态仍保留，不授当前全量PASS。 Sarkhan仅新增三真实成本结果，生产来源不改；冻结paid原末态FAIL，因native token名不同独立恢复证据而不重施。准确Mixed Hand/Battlefield/取消与原source已BF仍同选择器合同；无候选无窗口。当前总体版本证据仍未知/保守，不提升全局PASS。 Caustic额外mana与Kindle三张Flashback新增2完成/2合法性负例，源/runtime不变，UI合同对齐原数量/混合区/取消/原ID与manaAuto不可用，未因数值3/源Graveyard重做同一控件矩阵。保持两个原完整成功末态FAIL，独立核对冻结原结果，只补不充足尾段；未授全量当前PASS。 复现并修复CostPayment手牌放逐成本取消后原绿色牌被追加到手牌末尾：沿已有CostBeholdExile手牌顺序快照，只为来自Hand的CostExile追加捕获；已付成本/不可逆牌库保护不变。增量编译仅CostPayment.class改变、24原补丁context验证一致。复现呼魂非mana文字费用挤在标题和卡图间，ModeCards按实际固定字号文字高度留空间，原native费用绿色手牌句子三语言显示，保留Spree+费用前缀。三语言默认/最大字号真实mesh/不遮标题卡图/原按钮ray序号/重复提交防护321尾段检查，加两个原成功前缀，原FAIL均保留。 Scoped native runtime delta and source/UI hashes retained in cast-cost-closure-1010; old identities remain original versions and current-global report remains needs-retest. Heist原私有随机/重复/权限消费原生结果补8操作22mana，旧Gonti规则/UI合同复用；仅精确提示文案改变，六locale/scale有效UI前缀和确认尾段组件证据保留，native/runtime未变，未全量重跑或改报告当前状态。 实际复现并修复ChooseColor不在可逆multipart快照：保存原chosenColors和Hand顺序；Specialize只有旧正反卡图/当前详情读原面：95准确官方关联面，主Hand-Stack/来源施放/分配绘图旁路修正，原物理牌详情保留；Gut self触发Player改ValidPlayer。准确生成器是SpellAbilityEffect.addSelfTrigger，旧CardFactoryUtil描述纠正但原owner范围不变。25 pinned补丁context和portable/class/hash一致，旧新bytecode留存；95面资源解码是数据检查0效果case。当前/原详情17，原CostOrder三语言max23，六locale-scale颜色有效前缀，实际Hand-Stack有效前缀和GY-Exile pile ray7，Gut ownTurn蓝色/消失7，新增own-END异能精确三语言max8；英文临时异能补CopyDetails精确句子。Heist完成组与通用临时外框/付款/列表按准确合同复用，不重跑同义八Heist或95面。 Native/UI source and runtime version deltas retained; old successful/failed reports stay original, no manual current-global PASS. A01原stored-loyalty-before-damage与soul-burn-black-x-subset补齐：同一真实对局两施放13mana，Drain Life X4全黑对原Jace138忠诚2，伤害过程中原Limit=2，原生damage history4、实际回血2、原138入坟墓场；Soul Burn136实际7mana（三黑二红二蓝），原X部分严格二黑二红、基础2U+B分开，目标原Player2受4伤害，Limit保存伤害前40，实际只回血2；双方最终44/36，正常返回优先权。340驱动检查、25冻结断言；主语言最大字号四真实费用阶段18UI检查，四个BR准确图标及自动支付实际射线原ordinal。 仅测试及证据修改，当前runtime cc104不变；原UI合同实例差异核对、BR费用格式18检查增量留审计，原失败与当前全局失效状态不改写。 A01原joint-payment-different-payload补齐：Collective Voyage真实双方各选五张原Basic入场且横置；Minds Aglow两原副本分别2+取消3只抓2，以及2+3抓5，逐一核对原牌库顺序与手牌ID；Shared Trauma双方磨5张原ID。Mana-Charged Dragon真实宣攻2+3变10/5造成10伤害，原同一场上对象累加器归零；正常回合结束恢复5/5，下次双方0只造成5，无旧值残留。两局有效边界四咒语九已付贡献26mana，取消不扣资源；52冻结结果断言，龙尾段490驱动检查，找地两席位实际射线和选五张确认合计14去重UI检查。 仅夹具及证据新增，生产runtime cc104不变；原数字/费用/目标/攻击合同复用，单补新找地结果，历史全局失效身份保留。 A01原stored-first-cast-time完成：实际First Contact145第一张已付2，立即Static保存X1；响应Giant Growth147付1使原Bear148变5/5，游戏计数2而原145仍X1。四原牌21/56/18/11实际磨入坟墓场，选择原21回手并由对手公开确认，原其余手牌隐藏；继续结算生命40→42，记忆清空。第二原First Contact146付2保存X3，磨原42/34/49/17，拒绝取牌，全入坟墓场且生命仍42，真实优先权返回；3施放5mana、176驱动/32冻结断言。实际坟墓场+放逐区二选提示与列表指示漏翻译各补精确两行，三语言最大字号/新布局与原选牌、确认、公开确认、拒绝和清理43去重UI检查。 仅两个精确UI翻译插入，原数字/费用/历史/目标/私有策略与桥接无变更，旧全局失效保持。 A01原stored-etb-life-payment补齐：Minion实际付6mana/7life成7/7；Nameless Race付4mana/4life且原对手两白色非衍生永久物+两白色坟墓牌仅许可0..4，成4/4；Processor付4mana/5life，随后真实4T生成一原5/5衍生物，保存值随后续生命变化不改写。原零支付驱动FAIL的完整付6mana/0life→0韧性SBA进入坟墓场→空栈优先权结果独立恢复，不重跑零分支。两真实流程5付费操作24mana，原失败保留；实际数字按钮射线/原ordinal/0下限/4上限/必选无取消、三语言最大字号确认与清理通过。 原生产代码/运行包不变，旧全局失效保持。 A01原stored-last-known-mana-value-and-life的Soulgorger Orgg半组完成：实际进场20→1保存19；Revitalize实际付2升至4并抓准确原顶；Unsummon付1将原对象回手，离场仍回复19至23，原同伴/私有手牌/双方牌库与空栈优先权核对。3完成施放8mana、27冻结准确断言；旧FAIL保留，无新交互合同/UI套/编号。 统一界面/动态提示翻译、参数保持与公开源规则绑定；2765键三语言、8295键/990格式检查和真实UI合同增量。只翻显示层，Forge英文消息/编号/运行包不改；旧全局UI回归失效仍保持。 A01原stored-last-known-mana-value-and-life完整核销：保留Orgg实际保存/回复生命证明，Transmute四同局UU与独立无神器UU实际完成。牺牲MV3→找MV2/3免差费；MV2→4准确支付2入场且Disk横置；MV2→5拒绝差额3，原所选牌入坟墓场。清空牺牲记忆前保存LKI，搜索时记忆已空而保存值仍正确；无神器实际-1不搜索/不洗牌。5完成施放12mana、350驱动/70冻结准确断言。 两新增共用翻译键/33真实UI增量。 A01原rhystic-target-controller-payment核销：同一真实对局4张Lightning+两灵气6施放17mana，五次实际控制者付款9mana。对原Player2支付2受2伤/拒绝受4伤；对原对手Wurm支付2受2伤、自有Wurm拒绝受4伤，付款座位准确，实际伤害事件与原SpellAbility RhysticPaid为1/0。Errant/Power Leak实际附着原对手生物/结界；三个原对手维持分别选1/2、2/3拒绝、0/0，原生命34→33→31→27，每次PaidAmount归零，原卡/私有抽牌/两方mana来源及空栈优先权核对。907驱动/103冻结准确断言。 本轮只追加付款数量来源提示/共用翻译54UI增量。 A01原stored-noted-power-life-and-cost三卡组核销：Dragonborn实际+2再Growth到7、Fling牺牲LKI7/伤害7/boon7，原来源回手2不改变7，第一Bear9/2永久保留并回手重施一次、第二Bear2/2；保留原FAIL并冻结9操作15mana完成前缀。修复生成赐福的正数门槛后，实际正2/零0/负-2牺牲及下一生物7操作11mana通过。Splendor实际施放记录40，四个己方维持等40抓/低38不抓但更新38/等38再抓/升42抓，白色Revitalize触发+1再+3；三施放7mana及原手牌/牌库身份核对。重平衡Muse实际装备4、攻击并响应Growth到7、结算记录7，再移走装备变5而记录仍7，首BlazeX6仅R/下一BlazeX6为6R、一次效果移除，6操作12mana；修复Attacker取值及Calculate解析。窄桥接回归三操作4mana证实真实boon2/减费7向双方导出。合计5流程含1完成前缀、28成功付费操作49mana、564冻结断言及115UI展示检查。 本轮实际修复两个规则资源及两种记录值详情/翻译增量。 | A06.02历史记录审查完成：68批增量/31原关闭包、99记录/314直接证据顶层哈希全符；43重复引用只保留别名，弱证明撤回/替代与失败/旧producer/未知完整保留。A06.01继续核对229总指纹失效项和552/41首验的当前实际依赖；59活路径变/缺与4缺顶层runtime、2卡图相对根未定不能填当前哈希冒充旧producer，不自动全重跑。见evidence-record-review-1008/classification.json，无当前全量/联网PASS。 N57用户明确后置本包剩余事项；原编号/原证明保留，不算通过。 |
| R02 | Real dedicated BO1 on localhost: both seats actual pending-opening-request disconnect/reconnect with unchanged request ID/view hash, authorized hidden hand projections, London mulligan and six-card opening, correct series winner. Existing consumer transport increment; no visual/full-network PASS. Shared transport queue saturation fixed:2048 slots reported by4 role reports,2 real BO1 dedicated/LAN runs and3 reconnects,zero observed queue-full/exception logs; protocol/Forge/UI unchanged. Ordinary load only. R02.02/03原最低边界完成：当前Windows真实dedicated服务器与两个standalone客户端经UTP，目标/宣攻/阻挡/受控私有选牌/分堆及部分付款等待共7次宽限内恢复，原worker、请求、双方私有hash及操作权限保留；旧match/真实曾有效native ID/旧及重复client ID、手动AUTO竞争和丢失确认后的inputPending恢复；付款完成、取消退款后重试、实际30秒双方断线平局及部分付款方超时判负，旧令牌/动作不能复活或重复计分。4实际施放+2激活共18mana，103冻结断言/43证据引用。真实超512KiB状态被丢弃已修：protocol18可选有界gzip，JSON/hash不变，小包原路径，非法压缩/16MiB展开/旧协议拒绝。仅本机真实传输，不授跨机器/全部网络或费用组合PASS。 | R02.01审查、R02.02真实传输竞争及类型等待恢复、R02.03实际部分付款和宽限结果已完成；仅R02.04跨机器验证环境/突发房间容量决定仍开放。原dedicated/LAN队列和起手证明保留，本次localhost不是远程WAN/全部网络PASS；真实付款资源代表为普通mana，其他原生资源合同复用R12/R36原证据。见network-boundaries-1010/audit.json。 N57用户明确后置本包剩余事项；原编号/原证明保留，不算通过。 |
| A04 | 原374未知声明中275来源分类，补回Visit后剩100；2732写法与实际原生解析器一致，13工具检查。只分类不授予效果/UI PASS，见DeclarationClassificationAudit-2026-10-07。 | A04.01有界源码维度审查完成：2016结构身份和100声明/1873原行保留，651/143/1222三批准确身份不重测。3条原声明入口/结果缺口完整交A01.01/R49.02；5附带字段及实际payload/条件/生命周期交已有A01/A02/A03/A06。原行仅作审计索引，不是额外待运行包；不是全机制/每次出现/UI/联网PASS。见parameter-consumer-review-1009/audit.json。 |
| R37 | R37.02原LosesGame消费者原生结果：实际Door原281/282两次10费激活，准确原生目标ID1对应注册席位2；第一被原白金天使285防输阻止，实际Naturalize283破坏同285后第二次SpellEffect/Door来源使2离场，全部原有牌清理，注册1/3/4继续操作。实际Divine284从手中8费施放，原2指示物→自然第4回合维持1→第7回合维持0，剩余1/3/4IntentionalDraw、原2保留SpellEffect、native Draw/winner0、无请求且canRespond全false。一次有效流程4付款操作/30法术力，35精确离线断言；原1757驱动检查不是机制数。 追加执行当前生产Poll/MatchOutcome/OnlineSeries的9个适配器检查：原生ended原报文的零胜者头被解释为平局，两初始化会话槽的旧请求/等待/提交标记清理，BO3平局次数+1但胜局数不变，下一局和原先后手选择者保留。此处仅终局头/清理/BO3组件；四原生最终view不绑定到现两席位会话，不是4客户端/Unity/真实网络/EndRoomMatch全流程证明。 原R37.02准确6个席位字段修复：实际非当前2认输后注册IDs0/1/2/3、存活0/2/3；原actorSeat/votingPlayerSeat/replacementAffectedPlayerSeat/controllingSeat/exertedBySeats/protectingPlayerSeat把第四位写成3，当前都按注册身份写4。原生属性与InputConfirm/发布上下文仅用于投影适配器，0施放/付款，38个精确字段/源差异/运行包断言。空目标、非玩家/非战役占位0不变；exert仍先筛选存活玩家。生产只改6个表达式，原等待/权限/终局/协议结构未变，旧FAIL不改写。 R37.02真实存活控制归还：两条原生流程，Mindslaver1实际付4/牺牲/控制原3，在Courser部分Forest支付后1真实离场；原native输入/Forest已付/剩余2保留，物理3新请求继续付完Courser3，Divination实际付3且Archive/Reflection准确抓原牌库顶8，其他原席位无新私有ID；另一原4实际Slaver付4/牺牲/控制3，第二Divination付3在真实replacementOrder等待中4离场，同future/原source/options交回3完成准确抓8及新私有权限/后继优先权。另当前完整生产分支中交回3取消原Courser，原牌回同手、所有原地退款、普通/雪境pool全0、生命不变/空栈/后继3优先权。只修Bridge请求物理收件人与wire身份，保留旧controller共享队列与callback；旧离场者原请求已拒绝。6项实际完成付款21mana，不计适配器为规则场景；64冻结精确断言、5资格guard适配分支，现有生产双人终局释放回归通过。 R37.02终局绑定增量：未改生产双人Bridge，实际Divine Intervention付8、两次自然维持2→1→0，两个原生IntentionalDraw/winner0完整终局；复用当前编译版付款中控制者认输winner2原报文。准确原生request+ended经当前标准OnlineMatchSession/Poll/UpdateForgeSessions/EndRoomMatch/Send及真正NGO本机消息分发，再到HandleClientMessage；两参与者与旁观者个性化最终view、原对象/私有手牌ID/计数/ownDeck权限、旧请求清理/只读终局、房间ready/scene/restore/grace清零、断线成员移除及session进程/目录释放通过。BO3原1:1/2平局→第3平局且下一局8/原chooser2→winner2达1:2整场结束，重复Poll不重复计分。52已成功平局前缀及49缺失胜局尾段当前组件断言，171离线准确字段断言；1新原生流程/1实际8费操作，旧成功付款/多人规则未重跑。 R37.01原轮序结果边界完成：6真实流程/18实际付费施放49法术力及8横置或横置放逐激活。Aeon反转与Topsy静态方向/反转阶段结合、移除恢复；额外回合后进先出、Savor仅该额外回合跳过重置及后续恢复、Final Fortune额外回合结束离场；Meditate单次/Eater两次跳过的原Command计数与到零移除；Fatigue步骤一次跳过、Empty City Ruse下一维持注册全回合战斗跳过且含新增战斗、Moment Silence一次使用/未使用到期、Fatespinner原选择与每次所选阶段跳过及后续恢复；Time Stop真实咒语放逐/非牌异能移除、清除伤害/临时效果；Obeka当前玩家2不同激活者1的拒绝/接受与清理后优先权；当前结束者离场后激活者3选新2原实体ID再确认；额外回合已排给4而4离场时跳过并归还正常2。原请求身份、实际资源、精准对象、持续期限及后继席位由当前native快照和完整trace断言。 | R37.01原反转/额外/跳过回合阶段和结束回合的原生轮序、期限/来源/后继权限已完成，R37.02原生命周期与当前双人终局绑定已完成；仅R37.03生产四人客户端/大厅/战场及指挥官支持范围仍待决定。NoSchemes模式专属原结果与入口义务完整保留A01.01 scheme-set-abandon-lifecycle，不授模式PASS；真实远程网络归R02、当前全局依赖归A06。见turn-wheel-1010/audit.json。 用户2026-10-11明确排除当前goal：R37.03生产四人/指挥官及专属边界保存scope-exclusions.json，不作为本次完成门槛。 |

已关闭的原始有界包：
- R06 雪境费用支付；审计 `Docs/ManaVariantCostAudit-2026-10-06.zh-CN.md`。
- R07 混合法术力与双色通用费用；审计 `Docs/ManaVariantCostAudit-2026-10-06.zh-CN.md`。
- R08 非瑞克西亚及替代生命支付；审计 `Docs/PhyrexianPaymentAudit-2026-10-06.zh-CN.md`。
- R48 发现和其他链式选择；审计 `Docs/DiscoverConsumerAudit-2026-10-06.zh-CN.md`。
- R13 零多目标及结算时失效；审计 `Docs/TargetBranchConsumerAudit-2026-10-06.zh-CN.md`。
- R14 改目标和目标间分配；审计 `Docs/TargetBranchConsumerAudit-2026-10-06.zh-CN.md`。
- R50 耗竭与下一次重置状态；审计 `Docs/ExertBranchConsumerAudit-2026-10-06.zh-CN.md`。
- R05 一般分配数量与生命竞价；审计 `Docs/NumberAllocationConsumerAudit-2026-10-06.zh-CN.md`。
- R25 昼夜与速度状态；审计 `Docs/PlayerStatusConsumerAudit-2026-10-06.zh-CN.md`。
- R33 魔戒战斗异能；审计 `Docs/RingCombatConsumerAudit-2026-10-06.zh-CN.md`。
- R34 其他地城路径；审计 `Docs/DungeonPathConsumerAudit-2026-10-06.zh-CN.md`。
- R45 抓牌步骤豁免空牌库与预防到期；审计 `Docs/DrawBoundaryConsumerAudit-2026-10-07.zh-CN.md`。
- R42 多骰修正和重掷多选；审计 `Docs/DiceCombinationConsumerAudit-2026-10-07.zh-CN.md`。
- R46 可选搜索与来源区域；审计 `Docs/SearchBoundaryConsumerAudit-2026-10-07.zh-CN.md`。
- R43 多堆预定义堆与取消；审计 `Docs/PileBoundaryConsumerAudit-2026-10-07.zh-CN.md`。
- R16 牌库插入与双区域顺序；审计 `Docs/LibraryOrderConsumerAudit-2026-10-07.zh-CN.md`。
- R01 起手底牌顺序与配置取消重入；审计 `Docs/OpeningBoundaryConsumerAudit-2026-10-07.zh-CN.md`。
- R11 多色与跨区域非普通费用；审计 `Docs/MixedTapCostConsumerAudit-2026-10-07.zh-CN.md`。
- R10 替代费用撤销与支付恢复；审计 `Docs/ManaSourceRefundConsumerAudit-2026-10-07.zh-CN.md`。
- R21 同名叠放展开收起；审计 `Docs/PermanentGroupingConsumerAudit-2026-10-07.zh-CN.md`。
- R22 非生物衍生物与复制布局；审计 `Docs/NoncreatureCopyConsumerAudit-2026-10-07.zh-CN.md`。
- R41 灵气武具费用与附着身份；审计 `Docs/AttachmentConsumerAudit-2026-10-07.zh-CN.md`。
- R24 献力类别与持续效果层次；审计 `Docs/ContinuousStateConsumerAudit-2026-10-07.zh-CN.md`。
- R26 暗置私有偷看与翻面生命周期；审计 `Docs/FaceDownLifecycleAudit-2026-10-07.zh-CN.md`。
- R27 运土与截气的状态和许可；审计 `Docs/BendingLifecycleAudit-2026-10-07.zh-CN.md`。
- R15 区域浏览与空候选筛选；审计 `Docs/ZoneBrowserBoundaryAudit-2026-10-07.zh-CN.md`。
- R28 跨区域许可期限与控制变化；审计 `Docs/PermissionLifecycleAudit-2026-10-07.zh-CN.md`。
- R17 揭示确认与短时隐藏信息；审计 `Docs/KnowledgeLifecycleAudit-2026-10-07.zh-CN.md`。
- R29 特殊战斗关键词结果；审计 `Docs/AttackBandConsumerAudit-2026-10-08.zh-CN.md`。
- R30 预防替代下的战斗分配；审计 `Docs/CombatDamageBoundaryAudit-2026-10-08.zh-CN.md`。
- R31 攻击防守者与额外战斗；审计 `Docs/MultiplayerRingAudit-2026-10-08.zh-CN.md`。
- R09 受限与无色法术力；审计 `.tools/opal-commander-1009/audit.json`。
- R12 多段费用部分取消及替代；审计 `.tools/library-event-1009/audit.json`。
- R19 延迟触发到期与来源离场；审计 `.tools/delayed-lifecycle-1009/audit.json`。
- R23 身材与获得关键词颜色；审计 `.tools/keyword-sources-1009/audit.json`。
- R35 多徽记延迟标记及悬停；审计 `.tools/marker-fields-1009/audit.json`。
- R18 同时触发与APNAP顺序；审计 `.tools/chapter-boundaries-1009/audit.json`。
- S03 强度征募与凋萎分支；审计 `.tools/recruit-blight-boundaries-1009/audit.json`。
- R04 复杂名称禁令与长选择文案；审计 `.tools/name-ban-union-1009/audit.json`。
- R36 嵌套控制与未完请求归还；审计 `.tools/controlled-search-1009/audit.json`。
- R44 玩家指示物与多对象替代；审计 `.tools/multi-object-replacements-1009/audit.json`。
- R03 多方依次选择与等待状态；审计 `.tools/r03-payload-1010/audit.json`。
- R38 多次数字生成与法术书跨回合；审计 `.tools/digital-identity-1010/closure-audit.json`。
- R39 永久修改移除与换区；审计 `.tools/perpetual-lifecycle-1010/closure-audit.json`。
- R20 堆叠复制身份与数量；审计 `.tools/keep-name-1010/closure-audit.json`。
- R40 同时区域转移与资源展示；审计 `.tools/zone-motion-1010/closure-audit.json`。
- R47 动态通知长文本与语言状态；审计 `.tools/incubate-native-1010/closure-audit.json`。
- R32 君主与先制争夺；审计 `.tools/royal-multiplayer-1010/audit.json`。
- R51 施放选择的缺样本路径与组合；审计 `.tools/cast-cost-closure-1010/audit.json`。
- S02 数字特化及窃取特殊信息；审计 `.tools/specialize-tail-1010/audit-v2.json`。
- S01 融合组合牌面；审计 `.tools/meld-tail-1011/closure-audit.json`。
- S07 物理翻牌模拟与领奖链；审计 `.tools/flip-feedback-1011/closure-audit.json`。
- S08 卡恩重开与局间备牌（子游戏另列）；审计 `.tools/bo3-sideboard-1011/audit.json`。
- S06 空间区域选择；审计 `.tools/sector-display-1012/audit.json`。
- S05 机巧装置装配与转动；审计 `.tools/auxiliary-lifecycle-1012/closure-audit.json`。
- S04 吸引力平面及混沌辅助；审计 `.tools/attraction-visit-1012/closure-audit.json`。

## 结构清单如何拆分

2582 个未登记条目拆为 **574 个机制/执行维度条目**和 **2008 个参数、标签、脚本模式及期限条目**。后者是查遗漏的索引，不能直接删掉或标成通过。分类互相重叠，仍不是独立机制数。

| 分类 | 未登记 | 有列出场景证据 | 其他状态 |
|---|---:|---:|---:|
| 其他脚本模式 | 116 | 0 | 0 |
| 异能词／待核对文本标签 | 599 | 0 | 0 |
| 引擎操作 | 52 | 0 | 139 |
| 持续异能类型 | 89 | 0 | 7 |
| 持续期限 | 27 | 0 | 0 |
| 替代事件 | 34 | 0 | 6 |
| 机制／效果条目 | 215 | 0 | 131 |
| 脚本参数 | 1266 | 0 | 18 |
| 触发事件 | 141 | 0 | 7 |
| 费用类型 | 43 | 0 | 10 |

另外保留 **0 个未归族 API、100 种未归类声明、2 个旧测试脚本**。它们是审查输入，与结构条目/工作包重叠，不能相加。

另外 **38 个未登记脚本**已按源码哈希分类为工具、投影适配器、输入合同或历史回归；完整范围/剩余保留在 JSON，源码变化重新进入复核队列。分类不增加效果场景或 PASS。

## 已有验证，按原批次保留

登记：421 个引擎任务，1108 个规则/起手场景，12 个适配器检查；161 个 Unity 套，25 类共用流程。登记不是自动通过。

清单对应引擎批次：`None`；当前清单状态：需重测 318，未登记效果测试 2582。
Unity 清单状态：需重测 161。

最近完整历史基线详见 [验证方法与证据入口](../.agents/skills/mtgb-mechanic-validation/references/verification-method.md)。691 张截图只属于该批次，不按多次重跑相加，也不算 691 个机制。

## 开放工作包索引

| ID | 类型 | 优先级 | 工作包 | 当前事项 |
|---|---|---|---|---|
| A01 | 审查/工具 | P0 | 复核未登记机制与已有测试 | A01.01有界接入差异筛查：保留共享复用，确认是否仍有新请求/权限/隐藏信息/LKI身份/恢复分支；不逐卡重跑，补测数量未确定。 |
| R02 | 共用流程 | P0 | 请求恢复与真实网络时序 | R02.01审查、R02.02真实传输竞争及类型等待恢复、R02.03实际部分付款和宽限结果已完成；仅R02.04跨机器验证环境/突发房间容量决定仍开放。原dedicated/LAN队列和起手证明保留，本次localhost不是远程WAN/全部网络PASS；真实付款资源代表为普通mana，其他原生资源合同复用R12/R36原证据。见network-boundaries-1010/audit.json。 N57用户明确后置本包剩余事项；原编号/原证明保留，不算通过。 |
| R37 | 共用流程 | P1 | 多方轮序与额外回合 | R37.01原反转/额外/跳过回合阶段和结束回合的原生轮序、期限/来源/后继权限已完成，R37.02原生命周期与当前双人终局绑定已完成；仅R37.03生产四人客户端/大厅/战场及指挥官支持范围仍待决定。NoSchemes模式专属原结果与入口义务完整保留A01.01 scheme-set-abandon-lifecycle，不授模式PASS；真实远程网络归R02、当前全局依赖归A06。见turn-wheel-1010/audit.json。 用户2026-10-11明确排除当前goal：R37.03生产四人/指挥官及专属边界保存scope-exclusions.json，不作为本次完成门槛。 |
| R49 | 共用流程 | P1 | 未登记机制族代表场景 | R49.01原命名族定位与R49.02原10引用/8身份接收审查完成。8项入口/支持范围/实际结果归A01、依赖归A06；全部原生产者/断言保留，无变体排除或效果通过。 |
| A02 | 审查/工具 | P0 | 复核无场景的引擎操作 | A02.01原112API入口/消费者/证据定位审查关闭。实际原生结果/权限/恢复原输入保留并转A01共用核销；支持/特殊展示继续R49/S，字段与依赖继续A03/A06。A02.02原StoreSVar分类及历史证明身份不变；不代表112API效果/UI/联网通过。 |
| A03 | 审查/工具 | P0 | 复核事件持续异能费用维度 | A03.01原313维度定位与A03.02原18字段组/8输入组合范围审查均已关闭。实际原生结果/恢复/知识/请求差异保留原输入转A01，当前依赖归A06，子游戏生命周期仍受S08支持决定约束。旧552/41首验保持，不代表所有维度/付款恢复/组合效果通过。 |
| A04 | 审查/工具 | P0 | 结构参数和声明归类 | A04.01有界源码维度审查完成：2016结构身份和100声明/1873原行保留，651/143/1222三批准确身份不重测。3条原声明入口/结果缺口完整交A01.01/R49.02；5附带字段及实际payload/条件/生命周期交已有A01/A02/A03/A06。原行仅作审计索引，不是额外待运行包；不是全机制/每次出现/UI/联网PASS。见parameter-consumer-review-1009/audit.json。 |
| A05 | 审查/工具 | P0 | 接口可达性与隐藏调用链 | A05.01/A05.02有界入口审查均完成：145控制器/105游戏/48基础接口、615已发现调用候选及14必选输入发布分支分类；外部原生网络/宏/回放与当前启动区分。特殊模式6行完整转A01.01/R49.02，效果/字段/时序及当前依赖归既有共享事项。原行保留作审计索引，不是额外待运行包。见interface-consumer-review-1008/classification.json；入口/偏好/模式变动按条件重开。 |
| A06 | 审查/工具 | P1 | 记录逐批依赖与证据复用依据 | A06.02历史记录审查完成：68批增量/31原关闭包、99记录/314直接证据顶层哈希全符；43重复引用只保留别名，弱证明撤回/替代与失败/旧producer/未知完整保留。A06.01继续核对229总指纹失效项和552/41首验的当前实际依赖；59活路径变/缺与4缺顶层runtime、2卡图相对根未定不能填当前哈希冒充旧producer，不自动全重跑。见evidence-record-review-1008/classification.json，无当前全量/联网PASS。 N57用户明确后置本包剩余事项；原编号/原证明保留，不算通过。 |

## 避免重复的执行顺序

1. 按推荐块合并多个普通1v1输入，沿用完成的来源归类；仅缺失断言新运行，A06随同批消费者集中登记。
2. E02双人真实传输时序与恢复共用一次环境准备；E12已有1v1入口优先，指挥官按明确后置政策保留。
3. 每轮跑新增/变更任务及实际相关消费者；局部 UI 修改沿用未变化原生输入，不重跑无关引擎。
4. 全量留给阶段收尾、核心路由变化、运行包升级或影响无法界定；当前工具总指纹失效状态保持真实。

清单归类按消费者/原生分支做一次；每个结构条目保留分类或分支引用，参数/标签数量不转成逐条游戏测试。UI按真实请求合同选择代表输入；相同控件、提交路径、席位/可见性、身份类型、数量/顺序、确认/取消与生命周期可引用已有点击证明，核对新输入差异。不为每个原生case重复UI或三语言检查；新合同、文案/资源/布局变化补受影响检查。

本报告只刷新盘点，**自动逐任务依赖计算及增量证据合并尚未实现**。计划要求先判断影响范围，未把现有工具改成自动复用 PASS。

完成门槛：选定范围内每个可达交互有准确可操作 UI，实际结算与适用恢复边界有证据，特殊表现/明确不支持的模式逐项登记；审查队列有结论。有限测试仍不证明全部卡牌组合。

机器报告：`Tools/Validation/Reports/MechanicCoverage/remaining-plan.json`；逐条原始状态与排期方向：`remaining-inventory.csv`。
