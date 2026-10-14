# R30 战斗伤害分配边界

2026-10-08：原 R30「改变伤害、跳过分配、共享致命约束和多个攻击者」有界行完成。五条原生首验、一套三语言 UI 首验；累计509原生／39UI、30关闭／35开放／18部分，goal继续active。固定Forge `4ec5f1a2c32fa90ecb983a72b9eb47aa5c5d7676`，运行包 `76dcae762f091a9b3e3a69e27875c2ae517d6531fb1471823b5d215be1add37b` 未改。

## 先复用，只补差异

旧 CombatAllocationBoundaryAudit-2026-10-04 保留普通践踏、死触、先攻、连击、单个阻挡者阻挡多攻击者时的防守分配、同一草稿实例、重置/释放/确认、十一对象滚动、私有错误与三语言证明；不重跑旧十六场。旧单阻挡者场景未证明多个践踏攻击者共享已分配伤害，本批补准确消费者。

图Tier2 generation 2026-10-08T01:11:42Z；相关桥接/驱动/夹具/UI路径有no_recorded_issue但metadata_changed，全部读取当前源。`.tools`上游排除，直接核对Card.getLethalDamage、addAssignedDamage、CardView.updateAssignedDamage及Combat.assignCombatDamage/PlayerControllerHuman.assignCombatDamage：共享致命阈值包含已分配伤害及死触，跳过返回null后原生将攻击者移到队尾。源码不是效果证明，实际断言如下。

## 原生结果

| 场景 | 实际操作和结果 |
|---|---|
| fog | 真实施放浓雾、支付一绿；六点分配仍先满足阻挡者四点致命，4/2分配后全部战斗伤害预防；双方20，全部存活、伤害零 |
| furnace | 熔炉替代不提前降低致命门槛或增加分配总量；六点4/2实际成为8/4，双方生物死亡，对手16 |
| skip-shared | 两只同名庞巨血口龙被同一双头巨人阻挡；跳过第一只，第二只4/2，第一只新context返回、致命0、不能继续跳过，0/6；防守者4/0；对手12、只巨人死亡，两个准确攻击者分别标4/0伤害 |
| shared-deathtouch | 死触蜘蛛先分配1，后续践踏的原生致命0、0/6；蜘蛛与巨人死亡、龙无伤，对手14 |
| divide | 原生Butcher Orgg可选任意分配接受后，候选包括未阻挡的Wall；分配2/0/4，不依赖致命门槛；Bear死亡、Wall无伤、Orgg标2，对手16 |

每场独立核对原生操作席位、准确来源/目标ID、总量与确认门槛、手牌身份、牌库全序、场上/坟墓场/放逐ID、法术力与MAIN2优先权恢复。开局夹具仅摆放，不直接创造结算结果。冻结的driver、两份夹具、runtime/revision、轨迹、只读观察记录和result各保留原生产身份。

初次fog/furnace在最终驱动结果检查因单攻击者数组越界失败；shared-deathtouch原飞行Nighthawk不能被巨人阻挡，是夹具场景选择错误。保留失败，改为地面死触蜘蛛，仅重跑三个失败场景；skip-shared/divide原成功不重跑。

## Unity与生产修复

新UI入口CombatDamageBoundaryPresentationValidation重放准确skip-shared/divide请求，真正射线点击稍后、加号、确认；准确三语言1946项通过，15张context截图。核对来源切换、共享致命0、各阶段合法选项、确认门槛、旧请求/重复/非操作守卫和请求清空后的面板隐藏。

首次显示发现Skip遮盖Remaining数字。仅调整ForgeBattlefieldController.Allocation：有Skip时底部三按钮并排；无Skip保持原两按钮布局；同一持久按钮随请求重新定位。规则、桥接协议、费用、资源及运行包未改，故不重跑原生或无关UI。Skip分支只在原生combat maySkip时出现；请求清空后的context重建是全部AssignAmount的共用路径。后续无Skip以及generic共用两按钮位置计算保持，普通generic damage标题/完整高度/固定字号三语言相邻定向回归通过，旧点击合同复用。

首次UI中途Confirm射线检查失败，旧输出和生产源码存attempts/ui-first；后续即时诊断命中Confirm，原失败未提供命中对象，不能确认是产品缺陷。加入命中对象诊断后1682项检查曾通过，但最终截图发现英文/日文残留第三行和旧卡图：测试只遍历预期行，没有排除旧行。立即撤回R30关闭及UI首验；旧1682结果/截图/审计/汇总存attempts/ui-1682-stale，原生五场仍有效。新增准确行数、卡图纹理和玩家空图断言；修复请求清空时allocationContext失效，避免新会话复用同context编号却不重建候选。最终1946项三语言两流程通过，英文/日文最终截图已实际查看，未将旧结果改写成完整PASS。新脚本第一次自动刷新未纳入程序集，精确ImportAsset后确认类型存在、编译无错误才执行；不算游戏失败。

## A06影响与剩余

修改生产路径为Allocation.cs可选Skip布局及请求清空后的草稿缓存失效，受影响消费者为原生战斗分配；新规断言属于新驱动。已测跨消费者/清空后同context37重用、准确候选总数/每行卡图与玩家空图、正常4门槛、0门槛、两种操作席位、跳过后的新来源/新context及无Skip任意分配。其他旧普通两按钮/语言/滚动证明保留原版本；没有全量PASS合并、真实联网、任意机制组合或R31完成结论。对手选择、额外战斗及多人魔戒仍R31；控制R36、网络R02、其他替代组合R44、未知文本R47继续开放。

原始证明及全文件SHA-256清单见 `.tools/combat-damage-boundaries-1008/result.json`，旧原始行保留于首验账closedPackages。验证方法的漏项及补充断言写入skill ui-reuse参考；按writing-for-agents保持局部分配触发条件，不扩成每次全机制检查。系统goal保持active。
