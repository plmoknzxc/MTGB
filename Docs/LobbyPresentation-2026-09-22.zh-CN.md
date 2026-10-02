# 联机大厅界面整理（2026-09-22）

## 修改

- 建房/加入页与房间页使用深灰面板、青绿色主要操作，统一标题、字段、间距和按钮对齐。
- 连接页按玩家名称、地址/端口、房间号、客户端配置排列；房主对局设置放在右侧。
- 房间内两名玩家分为独立席位，显示名称、房主/自己、牌组与准备状态。
- 删除 `MenuVisualTheme.UpdateDeckPreview` 及调用：房间页不再加载或插入五张牌组卡图。独立局间换备页仍保留必要的卡牌网格。
- 保留现有建房、加入、选牌组、准备、开局和离开流程。
- 修正旧 Dropdown 将选项文字作为 Toggle.graphic 的问题：未选中项文字不再消失；独立勾号表示当前项。

## Unity Play Mode 检查

- 编译通过，运行结束前读取 Console：0 条 error。
- 实际启动局域网主机（独立测试配置 `lobby-ui-validation`，端口 37779），成功进入房间并显示本地房主席位。
- 下拉选择 BO3 后建房，服务返回 bestOf=3。
- 实际 UI Raycast 命中 Starter 牌组选项，发送点击后 Dropdown.value=1。
- 切回 Forge-Validation 并准备，服务返回 hasDeck=true、ready=true；取消准备返回 ready=false。
- 点击离开房间，退出测试并关闭主机。
- 房间 DeckPreview 节点数为 0。
- 检查 1587×846 和 1353×846（16:10）显示；恢复 Game View 原来的 Free Aspect。

截图：`Captures/Lobby-Connection-Refresh.png`、`Captures/Lobby-Connection-16x10.png`、`Captures/Lobby-Room-16x10.png`、`Captures/Lobby-Format-Dropdown.png`。

此次为界面调整验证，没有重跑双客户端加入/对战、Windows 构建或完整局间换备流程。
