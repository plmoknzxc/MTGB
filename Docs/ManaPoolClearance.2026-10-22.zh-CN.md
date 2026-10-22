# 法术力池避让（2026-10-22）

用户截图指出密集战场中双方的法术力池覆盖牌面和翻页箭头。旧 PlacePlayerMana 固定在战场 y=±222，并只避让徽记，未避让增加的第二行牌。

本批只修改 ForgeBattlefieldController.ManaPool.cs：法术力池放入左侧玩家信息列的中线两侧，上方为对手、下方为自己；增加深色底框，限制宽度248，额外颜色／限制桶继续横向浏览，行高随字体比例增加。保留服务端选项身份、可付款判断与重复提交保护。无需原生规则或服务端修改。

验证目录 `.tools/mana-clearance-1022/`，使用真实 IntegratedBattlefield 场景和明确合成的 BattlefieldStressPresentationValidation 夹具：

- checks.txt：双方 × 稀疏/密集 × 小/标准/大字号共48项通过；包括在屏内、池与导航不交叠可见牌面、射线无遮挡、横向浏览能到后续颜色。
- payment.txt：5项通过；合成 InputPayMana 请求验证对手池只读、自己原生选项启用、射线命中、准确提交序号、重复提交拒绝。仅验证客户端合同，不声称原生付款效果已重新执行。
- dense.png / sparse.png：实际截图，测试已向后滚动，因此截图中显示后四种法术力。

图 generation 2026-10-22T11:00:24Z，相关文件 metadata_changed；已按准确源文件回退检查。编译通过并进入场景。未覆盖全部宽高比；未打包或覆盖客户端。
