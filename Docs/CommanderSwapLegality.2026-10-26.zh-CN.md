# 法禁局间换主将：拍档、背景与行侣复查

2026-10-26，接续F01的局间合法性增量。本轮不恢复长期goal或用户后置项；不打包、覆盖客户端、部署服务器或发布Git。

## 结论与修复

服务器的生产 `HandleDeck` 已依次核对原注册100张牌池、指定行侣不变、原生主将构筑、原标识色及原生行侣条件，再更新权威牌组。拍档、指定拍档、选择背景、博士助伴不能只凭“都是传奇牌”拼接；更换后必须重新符合组合规则。

本轮发现一处真实漏洞：`Raised by Giants` 这样的非生物背景被单独留下时，Forge原先仍认为它是合法主将。`CardRules.canBeCommander()` 将背景列为候选是为支持第二主将，`DeckFormat.getDeckConformanceProblem()` 却没有区分单张与合法双主将上下文。修复该原生构筑入口：单独指定非生物背景、又没有明确允许担任主将的文字时，拒绝构筑。合法背景搭配及本身是传奇生物的 `Faceless One` 保留。同步 `NativePatches.json`，候选源码与按清单重新叠加的源逐字一致，原有补丁保持。

依据：[Wizards背景规则更新](https://magic.wizards.com/en/news/announcements/comprehensive-rules-changes-6-1-22)、[CLB发行说明](https://magic.wizards.com/en/news/feature/commander-legends-battle-baldurs-gate-release-notes-2022-06-01)。

## 行侣为何通常不会因单纯换主将而失效

行侣条件检查的是全部起始100张，包含主将与背景，而非只检查牌库的99／98张。严格保留原100张和原行侣时，只在主牌与主将之间移动这些牌，不会改变行侣检查的卡池。不能把一张原先违反行侣条件的牌放进统帅区来绕过限制。

服务器仍会重新检查：合法的 Kenrith／Niv-Mizzet Reborn 更换保留 Jegantha 成功；包含重复同种费用符号的 Wrath of God 的对抗输入被原生行侣检查拒绝；删除原行侣、换成 Zirda 或加入注册池之外的牌均不能提交。无效行侣的原注册状态属于可信夹具特意构造的对抗输入，不称为可正常注册的牌组。

法禁可在原100张内局间更换主将，但不能换标识色、增加外部牌或将行侣与主牌互换。见[法禁规则407](https://www.duelcommander.org/rules/duelcommander_comprehensiverules/)、[行侣FAQ](https://www.duelcommander.org/faq/)。

## 证据

目录：`.tools/duel-swap-boundaries-1026/`。

- `native-baseline/result.json`：13个真实原生构筑输入，唯一规则失败为单独非生物背景被接受。
- `native-candidate/result.json`：原13个输入修复后全部通过，覆盖背景更换生物／返回主牌／缺少选择背景、单主将与双拍档互换、指定拍档不匹配、博士与不同助伴、无博士的双助伴、非传奇生物。
- `extra-candidate/result.json` 有效五项及 `extra-tail/result.json` 一项：Friends forever合法搭配、不与普通Partner混搭、同标识色更换，以及Commander／Brawl的单独传奇生物背景正例与非生物背景反例。
- `authority-result.json`：16个局间输入经过实际生产处理器和异步原生检查；接受时核对准确提交的分区，拒绝时原权威卡组保持。覆盖原100张池／行侣／标识色、合法背景与拍档互换、非法组合、博士助伴以及行侣正反例。无UTP传输，不冒充完整联网BO3新证明。
- `authority-companion-tail.json`：只补第一次驱动等待不足的无效行侣输入，三个检查通过。此前48项检查有一项“在70秒内完成”失败，拒绝且未修改权威卡组的原断言已通过；尾段延长测试等待预算，不修改生产超时或规则，也不重跑其他通过分支。
- `ui-result.json`：19项实际射线／点击检查通过。局间界面显示生物与背景，允许先移回原生物形成中间草稿，拒绝给背景配没有“选择背景”的Yeva，再加入Skanos完成合法搭配；总100张保留、原生复检、撤销回原组合、准备后锁定和观众无编辑权限分别检查。只验证既有布局的新角色操作，复用F01同窗口布局与本轮前一批三配置证明，不新增全套字号或联网效果结论。

中间草稿允许暂时没有主将或只剩背景，便于用户完成更换；这不代表服务器会接受它作为下一局牌组。原本合法的背景在移入主牌后也不会继续获得第二主将身份。

## 驱动纠正与运行身份

保留原失败，未把整份FAIL改写为PASS。Friends forever补例最初误用了不同标识色的组合，原生正确拒绝；只更正为相同标识色的Chief Jim Hopper／Max并补尾段。牌表目录使用原版Stranger Things名称，未把未收录的Universes Within别名误当卡牌失效。首次C#探针因重复Newtonsoft类型编译失败，改为反射调用明确程序集；未修改生产程序集。

为让生产C#原生检查器调用到候选类，本轮在确认无在线会话后，仅临时替换开发运行包的 `DeckFormat.class`，未更改运行清单。测试结束已恢复原class哈希 `5092b1af01182e12d97c515458dda28a02a63d3cc37f4951317dc37e24a896b9`、保留原运行包哈希 `004e52f0719249863f05b55a49c3ff45b3a9f647caf3dcf7cc62d9c58fa7f6d6`，并清理测试候选检查缓存。候选class哈希及恢复验证见 `runtime-class-override.json`、`preservation.json`。编辑器无新增Console Error。

因此本轮已完成源码修复与有界验证，**候选尚未正式写入运行包，已安装客户端和服务器尚未更新**。旧F01—F05的开局、施放、税、回区和实际局间会话结果保留各自身份，不为本次构筑守卫重跑全部效果。

批末仅刷新机制分类一次，不带 `--run`；旧2582未登记关联／318需重测及用户后置项保持原口径，不作为本轮新增测试配额。
