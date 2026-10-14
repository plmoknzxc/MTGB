# 数字选择、生命竞价与数量分配

本批核对 R05 原行：大数、强制数字、一般数量分配与生命竞价。共同付款和重复支付生命沿用此前具体证据，不把共同付款算作竞价。只关闭原行边界，其他原生效果、多人／控制、费用及多重替代组合仍由原工作包追踪。

## 实际原生结果

| 场景 | 真实操作与断言 |
|---|---|
| auction-zero | Illicit Auction 的两席均拒绝加价，施放者以零赢得准确目标，控制者改变、拥有者保持；无人失去生命，返回优先权 |
| auction-other | 对手加价5后双方拒绝，只有赢家失去5生命；准确目标及拥有者保持 |
| auction-caster | 对手加价5、施放者再加1，以6赢得目标；施放者生命30→24、对手保持30，拥有者不变 |
| hellion-large | Volcano Hellion 实际进场触发，选择123；真实两个不可预防伤害事件均为123，己方生命200→77、准确目标生物死去，返回优先权 |
| hellion-cancel | 同一可选整数请求取消，原生按零处理；无正伤害、无失去生命，目标生物存活，返回优先权 |
| bounded-number | Expel the Interlopers 保留明确0–10的强制11候选，选择2使准确2力量生物死去；没有新增取消或扩大上限 |
| counter-division | Blessings of Nature 的4个指示物分配为2/2；原生至少每目标1、减量／重置／确认、两个准确ID的指示物及攻防核对 |
| shield-division | Embolden 分配3/1，随后实际 Pyroclasm：熊的2伤害全部预防、Memnite仍受1而死去；独立伤害事件与优先权核对 |
| mana-combo | Cascading Cataracts 实际支付5并横置，分配2红3绿；原生零下限、减量／重置、准确法术力池核对 |
| mana-different | Firemind's Vessel 实际横置，分配1红1绿；每色上限1，不能重复同色，准确池核对（夹具初始摆放为未横置，不验证其进场横置） |
| shield-on-damage | Divine Deflection 实际施放X=3后 Pyroclasm，原生 divideShield 在伤害处理中请求；每对象最多其2伤害，分配2/1，熊存活、Memnite受1死去、向准确对手反射3，生命30→27 |

夹具只安排初始卡牌、生命和可用土地；后续施放／激活、支付、选择、伤害、指示物和控制变化均由 Forge 执行。StateBranchFixture 和 VariableBranchFixture 仅只读结果。两批独立结果位于 `.tools/number-auction-1006/native/results.json` 与 `.tools/quantity-allocation-1006/native/results.json`；每次尝试的原请求、视图、观察、生产者／夹具／运行包哈希保留。

## 原生缺口与修复范围

ChooseNumberEffect 对没有 Max 的 ChooseAnyNumber 默认上限99，真实选择123被拒绝。固定卡池的12处此类声明已按路径、原文、行号和哈希分类到 `.tools/number-auction-1006/any-number-consumers.json`，当前均无显式 Max／Random；这是声明分类，不授予12张卡全部效果PASS。

[当前综合规则](https://media.wizards.com/2026/downloads/MagicCompRules%2020260925.txt)107.1c允许任意非负整数。审核补丁 `choose-any-number-without-arbitrary-cap` 只将非随机且无 Max 的此分支默认值改为原生 Int32 最大值；显式上限及随机默认范围保留。实现仍使用 Int32，不宣称数学上无穷范围。便携构建完成后运行包为 `0d108e2e0e509891ac96066396390ed8fabfa55b3bb168dce4c188a3190e7f88`，9条审核补丁、13个覆盖类。真实共同付款 join-forces 在该包复验2+3产生每席5士兵；这是关联回归，首验不重复计数。未编辑卡牌资料／卡图／翻译文件。

一般分配的三个实际入口分别为已选目标的分配（指示物／预防，至少每目标1；既有伤害分配引用目标审计）、产费颜色组合（零下限、同色／异色限制）、伤害处理中预防分配（零下限、最多实际伤害）。共用 AssignAmount 不意味着这些语义相同，本批分别保留原生断言。多个来源、多重替代顺序及完整 APNAP 仍在 R18。

## Unity、语言与实际失败

数字UI重放真实生命竞价双方的加价／拒绝、强制1–9枚举、有限0–10选择、原生123和取消；检验准确索引、草稿确认、旧请求／重复／非操作席位、查看战场／返回、阶段／AUTO空间和三语言。十位数 Int32 最大值仅为格式适配器，不算实际该数值伤害或费用效果。数值宽时右下控件向左扩展，固定字号保持，避免缩字和遮挡确认。

竞价的已知固定确认／结果文案分别本地化，保留原生数值和席位；未知消息不按猜测改写。双行确认高度曾按默认 lineSpacing=0 测量，实际渲染被裁切；测量统一为正常字体及 lineSpacing=1。首轮控制器不存在是验收夹具未初始化，修正测试创建实际控制器，不将其算作产品缺失。

数量UI按本批五个真实请求验证上下限／总量确认、加减／重置／确认准确ID、不可操作席位及隐藏／恢复生命周期，三语言与卡图检查。产费颜色沿用 CardSymbolText 的现有五色图标，不显示原生枚举名；分配法术力／指示物／预防伤害的固定标题本地化。

第一次数量规则失败源于测试误读原生指示物字段和法术力池结构、尚未选完替代顺序就确认；没有因此修改原生结果。数量UI首轮用 TMP spriteCount 断言图标数量，实际为可见 Sprite 类型的 Unicode 字符而计数为零；改检查真实字符类型、资源、Unicode和可见网格，再做截图。旧失败及不同源码冻结，不回写为PASS。

数字／数量UI最终路径和输入／生产者哈希登记到 `references/number-allocation-consumer-review-1006.json`。真实射线点击验证客户端提交，原生规则任务验证引擎结果；本批没有真实网络双客户端证明。

## A06 与剩余边界

变更路径仅 NativePatches、LandChoices 的数字布局、Notices 的固定竞价文案／高度测量和 Allocation 的五色图标／固定标题；测试驱动新增，不改变协议或后续费用规则。已追踪 ChooseInteger 与 BridgeAllocation／PresentAllocation 的双向调用，并准确读取相关源；图世代与路径覆盖保存审计机器文件，metadata_changed 源码回退，排除的上游／.tools直接读取。

R05 的原行通过本批11个新原生场景、2套UI及原共同付款／重复生命证据核销。R09保留限制／特殊产费，R18保留多来源／多重替代／APNAP，R36保留控制／多人／网络合同，R47保留未知动态长文案／语言／资源；Volcano Hellion 触发详情仍见英文与裁切，作为 R47 具体下一项，不能用数字UI通过掩盖。Int32限制也显式保留，其他 ChooseAnyNumber 消费者的下游效果不自动通过。总指纹失效及其他历史结果状态继续保持。
