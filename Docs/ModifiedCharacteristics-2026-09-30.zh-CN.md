# 战场修正颜色与徽记卡图修复 · 2026-09-30

- 额外获得的公开关键词显示蓝色，自身固有关键词保持默认色。
- 力量／防御力各自与当前牌面自然数值比较，增加为绿色，降低为红色，相同为默认色。受伤数字仍另行显示，不将受伤误作防御力降低。
- 桥接提供自然数值和额外关键词，双面牌／复制用引擎当前牌面，盖牌以其公开 2/2 状态为基准。特征定义异能的动态数值加入自然基准。
- 匿伏／伪装自带的守护算盖牌机制的固有关键词，其他效果增加的践踏等算额外关键词。观战投影保留这些公开字段，不保留原牌私有信息。

## 徽记卡图为何缺失

`Build-RelatedTokenIndex.py` 原来只接受 `all_parts.component == token`。本地来源把 Garruk, Cursed Huntsman Emblem 标为 `combo_piece`、类型为 `Emblem`，所以它未进入关联索引与随后的本地卡图导入计划。图号 TELD/19 正确。

筛选现接受类型为 Emblem 的关联条目，且只加入徽记而不加入其他组合关联牌。新增导入选项 `--emblems-only`。本轮基于本地元数据补齐 42 张徽记 JPEG，共 3,305,007 字节；文件保存到 CardImages/系列/编号.jpg。全部通过 PIL 图像完整性检查，Unity 实际读取 TELD/19 成功。运行时图片加载仍只访问本地文件。

## 验证

- `python Tools/ForgeBridge/Test-ModifiedCharacteristics.py`：实际引擎持续效应使怨恨上的 2/2 成为 4/2、虚弱上的 2/2 成为 0/1；Levitation 给予飞行，Vampire Nighthawk 固有飞行不标为额外。解除持续效应后数值与额外关键词恢复，全部通过。此测试布置并移除引擎持续效应，不声称逐项手动施放端到端。
- `python Tools/ForgeBridge/Test-EmblemFaceDown.py`：上一轮徽记、盖牌、翻面、隐私回归通过，新字段以公开盖牌状态为基准。
- Unity 显示真实引擎快照，确认绿色 4/默认 2、红色 0/1、默认 2/2、蓝色额外关键词。
- 截图：`.tools/modified-characteristics/colors.png` 与 `emblem-local.png`。
- 正式桥接源码、编译类与 runtime 哈希已同步。

本次卡图使用官方 Forge/Scryfall 图号体系，并检查文件完整性与 Unity 本地解码，没有修改卡牌规则。

完整 MTGB 项目校验通过（Temp/project-validation-result.txt = PASS），包括目录、持续会话与进程隔离。校验时缺少的 Forge-Validation 测试卡组由 Starter 临时副本提供，校验后已移除。
