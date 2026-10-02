# 制卡基础与多库验收

日期：2026-09-05。对应开发待办第一阶段；MTGB 与独立 MTGB-DataEditor 均已修改并在本机验证。

## 已实现行为

- `card_id` 由编辑器在单个数据库中分配，不要求全局唯一。跨库身份为 `SET/collector`，牌组、卡图、默认 Lua 路径均按该身份解析。
- 不同印刷身份即使整数 ID 相同，也全部保留。同一身份后加载覆盖；运行端日志及主菜单重载状态显示冲突来源。
- 根目录顺序为 `CardDatabases`、`数据库`、`Databases`、`Database`；各根先 `core.mtgbdb`，其余文件名忽略大小写排序。只有通过整库校验的文件参与覆盖。
- 无印刷身份的旧牌组条目只在整数 ID 唯一时解析；有明确身份但缺失的条目不会退回整数 ID，避免串牌。
- schema v8 的 `content_kind`：官方 0、DIY 1。编辑器卡图预览旁选择整张卡分类，工具栏筛选；游戏牌组编辑中的搜索按钮支持文字及分类；服务器按该字段执行房间 DIY 限制。
- 新卡与旧库迁移默认 DIY，不依文件名推断。当前 core 的六张基本地示例 MTGB/1–6 明确标为官方，其余现有测试内容为 DIY。作者应按实际内容复核迁移分类。
- Lua 同步嵌套调用共享全局表、使用独立虚拟机栈，返回后恢复外层事件；整条调用链共享 200000 指令预算，回调深度上限 32，短回调也计费。
- 新脚本模板及编辑器补全所读 SDK 文档同步 API 2，两个仓库 schema.sql 一致。

## 验证记录

| 检查 | 结果 |
|---|---|
| Lua 回归先失败再修复 | 修复前实际施放报 nested execution；修复后自定义伤害替代生效，同源嵌套生命触发返回后外层事件恢复 |
| Lua 限制 | 嵌套深度、共享预算、异常后继续执行均通过 |
| Unity 全项目 | `unity command mtgb_validate_project --timeout 180 --json` 成功，Editor.log 有 `MTGB project validation passed.` |
| 多库补充专项 | 两个官方系列 MTGB/1、SOS/65 与 DIY TST/10 均设库内 ID 1，加载、覆盖、卡图/Lua、牌组保存重载及房间限制通过 |
| 核心库迁移 | 34 张卡经 Data Editor 逐卡两次保存/重载，schema 8 数据校验通过，迁移前已备份 |
| 多库数据副本 | 四个夹具通过 Data Editor 两次保存/重载及数据校验 |
| 编辑器构建及回归 | dotnet build 零警告零错误；`--self-test` 通过；`--validate-foundation` 验证真实控件、分类往返、列表筛选、API 模板和补全版本 |
| UI | 检查编辑器默认窗口截图并修正分类位置；游戏搜索面板在隔离预览场景检查按钮、输入及关闭逻辑并查看渲染截图 |
| 契约同步 | `pwsh -File Tools/Validation/Check-AuthoringSdk.ps1` 通过 |

编辑器可执行文件：`D:/WorkSpace/MTGB-DataEditor/bin/Debug/net8.0-windows/MTGB.CardDatabaseEditor.exe`。

迁移备份：`Logs/core-before-foundation-20260905-155757-180.mtgbdb`。
界面截图：`Logs/foundation-editor-validation/editor-classification.png`、`Logs/foundation-editor-validation/game-search.png`。

## 使用和范围

旧库先备份，再用更新后的 Data Editor 升级、复核分类并校验；运行端只接受 schema 8。Lua 和卡图仍按现有目录约定使用；本次不决定 ZIP 或目录改造。

本阶段的房间检查覆盖服务器牌组校验和全项目协议回归；UI 验证包含程序触发控件和截图，不等同于两个真人客户端鼠标全流程验收。后者仍属于第三阶段。当前缺图的 DIY 测试牌仍使用既有缺图显示，本次未制作新卡图。常见效果模板库、作者试牌工具及教程属于第二阶段。
