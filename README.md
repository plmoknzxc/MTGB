# MTGB

MTGB 是使用 Unity 6 开发的万智牌式卡牌对战项目，目标是提供 MTGA 风格的对局体验，并让玩家通过独立数据编辑器、Lua 和卡图制作 DIY 卡牌。

项目仍在开发中。当前已有自动规则、牌组编辑及双人联机基础；新版战场为独立原型场景，尚未替换现有对局入口。

## 本次更新 · 2026-09-10

- **制卡与多库**：数据库升级至 schema 8；允许不同库复用库内整数 ID，跨库按系列与收藏编号识别卡牌。同一印刷身份后加载覆盖，加载状态显示冲突来源。
- **官方与 DIY 分类**：由 Data Editor 按卡定义，支持编辑器和游戏卡池的文字/分类组合搜索，房间按数据分类限制 DIY。
- **Lua 与规则**：新模板和 SDK 使用 API 2；支持效果同步调用自定义替代/触发回调，并保留嵌套深度和共享指令预算限制。修复固有效果跨区域后的绑定及部分费用、替代效应边界。
- **联机大厅**：完善房主开始比赛、准备条件、连接身份及断开清理；局域网房主退出时提示房间解散。联机协议升级为 v8，客户端与服务器须使用一致版本。
- **新版战场原型**：增加独立 IntegratedBattlefield 场景，以及手牌、战场区域、堆叠、卡牌检视和环境显示组件；完整交互与真实双端验收仍在后续开发范围。
- **验证**：扩充多库、边界卡牌、确定性及联机协议回归；加入编辑器 SDK 契约同步检查。

## 环境与启动

- Unity **6000.3.22f1**（以 `ProjectSettings/ProjectVersion.txt` 为准）。
- 当前 SQLite 和独立数据编辑器主要面向 Windows。
- 使用 Unity Hub 打开项目，打开 `Assets/Scenes/Main.unity` 并进入 Play Mode。
- 现有对局场景为 `Assets/Scenes/MTGAPrototype.unity`；新版独立原型为 `Assets/Scenes/IntegratedBattlefield.unity`。

## 制卡与多库加载

使用 [MTGB Data Editor](https://github.com/plmoknzxc/MTGB-DataEditor) 编辑 `.mtgbdb`，无需通过 Unity 制卡。

| 内容 | 当前路径约定 |
|---|---|
| 数据库 | `CardDatabases/*.mtgbdb` |
| 正面卡图 | `CardImages/<SET>/<COLLECTOR>.png`（也支持 jpg/jpeg） |
| 背面卡图 | `CardImages/<SET>/<COLLECTOR>_2.png` |
| Lua 脚本 | `CardScripts/<SET>/m<COLLECTOR>.lua` |
| 牌组 | `Decks/*.mdk` |

`card_id` 是单个数据库内唯一的正整数；跨库身份是 `SET/COLLECTOR`，牌组与资源均依据印刷身份解析。多个官方系列库与 DIY 库可以同时加载。

加载根目录顺序为 `CardDatabases`、`数据库`、`Databases`、`Database`。每个根目录先读取 `core.mtgbdb`，再按文件名忽略大小写排序读取其他库。同一身份由后加载且校验通过的库覆盖。

`content_kind` 为每卡分类：官方 `0`、DIY `1`，不根据数据库文件名推断。新卡及旧库迁移默认 DIY，需要作者按实际内容复核。

运行端只接受 **schema 8**。旧数据库应先备份，使用更新后的 Data Editor 升级并校验，再交由游戏加载。只有整数 ID 的旧牌组条目在 ID 有歧义时不会随意匹配其他卡牌。

Lua 作者接口见 [Lua SDK](Docs/LuaSDK.zh-CN.md)。新增或修改卡牌后，应完成编辑器保存/重载与数据校验，并验证实际施放、费用、目标和结算行为。

直接读取 ZIP、内容包格式与目录改造尚未定案。当前继续使用上述外部文件目录。

## 开发与验证

Unity 编辑器菜单：`MTGB > Validate Project`。通过 Pipeline CLI 可运行：

```powershell
unity command mtgb_validate_project --timeout 180 --json
pwsh -File Tools/Validation/Check-AuthoringSdk.ps1 -EditorRoot D:/WorkSpace/MTGB-DataEditor
```

自动规则和协议回归不能替代真人双端交互验收。新版战场的完整目标选择、费用交互及端到端流程仍需继续完善。
