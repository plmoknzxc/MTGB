# 费用及当前时机均可用才展示（2026-10-25）

用户已明确选择严格模式。本记录取代 hand-sections-notes.md 中上一轮“未获明确答复、先按使用许可”的暂定行为。复制场景的手牌、指挥官继续正常保留；坟墓场和放逐区只显示引擎当前优先权选项已确认能支付的牌。disabled、requiresPaymentCheck、非优先权输入或没有当前正向证据的候选均不进入这两个来源。分区标题顺序与常态颜色保持上一轮决定。

客户端对自己的当前请求直接按原生选项判断，不以较宽松的 playableGraveyard/playableExile 许可表替代。桥接新增每位玩家的 usableSourceIds：从现有 pending 优先权选项提取已确认支付的牌 ID，不运行第二套费用估算；请求 consumed、非优先权或对局结束时为空，物理行动玩家由 pending.gui.controller 决定。区域遍历只取坟墓场/放逐区。公开牌的即时可用状态按本轮要求供双方显示；只有获授权接收者可收到自己的暗置已知牌 ID，不发送手牌候选或支付材料。没有新的效果执行或自动施放。

对手与观战复用该资格元数据；观战再与已经公开的坟墓/放逐实体 ID 求交，排除暗置牌和其他区域 ID。候选卡面可从同一快照的公开区域取回，涵盖使用对手拥有牌的许可。新录像保留该时点元数据；旧录像仅从对应视角已录下的、可支付的原生优先权选项恢复，并限制区域和可见性。旧录像没有这些证据时隐藏，而不猜测。原录像对象不被修改。

## 原生证据

候选类编译到 `.tools/strict-source-1025/classes`，没有覆盖 ForgeRuntime/classes。基础 runtimeHash 为 004e52f0719249863f05b55a49c3ff45b3a9f647caf3dcf7cc62d9c58fa7f6d6；ForgeBridge.java SHA256 为 D72C3F73CAA8F9A4BA68ACA6A4914E198735F8D96F09F6245A13B372A838D993，候选 ForgeBridge.class 为 E35F087DEEC63CCE02EF17C2BC032AB5E3C28C54180E5CE933CC9423139CFFCF。Java 17 编译成功，保留原有 unchecked 提示。

`validate-strict-source-native.py` 配合 `SourceAvailabilityFixture.java` 仅安排初始位置，此后的付款和时机推进走真实原生流程。

- `no-mana/before.json` 与原首批 result-round1-driver.json 中 7 个 no-mana 断言：有返照/墓地使用许可但无支付资源时，资格为空。
- `result-delta.json` 31 个断言通过：用两块地实际支付不息骷髅的墓地异能后，仍在坟墓场、仍有许可的深入分析因法术力不足从资格中移除；另一局跨到对手回合，三思/不息骷髅仍可用，法术时机的深入分析移除。双接收者公开 ID 一致且没有发送私人手牌。
- `result-exile.json` 17 个断言通过：雾虚狮鹫在自己的主要阶段具备真实放逐施放候选，跨到对手回合后不再进入来源资格。

首批整体 FAIL 没有抹去。其付款驱动未识别优先权的 ok 是让过，错误选择末项而在维持阶段先激活不息骷髅，随后在主要阶段寻找已离开墓地的骷髅而 KeyError。保留 payment/requests-round1-driver.jsonl、before-round1-driver.json 和 result-round1-driver.json；改正驱动后仅补付款/时机增量，再独立补放逐样本，不重跑已有效的无费用正反例。

## Unity 与限制

`hand-strict-source-validation.json` 77 项通过：重放上述八个真实原生快照的双方视角、同界面付款/时机移除、禁止用旧许可/资格覆盖 disabled 与 requiresPaymentCheck、观战私有 ID 排除、新旧录像恢复、无证据回退及原输入不变。截图 hand-strict-source-eligible.png。规则断言与 UI 重放独立记录，没有新增完整公网对局或全费用组合通过结论。手牌/指挥官原色沿用上一轮独立证据，未以重复截图计数。

图覆盖 metadata_changed/not_tracked 路径读取当前源码补核。按桥接变更刷新 MechanicCoverage 分类，不运行 --run；全局分类仍有 2582 未登记、318 需重测，不视为本轮效果 PASS。没有恢复暂停的长期 goal。

正式场景 SHA256 仍为 3FF3CB51506C876A021973B1852BEB6B6902FA12AB20DFDF964D8EC808633405。本轮只有源码、复制场景 Editor 验证和隔离桥接候选；未打包、覆盖安装版、更新服务器或推送 Git。下次发布须同时带客户端与桥接更新；旧桥接缺少对手即时资格时按严格模式隐藏，不从许可列表猜测。
