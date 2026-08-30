# MTGB Lua API 与数据编辑器接入规范

本文档对应：

- MTGB 数据库 Schema：`7`
- MTGB Lua API：`2`
- Lua 运行时：Lua-CSharp
- 数据编辑器仓库：`D:\WorkSpace\MTGB-DataEditor`

本文档主要供 **MTGB-DataEditor 实现 Lua 文件创建、代码补全、参数提示、模板生成和静态诊断**。卡牌作者也可以把它作为脚本参考。

实际运行时代码是最终依据：

- `Assets/Scripts/Scripting/LuaCSharpCardScriptRuntime.cs`
- `Assets/Scripts/Scripting/CardScriptApi.cs`
- `Assets/Scripts/CardDefinition.cs`
- `Assets/Scripts/Core/GameEventRuntime.cs`

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
    ApiVersion = 1
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

`Game.ApiVersion` 是当前客户端公开的 Lua API 版本号，当前值为 `1`。

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
- 使用高于 `1` 的 `ApiVersion`。
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

`Game.CardType`、`Game.Supertype`、`Game.Color` 和 `Game.AbilityTrait` 是标志集合。需要组合时可使用 Lua 位运算符 `|`。
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
Game.ChangeController(card: Card, playerId: integer): boolean
Game.GetOpponent(playerId: integer): integer
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
按位或组合，例如 `Game.TargetKind.Permanent | Game.TargetKind.Player`。
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

需要组合多个特征时使用 Lua 位运算符 `|`。运行时依据 CR 605.1a 计算异能是否为法术力异能，而不是由脚本直接指定结论。只有声明了 `CouldAddMana`，且没有 `RequiresTarget`、`LoyaltyAbility` 或 `MovesCardIntoOrOutOfLibrary` 的启动式异能才不会进入堆叠。`Game.Timing.Mana` 只描述可用时机，不能把不符合条件的异能强制变成法术力异能。

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
        | Game.AbilityTrait.MovesCardIntoOrOutOfLibrary
)
```

## 8. 编辑器模板

### 8.1 产生法术力的基本地

基本地的能力也应由 Lua 注册，不要根据牌名、类别或规则文字猜测颜色。

```lua
local Card = { ApiVersion = 1 }

function Card.OnInit(self)
    Game.RegisterManaAbility(self.Card, "{U}")
end

return Card
```

### 8.2 抓两张牌

```lua
local Card = { ApiVersion = 1 }

function Card.OnPlay(self)
    Game.DrawForOwner(self.Card, 2)
end

return Card
```

### 8.3 启动式能力

```lua
local Card = { ApiVersion = 1 }

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
local Card = { ApiVersion = 1 }

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
local Card = { ApiVersion = 1 }

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
local Card = { ApiVersion = 1 }

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
- 不允许嵌套执行 Lua Hook。
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
