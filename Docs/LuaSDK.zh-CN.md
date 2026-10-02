# MTGB Lua API 与数据编辑器接入规范

> 历史迁移参考（2026-09-15）：MTGB 已切换 Forge，旧规则执行代码、Lua DLL 和绑定已退出。本文保留旧 DIY 的格式说明，不再作为新功能的 SDK 或模板开发目标。Data Editor 的 Forge 迁移另行实施；参见 [Forge 迁移计划](ForgeMigration.zh-CN.md)。

本文档对应：

- MTGB 数据库 Schema：`8`
- MTGB Lua API：`2`
- Lua 运行时：Lua-CSharp
- 数据编辑器仓库：`D:\WorkSpace\MTGB-DataEditor`

本文档主要供 **MTGB-DataEditor 实现 Lua 文件创建、代码补全、参数提示、模板生成和静态诊断**。卡牌作者也可以把它作为脚本参考。

实际运行时代码是最终依据：

- `Assets/Scripts/Scripting/LuaCSharpCardScriptRuntime.cs`
- `Assets/Scripts/Scripting/CardScriptApi.cs`
- `Assets/Scripts/CardDefinition.cs`
- `Assets/Scripts/Core/GameEventRuntime.cs`

## 原始卡文生成（2026-09-14）

Data Editor 的“效果描述”页点击 **从卡文生成**，直接使用基本信息中有序规则条目组成的卡文。无需另写 DSL。保存时重新读取卡文并编译；未知句子会列出行号并阻止保存，不忽略未实现的异能。支持中英文的部分完整句式，属于离线确定性解析，不是任意自然语言或完整 Oracle 卡池解释器。

原有完整句式保持原始 Lua 输出兼容。新增 `ComposableCardRules` 将卡文解析成能力、费用、目标、效果序列和有限结算条件，再交给确定性后端生成 Lua。支持顺序组合抓牌、弃牌、获得/失去生命、伤害、消灭、放逐、回手、横置/重置、回合内强化、临时关键词和正负指示物；能力时机包含咒语结算、自身进场/死亡/攻击、无目标启动异能。弃牌通过异步续接执行后续效果，空手牌直接继续。固定数量上限 1000。

部分中英文句式可生成相同效果，但并非双语任意句式等价解析。英文 `If you control a creature/artifact/enchantment, ...` 目前支持咒语或启动异能结算时检查，条件内仅支持单个同步效果。介入式 if、条件内多效果/异步选择、多目标组、目标为“任意目标”（含旅法师/战役）、模式、多面、副特征及大量机制仍会明确拒绝。原始卡文不是 Forge 脚本；目前没有追平 Forge 全机制。

可组合示例：`Draw two cards, then discard a card. You gain 3 life.`；`When Probe enters, draw a card. You gain 2 life.`；`Put two +1/+1 counters on target creature. Draw a card.`。牌名只核对自身指代，不按卡名或系列查表。新增输出带 `composable-1` 标记，保留格式 2；运行端和编辑器须同步更新。

扩展表复用 `card_script_descriptions`，`format_version = 2` 表示原始卡文，`source` 必须与正面规则文本完全一致。格式 1 继续表示高级 DSL，基础数据库 schema 仍为 8。生成脚本由 `CardRulesTextCompiler` 编译，编辑器与运行端逐字同步。加载时重编译并比对卡文和 Lua，旧客户端会拒绝不认识的格式；生成卡的目标完全由脚本声明，不能把进场触发目标当作施放目标。

抓牌后弃牌使用异步选择，期间没有优先权；读取效果操控者的手牌，不依赖来源当前操控者。普通 `GetZoneCard` 不允许读取手牌或牌库，需要使用带来源权限检查的 `GetMatchingZoneCards`。

雪境费用 `{S}` 消耗由雪境来源产生的法术力；雪境是来源属性，不是新颜色。该属性随法术力池序列化、确定性状态、支付与阶段清空处理。联机协议为 v11，双方均须更新。

## 效果描述编译（高级 DSL）

Data Editor 的“效果描述”页可启用描述模式、插入模板、检查/生成、保存卡牌或导出 Lua。描述采用固定语法，不解释自然语言卡文，也不兼容直接粘贴 Forge 脚本。基本信息中的规则条目仍是展示卡文，需要作者与效果描述核对。

```text
版本:1
[施放结算]
目标:任意生物或玩家
伤害:目标,3
```

源描述和生成 Lua 在同一数据库事务中保存到可选扩展表 `card_script_descriptions(card_key, format_version, source, generated_lua)`。数据库基础 schema 保持 8，描述格式独立版本为 1；旧手写 Lua 库无需改动。**描述卡必须使用本次更新后的游戏端与编辑器**，旧客户端不识别此扩展。运行端重新编译并比对生成结果，不一致或版本不支持时拒绝该库。

扩展包中，生成和手写 Lua 一律读取 `script_path` 指向的 `scripts/*.lua` 文件。生成文件必须与来源编译结果及数据库校验快照一致；缺失或被修改会拒绝加载，不回退执行数据库中的 Lua。取消描述模式并保存后可以手写当前文件；再启用生成会另建文件保留手写成果。旧散装数据库保留原有嵌入式读取，导出扩展包时补齐独立文件。显式“导出 Lua”用于另存一份，不改变模式。联机内容清单已包含实际执行的 Lua；当前雪境法术力与结算状态使用联机协议 v11。

语法与范围：

| 描述 | 含义 |
|---|---|
| `版本:1` | 第一条非注释行；`#` 开头为注释 |
| `[施放结算]` | 瞬间/法术结算，只能有一个段落 |
| `[进场]`、`[死亡]`、`[攻击]` | 此牌对应的触发；死亡/攻击要求生物；目标在入堆叠前选择 |
| `[启动]` | 必填 `名称` 和 `费用`，可填 `时机:法术时机` 或 `瞬间时机` |
| `[静态]` | 固有关键词与产费能力 |
| `目标:任意生物,0,2` | 至多两个目标；省略数量即恰好一个；最多 32 个 |
| `目标:任意永久物,1,1,神器或结界` | 第四参数可为神器、结界、神器或结界，宣告与结算均使用类别筛选 |
| `抓牌:你,2` | 玩家参数支持你、对手、已声明的玩家目标 |
| `弃牌:你,1` | 选择完成后继续后续效果；数量不足时弃掉所有手牌 |
| `条件效果:你控制生物;抓牌:你,1` | 结算时检查己方生物；也支持你控制神器、你控制结界；内部仅支持同步效果 |
| `获得生命:你,3`、`失去生命:对手,2` | 玩家生命变化 |
| `伤害:目标,3` | 对每个目标分别造成指定伤害；也支持你、对手、自身 |
| `消灭:目标`、`放逐:目标`、`回手:目标` | 永久物区域操作；也支持自身 |
| `横置:目标`、`重置:自身` | 横置/重置永久物 |
| `强化:目标,3,3` | 生物 +3/+3 直到回合结束；负数允许 |
| `赋予关键词:目标,践踏` | 直到回合结束 |
| `指示物:自身,+1/+1,1` | 增减指定名称的指示物 |
| `衍生物:SET,编号,你,2` | 创建已安装的指定卡牌的衍生物；不会自动安装被引用卡 |
| `可选:是` | 在结算时选择是否执行整段，不免除费用；后续效果只在选择执行后进行 |
| `附加费用:Discard:1` | 必须支付的额外施放费用，使用第 7 节费用格式 |
| `X:0,20` | 施放时声明 X，效果数值可填 X；实际费用取卡牌费用中的 `{X}` |
| `关键词:飞行,警戒` | 静态段；支持飞行、敏捷、警戒、践踏、死触、系命、延势、守军、先攻、连击、辟邪、不灭 |
| `产费:{G}` | 静态段；注册横置产费能力 |

目标范围包括任意/对手/己方生物、任意/对手/己方永久物、任意/对手玩家、任意/对手生物或玩家。永久物效果不能配玩家目标，玩家效果不能配生物目标。多个效果按书写顺序执行；多个进场/攻击/启动段落是不同异能；死亡效果合并在一个段落中。

v1 支持单面单特征卡，最多 32 段、每段 64 条、描述 64 KiB、输出 Lua 256 KiB。数值范围 0–1000，强化与指示物可使用 -1000–1000。未知段落、字段、费用、关键词、错误参数或不支持的组合会给出行号并阻止保存，不生成空效果兜底。

尚不支持多面/副特征、通用条件分支、通用持续/替代效应描述，以及启动异能的宣告目标。这些卡继续使用手写 Lua。**描述检查通过、Lua 可加载与实际行为正确是三个不同的验收结果。**

`Game.GetEffectController(source)` 在堆叠结算及其异步选择回调中返回该咒语/异能的操控者；其他时机返回来源的当前操控者。与永久物当前操控者、拥有者分别处理。`Game.GetEffectSource(source)` 在来源离场成为新物件时返回 nil，防止旧异能修改重进战场的同一张牌；在其他时机返回来源。生成代码使用这两个 API 处理“你”和“自身”。选项选择同样使用效果操控者。

编译器源码在游戏与编辑器中保持逐字同步，由 `Tools/Validation/Check-AuthoringSdk.ps1` 检查。编辑器验证入口为 `--validate-descriptions <输出目录>`；游戏专项入口 `DescriptionRulesValidationMenu.ValidateFromCommandLine` 读取项目根目录的 `description-database-path.txt`（内容是前述临时测试数据库路径），结果写到根目录 `description-rules-result.txt`。请在独立批处理验证项目中运行，不占用交互编辑器主线程。

## 1. 文件路径与卡牌标识

编辑器应当使用以下目录结构：

```text
MTGB/
├─ CardDatabases/
│  └─ cards.mtgbdb
├─ CardImages/
│  └─ SOS/
│     ├─ 65.png
│     └─ 65_2.png
└─ CardScripts/
   └─ SOS/
      └─ m65.lua
```

Lua 脚本规范路径：

```text
CardScripts/<系列代号>/m<卡图编号>.lua
```

例如：

```text
CardScripts/SOS/m65.lua
```

数据库的 `script_path` 必须保存相对于 MTGB 根目录的路径：

```text
CardScripts/SOS/m65.lua
```

### 1.1 标识含义

- `<系列代号>` 对应数据库 `set_code`。
- `<卡图编号>` 对应数据库 `collector_number`，也是卡图文件名使用的编号。
- 文件名中的 `65` **不是** 数据库内部数值 `card_id`。
- 同一张实体卡的正面、背面和全部 `card_features` 共用一个 Lua 文件。
- 双面卡正面卡图是 `65.png`，背面卡图是 `65_2.png`，脚本仍然只有 `m65.lua`。

### 1.2 路径要求

- `script_path` 必须是相对路径，不能指向 MTGB 根目录之外。
- Lua 文件使用 UTF-8 编码，建议不带 BOM。
- 系列代号和编号应先去除首尾空白。
- 编辑器创建脚本时应自动建立缺失的系列目录。
- `CardScripts` 是正式脚本目录；中文 `脚本` 目录和数据库 `script_path` 指向的其他位置仅用于兼容旧项目。
- 不要在正式目录与兼容目录中同时保留同一卡牌的两份脚本，以免加载到旧文件。

## 2. 脚本模块格式

每个脚本必须返回一个 table。推荐编辑器使用以下内容作为新脚本模板：

```lua
local Card = {
    ApiVersion = 2
}

function Card.OnInit(self)
    -- 在这里注册触发式、启动式、替代式、持续式或规则效果。
end

function Card.OnPlay(self)
    -- 旧式法术结算入口。新脚本优先使用注册式能力。
end

return Card
```

规则：

- 返回值不是 table 时，脚本加载失败。
- `ApiVersion` 高于客户端支持版本时，脚本加载失败。
- `OnInit(self)` 在脚本实例建立后自动调用一次。
- `OnPlay(self)` 是兼容现有简单法术的结算入口。
- 注册 API 中的 `hookName`、`conditionHookName`、`completedHook` 等字符串，必须对应返回 table 上的函数名。
- Hook 的 `self` 就是脚本返回的 table。
- 当前卡牌代理位于 `self.Card`。

推荐函数声明形式：

```lua
function Card.Resolve(self)
    Game.DrawForOwner(self.Card, 2)
end
```

不要把函数定义成未加入返回 table 的独立全局函数。

## 3. 编辑器应提供的 Lua 支持

### 3.1 补全根节点

至少补全：

```text
Game.
Game.Zone.
Game.Event.
Game.Replacement.
Game.ReplacementCategory.
Game.Continuous.
Game.ContinuousCharacteristic.
Game.Layer.
Game.RuleOperation.
Game.Duration.
Game.CardType.
Game.Supertype.
Game.Color.
Game.Timing.
Game.TargetMode.
Game.CastCostKind.
Game.FeatureRole.
Game.Rule.
self.Card.
self.Card.Data.
```

运行时同时接受：

```lua
Game.DrawForOwner(self.Card, 1)
Game:DrawForOwner(self.Card, 1)
```

文档、模板和自动格式化统一推荐点调用 `Game.Function(...)`。

`Game.ApiVersion` 是当前客户端公开的 Lua API 版本号，当前值为 `2`。

### 3.2 字符串 Hook 交叉检查

编辑器应识别以下参数中的函数名字符串：

- 触发式能力的结算 Hook、条件 Hook。
- 替代效果的处理 Hook、条件 Hook。
- 启动式能力的结算 Hook、可启动条件 Hook。
- 持续效果和规则效果的筛选 Hook。
- 选择请求的完成 Hook、取消 Hook。
- 施放目标组的筛选 Hook。

例如：

```lua
Game.RegisterTriggeredAbility(
    self.Card,
    Game.Event.PermanentEntered,
    "OnPermanentEntered",
    "CanTrigger",
    Game.Duration.WhileSourceRemains,
    true
)
```

编辑器应检查 `Card.OnPermanentEntered` 和 `Card.CanTrigger` 是否存在，并支持从字符串跳转到函数。

### 3.3 建议诊断级别

错误：

- 文件未返回 table。
- 使用高于 `2` 的 `ApiVersion`。
- 注册 API 引用的必需 Hook 不存在。
- 枚举成员不存在。
- `script_path` 是绝对路径或逃逸 MTGB 根目录。
- `set_code`、`collector_number` 或脚本路径为空。

警告：

- 定义了 Hook，但没有任何入口或注册引用它。
- 在 `OnInit` 之外重复注册永久能力。
- 长期保存 `ObjectID`，但脚本又会让对象改变区域。
- 同一卡牌同时存在于 `CardScripts` 与旧兼容目录。
- 使用 `OnPlay` 编写需要目标、模式、额外费用或异步选择的复杂效果。

提示：

- 条件 Hook 应返回布尔值。
- Lua API 中的列表下标从 `1` 开始。
- 异步选择创建成功不等于选择已经完成。

## 4. `self.Card` 类型

编辑器可以把 `self.Card` 视为只读的 `CardProxy`。

### 4.1 固定身份字段

```text
Name: string
ID: integer
OracleID: string
SetCode: string
CollectorNumber: string
TypeLine: string
ManaCost: string
Data: CardDataProxy
```

`self.Card.Data`：

```text
cardName: string
cardID: integer
setCode: string
collectorNumber: string
manaCost: string
```

### 4.2 运行时字段

```text
Owner: integer
OwnerPlayerID: integer
ObjectID: integer
Zone: Game.Zone
Controller: integer
ControllerPlayerID: integer
CurrentAttack: integer
CurrentDefence: integer
CurrentDefense: integer
DamageTaken: integer
IsTapped: boolean
IsDead: boolean
CardTypes: flags<Game.CardType>
Colors: flags<Game.Color>
ManaValue: integer
FeatureRole: Game.FeatureRole
FeatureIndex: integer
FeatureFace: integer
ClassLevel: integer
IsCaseSolved: boolean
IsPrepared: boolean
ResolvingChapter: integer
```

### 4.3 当前事件字段

```text
EventType: Game.Event
EventAmount: integer
EventPlayer: integer
EventFromZone: Game.Zone
EventToZone: Game.Zone
EventPrevented: boolean
```

代理会在每次进入 Hook 前刷新。卡牌改变区域后会成为新的游戏对象，因此不要跨区域长期缓存旧 `ObjectID`。

## 5. 常量

编辑器应使用下面的成员生成补全，不要让用户填写原始整数。

```text
Game.Zone:
  None, Library, Hand, Battlefield, Graveyard, Exile, Stack, Command

Game.Event:
  None, SpellCast, SpellResolved, PermanentEntered, PermanentLeft,
  ZoneChanged, CardDrawn, CardDiscarded, CardDied, CardTapped,
  CardUntapped, PhaseChanged, AttackDeclared, BlockDeclared, Damage,
  LifeChanged, CounterChanged, AbilityActivated, AbilityResolved,
  CardRevealed, CardMilled, PermanentSacrificed, TokenCreated

Game.Replacement:
  InvokeHook, Prevent, AddAmount, MultiplyAmount, SetAmount,
  ChangeDestination

Game.ReplacementCategory:
  General, BackFaceUpAsEnters, CopyAsEnters, EntersUnderControl,
  SelfReplacement

Game.Duration:
  Permanent, UntilEndOfTurn, WhileSourceRemains, Once

Game.RuleOperation:
  Add, Set, Minimum, Maximum, Allow, Deny

Game.CardType:
  None, Artifact, Battle, Creature, Enchantment, Instant, Kindred,
  Land, Planeswalker, Sorcery

Game.Supertype:
  None, Basic, Legendary, Ongoing, Snow, World

Game.Color:
  None, White, Blue, Black, Red, Green

Game.Timing:
  AnyPriority, Sorcery, Mana

Game.AbilityTrait:
  None, CouldAddMana, RequiresTarget, LoyaltyAbility,
  MovesCardIntoOrOutOfLibrary

Game.TargetMode:
  None, AnyCreature, OpponentCreature, FriendlyCreature,
  AnyPermanent, OpponentPermanent, FriendlyPermanent, AnyPlayer,
  OpponentPlayer, AnyCreatureOrPlayer, OpponentCreatureOrPlayer

Game.CastCostKind:
  Alternative, Additional

Game.FeatureRole:
  Primary, Generic, Adventure, Omen, Prepare, RoomDoor, SplitPart,
  ClassLevel, CaseStage

Game.Continuous:
  AddCardType, RemoveCardType, SetColors, AddKeyword, RemoveKeyword,
  SetPowerToughness, ModifyPowerToughness, SwitchPowerToughness,
  SetController, ReplaceRulesText, SetCardTypes, RemoveAllKeywords,
  CopyCharacteristics, SetSupertypes, AddSupertype, RemoveSupertype,
  SetSubtypes, SetName, SetFaceDownCharacteristics

Game.ContinuousCharacteristic:
  None, Copy, Control, Text, Type, Color, Ability, PowerToughness, All

Game.Layer:
  Copy, Control, Text, Type, Color, Ability, PowerToughness

Game.Rule:
  MaxHandSize, LandPlaysPerTurn, CastAsThoughFlash
```

`Game.CardType`、`Game.Supertype`、`Game.Color` 和 `Game.AbilityTrait` 是标志集合。当前编译器不接受 `|`；组合互不重复的单比特成员时使用 `+`，不得把同一位重复相加。
无色卡牌在颜色标志中使用 `Game.Color.None`；当前没有 `Game.Color.Colorless` 成员。

## 6. API 签名

以下签名使用便于编辑器实现提示的伪 Lua 类型：

- `Card`：`self.Card` 或 API 返回的卡牌代理。
- `Card[]`：Lua 数组，下标从 `1` 开始。
- `string[]`：Lua 字符串数组。
- `effectId`：整数；`0` 表示注册失败。
- 参数后的 `?` 表示可省略。

### 6.1 基础、玩家与伤害

```text
Game.Log(message: string): void
Game.DrawCards(playerId: integer, count: integer): void
Game.DrawForOwner(source: Card, count: integer): void
Game.DealDamage(targetPlayerId: integer, amount: integer): void
Game.DealDamageToCard(source: Card, target: Card, amount: integer): boolean
Game.DealDamageToTarget(source: Card, amount: integer): boolean
Game.DealDamageToPlayer(source: Card, playerId: integer, amount: integer): boolean
Game.GainLife(playerId: integer, amount: integer): void
Game.GainLifeForOwner(source: Card, amount: integer): void
Game.LoseLife(playerId: integer, amount: integer): void
Game.LoseLifeForOwner(source: Card, amount: integer): void
Game.GetOwner(card: Card): integer
Game.GetController(card: Card): integer
Game.GetEffectController(card: Card): integer
Game.GetEffectSource(card: Card): Card?
Game.ChangeController(card: Card, playerId: integer): boolean
Game.GetOpponent(playerId: integer): integer
Game.WinGame(playerId: integer): boolean
```

### 6.2 防止、替代和重生

```text
Game.PreventNextDamage(card: Card, amount: integer): void
Game.PreventAllDamage(card: Card): void
Game.PreventNextDamageToPlayer(playerId: integer, amount: integer): void
Game.PreventAllDamageToPlayer(playerId: integer): void
Game.PreventNextDamageBySource(source: Card, amount: integer): void
Game.PreventAllDamageBySource(source: Card): void
Game.ModifyOutgoingDamage(effectSource: Card, damageSource: Card,
  operation: string, value: integer, combatOnly: boolean,
  untilEndOfTurn: boolean): effectId
Game.ModifyIncomingDamage(effectSource: Card, target: Card,
  operation: string, value: integer, combatOnly: boolean,
  untilEndOfTurn: boolean): effectId
Game.ModifyPlayerDamage(effectSource: Card, playerId: integer,
  operation: string, value: integer, combatOnly: boolean,
  untilEndOfTurn: boolean): effectId
Game.RemoveDamageModifier(effectId: integer): boolean
Game.Regenerate(card: Card): void
```

伤害修改 `operation` 使用运行时支持的名称，例如 `Add`、`Multiply`、`Set` 或 `Prevent`。编辑器应为该字符串参数提供候选值。

### 6.3 目标与附着

```text
Game.CanTargetCard(source: Card, target: Card): boolean
Game.CanAttachToCard(source: Card, target: Card): boolean
Game.AttachToCard(source: Card, target: Card): boolean
Game.DetachCard(source: Card): boolean
Game.GetTargetCard(source: Card): Card?
Game.GetTargetPlayer(source: Card): integer
Game.GetTargetCount(source: Card): integer
Game.GetTargetCardAt(source: Card, index: integer): Card?
Game.GetTargetPlayerAt(source: Card, index: integer): integer
Game.AddTargetCard(source: Card, target: Card): boolean
Game.AddTargetPlayer(source: Card, playerId: integer): boolean
Game.ClearTargets(source: Card): void
Game.RegisterTriggerTargets(card: Card, hook: string, prompt: string,
  mode: Game.TargetMode, minimum: integer, maximum: integer,
  filterHook: string = ""): boolean
Game.CounterTargetStackObject(source: Card): boolean
```

### 6.4 施放声明、模式、X 与目标组

这些 API 一般在 `OnInit` 中配置：

```text
Game.ConfigureCastModes(card: Card, minimum: integer,
  maximum: integer): boolean
Game.AddCastMode(card: Card, id: string, label: string): boolean
Game.ConfigureCastX(card: Card, minimum: integer,
  maximum: integer): boolean
Game.AddCastCost(card: Card, id: string, label: string,
  kind: Game.CastCostKind, costText: string, optional: boolean): boolean
Game.AddCastTargetGroup(card: Card, id: string, prompt: string,
  targetMode: Game.TargetMode, minimum: integer, maximum: integer,
  filterHookName: string, distinctWithinGroup: boolean): boolean
Game.AddCastTargetGroupAdvanced(card: Card, id: string, prompt: string,
  targetKinds: flags<Game.TargetKind>,
  allowedZones: flags<Game.TargetZone>,
  controllerRelation: Game.TargetController,
  requiredCardTypes: flags<Game.CardType>,
  requiredColors: flags<Game.Color>,
  minimum: integer, maximum: integer,
  filterHookName: string, distinctWithinGroup: boolean,
  relations: flags<Game.TargetRelation>,
  relationGroupId: string): boolean
```

高级目标组支持永久物、玩家、区域中的牌、堆叠上的咒语和异能。标志值可使用 Lua
互不重复的标志相加组合，例如 `Game.TargetKind.Permanent + Game.TargetKind.Player`。
`requiredCardTypes` 和 `requiredColors` 中指定的标志必须全部满足；需要表达“或”条件时，
使用 `filterHookName` 做额外筛选。`relationGroupId` 指向另一个目标组，
可配合 `SharesCardType`、`SharesColor`、`SameController`、
`DifferentController` 和 `DistinctAcrossGroups` 表达组间关系。
`minimum` 为 `0` 即“至多”；未选择目标的咒语不会因为没有目标而在结算时被反击。

目标在宣告时检查一次，在结算时按稳定对象身份重新检查。部分目标失效时仅移除非法目标；
只有原本选择过目标且所有目标都已非法时，咒语才会因规则被反击。

结算时读取已经锁定并重新检查过的声明：

```text
Game.GetCastX(card: Card): integer
Game.IsCastModeSelected(card: Card, modeId: string): boolean
Game.WasAdditionalCostPaid(card: Card, costId: string): boolean
Game.GetTargetGroupCount(card: Card, groupId: string): integer
Game.GetTargetCardInGroup(card: Card, groupId: string,
  index: integer): Card?
Game.GetTargetPlayerInGroup(card: Card, groupId: string,
  index: integer): integer
```

### 6.5 选择 UI

选择 API 是异步流程。返回 `true` 只表示请求成功建立，结果必须在完成 Hook 中读取。

```text
Game.ChooseOptions(source: Card, prompt: string, labels: string[],
  minimum: integer, maximum: integer, mayCancel: boolean,
  completedHook: string, cancelledHook: string): boolean
Game.ChooseMode(source: Card, prompt: string, labels: string[],
  mayCancel: boolean, completedHook: string,
  cancelledHook: string): boolean
Game.OrderOptions(source: Card, prompt: string, labels: string[],
  completedHook: string): boolean
Game.ChooseNumber(source: Card, prompt: string, minimum: integer,
  maximum: integer, mayCancel: boolean, completedHook: string,
  cancelledHook: string): boolean
Game.ChooseRepeatedOptions(source: Card, prompt: string,
  labels: string[], minimum: integer, maximum: integer,
  maximumPerOption: integer, mayCancel: boolean,
  completedHook: string, cancelledHook: string): boolean
Game.DistributeAmount(source: Card, prompt: string, labels: string[],
  totalAmount: integer, minimumTargets: integer,
  maximumTargets: integer, mayCancel: boolean,
  completedHook: string, cancelledHook: string): boolean
Game.GetLastChoiceValue(): integer
Game.GetLastChoiceCount(): integer
Game.GetLastChoiceOption(index: integer): integer
Game.GetLastChoiceAllocation(optionId: integer): integer
```

多选、排序、分配和重复选择都会先保留完整选择，再由玩家统一确认。确认前再次点击已选项
会取消或替换选择；只有请求的 `mayCancel` 为 `true` 时才允许取消。选择请求存在期间，
规则层会阻止施放其他牌、推进阶段、传递优先权等无关游戏动作。

### 6.6 区域、查找与移动

```text
Game.GetZoneCount(playerId: integer, zone: Game.Zone): integer
Game.GetZoneCard(playerId: integer, zone: Game.Zone,
  index: integer): Card?
Game.GetMatchingZoneCards(source: Card, playerId: integer,
  zone: Game.Zone, filterHookName: string): Card[]
Game.MoveToZone(card: Card, destination: Game.Zone,
  toTop: boolean): boolean
Game.MoveCardsToZone(cards: Card[], destination: Game.Zone,
  toTop: boolean): boolean
Game.ShuffleLibrary(playerId: integer): boolean
Game.Mill(playerId: integer, count: integer): integer
Game.Discard(card: Card): boolean
Game.ReturnToHand(card: Card): boolean
Game.CounterSpell(card: Card): boolean
Game.CounterTargetStackObject(source: Card): boolean
Game.WinGame(playerId: integer): boolean
Game.ChangeCardFace(card: Card, faceIndex: integer): boolean
Game.CastWithoutPayingMana(card: Card, featureIndex: integer): boolean
```

`MoveCardsToZone` 会先预检整个数组。预检失败时不会只移动其中一部分。移到牌库顶时，Lua 数组第 `1` 项成为最上面的一张。

### 6.7 战场、衍生物与复制

```text
Game.Destroy(card: Card): boolean
Game.DestroyTarget(source: Card): boolean
Game.Exile(card: Card): boolean
Game.ExileTarget(source: Card): boolean
Game.PutIntoGraveyard(card: Card): boolean
Game.Tap(card: Card): boolean
Game.Untap(card: Card): boolean
Game.CreateTokens(setCode: string, collectorNumber: string,
  controllerPlayerId: integer, count: integer): integer
Game.CreateEmblem(setCode: string, collectorNumber: string,
  controllerPlayerId: integer): Card?
Game.CopySpell(sourceSpell: Card, controllerPlayerId: integer): Card?
Game.CopyStackAbility(stackIndex: integer,
  controllerPlayerId: integer): boolean
```

### 6.8 指示物与状态

```text
Game.AddPoisonCounters(playerId: integer, amount: integer): void
Game.GetPoisonCounters(playerId: integer): integer
Game.GetLoyaltyCounters(card: Card): integer
Game.AddLoyaltyCounters(card: Card, amount: integer): boolean
Game.GetCounters(card: Card, counterName: string): integer
Game.AddCounters(card: Card, counterName: string,
  amount: integer): boolean
Game.GetCardName(card: Card): string
Game.IsOnBattlefield(card: Card): boolean
Game.GetObjectId(card: Card): integer
Game.GetZone(card: Card): Game.Zone
Game.HasType(card: Card, cardTypes: flags<Game.CardType>): boolean
Game.HasSubtype(card: Card, subtype: string): boolean
Game.HasSupertype(card: Card,
  supertype: flags<Game.Supertype>): boolean
Game.HasKeyword(card: Card, keyword: string): boolean
Game.GetCardTypes(card: Card): flags<Game.CardType>
Game.GetColors(card: Card): flags<Game.Color>
Game.GetManaValue(card: Card): integer
Game.IsTapped(card: Card): boolean
```

### 6.9 特殊结构

```text
Game.GetFeatureRole(card: Card): Game.FeatureRole
Game.GetFeatureIndex(card: Card): integer
Game.GetFeatureFace(card: Card): integer
Game.GetFeatureName(card: Card): string
Game.GetFeatureRoleValue(card: Card): string
Game.GetResolvingChapter(card: Card): integer
Game.GetClassLevel(card: Card): integer
Game.IsCaseSolved(card: Card): boolean
Game.IsFeatureActive(card: Card, faceIndex: integer,
  featureIndex: integer): boolean
Game.IsRoomUnlocked(card: Card, faceIndex: integer,
  featureIndex: integer): boolean
Game.Prepare(card: Card, featureIndex: integer): boolean
Game.StopPreparing(card: Card): boolean
Game.SolveCase(card: Card): boolean
Game.ChangeCardFace(card: Card, faceIndex: integer): boolean
```

历险、Omen、备法、房间、分割牌、职业和案件不使用独立脚本。脚本应通过当前 `FeatureRole`、`FeatureIndex`、`FeatureFace` 和数据库特征结构分派行为。

### 6.10 注册式能力

```text
Game.RegisterManaAbility(card: Card,
  producedSymbols: string): boolean
Game.RegisterFetchBasicLandAbility(card: Card): boolean

Game.RegisterActivatedAbility(card: Card, abilityName: string,
  description: string, cost: string, resolveHook: string,
  canActivateHook: string, timing: Game.Timing,
  ruleTraits: flags<Game.AbilityTrait>): boolean

Game.RegisterTriggeredAbility(card: Card, eventType: Game.Event,
  hookName: string, conditionHookName: string,
  duration: Game.Duration, otherCardsOnly: boolean,
  linkId?: string): effectId

Game.RegisterDelayedTriggeredAbility(card: Card,
  eventType: Game.Event, hookName: string,
  conditionHookName?: string, otherCardsOnly?: boolean,
  linkId?: string): effectId

Game.RegisterReplacementEffect(card: Card, eventType: Game.Event,
  operation: Game.Replacement, hookName: string, value: integer,
  duration: Game.Duration, conditionHookName?: string,
  linkId?: string,
  category?: Game.ReplacementCategory): effectId

Game.RegisterContinuousEffect(card: Card,
  operation: Game.Continuous, filterHookName: string,
  dependencyKey: string, cardTypes: flags<Game.CardType>,
  colors: flags<Game.Color>, textValue: string, valueA: integer,
  valueB: integer, duration: Game.Duration,
  linkId: string, characteristicDefining?: boolean,
  reads?: flags<Game.ContinuousCharacteristic>,
  writes?: flags<Game.ContinuousCharacteristic>,
  copySource?: Card,
  supertypes?: flags<Game.Supertype>): effectId

Game.RegisterRuleEffect(card: Card, ruleKey: Game.Rule,
  operation: Game.RuleOperation, value: integer,
  affectedPlayerId: integer, filterHookName: string,
  duration: Game.Duration, linkId: string): effectId

Game.GetRuleValue(ruleKey: Game.Rule, playerId: integer,
  subject: Card?, baseValue: integer): integer
Game.GetRulePermission(ruleKey: Game.Rule, playerId: integer,
  subject: Card?, baseValue: boolean): boolean
Game.RemoveRuntimeEffect(effectId: integer): boolean
Game.RemoveLinkedEffects(card: Card, linkId: string): integer
```

`Game.RegisterReplacementEffect` 会按 CR 616 在每次应用后重新检查。若同一事件同时有多个可用替代/防止效应，受影响玩家选择先应用哪一个；`category` 用于“自身替代、进场控制、复制进场、背面朝上、一般替代”的规则优先级。未指定时使用 `General`。

`ChangeDestination` 的 `hookName` 留空时，`value` 为目标 `Game.Zone`（不能为 None 或 Stack），由内建操作更改目的地区域；提供 Hook 时仍由 Hook 调用 `SetEventDestination`。例如生物死亡改为放逐可以使用 `CardDied` 事件、空 Hook 和 `Game.Zone.Exile`，无需重复进入 Lua。

持续效果按 CR 613 的复制、控制、文字、类别、颜色、异能、力量/防御力层执行。`dependencyKey` 是当前效果依赖的一个或多个 `linkId`，多个值以 `,`、`;` 或 `|` 分隔。动态筛选条件读取哪些特征、效果写入哪些特征，应分别写入 `reads` 与 `writes`，运行时据此在同层中计算依赖并让依赖关系覆盖时间戳。特征定义能力应设置 `characteristicDefining = true`。

### 6.11 当前事件

这些 API 只应在触发、替代等事件 Hook 中使用：

```text
Game.GetEventType(card: Card): Game.Event
Game.GetEventAmount(card: Card): integer
Game.GetEventPlayer(card: Card): integer
Game.GetEventSource(card: Card): Card?
Game.GetEventCard(card: Card): Card?
Game.GetEventFromZone(card: Card): Game.Zone
Game.GetEventToZone(card: Card): Game.Zone
Game.SetEventAmount(card: Card, amount: integer): boolean
Game.SetEventDamageTarget(card: Card, target: Card): boolean
Game.SetEventDamagePlayer(card: Card, playerId: integer): boolean
Game.PreventEvent(card: Card): boolean
Game.SetEventDestination(card: Card,
  destination: Game.Zone): boolean
```

`SetEventDamageTarget` 与 `SetEventDamagePlayer` 仅能修改正在处理的伤害事件。它们用于伤害替代效应改变受伤对象；引擎会按 CR 616 重新检查新受影响对象可用的替代与防止效应，并在最终提交伤害时使用修改后的永久物或玩家。

战役移去最后一个防御指示物时，由引擎创建内建的 `OnBattleDefeated` 触发。该 Hook 是可选扩展，不负责基础规则：即使脚本未定义它，引擎仍会在触发结算时放逐该战役，并让触发的操控者选择是否不支付法术力费用施放其背面；模式、目标、附加费用与最终确认仍走统一施放流程。

### 6.12 特征、持续修改与规则值

```text
Game.SetMaxHandSize(playerId: integer, value: integer): void
Game.AddKeyword(card: Card, keyword: string): void
Game.RemoveKeyword(card: Card, keyword: string): void
Game.Buff(card: Card, power: integer, toughness: integer,
  source: string): boolean
Game.BuffUntilEndOfTurn(card: Card, power: integer,
  toughness: integer, source: string): boolean
Game.SetPowerToughness(card: Card, power: integer,
  toughness: integer, source: string): boolean
Game.SetPowerToughnessUntilEndOfTurn(card: Card, power: integer,
  toughness: integer, source: string): boolean
Game.SwitchPowerToughness(card: Card): boolean
Game.SwitchPowerToughnessUntilEndOfTurn(card: Card): boolean
Game.AddCardType(card: Card, cardTypes: flags<Game.CardType>,
  source: string, untilEndOfTurn: boolean): effectId
Game.RemoveCardType(card: Card, cardTypes: flags<Game.CardType>,
  source: string, untilEndOfTurn: boolean): effectId
Game.SetColors(card: Card, colors: flags<Game.Color>,
  source: string, untilEndOfTurn: boolean): effectId
Game.GrantKeyword(card: Card, keyword: string, source: string,
  untilEndOfTurn: boolean): effectId
Game.SuppressKeyword(card: Card, keyword: string, source: string,
  untilEndOfTurn: boolean): effectId
```

## 7. 费用字符串

启动式能力和附加施放费用使用分号分隔：

```text
{1}{U}; Tap; Sacrifice; Exile; Life:2; Discard:1;
SacrificePermanent:1; ExileHand:1; RevealHand:1;
Counter:+1/+1:1
```

编辑器应对费用字符串提供语法高亮和校验：

- 法术力费用使用数据库相同的大括号符号格式。
- `Tap`：横置来源。
- `Sacrifice`：牺牲来源。
- `Exile`：放逐来源。
- `Life:n`：支付生命。
- `Discard:n`：弃牌。
- `SacrificePermanent:n`：牺牲永久物。
- `ExileHand:n`：从手牌放逐。
- `RevealHand:n`：展示手牌。
- `Counter:<名称>:n`：移去来源上的指示物。

`Game.AbilityTrait` 用于声明启动式异能自身的规则特征：

- `None`：无特殊特征。
- `CouldAddMana`：结算时可能向玩家的法术力池添加法术力。
- `RequiresTarget`：该异能需要目标。
- `LoyaltyAbility`：该异能是忠诚异能。
- `MovesCardIntoOrOutOfLibrary`：该异能的费用或效果会使牌移入或移出牌库。

需要组合多个互不重复的特征时使用 `+`。运行时依据 CR 605.1a 计算异能是否为法术力异能，而不是由脚本直接指定结论。只有声明了 `CouldAddMana`，且没有 `RequiresTarget`、`LoyaltyAbility` 或 `MovesCardIntoOrOutOfLibrary` 的启动式异能才不会进入堆叠。`Game.Timing.Mana` 只描述可用时机，不能把不符合条件的异能强制变成法术力异能。

评估 `MovesCardIntoOrOutOfLibrary` 时，只看该异能自身的费用、效果及自身替代效应；不要因为其他来源的替代效应可能改变事件而添加此标志。反之，只要费用或未被其他来源替代后的自身效果会移动牌库中的牌，就必须添加该标志。

例如，一个既可能产费、又会抓牌或以其他方式从牌库移动牌的异能必须进入堆叠：

```lua
Game.RegisterActivatedAbility(
    self.Card,
    "产费并抓牌",
    "添加法术力，然后抓牌。",
    "Tap",
    "ResolveManaAndDraw",
    "",
    Game.Timing.AnyPriority,
    Game.AbilityTrait.CouldAddMana
        + Game.AbilityTrait.MovesCardIntoOrOutOfLibrary
)
```

## 8. 编辑器模板

### 8.1 产生法术力的基本地

基本地的能力也应由 Lua 注册，不要根据牌名、类别或规则文字猜测颜色。

```lua
local Card = { ApiVersion = 2 }

function Card.OnInit(self)
    Game.RegisterManaAbility(self.Card, "{U}")
end

return Card
```

### 8.2 抓两张牌

```lua
local Card = { ApiVersion = 2 }

function Card.OnPlay(self)
    Game.DrawForOwner(self.Card, 2)
end

return Card
```

### 8.3 启动式能力

```lua
local Card = { ApiVersion = 2 }

function Card.OnInit(self)
    Game.RegisterActivatedAbility(
        self.Card,
        "抽牌",
        "抓一张牌。",
        "{1};Tap",
        "ResolveDraw",
        "CanActivateDraw",
        Game.Timing.AnyPriority,
        Game.AbilityTrait.None
    )
end

function Card.CanActivateDraw(self)
    return Game.IsOnBattlefield(self.Card)
end

function Card.ResolveDraw(self)
    Game.DrawForOwner(self.Card, 1)
end

return Card
```

### 8.4 触发式能力

```lua
local Card = { ApiVersion = 2 }

function Card.OnInit(self)
    Game.RegisterTriggeredAbility(
        self.Card,
        Game.Event.PermanentEntered,
        "ResolveEntered",
        "CanTriggerEntered",
        Game.Duration.WhileSourceRemains,
        true,
        "entered-trigger"
    )
end

function Card.CanTriggerEntered(self)
    local eventCard = Game.GetEventCard(self.Card)
    return eventCard ~= nil
        and Game.HasType(eventCard, Game.CardType.Creature)
end

function Card.ResolveEntered(self)
    Game.DrawForOwner(self.Card, 1)
end

return Card
```

### 8.5 异步选择

```lua
local Card = { ApiVersion = 2 }

function Card.OnPlay(self)
    Game.ChooseOptions(
        self.Card,
        "选择一项",
        { "抓一张牌", "获得 3 点生命" },
        1,
        1,
        true,
        "ChoiceCompleted",
        "ChoiceCancelled"
    )
end

function Card.ChoiceCompleted(self)
    local optionId = Game.GetLastChoiceOption(1)
    if optionId == 1 then
        Game.DrawForOwner(self.Card, 1)
    elseif optionId == 2 then
        Game.GainLifeForOwner(self.Card, 3)
    end
end

function Card.ChoiceCancelled(self)
end

return Card
```

### 8.6 按特征角色分派

```lua
local Card = { ApiVersion = 2 }

function Card.OnPlay(self)
    local role = Game.GetFeatureRole(self.Card)

    if role == Game.FeatureRole.Adventure then
        Card.ResolveAdventure(self)
    elseif role == Game.FeatureRole.Primary then
        Card.ResolvePermanent(self)
    end
end

function Card.ResolveAdventure(self)
    Game.DrawForOwner(self.Card, 1)
end

function Card.ResolvePermanent(self)
end

return Card
```

传纪章节可通过 `Game.GetResolvingChapter(self.Card)` 读取正在结算的章节；职业、案件、房间和备法分别使用对应的特殊结构 API。

## 9. 运行时限制

- 单个 Lua 源文件最大 `256 KiB`。
- 单次 Hook 指令预算为 `200,000`，每 `1,000` 条检查一次。
- 允许同步嵌套 Lua Hook，最多 32 层；共享外层指令预算，每次回调至少计入 1,000 条指令。
- 单个脚本实例保留对象图估算上限为 `4 MiB`。
- 单个实例最多保留 `4,096` 个 table、`32,768` 个 table 项。
- 超限脚本会被隔离并停止继续执行。
- 脚本不能访问 Unity 场景对象或任意 C# 类型，只能通过公开的 `Game` API 修改游戏状态。

编辑器不应补全 `UnityEngine`、文件系统、网络、反射或任意 C# 桥接对象。

## 10. 当前边界

现有 API 已覆盖 P0 的脚本装载、能力注册、施放声明、目标、异步选择、区域移动、事件、CR 613 运行时层/时间戳/依赖、CR 616 替代效应重检与玩家选择，以及 V7 特殊结构接入。

以下内容仍不能视为完整 CR 覆盖，编辑器不应把它们宣传为完全支持：

- 所有非战场区域中的特征定义能力，以及不声明 `reads`/`writes` 时自动分析 Lua 条件来推导依赖。
- 需要一次处理多个彼此关联事件的少数替代效应（例如复杂的同时区域移动）仍需逐卡回归。
- 所有复杂隐藏信息选择、任意数量组合目标及特殊牌逐卡回归。
- 每张真实卡牌的具体效果仍必须由对应 Lua 脚本实现。

## 11. 编辑器实现清单

MTGB-DataEditor 的 Lua 功能至少应完成：

1. 根据 `set_code + collector_number` 创建 `CardScripts/<系列>/m<编号>.lua`。
2. 把相对路径写入数据库 `script_path`。
3. 使用本文第 2 节模板生成新文件，而不是只写 TODO 注释。
4. 提供 `Game`、枚举、`self.Card` 和全部 API 的补全与参数提示。
5. 为 Hook 名字符串提供存在性检查、补全和跳转。
6. 为费用字符串、区域、事件、持续时间、目标模式和特征角色提供候选值。
7. 在保存脚本前执行基础语法检查和本文第 3.3 节的静态诊断。
8. 明确显示当前文件绑定的 `set_code`、`collector_number`、`script_path` 和所有相关特征。
9. 正面、背面和副特征编辑页都打开同一个脚本，避免错误地产生 `_2.lua` 或按特征拆分脚本。
10. 保持 UTF-8 无 BOM，并阻止脚本路径逃逸 MTGB 根目录。

## 多库身份与嵌套回调（2026-09-05）

数据库结构为 schema 8，Lua API 保持 2。cardID/ID 是库内整数编号，不保证不同数据库之间唯一。跨库定位卡牌使用系列代号与收藏编号；对局目标使用物件身份。

Lua 发起的事件可以同步调用自定义 Lua 替代条件和处理 Hook；嵌套调用使用独立 VM 栈并共享外层执行预算，最多 32 层。每次回调至少计入 1000 指令预算，不能通过拆成短回调绕过 200000 指令总额。调用结束恢复外层事件上下文。

## 复杂规则接口（2026-09-12，API 2 增量）

```text
Game.RegisterTriggerTargets(card: Card, hook: string, prompt: string,
    mode: Game.TargetMode, minimum: integer, maximum: integer,
    filterHook: string = ""): boolean
```

在 `OnInit` 中为触发 Hook（包括 `OnChapter1` 等章节）声明一组互不重复的目标，数量范围为 0–32。引擎在异能进入堆叠前让操控者选择目标；达不到最少数量时移除该异能。结算时重新检查物件身份与目标合法性：原有目标全部失效则整个异能不结算，部分仍合法则只保留合法目标。零目标的可选声明正常结算。伤害等效果通过既有 `Game.DealDamageToTarget`、目标读取 API 执行，不能再次在结算 Hook 中选择目标。

`Game.CounterTargetStackObject(source)` 反击 `source` 已选择的第一个堆叠对象，可以是咒语、触发式或启动式异能；使用堆叠对象身份，不会把章节来源永久物当作被反击的咒语。施放声明使用既有堆叠目标接口选择该对象。

`Game.WinGame(playerId)` 供脚本在自行检查胜利条件后结束当前双人对局，结果原因为 `CardEffect`；无效玩家或已结束对局返回 false。该接口不自动推导牌面条件，也不实现多人、团队、不能获胜/不能输等比赛变体。

`Game.ChangeCardFace(card, faceIndex)` 切换战场上已有的卡面，重新计算特征并保留物件身份。它不表示施放背面，不提供任意双面牌规则文字的自动解释。离开战场进入手牌、墓地或放逐区时恢复正面。

房间两扇门均有解锁入口，解锁本身为特殊动作，其 Hook 作为触发处理。职业升级支付费用后进入堆叠，结算时才改变等级；被反击或来源离场重进不会升级。案件仅在操控者的结束步骤触发 `OnCaseCheck`，脚本负责检查解明条件后调用 `Game.SolveCase`。备法副本可以取消施放后再次施放，确认施放才消耗准备状态。

回归入口为 Unity 菜单 `MTGB/Validate Complex Rules`，并已纳入 `MTGB/Validate Project`。这是权威规则命令及视图往返验证，真实网络和新版战场鼠标交互另行验收。

## 扩展包中的 Lua

`.mtgbpack` 为 ZIP，使用 `manifest.json`、`cards.mtgbdb`、`scripts/`、`images/`。包内手写脚本路径相对包根，例如 `scripts/m65.lua`；无需系列子目录。自动生成 Lua 同样保存在 `scripts/`，数据库扩展表保留生成来源和校验快照，运行时执行文件。包内加载只使用该包资源，不能通过路径访问包外文件。Lua API 和效果行为不随打包变化。
