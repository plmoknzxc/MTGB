# 叠放数量首帧行高复核

## 结论

魔戒截图观察到的黑底无数字已定位并修复：叠放数字创建时继承 `verticalOverflow=Truncate`，固定24高小于字号1.3下CJK实际行高35.6856232，首帧不生成可见数字网格。PreferenceText后续刷新会改变溢出方式，所以后续帧可恢复，不能用稳定帧推断首次创建没有问题。

修改仅限 Arena 的 Count 数字框：保持原字号，按实际preferredHeight增加文本与底框高度。未改变折叠资格、卡牌身份、数量、规则或提交。

## 证据

- 原观察：[魔戒审计](RingCombatConsumerAudit-2026-10-06.zh-CN.md)与原截图保持冻结。
- `.tools/stack-count-1006/before-cn-measure.txt`、`before-measure.txt`及原图片保存首次观测；后续稳定帧图片中的数字恢复与原观察分别记账。
- `StackCountPresentationValidation` 对真实魔戒分组地输入，双方公开视角、中文/英文/日文和字号0.85/1/1.3，在首次EndOfFrame与后续0.6秒检查实际网格、preferredHeight边界和字号不变。结果 `.tools/stack-count-1006/ui/20261006T0606243825899Z/result.txt` PASS，输入和生产者哈希独立保存。

R47只核销这处数量显示缺口；其余动态文案/资源过渡仍开放。此消费者增量不增加规则或UI首验数，不重复BO3、玩家指示物、状态徽记或整场密度。网络和当前全量未验证。
