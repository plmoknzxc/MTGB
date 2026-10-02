# 战场齿轮、完整卡面与 Z 撤回

## 实现

- 左上角只显示矢量齿轮图标，打开组卡界面共用的 `GameSettingsPanel`（中/日/英、文字大小）。
- 对局传入认输回调后，设置底部显示认输；再次确认才提交。取消无副作用，重复点击不会二次提交。普通组卡设置不显示认输。
- 模态设置持续位于最上层，战场快照刷新后仍拦截点击。Z 在设置打开或输入文字时不响应。
- 场上直接复用手牌的完整 Sprite 和 Image.preserveAspect；删除灰色自制标题栏、插画裁切及重排。卡框比例与手牌一致，只等比缩小；保留横置角度、变暗与较小半透明箭头。
- Z 和撤回按钮共同调用 `TryUndoPresentedAction`，只提交本席位当前 Forge 优先权请求中的 Undo。没有 Undo、已提交、付款取消或 End Turn 均不执行。Ctrl+Z 不触发，按住 Z 不重复提交。

## 验证

- Unity 编译无错误。
- `ArenaPresentationValidation.Run()`：同印刷卡手牌/场上 Sprite 相同，完整 UV 不裁切，最终绘制网格横纵比例一致；保留叠放、菜单点击、手牌拖曳回归。
- `BattlefieldMenuValidation.Run()`（和上述入口位于同一 Editor 测试文件）：实际绘制齿轮、打开现有语言/字号面板、投降确认/取消/重复保护、普通设置隐藏认输。
- 注入 Unity Input System 键盘事件，经过控制器 Update 验证 Z 提交一次，Ctrl+Z 不提交，按住不重复。另验证不会把付款 Cancel 或 End Turn 当成撤回。
- 设置打开时模拟新战场快照，确认仍在最上层；期间撤回被拦截。

测试使用展示数据和回调计数，不冒充完整联机投降或新的 Forge 规则验证；现有真实引擎的产费/撤回流程未修改。本次未重新打 Windows 包。

截图和报告：`Tools/Validation/Reports/BattlefieldMenu/`。
