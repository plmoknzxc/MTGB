# Forge 测试牌组

在大厅选择 `Starter`，双方提交牌组、准备并开局，开局后通过 IntegratedBattlefield 战场的卡牌和选择面板操作。

## Starter：手动测试

60 张主牌，15 张备牌。主牌为 24 Mountain，以及各 4 张 Lightning Bolt、Shock、Raging Goblin、Goblin Piker、Volcanic Hammer、Hill Giant、Viashino Warrior、Goblin Raider 和 Canyon Minotaur。备牌为 7 Mountain、4 Gray Ogre、4 Hurloon Minotaur。

用于测试下地、产费、瞬间与法术目标、普通生物、敏捷、攻击阻挡和 BO3 换备。备牌选用简单生物与基本地，方便检查换备和注册卡池约束，不是竞技构筑。已验证该文件正常起手并完成闪电击结算；不代表全部战斗组合已验证。

## Forge-Validation：自动回归

固定 60 张主牌，其中 36 张山脉，供固定种子的闪电击施放回归使用。高地比例是测试配置。一般手动测试请选择 Starter。

## 文件格式

`.mdk` 是 UTF-8 文本。`#name` 指定名称，`#main` 指定主牌，`!side` 指定备牌。每行是“数量 系列代码/收藏编号”，例如 `4 2ED/162` 表示 4 张该印刷的 Lightning Bolt。

牌组条目使用精确印刷编号。主备牌合并按规则卡名检查数量限制，基本地及 Forge 定义的特殊数量规则除外。当前对局不支持 Commander。

DIY 卡包加载尚未实现。

验证命令：`python -X utf8 Tools/ForgeBridge/Run-RuleSmoke.py --deck Decks/Starter.mdk`。Unity 菜单 `MTGB/Forge/Validate Catalog` 校验两份牌组，`MTGB/Forge/Validate Persistent Session` 保存重载 Starter 并创建真实 Forge 对局。
