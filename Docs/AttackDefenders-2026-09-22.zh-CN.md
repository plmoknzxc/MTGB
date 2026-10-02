# 逐个生物选择攻击对象

## 本次实现

- 宣告攻击者时，从生物拖出橙色箭头，松手到合法玩家、鹏洛客或战役上，提交该生物的攻击分配。
- 也可以先点击生物，再点击高亮的攻击对象。选中的生物显示金色边框。
- 已分配的生物可重新拖曳改选；选中后，卡牌上方的「取消此生物攻击」撤回它的分配。
- 空白处松手仅取消本地选取。不同生物的分配分别显示连线，最后点击「确认攻击」。右侧不显示攻击对象选项列表。
- 鹏洛客显示忠诚指示物数量，战役显示防御指示物数量。

## 规则和协议

Forge 固定版本：`746455d75515daabec62971e0544cf19c66356b3`。

攻击对象候选取自 `Combat.getDefenders()`，每个组合由 `CombatUtil.canAttack(attacker, defender)` 检查。战役按保护者判断，不能只按控制者的座位筛选。最终宣告、攻击限制/要求和战斗伤害仍由 Forge 处理。

新候选 `attack:<attackerId>:<player|card>:<defenderId>` 带有攻击者、对象种类和对象 ID。撤回单个生物用 `attack-remove:<attackerId>`。一次经过会话、座位、请求号及候选校验的响应，执行 Forge `InputAttack` 的真人选取序列；没有由 Unity 写入 Combat、生命或指示物。

重复选择当前对象不会把攻击切换成取消。重新指定时先调用原生撤回，再选对象及生物。快照包含每个攻击者的 `defenderKind` 和 `defenderId`，用于两个席位显示一致的分配。

## 已执行验证

### 真实引擎

`Tools/ForgeBridge/Test-AttackDefenders.py` 配合隔离入口 `tests/AttackDefenderFixture.java`。

测试入口直接布置固定战场（跳过这些永久物的施放和进场异能），使用官方卡牌。生产入口不接受测试开关，测试类不放入打包运行时。宣告攻击、双方让过和伤害结算全部经过真实 Forge 对局循环。

- 三个 Goblin Piker 分别攻击对手、对手的 Jace Beleren、己方控制且由对手保护的 Invasion of Zendikar。
- 玩家生命 **20→18**；鹏洛客忠诚 **5→3**；战役防御 **5→3**。其他永久物指示物不变。
- 覆盖重新分配、同对象重复选择、取消单个攻击。
- 防御者生物、己方鹏洛客、由己方保护的战役没有被提供为对应合法候选。
- 非法组合、错误席位、重复/过期响应被拒绝；测试子进程正常退出。

### Unity Play Mode

`AttackDefenderPresentationValidation.Run()` 回放上述测试输出的真实 Forge 快照，检查实际场景的指针拖曳、射线落点、箭头网格、三个对象的准确回调、点击替代操作、单个取消、三条独立分配连线，以及空白/非法/过期/重复/对手席位操作。

另运行 `ForgeBattlefieldValidation.RunCombat()`：实际 Starter 对局施放 Raging Goblin，通过场景拖曳攻击玩家，真实战斗伤害 **20→19**。此测试直接把 UI 回调发送至运行中的 Forge。

`ForgeBattlefieldValidation.RunBlocking()` 也通过：双方正常施放 Raging Goblin，通过场景完成攻击、阻挡、取消阻挡、重新阻挡；战斗后双方生物进入坟场，生命仍为 **20/20**。结果副本为 `combat-result.txt` 和 `blocking-result.txt`。

记录目录：`Tools/Validation/Reports/AttackDefenders/`。`engine-result.txt` 是规则验证结果，`ui-result.txt` 是快照回放的界面验证结果，`trace.jsonl` 为完整引擎消息，`drag-0.png` 至 `drag-2.png` 和 `assignments.png` 为场景截图。

## 重复运行

在项目根目录，用已安装的 JDK 编译测试入口，然后运行 Python：

```powershell
javac -encoding UTF-8 -cp 'ForgeRuntime/classes;ForgeRuntime/lib/*' -d .tools/attack-defenders/classes Tools/ForgeBridge/tests/AttackDefenderFixture.java
python Tools/ForgeBridge/Test-AttackDefenders.py
```

Unity 打开 `IntegratedBattlefield`，进入 Play Mode，执行 `AttackDefenderPresentationValidation.Run()`。真实对局回归使用已有菜单 `MTGB/验证/战场/攻击与伤害` 和 `MTGB/验证/战场/阻挡`。

## 结论边界

已验证基本的玩家/鹏洛客/战役攻击分配与对应战斗伤害。没有据此宣称全卡、结帮编组、多人对局、所有攻击附加费用与复杂战斗组合均已验证，也未在本次重新执行 Windows 打包验收。
