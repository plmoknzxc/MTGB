# 战场布局、叠放与费用门槛修正

## 参考

用户录屏：`C:/Users/Sam/Videos/Overwolf/Outplayed/MTG Arena/MTG Arena_09-22-2026_3-48-32-806/MTG Arena_09-22-2026_3-53-44-666.mp4`。
查看全片取样及 01:30 双地画面；取样仅用于观察，没有将录屏画面导入游戏资源。

## 原因及改动

- 之前把整张竖版牌转 90°，与参考中紧凑的名称/插画牌面、轻微斜转、横置箭头不同。现在使用现有完整卡图的上部 UV 区域保留原卡框，桌面倾斜 X 26°、Y 14°，横置在平面内转 -12°，叠加半透明箭头；手牌/详情保持完整卡面。地牌从行左侧排列。
- 之前 `choosingIndividual` 对所有非优先权请求都成立，导致支付请求拆开双方所有组。现在只在目标/攻击/阻挡请求中展开含候选的组；同状态基本地直接操作代表牌，支付不强制展开，撤回后自然按最新状态归并。
- Forge 的 `getActivateAction` 只表示可以提出操作，不表示费用足够。桥接现在同时使用 Forge 已计算的 `actionableCards` 集合，过滤优先权阶段不可操作的手牌；费用和结算仍由 Forge 执行。

## 验证

- `python Tools/ForgeBridge/Test-Affordability.py`：真实 Forge 对局，两地不提供 Hill Giant；两地逐一横置并撤回，恢复未横置和空法术力池；四地正常支付施放，双方让过后进入战场。通过。
- `python Tools/ForgeBridge/Test-ShockInteraction.py`：无地不提供 Shock；下地后正常选目标、支付、双方让过并造成 2 点伤害。通过。
- `python Tools/ForgeBridge/Test-LandAbilities.py`：山脉单击直接产红、西瓦暗礁多能力选择产无色。通过。
- `ArenaPresentationValidation.Run()`：展示测试验证支付不拆双方组、全部撤回合成 ×5、横置符号/角度、手牌遮挡、拖放和密集场面；使用显式合成状态，不替代上面的真实规则验证。通过。
- `ForgeBattlefieldValidation.Run()`：Unity 场景真实 Forge 对局，下地、闪电击选目标、支付、双方让过，生命 20 → 17；重复响应及非法目标检查通过。

## 边界

Forge 的 actionable 集合是引擎现有的预测结果，存在扫描超时和复杂费用的预测边界。这次验证覆盖普通法术力支付，不代表所有替代费用、减费、额外费用都已验证。复杂牌框/双面牌的紧凑裁切需要后续逐类核对。
