# R31 攻击限制、战斗费用与额外战斗

2026-10-08：十条新增原生首验、一套196项Unity真实输入／射线检查通过。R31部分完成，仍保留多人魔戒四级各席实际失去生命的边界；未授予四人、真实联网或当前全量PASS。累计519原生／40UI首验，30关闭／35开放／19部分，八个执行批次，goal保持active。

## 减量与范围

旧AttackDefenders已证明三只生物分别攻击玩家、对方旅法师及己方由对手防卫的战役，实际生命20→18、忠诚5→3、防御5→3；旧UI起拖和按钮含直接回调，不能代替本轮真实射线点击。本轮沿用原生初始请求，仅补准确来源和三种防守目标的点击、重复／旧请求守卫。

旧耗竭的选择、下次重置与恢复证明沿用，不重跑六个旧场景。新增Celebrant实际可选耗竭产生额外战斗及拒绝分支；Relentless Assault验证第二主阶段施放后插入战斗和主阶段；攻击／阻挡税因同一特殊费用预览消费者实际崩溃而扩展到两位付款者、自动／手动、取消和部分付款退款。

图Tier2最初generation2026-10-08T01:24:47Z、后续01:44:28Z；相关源码coverage无记录缺口但metadata_changed，读取准确源码。被排除的`.tools`上游及补丁生成文件直接读取。核对InputAttack、CombatUtil.checkPropagandaEffects/payRequiredBlockCosts、AddPhaseEffect、InputPayMana.ensureAutoPayManaSources与SpellAbility.EmptySa/copy；源码用于定位，不计效果PASS。

## 原生实际结果

| 场景 | 操作与断言 |
|---|---|
| relentless-main2 | 第一战斗熊造成2伤害；第二主阶段真实施放Relentless Assault并支付四地，熊重置后再次攻击；同回合两次战斗／两次第二主阶段，对手16 |
| celebrant-exert | Celebrant和熊宣攻，选择耗竭；只重置熊，第二战斗只有熊可再次攻击；对手12，Celebrant耗竭义务和本回合次数准确 |
| celebrant-decline | 拒绝耗竭，只一次战斗；对手14，没有耗竭义务 |
| attack-tax-pay | Ghostly Prison攻击税，手动支付两地后熊造成2；对手18 |
| attack-tax-auto | 同费用明确提交原生Auto，两地付款、对手18 |
| attack-tax-cancel | 初始付款取消，该熊不再攻击；双方20，熊及两地未横置，回到第二主阶段 |
| attack-tax-partial-cancel | 已支付第一地后取消；准确第一地退款，两地及熊未横置、池为空，双方20，继续第二主阶段 |
| block-tax-pay | 对手用Hipparion阻挡Hill Giant并支付一绿；马死亡，巨人标1伤害，双方20、付款地横置 |
| block-tax-cancel | 对手拒绝阻挡税，阻挡取消、马和地保持原状，巨人无伤，对手17 |
| must-attack-wall | 空宣攻不满足Juggernaut要求，原生再次请求后实际宣攻；两种墙均非合法阻挡，熊合法但不阻挡，对手15 |

每场开局夹具仅摆放，宣攻／阻挡／选择／施放／支付和阶段推进均由原生输入执行。准确双方生命、完整牌库顺序、私有手牌ID、场上／坟墓场／放逐ID、地／攻击者横置、池清空与第二主阶段恢复分别断言。每次尝试保留生产driver、两份夹具、runtime/revision、轨迹、只读观察和结果。

## 真实崩溃及修复

攻击税首次在出现付款请求前fatal。已有`human-auto-preview-preserves-intent`补丁为防止Offering／Emerge预览清除真实选择，复制`saPaidFor`；Forge的战斗费用使用非Cloneable的`SpellAbility.EmptySa`，copy返回null，ComputerUtilMana随后空引用。修复只在这个占位异能分支沿用原生预览，普通施放意图继续复制并恢复共享reservation。没有改写费用、合法性、阶段规则、卡牌资料或上游checkout。

修改持久来源为`Tools/ForgeBridge/NativePatches.json`，用现有Apply-NativePatches与Package-Runtime编译／打包。旧运行包76dcae76、新包9a6f9701分别保留身份。前四条已成功场景不重跑：它们未经过EmptySa税费分支；Relentless普通施放仍走未改的复制路径。修复后六条税费首验通过，另外原Offering加费和Emerge减费两条真实回归通过，结果在本批隔离目录，不覆盖旧首验证明。

失败记录保留：第一次普通沙箱停在javac，确认本任务父子进程后中断，改正常权限运行，未进入对局也不算游戏失败；第一次税费fatal不改写PASS。修复后的两个攻击税取消场景已产生正确末态，但驱动误要求一定重新打开宣攻；原生取消生物攻击后直接继续阶段也是有效恢复。修正该预期，仅重试这两条失败场景，其余成功生产身份保留。

## Unity及A06

CombatPhaseBoundaryPresentationValidation实际重放三种防守目标和两种第二战斗输入，射线点击准确来源／目标；旧战斗草稿不带入新请求。双方各自付款位的原生税费请求，在准确zh-CN／en-US／ja-JP核对费用图标、自动支付／取消标签、原生合法选项门槛、真实射线、重复／旧请求／只读守卫及第二主阶段清理；196项通过。最终中文攻击税、英文阻挡税、日文攻击税和战役选择截图已实际查看。未修改生产Unity代码；普通支付布局、固定字号、阶段条和AUTO位置等旧合同沿用。

本批没有验证客户端自动控制调用、四人战场或真实网络；自动支付是明确点击原生Auto，不能写成所有自动控制策略已通过。新运行包使全局指纹失效，如实保持“需重测”，不把本批局部成功合并为全量PASS。R31剩余多人魔戒四级各席失血仍保留；共享控制与请求归还引用R36，多方轮序引用R37，联网引用R02。

全部原始／失败／最终证明与SHA-256见`.tools/combat-phase-boundaries-1008/result.json`。R31原始边界保存在进度账partialPackages，没有静默移出goal。
