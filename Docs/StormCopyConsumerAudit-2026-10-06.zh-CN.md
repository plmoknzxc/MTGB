# 风暴复制身份与数量增量审计

2026-10-06，运行包 `fab8031f7b97fdc498631137a3247e8aced754e6f74ae02978f6af6b3b577ae1`。

## 四条实际规则

先实际施放扑翼机及玄铁秘耳，再施放、选目标和支付散装弹药。真实风暴计数为 2，生成两个复制。三个咒语有不同堆叠 ID 和来源卡 ID，恰有一个原件、两个 copied=true，控制席位均为 1。双方公开堆叠投影一致。

| 场景 | 实际操作与断言 |
|---|---|
| storm-split | 原件目标为对手，两个复制通过原生新目标请求改为自己；最终生命 28/29 |
| original-countered | 反击咒语准确以原件堆叠 ID 为目标；两个复制继续结算，生命 30/28 |
| copy-countered | 反击咒语准确以其中一个复制 ID 为目标；另一个复制与原件结算，生命 30/28 |
| trigger-countered | 阻抑准确以风暴触发 ID 为目标；无复制生成，仅原件结算，生命 30/29 |

每条流程检查双方生命、原件仅一张进入坟墓场、复制不成为额外实体卡及返回下一优先权。最后一条不凭空要求复制出现。全部为实际施放、支付、目标、结算，无结果注入；夹具只设开局。

驱动先执行 storm-split，再执行其余三条，最后由同一驱动/运行包的四个独立结果合并总结果；未重新执行已有通过场景。证据 `.tools/storm-copy-1006/native/results.json`。本批无失败规则场景。

## Unity 增量与复用

中文重放 original-countered / copy-countered 的实际未选、已选四个原生请求，并检查操作者和只读显示。三个同图咒语分别保留真实 ID；横向列表中真实射线点击原件和复制，选后确认、重选使用原生 ID，旧请求/重复/只读提交被阻止。查看模式不提交，查看战场往返字号一致。最终 PASS。

既有 StackList 三语言布局/控件合同证据沿用；本次新对象是同卡图不同原生复制实例，因此补真实中文输入，不重跑旧布局压力与全部语言。新增 UI 入口只登记为既有消费者的增量回归，UI 首验数量不增加。没有生产代码变更。旧三语言存档的 StackList 整文件与当前居中修正后的文件哈希不同；本次依实际输入与当前精确合同做增量验证，只沿用历史三语言格式路径证据，不主张当前整文件三语言通过。比较结果保存在 reuse-method-hash.json。

截图 storm-fan.png 是关闭横向查看后的侧边堆叠及原生待选状态，包含测试迁移提示，不作干净产品截图或实际在线对局证据。

## 来源与剩余

桥接按原生 StackItem.getId 输出 ID，保留 isCopied 和 activatingPlayer 对应的 controllerSeat；目标提交用 stack:ID。Unity 侧边牌缓存与横向列表分别以堆叠 ID 建立对象，不以卡名或卡图合并。

图 Verify 发现 PresentStackCards / PresentStackList / selectStackTarget 后核对准确源码。覆盖代际 `2026-10-06T04:30:20Z`，metadata_changed/not_tracked 补准确源；跨模块 Max/Contains/Remove 等启发式调用不作为真实依赖。排除的上游 CopySpellAbilityEffect 有界读取 amount、copy host、MayChooseTarget 和 orderAndPlaySimultaneousSa，未作完整上游审计。

R20 核销四条命名风暴边界；永久物复杂复制例外、复制状态/来源改变及其他复制交互仍开放。R18 其他触发、R36 控制与私有席位、实际传输继续开放。不得据此宣称全部风暴/复制组合或联网通过。
