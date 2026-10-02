# 手牌层级和地牌能力交互验证

## 修正

- 手牌悬停置顶后，旧 sibling index 可能因相邻牌悬停或快照更新而失效。现在按当前手牌次序恢复层级，并防止重复 PointerEnter 覆盖原状态。
- 保留桥接的唯一能力直接执行逻辑。多能力请求附带来源卡牌编号；Unity 在所点地牌上方生成候选菜单，并隐藏侧边同组候选。提交后菜单立即关闭，仍使用原有请求编号与重复提交保护。
- 真实西瓦暗礁测试发现，无色法术力使用了 MagicColor 的 0，而 Forge 法术力池使用 ManaAtom 的 32。法术力池展示和支付候选均改为 Forge ManaAtom.MANATYPES。

## 2026-09-22 实测

- `python -X utf8 Tools/ForgeBridge/Test-LandAbilities.py`：真实 Forge，山脉直接点击，不出现能力确认，横置且红色法术力 +1；西瓦暗礁产生多个带来源编号的能力候选，选择无色后横置、无色法术力 +1、生命保持 20。
- `Test-ShockInteraction.py`：未付费取消恢复、正常支付与优先权结算仍通过。
- `ForgeBattlefieldValidation.Run()`：Unity 场景施法、目标拖放、费用及结算通过。
- `ArenaPresentationValidation.Run()`：重复进入/离开、交叉悬停及快照更新后手牌顺序稳定；菜单在地牌上方、侧栏隐藏、菜单按钮射线可达且只提交一次；原有拖拽与密集战场检查通过。

菜单位置测试使用明确的展示状态，真实西瓦暗礁规则由 Java 集成测试验证；不是完整双客户端端到端验收。图片为展示测试截图，不能作为特定地牌实际印刷能力的证据。

证据：`Tools/Validation/Reports/LandInteraction`，包含山脉和西瓦暗礁结果及菜单截图。原始场景输出位于 `.tools/forge-migration/arena`。

新 Java 桥接在新建对局进程中生效，已经运行的旧对局进程不会热更新。尚未重新生成 Windows 发布构建。
