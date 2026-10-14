# 结算中的额外法术力支付与单项确认

## 本轮范围

用户要求先展示当前单项确认，随后按录屏把支付的取消／自动支付放在右下推进按钮位置。未重新设计生命支付的确认窗口，未重跑随机动画或全卡池；长期机制 goal 保持暂停。

参考源：`C:/Users/Sam/Videos/Overwolf/Outplayed/MTG Arena/MTG Arena_10-04-2026_10-16-38-217/MTG Arena_10-04-2026_11-6-47-633.mp4`。48:30 的实际帧是施放付款，上方蓝色取消、下方橙色自动支付；该帧支持控件布局，不能作为 Mana Leak 结算时额外费用的规则证据。本轮另用真实 Forge 操作证明后者。

图工具的项目查询和覆盖调用均返回 Transport closed，因此读取准确源文件，结论仅限下述路径。`MechanicCoverage.py` 只刷新结构清单，没有执行全量效果任务；清单中“需重测”保留工具总指纹失效状态，不把局部通过改写成全量 PASS。

## 当前单项确认

真实施放 Hallowed Fountain，原生请求为 `InputConfirm`，提供 `ok`／`cancel`。现有表现是中央的“是否支付 2 生命”是／否窗口，无来源牌图；正文仍是英文 `Do you want to pay 2 life?`，这是本轮截图揭示的既有本地化缺口。保留现状供用户判断，不宣称确认正文三语完整。

截图：`.tools/payment-dock-1005/zh-CN-single-shockland.png`。截图通过真实原生请求重放取得，非联机整局录像。

## 修改

- 新共用 `ForgeBattlefieldController.Payment.cs`：原生 PayMana 请求显示右下两按钮，取消在上、自动支付在下，费用使用 `CardSymbolText` 图标；没有原生自动选项时禁用自动支付。沿用原生准确编号与请求身份，不按客户端地／池数量推测可支付性。
- 保留手动点地与法术力池。通用窗口及施放牌图旁不再重复显示取消／自动支付；仍保留其他需要的原生选项。
- 支付期间隐藏原推进按钮和阶段短条，结束后恢复；两个按钮与 AUTO 开关留出间距。使用现有切角按钮图形，取消采用蓝色变体。
- AUTO 在没有本地施放意图或该咒语已上堆叠时等待明确付款决定。正常本地施放的自动付费保留。协议中两种用途都使用 `InputPayManaOfCostPayment`，没有独立用途字段；重连丢失本地意图时也会等待点击，不猜测费用目的。

最终截图：`.tools/payment-dock-1005/final-tax-auto-zh-CN.png`，等待入场动画完成，前牌是 Mana Leak、后牌是 Opt。

## 独立规则结果：六个真实场景

Java 夹具只安排手牌／地，后续施放、支付、目标与结算由 Forge 处理。可复用驱动：`Tools/ForgeBridge/PaymentDockNativeValidation.py`，夹具 `PaymentReviewFixture.java`；原始本轮驱动副本保留在 `.tools/payment-dock-1005/`，断言与原始轨迹位于其 `native/`。新检出先执行 `python -X utf8 Tools/ForgeBridge/PaymentDockNativeValidation.py` 生成六个原生场景，再运行本轮 Unity 入口；不要直接依赖其他机器的缓存文件。

| 场景 | 原生结果 |
|---|---|
| Hallowed Fountain 支付 2 生命 | 18 生命，地未横置 |
| Hallowed Fountain 不支付 | 20 生命，地横置 |
| Mana Leak 额外费用自动支付 | Opt 结算并抓一张，自己的岛共横置 4 张 |
| Mana Leak 取消付款 | Opt 被反击、不抓牌，自己的岛共横置 1 张 |
| Mana Leak 法术力不足 | 付款请求仅有取消；取消后 Opt 被反击、不抓牌 |
| Mana Leak 手动逐张点岛付款 | Opt 结算并抓一张，自己的岛共横置 4 张 |

四个 Mana Leak 场景均核对准确 Opt 牌号进入坟墓场、双方生命、牌库数量差异和横置岛数量。这里的抓牌与灵视请求不等于所有占卜边界的首验。

`native/results.json` 保存最后一次四个额外费用场景运行；早先两个生命支付场景的请求、结果与轨迹独立保留。`Tools/ForgeBridge/PaymentDockProof.py` 重新核对六个已保存的真实结果并生成 `proof.json`，不伪称又运行了一轮。证明文件包含受影响源码和输入结果的 SHA256。

## Unity 增量证据

`PaymentDockPresentationValidation.Run()`：中／英／日三种界面语言，真实请求重放；真实 EventSystem 射线点击核对原生编号、重复／过期／非行动方／提交中拦截、费用不足禁用、固定字号与标签边界、费用图标、右下矩形间距、无重复支付窗口、查看战场／返回字号及普通优先权恢复。直接调用实际自动控制方法，并为 controller 与 service 同步设置同一请求；检查无施放意图及咒语已上堆叠两种额外付款不会被 AUTO／AUTO OFF 跳过，正常本地施放可自动付一次，相同费用不自动重试。

受影响消费者只跑 `ManaResultPresentationValidation.RunPoolOnly()`：手动池准确颜色与原生编号、施放预览、付款失败后重试、控制对手后恢复、过期／重复／非行动方拦截及三语图标布局。结果 `.tools/mana-glyph-review-1004/result.txt`，本轮复制先前结果到 `.tools/payment-dock-1005/pool-previous-result.txt`。

两套均 PASS；另在真实税款请求中以 EventSystem 射线点岛 `card:122`，确认准确提交选项 2 且重复点击被拦截，记录在 `manual-land-ui.txt`。首轮失败原样保留：

- `overlap-ui-result.txt`／`overlap-attempt.png`：新自动支付按钮与 AUTO 开关重叠。支付组上移 14 像素，支付期间隐藏阶段短条；后续实际矩形与点击通过。
- `auto-driver-ui-result.txt`：驱动只给 controller 设置请求，service 的旧请求保护拒绝提交。补齐相同 service 请求后通过；该失败不属于产品自动付款退化。
- 原生驱动最初未识别 Scry 的 `scry-top`／`confirm-scry` 编号，修正后四个税款结果通过；保存的原生 attempts 仅证明驱动修正经过，不冒充规则失败。

## 未验证边界

本轮没有联机往返时序、四人局、所有费用类型、雪境／混血／非瑞克西亚／受限法术力，也没有完整确认文本翻译或所有牌的动画等价性证明。实际引擎操作和 Unity 请求重放是两份独立证据；不是端到端联机测试。该支付 UI 改动可复用给同类原生请求，但不据此宣布全部机制已验证。

skill 的 `ui-reuse.md` 增补付款用途与驱动状态反例，并以真实失败后复核结果检查修正；登记 `payment-dock` UI 套，法术力来源与支付流程引用它。
