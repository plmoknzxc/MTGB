# MTGB

> 历史说明：以下为 Forge 迁移前的项目文档，仅供迁移参考。旧规则、Lua 执行与旧卡包加载入口已退出；当前构建与运行方法见 [项目首页](../README.md)。

## 扩展包安装与制卡

卡牌内容现以 `CardPacks/*.mtgbpack` 分发。一个包对应一个系列，下载后放入 `CardPacks` 即可，文件本质为 ZIP，也支持 `.zip` 扩展名。

```text
CardPacks/
  MTGB.mtgbpack
  SOS.mtgbpack
  TST.mtgbpack
  MH1.mtgbpack
```

每个包包含 `manifest.json`、`cards.mtgbdb`、`scripts/` 和 `images/`。包内无需再套系列目录：脚本如 `scripts/m65.lua`，卡图如 `images/65.png`、`images/65_2.png`。自动生成和手写 Lua 统一保存在 `scripts/`，数据库保留生成来源与校验快照。当前 MH1 包仍只有 10 张已测试样本。

游戏缓存数据库，按需读取并缓存 Lua 与卡图；替换包后重启游戏或重新加载卡库生效。缓存按包内容 SHA-256 隔离。缺失包内资源不会借用其他包或散装目录的同名资源。

Data Editor 支持“新建扩展包”“打开扩展包 / 库”“导出扩展包”。打开包后，保存卡牌、脚本和导入卡图都会写回包；写入成功前保留原包。旧数据库可按当前卡牌的系列导出。不同编辑器同时保存同一包会提示冲突，避免覆盖他人的版本。

`manifest.json` 使用格式 1，包含 `id`、`version`、`set_code`、`schema_version`（8）。一个包内只允许该系列。更新时保留包 ID 和卡牌 printing key；官方／DIY 仍由数据库 `content_kind` 决定。重复包 ID 按文件名排序只加载第一个并报告错误；跨包重复 printing key 会显示覆盖警告。

散装 `CardDatabases`、`CardScripts`、`CardImages` 仍支持开发使用；散装库先加载，扩展包随后按文件名排序加载。以下文档中旧散装路径用于开发模式。


MTGB 是使用 Unity 6 开发的万智牌式卡牌对战项目，目标是提供 MTGA 风格的对局体验，并让玩家通过独立数据编辑器、Lua 和卡图制作 DIY 卡牌。

项目仍在开发中。当前已有自动规则、牌组编辑及双人联机基础；新版战场为独立原型场景，尚未替换现有对局入口。

## 原始卡文生成与 MH1 导入验证

Data Editor 新增“从卡文生成”：读取基本信息中的原始中英文卡文，生成 Lua；扩展包保存时同步写入 `scripts/*.lua`。当前是有限句式的离线解析器；未支持的句子会阻止保存，不能视为完整卡池自动实现。高级 DSL 与手写 Lua 仍保留。

生成器现增加可组合能力树：把时机、费用、目标与多个效果分开解析，支持抓牌/弃牌/生命/区域移动/强化/指示物等按顺序组合。异步选择结束后继续后续效果，并支持有限的结算条件。原有已支持卡文保持 Lua 输出兼容。当前仍缺大量机制，**尚未追平 Forge**；细节和明确边界见 Lua SDK。

开发者可用 `dotnet run --project Tools/RulesCoverage -- --forge <Forge源码目录> <报告.json>` 批量扫描 Forge Oracle 卡文并按机制汇总未通过条目。扫描使用编译器与卡牌内容哈希缓存，不启动 Unity；这是编译覆盖统计，不是对局正确性证明。真实行为测试入口为 `ComposableRulesValidationMenu.ValidateFromCommandLine`，需要先用编辑器 `--validate-composable` 生成独立测试包。

本批 MH1 为独立数据库，收录塞哥维亚天使、市集行商法师、战舰水母、幽魅忍者、揭示之雨、罗堰部族、花丛之墙、自然颂歌、冰壳巨像与覆雪树林。卡文与卡图来自 Scryfall MH1 印刷数据，规则复核参考 Wizards MH1 Release Notes。专项测试入口为 `Mh1RulesValidationMenu.ValidateFromCommandLine`，应在独立批处理 Unity 项目运行。

## 本次更新 · 2026-09-14

- **描述制卡**：Data Editor 增加“效果描述”页，支持模板、行号诊断、Lua 预览、保存和导出。按固定语法描述效果即可生成 Lua，无需单独维护 TXT。
- **脚本保存**：扩展包中的生成与手写脚本统一位于 `scripts/`。数据库保留来源和生成快照用于校验，实际执行关联的 Lua 文件；缺失或与来源不符会拒绝加载。旧散装数据库仍可读取，导出包时会补齐文件。
- **首版范围**：单面单特征卡的咒语、进场/死亡/攻击触发、无目标启动异能、关键词与产费；效果支持抓牌、伤害、生命、区域移动、强化、指示物、衍生物、额外费用、X 和整段可选执行。多面、副特征、通用条件与持续/替代效应描述继续使用手写 Lua。
- **结算上下文**：生成效果中的“你”使用异能操控者，“自身”检查物件身份；来源转移控制或离场重进不会误用新对象。联机协议为 **v11**，客户端与服务器须一起更新。

例如在瞬间牌的效果描述页填写：

```text
版本:1
[施放结算]
目标:任意生物或玩家
伤害:目标,3
```

这是确定性编译的描述语法，不是自然语言自动识别。卡面文字仍单独编辑。详细语法与边界见 [Lua SDK](Docs/LuaSDK.zh-CN.md)。描述卡要求本次更新后的运行端；数据库基础 schema 仍为 8，旧手写 Lua 卡库无需迁移。

## 上次更新 · 2026-09-12

- **复杂规则**：补齐历险许可、章节目标与反击、替代顺序、同时区域移动身份检查、生命费用及状态动作边界。
- **特殊牌型**：修复房间另一扇门的解锁入口、职业升级的堆叠结算、案件的结束步骤、备法副本登记/取消/控制权/时机，以及换面后的区域重置。
- **DIY 接口**：API 2 增加触发目标声明、反击目标堆叠对象、切换卡面及自定义胜利接口；两端 SDK 同步。
- **验证范围**：新增复杂规则专项，纳入全项目回归；覆盖两席权威命令、目标选择、优先权与视图往返，不代表所有万智牌组合或新版战场实机交互已完成。
- **版本要求**：联机协议 v9；规则状态与对局结束原因有更新，客户端和服务器须一起更新。

## 上次更新 · 2026-09-10

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
