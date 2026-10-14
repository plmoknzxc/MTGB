# 对手手牌、法术力池与思绪复核

## 变化

- 法术力符号与数量并排、垂直居中，数量固定基准字号 18，保留用户字号偏好。根据实际数字宽度分配位置，仅显示非零颜色，无背景框；整行高度 34，向上留出 AUTO 间隔，长行向左避让屏幕右边缘。
- 资源仍来自当前原生操作玩家。控制对手时显示并点击被操控玩家的池，提交当前 `mana:<color>` 原生选项和版本；费用是否合法、如何扣费由 Forge 决定。
- 对手手牌从 30×42 色块改成 84×118 的本地完整卡背，扇形排列并复用对象。原生当前接收方快照明确给出手牌身份时才显示牌面。普通未知牌不推断身份，结算／切换／清空后不保留已揭示牌面。超过 24 张时顶部最多画 24 张，玩家栏继续显示真实数量。
- 弃牌选择使用当前语言的标题和说明。原生先展示手牌、随后提供可弃候选；客户端不把地牌添加为可选对象。

## 实际结算

新增 `Test-Thoughtseize.py`，复用已有局面夹具与生产桥接：真正施放思绪、指定对手、用沼泽支付黑色；原生揭示九张手牌，提供失效／迫从两个非地候选。选择准确的失效编号后，对手手牌减少一张，准确编号进入坟墓场，施放者失去两点生命；两方牌库数量不变，手牌重新隐藏，优先权恢复。

只运行新增的 `thoughtseize` 任务。原始流、局面与结果在 `Tools/Validation/Reports/Thoughtseize/`，原生批次编号以归档的 `engine-latest.json` 为准。没有重新执行不受影响的其余规则任务。

## 界面证据

- 新 `OpponentHandPresentationValidation`：中／英／日重放施放前、揭示、弃牌候选和结算后请求，完整本地牌面／卡背比例、准确选项的实际射线、旧版本／重复／非操作权限、隐藏恢复／清空；24 张和零张明确单列为显示边界夹具。
- `ManaResultPresentationValidation`：三语言新版横排、六色／长数字／零池、右边界／优先权／AUTO 间隔、实际池点击与支付后状态、旧／重复／旁观者、错误颜色后恢复。截图与输入沿用真实支付测试，不把显示夹具当规则效果。
- `PlayerControlPresentationValidation`：双方座位、整回合／战斗控制、手牌／池归属／点击及解除通过。此套发生在最后的纯位置避让调整前；最终位置另用上述法术力池套及真实第二座位蓝色支付请求核对：操作者为第二座位，资源来自第一座位，操作者池为零；两点蓝色可射线提交准确选项，随后原生占卜状态更新为一点。
- 共用 `CardSelectionPresentationValidation` 回归通过：搜证、刺探、排列／找地、X、混合墓地归属、房门和当前指示物等已有界面断言。该套不当作新增实际结算。

证据归档 `.tools/hand-pool-review-1004/proof.json`：输入／来源／结果哈希及截图。采样为 1560×898；只支持实际列出的语言、尺寸及边界。原 goal 保持暂停，没有宣称当前全量机制通过。

首次原生驱动没有覆盖 `InputSelectCardsFromList`，随后补入；两座位分别收到 RevealCards 时，又发现快照文件被第二座位覆盖。改为分别保存，原失败在 `.tools/thoughtseize/attempt-1/` 与 `hand-attempt-1/` 保留。六色长数字的初版布局与原通过记录保存在 `pre-boundary/`，最终加右边缘和 AUTO 几何断言复核。

图工具 `list_projects/check_index_coverage` 为 `Transport closed`，使用准确源文件回退，结论限于上述展示和实际场景。

## 截图

- [普通对手手牌](References/HandPool-2026-10-04/opponent-hand.png)
- [思绪揭示完整手牌](References/HandPool-2026-10-04/thoughtseize-revealed.png)
- [思绪选择弃牌](References/HandPool-2026-10-04/thoughtseize-discard.png)
- [思绪结算后](References/HandPool-2026-10-04/thoughtseize-resolved.png)
- [新版法术力池](References/HandPool-2026-10-04/mana-pool.png)
- [六色与长数字显示夹具](References/HandPool-2026-10-04/mana-six-colors.png)
