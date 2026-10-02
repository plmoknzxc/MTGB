# 起手保留／调度选项缺失修复

## 原因

用户日志的调用链为 `PreferenceText.SetBaseSize → Apply → Current`，由 `PresentStackCards` 更新隐藏的堆叠标题触发。

语言/字号绑定器会为隐藏文字添加 `PreferenceText`。Unity 尚未执行隐藏对象上的 `Awake`，但战场刷新已调用 `SetBaseSize`，导致缓存的 `Text`、`TMP_Text` 都为空。异常发生在保留/调度按钮完成绑定之前，因此整个起手交互停住。

之前战场测试使用已经激活过的标题，未覆盖这一初始化顺序。已在原有调度回归里加入“隐藏堆叠、新建且未执行 Awake 的文字绑定”前置条件。

## 修复

- `PreferenceText` 首次应用字号/翻译时主动绑定文字组件，不再依赖 Awake 已经执行。
- 保留原始字号，重复调用不叠加缩放。
- 激活隐藏文字时保留外部刚更新的文字内容，避免恢复为旧按钮文案。
- 不改动 Forge 调度规则和响应协议。

## 验证

1. 修复前，隐藏父对象下创建文字绑定后调用 `SetBaseSize(20)`，稳定重现同一 NullReferenceException；`PreferenceTextLifecycleValidation.Run()` 回归先失败再通过。
2. uGUI 和 TMP 均验证：Awake 前设置字号、隐藏时更新文字、激活、反复调整字号。
3. `ForgeBattlefieldValidation.RunMulligan()` 在真实 Forge 对局中，通过场景射线点击完成双方重新抓七张、保留、选择一张放回牌库底、确认；开始对局时双方均为六张手牌。

结果：`Tools/Validation/Reports/PreferenceTextLifecycle/result.txt` 和同目录 `mulligan-result.txt`。本次验证使用编辑器内两个席位连接同一真实规则会话，没有重新执行独立双客户端或 Windows 构建验收。
