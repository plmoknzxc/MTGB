# 抓牌步骤、空牌库与预防到期：R45 有界关闭

日期：2026-10-07。七条真实原生流程与一套新结果界面验证通过；累计386原生首验、35UI首验。仅关闭原R45行，开放包54→53，关闭11→12，21部分包保持。

## 原行与断言

`R45 / reuse / P1 / 抓牌步骤豁免空牌库与预防到期 / replacements,outcome`

原边界：**第一张豁免、空牌库/输局、预防跨回合到期**。

| 场景 | 实际验证 |
|---|---|
| draw-first-thief | Real turn2 own DRAW: normal first card is exact exempt library-top ID; Howling Mine extra draw alone offers affected-seat2 native order. Thief chosen redirects extra draw to exact seat1 top ID, no Treasure; both full library suffixes, private hands/life20/pool0 and draw-step priority checked. |
| draw-first-token | Same real turn2 first-card exemption and extra draw order; Hullbreacher chosen creates exactly one new seat1 Treasure ID, no redirected draw; seat2 keeps normal first ID, both full library suffixes/private hands/life20/pool0 and draw-step priority checked. |
| empty-loss | Actual Reach Through Mists cast and U paid using exact Island on library0; attempted draw produces native ended winnerSeat2, both final views, zero library/life20/pool0/source tapped; no outcome injection. |
| empty-lab-win | Same actual Reach/U/empty draw with opening Laboratory Maniac; native win replacement produces ended winnerSeat1 and both final views with library0/life20/pool0/Island tapped; no outcome injection. |
| empty-skip | Actual Reach/U on library0, native optional Familiar ok skips draw; spell exact ID grave, both full libraries unchanged, hand0/life20/pool0 and main priority restored without ended. |
| prevention-expiry | Actual Ethereal Haze cast/W and native opponent Sorcerer T activation/target seat1: creature-source damage0/life20 in turn1; same source naturally untaps, real turn2 UPKEEP T activation deals1/life19 after EOT. Exact IDs/payment, both libraries unchanged before draw and priority restored. |
| empty-skip-decline | Actual Reach/U/empty library with Familiar; native optional cancel declines skip and original attempted draw produces ended winnerSeat2, both final views/library0/life20/pool0/Island tapped. |

夹具只安排初始手牌、来源永久物与牌库长度，读库观察不修改结算。真实施放、支付、选择、伤害与轮序产生结果；未验证这些初始来源本身的施放/进场。运行包哈希 `f9cf6599e46473ecb816f63ee85f100bda9698724e79cab39d1b1e5150bd6316` 未变。

## 界面与复用

替代顺序新旧真实请求逐字段核对数量、顺序、原生ID、确认/reset门槛和完整已知来源正文；变化为操作席位2，引用既有第二席位/其他玩家守卫。Familiar的optionalReplacement、ok/cancel、操作及受影响席位、完整正文与旧请求相同，空牌库及结果变化由七条原生断言负责。U/W支付、原生玩家目标、生命/珍宝展示沿用原控件证明。逐项来源见 [合同比对](../.tools/draw-boundaries-1007/ui-reuse.json)。

新 `DrawBoundaryPresentationValidation` 读取两个实际原生ended封包，各自重放两个最终席位，共四格。当前中文胜者标题、继续查看/返回结果真实射线点击、公开放逐60个准确ID只读浏览、不可提交规则选项、面板边界及切换字号稳定全部通过。这是UI适配器重放，不增加原生效果或当前联网证明。

发现并修复两个产品问题：结果按钮继承自动缩放；只读卡牌浏览遮罩阻挡设置旁边返回结果按钮。仅修改 EndReview.cs，分别关闭best-fit并应用现有字号偏好、提升该快捷按钮Canvas层级，原位置保留。旧结果套直接onClick.Invoke未覆盖实际遮挡，新套按真实射线核对。未改共用支付/替代/目标控件，不重跑无关套。

最终结果：`.tools/draw-boundaries-1007/ui/20261006T2015453317623Z/result.txt`；四步与四图保留，胜者画面已实际查看。旧manual/auto真实网络日志仅历史未版本化旁证，没有宣称本轮实时传输/全部语言通过。

## 原失败与版本

原生首次驱动错认OrderReplacements（实际OrderAbilities），预防到期驱动错在UPKEEP要求已有DRAW；两处属于驱动断言错误，原失败保留。前五成功不重跑，只重试预防，最后仅新增Familiar拒绝场景。三个成功驱动身份全部冻结；聚合不改写旧整套FAIL或伪称当前驱动整套通过。

两个UI失败分别保留字体和真实射线问题及当时源码；最终四格通过。Unity工具超时后核对同一次Running=false及已写PASS，没有再次运行。结束时IntegratedBattlefield退出Play，控制台0错误。

## 影响与剩余

源码、输入、结果、截图、失败及历史复用证明哈希集中见 [冻结证据](../.tools/draw-boundaries-1007/result.json)。当前图Verify证据有排除/变更范围，已精确读源；不作全项目穷尽声称。其他抓牌谓词、可选搜索、多方/控制、其他期限、其他替代组合和语言缺口仍留A02/A03/R44/R46/R49/R19/R36/R47等原归属；R45闭包不扩为所有抽牌/预防通过。
