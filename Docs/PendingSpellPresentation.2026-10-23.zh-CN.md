# 施放中子目标误显示零生命与异能变化颜色

## 原始报告与录像定位

用户截图询问阳光戒正文为什么蓝色，并转述选目标时右侧出现“player2 gain life 0”、记作杰斯凯启示。只读取得服务器最新6份已保存录像，身份／SHA256见`.tools/rule-color-jeskai-1023/server-replays.json`，没有修改线上服务、房间或录像。

准确零生命字段位于`2026-10-23 20-40-14 G2 a2302a6948e8443ab68ce8799f0b6255.mtgbr`第433帧、接收者2的InputSelectTargets，来源是Together as One（携力如一），abilityText为`Player 2 gains 0 life.`、abilityKind为ability、rulesText为空。第435／446帧才进入付款。该牌X取决于实际支付的法术力颜色数，此时零不是已完成结算的结果。同录像第327／369帧有杰斯凯启示，目标阶段另有CARDNAME／any target残留。原JSON、字段扫描、原界面zero-before.png分别保留。

## 根因和修改

Forge的后续目标由AbilitySub对象承接。桥接先前直接拿子对象isSpell和getStackDescription作为展示：子对象不被认作咒语，未付款的Converge又会提前计算为0，Unity因此错误展示一张独立异能牌。

BridgeAbilityText.targetingPresentation只在原生根对象确为咒语时返回根，其他起动／触发对象维持原值。ForgeBridge的InputSelectTargets、SelectStackTarget两处共用该显示来源。目标候选、数量、支付和结算仍使用原目标对象；不改X、不把0硬替换成某个数字。施放后续目标保留原咒语卡面，正常异能仍用异能展示。Unity交互源码和卡牌资料未修改。

## 运行证明

证据根目录`.tools/rule-color-jeskai-1023/`，新桥接及辅助类只编译在该目录classes，固定Java17；没有覆盖ForgeRuntime或服务器。

- 红灯：服务器原帧在Unity实际显示Player 2 gains 0 life；原生携力如一的后续两个目标请求被检查器判为RED。原生在旧展示元数据下仍能正确以三色付款结算3，证明不能把该提示当规则计算错误。
- 携力如一：设置初态为两岛、两山、两平原，之后真实选择抓牌目标／伤害目标，付6点含三种颜色的法术力，完成抓3、对手失3、自身得3，生命23／17。修复后四个选目标状态均为spell并保留Converge原文，检查器GREEN。
- 杰斯凯启示：真实施放、回手对方灰棕熊、对手受4伤害、产生两个Monk、抓2、自己得4生命，终点24／16、手牌2、牌库60→58。原生后续目标保留spell展示。
- 起动式守卫：腐食流浆仍为ability，准确放逐两张Opt中的ID124、保留123，完成结算。
- 触发式守卫：普通施放Solitude后仍为trigger，准确放逐对方灰棕熊，对手获得2生命，终点20／22。
- Unity：使用上述新原生目标请求，经生产Present／PresentTargeting，中日英×小／标准／大字体×携力如一两个阶段及杰斯凯后续阶段，108项检查右侧来源可见、保留咒语卡面、卡图有效、不残留未付款0生命／局部CARDNAME／any target异能牌；另3项颜色守卫，共111项。converge-target-2-after.png和target-1-after.png已查看。是程序化请求重放，不声称操作系统鼠标完整对局或所有语言卡图均已配齐。

保留开发失败：初次原生驱动不识别Jeskai的oneOrNone选实体合同，改驱动后通过，未改产品选择；触发式守卫最初对“至多一个”直接确认跳过，正确得到20／20，原trace保留，改为实际选目标后通过；编译先误取无javac的JRE、再误用release21，最终按本机固定Java17编译通过。首次失败后的旧类重跑保留原RED，不当修复结果。

## 蓝色的含义及限制

详情正文蓝色#70BFFF是原有的“相对原始状态改变”标记，来源为特征差异／复制差异，和目标来源的敌我蓝／红是独立用途。复制、增加或改写异能可能出现该色，未修改的原生阳光戒不应仅因它是自己的牌变蓝。

本批真实原生阳光戒originalRules=currentRules、无hasAbilityChanges，实际详情没有颜色标签，“加”的字形为普通色，sol-ring-before.png已查看；真实规则发生变化的格式化仍保留蓝色。没有据截图擅自清除有效变化提示。用户答复不记得截图那张是否复制或被改动，因此该截图的原始状态仍未知，不重复追问，也不能把此个案认作已复现／修复的误标。

## 当前边界

MechanicCoverage只不带--run刷新分类，2582未登记／318需重测保留原身份。没有恢复暂停的长期goal或授全卡池通过。

下一更新包需重建并带入Windows／Linux原生桥接，客户端／服务端同更；现有协议字段未新增，不能仅改客户端去掩盖旧服务器元数据。本批尚未打包、覆盖安装版、部署服务器或推送Git。旧录像字段保持原样；PresentReplay不重放当时的私有选目标窗口。
