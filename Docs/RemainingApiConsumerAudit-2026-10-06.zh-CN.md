# 剩余四个API的消费者分类

日期：2026-10-06。只审查固定原生源码和当前脚本，不运行或授予效果/UI PASS。四个API获得初步归属，A02/S06/S07仍开放。

| API | 原生处理与候选复用 | 继续待验 |
|---|---|---|
| StoreSVar | 算出整数，保存到来源牌及根/子异能SVar；本体没有新玩家选择。下游效果沿用各自的请求、结果及有效特征 | 七种计算、EachPlayer累计、根/子链保存、目标/触发对象、条件/时序/替代/控制组合的实际结果 |
| ChooseSector | 原生Alpha/Beta/Gamma经HumanController→IGuiGame.one→桥接modal；空间塑师还逐个给生物分配区域 | 当前检查的桥接与Assets/Scripts未发现区域字段/展示，需分配、选区、加指示物/消灭、跨区域阻挡的真实证明与空间表现；三个通用按钮不等于完成 |
| ClaimThePrize | 以准确玩家和吸引力ID触发ClaimPrize。Pick-a-Beeble脚本由骰子、幸运指示物、宝物、阈值判定进入领奖，再奖励、牺牲和开新吸引力 | 复用触发/堆叠/指示物/衍生物/区域消费者，补阈值、归属、顺序与对象生命周期；此原生API未提供独立交互小游戏 |
| FlipOntoBattlefield | 原生先选位置牌，再随机模拟翻转/命中和邻牌/附着，记忆命中ID并通知；不是物理投掷操作 | S07保留当前模拟方式的展示决定、空场/取消/未翻转/未命中/邻牌/附着/衍生物及两张牌不同后续结果；选牌+普通通知只说明候选复用路径 |

这些分类消除了“名字看起来特殊就一定要造新界面”的预设，不消除缺少实际验证的分支。StoreSVar的下游效果可能依赖费用、替代、永久状态或目标，不能把整个API视为无效果的排版助手。

## StoreSVar的结构输入

当前固定cardsfolder中68张牌、146行AB/SP/DB操作，七种Type：Number 80、CountSVar 45、Calculate 14、Targeted 4、Triggered 1、AdditiveForEach 1、Count 1。准确行号、参数与每个脚本SHA-256见 [结构归档](../.tools/state-branches-1006/store-svar-structural-inputs-complete.json)。这是待选择代表规则场景的结构输入，不能计作146场效果。

最初临时扫描只取AB/DB，得到65张/140行，遗漏Divine Deflection、Rhystic Lightning和To the Slaughter的SP根操作及其Targeted/Count分支。已按覆盖工具同一AB/SP/DB语法重扫，保留初次非权威输入，最终归档另存。不能用异能子链的样本猜整个计算器范围。

## 证据与范围

[消费者与源码哈希记录](../.agents/skills/mtgb-mechanic-validation/references/remaining-api-consumer-review-1006.json)保存四个Effect、HumanController、PlayerController、GameAction、Card、ClaimPrize触发、桥接与代表脚本身份。图查询无匹配，上游.tools被排除，桥接metadata_changed；已读取准确源码。区域字段的负面观察只限已检查的Tools/ForgeBridge/java和Assets/Scripts，不作全仓库/全部动态接口的穷尽结论。

本次没有改变运行包或产品代码，原征募/凋萎/强度运行仍保持独立身份。家庭归属、S06/S07的具体边界和A02审查范围已更新，未关闭工作包。当前总指纹和历史验证状态保持真实状态，分类不合并成当前全量PASS。
