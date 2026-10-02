# 找地请求与永久物分区

## 繁茂丘陵找地失败

原始会话 `a765fff9e2164979bf04b9639c3a4f26` 的 Unity 日志显示“Forge 桥接消息无效”。旧诊断仅记录引擎 stderr，漏掉了 Unity 解析异常。

以 living end 的印刷版本为基础，在内存中提高繁茂丘陵数量后，通过真正的 `OnlineMatchSession` 重现：支付生命、牺牲、双方让过后，`InputSelectEntitiesFromList` 请求被 Unity 拒绝。错误为 JSON 缺少逗号或结束括号；原始消息中“不可选”发生错误解码，破坏了紧随其后的引号。

Java 管道写 UTF-8，但 Windows 下 Process 默认解码不匹配。现在标准输出和标准错误明确以 UTF-8 读取。解析失败时，仅在主机诊断文件中保存具体异常和出错消息，不发给对手。

验证入口：`ForgeFetchValidation.Run()`；报告 `Tools/Validation/Reports/CardSelection/fetch-session.txt`。覆盖真实会话中的合法候选、带中文标记的不可选牌、确认选择以及生命/牌库/战场变化。测试不保存或修改玩家卡组文件。

## 永久物分区

参考用户提供的 MTG Arena 录屏与截图：生物在中央，非生物地在后排左侧；其余非生物永久物（神器、结界、鹏洛客、战役）放右侧独立区域。按当前类型分区，因此神器生物、结界生物、成为生物的地进入生物行。右侧预留宽度，多个永久物换行。

Play 模式在 IntegratedBattlefield 场景执行 `ForgeSupportLayoutValidation.Run()`，待 0.4 秒呈现动画结束后执行 `Verify()`，检查双方布局、Dryad Arbor 的生物分区和右侧永久物点击射线；报告及截图位于同一验证目录。

## 待用户说明

“是/否”分散在不同位置的确认 UI，与地的产费选项样式一起等待用户提供交互参考；本次不修改该样式。
