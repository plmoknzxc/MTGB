# 真实多徽记与悬停消费者审计

## 原生首验

本批三条真实操作，运行包`b3c1f7604b168fa54075d81674a745ba185d4581a9742ed46e06408bc55f8bed`，原始驱动与RoyalStateFixture仅安排初始牌/场地，结果由原生施放、费用、忠诚异能、控制及状态动作生成。

- `double-emblems`：实际两次施放赞迪卡伙伴基定并各启用-4，两张旅法师归零进坟墓场，两枚同文徽记有不同原生ID；原生熊2/2→3/3→4/4，对方熊2/2不变。
- `mixed-source-left`：实际赞迪卡伙伴基定-4、试炼基定0生成不同徽记，英雄殉身消灭试炼基定；两枚不同徽记与本方3/3熊继续保留。这里只验来源离场、持有者及加成，试炼徽记的所有输赢条件组合仍在原outcome/replacements包，不以悬停文字代替效果。
- `stolen-source-emblem`：本方实际施放基定，下一回合对方施放没收取得控制并启用-4，徽记归对方、只使对方熊3/3，来源仍进原拥有者本方坟墓场。后续自己第三回合主要阶段，三条流程的徽记ID/持有者/规则均稳定；双方公开视图一致。

原失败是驱动从座位1公开视图找座位2手牌中的没收，正确隐私过滤造成StopIteration。修正为只在实际操作方视图取手牌选择，并保留旧两条PASS驱动身份；没有修改公开信息或生产路由。

## 界面修复

实际中文悬停显示英文 `Creatures you control get +1/+1.`，现有译文在来源卡的忠诚异能引用段落里。新增只读`EmblemText`格式分支：徽记、公开、准确tokenSourcePrintingKey能取到来源、原生名称准确为Emblem — 来源名时，取来源卡现有译名。原生完整徽记规则必须准确等于来源“获得徽记”引号中的整个配方，且来源/译文行数对应，才抽取同一行现有中文/英文/日文引文。

未知改写、自定义名、未知来源与无对应段落保留原文，不用普通引用句猜测徽记效果。没有改变规则、翻译数据、Forge运行包、数量/身份/权限或折叠键；下述两项准确图片关联另行补齐。

显示代码改动仅InspectIntegrated中card.emblem的Name/Rules；非徽记保持原MarkerName/MarkerRules，状态/延迟消费者的折叠/分页实现不改。徽记自身ID和持有者不替换成来源。

## 准确图片关联与失败修复

首套UI在实际徽记Face无sprite处失败：`.tools/multi-emblems-1006/ui/20261006T0707276545291Z/result.txt`。原驱动保存于`ui-attempt1/driver.cs`，没有把缺图改写为通过。赞迪卡伙伴基定原生键为TBFZ/12但本地未安装；试炼基定无tokenArtKey、来源为AKR/19，准确来源图片索引也缺徽记关联。生产TryBattlefieldArt未修改，不用旅法师卡面替代徽记。

完整阅读CardDataAuthoring与图片工具后，按[Scryfall PIO/14](https://scryfall.com/card/pio/14/gideon-ally-of-zendikar)、[AKR/19](https://scryfall.com/card/akr/19/gideon-of-the-trials)的准确all_parts编号取[TBFZ/12](https://scryfall.com/card/tbfz/12/gideon-ally-of-zendikar-emblem)、[TAKH/25](https://scryfall.com/card/takh/25/gideon-of-the-trials-emblem)。API原始JSON、候选索引、原索引与changedKeys/hash保存在`.tools/multi-emblems-1006/art-source`；核对来源名、准确来源印次、关联ID、徽记类别与完整配方，只更改PIO/14、AKR/19两个索引条目，保留原Knight Ally关联及其余条目。两张JPEG488×680解码/verify通过，使用既有下载器原子写入、未覆盖旧图，分别147046/134283字节，结果含SHA-256。

离线索引工具和emblems-only下载过滤原先只接受类别完全等于Emblem，漏掉Emblem — Gideon。两处改为取破折号前的准确主类别；`Tools/ForgeBridge/tests/test_related_token_emblems.py`离线回归通过，验证无副类型/有副类型徽记都链接及进入emblems-only，而普通Token Creature — Emblem不被当成徽记、普通combo_piece排除。此为工具/资源校验，不计新增规则场景。全卡池重新导入、其他缺图/中文日文扫描仍留R47，不运行已经删除的bulk缓存重下载。

## 当前界面证据与A06影响

`.tools/multi-emblems-1006/ui/20261006T0718411318015Z/result.txt`最终PASS：三语言×双方视图×创建/下一自己回合的实际原生快照，核对同/异类实际编号、×2折叠、真实射线悬停、原生持有者和来源离场/被夺后显示；RawImage纹理准确为CardImage_TBFZ_12或CardImage_TAKH_25，固定字号与规则高度、未知规则/自定义名负例、只读无提交通过。中文mixed-source-left-hover-165截图人工复核：实际试炼徽记图完整、中文名称与完整译文未裁切；卡图现为英文扫描，详情三语言取现有对应译文，不声称中文/日文扫描已安装。

实际输入和生产者哈希分别在input-hashes.tsv/producer-hashes.tsv；原生驱动跨两个版本保留driverArtifacts，夹具哈希固定09d4cfbdbaf1ab92d749fc7731414f837f441e943d10e2a669f277528f10b60e，运行包b3c1f760不变。原错误席位私有手牌与原缺图失败均保留。规则/桥接/UI路由未变，不重跑已通过三条原生或其他状态/延迟/分页整套。资源影响准确限两个来源的GetReferences/徽记图片消费者，实际UI加载和准确纹理证明已覆盖；非徽记Inspect分支按源代码保持原调用，未知依赖仍保留A06。首验登记新增3原生；本套属于既有marker/inspect消费者增量，不新增UI首验。

## 取证口径与未验证边界

三语言双方真实native snapshots检查同/异类徽记ID、实际卡图/数量、真实悬停射线和中文/日文来源引文、英文原规则、未知改写/自定义名负例、来源离场及跨回合同ID详情。此为已有marker/inspect消费者增量，不新增UI首验；旧折叠展开/分页适配器证明仍保留各自身份，不重复宣布新的效果PASS。

R35仅核销这三个命名原生范围和采样悬停；其他复杂徽记/动态配方、延迟标记来源/控制变化与未采样跨阶段/条件组合仍开放。源自试炼基定的输赢替代不是本批规则断言；原始状态基线、未知徽记译文/缺图回退、资源未就绪和真实联网分别保留R23/R47及原包。
