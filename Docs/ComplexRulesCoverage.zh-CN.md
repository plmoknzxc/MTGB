# 第四阶段：已验证规则范围

日期：2026-09-12。目标为权威规则核心与通用 DIY API；不宣称覆盖全部万智牌卡牌、全部 CR 组合或新版战场的真实双端交互。

## 验收入口

- 专项：`MTGB/Validate Complex Rules` / `ComplexRulesValidationMenu.ValidateForAutomation()`。
- 全项目：`MTGB/Validate Project` / `MTGBValidationRunner.ValidateFromCommandLine()`。
- 自动化可调用 `ScheduleForAutomation()`，结果写入项目 `Temp`，避免 Pipeline 单次主线程请求的 5 秒超时造成误判。
- 专项包含 27 个命名测试组，其中多组分别执行两席、正反顺序或多个子场景。任何 Lua Error、Exception、Assert 都使对应组失败。
- 用例使用现有 TST 卡及内存构造的代表卡/Lua，通过实际权威动作、选择和优先权推进验证。未新增需要分发的数据库、脚本或卡图。

## 机制与证据

| 范围 | 已验证行为 | 测试 |
|---|---|---|
| 历险 | 被反击进墓地；放逐被替代为回手时没有后续施放许可；现有正常放逐及再次施放流程 | AdventureCounter、AdventureReplacement；既有 Specialist 专项 |
| 传纪 | 两席在章节入堆叠前选择目标，不可取消必选目标；离场重进使目标失效；全失效不结算，部分合法继续结算；可选零目标；无必选目标移除章节；最后章节无目标时及时牺牲 | SagaTargets、ChapterTargetBoundaries |
| 章节堆叠 | 多章节选择顺序；最后章节被反击后的牺牲；复制保留目标；Lua 反击的是章节对象，永久物保留；拒绝过期堆叠编号 | SagaMultiple、SagaCounter、ChapterCopy、LuaChapterCounter |
| 替代效应 | SetAmount 与 MultiplyAmount 的非交换顺序由受影响玩家选择，含第二席；每次重检且同一替代不重复应用 | MixedReplacements；既有替代分类、链接清理专项 |
| 同时区域移动 | 两席永久物批量进墓地；准备阶段完成后统一提交；替代选择期间身份变旧时整批拒绝，不移动同区域的新物件 | SimultaneousMoves；既有 ValidateAtomicMultiCardZoneMove |
| 持续效应 | 控制权、类别、颜色、关键字四层显式依赖优先于时间戳；来源离场失效，重进不能重新绑定旧效果 | ContinuousDependencies；既有结构依赖、复制/牌面、攻防子层专项 |
| 费用 | 可以支付恰好等于剩余生命的费用；取消不支付；支付到 0 后先执行状态动作，咒语不先结算回血 | LifePayment；既有费用锁定与支付专项 |
| 房间 | 施放另一扇门后，主门仍有解锁入口；通过 ActivateAbility 命令解锁；解锁本身为特殊动作，相关 Hook 进入触发流程；离场清除门状态 | SpecialFeatureFlows |
| 预兆与连体 | 选择副特征、取消、支付、响应窗口及结算；预兆洗回牌库，连体进墓地 | SpecialFeatureFlows |
| 职业 | 命令启动后支付费用并入堆叠，结算才升级；反击或离场重进不升级；离场重置等级 | SpecialFeatureFlows、ClassCounter |
| 案件 | 仅操控者的结束步骤触发检查；脚本解明后切换状态；离场清除已解明状态 | CaseAndFaces |
| 备法 | 放逐副本有合法物件编号与区域；取消可再施放；施放消耗准备；可再次准备；离场或停止准备清理副本；控制权转移更新权限；法术副本遵守时机限制 | SpecialFeatureFlows、PreparedControl |
| 双面 | Lua 换面更新特征而不更换物件身份；回手恢复正面；战役背面施放继续由既有独立回归验证 | CaseAndFaces；既有 GameChoice 战役专项 |

## DIY 表达能力审计

| 需求 | 实现方式与边界 |
|---|---|
| 修改手牌上限、每回合下地数、视同闪现施放 | 已有 `RegisterRuleEffect`，验证玩家范围、数值/许可查询及 WhileSourceRemains、UntilEndOfTurn 生命周期。不是任意字符串都能改变引擎规则。 |
| 章节或其他触发指定目标 | 新增 `RegisterTriggerTargets`，按 Hook 声明一组目标、数量与过滤条件；不通过卡名或编号写 C# 分支。 |
| 反击咒语/异能 | 新增 `CounterTargetStackObject`，与既有高级堆叠目标声明组合；`CounterSpell` 继续保留。 |
| 条件满足时获胜 | 新增 `WinGame(playerId)`；Lua 检查条件后调用。验证双席获胜、CardEffect 原因、结束后不能覆写以及视图往返。 |
| 切换已有卡面 | 新增 `ChangeCardFace`；只切换战场已有面，不自动解释牌面文字或等同施放背面。 |
| 多人、团队、指挥官、不能赢/不能输等比赛变体 | 后续工作；不把直接 WinGame 接口宣传为这些变体已实现。 |

## 本轮修复

1. 历险只有实际进入放逐区后才授予后续施放许可。
2. 支付生命检查允许支付全部剩余生命。
3. 联机第二席能够选择不同替代效应的顺序。
4. 增加触发目标队列、结算合法性检查与通用堆叠对象反击；队列及目标声明计入确定性状态。
5. 批量区域移动提交前检查区域变更版本。
6. 修复房间主门入口、职业升级时机、案件的结束步骤归属。
7. 修复备法物件登记、取消后的副特征保留、当前操控者权限、法术时机及副本清理。
8. 增加换面与脚本胜利 API，以及非战场区域的卡面重置。
9. 联机协议升级为 v9，避免与旧规则状态/结束原因混用；数据库仍为 schema 8，Lua API 仍为 2。

## 不包含的验收

- 本轮未做真实网络传输、两个独立客户端的鼠标操作或新版战场视觉验收；这些属于第三阶段。
- 显式依赖与 reads/writes 元数据不等于自动解析任意 Lua 条件并推导全部 CR 613 依赖。
- 相互关联的多事件替代、任意多组触发目标及每张真实卡牌的全部规则文字，仍需对应脚本及逐卡验证。
- 当前代表卡在自动测试中构造，不是可从 Data Editor 打开的新示例牌库。作者教程、模板与试牌工具属于第二阶段。

规则参考：[官方规则入口](https://magic.wizards.com/en/rules)、[Class 发布说明](https://magic.wizards.com/en/news/feature/bloomburrow-release-notes)、[Room 发布说明](https://magic.wizards.com/en/news/feature/duskmourn-house-of-horror-release-notes)、[Prepare 发布说明](https://magic.wizards.com/en/news/feature/secrets-of-strixhaven-release-notes)。测试结果约束的是上表列出的行为，不能外推为全部卡牌规则已支持。

## 最终运行结果

- 2026-09-12 14:55（Asia/Tokyo）：重启后的 Unity 6000.3.22f1 已编译最新代码；复杂规则 27 组通过，全项目验证返回 PASS，并出现 `MTGB project validation passed.`。
- 两套 Boundary 牌组各 16 场，共 32 场对局，全部回放匹配；数据库、多库、边界卡、选择、确定性及协议验证一并通过。
- Data Editor 构建 0 警告/0 错误，自检进程退出码 0；`Check-AuthoringSdk.ps1` 确认 schema 8、API 2 与两端 SDK 文档一致。
- 最终运行方式为编辑器延迟调度，全量验证期间会占用主线程。本轮曾中断独立批处理，不能把那次中断计为通过；后续长回归应使用独立批处理，避免阻塞交互编辑器。
- 本地证据保存在 `.codex/validation/phase4/`，不提交测试日志、预览图或本验收文档。当前改动尚未提交。
