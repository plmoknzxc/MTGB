# 机制效果验证清单

这是缺口清单，不是“全部卡牌验证通过”的证明。

扫描范围：当前本地 Forge 卡牌、衍生物和辅助效果脚本；固定版本关键词、触发、替代、持续异能枚举、费用解析器；CR 标题和说明词库。
按脚本字段发现引用；没有直接引用的机制仍保留。未识别脚本声明单独留在 JSON，不能当作已覆盖。
引擎事件、参数、费用和期限是结构维度，不是独立的玩家关键词。类别互相重叠，不能相加当作机制总数。引用卡数不等于包含该机制的完整卡数。

测试在预置局面中通过实际 ForgeBridge 请求施放/激活、支付和结算；不证明 Unity 点击、显示或联机交互正确。
未登记表示此清单没有经复核的效果断言，不等于确定未实现；其他已有测试可能尚未登记。每个 PASS 只支持该场景列出的断言。

Forge 版本：`4ec5f1a2c32fa90ecb983a72b9eb47aa5c5d7676`；扫描脚本：33980

先看 [共用交互与结算表现](families.md)：录像参考、规则断言、Unity 检查与未支持流程分别记录。

## 分类范围

| 分类 | 条目 | 有直接脚本引用 |
|---|---:|---:|
| 其他脚本模式 | 116 | 116 |
| 异能词／待核对文本标签 | 599 | 599 |
| 引擎操作 | 191 | 191 |
| 持续异能类型 | 96 | 87 |
| 持续期限 | 27 | 27 |
| 替代事件 | 40 | 34 |
| 机制／效果条目 | 346 | 264 |
| 脚本参数 | 1284 | 1284 |
| 触发事件 | 148 | 138 |
| 费用类型 | 53 | 43 |

未归类声明：100 种；尚未复核登记的测试脚本：11 个。
脚本参数单列用于查遗漏；参数出现不证明整项效果已经测试。异能词／文本标签按规则说明开头的标签提取，仍需核对分类，不能直接当官方关键词。
目标是否合法、替代选择顺序、持续效果层次、权限到期、多方交互等组合仍需专门场景。

## 状态汇总（结构条目，不能当机制总数）

- 未登记效果测试：2582
- 需重测：318

## 优先复核

| 机制 | 中文 | 状态 | 代表卡 |
|---|---|---|---|
| Delayed triggers |  | 需重测 | A Good Day to Die, A-Dorothea, Vengeful Victim |
| Perpetually |  | 需重测 | Absorb Energy, Accident-Prone Apprentice |
| Conjure |  | 需重测 | A Golden Opportunity, Ace Flockbringer |
| Morph | 变身 | 需重测 | Abomination of Gudul, Abzan Guide |
| Seek |  | 需重测 | Adherent's Heirloom, Anguished Recollection |
| Suspend | 延缓 | 需重测 | Aeon Chronicler, Ancestral Vision |
| Spellbook |  | 需重测 | Adaptive Armorer, Arms Scavenger |
| Infect | 侵染 | 需重测 | Blackcleave Goblin, Blight Mamba |
| The Ring Tempts You | 魔戒引诱你 | 需重测 | Bilbo, Retired Burglar, Birthday Escape |
| Disguise | 伪装 | 需重测 | Alley Assailant, Arno Dorian |
| Venture into the Dungeon | 深入地城 | 需重测 | A-Acererak the Archlich, A-Cloister Gargoyle |
| Draft |  | 需重测 | Adaptive Armorer, Arms Scavenger |
| Manifest Dread | 显化惧象 | 需重测 | Abhorrent Oculus, Bashful Beastie |
| Earthbend | 运土 | 未登记效果测试 | Aang, at the Crossroads, Avatar Kyoshi, Earthbender |
| Incubate | 抚育 | 未登记效果测试 | Assimilate Essence, Blight Titan |
| Manifest | 显化 | 需重测 | Arashin War Beast, Cloudform |
| Firebending | 振火 | 需重测 | Avatar Aang, Azula, Cunning Usurper |
| Waterbend | 截水 | 需重测 | Aang's Iceberg, Aang, Swift Savior |
| Initiative |  | 需重测 | Aarakocra Sneak, Avenging Hunter |
| Airbend | 截气 | 未登记效果测试 | Aang, Airbending Master, Aang, Swift Savior |

完整清单与每项断言见 [coverage.csv](coverage.csv) 和 [coverage.json](coverage.json)。

尚未复核登记的测试脚本也列在 JSON 的 testsNotReviewedForThisManifest，不能静默遗漏。

本轮日志、请求轨迹和局面快照保存在项目 `.tools/mechanic-coverage/runs/`，不随仓库提交。
