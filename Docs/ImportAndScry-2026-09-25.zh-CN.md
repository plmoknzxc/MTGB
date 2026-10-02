# 带样式牌表导入与占卜结算修复

## 导入

MO/MTGA 文本允许卡名后的 `<样式>` 和末尾 `(F)`、`(Foil)`、`*F*` 等标记，同时识别 `[系列]`、`[系列] 编号` 和原有 `(系列) 编号`。
样式、闪卡标记仅作为可忽略的导入注释，不保证复现该异画或闪卡效果。提供编号时仍严格核对卡名与印刷编号。

兼容 UZ/US → USG、MI → MIR。无编号的 MB1 条目优先查 MB1；找不到时使用 Forge 的同名 PLST 表示。数量与主牌、备牌分区保持原样。

验证：`DeckExchangeValidation.RunCodec()`；截图的 12 行牌表已用实际目录全部导入，结果见 `Tools/Validation/Reports/DeckExchange/decorated-result.txt`。

## Preordain 卡在堆叠

双方让过后引擎进入占卜，原桥接没有实现 Forge 的 `manipulateCardList`。规则线程抛异常退出，客户端仍显示等待操作。

现在通过控制器的 `arrangeForScry` 专门接入占卜：逐张选择置顶或置底及顺序，支持重新选择、最终确认；对手的视图不包含这些选择或所看的牌。规则线程异常会返回明确的 fatal 消息并退出，详细异常仅保留在服务器诊断中。

验证：`python Tools/ForgeBridge/Test-Scry.py` 覆盖真实施放、双方让过、顺序相反、分放两端、全部置底、重选、非法选项和错误席位拒绝、规则线程异常退出。`Test-StackInteractions.py` 的三层堆叠、反击与同时触发排序回归通过。

本次未更改产费选项布局。已异常退出的旧对局需要重新开局。
