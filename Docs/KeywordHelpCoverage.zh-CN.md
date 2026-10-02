# 悬停关键词说明

## 覆盖范围

- 2026-09-25 CR 的 701 / 702 节：264 个条目，均有说明（部分条目包含多个名称）。
- 当前项目固定版本 Forge `Keyword.java`：200 个非内部关键词定义，均有说明。
- 另外包括历险、预兆、备法等展示机制。合计 282 项。
- 每项提供中文及英文说明；原有日文说明保留，尚无日文说明的项回退英文。没有确认中文名称的新机制保留英文名称，说明仍为中文。
- 覆盖的是说明文字，不表示游戏引擎的所有规则实现都经过本轮验证。

资料基线：[官方规则 TXT](https://media.wizards.com/2026/downloads/MagicCompRules%2020260925.txt)。英文参考项目固定版本 Forge 的提醒文字；中文为适用于不同数值／费用的机制摘要，不直接拿某一张牌的数值当通用规则。

## 识别与展示

- 关键词可出现在句中，不要求行首或附带提醒文字。
- 优先匹配完整长词：海岛循环属于类别循环，威力变身不重复显示变身。
- 忽略括号内提醒文字及其嵌套内容，防止飞行的说明额外产生延势说明框。
- 普通动作如施放、派出、消灭、牺牲保留说明条目，但不自动占用悬停机制栏。
- 中日同形词按当前界面语言消歧。别名是经过筛选的独立名称，不从整句自动推测。
- 单张牌的风味异能标题、模式标题不视为独立规则关键词，也不会把整段效果冒充机制说明。

## 更新与检查

编辑 `Tools/ForgeBridge/KeywordDescriptions.zh-CN.tsv`、`KeywordActions.tsv`、`KeywordAliases.json` 等输入，再运行：

```powershell
python -X utf8 Tools/ForgeBridge/Build-KeywordGlossary.py
python -X utf8 Tools/ForgeBridge/Build-KeywordGlossary.py --check
```

检查会对照 Forge 枚举和 `KeywordRulesBaseline.json`，遇到缺项、空说明、未展开模板或生成文件过期即失败。升级 CR 时需同步更新该基线；脚本不会在游戏运行时联网读取规则。

Unity 检查入口：`KeywordGlossaryValidation.Run()`。验证全部名称／别名，以及句中囤兵、搜证、呼魂、类别循环、长短词冲突、嵌套提醒文字和详情链接。结果写入 `.tools/keyword-glossary/result.txt`。
