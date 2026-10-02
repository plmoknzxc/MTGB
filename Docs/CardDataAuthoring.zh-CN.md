# MTGB 卡牌数据编写与 AI 导入规范

> Forge 迁移说明（2026-09-15）：本文旧 `.mtgbdb`、扩展包与 Lua 流程仅用于保留的 DIY 迁移素材。新客户端读取 `ForgeRuntime` 官方目录，旧规则与 Lua 已退出；通过旧结构校验不代表内容已接入 Forge。Data Editor 的 Forge 保存契约后续实施，见 [迁移计划](ForgeMigration.zh-CN.md)。

本文是 AI 与维护者创建、导入或修改 MTGB 卡牌数据时的执行契约。目标是让数据与 **MTGB Data Editor** 实际保存的结果一致，而不只是“能被 Unity 勉强读出来”。

## 1. 权威来源

发生冲突时，按以下顺序判断：

1. 当前版本 MTGB Data Editor 的 `schema.sql`、`CardModels.cs`、`CardDatabaseService.cs` 与 `CardFeatureValidation.cs`。
2. MTGB 运行端的 `CardDatabaseRepository.SupportedSchemaVersion` 与 `CardDefinition`。
3. 本文。

Data Editor 仓库：<https://github.com/plmoknzxc/MTGB-DataEditor>

当前 MTGB 与 Data Editor 都使用 schema v8。开始批量写卡前必须重新核对版本；版本不一致时先停止导入并升级其中一端，不能猜测兼容关系。

MTGB 运行端只接受完整的 schema v8 数据，不提供旧版兼容读取：旧 schema、缺少 v8 表、只填写 `rules_text` 而没有结构化规则条目、缺少规范化数值表达式或主体镜像不一致，都会导致整份数据库被拒绝。旧数据库必须先在离线副本中迁移并通过 Data Editor 校验，不能依赖游戏运行时自动补全。

## 2. 数据模型

- **卡牌（Card）**：一张实体卡，共享 `card_key`、`card_id`、系列、卡图编号、Oracle ID、卡图和一个 Lua 文件。
- **面（Face）**：正面为 `face_index = 0`，背面为 `face_index = 1`。双面牌必须分别保存两面的特征。
- **特征（Feature）**：一套完整的名称、类别、费用、规则、数值和副类别。每一面必须有且只有一个主特征 `feature_index = 0`、`feature_role = 0`。
- **规则条目（Rule Entry）**：属于某个特征的有序规则内容。普通规则用 `entry_kind = 0`；传纪章节用 `entry_kind = 1`。
- **特殊区域**：历险、Omen、备法、房间门、分割部分、职业等级和案件阶段是同一面内的副特征，不是背面。

先判断“背面”还是“副特征”，再写数据。战役转化后的牌面、转化式双面牌写成 `face_index = 1`；历险咒语等写成同一面的 `feature_index > 0`。

## 3. 一张普通单面牌的最低完整结构

必须同时具有：

- `cards` 中一条主体记录。
- `card_features` 中一条 `(face_index = 0, feature_index = 0, feature_role = 0)` 主特征。
- 非空规则文本时，`feature_rule_entries` 中至少一条对应的普通规则条目。
- `cards` 的名称、类别、费用、规则、数值、超类别和副类别必须是正面主特征的兼容镜像。

`cards.rules_text` 和 `card_features.rules_text` 是兼容文本；`feature_rule_entries` 是 Data Editor 的结构化规则来源。只写兼容文本、不写规则条目不算完成。

## 4. 主体字段

| 字段 | 规则 |
|---|---|
| `card_key` | `大写系列代号/卡图编号`，例如 `TST/9`；必须与 `set_code`、`collector_number` 一致。 |
| `card_id` | 当前数据库内唯一的正整数，由 Data Editor 自动分配；不同数据库允许重复。跨库身份使用 card_key（系列/收藏编号），不能用 card_id 合并不同卡牌。 |
| `content_kind` | 内容分类：官方 `0`、DIY `1`；由编辑器选择。新建卡与旧库迁移默认 DIY，不能根据数据库文件名推断。 |
| `oracle_id` | 同一规则卡牌不同印刷版本的共享标识。没有真实共享关系的 DIY/测试牌留空，不伪造 `test-xxxx`。 |
| `set_code` | 去除首尾空白并转为大写；同时是卡图和脚本目录名。 |
| `collector_number` | 非空字符串，可包含数字与合法后缀；同时决定卡图、背面卡图和默认脚本文件名。 |
| `script_path` | 相对 MTGB 根目录，例如 `CardScripts/TST/m9.lua`；不能是绝对路径。无脚本的牌留空。 |
| `enabled` | 可用卡为 `1`；草稿卡应为 `0`，不能以残缺状态启用。 |

## 5. 类别、超类别与副类别

`type_flags` 是可组合位标记：

| 类别 | 值 |
|---|---:|
| 神器 Artifact | 1 |
| 战役 Battle | 2 |
| 生物 Creature | 4 |
| 结界 Enchantment | 8 |
| 瞬间 Instant | 16 |
| 亲族 Kindred | 32 |
| 地 Land | 64 |
| 鹏洛客 Planeswalker | 128 |
| 法术 Sorcery | 256 |

`supertype_flags` 同样可组合：基本 `1`、传奇 `2`、长效 `4`、雪境 `8`、普世 `16`。

要求：

- 亲族不能单独存在，必须与瞬间、法术、结界等另一种主类别组合。
- 类别、超类别和副类别只写在各自字段中，不能把“瞬间”“生物 — 妖精／德鲁伊”等类别行复制进规则文本。
- 副类别只写真实的规则副类别。普通产费神器不能为了描述外观而填写“装备”。
- 多类别直接组合位值，不要为了通过 UI 而只保留其中一种类别。

## 6. 费用与类别数值

- 法术力费用使用 `{2}{U}`、`{G/W}`、`{W/P}` 等 Data Editor 文本格式；无费用写空字符串。
- 生物必须填写 `power_expression` 与 `toughness_expression`。固定数值同时镜像到整数 `power`、`toughness`。
- 非生物特征的力量/防御力表达式按 Data Editor 保存结果规范化为 `0`。
- 鹏洛客初始忠诚写入特征的 `loyalty`；“初始忠诚 4”不重复写进规则文本。
- 战役布防值写入特征的 `defense`；不能拿 `toughness` 代替。
- 复合类别按其全部类别填写所需数值，不假设生物、鹏洛客、战役互斥。

## 7. 规则条目

每个特征分别维护规则条目：

- 普通规则：`entry_kind = 0`，`chapter_numbers = ''`。
- 章节异能：`entry_kind = 1`，`chapter_numbers` 为逗号分隔的正整数，例如 `1,2,3`。
- `entry_order` 从 `0` 连续递增，同一特征内不得重复。
- 普通规则文本可以包含多行异能，但不包含卡名、类别行、费用、力量/防御力、忠诚或布防值。
- 没有规则异能的白板生物使用空规则文本，不写“它没有异能”之类解释。
- `card_features.rules_text` 必须由有序规则条目组合而来；不要手工维护两个不同版本。

传纪必须具有“传纪/Saga”副类别并至少有一个章节条目；有章节条目的特征也必须具有该副类别。

## 8. 特殊区域与双面牌

`feature_role`：主特征 `0`、普通副特征 `1`、历险 `2`、Omen `3`、备法 `4`、房间门 `5`、分割部分 `6`、职业等级 `7`、案件阶段 `8`。

- 每一面只能有一个主特征；主特征必须位于 `feature_index = 0`。
- 副特征按显示顺序使用 `feature_index = 1, 2, ...`。
- 历险/Omen/备法/分割区域的相应副类别归属于副特征，不重复挂在主特征上。
- 房间门、职业等级和案件阶段必须填写非空且同一面内不重复的 `role_value`；职业等级必须是正整数。
- 分割部分的 `role_value` 留空，顺序由 `feature_index` 表示。
- 双面牌的正反面名称、类别、费用、规则和数值完全分开。正面规则不能拼接“背面：……”以及背面的类别或规则。

## 9. Lua、效果与提示文本

### 效果描述模式（2026-09-14）

单面单特征卡可在 Data Editor 的“效果描述”页使用描述 v1（高级 DSL）或格式 2（原始卡文），保存时自动生成 Lua。扩展包中生成和手写脚本统一保存在 `scripts/*.lua`，`script_path` 指向实际执行文件；来源和生成快照保存在 schema 8 的可选扩展表 `card_script_descriptions`。运行端会编译核对来源、快照与文件，缺失或不一致则拒绝加载，不回退执行数据库中的 Lua。取消描述模式并保存后可以手写当前文件；再启用生成时另建文件保留手写成果。旧散装数据库保留嵌入式读取，导出包时补齐文件。

描述卡要求本次更新后的编辑器与游戏端。卡面规则条目、类别及数值仍按本文其他章节维护；描述不会从卡文猜测效果。保存、数据校验与行为验证仍分别执行，生成成功不能替代行为验证。详细语法、能力范围与错误诊断见 `Docs/LuaSDK.zh-CN.md` 的“效果描述编译”节。

### 手写 Lua 模式

- 一张实体卡及其所有面、所有特征共享一个 Lua 文件。
- 默认路径是 `CardScripts/<SET>/m<COLLECTOR>.lua`。
- 新脚本声明当前运行端的 `ApiVersion = 2`，包含 `OnInit`/`OnPlay`（按需）以及 `return Card`；版本以 `LuaCSharpCardScriptRuntime.SupportedApiVersion` 为准。
- 施放目标、触发条件、费用和结算必须与规则文本一致；不能靠含糊规则文本让运行端猜目标。
- Lua 已实现效果时，`feature_effects` 可以为空；不要同时留下语义冲突的旧 `card_effects`。
- 提示文本使用 `feature_strings`，索引从 `0` 开始且同一特征内唯一。
- 写 Lua 前阅读 `Docs/LuaSDK.zh-CN.md`，只使用当前 SDK 已公开的 API。

## 10. 卡图

- 正面：`CardImages/<SET>/<COLLECTOR>.png`
- 背面：`CardImages/<SET>/<COLLECTOR>_2.png`
- `.jpg`、`.jpeg`、`.webp` 可供编辑器查找，但导入后推荐统一为 PNG。
- 数据有背面时应提供背面图；缺图可以暂时显示卡背，但必须在交付说明中列出。

## 11. AI 批量导入流程

1. **读取权威实现**：确认 Data Editor 与 Unity 的 schema 版本一致，并阅读本规范。
2. **建立卡牌清单**：逐张写明实体卡、面、特征、类别、费用、规则条目、数值、目标、Lua 和图片。
3. **语义复核**：规则文本、结构化字段与 Lua 三者逐项对齐；每个效果只能有一个明确含义。
4. **在副本上写入**：先备份 `.mtgbdb`，启用 SQLite 外键并在单一事务中写入；失败时回滚。禁止在 Unity 或 Data Editor 正在保存该数据库时并发写入。
5. **模拟 Data Editor 保存**：规范化系列代号、规则条目顺序、类别数值表达式和兼容镜像；写完后用 Data Editor 打开并保存至少一张代表卡进行差异检查。
6. **Data Editor 校验**：执行其 `--validate`，结果必须为退出码 `0` 且无错误。
7. **Unity 校验**：运行 `unity.exe command mtgb_validate_project`，必须出现 `MTGB project validation passed.`。
8. **行为验证**：至少覆盖加入空牌组、构筑保存/重载、单机施放、联机同步；有目标、触发、特殊区域或双面的牌分别验证其完整流程。
9. **交付审计**：输出逐卡结果，明确区分“数据结构通过”“Lua 编译通过”“游戏行为已验证”。三者不能互相替代。

## 12. 完成标准

一批卡牌只有同时满足以下条件才算完成：

- 每张启用卡的主体、所有面、所有特征和规则条目完整。
- 所有兼容镜像与正面主特征一致。
- 类型、数值、目标、规则文本和 Lua 行为互相一致。
- `card_id`、`card_key`、特征位置、规则顺序和提示索引均无冲突。
- Data Editor 与 Unity 两套校验均通过。
- 卡组保存并重载后卡牌仍能按 printing key 正确解析。
- 实际游戏中的施放、目标、费用、结算、触发和区域移动符合卡面。

“数据库能加载”“Unity 没报红”或“Lua 能编译”都只是中间检查，不是完成标准。

## 13. 多库与迁移

运行端只读取 schema v8。旧 v7 库先备份，在 Data Editor 中升级并复核内容分类，再通过双重校验；运行端不自动迁移。跨库相同 card_key 按固定加载顺序由后加载库覆盖，库内编号重复不构成跨库冲突。数据库按 CardDatabases、数据库、Databases、Database 的根目录顺序读取，各根先 core.mtgbdb，再按文件名忽略大小写排序。

## 14. 扩展包模式（2026-09-14）

日常分发使用 `CardPacks/<SET>.mtgbpack`（ZIP）。包内 `manifest.json` 指定 format_version=1、schema_version=8、稳定 id、version、唯一 set_code；根数据库为 `cards.mtgbdb`。数据库结构和本规范的制卡校验要求不变。

包内卡牌只属于 manifest 指定系列。手写 script_path 改为相对包根的 `scripts/m<COLLECTOR>.lua`；卡图为 `images/<COLLECTOR>.png` 与 `images/<COLLECTOR>_2.png`。不再增加系列子目录。原有 CardScripts/CardImages 路径仅用于散装开发模式。原始卡文生成使用描述格式 2，高级 DSL 使用格式 1；两者都在 scripts/ 保存独立 Lua，数据库保留来源和校验快照。旧版仅嵌入生成结果的包在编辑器工作副本中补齐文件，保存后完成升级。

必须通过 Data Editor 新建、打开、保存或 --export-pack 导出包，再执行 --validate；禁止只手工压缩不检查数据库内路径。打开包使用独立工作副本，保存时检查原包内容哈希并原子替换；更新时保留包 id 和 printing key。游戏按 SHA-256 缓存数据库及按需资源，不跨包查找缺失资源。重复包 ID 报错并仅使用排序靠前的包；跨包 printing key 覆盖有警告。

现有散装库可按系列分别导出。迁移前后需要比较运行内容哈希、脚本与卡图字节，并验证牌组保存重载、游戏行为。数据校验、Lua 校验与行为校验依旧分别执行。
