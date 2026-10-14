# 非瑞克西亚与替代生命支付复核

日期：2026-10-06。用户要求继续 goal；按当前授权推进。下述原生规则、Unity 重放和来源合同分别记录，没有合并为当前全量 PASS。

## 实际漏接与修复

真实施放 Moltensteel Dragon 后，原生 InputPayManaOfCostPayment 允许点击付款玩家的生命值，桥接只导出了池资源与牌，漏了生命选项。桥接现在读取固定版本原生付款对象、剩余费用与已预留生命，用原生 canPayLife 和原生分片判断是否提供 player:<id>；点击交回原生 selectPlayer。没有由 Unity 扣生命或自行推导合法性。K'rrik 的黑色生命替代走同一原生合同。

请求增加 remainingManaCost 和 reservedLifePayment。Unicode/花括号费用经已有 Sprite 转换；预留生命用中英日当前语言显示。HUD 保持已提交的原生生命值，费用完成后才显示扣除。普通法术力费用/康咒额外付款 reservedLifePayment=0，不被混入生命说明。界面仍沿用右下两个支付按钮与实际付款玩家生命区。

固定上游 InputPayMana / InputPayManaOfCostPayment 的来源哈希写入 BaseInteractionContract。Build-Bridge 原生合同、30 个标记注册与打包检查通过，最终 runtimeHash 为 `cc11d485da20d7420e0c310862044843213b7b6fe8a8963ed9381b24cf09ccad`。来源合同只是适配器证据，不代替实际效果。

## 原生八场

- `phyrexian-mana`：Actually cast Moltensteel Dragon using six red mana at 20 life; no life reserved/spent; native 4/4 enters, six sources tapped and empty pool.
- `phyrexian-life`：Actually cast Moltensteel Dragon from 5 life using four generic mana and two native 2-life reservations; HUD/native life remains 5 until completion, commits to 1; native 4/4 enters.
- `phyrexian-mixed`：Seat 2 actually casts Moltensteel Dragon from 3 life using one red, four generic and one native 2-life reservation; commits to 1, native 4/4 enters.
- `phyrexian-insufficient`：Four islands and 3 life cannot pay Moltensteel Dragon; native priority never offers cast, exact hand card retained and no seeded resolution.
- `phyrexian-prohibited`：Angel of Jubilation prohibits life payment; four islands and 20 life do not offer Moltensteel Dragon cast; card retained.
- `phyrexian-prohibited-mana`：With Angel of Jubilation, six mountains pay Moltensteel Dragon entirely in mana; no player life option, life 20 unchanged and actual Dragon 5/5 under Angel boost.
- `phyrexian-cancel`：Reserve 2 life for Moltensteel Dragon then cancel before mana activation; complete native player equals pre-cast state and no life deducted; actually recast, reserve 4 life, pay four mana, enter 4/4 at life 1.
- `phyrexian-black`：K'rrik, Son of Yawgmoth provides native black life replacement: actually cast Grave Titan with two 2-life reservations and four generic mana; life 5 to 1, Titan 6/6, two Zombie tokens, K'rrik receives exactly one +1/+1 counter.

每场真实输入由 Forge 产生，使用实际咒语施放/付款/结算与结束状态。夹具只安排开局；没有修改卡资料或规则。适用的错误座位响应被拒绝后仍能使用原请求。每次独立原生尝试及逐条轨迹保留在 `.tools/mana-variants-1006/native/<case>/<time_ns>`；原始结果、原运行包身份、源哈希和 trace 哈希冻结到 `registration/life`。

## Unity 与定向回归

三语言、付款座位 1/2：先实际射线点击手牌施放，再重放原生付款请求；付款玩家生命区射线提交准确 player ID，一次提交后重复/旧请求无效；观看者、另一玩家和 inputPending 无法付款。核对二次生命预留、未提交生命 HUD、取消后恢复、原生完成至生命 1、生命不足/Angel 禁止付款；普通结算多付 {3} 使用 typed 费用，按钮仍准确提交 cancel。TMP 实际可见 Sprite、固定字号和说明边界通过。

最终完整手牌流程结果：`.tools/mana-variants-1006/phyrexian-ui/20261005T1845455675781Z/result.txt`。雪境/混合/双色通用 Unity 消费者回归：`.tools/mana-variants-1006/ui/20261005T1835007714692Z/result.txt`。原生最终版本定向回归为 snow-paid / hybrid-mixed / twobrid-white；XX 增一费与康咒额外三费位于 `.tools/mana-variants-1006/x-regression-final`。后者仍区别施放费用与结算额外费用，不改变用户印刷 X/XX 卡面方案。

## 关闭的原始有界行

```json
{
  "id": "R08",
  "mode": "reuse",
  "priority": "P0",
  "title": "非瑞克西亚及替代生命支付",
  "families": "mana",
  "boundary": "法术力/生命/混合付款、生命不足/禁止支付、取消不扣预留资源"
}
```

R08 关闭限于该行的法术力/生命/混合、资源不足/禁止生命支付和取消不扣预留资源。完整行、具体 native/UI 身份与此审计 SHA-256 留在 verified-progress.json，关闭不删除证明。

## 失败与未验证

初次真实输入缺 player 生命选项，失败保留；typed 字段修复前英文原生预留说明不能算本地化通过。UI 初次完整手牌检查遇到隐藏 CanvasGroup，清洁 Play 场景中的最终三语言流程通过；没有证据把此前现场残留定位为具体运行时缺陷，未靠强制卡牌 alpha 伪造成功。初次隔离与重复检查失败均保留。

其他非瑞克西亚颜色/复合分片、生命替代叠加、实际法术力能力激活后的撤销、复杂受限来源、替代/不可支付例外、多人与实联网仍未验证。已有其他工作包继续开放；不声称每张卡、所有组合或当前全部 UI/网络通过。
