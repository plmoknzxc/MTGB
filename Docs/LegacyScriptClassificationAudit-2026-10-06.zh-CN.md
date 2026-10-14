# 旧脚本分类审查

日期：2026-10-06。完整读取当前未登记的十个 Test-*.py 驱动，核对安排状态与实际操作的夹具；代码图覆盖无记录缺口但元数据变化，按准确源码回退。这里只分类来源与断言，不执行旧脚本，也不升级任何规则或 UI PASS。

## 分类结果

1 个本地测试服务器、4 个投影/直接状态适配器、1 个原生输入合同、4 个含历史实际操作的回归脚本。详细范围与剩余审查如下：

### CardImageServer

类型：`fixture-service`。

Loopback HTTP fixture for download UI: 404/503 retry, slow/bad image payload. No rules runner or effects assertions.

待办：Keep for CardArtDownloadValidation; resource/download tests do not establish any mechanic.

### LandTypes

类型：`projection-adapter`。

Eight direct battlefield/static-layer setups: type/mana/ability projection, controller scope, supertypes and direct removal restoration. Effects are placed, not cast.

待办：Retain static-layer evidence as adapter; actual casts, payment/replacement combinations and shared layer consumers still require explicit rules cases.

### MarkerCoverage

类型：`projection-adapter`。

Direct delayed-trigger storage and Command marker setup; special-states flag seeds player counters and room/ring/day state. Both-viewer IDs/privacy assertions.

待办：Do not count storage paths or directly seeded states as actual delayed-effect/monarch/dungeon resolution. TemporaryCopies remains distinct actual-effect evidence.

### PermanentGrouping

类型：`projection-adapter`。

Direct six-category metadata setup, remembered-state exclusion, changed loyalty and battle protector distinctions. No card effect cast.

待办：Keep for grouping UI consumers; metadata or layout does not prove all permanent effects.

### ZoneBrowsing

类型：`projection-adapter`。

Direct zone setup verifies IDs, counts, printing keys and hidden hand/library/face-down exile projection.

待办：Does not prove effects that move/reveal cards, changing browse permissions or actual zone-button clicks.

### AutoPass

类型：`native-input-contract`。

Ten native priority setups verify canRespond and available actions, mulligan not answered; no offered spell/ability is resolved.

待办：Keep input-availability contract separate from actual response resolution, Unity AUTO policy and real network timing.

### Interactions

类型：`legacy-effect-regression`。

Mox Opal cancel/blue activation, evoke cancel/menu, actual Skateboard trigger target/reset/confirm and tapped Welding Jar. trigger-auto is optional, not default.

待办：Legacy classes path .tools/selection-tests/bridge may be absent/stale; historical source/proof versions and modern registered overlaps need reconciliation before migrating assertions. No current PASS granted.

### Scry

类型：`legacy-effect-regression`。

Four actual Preordain scry/reset/order/draw branches; fifth case is forced worker fatal/private-diagnostic handling, not a scry effect.

待办：Legacy output/runtime identities and registered selection overlap require proof review; preserve worker failure as separate contract assertion. No current PASS granted.

### ShockInteraction

类型：`legacy-effect-regression`。

Actual zero-land unaffordable Shock, land play, target/payment and life 20 to 18; also legacy priority/auto-pass behavior assertions.

待办：Older graveyard/string/auto-pass expectations and proof identity need review before reuse; not all targeting/payment/network behavior. No current PASS granted.

### RealityFracture

类型：`legacy-effect-regression`。

FRA/FRC instantiation count is data validation; separate actual No Admittance cast verifies life 20 to 17 and one Jace token.

待办：Instantiation of >200 cards does not establish their effects. Recover historical proof/runtime before registering the single effect assertion; no current PASS granted.

## 方法纠正与守卫

旧覆盖生成器把“未登记脚本”直接命名为“未复核测试”，使服务器/适配器反复进入效果待测计数。现在 manifest.reviewedScripts 保存独立分类、驱动/夹具 SHA-256、具体范围与剩余。当前源码身份匹配的分类离开未知脚本队列；报告仍保留所有分类记录。驱动/夹具缺失或哈希变化，立即重新进入复核队列，不悄悄丢失。分类没有进入 jobs/cases，不增加效果场景或 PASS。

TestMechanicCoverage 八项检查通过，包括真实内容变化重新开启驱动/夹具分类、分类不会授予效果 PASS、总指纹失效、部分运行和缺断言拒绝。来源摘要纳入完整分类记录，范围文字变化也要求审查。

A01 仅核销这十个脚本的初步分类；716 机制/执行维度的语义审查与上述历史效果证据迁移仍开放。未把 A03 的事件/替代/费用维度审查误当作旧脚本队列完成。源码快照和原分类冻结在 `.tools/mana-variants-1006/script-review`；旧结果保留，未重写其运行包或断言。
