# 机制接入与验证 goal

## N89：主将永久改动选择的提示（2026-10-24）

用户随后明确仅需按钮写明白：当前正文缩为一句选择提示，保留“清除永久改动／保留永久改动”按钮，三语同步；下述详细解释版为历史证明，最新局部检查位于`.tools/commander-reset-short-1024`。

用户反馈“全部保留／全部清除”含义不清，已明确按钮对象、恢复原异能与继续失去异能的区别、强化／削弱一起处理及税不变，并仅加宽该确认窗口。三语三档字号235项实际请求UI检查通过，原生效果复用F04，不重复付费测试；未打包或部署。现有必做0项、原3项后置不变。见[提示改进](ArenaCommanderPerpetualReset.2026-10-24.zh-CN.md)及`.tools/commander-reset-wording-1024/audit.json`。

## N88：易主且变类型主将的拥有者选择（2026-10-24）

F05.01完成。实际夺取主将后变成非生物结界／失去原异能的珍宝神器，再消灭或临时放逐，仍由原拥有者席位2选择；接受回区后来源离场不能拉回，拒绝后来源离场正确回到拥有者战场；同次放逐返回无中间回区提示。四条原生流程14次施放、31项中文大字号UI检查通过；复用旧三语合同，原操控者没有启用的回区按钮。本轮无生产修复，不授新联网或所有延迟放逐牌通过。当前必做1→0，原3项后置及手牌布局不变。详见[补验记录](CommanderRuleBoundaries.2026-10-24.zh-CN.md)及`.tools/commander-stolen-types-1024/audit.json`。

## N87：补Arena主将永久改动清除选择（2026-10-24）

F04.01完成。用户指出宗贵羞辱后的主将回统帅区可以选择移除永久改动，核对官方Arena公告确认属实；N86仅验证了保留分支，不能代表完整规则。现补拥有者“全部清除／全部保留”，六个实际原生流程22次施放、97项三语大字号UI检查通过；八类修改及费用缓存单列API适配器。原F03证明及失败保留，skill补充Arena专属规则来源检查。当前登记必做0项，原3项后置与手牌布局安排不变。详见[补充记录](ArenaCommanderPerpetualReset.2026-10-24.zh-CN.md)及`.tools/commander-perpetual-reset-1024/audit.json`。仅更新项目源码和运行包，未打桌面包或部署。

## N86：主将移区、上忍术与宗贵羞辱（2026-10-24）

F03.01—F03.03按用户点名完成：11个原生流程／25次完成的付费操作、79项三语大字号UI检查。修复永久失去异能误删正常施放动作，以及忍术返回费用漏译；同次放逐返回／消灭拉回不误插状态动作，拥有者选择与控制者分开。当前登记必做0项，原3项后置及手牌布局决定保持。详见[检查与修复](CommanderRuleBoundaries.2026-10-24.zh-CN.md)和.tools/commander-boundaries-1024/audit.json。项目内规则包已同步，未打桌面更新包或部署。

## N85：继续完善指挥官卡牌结算（2026-10-24）

用户要求手牌布局之后再说，暂保留当前切换方式，优先处理指挥官卡牌结算。F01有界接入证明保持；新增F02.01统帅区多主将选择与手牌先施放、F02.02触发免付原费用与附加主将税、F02.03标识色及统帅区来源触发三项具体上下文增量。候选卡资料扫描仅作定位，不计测试配额或逐卡PASS；图连接中断，按准确源文件核对。

F02.01—F02.03已闭合：五个可达原生上下文流程与58项三语大字号UI检查通过；单列一个非合法Arena牌组的原生变体夹具，不混作争锋构筑证明。修复信标选主将提示漏译，未改Forge规则。当前登记必做0项；原3项用户后置不恢复，手牌布局继续后置。详见[结算增量](CommanderCardResolution.2026-10-24.zh-CN.md)与`.tools/commander-settlement-1024/audit.json`。未打包或部署。

## N84：法禁与100张争锋最终验收完成（2026-10-24）

F01.01、F01.02按构筑、建房权威、开局、主将规则、区域交互及结束／下一局核验闭合并保留原编号归档。当前必做接入0项；R02.04、R37.03、A06.01三项用户后置保持未验证。

修正来源牌区拥挤、BO3空行侣比较、主将替代正文漏译、Lutri行侣专属禁牌及局间标题字号／语言缓存。17个原生对局夹具、4个实际C#会话小局、构筑检查与Unity证据分别保存。原失败、成功前缀和增量尾段保留；没有新增UTP／外网或全卡池PASS。桌面打包／覆盖、远端部署和Git发布未执行。

详见[接入验收](CommanderFormatsIntegration.2026-10-24.zh-CN.md)及`.tools/duel-brawl-integration-1024/audit.json`。下方N83公开区域补验及更早记录为历史。

## N83收尾复核：补公开统帅区行侣（2026-10-24）

F01.01、F01.02按构筑、建房权威、开局、主将规则、区域交互及结束／下一局核验闭合并保留原编号归档。最终审计发现对手／观众统帅区遗漏行侣，F01两项暂恢复开放；R02.04、R37.03、A06.01三项用户后置保持未验证。

修正来源牌区拥挤、BO3空行侣比较、主将替代正文漏译、Lutri行侣专属禁牌及局间标题字号／语言缓存。15个原生对局夹具、3个实际C#会话小局、构筑检查与Unity证据分别保存。原失败、成功前缀和增量尾段保留；没有新增UTP／外网或全卡池PASS。桌面打包／覆盖、远端部署和Git发布未执行。

详见[接入验收](CommanderFormatsIntegration.2026-10-24.zh-CN.md)及`.tools/duel-brawl-integration-1024/audit.json`。下方N82及更早记录为历史。

## 历史 · N82：法禁与100张争锋接入进行中（2026-10-24）

用户明确目标为法禁与争锋的实际接入。N81只关闭普通1v1共用机制审查，不能据此宣称这两种赛制已可对局。当前恢复专属接入F01.01、F01.02，两项均开放；其他用户后置范围保留。

争锋按Arena100张（主将1＋主牌99）、25生命、BO1、首次免费调度；法禁100张、20生命、无主将21伤害败北，双主将遵守从统帅区施放限制和局间同标识色换主将。已完成有界原生开局/调度、两次主将施放与加税、法禁双主将和手牌施放、实际21伤害存活验证。本地ForgeRuntime已重建并更新源码/哈希；服务器权威校验、Unity统帅区/确认框与完整局间流程仍需验收，当前不授整体完成。没有覆盖桌面、更新远端或上传Git。

具体过程与剩余见[赛制接入记录](CommanderFormatsIntegration.2026-10-24.zh-CN.md)。

## N81：当前普通1v1接入范围收尾（2026-10-24）

N81完成A01.01结束条件复核：1085条当前根入口、1386条后继源码引用均有准确归属；动态构建分支和N67-N72实际修复/前向缺口关闭证据已核对，没有当前明确接入缺口或失败尾段。关闭独立选择、生成/事件两个原组及A01.01，当前普通1v1接入范围待办1→0、原组2→0。复用原有界原生/UI证据，0新增实局、0新增整牌PASS；70条准确原卡结果不变。用户后置事项及全局REVIEW_SOURCE_CHANGES不变。

本次关闭依据是根入口、准确后继、项目动态注入和实际失败修复的共同复核，不是仅因引用归零。源码/归档证据校验共1696处；原有实际付费原生增量保留211条合格登记引用及4条UI登记，完整旧A01部分进度另存，既有断言与版本不改写。N71凯法涅英文旧OPEN记录由N72前向关闭，未伪造旧记录。

仍未验证：原用户后置的指挥官/生产四人、完整子游戏及特殊模式、外部机器/容量、A06全局当前依赖。普通牌池未逐卡穷举，不能承诺所有组合零缺陷。自动报告继续保留REVIEW_SOURCE_CHANGES。

发布状态：N67-N72修复在当前源码、Editor与隔离原生覆盖层中验证；本次没有打包、覆盖桌面客户端、安装原生运行包、更新服务器或上传Git。后续发布须重建两端桥接/原生配方，并保留既有FFmpeg打包要求。

依据：[结束条件审计](../.tools/integration-completion-1024/audit.json)、[逐门槛记录](../.tools/integration-completion-1024/gate-checks.json)。

## N80：效果注册、产费、触发与控制流程（2026-10-24）

用户再次“继续”，已从N79暂停断点恢复。N80完成最后92条效果注册、延迟/立即触发、产费、控制流程与终局后继的准确参数/源码接入核对。核对已选值复制、原生操作者与记忆对象、颜色组合分配、多人付款、BO3准确移除及终局等待清理，未发现新接入缺陷。后继源码引用已处置1294→1386，未处置92→0；没有新增原生/UI实局或整牌PASS。A01.01一个事项和两个原组仍开放，接着核对整体结束条件。

下一步：不再按后继引用开新排查配额。对照A01.01结束条件，核对1085当前根入口分类、1386后继处置、项目注入分支及N67-N72已知修复的实际证据/未结尾段；仅明确缺口补测。源码目录归零不能单独关闭目标。

依据：[本批审计](../.tools/control-registration-descendants-1024/audit.json)、[最新断点](../.tools/control-registration-descendants-1024/review-frontier.json)。本轮没有打包部署或上传Git。

## N79 收尾暂停断点（2026-10-24）

按用户“完成这轮之后先暂停一下，告诉我剩下具体还差多少”收尾；N79 已登记并通过 234 处证据引用及目录分区校验，本轮无新增原生或 Unity 实局测试。当前仍有 A01.01 一个事项、两个原组开放：独立选择后继 41 条、生成与事件后继 51 条，共 92 条待核对源码引用。

| 剩余范围 | 引用数 |
| --- | ---: |
| Effect 效果注册 | 44 |
| 延迟触发 12、立即触发 2 | 14 |
| 产费 11、替代产费 1 | 12 |
| 重复 2、分支 5、保存值 1 | 8 |
| 洗牌 3、输掉对局 6、抛硬币 2、额外回合 1、成为君主 1、比赛移除 1 | 14 |
| 合计 | 92 |

92 条是查找引用，不是缺陷数或测试场数。完成这些引用后仍需核对两个原组的结束条件；若发现具体接入缺口，再补相应实局证据。指挥官/特殊模式、外部机器和全局依赖核对继续按原用户决定后置。本断点未打包、部署或上传 Git。

## N79：特征、指示物、控制与牌面状态后继（2026-10-24）

N79完成164条特征/状态变化后继的完整参数与源码接入核对，包括动画/攻防/保护、指示物、变身/翻回、控制权、横置/重置、相位与御土。明确UpTo数字、重置候选、横置/重置二选一及PayLife2结算中付款，未发现新接入缺陷。复用19类旧有界合同，没有新增原生/UI运行或整牌PASS。后继处置1130→1294，未处置查找引用256→92；仍1事项2原组开放。

区分原生控制者/拥有者/请求操作者；改变现有卡的状态仍保留物理ID，时间戳/区域与移出防护由原生判断。Pump的DefinedKW复用已选值，ProtectionAll的ChosenColor不产生第二次保护选择；多种固定指示物与来源指示物不误当新类型选择。御土构建时注入目标限制，后续准确记忆返场触发沿既有合同。

本轮为接入与源码处置，不授每张原牌/所有生命周期组合通过。剩余92条仍是查找引用，不是92场测试；目录归零后须另核对整体结束条件，当前目标继续开放。没有打包部署。依据：[本批审计](../.tools/state-change-descendants-1024/audit.json)、[最新准确断点](../.tools/state-change-descendants-1024/review-frontier.json)。

## N78：抓牌、伤害、消灭与牺牲后继（2026-10-24）

N78完成215条抓牌/伤害/移除后继的完整参数与源码接入核对：Draw94、DealDamage18、DamageAll3、Destroy47、DestroyAll14、Sacrifice2、SacrificeAll31、Mill3、Counter1、Fight2。确认Upto抓牌、玩家牺牲选择、UnlessCost付款及所属者排序的各自操作者，未发现新接入缺陷。复用10类旧有界合同，没有新增原生/UI运行或整牌PASS。后继处置915→1130，未处置查找引用471→256；仍1事项2原组开放。

可选抓牌由原生玩家确认并选择数量；牺牲的合法候选由原生按当前控制者产生，批量牺牲的Controller过滤与UnlessPayer付款身份分别保留。固定/目标对象、离场前信息和堆叠实例ID不按牌名替代。伤害/消灭的独立替代与触发反应不因本批消费者核对获得整链通过。

本轮只有源码接入处置与历史有界证据复用，未新增当前实局、打包或部署。当前准确后继目录以本批remaining-nodes为准。依据：[本批审计](../.tools/draw-damage-removal-1024/audit.json)、[最新准确断点](../.tools/draw-damage-removal-1024/review-frontier.json)。

## N77：检视、揭示与选择后继（2026-10-24）

N77完成45条检视/选择后继的完整参数与源码接入核对，涵盖Dig16、DigUntil8、Reveal4、ChooseType7及10条其它检视/选择定义。核对跨玩家操作者与区域拥有者、公开/临时查看、合法候选与只读预览、零候选/零下限与取消。未发现新接入缺陷，复用12类旧有界合同，没有新增原生/UI运行或整牌PASS。后继处置870→915，未处置查找引用516→471；仍1事项2原组开放。

按启动者选择但操作他人牌库的Dig与原拥有者整理/刺探分别保留，Reveal随机子集由原生限制，临时查看在原回调后清除。ChooseCard的Mandatory与MinAmount0独立；数字选择Min1且默认Max99。Seek为原生随机移区，不展示整牌库候选；显化/隐蔽的公开牌背与控制者私有身份分别沿已有投影。

本批为请求/身份/权限/恢复源码处置，原始结果版本不变，不授45张牌或整个目标当前实局通过。下一步按最新remaining-nodes核对实际效果消费者。未打包部署。依据：[本批审计](../.tools/inspect-select-descendants-1024/audit.json)、[最新准确断点](../.tools/inspect-select-descendants-1024/review-frontier.json)。

## N76：复制、克隆、生成牌及衍生物后继（2026-10-24）

N76完成93条复制与生成后继的完整参数/源码接入核对：复制咒语或异能34、复制永久物15、克隆3、生成牌19、衍生物22。核对对象/控制者、复制选择、原生排序与重新选目标、战斗中进场和取消恢复；未发现新接入缺陷。复用7条原始有界结果，没有新增原生/UI运行或整牌PASS。后继处置777→870，未处置查找引用609→516；仍1事项2原组开放。

原生Copied Spell与永久物复制/克隆的身份分别保留；重新选目标取消时恢复旧目标。Populate构建时补入的选择器、复制品攻击中进场的防御者选择，以及衍生物的创建者/控制者均核对准确回调。生成牌的手牌/战场/None目的地沿原生移动和可见性投影，不新增客户端区域或按名字猜身份。

本批只处置生产/交互合同；替代、进场、Conjure及其它下游效果仍有独立范围，未授93张原牌或整个目标通过。没有打包部署。依据：[本批审计](../.tools/copy-create-descendants-1024/audit.json)、[最新准确断点](../.tools/copy-create-descendants-1024/review-frontier.json)。

## N75：生成触发、替代及命令区可执行定义（2026-10-24）

N75完成26个生成定义的有界接入核对：16替代定义、7触发定义、2嵌套SVar及Discord命令区ST$ Play。复用N64有效的具体接入处置并核对当前源码，不恢复逐卡随机/整链配额。未发现新接入缺陷，没有新增原生/UI运行或整牌PASS。后继处置751→777，未处置查找引用635→609；仍1事项2原组开放。

核对效果生成时复制命名状态、原生触发/替代注册区域、时机与法术力转换、准确替代对象排序及命令区效果标记的card:id通道。CantHappen不走选择，Prevent和Updated由原生处理；后续Execute/ReplaceWith的具体效果保留独立范围。两条SVar:name:value按Animate原生解包，不当成独立交互。

N64整体闭合结论的撤回不抹除有效的Discord局部复用记录；本批核对的是当前接入，不把所有随机牌名/来源变化追加为逐卡任务。仍须继续实际效果消费者的差异筛查，未授整体完成。未打包部署。依据：[本批审计](../.tools/generated-definitions-1024/audit.json)、[最新准确断点](../.tools/generated-definitions-1024/review-frontier.json)。

## N74：按所选牌名生成并施放的后继核对（2026-10-24）

N74完成独立选择组8条Play后继的完整参数与源码接入核对：按所选牌名生成复制品，以及MakeCard到None区域后按准确记忆对象复制施放；未发现新接入缺陷。复用N54 Garth与N67—N71实际付款/免费/税/取消等原证明，没有新增原生或Unity运行、整牌PASS。准确后继处置743→751，尚未处置引用643→635；仍1事项2原组开放。Discord的ST$ Play单列保留。

操作者沿用原生启动者；没有跨玩家私有区授权。可选确认、强制费用/目标、零候选和取消保留原生分支。Svega的临时None区域对象由MakeCard加入集合后被Play读取，桥接缓存准确物理对象；未把原生普通效果组合扩为逐卡实测。

下一步从命令区ST$ Play以及生成触发/替代、复制与检视消费者继续，当前目录以本批remaining-nodes为准。未打包、覆盖客户端或部署。依据：[本批审计](../.tools/chosen-copy-play-1024/audit.json)、[最新准确断点](../.tools/chosen-copy-play-1024/review-frontier.json)。

## N73：后继目录纠正与区域、状态、施放权限筛查（2026-10-24）

N73纠正后继目录按首项排根的2处漏关联，重新校验1095根/1033源文件；保留旧审计。完成269条区域/弃牌、452条状态/清理、4条生成施放/许可的有界接入处置，复用原证据，未发现新接入缺陷；没有新增原生/UI运行或整牌PASS。已与N72的18条回调核对合并为准确后继清单，仍1事项2原组开放。

跨玩家搜牌的操作者与区域拥有者、目标玩家弃牌、附件/攻击中进场、可选付款及原生排序分别核对。生成后主动施放与静态MayPlay许可分开：后者在桥接前完成费用转换，保持原对象/操作者与期限，不能直接套立即施放证明。清理公开通知与静态费用排序也保留各自合同。

修复的是排查工具：冻结链按源代码行排序，现按effectiveLine及根原文取后继。两个漏关联的实际节点此前已在另一根下出现；没有据此宣称游戏效果出错。剩余准确节点仅作查找入口，不能当作待跑对局数量。尚未关闭两组或整个目标；未打包部署。依据：[本批审计](../.tools/downstream-index-1024/audit.json)、[准确后继进度](../.tools/downstream-index-1024/review-frontier.json)。

## N72：生成触发漏译与后续选择回调（2026-10-24）

N72修复凯法涅生成触发正文中日文回退英文、保留关联牌名，并修正施放选项日文标题。135项主检查、6项标题截图检查和9项旧格式投影/点击检查分别通过；相关审判官日文只验证文字投影。复用N71真实付款结算，不新增原生场景。并行完成16原输入的18个选择后继声明源码核对，未发现新接入缺陷，未核销整牌；仍1事项2原组开放。

从准确原卡文移除对应的首张牌展示说明，再匹配完整后续触发；没有按中日英文句数强行截取，也不覆盖已改变的条件或费用。未知关联后缀保持原信息。日文施放标题在共用出口按动作语境翻译。真实请求的三语、三档字号、双方视角、滚动和原选项提交分别检查；旧失败及有效前缀保留，只续未完成差异。

N71记录的凯法涅英文缺口由本批向前引用关闭，原历史文件不改写。后继源码核对覆盖选择玩家、公开手牌、私下检视及确认回调，余下范围见子审计，不能把这些结果视为整组完成。尚未打包、覆盖客户端、部署或推送。依据：[本批审计](../.tools/trigger-sentence-1024/audit.json)、[选择后继核对](../.tools/choice-descendants-1024/audit.json)、[中文实际界面](../.tools/trigger-sentence-1024/final-zh-CN.png)。

## N71：效果费用预览与连体选面卡图（2026-10-24）

N71修复效果付费施放的选面费用：嘉姬替代费用1、凯法涅减费后U、战咒食人魔额外RR与实际付款一致；免费/带税免费/普通付费/取消4条回归通过，共7条原生流程。标准连体牌两面插画分别裁切并转正，三语三档等235项Unity检查通过。截图新发现凯法涅后继触发英文回退，保存真实请求待修；仍1事项2原组开放。

真实旧失败保留：选面仍显示1U，但嘉姬实际付1、凯法涅实际付U。原生选面期间绑定准确效果/选项，预览副本复用实际费用转换；预览复制保留临时加减费参数，真实选择和支付对象不被预览修改。三条新流程分别证明付款、横置目标、抓牌及恢复优先权；食人魔另核对加攻和牌放逐。标准Split卡图只修选面消费者，不改整张原卡图或Room/Aftermath版式。

截图核查另发现凯法涅生成触发正文有英文，下一轮直接重放保存请求修复。未验证的非纯法术力替代费用与异框裁图不算通过；两组后继筛查继续。只有源码与隔离运行更新，未打包、覆盖客户端、部署服务器或推送Git。依据：[本轮审计](../.tools/effect-cost-faces-1024/audit.json)、[减费选面截图](../.tools/effect-cost-faces-1024/reduce-choices.png)。

## N70：生成／事件根入口归类（2026-10-24）

N70完成679条生成/事件根入口的有界归类（675当前、4后置），11类请求/对象通道，逐条核对原行/哈希和全部根参数；Populate构建时补入的选牌入口单列，N69对手选目标缺口保留实测证明。结合N66，两组共1095根入口已归类，后继接入与已记录展示/费用边界仍开放。0新实局、0新UI、0整牌PASS，仍1事项2原组。

478条延迟注册、70条原生特征变更、45条衍生物生成、62条既定/目标复制、8条复制单选、1条殖民补入单选、1条对手选目标、6条公开移区、4条私有手牌移区、2条批量移区、2条命令区静态效果分别保留。到期的外部记忆触发与复制品自带触发、操作者和原始对象身份未混为同一结果。

本轮是源码/合同筛查，没有新增产品修改或重跑旧对局。后继目录里的61种标签只作查找入口，包含效果和静态模式，不是61次新增测试。下一步核对两组后继是否引出新的请求、权限、可见性或恢复分支；未完成前不关闭整个目标。依据：[本轮审计](../.tools/generated-contracts-1024/audit.json)。

## N69：对手选目标的可用性预判（2026-10-24）

N69修复回音廊（Echo Chamber）有合法对手生物及足够费用仍无法启动：预判先设置原生选目标者，计数后恢复。真实对手选第二张同名牌→原启动者付4→复制品进场及回合末离场通过；空目标/辟邪/缺费三个守卫、普通寒冰付费结算及18项Unity交互分别通过。679生成/事件根仅冻结当前资料目录，整体仍1项2原组开放。

原生红灯证明确有入口缺陷；没有修改Forge的目标合法性或复制规则。修复后对手选择ID128，越权响应被拒，启动者付款，产生ID130并在结束步骤离场，原两个生物保留。共享普通目标/支付流程通过；UI使用原生请求进行真实控件射线点击，与引擎结果分开登记。

仅源码和隔离验证类更新，未覆盖客户端、服务端或推送Git。679条目录不是679次测试或整组通过；接入筛查继续。依据：[专项审计](../.tools/generated-screen-1024/audit.json)。

## N68：免费施放预览与标题避让（2026-10-24）

N68补齐免费施放选面的费用预览与明确提示，并修复大字号标题/说明压到选项的问题。4条真实原生流程通过（免费、莎利雅加费、普通付费双选、取消）；三语三档字号等140项UI及4项快照兼容检查通过。整体仍1项2原组开放，0原输入关闭。

免费施放选面现显示“无需支付法术力费用”，预览经原生费用调整：普通免费为0；莎利雅场景为1，实际精研付5、寒冰再付1，成功横置及抓牌。普通手牌施放不带免付提示，仍付1U。大字号实测发现的说明/选项重叠已按实测标题高度修复。

本轮仅改源码及隔离验证类；更新包须同步重建原生补丁、桥接与Unity两端，重新生成原生来源清单，未覆盖现有客户端或服务器。连体卡图裁切和非免费效果施放预览仍保留边界。依据：[专项审计](../.tools/free-cast-preview-1024/audit.json)。

## N67：免费施放牌面过滤修复（2026-10-24）

N67修复PlayEffect免费施放连体牌时被原费用过滤、跳过选面的接入缺口。保留原失败；免费成功、施放禁令、取消、普通付费4条原生流程通过，Unity原请求选择/取消/清理16项通过。整体筛查仍1项2原组开放，0原输入关闭。

米捷兹的精研花完4点法术力后，旧桥接把热火／寒冰两面按原费用均排除；现延后至原生实际付款阶段判断，施放禁令继续有效。实际选寒冰后目标岛横置、抓1、原牌留在放逐区；取消及禁令均正常恢复优先权。普通手牌两岛仍只允许付费施放寒冰。

选择窗口印刷费用与免费上下文、连体卡图裁切仍有展示限制；不声明全部费用/卡图适配完成。没有更新安装版或服务器。依据：[专项审计](../.tools/effect-casting-1024/audit.json)。

## N66：独立选择根请求筛查（2026-10-24）

N66完成独立选择416原声明的根请求筛查（命名99、类别182、颜色135；当前410、后置6），核对当前卡源哈希/准确原行及全部根参数，归为16种请求分支。复用已有秘密/私有信息、空候选、多选及准确名称合同；0新增实局、0整牌PASS、0原输入关闭。两原组仍开放，下一步检查选择后继与生成/事件的项目接入差异。

本轮是源码与旧证据的有界筛查，没有改动生产代码、运行新对局或更新安装包。命名牌的全池/指定牌/固定列表/随机分支，类别的秘密/记录/第二类别，以及私有手牌决定颜色和多选数量分别保留。所有原卡精确结果和历史失败身份不变。当前仍 **1项、2个原组开放**，未确认新的补跑场景；尚未核对的后继不得按相同根窗口直接算通过。

分类刷新仍是2582未登记、318需重测结构条目，不能作为机制缺口数。规划生成器还报告 reviewedScripts/unreviewedTests 来源变化，本轮未将其静默接受为已审查。实时 goal 已恢复 active，较早的暂停/完成记录只作历史；用户后置范围继续保留。

依据：[根请求清单与限定结论](../.tools/integration-screen-1024/audit.json)、[后继筛查入口](../.tools/integration-screen-1024/downstream-candidates.json)。

## N65：撤回过早完成结论，保留有效复用

N64关闭A01.01的依据不足以说明整个剩余目录不存在接入差异。两类生成权限与延迟LKI/反身触发做过旧证据和源码对照，未新增完整场景实测；1095原声明中70条有准确原卡结果关联、10条后置，1015条没有逐项证明全是重复或无需验证。77共用实现有旧结果，不等于所有参数/权限/生命周期差异已完成归类。

当前恢复 **1项有界接入差异筛查**，**0条已确认新增测试**，实际补测数量尚未确定。筛查只找项目面对的新请求/身份/操作者与可见性/授权/恢复差异，相同合同批量复用；不重新要求逐卡完整施放、每条引擎效果链对照或1000多个测试。历史真实结果、有效复用、后置范围和全局失效状态保持；N64完成结论被本记录更正，不能继续据此声称goal已充分验证完成。系统goal曾被提前结束，目前本地剩余清单已恢复，系统状态不能代替完成证据。

依据：[完成结论更正](../.tools/redundant-work-disposition-1012/closure-clarification.json)、[当前自动报告](MechanicValidationRemaining.zh-CN.md)。下面N64及更早进度为历史，N64整体收尾结论已撤回。

## N64：实际取消多余工作，普通1v1接入范围收尾

之前的问题不只是计数大：N62/N63仍要求没有具体项目接入差异的生成授权与延迟后继整链比较。这两类追加审查现已取消，不再执行逐卡旧证据关联、未修改Forge效果组合的整链补跑或兄弟异能读取值验证。

已有真实流程分别证明生成牌的确认及实际施放、权限到期和来源依赖、先牺牲再选择反身目标、独立触发记忆、离场力量记忆以及延迟条件/时间戳/清理。相关当前接口与对象提交复用这些接入合同，未发现需要新增客户端请求/字段/权限/恢复分支的具体差异。因此A01.01以有界接入复用闭合，当前必做事项 **0**；0新增对局、0新增整牌PASS，552原生/41UI首验、70准确原卡结果及全部历史失败身份不变。

这项收尾回答的是普通1v1缺少交互/桥接、错误身份/权限和无法继续结算的问题；未逐卡实测或未穷举的Forge内部组合保留为验证限制。6特殊模式原组、指挥官/完整子游戏、用户自行负责的跨机器测试及后置的全局版本依赖核对保持未验证。全局旧证据失效报告不改写成全量PASS。

依据：[取消清单与旧结果依据](../.tools/redundant-work-disposition-1012/audit.json)、[自动当前报告](MechanicValidationRemaining.zh-CN.md)。下面N63及更早记录是历史进度，不作为当前待办。

## 当前集中核对进度（N63）

N63完成共用选择合同复用与77执行器/39控制器调用核对，移除N62误留的囚禁太阳N44旧缺口（N51已实际补齐）。当前1事项、2类共同核对责任，0已确认新运行；准确原结果70条保持，0原生/Unity新运行。

生成授权与延迟LKI／反身后继仍需结果比较，不能用回调已存在代替后续结果。本批只沿用准确旧证明，未给其他原卡新增PASS。依据：[本批核对](../.tools/shared-contract-routing-1012/audit.json)。

## 当前工作量修正（N62）

之前推进时把逐卡证据关联变成了实际完成门槛，工作量被放大。现改为 **1个集中合同核对事项**，包含选择值下游、生成对象权限、延迟生命周期三类责任；**已明确必须新增的运行0条**，核对尚未结束，不表示无未知或goal完成。此前保留的三条具体尾段已补完。

1015条未关联声明、806条细分记录只保留为追溯目录，退出待办数量和估时；不要求它们逐卡归零。77种共享实现均已定位旧有界结果，先比较共同合同，只将具体不匹配的项目交互、权限、字段或生命周期结果列入补测。70条准确原结果及全部旧证明保持原身份，未逐卡实测的范围不伪称PASS。

依据：[工作量复核](../.tools/workload-contract-review-1012/review.json)。以下旧批次数量属于历史统计。

2026-10-12 N61一轮核销3条生成／事件原声明：Parting Gust 不赠礼的实际放逐／结束步骤归还并加一个指示物；Foe-Razer Regent 两次真实互斗，各生成延迟触发并在结束步骤合计加四个指示物；Faerie Dragon 原生随机强化后，攻击分支销毁、未攻击分支保留并自然清除强化。新增4个完整原生流程、3次施放、2次激活、14个完成端点，实际支付16法术力；884原生断言、2479离线结果／双方视图断言。生产代码未改，共用UI保留原批次证据，0新增Unity运行。 当前仍A01.01一事项、两原组开放；准确原结果67→70，未关联声明1018→1015，去重核对上下文809→806。声明/核对上下文不是新对局数。117/125原输入组完成，6特殊模式组与R37.03/R02.04/A06.01原后置决定保持。 证据 `.tools/generated-reader-tail-1012/audit.json`。以下旧数量为历史记录。

2026-10-12 N60一轮核销4条独立选择原声明：Desert Cenote、Thriving Isle 的所选绿色实际产出并用于施放 Llanowar Elves；Illusionary Terrain 两个所选地类别读取消费、双方地转换、实际 Naturalize 移除后还原。新增3个完整原生流程、4次施放、2次横置激活，实际支付6法术力；881原生断言，83已完成Unity检查。仅修正一个Unity文件的激活异能标题、行高和菜单标题中英日切换。 当前仍A01.01一事项、两原组开放；准确原结果63→67，未关联声明1022→1018，去重核对上下文812→809。声明/核对上下文不是新对局数。117/125原输入组完成，6特殊模式组与R37.03/R02.04/A06.01原后置决定保持。 证据 `.tools/choice-reader-delta-1012/audit.json`。以下旧数量为历史记录。

2026-10-12 N59一轮核销13条原声明（独立选择1、生成/事件12）：自然传记章节的所选类别读取、准确新旧移区对象、下回合归还、强制攻击、Shirei来源守卫、魔戒持有者条件、可选延迟法术力、Sakashima回手、临时复制品到期，以及复制延迟触发的抽牌/放逐。933项离线证据断言，复用原真实付费和UI证明，0新原生/Unity运行。 当前仍A01.01一事项、两原组开放；准确原结果50→63，未关联声明1035→1022，去重核对上下文824→812。这些是证据核对记录，不是新对局数。117/125原输入组完成，6特殊模式组与R37.03/R02.04/A06.01原后置决定保持。 证据 `.tools/payload-reuse-next-1012/audit.json`。以下旧数量为历史记录。

2026-10-12 N58一轮核销19条原声明（独立选择12、生成/事件7）：选类别后返回/Seek/免费一次/死亡指示物/注记抽牌、改色防护/地类别到期、Turnabout整组横置重置、真实复制品到期牺牲、Hippo延迟数字法术力，以及Seraph/Krovikan两准确原来源的共同生命周期。189项离线断言，复用旧真实付费与UI证明，0新原生/Unity运行。 当前仍A01.01一事项、两原组开放；准确原结果31→50，未关联声明1054→1035，去重核对上下文842→824。这些均不是新对局数。117/125原输入组完成，6特殊模式组与R37.03/R02.04/A06.01原后置决定保持。 本轮按用户“先做一轮”授权完成；证据 `.tools/payload-reuse-round-1012/audit.json`。以下旧数量为历史记录。

2026-10-12 N57抚育特殊资源按用户决定复用TMOM/18正面和RAV/81黑暗亲信背面；实际双方正反面/详情32检查、准确原ID/3/2和原规则保持。0新原生对局，旧转化/支付/揭示原证明保留。完整关联77共享实现的旧有界结果范围（46来自正式首验/基线API标签，另31补原A01闭合和准确专项证明）；重复声明合并审查引用，不按API或原声明开新测试。 当前仅1个事项A01.01，含独立选择与生成/事件2原组；117/125原输入完成，6特殊模式组后置。R02.04外部机器/容量交用户自行测试，A06.01全局当前依赖核对后置；与Commander共3完整事项后置，不算通过。两组只核对选择值下游、生成对象权限/离场和延迟/LKI/反身链的具体差异；旧基础结算和UI合同复用，不把1054未精确逐卡关联或77共享实现当作新测试数量。 证据 `.tools/incubob-art-and-reuse-1012/audit.json`。本次为用户直接授权的资源/整理任务；系统goal仍PAUSED。以下为历史批次记录。

2026-10-12 N55批量核对1095原声明的当前源码/牌面和处置，12根API队列用于消费者审查而不是12测试。复用A Killer Among Us同宿主实际秘密选择、两原件/14mana、取消恢复和揭示知识、Human匹配加三counter/死触、实际Goblin不匹配、到期及下一优先权。原快照哈希与执行冻结卡源一致；0新对局/UI。10原时空/先锋/魔王声明按已有用户授权后置，原ID保留、不授PASS。 当前仍3事项、A01仍116/125完成及当前3组/后置6组。独立选择准确原结果24/416、6条模式后置，生成事件7/679、4条模式后置；386和668只是尚待消费者关联的普通声明，不能作为新增测试/轮数。确切补跑量尚未确定。Kiki旧到期流程执行卡源身份缺口保留部分证明；抚育卡图、外机/容量、A06依赖继续。 证据`.tools/payload-bulk-reconciliation-1012/audit.json`，Goal ACTIVE。

2026-10-12 N54补齐3个准确原声明：Alpine Moon旧实付R命名原Urborg后移除对手地类别/静态并生成AnyMana；Council同宿主实际4费进场、己方命名Inspiration减至2费抽2、对手准确优先权禁用、Unsummon1费移走后恢复并按4费抽2、私有手牌；Garth实际横置选择/接受Shivan Dragon复制品并付4RR6费得到原5/5飞行对象。五末态、六提交操作、五正支付共17mana；89准确原ID/冻结源码/请求核对。修复制牌未加入None集合时确认预览漏图，共用confirmAction只缓存准确原参数并finally恢复，当前17零效果适配器＋15主语言UI检查；旧raw两错时机断言FAIL和弱卡图断言视觉FAIL保留，未重跑已付费用流程。 当前仍3事项：A01.01当前3组（独立选择、生成事件、抚育准确缺图），R02.04外部机器/容量，A06.01剩余历史依赖合并。A01仍116/125完成、6特殊模式组后置；独立选择准确结果20→23/416，未关联393是原声明而不是393新增对局。Moon单独实际激活/离场不从投影推断，无全量或外部机器联机PASS。 证据`.tools/name-consumer-delta-1012/audit.json`，Goal ACTIVE。

2026-10-12 N53一次关联7个准确独立选择原声明：Anina X3随机生成、Ornate M1/2/3存值推进、Nissa X12全11候选生成及38mana原支付、Killer Cosplay克隆/脱离还原、Psychic Paper牌名与类别两个原根及装备层还原、DCI所选名称移除双方六物理原件含备牌。49离线核对；实际冻结执行源码与当前原path/face/line逐一相同。五旧宿主独立保留原runtime与FAIL有效前缀，未重跑任何付费操作或Unity套。 当前仍3事项、A01仍116/125完成及当前3组/特殊模式6组后置；本轮独立选择准确结果关联13→20/416，整体组尚未关闭。剩余声明须归并消费者/旧结果后仅补实际差异，不当396新增对局。生成事件、抚育准确资源缺口、外部机器/容量和剩余历史依赖继续。 证据`.tools/payload-proof-joins-1012/audit.json`，Goal ACTIVE。

2026-10-12 N52：N52普通辅助牌BO3增量：原物理注册区域随离场保留；无赌注也捕获来源。两真实DCI支付共14mana禁用双方公开/隐藏游乐设施和装置；原游乐设施交换成功前缀4mana保留，仅补有有效组别的装置交换尾段4mana。3独立流程（2终局+1前缀）、4完成操作22mana；主机75检查及两正常下一局准确注册9/14或赢得重复装置，原卡池恢复、拒绝伪造/错区/冲突；12当前编译零效果适配器复用旧主备原终局，并核对游乐设施同合同。原生牌名候选漏辅助牌修在共用BridgeController，沿原谓词/面身份/最终合法性；两真实牌名请求23实际Poll/射线/权限检查。无卡牌资料/规则类/卡图更改，运行包仅桥接类增量。 当前仍3事项：A01.01当前3组（独立选择、生成事件、抚育准确缺图），R02.04外部机器/容量，A06.01剩余历史依赖及证据合并。辅助牌BO3原注册来源/准确授权下一局差异已按本轮界定范围补齐；不为总runtimeHash变更重跑旧付费流程。A01仍116/125完成，6特殊模式组后置未验证；原77/81关闭、552/41首验保持。 证据`.tools/auxiliary-series-1012/audit.json`。Goal ACTIVE，无全量或远程联机PASS。

2026-10-12 N51补齐Caged Sun非mana-ability来源ManaAdded：Ashaya使Deathrite Shaman成为地，两次真实横置/坟墓目标放逐，所选蓝色先产生原U再由普通堆叠触发加Sun U，未选红色只加原R；实际Sun付6、Twiddle付U及自然清池。1流程4操作（2有mana、2付横置）7mana、5末态；87保留有效UI检查含三语言1.3双方堆叠/图标、详情、真实颜色ordinal射线/权限/重复和清理。修公开Produced符号后缀阻断印刷正文匹配造成整段英文，仅一个UI源增量；不改原生/runtime。准确复用Svega旧原选择/扣3忠诚/生成Liliana免费施放/清理，不重跑旧对局。 当前仍3事项，A01仍116/125完成、当前3组及特殊模式6组后置未验证。独立选择原声明41和341本轮完成，原组416声明未整体关闭；其余精确来源/消费者/合同/结果尚待合并，不能当403个新待测对局。生成/事件与抚育精确缺图仍开放，R02.04外部机器/容量及A06.01依赖/辅助BO3来源保持。 证据`.tools/choice-nonmana-1012/audit.json`，Goal ACTIVE。

2026-10-12 N50：普通游乐设施原最低输入和S04当前范围关闭，当前4→3事项、原77/81关闭；A01完成115→116/125、当前4→3组、特殊模式6组后置未验证。四独立原生流程含完成前缀：10完成付费操作32mana及1life；未完成Line付款3不计。实际自然/额外掷骰、命中/重复/唯一计数、销毁垃圾场、修改骰值与普通/造访专属异能、Ferris移出相位/低点数/正常重置异常/返回及生成清理；381完成UI检查，83准确印次灯号adapter为0效果。原FAIL/前缀保留，不重跑旧付费开场；无生产/运行包变更。当前3事项：A01.01当前3原组（独立选择、生成事件、抚育资源），R02.04外部机器/容量，A06.01当前依赖合并及辅助牌BO3归属/移除来源。特殊模式6原组仍后置未验证。22游乐设施/45装置的独立子效果保留A01生成/事件原payload归属；普通无可达多接收者声明的Open分支只作源码分类，不授生成改写或多玩家通过。 证据`.tools/attraction-visit-1012/audit.json`；Goal ACTIVE，无全量或远程机器PASS。

2026-10-12 N49：装置原最低输入和S05.02关闭，当前5→4事项、原76/81关闭；A01完成114→115/125，当前5→4组、特殊模式后置6组未验证。10完成付费操作共33mana（原Cog前缀与三独立流程分列），469实际UI检查含三正式语言1.3。原失败/前缀保留，重复输出无损压缩及去重约3.58GB；不重跑N48注册/编辑。原生规则和运行包未改。当前4事项：A01当前4原组（独立选择、生成事件、抚育、游乐设施造访），R02.04外部机器/容量，S04.02游乐设施入口/展示，A06.01当前依赖合并。特殊模式6原组后置未验证。辅助牌BO3归属/移除的原区域来源仍待A06核对；45装置/22游乐设施的独立子效果保留A01原payload归属，不以通用合同授全量PASS。 证据`.tools/auxiliary-lifecycle-1012/audit.json`；Goal ACTIVE，无全量或远程联机PASS。

2026-10-12 N48辅助编辑增量：卡组编辑辅助入口、同名灯号保护、加入/移除/分页、主备按钮路由和载入/另存深复制已补齐；未知印次可见并可移除。原33成功前缀保留，149布局与769满页/末页检查三正式语言1.3通过；已保存卡组文件未变，不重复两原付费施放。当前仍5事项，A01当前5组/特殊模式后置6组/完成114原组；接续战场公开组别/灯号/垃圾场/当前CRANK及原生生命周期。证据`.tools/auxiliary-entry-1012/editor-audit.json`，Goal ACTIVE，无全量或远程联机PASS。

2026-10-12 N48辅助入口增量：正式卡组数据保存/导入与主机校验接通游乐设施10张/装置15张独立区域，准确印次和同名限制，BO3换备锁定辅助印次。实际组装2/打开2两咒语共8mana，库顶ID、组别1/3及功能灯号准确，双方恢复优先权；11主机adapter与无夹具正常生产worker启动分别留证。尚未关闭原输入或事项：整体当前5项，A01当前5组、后置6组、完成114/125。卡组编辑与战场UI、自然CRANK/造访及原边界继续补验；不新增全量PASS。证据`.tools/auxiliary-entry-1012/audit.json`。Goal ACTIVE。

2026-10-12 N48范围决定：特殊模式后置，先完成普通1v1。A01原125组中114完成、6特殊模式后置未验证、当前余5（独立选择、生成/事件、装置、抚育资源、游乐设施）。整体当前5事项：A01.01、R02.04、S04.02游乐设施、S05.02装置、A06.01；原81事项仍75关闭。本次不新增效果PASS、不重跑旧对局。证据`.tools/ordinary-scope-1012/audit.json`。Goal ACTIVE。

2026-10-12 N47：N47原GainOwnership与Darkpact ExchangeZone最低输入：实际赌注模式注册、原目标/牌库顶物理ID与同owner/异owner，四生产者实际付款/牺牲/交换或支付/追加赌注分支及controller不同owner；精确本场BO3归属和原owner移除对接、双方公开赌注区/通知、原目标/选择确认射线与三语言；主机原子更新、第五张精确授权卡池、换备、正常后续worker、观战者私有隔离、新比赛恢复。 43离线对接断言、36主机检查、双方公开区域/选择/通知/房间入口三语言证明；旧FAIL与成功前缀保留，仅补驱动或生产失败的差异。当前仍5事项：A01余11/125原组，R02.04外部机器/容量、S04.02游乐设施/时空入口、S05.02装置入口、A06.01依赖对齐。原81事项75关闭。指挥官与完整子游戏仍排除当前goal。 证据`.tools/series-ownership-1012/audit.json`，goal active；全局旧证据需复核状态保持。

2026-10-12 N46：N46 RemoveFromMatch原最低范围：两实际生产者原生移除/非普通死亡/库存标记与精确物理对象，双方公开通知；新结构字段由实际ended送入生产Poll/host，按原owner/printing/main或side更新本场BO3暂存卡池，普通下一局准确注册57/58或59/60张；换备/重置/准备沿有效ownDeck，不保存卡组或收藏。原池/版本/重复ID/原子失败/异常重试/新比赛恢复及清空牌库的真实下一局正常判负；实际颜色与命名选择射线、权限和清理，三语言双方通知。 两原生生产者与后续生产worker分开留证，70当前选择/通知检查、host44成功前缀+22尾段、清空卡池220原始检查（去重标签单列），41离线对接断言。原FAIL保留，只补缺失尾段；全局报告继续标旧证据需复核，不伪造当前全量PASS。当前仍5事项：A01余12/125原组，R02.04外部机器/容量、S04.02游乐设施/时空入口、S05.02装置入口、A06.01依赖对齐。原81事项75关闭。归属交换/赌注及其BO3入口仍属GainOwnership未完成，按本场模拟，不改保存卡组/收藏；指挥官/完整子游戏排除保持。 证据`.tools/series-removal-1012/audit.json`，goal active。

2026-10-12 N45：S06.02公开Card.sector/ChosenSector字段、同名不同分区分组、α/β/γ战场标记和完整详情；保留原居中三选项与canonical key/ordinal、对方不能选/重复门槛/下一优先权清理。暗置仍有公开所属分区，不显示私有已选属性；非战场无所属标记，已知离场/LKI来源按原生chosenSector保留。 复用旧S06原生规则/551选择窗口检查，不重跑五回合流程；新实付4mana施放与六原生分配前缀、独立忠诚尾段分别冻结，175当前UI与4只读投影adapter通过。原空字段KeyError/错误节点路径/指示物Count误作组数的FAIL保留，未改生产来迎合测试。当前5事项：A01余13/125原组、R02.04外部机器环境/容量、S04.02游乐设施/时空接入、S05.02装置接入、A06.01依赖对齐。指挥官/完整子游戏排除保持。归属交换与跨局移除按用户决定仅本场BO3模拟，不改保存卡组/收藏，入口与后续局仍待补。 证据`.tools/sector-display-1012/audit.json`；Goal ACTIVE。

2026-10-12 N44：N44独立选择三真实消费者：Caged Sun所选蓝色只给己方对应生物+1/+1，并由原Island法术力异能增产至2U、自然阶段清池；Addle真实支付2并选蓝色，仅弃对手公开Opt、保留Lightning Bolt，占栈保存值被子消费者使用后随入墓新对象清理；Belbe’s Portal真实支付5选Elf、再支付3横置，只让原Elf从手进场而保留Bear。3流程6末态、5实际操作（4付费16mana）；101冻结原生核对检查。59实际Poll/颜色类别与对手公开手牌射线、确认和双方恢复检查；9准确旧NameCard父链反向绑定已有生成结果，114检查，无引擎重跑。共用类别词表只补Army中文“军队”，当前真实选择控件加载/原ordinal点击通过，其他词条及英日字段不变。 当前仍6事项、原74/81关闭，A01仍112/125余13原组。本轮独立选择原根3、26及九旧父根103/123/127/351/352/354/358/360/407按实际范围补齐，41部分补齐；Caged Sun非mana-ability来源ManaAdded分支保留，整体416独立选择不能授全量PASS。Svega/Avatar两额外候选旧父链尚未核对、不先计复用；生成事件、开局/变体及专用模式等沿原13归属继续。 证据`.tools/choice-payload-tail-1012/audit.json`。Goal ACTIVE。

2026-10-12 N43：N43原RemoveFromMatch两实际来源补齐本局结果：Blacker Lotus真实选择蓝色并横置，原牌从区域消失、Match记录原PaperCard、可收集标志的原生lostOwnership记录、四点法术力自然阶段清空；Look at Me, I'm the DCI真实支付7，按非基本牌名移除双方手牌/牌库/战场/坟墓场/放逐/已注册备牌中的7原牌，两Blood Artist不触发死亡。2实际操作，1付费咒语7mana、1法术力异能产生4mana；67离线核对检查。 当前6事项、74/81原事项关闭、A01仍112/125余13。api-choice-RemoveFromMatch整体未闭合：原生日志未投影双方通知，当前每局新Match不能证明跨BO3移除，产品库存/赌注范围待用户文字选择；不排除或授PASS。其他原组及完整goal保持。 证据`.tools/match-removal-1012/audit.json`。运行包未变，Goal ACTIVE。

2026-10-12 N42：N42补齐两处原生生成延迟消费者：朽坏攻击后战斗结束牺牲、MayFlashSac非正常时机施放后清理步骤牺牲。3真实流程、6完成付费咒语/12mana、12终点；正常施放不登记延迟，同物理牌回手再正常重入由时间戳隔离旧触发。7条原生成声明准确复用旧复制/返回/自然到期结果，不重新施放。共用延迟详情/堆叠补清理步骤与精确生成正文三语言漏译，68成功前缀＋47差异尾部展示检查。 当前仍6事项，74/81原事项关闭；A01仍112/125，余13原组。679生成/事件声明与416独立选择整体未闭合；1095当前准确源码及748签名候选仅为归类，不是效果通过或748待测场景。指挥官/完整子游戏继续goal外。 运行包a910d3f4dc7f96284abcf61f8d570dcad9e9d5d8a22b653aa350ee7df9575c47未变；40原生/7资料补丁保持；证据`.tools/generated-delayed-tail-1012/audit.json`。Goal ACTIVE。

2026-10-12 N41：N41补齐原生成链剩11根：9根新结果＋Nissa/Svega2根旧结果复用。实际复制品及来源离场/自然到期、Vess两分支、传记章节、先锋头像永久物与免费咒语两臂、Time Sidewalk真实注册起手；Nissa11准确忠诚额外+1离线补断言。Booster整包公开保留原编号/印次，修复None卡预览只有名字无卡图；赠礼方法三语言且费用等于正常基础费用。5代表已付操作11mana，旧Nissa38mana不重跑。 当前6事项、原81事项74关闭；A01从111/125到112/125，余13原组。原21根生成链累计21/21准确结果/复用，该原组闭合；指挥官/完整子游戏继续goal外。其余独立payload、特殊模式、A06依赖对齐和远端环境仍开放。 运行包a910d3f4dc7f96284abcf61f8d570dcad9e9d5d8a22b653aa350ee7df9575c47；40原生/7资料补丁。74执行UI检查含复用断言，不增加首验/登记套数。证据`.tools/booster-copy-tail-1012/audit.json`。Goal ACTIVE。

2026-10-12 N40：N40原21根生成链补齐9根原生结果，复用莱埃泽尔蓝色1根：承诺/拒绝赠礼、TokenCard手牌/坟墓场真实存活及返回/死亡、弃牌/牺牲产牌、双方七牌归属/永久任意类型支付与全红施放、随机副本及实际Elesh Norn双ETB。14完成代表操作46mana；旧拒绝赠礼5mana复测单列不重复核销。 当前6事项、74/81原事项关闭；A01仍111/125，余14组。本21根组已有10根原生结果/复用，11根仍需补齐；Booster根2整包公开及赠礼一般提示保留；指挥官/子游戏继续goal外。 运行包00240829d6df4dd161d3ca871682f4d7c1fea8ecc7148d649d7d5b234ebaf5bb；40原生/7资料补丁。45有界UI检查与规则结果分列。证据`.tools/payload-tail-1012/audit.json`。Goal ACTIVE。

2026-10-12 N39：N39补齐特化原组剩余21根：Gut四色复制数量/攻防/关键词及自然自身END牺牲；Laezel四色Seek原牌/总CMC<=3复活/两士兵/全区永久强化；Lukamina红Wolf、SarevokSeek原牌和两新副本、Rasaad实际ETB放逐/白色改1/1无异能/来源离场原牌返回保留；Viconia五色真实关联放逐/两类独立目标/副本永久效果及全红付绿色费用、黑色实际ETB；Wyll五色真实牺牲/高低CMC期限/回手/对手5生命支付与拒绝/非零重置额外战斗/永久8/8践踏。35完成付费操作77mana；仅文案修复尾段再3操作4mana。 蓝色TriggerDescription误抄白色且实际请求可见，按正确蓝Oracle修正文案，所有执行参数原样；UI共用消费者对已公开精进牌面用自己的印刷规则而非原正面，准确形态后缀/连续已选目标保持ID，原两句提示本地化及长段固定字体滚动。三语言1.3与修改后原选项点击分别补验。 当前6事项，原81事项74关闭；A01从110/125到111/125，余14原组。specialize-independent-face-payloads原52根全部有对应结果/复用证明；本批新增21根，不新增事项/首验身份。完整子游戏与指挥官继续goal外。 运行包61c31aa6647ac44e79e850da1cb797e281c096d6661a7d7793be1644404a4212；40原生/6资料修复。证据`.tools/specialize-final-1012/audit.json`。Goal ACTIVE。

2026-10-12 N38：N38补齐原特化组13个原根（Karlach五形态的返回/独立结果、Klement三形态）。保留实际成功前缀，只补缺失后继：Karlach蓝色返回永久不能阻挡、Seek原牌免费施放/0mana及自然期限清理；Klement系命恩赐只作用下一生物，第二生物不继承、原牌回手再施放不保留指示物。共13完成真实操作：12有mana操作合计43mana、1次免费Opt。修复蓝色同名DBEffect覆盖及缺MayPlay许可；系命心形与已知关键字图标大小写适配；共用施放选择标题/整段印刷文本/免费许可三语言与首屏可见性。 当前仍6事项、74/81原事项关闭；A01仍110/125完成、余15原组。特化52原根累计31已补结果、余21（N36/N37及原Gut红色证明复用）；本批新增13原根，不关闭整个组。完整子游戏与指挥官继续排除。 运行包28997a853cefdd8d7399184e231f906ebcd2fd03824a9bcabfc75effb9cf00d6；40原生/5资料修复。证据`.tools/specialize-more-1012/audit.json`。Goal ACTIVE。

2026-10-12 N37：N37补齐原特化组11个独立牌面入口：Vhal五种实际移除4个Study后的分配指示物/挖掘4/复活/伤害/Seek；Jaheira五种可选消灭及独立指示物/占卜2/失去3生命/下一生物一次性永久强化/获得4生命；Laezel蓝色随机对手牌库生物生成、记忆清理、永久任意颜色支付及实际全红支付绿色生物。保留Vhal五结果原FAIL前缀，Jaheira独立宿主只补尾段，非同局连续证据。共16实际付费操作/45mana，42Unity真实请求、十牌面纹理、点击/支付/私有及清理检查。 当前仍6事项、74/81原事项关闭；A01仍110/125完成、余15原组。特化52原根本轮新增11根结果，N36的6根与既有Gut红色原证明继续复用，其他结果仍属原组；不把整个组标为通过。完整子游戏与指挥官仍排除。 运行包8f39c7dfc7d0bf5311765785839029761bb656ceb8097b531d2e5649106c408b；40原生/4资料修复保持。证据`.tools/specialize-payload-batch-1012/audit.json`。Goal ACTIVE。

2026-10-12 N36：N36补齐原specialize-independent-face-payloads内五形态共用死亡链与Lukamina蓝色Specializes结果：实际Terminate两次及蓝色特化，共3完成付费操作/7mana；准确同物理ID、死亡触发当前坟墓场对象、Unspecialize/RememberChanged条件横置返回、新时间戳及4/4→2/2；蓝色目标横置跨自然对手重置步保持、来源离场后的下一对手重置解除。39准确离线断言、35当前Unity真实请求/目标/确认/恢复及纹理检查。白色已付3的变形前缀独立保留，后继宿主由准确白色状态启动，不声称同宿主连续证明。 当前仍6事项、74/81原事项关闭、A01 110/125完成/15原组未闭合；本组52原根中新增补齐5个Unspecialize及1个蓝色Specializes，其他特化结果继续原组（已有Gut红色证明保留复用）。416/679/21独立选择/延迟/非Conjure行仅完成准确牌面链审查，未知结果仍开放。完整子游戏/指挥官排除不变，A06当前依赖及R02远端仍待完成。 原生40补丁/卡脚本4补丁；运行包2f58769423c75f1ac3c746fe93525fd21360d3600aa775da9c7f8254da95633d。首次552/41、登记416native/156UI保持；Goal ACTIVE。证据`.tools/payload-reconciliation-1012/audit.json`。

2026-10-12 N35完成S08.02当前卡恩/BO3范围并归档S08：当前6事项、74/81原事项关闭；A01仍110/125、余15。现有换备数量/拖动/撤销/真实提交、牌池/印次拒绝、原生两局与新session/准确主备/私有ownDeck、输家先后攻/准备取消/同步与旧match拒绝、引擎实际失败不计分及重试已补。旧平局/卡恩证据按消费者复用；0生产改动、0新付费首验，552/41保持。完整子游戏及指挥官仍排除，不计PASS。两局通过保存的真实第1局结果跨隔离host补尾段，非同host连续联网/不同远端客户端或全量当前版本PASS。证据`.tools/bo3-sideboard-1011/audit.json`；Goal ACTIVE。

2026-10-11 N34卡恩及共享重置原输入完成：A01 108→110/125，余17→15；当前7事项、73/81原事项关闭，S08只剩BO3，子游戏/指挥官排除保持。N34完成原卡恩同局重开及共享可变状态最低输入的普通1v1部分：真实三类放逐/忠诚支付、临时复制与延迟牺牲、真实1/1蓝色蛙修改恢复；同Game/同session重新调度、发动者先手、双方隐藏手牌与生命/指示物/日夜/君主/魔戒/Command清理。原生重开事件递增视图revision、清知识/旧选择缓存，Unity清旧详情/区域窗口/分页/通知并提供双方短通知和原下一输入。 2真实流程、8已付操作共3mana，202相关UI检查；初始驱动FAIL/原成功前缀及179UI保留，仅补23位置检查。运行包11个受测桥接类更新，其余原字节及40原生/3资料修复保持。证据 `.tools/restart-series-1011/audit.json`；Goal ACTIVE，无全量或远程联机PASS。

2026-10-11 N33关闭原S07.02与S07：当前8→7事项，73/81原事项关闭；A01仍108/125、余17，552/41首验不重算。原S07.02原生随机翻牌反馈：复用必选位置选牌与非阻塞通知，当前桥接捕获准确次数/位置/命中物理ID，双方公开；命中牌已离场仍保留结果，场上命中牌细蓝框，重叠/展开仅标正确成员，通知到期清除并能继续原下一优先权。 4真实付费回调共8mana；431UI检查含7分组适配器，三正式语言1.3，保留失败与完整CN前缀，只补差异。发布7个受测Bridge类，40native定义/3资料修复保持；原生效果、其他运行文件字节不变。证据 `.tools/flip-feedback-1011/audit.json`；子游戏/指挥官排除未变，Goal ACTIVE，无全量或远程联机PASS。

2026-10-11 N32原AWOL移除最低输入核销：A01 107→108/125，余17；当前8事项、72/81原事项关闭及原552/41首验不变。N32原api-state-RemoveFromGame最低输入：真实AWOL对准确攻击生物付3，先放逐后从所有原生区域移除；真实Unsummon付1响应使目标回手，AWOL不再放逐/移除，双方公开数量/身份及下一优先权一致。修复只在RemoveFromGameEffect读取当前物理对象后ceaseToExist。 2真实流程、3已付操作7mana、25定向UI检查；原生结果已完成后的驱动字段FAIL冻结补审，不重复施放。单独新增第40个native源覆盖，55857原文件字节未变；3规则资料修复保持。证据 `.tools/removed-object-1011/audit.json`；Goal ACTIVE，排除范围未变，无全量/远程联机PASS。

2026-10-11 N31原融合最低输入与展示核销：A01 106→107/125，余18；S01.02及S01关闭，72/81原事项关闭，当前8、原待办9含排除指挥官整项。子游戏/指挥官排除范围保持。N31完成原meld-linked-native-objects普通1v1最低输入与S01.02融合展示：真实原件链接/单场上对象、原件选择、正常离场/重定向/库顶库底及坟墓场排序、攻击米斯拉、克撒7→9忠诚、缺伙伴/非自身所有/衍生物/实际Clone/禁止部分放逐、合成特征LKI及单次死亡触发；现有详情按钮打开两原件只读列表、完整融合图和真实拆分清理。 39原生覆盖类/3规则资料修复保持，GameAction已有Craft修改原样保留；运行包9类变化、其余字节核对。533 UI检查与19原生历史/前缀分列，原FAIL不改写，552/41首验不重算。证据 `.tools/meld-tail-1011/audit.json`；Goal ACTIVE，无全量/远程网络PASS。

## 当前范围调整（用户 2026-10-11 明确要求）

完整子游戏与指挥官/生产四人专属接入已排除当前 goal；不参与本次完成门槛，也不自动恢复验证。原编号、缺口、卡牌表和后续接续条件见 [当前 goal 外的后续工作](MechanicValidationExcluded.zh-CN.md)。卡恩重开、BO3局间换备及普通1v1共用机制继续。排除不是完成或验证通过，旧批次范围与证明保持原身份。

## 每轮开始的磁盘维护（用户 2026-10-11 授权）

每个新执行轮先检查 C、D 盘剩余空间：**C < 5 GiB** 时保存现场并正常重启当前项目 Unity；**D < 3 GiB** 时整理项目，删除已确认无用、过时且可再生成的产物。阈值按实际字节数严格比较；不触发时只检查。按验证 skill 的 [磁盘维护流程](../.agents/skills/mtgb-mechanic-validation/references/workspace-maintenance.md#goal-每轮开始检查) 保护有效证据、保存续跑位置并复查。该维护属于执行前检查，不新增机制工作量，不改变进度计数或用户的暂停/继续授权。


2026-10-11 N30原景点领奖最低输入登记：N30原景点领奖最低输入：1原ID完成，1原ID部分完成；A01 106/125，余19；其余特殊模式边界保留。 原景点领奖阈值输入完成：4真实历史14末态、14实付共56mana；同来源低于/达到6 Luck、正常1及奖励2 Treasure、准确Junkyard/补开一个/空辅助牌库、Gamer事件Card与Player及目标消灭、静态奖励先于普通触发队列。开景点2/不足1/空0为另一原输入部分证据。原FAIL及有效前缀保留，未支付后续操作不计。262相关UI检查含169原交互沿用与93展示差异；修英文确认及长名/提示布局。 552/41首验及71/81、当前9＋指挥官整项1/3分支后置保持。证据 `.tools/payload-tail-1011/audit.json`；Goal ACTIVE，无全量或远程联机PASS。

2026-10-11 N29原NameCard最低输入登记：N29原NameCard最低输入：1原ID完成，0原ID部分完成；复用原13名称场景；补齐34条件组原生差异及静态候选，修正Garth耗尽旧名复制。A01 105/125，余20；其他边界不核销。 原NameCard名称条件最低输入完成：保留34原分组，静态合法候选与旧13个名称效果/defined-split-empty原证明单列复用。新真实非基本地/AND/OR、传奇及计算MV、实际Equipped/Imprinted费用、固定随机逗号牌名、M1/2/3与X3、Nissa X12全11候选排除/耗尽、Garth同宿主六次选择及最后选择/重置/空列表。13完整或准确冻结前缀、29末态、18实付80mana及10零mana费用操作；204相关UI/27状态，三正式语言1.3、原ordinal/射线/等待/过期/重复/清除。修原Garth耗尽后继续复制旧名字的Play子分支，新增唯一39th native override；原55856运行文件字节不变。 552/41首验及71/81、当前9＋指挥官整项1/3分支后置保持。证据 `.tools/name-predicate-tail-1011/audit.json`；Goal ACTIVE，无全量或远程联机PASS。

2026-10-11 N28原AlterAttribute最低输入登记：N28原AlterAttribute最低输入：1原ID完成，0原ID部分完成；复用Prepared取消/控制权/离场；补齐五类属性和事件身份、状态展示。A01 104/125，余21；其他边界不核销。 原AlterAttribute最低输入的普通1v1部分：驾驭、谋划、侦结、疑犯、乘驾真实付费/撤销/自然期限，三个真实事件消费者保留Card及Player身份；Prepared源牌/副本/效果三ID、施放一次消耗及清理，取消/控制权/离场沿用旧实际证据。补齐公开属性字段、状态分组、蓝色状态标记及详情。 552/41首验及71/81、当前9＋指挥官整项1/3分支后置保持。证据 `.tools/attribute-selector-tail-1011/audit.json`；Goal ACTIVE，无全量或远程联机PASS。

2026-10-11 N27原ChooseCard最低输入登记：N27原ChooseCard最低输入：1原ID完成，0原ID部分完成；复用旧搜寻权限/取消证据，修正实际组队归属和选择提示。A01 103/125，余22；其他边界不核销。 N27完成原api-choice-ChooseCard最低输入：力量总和含负值/零选择与停止、不同力量刷新及真实回合结束、逐力量分组、基本地类别、双方自己的冒险团、秘密控制/非控制选择及集体展示。复用R36原搜寻权限/取消证明。修正集体行动只选择自己控制的生物，并补选择提示三语翻译。 552/41首验及71/81、当前9＋指挥官整项1/3分支后置保持。证据 `.tools/mechanic-tail-review-1011/audit.json`；Goal ACTIVE，无全量或远程联机PASS。

2026-10-11 N26原层/房间/咒语书最低输入登记：N26原层/房间/咒语书最低输入：7原ID完成，1原ID部分完成；引用旧复制/抚育证据，缺失结果/UI独立登记。A01 102/125，余23；其他边界不核销。 N26关闭7原A01输入：AnimateAll组筛选/继承产费/普通boost/实际到期；ExchangeWords与预先改词、后续Green→Red实际目标和触发、新对象/来源离场；MakeCard X0/X14随机咒语书重复抽取；房间0/1/2锁定候选、门身份与完整后续交互；复制替代真实非空Remembered及旧复制层原结果逐字段复用。保留原FAIL与其有效前缀，各阶段独立登记。 552/41首验及71/81、当前9＋指挥官整项1/3分支后置保持。证据 `.tools/layers-spellbook-reconcile-1011/audit.json`；Goal ACTIVE，无全量或远程联机PASS。

2026-10-11 N25原重复/秘密选择/名称最低输入登记：N25原重复/秘密选择/名称最低输入：7原ID完成，实际结算/FAIL有效前缀与97UI/零付款适配器独立登记；修复RevealChosen费用退款与纯数字骰值保存，补附加名字状态。A01 95/125，余30；其他边界不核销。 源码/class按精确受测身份发布，36native定义；同宿主重置不与独立尾段拼接。552/41首验及71/81、当前9＋指挥官整项1/3分支后置保持。证据 `.tools/repeat-reveal-names-1011/audit.json`；Goal ACTIVE，不是全量或远程联机PASS。

2026-10-11 N24原特征/关键词/取消费用最低输入登记：N24原特征/关键词/取消费用最低输入：10原ID完成，14流程40末态，28正mana付款55mana另1次T；66UI检查。A01 88/125，余37；原FAIL/历史runtime/局部classes身份分别保留，其他边界不核销。 修复自然CDA类型/颜色误标变化、公开不能阻挡详情及取消多段费用重复本局施放/hidden状态漏回滚。旧控件与成功结算先复用；测试驱动FAIL保留，不为绿化重跑。发布e899精确受测8class，552/41首验及71/81、当前9＋指挥官整项1/3分支后置保持。证据 `.tools/characteristics-keywords-payments-1011/audit.json`；Goal ACTIVE，不是全量或远程联机PASS。

2026-10-11 N23原限制/生命周期/产费替代最低输入登记：N23原限制/生命周期/产费替代最低输入：完成["combat-pool-first-end-before-extra", "conditional-no-counter-not-permanent", "early-end-combat-pool-cleanup", "lose-mana-color-conversion-provenance", "mana-burn-cleared-only", "nonartifact-ban-allows-ability-cost", "piracy-extra-restriction-and-source-permission", "production-replacement-type-amount-choice", "restricted-mana-colored-two-hybrid-arm", "restricted-static-operation-flags", "static-retention-color-all-expiry"]，部分[]；N23原11个最低输入完成：静态操作限制、非神器限制对异能、Boseiju条件不可反击、双色单边和Piracy来源许可；提前结束/额外战斗的法术力清空、Horizon/Omnath/Ozai转换、Yurlok仅实际清空燃烧及保留效果到期；原七个命名产费替代来源的类型/数量/顺序/选择。23冻结完整流程或前缀、127完整端点、45成功付款86mana。旧已成功结果独立复用，驱动原失败不改写。；23流程/127完整末态/45完整付费/86mana；684UI/8083独立检查；原paid缺陷失败独立保留，rawFAIL完整前缀冻结不重跑；literal DoubleStrike原分类遗漏保持开放；A01 78/125余47。 生产改动：{"native": "PhaseHandler.endCombatPhaseByEffect在expiry/SBA后调用既有onPhaseEnd，运行原pool-loss/保留/转换/燃烧路径，不开始跳过的COMBAT_END步骤。", "unity": "AbilityEffectText准确三个原生生成的产费替代正文补中/日显示，英文与未知自定义长文本保留；只复验实际受影响正文。", "bridge": "无生产改动", "data": "无卡牌资料文件/导入翻译/图片改动", "publication": {"path": ".tools/primitive-api-tail-1011/runtime-publish.json", "sha256": "585ced9a85d4b0dae044795dd818b1e1391217433a4f326b88ab73773470f8ab"}}。旧8bf2付费结果/独立native补丁类与新runtime发布差异分列；源缺陷真实付费失败不算完整末态，适配器/源码检查不算付费场景。总71/81、当前9＋指挥官整项后置1/3分支及552/41首验保持。保留边界：{"restrictions": [], "lifecycle": ["Bounds only the five retained original declarations/minima and exact reachable printed producers in these real 1v1 flows; no generated-only branch expansion or all-producer claim.", "Old N22 Drain paid evidence is reused as frozen context, not recounted as N23 flows or paid operations. No raw successful proof replay for runtime hash or late driver errors.", "Raw FAIL statuses and failed suffixes remain unchanged. Original Mandate paid production failure is separate from successful prefix and isolated fix.", "Mana identity is a multiset: Roku6 and Agni2 may share a single actual Mana reference repeated several times. Quantities never assert distinct identity per unit.", "LifeLostAll evidence is the original native Triggered Map on actual Ob Nixilis abilities plus real counters/life; no synthetic UI notice or projected event substitutes for it.", "Current publication and Unity proof are root-owned. This file records isolated tested PhaseHandler classes with base8bf2; no full current runtime, network/multiplayer, or Unity-wide PASS.", "Forced Mandate pool-loss native sample covers ordinary/combat/persistent plus skipped-step trigger. Forced-ending with additional conversion/burn/replacement interactions is not separately played; normal consumer-path conversion/burn cases were actual base8bf2 flows.", "Burn-tail control endpoint was reached at turn4 because old wait start used default actor1; actual ordinary pool clear happened at turn2 MAIN1 end and is retained in original events. No paid replay to obtain an earlier summary endpoint."], "replacements": ["Only original seven named producers and frozen minimum predicates. The other ReplaceMana declarations/custom factory payloads remain outside this selected minimum.", "Opening positioning bypasses ETB of initial permanents; no new ETB claim.", "Source removed/target leaves and other permanent-effect interactions beyond these frozen assertions remain outside this minimum.", "Raw driver compile/environment/selector/assertion failures are retained. Actual completed endpoints are independently audited; no source-only evidence labeled native PASS.", "Unity controls/text/pixel visibility and remote network delivery are separately owned by root; this file provides actual wire contracts only.", "No native production/Bridge/runtime/NativePatches/card data/Unity/canonical ledger was modified by this work."], "global": "原literal DoubleStrike分类遗漏及所有其他原A01/A06边界保留；指挥官专属整项1/条件3仍后置；真实网络归原R02。当前runtime整套、其他生产者/组合不授PASS。", "morph": "仅原生实际合法支付、翻正/资源结果核销static最低输入；request101后的RevealCards确认与后续Willbender效果未执行。", "driver": "替代流程13个驱动失败保留；新驱动仅已保存合同/结果纯函数回放校验，未宣称新驱动整场实跑PASS。"}。证据 .tools/primitive-api-tail-1011/audit.json；Goal ACTIVE，无所有producer/全量/远程PASS。

2026-10-11 N22原DigMultiple/DrainMana/Flip最低输入登记：N22原DigMultiple/DrainMana/Flip最低输入：完成["digmultiple-category-results", "digmultiple-rest-order", "drain-clear-retained-and-all-players", "drain-remembered-delayed-number", "drain-transfer-source-and-spending", "flip-empty-and-real-outcomes"]，部分[]；N22 closes only six existing A01 original minimum inputs: DigMultiple category overlap/empty retry/extra destinations/ChangeLater/sequential privacy and actual remaining-card bottom order; Drain retained and all-player pool removal, remembered delayed count, original mana provenance/restrictions/actual spending; Flip empty/nonflip/miss/exact hit/destroy/damage/tap/prevention child outcomes and restored next input.21 completed native flows/37 staged endpoints/27 paid78mana;2paid6mana original defect failures separate. No new goal IDs or all-producer/full-current-runtime PASS.；21流程/37完整末态/27完整付费/78mana；927UI/2754独立检查；原paid缺陷失败独立保留，rawFAIL完整前缀冻结不重跑；literal DoubleStrike原分类遗漏保持开放；A01 67/125余58。 生产改动：["Two strictly pinned native overrides installed with exact independently tested bytecode: Dig nonrandom rest ordering and empty Flip landing guard.", "Current shared OrderCards instruction states actual bottom-group order; fixed-size top-right order labels measure actual font height to keep glyphs visible.", "22 exact native interface keys for categories, mandatory retry, landing selection/results added and generated into existing three-language tables. Card data, printed art and wire schema unchanged."]。旧e3a付费结果/独立native补丁类与新runtime发布差异分列；源缺陷真实付费失败不算完整末态，适配器/源码检查不算付费场景。总71/81、当前9＋指挥官整项后置1/3分支及552/41首验保持。保留边界：["DoubleStrike: Selective Adaptation literal ChangeValid omits Double Strike despite printed text. This existing A01 source/payload gap remains open; display-table readiness is not a rules PASS.", "Original generated/custom DigMultiple payloads and remaining position/conditional scope stay with existing A01 ownership; completed six IDs cover only their frozen minimum result assertions.", "Drain departed-player/terminal-game, generated nonmana activation and ManaBurn/other untested producers retain original conditional scope. Ordinary1v1 results and exact actual pool units are proved; shared JavaMana handles are treated as a multiset for Hippo, not distinct units.", "Flip cleanup uses explicit original child IDs/results, literal tail and prior shared cleanup evidence; stale opening-card empty memory readings are not new direct cleanup proof. Native neighbor order is not Unity geometry; S07.02 simulation/animation and physical flip support remain open.", "Live remote delivery and universal first-frame input stability remain separately scoped. The isolated UI raw authority failure is preserved with final explicit field diagnostics, not promoted to an engine defect or a remote-network PASS."]。证据 .tools/dig-drain-flip-1011/audit.json；Goal ACTIVE，无所有producer/全量/远程PASS。

2026-10-11 N21原Balance/Fight/Clash最低输入核销：均衡四真实付费分支的原计数/舍弃牺牲/替代及触发时点；互斗真实非战斗同步伤害、替代变化/原双对象/可选拒绝/自然可达自身互斗及Fight事件；比点赢/平/对手赢/单空/双空、原公开顶牌/双方置顶置底/原事件与子结果。当前桥单牌留顶保留已有公开知识，私有/置底/洗牌不泄露；三语言1.3准确原控制提交及完整结果文字。 14真实原生流程16完整末态15完整付费43mana、1417独立检查/301完成UI检查。生产改动：["Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java", "Assets/Scripts/UI/ForgeBattlefieldController.CardSelection.cs", "Assets/Scripts/UI/ForgeBattlefieldController.Localization.cs", "Assets/Scripts/UI/ForgeBattlefieldController.AbilityFace.cs"]。旧52f2付费结果与当前e3a知识适配器分列，适配器0付费0效果场景；rawFAIL完整前缀不改写、不重跑成功。A01完成58→61/125、余67→64；总71/81、当前9＋指挥官整项后置1/3分支及552/41首验保持。保留边界：只核销这三条原最低输入。未建立可达生产者的共享FightDamageMap、定义对象缺失/相位/非生物、控制者与激活者分歧、其他生成工厂/每个payload/多玩家仍保留原条件源码范围；不授全量当前runtime、远程或指挥官PASS。原FAIL与旧52f2付费证据原身份保留，e3a知识适配器0付费0机制场景；旧知识缺陷不改写为通过。。证据 .tools/balance-fight-clash-1011/audit.json；Goal ACTIVE，无所有producer/全量/远程PASS。

2026-10-11 N20三原控制权/分组最低输入闭合：异大小2/3候选选2/1/0与严格第二组数量/原身份/真实控制权时间戳；窃取瞬间原stack/ability/source编号保留、实际操作者转移/新目标/准确3伤，窃取生物新控制者进場；Perplexing交换接受拒绝与来源先回手原咒语仍由原操作者结算；朋友敌人三组合实际原地横置/洗牌、原神器牺牲、每玩家一次选择/组内记忆不泄漏。11真实完整/准确raw FAIL恢复流程11末态14付费48mana、1207独立检查与207完成UI三语言1.3。只修Unity分组堆叠来源/动态玩家标题/准确子正文/固定字号测量；Engine/Bridge/runtime/卡资料不改。原FAIL保留、不重跑成功，原无可达CantGainControl生物生产者及null栈等条件源码风险保留。A01完成55→58/125、余70→67；总71/81、当前9＋指挥官整项后置1/3分支及552/41首验保持。证据 .tools/control-group-flows-1011/audit.json；Goal ACTIVE，无所有producer/payload/全量/远程PASS。

2026-10-11 N19原类别/状态最低输入闭合：8真实完整/准确FAIL恢复流程24末态19费用38mana；复制Delver翻面一次、原与复制两StoredTransform清理，Cloudshift同ID新时间戳不受旧触发；盖牌公开合法接受拒绝/非生物过滤、直接与合变非永久组件拒绝翻正；Cyberman类型攻防覆盖与还原、含DFC合变拒绝盖牌；实际夜间入场背面、两盖牌准确择一。524独立检查与174完成UI三语言1.3。复用旧Transform/Flip/Morph及S02/N17/N18准确证明，原FAIL/历史身份保留，不为哈希重跑。只修翻正说明/实际确认、已印费用前缀与原公开攻击者上下文翻译、匿名Hn目标标签；未知文字保持，Engine/Bridge/runtime/卡数据不变。A01完成54→55/125、余71→70；总71/81和当前9＋指挥官整项后置1/3分支及552/41首验保持。证据 .tools/state-guards-1011/audit.json；Goal ACTIVE，无当前全量/所有模式payload/远程PASS。

2026-10-11 N18类别选择差异补证：Shared真实制作两原件/交集/一次免费再正常4费；MostPrevalent对手牌库并列Note/清理/fight死亡counter及可选零；随机类别来自真实对手全牌集合并准确消灭、空候选正常续行；秘密三token/内部字段不提前公开/实际牺牲揭示通知/三counter/4伤害与死触到期。修制作材料在ETB选择后才恢复导致漏问类别、空随机类别崩溃；仅三native类变化。Shared紧凑值按钮与公开Type/Player通知/动态literal玩家名，三语言1.3；6真实/准确旧FAIL结果流程19末态11费用操作25mana，81UI/146独立检查。旧哈希/FAIL/成功前缀保留，同合同不重跑。类型以上差异核销到部分范围，原type-state-selector-deltas仍含SetState guard未闭合；A01完成54/125余71、总71/81、当前9＋指挥官整项后置1/3分支与552/41首验不变。证据 .tools/type-state-tail-1011/audit.json；Goal ACTIVE，无当前全量/远程/整个类型状态PASS。

2026-10-11 N17颜色原输入闭合＋类别结果增量：13真实完整/冻结前缀流程25完成末态，15付费28mana；私有手牌零色/单色上限1/两三色0..2选0/2、排蓝1..1、选择一种/全部五种、纯随机存值与准确防护。类型Note去重/空选自动、两自然章节排除旧类/实际counter、Volo准确原顶抓1、双方Type2永久转换与临时转换自然到期完成。修两原地类别问句错接同来源异能卡，复用值列表/既有双语词汇，五基本全显与18最后类别可达；211局部UI/313独立检查。仅两Unity局部文件改动，原runtime/engine/Bridge/卡数据不变。保留原FAIL/缺字段/驱动错配及成功前缀，只补差异。color-candidate-count-deltas核销，type-state-selector-deltas其余Shared/秘密/SetState等仍开放；A01完成53→54/125、余72→71，总71/81与当前9＋指挥官整项后置1/3分支及552/41首验不变。证据 .tools/selector-flows-1011/audit.json；Goal ACTIVE，无全量/跨机器/全部类型状态PASS。

2026-10-11 N16清理结果通知闭合：复用N13/N14/N15真实字段/记忆/压印条件/类别/延迟触发/重复清理证明，只补原GameLog结果交付。Faerie与Whimsy两真实父链2付费13mana，6条清理前牌/玩家日志向双方导出原编号/顺序/来源，原规则结果与输入序列不变，正常空栈优先权。现有非阻塞通知混合队列、重复Poll去重、期限清理、三语言1.3及昵称原文318局部UI检查；539独立检查。仅新增通知订阅类和桥接入口两运行类、局部格式/四翻译键与生成表；其他引擎/资产/JRE字节不变，不新增历史按钮。保留昵称夹具中断及原观察器版本差异，不重跑已完成结果。api-result-Cleanup核销，A01完成52→53/125、余73→72；总71/81、当前9＋指挥官整项后置1/3分支与552/41首验保持。证据 .tools/cleanup-log-delivery-1011/audit.json；Goal ACTIVE，无当前全量/跨机器/指挥官PASS。

2026-10-11 N15清理结果增量：真实同源双方Bear/Cat与两次战斗Land/Nonland选择后清理、随机80原地类别结果；来源离场/失去控制权只清自身延迟触发并保留另一来源，重施不残留。真实Faerie随机牌与Whimsy八动作五日志，清理前牌/玩家身份保留且发事件时字段已空；原日志属于GameLog STACK_RESOLVE，Bridge尚未交付结果通知，Cleanup整输入保持开放。复用旧Vault实际压印条件返手/清理证据，不重跑。6完整/冻结恢复流程23末态11操作37mana，102当前UI/770冻结检查；修地/非地选择误用异能卡样式，三语言1.3实际点击通过。保留原付款后随机重置地导致驱动误判、原未处理坟墓场选牌和旧UI失败；支付前缀独立恢复，不重跑完成结算。Runtime/引擎/桥接/卡数据不改。A01完成52/125、余73保持，但上述清理子断言已完成；当前9＋指挥官整项后置1/3分支与552/41首验不变。证据 .tools/cleanup-tail-flows-1011/audit.json；Goal ACTIVE。

2026-10-11 N14两原输入核销：真实可选选玩家接受/拒绝、两玩家记忆伤害加倍及来源移除、秘密选择原生字段/公开CardView区别及攻击后揭示放逐、随机前次玩家排除无候选横置清理、生成Siege保护者与准确两衍生物；控制权双原件归属/时间戳不变、响应移走一方/拒绝可选/Guardian Beast阻止获控均不半交换。清理选择性玩家记忆六末态、同源颜色重复及反席蓝法术力、同源命名X0两次/X1一次与真硬币伤害/连续全正面零伤害部分通过，其他Cleanup字段开放。修原法术力可用性探测改写在结算异能操作者，及随机Conjure将双面背面空费用误作MV0候选；仅三native override类，旧成功保留旧runtime。四统一语言键及受影响三语言1.3，横向颜色分页/草稿确认真实射线通过。16完整/原FAIL独立恢复流程38末态27实际操作54mana，175完成UI/1144冻结断言。保留驱动误点背后卡/未翻到红色页、合法全正面无伤害、旧生成MV4及错收件人FAIL；无全量重跑。A01完成50→52，未核销75→73；总71/81、当前9＋指挥官整项后置1/3分支、552/41首验保持。证据`.tools/player-control-memory-flows-1011/audit.json`；Goal ACTIVE，无全量/跨机器/指挥官接入PASS。

2026-10-11 N13两原输入核销：Extinction真实奇数/偶数选值，结算中原源枚举及入墓后重置，双方0/偶/奇准确原对象放逐；Godspeaker任选2/0与无合法候选，Beachhead真Soldier可选展示接受/拒绝横置结果，反席位Hired随机公开双方及空手跳过，ExtractBrain X3选择实际2且放弃免费施放，Wingbright两次公开非地再选两原牌，Keranos实际下一回合指定抓地额外抓/非地伤害。清理原来源记忆/选牌的部分字段通过，手牌公开记忆与两原牌持续赋予异能保留；原Shock随后真实施放产生准确抓1/得3再受2。14完整/准确原FAIL前缀恢复流程21末态16实际操作（含2次下地）38mana，196实际Poll/操作与等待席位/原ordinal射线/重复/可选草稿确认/三语言1.3固定字号及高度，730冻结断言。统一表修Odds误译“赔率”和原奇偶英文问句；引擎/桥接/协议/runtime不改。初始Lion为Cat不合法Soldier、第一玩家首回合不抓、入墓枚举重置、RevealCards原牌在previewCards及赋予异能在currentAbilityText的驱动错误均保留，不为错误假设改生产。其他Cleanup字段继续原输入PENDING。A01完成48→50，未核销77→75；总71/81、当前9＋指挥官整项后置1/3分支、552/41首验保持。证据`.tools/selection-memory-flows-1011/audit.json`；Goal ACTIVE，无全量/跨机器/指挥官接入PASS。

2026-10-11 N12两原输入核销：真实Spy Network私有手牌/牌库顶/盖牌三链与空候选后自身排序；Smoke Teller同ID重复/反席位及原盖牌离区后另一次原生公开回手。修复Bridge被授权LookAt仍展示牌背：只在实际接收者记忆原战场对象游戏/层时间戳，knownFace三旧字段投影原牌面；离场事件/身份不符清理，不改原生权限或引擎。Slip原对象与灵气/装备ID/时间戳不变、无换区，正常对手回合后己方重置返回；Oubliette跨真实下次己方重置仍保持，来源Disenchant离场后横置返回；Spatial Binding实际付1生命，第一次Slip只加1但不跃出、到期第二次加1并跃出；Disciple禁止跃入时Time and Tide保持原跃离熊，真实Unsummon移走来源后第二Time and Tide原对象附着恢复；同时切换及任选0/2实际筹谋原抓弃ID/费用分别核对。13完整/冻结恢复流程31末态23实际已付操作41mana+1life，1269冻结检查，165实际Poll/原ordinal射线/重复与非接收者守卫/准确原身份本地化卡图/跃离缩略图隐藏恢复检查。原晚段驱动FAIL、旧runtime与成功UI112检查保留，仅补53纹理/反席位/缩略图差异；截图出现合法中文异版，不要求固定英文printing纹理。仅两Java生产源及四桥接类变化，原UI/语言/引擎不改，所有其他A01/独立payload/Cleanup保持。A01完成46→48，未核销79→77；总71/81、当前9＋指挥官整项后置1/3分支、552/41首验保持。证据`.tools/look-phase-flows-1011/audit.json`；Goal ACTIVE，无当前全量/跨机器/指挥官接入PASS。

2026-10-11 N11三原输入核销：Anticognition真实对手坟墓场7/8张阈值，准确原堆叠目标/二次确认，支付2保留原熊、拒绝反击及真分支反击后占卜准确置顶置底；行走机库双X宣布2付4，Unbound有效X变4，Glava接受变5/拒绝仍2，X0天然0/0SBA入墓且无虚构振翼机。原栈ID及实际费用不变；原生数字/原图费用/有效X分别断言。ChooseDirection旧五多席位顺序/同时转移/空候选及三生命周期记录核对原枚举、顺序、接收者与原身份，无引擎重跑。7新完整/冻结恢复流程13末态10实际施放（9有mana）与1额外2支付，共26mana；49当前实际Poll/堆叠刷新/蓝色实际字形边界/原XX卡图/清理检查，1496冻结检查。原零X驱动在SBA后错误要求castSA不为null、堆叠选择遗漏二次确认与TMP行高误判均保留，不为测试预期改生产。无生产代码/引擎/运行包/翻译变化；原选择目标/额外支付/占卜射线合同按准确字段复用。A01完成43→46，未核销82→79；总71/81、当前9＋指挥官整项后置1/3分支、552/41首验保持。证据`.tools/branch-x-flows-1011/audit.json`；Goal ACTIVE，无当前全量/跨机器/指挥官接入PASS。

2026-10-11 N10选牌结果尾段三原输入核销：刺探准确置顶/入墓顺序、首次与重复触发、空库、增看两张接受拒绝；筹谋多生物先后、弃非地/地、原来源回手后的抓弃与不给新对象指示物、第三抓触发时点、Leader真实替代及不能抓牌空手跳过；显化惧象一张/零张仍有真实Watcher，以及Priest只读换区事件先放逐再按同一效果进入坟墓场。规则推论依据CR701.62/400.7j/614.6，不把旧Exile-only驱动断言当规则错误。10完整/准确恢复流程18末态16付费34mana，237完成UI检查/711冻结断言；旧原FAIL与成功证据保留，只续失败差异。补两问句与准确跃离子正文三语言，Scry/Surveil说明固定字号并测高度；三语言最大字号原ordinal/真实射线/清理通过。无Forge/桥接/协议改动；统一表生成附带保存的两动态字体缓存保留前后哈希。A01完成40→43，未核销85→82；总71/81、当前9＋整项指挥官后置1/3分支、552/41首验保持。证据`.tools/selection-results-tail-1011/audit.json`；Goal ACTIVE，无当前全量/联网PASS。

2026-10-11 N10温习原最低输入核销：实际Lesson/弃牌抓牌/放弃/凤凰替代及Mindslaver受控温习，精确身份/两玩家生命/扣费/牌库顺序/物理与逻辑操作者；受控不能选择备牌Lesson。修原已公开备牌Lesson移入手牌后对手身份丢失，仅BridgeLibraryKnowledge类改变；此前成功原生不重跑，定向Private Peek/Public Mulch付费回归。显化惧象两个不同顶牌/私有2/2和Cage阻止后两牌入墓作为原输入部分证据，Priest两牌入墓的初始Exile-only断言尚未独立确认，保留原FAIL/规则疑点，未核销整个Manifest输入。7完整/准确末态流程8付费18mana，93已完成UI检查/339冻结断言；UI仅驱动错认手牌暂存/普通确认及替代效果按钮，生产Unity未改。A01完成39→40、未核销86→85；总71/81、当前9＋整项指挥官后置1/3分支、552/41首验保持。证据`.tools/selection-results-1011/audit.json`；Goal ACTIVE。

2026-10-11 N09四原囤兵/续战/探查/探察最低输入核销：7完整流程＋2准确raw FAIL成功前缀，共16实际结果/操作40mana，263当前相关UI/377冻结断言。双倍军队及指示物、两军队准确选牌/记忆力量磨原8顶、原Zombie加Orc及离场清效果；续战counter/Spirit两选择和原来源回手自动两3/3Spirit；对手探查接受/拒绝失1、token倍增与真实次数分开、重复Num/记忆清理，准确线索实际支付牺牲抓原顶；探察地公开原身份进手仍被对手所知、空库仍加1/无虚构顶牌、来源回手仍探察无counter，旧保留/入墓/公开顶抓走结果引用原证据不重跑。修续战/探查准确问句中英日、卡牌确认按实测完整高度及重选标签。保留全部原FAIL，整屏卡文含关键词不能充当问句翻译证据。A01完成35→39、未核销90→86；总71/81、当前9＋指挥官整项后置1/3分支与552/41首验保持。证据`.tools/token-explore-flows-1011/audit.json`；Goal ACTIVE，无全部producer/参数/跨机器/当前全量PASS。

2026-10-11 N08四原生命/攻防交换最低输入核销：10真实流程40结果、30施放/激活/横置费用操作141mana（26操作有mana）、612相关UI（原成功前缀42＋当前尾段570）及7945冻结检查。固定设生命/0与2玩家重新分配、重复40多重集/禁止加减生命；两与单目标交换、实际损失记忆抓原顶及清理、Archive倍增回血/后实际差值衍生物；三API实际LifeLostAll接Ob指示物/准确原顶放逐；力量/防御力净值快照设新基础、普通增益保留/自然到期/再交换、禁止变化/原来源响应回手；Master真实净力量交换、两存活原对象共享战斗到期与4/6实伤，R39永久基础力量真实换区重施复用。仅补右下生命数值选择接收者三语言动态玩家名；原ID/排序/费用与规则不变。原重复值驱动FAIL及两UI控件识别FAIL保留，不重跑已成功效果/点击。A01完成31→35，未核销94→90；总71/81、当前9＋整项指挥官后置1/3分支与552/41首验保持。证据`.tools/life-power-flows-1011/audit.json`；Goal ACTIVE，无全部生产参数/跨机器/当前全量PASS。

2026-10-11 N07四原战斗关系最低输入核销：8实际流程（7完整/1冻结前缀）29结果、8完成付费20mana、390相关UI/676冻结检查。实际守方单选/目标/全部双攻击者强制阻挡、非法确认原阶段保留后修正与存活目标战斗到期；Portal玩家改攻旅法、原Ulamog排队触发保留/准确20原顶放逐和旅法10实伤；Planetar逐张拒绝/接受与分开生命/忠诚伤害；Fog无实阻挡者第一/重复及显式unblock后再阻挡、准确践踏4伤；Noble真实配对/blocked/一次武士道和2/3实伤。修重复Fog多触发及Block配对未标blocked两原生类，所有旧运行文件字节不变。原驱动误判门槛、缺确认及零伤害分配/observer字段错误的FAIL保留，Blaze只核销request50前有效付费/双关系，不冒称尾段实伤/到期。A01完成27→31，未核销98→94；总71/81、当前9＋整项指挥官后置1/3分支和552/41首验保持。证据`.tools/combat-relations-1011/audit.json`；Goal ACTIVE，无所有生产参数/跨机器/当前全量PASS。

2026-10-11 N06当前对象/战斗/附着四原输入核销：5真实流程（4完整/1冻结前缀）35结果、21付费操作45mana、123相关UI/1162冻结检查。拘留三禁止/原生空攻阻自动推进/准确来源下回合到期及真实换区重施、金刚狼先清旧伤后留新伤/金字塔销毁替代一次性与自然到期、普通离战保持blocked/显式unblock实际伤害/攻击者移除及准确记忆和额外战斗、Disarm两武具保持场上卸下与空集合通过。发现拘留未投影，补公开bool、分组和蓝色状态/详情及暗置观战保留；引擎/卡脚本及所有非bridge运行包字节不变。原驱动等空阻挡窗口误停，冻结至request123含自然到期与恢复选项，原FAIL不改写、不重跑。A01完成23→27，未核销102→98；总71/81、当前9＋指挥官后置1/3分支及首验552/41不变。下一N07原4战斗关系输入先对齐旧证据。证据`.tools/current-object-flows-1011/audit.json`；Goal ACTIVE，无全局当前/所有生产参数/跨机器PASS。

2026-10-11 N05批量消灭/牺牲两原输入核销：6真实流程14结果、13完成操作（10施放/3激活）42mana及11忠诚，76相关UI/341冻结检查。实际群体过滤/盾与不灭/NoRegen、装备可选拒绝接受、实际销毁数回血与全部对象力量18伤害、禁止牺牲及自有原因、准确4衍生物、原暗置卡/压印条件弃牌返手及清理通过；初始11忠诚/装备附着只为开局位置。仅完整武具消灭问句三语言修复，原engine/runtime不变，旧成功效果/UI合同复用。原两驱动FAIL均在付费前，修时机/实际message字段；保留所有原结果不重跑。A01完成21→23，未核销104→102；本goal轮含N04合计核销4组（19→23，106→102）。总71/81、当前9＋指挥官整项后置1／3分支、首验552/41不变。下一N064个原当前对象/战斗/附着状态组先对齐旧proof。证据`.tools/mass-results-1011/audit.json`；Goal ACTIVE，无全量当前或全部生产参数PASS。

2026-10-11 N04再生两原输入核销：5真实流程24结果、22施放51mana，176相关UI及384冻结结果检查。两盾独立选择/消耗、伤害清除/横置/离战、重入/自然到期、直接再生/禁止、控制子效果及共享盾记忆均有原身份结果。仅生成护盾来源/规则与持续详情两消费者展示修复，原引擎/runtime未变；原驱动失败、英文最大字号溢出及有效前缀保留，不重跑已通过效果。A01完成19→21，未核销106→104；总71/81，当前9＋指挥官整项后置1／3分支，首验552/41不变。下一N05合并原批量消灭/牺牲2组。证据`.tools/regeneration-flows-1011/audit.json`；Goal ACTIVE，无全量当前或所有生产参数PASS。

2026-10-11 R02.02/R02.03真实双客户端原边界完成：R02.02/03原最低边界完成：当前Windows真实dedicated服务器与两个standalone客户端经UTP，目标/宣攻/阻挡/受控私有选牌/分堆及部分付款等待共7次宽限内恢复，原worker、请求、双方私有hash及操作权限保留；旧match/真实曾有效native ID/旧及重复client ID、手动AUTO竞争和丢失确认后的inputPending恢复；付款完成、取消退款后重试、实际30秒双方断线平局及部分付款方超时判负，旧令牌/动作不能复活或重复计分。4实际施放+2激活共18mana，103冻结断言/43证据引用。真实超512KiB状态被丢弃已修：protocol18可选有界gzip，JSON/hash不变，小包原路径，非法压缩/16MiB展开/旧协议拒绝。仅本机真实传输，不授跨机器/全部网络或费用组合PASS。 总71/81已闭合，10待办=当前9（1补证据/1审查/7决定）+指挥官整项后置1，另3条件分支后置。A01仍125去重输入/19完成/106未完整核销；原552/41首验不变。下一N04再生两原输入合批，goal active。证据`.tools/network-boundaries-1010/audit.json`。

2026-10-10 增减指示物尾段完成：N02第五原输入AddOrRemoveCounter核销。Original AddOrRemoveCounter minimum now complete: existing chosen type add/remove and EachExisting reuse; actual Guile empty auto-add; original optional Yes/No at both triggered players, later sacrifice/damage; forced receive-only/remove-only/neither and type-specific Melira restriction; Shape true/false RemoveCondition across real attack/upkeep; Guile removal remembered child real4 damage and cleanup. 8完整/前缀流程17结果，9完成付费20mana；212相关UI检查。修复时光纺丝/Melira真实空类型导致结算崩溃；保留全部原FAIL与Guile成功施放前缀，只补子效果尾段。原runtime标识发布时点限制如实记录，不伪造原版本。A01完成18→19，未核销107→106；当前11＋整项指挥官后置1／3分支、69/81及552/41首验不变。下一N03真实双人时序恢复。证据`.tools/counter-addflags-1010/audit.json`；goal active，未授全量/联网PASS。

2026-10-10 指示物四原输入完成＋Add前缀保留：四个原输入最低消费者结果完成：Dust两区域TIME数量与真实延缓续问；Aether全部类型/实际衍生物放逐、Temporal只活动方HOURGLASS全部；任选类型/多牌/零停止与下游抓失生命、玩家、共享3预算、Ashling移除记忆伤害与Amy放逐自动单选；移动All/Any/零/来源汇集/多接收者/EachNotOn/NonZero与来源实际移除、目的倍增、Goldberry真实抓1；三次时间旅行重建/空选/最后TIME免费施放。 17实际流程（12完整、5准确前缀），23完成付费操作68mana，388相关UI检查；真实Eventide只有2指示物却抓3/失3修复为准确2，单CountersRemoveEffect override。类型/数字原问句错接异能卡修为现有值选择；名称和3语言/最大字号/实际射线/原ordinal/只读清理通过。单候选/固定数量原生自动、伤害触发时点及3/3Hill实际死亡的驱动误判冻结原FAIL，不重跑已完成结果。A01去重125，完成14→18，未完整核销111→107输入账数；Add尚有原Optional/强制条件/权限/移除记忆子效果，Clockspinning/Quarry3操作不再重复。总69/81、当前11＋指挥官整项后置1、3分支后置及552/41首验不变。证据`.tools/counter-flows-1010/audit.json`；goal active，无当前全量或真实联网PASS。

2026-10-10 同批四个横置/重置原输入核销：四个原API最低消费者结果完成：两Turnabout各一次整组二选，Hidden Strings逐张相反决定并拒绝编码；真实Blinding/Trumpet记忆1伤和清理，Deeproot两新横置仅一聚合衍生物；两Cloud选0/2，White Plume对手维持必须1，Gond替代实际Gate进场无普通Untaps，Cloud+Orb正常两重置准确磨原两顶。 8真实流程（7完整/1有效前缀）、9付费施放25mana、1实际下地、294相关UI检查；修复Tap漏译和整组原{0}提示，三语言/最大字号/原选项射线/门槛/只读/清理通过。原FAIL保留，中文英文成功前缀不重跑。A01去重具体输入125，完成10→14、未完整核销115→111；这是输入账数，不是测试或轮数。总69/81与11当期+1后置事项、指挥官3分支后置、552/41首验保持。证据`.tools/tap-flows-1010/audit.json`；goal active，无当前全量/真实联网PASS。

## 2026-10-10 排期优化与指挥官后置

用户明确要求尽快完成、指挥官相关先记录之后验证。当前未完成12项保留，其中R37.03生产四人／指挥官整项后置，当前排期11项（3证据／1审查／7决定）；A01属性／融合组件与S08子游戏中的指挥官专属分支后置，普通1v1部分继续保留。后置是排期决定，不是效果通过或删除。

执行优先：N01横置／重置4个原结果记录、N02指示物5个原消费者记录、N03双人联网时序＋恢复2项共用准备、N04再生2个原API差异。先对齐已有证明，再用共享开局和同一交互合同集中补缺失断言；已完成结果与共用UI／语言不重跑，驱动失败保留成功检查点并仅续失败差异，批末一次登记。候选记录数不等于需要新运行的数量。

计数修正：A01原88条顶层记录实际86个不同ID，含两个重复ID和嵌套汇总；已完成10个具体输入有原证据。不能由88−10报告净剩余78组或78轮。后续按实际未核销结果汇报，而非原引用记录数量。

本次仅更新排期、skill和报告；原验证账本／首验552／41及所有已有结果保持。系统goal仍paused，本次优化不修改系统状态或启动效果／Unity验证。当前完整scope政策在`.agents/skills/mtgb-mechanic-validation/references/remaining-work.json`；后续用户安排指挥官阶段时恢复上述后置项。


2026-10-10 等级门槛原组完成：珊瑚盔统领原等级组：实际UU施放前缀；同一场上对象四次真实升级1，等级0/1=2/2、2/3=3/3飞行、4=4/4飞行且仅己方其他人鱼+1/+1。实际GrowthG成为7/7，基攻防仍4/4。独立初始化4级尾段实际Hexmage牺牲移除全部等级，同ID恢复2/2、飞行和他人人鱼加成撤销，再真实Growth5/5。最后独立Growth尾段自然清理恢复2/2，对手回合不能升级；原占栈时SorcerySpeed不可激活检查保留。 75冻结检查、104实际UI检查；三次驱动FAIL的有效施放/升级/移除前缀保留，只补缺失尾段。无生产变更。A01原组9→10/88；总69/81、12待办（3证据/1审查/8决定）、552/41首验保持。证据`.tools/level-threshold-1010/audit.json`；goal active。

2026-10-10 横向牌区/专用法术力增量：地、生物、支援区固定两行横向列排布，像素拖动/无惯性/松手整列对齐/单列滚轮和短箭头，数量与忠诚徽章完整边界/原代表ID可达；完成1093牌区前缀证据保留。法术力改短单行横向四可见列，普通/雪境/原生用途限制分桶，受限彩色盾框与准确付款ID。真实Cavern普通/专用R各1，错误Shock支付不改费用/资源，普通支付Shock2；Human2R部分专用付款取消准确退款再施放，实际4spell mana、159检查。三语言当前池/通用选择窗口164及六牌区箭头/真实Cavern列表33检查；原失败保留，只补差异。证据`.tools/horizontal-pool-1010/audit.json`。总69/81、12待办保持，552/41首验不增加；goal active，无实时网络/当前全量PASS。

2026-10-10 记录力量/生命/减费原组完成：A01原stored-noted-power-life-and-cost三卡组核销：Dragonborn实际+2再Growth到7、Fling牺牲LKI7/伤害7/boon7，原来源回手2不改变7，第一Bear9/2永久保留并回手重施一次、第二Bear2/2；保留原FAIL并冻结9操作15mana完成前缀。修复生成赐福的正数门槛后，实际正2/零0/负-2牺牲及下一生物7操作11mana通过。Splendor实际施放记录40，四个己方维持等40抓/低38不抓但更新38/等38再抓/升42抓，白色Revitalize触发+1再+3；三施放7mana及原手牌/牌库身份核对。重平衡Muse实际装备4、攻击并响应Growth到7、结算记录7，再移走装备变5而记录仍7，首BlazeX6仅R/下一BlazeX6为6R、一次效果移除，6操作12mana；修复Attacker取值及Calculate解析。窄桥接回归三操作4mana证实真实boon2/减费7向双方导出。合计5流程含1完成前缀、28成功付费操作49mana、564冻结断言及115UI展示检查。 证据`.tools/noted-1010/audit.json`。总69/81、12待办（3证据/1审查/8决定）不变；A01原组8→9/88。两处规则修复保存固定源哈希/严格上下文资源patch，打包复制后重套；上游及目录/Oracle不改。系统goal active，继续授权有效。

2026-10-10 Rhystic控制者支付原组完成：A01原rhystic-target-controller-payment核销：同一真实对局4张Lightning+两灵气6施放17mana，五次实际控制者付款9mana。对原Player2支付2受2伤/拒绝受4伤；对原对手Wurm支付2受2伤、自有Wurm拒绝受4伤，付款座位准确，实际伤害事件与原SpellAbility RhysticPaid为1/0。Errant/Power Leak实际附着原对手生物/结界；三个原对手维持分别选1/2、2/3拒绝、0/0，原生命34→33→31→27，每次PaidAmount归零，原卡/私有抽牌/两方mana来源及空栈优先权核对。907驱动/103冻结准确断言。 付款数量的来源提示放于阶段文字上方，共用格式翻译/3locale/数值与实际付款射线/只读权限54UI检查。原失败保留，已完成引擎不重跑。证据`.tools/rhystic-1010/audit.json`。总69/81、12待办（3证据/1审查/8决定）不变；A01原组7→8/88。系统goal现已active，继续授权有效。

2026-10-10 Transmute/Orgg原保存LKI/生命组完整完成：A01原stored-last-known-mana-value-and-life完整核销：保留Orgg实际保存/回复生命证明，Transmute四同局UU与独立无神器UU实际完成。牺牲MV3→找MV2/3免差费；MV2→4准确支付2入场且Disk横置；MV2→5拒绝差额3，原所选牌入坟墓场。清空牺牲记忆前保存LKI，搜索时记忆已空而保存值仍正确；无神器实际-1不搜索/不洗牌。5完成施放12mana、350驱动/70冻结准确断言。 两漏译键沿共用Interface表修复，3locale最大字号/原牌选择射线/支付取消33UI增量。证据`.tools/transmute-1010/audit.json`。总69/81、12待办（3证据/1审查/8决定）不变；A01原分类组完成6→7/88。用户继续授权有效，系统goal暂停字段无法由工具恢复；不替换目标。

2026-10-10 共用语言入口修复与A01原Orgg子边界：统一界面/动态提示翻译、参数保持与公开源规则绑定；2765键三语言、8295键/990格式检查和真实UI合同增量。只翻显示层，Forge英文消息/编号/运行包不改；旧全局UI回归失效仍保持。 A01原stored-last-known-mana-value-and-life的Soulgorger Orgg半组完成：实际进场20→1保存19；Revitalize实际付2升至4并抓准确原顶；Unsummon付1将原对象回手，离场仍回复19至23，原同伴/私有手牌/双方牌库与空栈优先权核对。3完成施放8mana、27冻结准确断言；旧FAIL保留，无新交互合同/UI套/编号。 同原子组Transmute Artifact：牺牲后记忆LKI法术力值、失败sentinel门控、清空记忆后的数值保持及更便宜/等价/更贵搜索与差费支付仍待验；其他A01边界保持。 总69/81，剩12（3证据/1审查/8决定）；首552/41不变。证据`.tools/language-unification-1010/audit.json`、`.tools/orgg-life-1010/audit.json`；Goal ACTIVE。

2026-10-10 A01进场生命捕获原组补证：A01原stored-etb-life-payment补齐：Minion实际付6mana/7life成7/7；Nameless Race付4mana/4life且原对手两白色非衍生永久物+两白色坟墓牌仅许可0..4，成4/4；Processor付4mana/5life，随后真实4T生成一原5/5衍生物，保存值随后续生命变化不改写。原零支付驱动FAIL的完整付6mana/0life→0韧性SBA进入坟墓场→空栈优先权结果独立恢复，不重跑零分支。两真实流程5付费操作24mana，原失败保留；实际数字按钮射线/原ordinal/0下限/4上限/必选无取消、三语言最大字号确认与清理通过。 总69/81，剩12（3证据/1审查/8决定）；A01整项仍开放，首验552/41不变。证据`.tools/etb-life-1010/audit.json`；Goal ACTIVE。

2026-10-10 A01第一施放捕获原组补证：A01原stored-first-cast-time完成：实际First Contact145第一张已付2，立即Static保存X1；响应Giant Growth147付1使原Bear148变5/5，游戏计数2而原145仍X1。四原牌21/56/18/11实际磨入坟墓场，选择原21回手并由对手公开确认，原其余手牌隐藏；继续结算生命40→42，记忆清空。第二原First Contact146付2保存X3，磨原42/34/49/17，拒绝取牌，全入坟墓场且生命仍42，真实优先权返回；3施放5mana、176驱动/32冻结断言。实际坟墓场+放逐区二选提示与列表指示漏翻译各补精确两行，三语言最大字号/新布局与原选牌、确认、公开确认、拒绝和清理43去重UI检查。 总69/81，剩12（3证据/1审查/8决定）；A01整项仍开放，首验552/41不变。证据`.tools/first-contact-1010/audit.json`；Goal ACTIVE。

2026-10-10 A01共同出力原结果组补证：A01原joint-payment-different-payload补齐：Collective Voyage真实双方各选五张原Basic入场且横置；Minds Aglow两原副本分别2+取消3只抓2，以及2+3抓5，逐一核对原牌库顺序与手牌ID；Shared Trauma双方磨5张原ID。Mana-Charged Dragon真实宣攻2+3变10/5造成10伤害，原同一场上对象累加器归零；正常回合结束恢复5/5，下次双方0只造成5，无旧值残留。两局有效边界四咒语九已付贡献26mana，取消不扣资源；52冻结结果断言，龙尾段490驱动检查，找地两席位实际射线和选五张确认合计14去重UI检查。 总69/81，剩12（3证据/1审查/8决定）；此内部原输入组已核销，A01整项继续开放，首验552/41不变。证据`.tools/joint-payload-1010/audit.json`；Goal ACTIVE。

2026-10-10 A01两原结果组补证：A01原stored-loyalty-before-damage与soul-burn-black-x-subset补齐：同一真实对局两施放13mana，Drain Life X4全黑对原Jace138忠诚2，伤害过程中原Limit=2，原生damage history4、实际回血2、原138入坟墓场；Soul Burn136实际7mana（三黑二红二蓝），原X部分严格二黑二红、基础2U+B分开，目标原Player2受4伤害，Limit保存伤害前40，实际只回血2；双方最终44/36，正常返回优先权。340驱动检查、25冻结断言；主语言最大字号四真实费用阶段18UI检查，四个BR准确图标及自动支付实际射线原ordinal。 总69/81，剩12（3证据/1审查/8决定）；此两组已核销但A01整项仍开放，不增加首验552/41或新goal编号。证据`.tools/stored-drain-1010/audit.json`；Goal ACTIVE。

2026-10-10 S02.02最后两原组及S02闭合：真实特化选择/对应弃牌/先弃后部分支付取消准确恢复再试，Imoen5-2/Shadowheart14禁用和13合法/当前牌面移区再施放；Gut实际跳过自己END、留过对手END到下个自己END牺牲。修复ChooseColor取消、95准确面映射/主绘图旁路/当前详情、self触发Player→ValidPlayer，受影响费用和异能提示三语言验证。14实际完成付费操作35mana、48冻结核对、6完整native场景1184驱动检查及原成功FAIL前缀；95卡图是数据/解码不是95效果case。总69/81，剩12（3证据/1审查/8决定），50闭包7部分包3执行批次；C03–C06转E01，首552/41、原失败/版本、A01独立payload和A06失效保留。证据`.tools/specialize-tail-1010/audit.json`。Goal ACTIVE；无真实联网或当前全量PASS。

2026-10-10 S02.02窃取原随机/重复/私有许可/离区重入组完成：实际Num2两个不同非地原ID，两次最多3随机候选；来源Donate转控制/Disperse离場不转许可，原双G以两红施放；单候选自动1/空候选0，真实Processor2把原暗置牌移owner坟墓场后ClingB重放逐不再有旧许可或可用牌棚。7咒语+1激活22mana、125冻结断言；原四操作成功FAIL前缀复用，不重跑来源和付款。实际英文窃取提示补精确三语言，六字号/语言前缀+原card/confirm-cards ray与非操作方窗口缺席证据。总68/81剩13保持，因为S02.02尚余两个原特化结果组；完成窃取不再列为未验。证据`.tools/heist-tail-1010/audit.json`；无全量当前或真实联网PASS。

2026-10-10 R51.02最后三原组与R51包闭合：Elven Passage实际接受/拒绝/无Elf搜索解横差异，Molten普通自己回合/真实对手回合Behold Flash/独立下次许可/取消/无Dragon合法性，Endurance实际正常及呼魂取消、绿色手牌已放逐且强制加2已花1mana后精确恢复。5完成付费操作（2咒语4mana、3激活3life）、6取消边界、223冻结断言；修复原放逐手牌取消顺序、呼魂文字费用与标题卡图挤占，三语言默认/最大字号321界面尾段及原成功前缀复用。原FAIL、四core及七组身份保留，0新注册/首验。总68/81，剩13（4证据/1审查/8决定）；R51归档，C02取消合同转E02继续供R02.03。证据`.tools/cast-cost-closure-1010/audit.json`；未授全量当前或真实联网PASS。

2026-10-10 R51.02两原组完成：无Dragon实际额外1B/-3/-3/原源坟墓场，不足总2的Auto不可用/cancel完整恢复；Kindle实际Graveyard Flashback1R+三个原混合区Elementals、提前确认拒绝/原选中不变、少于三张不能施放、原源Exile、敏捷2/2copy/真实结束触发牺牲原ID/空栈优先权。2完成施放4mana/2真实负例/98冻结断言，两个原FAIL的完整成功结果保留/独立核对，不重跑效果。原7组现已核销4组，R51.02余3组；总67/81剩14（5证据/1审查/8决定），0新编号/生产变更/UI套。证据`.tools/behold-alt-flashback-1010/audit.json`。

2026-10-10 R51.02 trigger-cost原Sarkhan成本结果完成：三真实1R施放/实际入场，接受Behold得自有珍宝1，选择器取消/无Dragon得0；原Hand/Battlefield身份、准确私有公开、2/2/原生命/两个mana/空栈本人优先权均核对。3施放6mana、49离线断言；原paid FAIL因原生名字Treasure Token而错断言保留，冻结完整末态恢复证据，不重跑支付。R51.02原7组本轮核销可选Generic和trigger-cost两组，余5组；总67/81剩14（5证据/1审查/8决定），0新编号/UI套。独立Dragon入场其他payload仍A01，非网络/当前全局PASS。证据`.tools/behold-trigger-1010/audit.json`。

2026-10-10 R51.02可选Generic四消费者完成：真实支付/拒绝Behold的Scry2/0、额外目标/摧毁、手牌/战场原Dragon永久+2/+2与未选不改变、原Opt额外4/2实际付款/抓牌，另无神器时未付Behold仍真实合法施放。修复原生正向OptionalCost目标门槛，仅新增TargetRestrictions override，负向共享目标及普通目标保持。12完成施放31mana，303离线核对；旧FAIL/有效前缀保留只补失败尾段。R51.02剩余原7组扣除1组，余6组；整项仍开放，总67/81剩14（5证据/1审查/8决定），0新编号/UI套/首验身份。证据`.tools/behold-generic-tail-1010/audit.json`。

2026-10-10 R51.02四原精确费用子边界完成：Fuse实际付5双半/清理、两个独立增幅不选/红/白/双选实际3/4/4/5及准确paid flags/独立触发结果、礼赠给注册3且目标2的原对象归属、调和原graveyard横置2power付3/拒绝付5及搜索原基本地/结算放逐。4完整流程+1已成功Harm前缀，8完成施放31mana/68冻结断言；4驱动FAIL保留，只补失败尾段，0生产改动/新UI套/新编号。R51.02尚有7个已分类Behold/Endurance结果组，故总67/81剩14（5证据/1审查/8决定）保持；四core从剩余排期扣除，不再重复。四原生礼赠不授生产四客户端PASS。证据`.tools/cast-cost-tail-1010/audit.json`。

2026-10-10 R37.01原轮序范围闭合：6真实流程18实际付费施放49mana/8横置或放逐激活；实际反转/静态方向结合与恢复、额外回合后进先出/仅该回合跳过重置/延迟结束离场、单次两次跳过计数与恢复、步骤/阶段即刻和次维持注册/期限/新增战斗、结束回合原栈卡放逐与能力移除/可选拒绝接受/结束者离场新chooser/清理、排队额外席位离场继续均已证。62冻结精确断言，保留2驱动FAIL，已成功流程不重跑。总67/81剩14（5补证据/1审查/8决定），无新增编号/生产变更/UI套，首552/41保持。NoSchemes模式入口和准确原native结果完整保留既有A01.01 scheme生命周期未验证；R37.03生产四人/指挥官、R02真实远程网络/A06全局依赖仍开放。证据`.tools/turn-wheel-1010/audit.json`。

2026-10-10 R37.02原生命周期范围闭合：当前未改生产双人Bridge实际Divine8自然draw，复用当前真实winner2全报文；实际EndRoomMatch/标准session/Poll/NGO本机消息/客户端最终view、私有信息/旁观者/只读/清理、BO3平局后决胜及防重复计分通过。1新原生8费操作，52成功平局前缀/49补胜局尾段组件断言、171离线准确断言；旧规则/付款不重跑。总66/81，剩15（6补证据/1审查/8决定），0新编号/UI首验，首552/41保持。R37.01轮序、R37.03生产四人/指挥官、R02真实远程网络及A06当前全局依赖仍开放。证据`.tools/terminal-binding-1010/audit.json`。

2026-10-10 R37.02存活共享控制归还修复：控制者离场后仍接受旧付款回复的真实bug已修；原native付款输入/已付Forest/剩余2及modal future/原sources/options保留，新物理本人请求与旧ID拒绝，真实继续/两次抓准确顶8与新私有权限通过；交回后实际取消原牌回手、地/pool全退款、生命/空栈/后继本人优先权通过。2新真实流程/6完成操作21mana，64冻结断言/5guard适配分支，当前2终局释放回归通过。仅Bridge/Gui生命周期修复，四辅助class仅debug line变化，无新UI套；生产四人/UI/网络不据此授PASS。实际EndRoomMatch/会话/BO3最终绑定仍R37.02。总65/81，剩16（7补证据/1审查/8决定），新增0；首552/41保持。证据`.tools/control-return-1010/audit.json`。

2026-10-10 R37.02六席位字段修复：实际2离场后，原第四位在actor/vote/replacementAffected/control/exert/protector全部变3，改为固定注册席位后都为4，空占位0与存活筛选不变。0付款投影增量/38精确断言，实际生产编译仅2个class变化，规范portable manifest已更新；原规则/支付/界面及失败证据复用保留。直接属性/回调上下文只证明序列化，不是实际共享控制/督战/投票/替代/战役效果或四客户端支持。R37.02控制者离场归还及实际终局会话/BO3绑定仍保留。总65/81，剩16（7补证据/1审查/8决定），无新编号/新UI套，首552/41保持。证据`.tools/seat-projection-1010/audit.json`。

2026-10-10 R37.02补完原生防输/效果离场/效果平局差异：一次有效流程4付款操作/30法术力，原白金天使阻止Door→实际Naturalize移除→原2SpellEffect离场、剩余席位继续；原Divine实际2→1→0、剩余三方平局、无旧请求。原生驱动末尾错查Unity字段，FAIL不改写；35准确快照断言补验证，生产Poll/IsDraw/BO3组件9适配器检查通过，无成功付款重跑/新UI套。R37.02共享控制/其他6 seat字段/实际终局会话绑定仍具体保留。总65/81，剩16（7补证据/1审查/8决定），0新增编号，原552/41首验保持。证据`.tools/loss-draw-native-1010/audit.json`。

2026-10-10 R32.01原多人持有者离场范围闭合：实际四席位1在己方回合离场→2、非当前2在3回合离场→3；准确旧源/辅助清理，新持有者实际抓牌/深入，同牛指示物旧2→4核对相对+2。修复退出后旧输入等候/再次向离场者要优先权、playerSeat缩位与终局canRespond；独立零付款权限196及实际生产2人终局10补差异。原FAIL保留，原施放/支付不重跑，0新UI首验。总65/81，剩16（7补证据/1审查/8决定），原552/41保持。生产四人界面/指挥官仍R37.03；R37.02其他损失/平局/控制/剩余seat字段具体待验，无全局或真实网络PASS。证据`.tools/royal-multiplayer-1010/audit.json`。

2026-10-10 S06.01分区原范围闭合：真实Space Beleren施放、七只生物准确分区、同区阻挡/过期、Beta指示物与Alpha消灭；修复原GUI.one被当作空异能卡且隐藏分配对象，改为居中紧凑按钮/准确卡名编号。三语言及最大字号、原选项射线与权限/清理通过。总64/81，剩17（8补证据/1审查/8决定），0新编号，首552/41保持。S06.02战场字段/分组/空间展示仍开放；无四人/真实网络/全局PASS。证据`.tools/sector-native-1010/audit.json`。

2026-10-10 A01.01原抚育缺口补证：两真实原生流程、8付款操作（5施放/3转化）、24法术力、938驱动检查及51离线精确断言：Season+Dawn产生四个40指示物正面；Solemnity+第二Dawn再产生四个零指示物正面，旧40不变；旧146转化40/40并无重复激活选项，新152转化0/0被状态动作删除，七个准确同伴保留。Incubob129造132，付2转化同132为Dark Confidant3/2，1指示物保留；自然下次维持触发、双方94/95分别只读确认原顶130 Air Elemental，入手并失去5生命，记忆对象清空；正常抓原次顶21，只有130继续对手公开，21仍私密。47去重当前UI合同检查，原失败有效14/13前缀加28尾段减8重复；原FAIL不改写。准确确认按钮射线提交原编号/重复拒绝/非接收者不能确认/旧预览清理/两视图最终手牌和生命核对，背面当前异能正文实际mesh。 Incubob实际来源UNK/UB02b存在97713生产catalog，特殊tokenArtKey=TUNK/N.A.无本地可用精确衍生物卡图；反面不得套普通Phyrexian或同名实体Dark Confidant图。规则/操作与当前正文可用，但缺图资源仍在原A01.01同组保持开放，未声明完整展示或所有UNK支持。 总63/81，剩18（9补证据/1审查/8决定）；原编号无新增，首552/41保持。来源`.tools/incubate-payload-1010/audit.json`。

2026-10-10 R47原展示范围闭合：实际两流程/四施放/两次付2转化/18法术力/450驱动检查：Dawn已知20地造两个独立抚育20，转化同145为20/20且147仍正面；Exarch实际双X=0不造衍生物，双X=2费用5造两个抚育3。零地Dawn造两个零指示物非生物正面，付2转化其中一个后0/0状态动作删除，仅另一正面仍在、准确归还优先权。实际反面TMOM17无图已复现；按准确来源关联Incubator // Phyrexian双面，优先准确key，唯一来源配对才回退本地18/18_2，不在歧义来源猜图。171当前执行UI/图路径检查（非独立机制数）：双方原生封包经production Poll、同ID缓存转化、实际缩略图/详情mesh/攻防、零SBA清理、真实射线原145与重复门槛。旧失败驾驶器把富文本20/20和对手响应权限误判，冻结有效21/70前缀，只补缺失尾段75和图路径守卫5；重复21不计。 总63/81，剩18（9补证据/1审查/8决定），0新编号，首552/41保持。A01.01原抚育替代后实际token/counter数量/owner/controller、激活资格负例和Incubob独立背面异能仍保留；Meld完整原件/双面预览归S01.02，活动时空骰枚举归S04.02，原生Flip命中反馈归S07.02，全部原操作/断言保留且均PENDING。A06全局当前依赖和R02真实网络未通过。未知正文保持原文，不宣称全卡全语言完成。 证据`.tools/incubate-native-1010/closure-audit.json`。

2026-10-10 R47.05来源/规则差异：R47.05已记录Teyo临时方向规则正文与Hallowed Healer来源行完成。实际旧原生请求经production Poll：准确Teyo来源/完整段落才本地化，未知追加条件/其他来源保留原文；Healer匹配真实效果ID167、来源名字及source166，显示译名、两个原编号及选择来源正文，不变更原请求。实体列表按正文/标题实际高度留间隔；默认/最大字号完整mesh、周期绑定稳定、真实候选射线提交原索引且拒绝重复。截图发现详情类别仍中文，追加中英日实际TMP类别绘制12项，仅该标签续尾。61尾段+11/18原FAIL有效前缀+12类别尾段=102执行检查（非102独立机制），原失败保留；详情旧Text停用，真正SymbolRules mesh核对。旧支付/方向生命周期/预防结果复用，0规则重跑/新增首验/新编号。 总62/81，剩19（10补证据/1审查/8决定）。R47.05仍开放：抚育实际tokenArtKey/backFace正反面图片比对随A02.01原抚育结算/数量/零值消费者补证据；融合牌与两原件预览随S01.02决定，活动时空骰结果随S04.02决定，Flip结果随S07.02决定。Teyo规则、Healer来源、Spark完整正文/滚动、通知滚动、方向/投票、召集首帧、翻译I/O故障恢复不重复。A06启动/全局依赖及R02真实网络保留；未知规则保持原文，不宣称全部未知文本已经翻译。 证据`.tools/remaining-display-1010/audit.json`。

2026-10-10 R40.01及R40整包闭合：Three exact actual Interlude members161/162/166 from pre-exile battlefield→public exile→return on both seats; current copy artwork and separate destinations. Repeated snapshot does not restart, real frame progresses, retarget begins current pose and is destroyed once. Unknown private ID drops flight without inferring destination; viewer/null/disable resets. Actual no-target each damage one life change and wall damage, Arc own+5/opponent−3 and exact wall damage, Proliferate player ENERGY/POISON and card P1P1/CHARGE/PT, both views, same snapshot unchanged, real numeral meshes and native next actions. Existing life/value behavior stays immediate, not a generic animated delta cue. Existing mana pulse remains bounded same-owner increase. Two actual native stack IDs5/2 animate separately; removal/reinsert timing adapters leave surviving groups visible/blockable and release old ghosts, real ValueOption_0 ray submits original ordinal. Existing casting cancel/return and facedown/notice contracts retained by scope/source. Native cap16 is disclosed. 总62/81，剩19（10补证据/1审查/8决定），新增0原编号。719当前展示检查，0规则重跑，首552/41保留。R02 genuine network/recovery, R37 multiplayer/turn lifecycle, R47 other resource/locale differences, A01 independent generated payloads/mode entries, A06 whole dependency merge and all eight user-support decisions remain original owners. Shared projection animates authenticated IDs between rendered snapshots, not every intermediate native event; no Library/Command identity guessing or hidden information. Existing Stack/cast path owns stack movement. 证据`.tools/zone-motion-1010/closure-audit.json`。

2026-10-10 R20.02复制结果闭合：KeepName两条条件原生注册流程、10次实际付款/6次施放/4次激活、677有效驱动检查及266冻结断言。普通Shapesharer保留名字，4/4飞行→2/2、来源回手后独立保留；真实Clone先成为Air Elemental，再两次KeepName复制熊2/2和鸟0/1，保留这次复制前的Air Elemental名字，实体原牌仍Clone。三准确来源全部回手，接收者Bird法术力异能绑定自身ID，11张地真实消耗。连同旧八牌面/例外字段/继承/名称/复制数证据，R20原复制结果门槛完成。 总61/81，剩20（11补证据/1审查/8决定），新增0原编号。仅核销R20复制结果；正常生产main→run(args,null)仍为双人Constructed，只载入Main/Sideboard，无Avatar注册。测试使用已存在的验收registrationHook及Forge RegisteredPlayer.assignVanguardAvatar，未改变生产GameType、未实现Vanguard模式，条件结果不是生产入口或新Command UI验收。原KeepName完整operation/missingAssertions和A01/A05入口要求显式保留A01.01 variable-vanguard-conspiracy-entry，仍PENDING；包括支持/排除决定、真实生产注册入口、Command展示与后续其他六变量来源，不隐式排除。R23/R47/A06及R02原边界保持，旧成功不为总指纹重跑。 证据`.tools/keep-name-1010/closure-audit.json`。

2026-10-10 R47.05长选择问句：旧Spark原问句328字符仅显示243并缩字到10已修复，当前三语言/默认最大字号271项界面检查通过；固定字号、标题间隔、完整只读正文滚动、查看返回/重复请求/偏好绑定保位置、原生候选与ok确认索引核对。旧复制效果不重跑，未知英文原文保留；窄640x780为临时布局适配器。原FAIL/编辑器热重载与0检查Play退出保留于A06。60/81，剩21（12证据/1审查/8决定），其他动态规则/语言图像仍开放。证据`.tools/picker-fallback-scroll-1010/audit.json`。

2026-10-10 R47.05长通知滚动：修复原文越框且隐藏历史无法阅读，三语言/默认最大字号193项实际UI执行通过；超长正文只读滚轮/拖动、完整末行、新ID回顶/同ID保位置、周期字号绑定和阅读到期/队列/重复/重置均核对。窄540x500逻辑画布/40行混合正文为明确布局适配器，原方向/投票效果不重跑。60/81，剩21（12证据/1审查/8决定）；长选择问句/动态规则/图像与A06/R02仍开放。证据`.tools/notice-scroll-1010/audit.json`。

2026-10-10 R47.05方向/投票文案：已完成实际两行方向通知、蒂维特/祈求权力选项、蒂维特投票值及额外票数字幕的三语言/最大字号差异；中文与卡文搜证/行贿一致。39有效前缀+355续段，中文53覆盖旧措辞；原FAIL/Play中断保留，原5方向/9投票效果沿原证明不重跑。总60/81，剩21（12证据/1审查/8决定）；其他动态正文/长文/资源及A06/R02仍保留。证据`.tools/vote-direction-text-1010/audit.json`。

2026-10-10 R47.05游乐设施结果增量：R47.05原游乐设施结果格式边界完成：插队客实际付4法术力施放、ETB原生六面骰为1，准确原source129/UNF80/public dice消息为Player 1 rolled to visit their Attractions. Result: 1.；两接收者原生ID不同、actor/value相同。旧UI实际无结果对象已复现；共用结果提取支持准确ToVisit格式，未知dice格式保留原文通知，不取无关数字。两席位三语言实际数字/mesh/透明度/不拦射线/相同封包不重播/到期清理78项，未知格式与多值/ignored/natural/coin/planar类型守卫为明确适配器，正文翻译检查是辅助方法输出不是可见历史窗口。另碎裂能量石实际付2施放、横置激活时空骰，真实Constructed activePlanes=null时无掷骰、无通知、堆叠清空归还优先权。两真实流程2施放1横置，247原驱动/43冻结，不能以此证明已支持时空追逐或游乐设施牌库/亮灯/Visit。 总60/81剩21（12证据/1审查/8决定），新增0编号。R47.05剩余：已登记其他动态原生正文/未知回退、任意长文滚动/窄屏及图像/语言边界；attraction原格式/两席位非阻挡结果生命周期已完成，planar枚举通知等S04.02模式支持决定后补差异。召集首次标题/最大字号/查看返回与翻译文件I/O失败恢复已完成不重跑。全部任意字体Addressables损坏与长来源未宣称穷尽；A06启动/全局依赖、R02实时网络和R47.04原缺译审计保留。 证据`.tools/auxiliary-result-1010/audit.json`。

2026-10-10 R47.05召集首帧增量：R47.05原召集首帧门槛完成：复用实际COLORLESS召集原生请求，不重跑付款与结算。默认字号首帧有标题，但最大1.3字号原26px边界不足、实际preferred30.75057px，原FAIL保留。紧凑法术力窗口按实际标题/来源高度预留，空来源隐藏，图标在标题下方、实际菜单边界重算；补该标题准确中文/英文/日文。三语言两字号各初次即时canvas mesh、首帧截图、周期BindScene后与真实查看/返回双射线，标题字号17/22、内容/实例不变，不自动缩小、不越标题框及图标视口。181项当前UI检查；另7项实际原Card122来源+普通C+确认页脚布局适配器，来源换行与标题/图标/页脚分离，不冒充新原生请求/付款。处理体首帧和先前真实资源失败/恢复114项沿原批引用。 总60/81剩21（12证据/1审查/8决定），新增0编号。R47.05仍开放：已登记动态原生正文/未知回退和任意长文滚动/窄屏、attraction/planar准确结果通知、已登记图像/语言边界。原召集首次标题字形/默认最大字号/查看返回与翻译文件I/O失败恢复门槛已完成，不重跑。未知任意长来源名及全部字体Addressables缺失/损坏未声称穷尽；A06启动/全局依赖、R02实时网络保持。类型缺译28中文/31日文规范名和15历史扩展沿R47.04原审计保留。 证据`.tools/convoke-first-frame-1010/audit.json`。

2026-10-10 R47.05资源失败回退增量：R47.05已完成原locale-load-fallback最小门槛：真实只读独占锁使选中中文ReadCards抛IOException，旧实现继续显示日文已复现；修复完整语言资源原子发布及当前失败回退规范卡文，保留请求语言与可用字体；释放锁后同语言真实重载恢复中文。旧失败日志实际触发更新英文请求，确认旧generation不能覆盖或清除新请求。三语言真实详情标题22/正文15固定，无自动缩放；三个原始状态按钮顶部真实射线点击字号不变，返回使用直接Invoke单列。实际冷搜索同故障不抛异常，中文无译匹配、日文和规范英文仍可搜；50重复查询只一次警告，真实重载修复中文搜索槽。原native Shock121 DTO只作原数据展示输入，没有新效果/联网认证。25有效前缀+19有效续段+70相关搜索增量=114执行检查，另原实现10项复现。 总60/81，剩21（12证据/1审查/8决定），本轮完成包内原locale-load-fallback门槛，整项仍开放。R47.05仍开放：已记录召集首次标题字形/首帧资源呈现、其他动态原生正文和任意长文滚动/窄屏、attraction/planar准确结果通知及已登记图像/语言边界；不要求所有卡逐语言重跑效果。处理体首帧沿原证据复用，类型缺译28中文/31日文规范名及15历史扩展沿R47.04原审计。当前只验证翻译文件实际I/O不可读与同语言恢复，不声称全部字体Addressables缺失/损坏都已通过，A06启动/全局依赖与R02实时网络保留。 证据`.tools/locale-fallback-1010/audit.json`。

2026-10-10 R39.01/R39原消费者门槛闭合：R39.01原命名永久字段/自移除/换区/复制/替代门槛全部有结果，本轮补齐最后复制与Moved两类。实际9次付费施放+2次付费激活，原生693驱动/198冻结/272当前UI。小猪永久4/4Spirit/Flying与真实Giant Growth+3/+3作为外部改写，Quasiduplicate复制所得原2/3Boar、无来源日志或飞行。Clone真实可选进场复制仅用可复制基线，保留其自身先授Artifact与后授Lifelink两记录；回手恢复物理Clone0/0及其原ETB关键词，真实重施复制另一个衍生物，自身记录仅一次。Kiki的AddKeywords Haste为可复制例外，后续复制保留敏捷；独立AtEOT牺牲只关联原220，新222与Clone不继承。来源原128回手/重施保留准确三记录并移除普通+3/+3，自然结束步骤原Kiki衍生物消失、两Quasi与Clone均存活；实际消耗28法术力。 实际双方9次付费施放，原生612驱动/204冻结/135当前UI。Tollkeeper真实进场公开对方生物/地，选择原Visionary208，独立永久ETB Updated横置219；三次真实施放均横置且各准确抓牌库顶一张，正常回合抓牌独立扣除。Brittle Blast实际给当时对手生物永久死亡改放逐338，原Visionary受5伤害转放逐，无递归/额外进场；其他原熊仅得死亡记录、未受伤，我方Tollkeeper不被赋予。Pull from Eternity真实放逐到坟墓场、Regrowth到手牌、再次实际施放后两个原时间戳/替代trait hostId208准确保留；Shock真实击杀仍到放逐。实际原生旧对象/新对象、不同区域时间戳及getChangeZoneLKIInfo已记录Battlefield LKI分别核对，日志共享但不重复重放；实际法术力我方7/对方9。 共2真实流程20实际付费操作18施放、1305驱动/402冻结、407当前Poll/详情/真实射线检查；前3批准确自移除/费用颜色/八记录族保留沿原生产者复用，不重跑。 总60/81，剩21（12补证据/1审查/8决定），新增0编号。关闭只限R39原具名消费者最小门槛；264独立持续/条件/触发/异能载荷/工厂原A01/A03全部保留，不能把八种存储记录等同264条均通过。正常永久复制分支的费用/颜色等其他独立例外仍原A01/R20/R44具体归属，不宣称所有perpetual载荷可复制规则已穷举。R20 KeepName/Vanguard可达入口、R44原独立可选/多对象合同、R47动态英文/长正文/字体资源、A06总依赖和初次Play呈现等待门槛风险、R02实时网络均保持原状态。额外Prankster来源重施沿前批未运行标记，不混充结果。生产代码/卡资料/运行包没有修改；552/41首验不新增。 证据`.tools/perpetual-lifecycle-1010/closure-audit.json`。

2026-10-10 R39.01基础/类别/关键词/静态费用字段增量：R39.01字段保留最小增量完成：一局真实10次付费施放、1次付费小猪激活及Sarkhan实际加忠诚，11段实际付款32法术力；506原驱动/323独立冻结核对，243当前Poll/详情/真实原始-当前射线切换，另2个实际DTO颜色格式检查单列。Saga101只给费用<=2的小猪+1力量；小猪实际激活授予Types/Keywords/NewPT124，基础4/4、总5/4、Boar Spirit/Flying。Prankster实际ChooseModes并选两个生物，交换基础力量140：小猪基础5/4总6/4，飞龙4/5，不用被加攻后的5/4当对方基础、不修改空韧性。小猪真实Unsummon回手及施放保留五个准确记录/时间戳，类别飞行及数值不叠加。Sarkhan112实际永久授予原飞龙ReduceCost1和按mana value6付通用费用；特征/卡面4RR不变，首次正常实际支付3RR=5，真实回手后替代费用实际6减1=5；自身异能、原飞行以及空韧性4/5跨换区保留，非新施放许可，两个给定静态异能属于原124。Prankster真实离场到手牌后，两个独立受影响目标记录保留。原生不同Card对象/时间戳和同原记录引用分别核对；双方私有手牌投影检查。原驱动在完整最小流程request132后误追加不可支付的来源重施，原FAIL保留；独立冻结核销仅到132的真实结果，未改写整驱动PASS、未声称来源重施完成。当前UI显示蓝色改写基础、蓝色新增类别/飞行/费用异能，普通+1总力量6绿色，原始状态真实点击返回卡面原攻防，动态英文类别/异能仍R47。 总59/81、剩22（13补证据/1审查/8决定），新增0编号；R39剩余复制/替代继续，未整项关闭。R39.01仍待两类：CopyPermanent/Clone实际来源与受赠者复制基线/独立永久记录/来源离场及受赠者换区重施；Boareskyr原生入场Updated横置和Brittle死亡转放逐、原受影响对象/记录/合法重施。原八记录族具名最小字段保留门槛已由旧Hypnotic/R38和本轮三批R39实际结果共同满足；R38 Robot/Menace旧样本本身仍仅原区域结果，不冒称后续换区。本轮新增小猪的Types/Keywords/NewPT真实Battlefield-Hand-Battlefield，成本颜色及Incorporate沿前批原对象真实Grave-Hand-Stack-Grave，两生产者LosePerpetual仍沿前批身份。所有264独立持续/条件/载荷/工厂A01/A03、全部预览费用/文案语言R47、A06全局和R02实时网络边界保留。额外Prankster来源重施未运行，不属于本轮已证结果。 证据`.tools/perpetual-retention-1010/audit.json`。

2026-10-10 R39.01费用颜色实际增量：R39.01准确费用/颜色实际增量完成：一局双方7次真实付费施放及Easel实际横置激活，679驱动/344冻结结果核对，150当前Poll/真实原始-当前切换射线与费用图标检查。Thought Partition真实公开对手两非地手牌、选原Shock170，永久ManaCost98={5}及Colors98=覆盖白，独立真实Sphere加费1，两次实际施放均支付1再5共6；真实Regrowth支付3回原手牌后保留同两记录、白色和费用5。自己的Shock121经Easel追加UR及复制触发104，特征RUR/蓝红、实际支付税1再URR共4，原件与实际复制共4伤害；Mapping实际支付3，选原坟墓Shock追加U/抓牌触发122、按多色资格返回准确原对象；实际重施税1再UURR共5，原件/复制再4伤害且实际抓准确牌库顶一张。原始付费资源我方14/对方15，最终生命36/32、原记录不重复；字段与实际支付分别证明。原公开非地持续可见、离开手牌后撤销、Regrowth回手不公开准确核对，不能用所有对手手牌为空误判真实公开许可。详情保留原红色全卡图，新增蓝色实际费用5或RUR/UURR及蓝色当前颜色，真实原始/当前切换恢复；两席位和公私字段经过Poll。 总59/81、剩22（13补证据/1审查/8决定），新增0编号；R39剩余字段/复制/替代继续，未整项关闭。R39.01仍待：NewPT/ExchangePower只改力量及空韧性、独立费用静态特征授予；CopyPermanent/Clone来源与受赠者永久记录；Boareskyr入场更新横置和Brittle死亡转放逐/合法重施。八记录族其他已有Hypnotic、R38真实类型/关键词及本批成本颜色/此前自移除沿原身份复用。没有演示或伪造永久费用删除；LosePerpetual两个命名实际生产者只移除其来源异能，已独立验证保留无关修改，其他生成载荷原A01仍保留。264声明/独立载荷/工厂、全部费用预览/动态文案语言/R47、A06依赖和R02实时网络保持。 证据`.tools/perpetual-fields-1010/audit.json`。

2026-10-10 R39.01自移除实际增量：R39.01自移除最小实际消费者完成：一局9次实际付费施放、一次真实免费重施、三个自然攻击，1869驱动/550冻结核对、202当前Poll/射线与详情检查。真实Naktamun为两熊永久+1/+0，Boss赋两个独立原手牌对象；熊首次施放生成一个珍宝后只移除Boss时间戳113，另一对象和PTBoost83保留，Unsummon回手重施没有额外珍宝。Rune同时获Torch独立137，施放仅移除Boss；自然第3回合拒绝免费Play准确保留137，第5回合真实接受并造成2伤害/另给原手牌Dragon新记录后移除137且清理记忆。第二Unsummon/第7回合重施/第9回合再次攻击不再请求免费Play，准确两珍宝/3次3攻伤害/2次2点Torch伤害、生命40/27。旧/当前原生对象共用日志列表但独立受赠者不共用，实际记录不重复应用。当前两公开视图派生异能字段移除，详情蓝色异能与绿色普通加攻分开，真实原坟墓预览二选一取消/确定及过期/重复/非操作/旧回调/清理通过。 总59/81、剩22（13补证据/1审查/8决定），新增0编号，R39未整项关闭。R39.01仍开放：ManaCost替换与Incorporate费用/颜色、ExchangePower空韧性及八记录族准确字段/换区重施；CopyPermanent/Clone来源与受赠者记录；Boareskyr更新入场横置、Brittle死亡重定向/重施。原Hypnotic及R38加攻/赋类别威慑证明复用。264声明/独立载荷/工厂原A01/A03保留；旧/当前对象共享日志本批实际观察，LKI helper仍原源码/字节码审查，不声称玩法复制继承。无origin-trigger的内部guard未捏造可达操作。R47所有动态英文/通知裁剪及A06全局依赖、R02实时网络保持。 证据`.tools/perpetual-removal-1010/audit.json`。

2026-10-10 R38.02/R38原消费者门槛闭合：R38.02原数字牌身份/接收者/最终区域门槛闭合：共6真实流程/13实际付费操作，1784已完成驱动检查、2016冻结核对，331当前Poll/真实射线检查；原六条数字基线复用。首批连续DraftNum2/SpellbookAmount2/Amount3双方独立ID、Wake自然下一回合、Seek各玩家/多Num/Types/Top10不足与原牌库顺序完成。追加实际2W传记与1G熊，原熊3/2被Prototype牺牲，新打印2/2非衍生物被Priest替代到放逐；原生成返回ID156仍由RememberMade赋Robot/神器，原记忆准确捕获且清理。自然Puppet原熊122放逐，Seek原Centaur127压印并获永久威慑，原记忆/压印在实际stats事件捕获且清理；自然传记II Seek原Runeclaw126先手牌成功，再入场被Priest替代放逐，准确RememberFound及SeekAll两次原ID/实际Val扣血增命。另两原生RegisteredPlayer指挥官条件开局，实际3B Herald搜寻原Isamaru，原生可选Library→Command替代接受/拒绝；接受仅原Command、不记忆/不赋Warp/不触发SeekAll，拒绝准确Hand/RememberFound/Warp/SeekAll，原生资源/下一操作者正确。Seek同一成功Hand集合与共享非空门槛供RememberFound和ImprintFound，原生Puppet实际非空压印另验，不冒充逐标志组合穷举。331UI包括246连续选牌/记忆手牌与85坟墓原目标选中确认/替代使用拒绝、双方等待/私有/旧请求/重复/旧回调/清理。驱动末态错枚举LORE/界面MENACE、漏记InputConfirm和JSON空card对象保留原FAIL，完整原结果补验/只续未执行差异，没有重跑成功施放。 总59/81，剩22（13补证据/1审查/8决定），新增0编号。关闭仅限R38原命名消费者门槛。原363数字声明/560完整来源及独立SVars/工厂/非Conjure生成完整保留于A01.01；Giant Secrets原空法术书风险及其转交身份沿原A01记录，不删或改为通过。指挥官两条件样本只验证原生注册/替代，不代表当前Constructed生产入口、四客户端、大厅或完整Commander模式已支持；原R37.03/R02仍PENDING。相同Hand结果集合的两记忆参数共用guard，仅正向压印另有真实Puppet，未做参数组合全穷举。截图长问句英文/截断及全部资源语言仍R47.05，旧早期Play时序/A06全局依赖仍保留。552/41首验身份不新增；生产代码/运行包/卡资料/界面0改动。 证据`.tools/digital-identity-1010/closure-audit.json`。

2026-10-10 R38准确数字牌身份增量：R38.02数字牌准确身份增量：3实际流程/9真实施放/21法术力，1019已完成驱动检查与719冻结核对。Relics连续DraftNum2四次原三选候选有12个全新ID，同名Boros选牌独立ID，准确记忆二选一进场横置；Swine连续SpellbookAmount2移除已选名称，准确两非衍生物/手牌与场上ID/记忆清理。Wake自然跨到自己下回合再次由实际Shock击杀触发，原候选不重用。Sandcloud实际Amount3为每位玩家各生成三独立Desert，实际随机插入及原牌库相对顺序保持；实际Bolt杀Manor使双方Seek各自原非地<=2对象，Opposition Agent不把Seek当搜索，准确私有手牌/no-exile。Bounty两次真实施放分别Types地+非地和Num2两个不同原对象，牌库其他原ID顺序保持；Grow前十唯一蜘蛛与外部龙，Num2只取得蜘蛛，真实随后洗牌与手牌+1/+1。246当前UI检查原四次Draft选中/确认、两次剩余牌名、手牌原记忆对象二选一和确认、双方等待/私有/过期/重复/旧回调/恢复。多接收者驱动在完整结算后错把已施放生物要求入坟墓场，保留FAIL并冻结正确末态补验，不重跑成功对局。生产改动0；原六基线与552/41身份保持。 总仍58/81，剩23（14证据/1审查/8决定），原事项未删，R38记忆/最终区域尚待。R38.02仍PENDING：Seek RememberFound/ImprintFound的准确对象与真实child结果、MakeCard/Seek实际替代后返回对象/最终区域/记忆/SeekAll差异未冒充已完成。精确下一样本：Prototype X-8实际牺牲已施放熊并MakeCard，Containment Priest将非施放复制牌替代至放逐，核对RememberMade接收者/Robot神器字段/清理；Puppet Raiser真实原坟墓对象放逐后按CMV+1 Seek、ImprintFound赋Menace/清理；Naktamun自然II Seek/RememberFound接原对象并由Priest将后续入场替代到放逐。应先核对原script/实际条件，三个后继可同流程共享，未启动。Seek本身Library→Hand替代及全部生成工厂仍按原完整范围保留，不用后续Hand→BF替代混充。原A01独立payload/factory与R47所有文字语言/A06依赖仍开放。两次早期Play UI回放曾在共享控制器门槛失败；同步读取的原own/wait正确，稳定Play当前58实际射线通过；原FAIL保留，不以推测原因消掉早期时序风险，A06原启动/证据范围保留。 证据`.tools/digital-identity-1010/audit.json`。

2026-10-10 R03.02/R03原消费者门槛闭合：R03.02原多方消费者门槛闭合：原11组投票/方向/恶徒选择、GainControlVariant五模式、SwitchBlock合法性/同band/目标离场/有限与无限容量、Camouflage合法池/数量/真实随机关系均沿原命名结果核销。本批补4实际流程/13实际付费操作：原Brooding触发时Bolt/Shock杀来源，非衍生物归还且真实捐赠衍生物保留外国控制；Plotter实际换地、Herald实际施放并离场，Land.YouCtrl按原来源控制者筛选，外部池自有地不归还；Split平票复制原堆叠目标、两次准确原顶牌ID抓牌与多数反击无抓牌，AllVotes准确原投票者及Grudge两条自然扣血触发。1876冻结结果检查，原失败因复制源新ID误判后用已完成原记录补验，仅继续未完成多数分支。321当前UI检查补堆叠目标/投票原对象及门槛、双方等待/私有/过期/重复/旧回调/恢复和三语言查看返回，其他相同控件按当前哈希引用旧202证明。原47声明结构字段逐一保留、完整文件与子SVar未删，不以每张牌追加重复对局。 总58/81，剩23项（14补证据/1审查/8决定），新增0编号，生产改动0。原A01.01仍保留全部独立子效果/谓词载荷/生成工厂与激活可达性；R37.01/R37.03与R02保留原生AI、多防御者与生产四人客户端支持，普通main仅两Human、无registrationHook，旧战斗排序开关同为当前正常入口未启用的条件边界，不是相应流程已通过。R47.05动态问句/长文/全部语言资源与A06.01当前全局依赖仍开放；R03归档中的完整条件分支和原字段持续保留，启用模式后复核原消费者结果。 证据`.tools/r03-payload-1010/audit.json`。

2026-10-10 R03.02伪装有限容量：R03.02 Camouflage额外有限阻挡容量：实际G施放与三原生宣攻，Brave the Sands使两原有限墙可进入前两堆且第二次选入后从第三堆候选精确移除；Wall of Glare无限阻挡保留全部三堆。实际标准Random抽样配对、完整原ID阻挡关系、显式分配加单阻挡者自动伤害、全部场上原ID/特征/横置/池/生命/牌库/手牌与唯一咒语入坟墓场，恢复MAIN2玩家1。1新实际流程，297冻结检查；驱动421已完成检查后遗漏自动伤害的FAIL保留，完整终局已存，独立补验不重跑，不计驱动整套PASS。原418/209 UI证明按准确合同和当前消费者哈希引用，无新增UI套。 总57/81，剩24（15证据/1审查/8决定）。GainControlVariant五原生模式及SwitchBlock边界已证，原生产载荷/来源资格/工厂与Vote完整flags/child payload仍需按原冻结字段逐项对照，不以每张牌增加重复对局。Camouflage额外有限容量已实际核销；当前生产main→run无registrationHook，只创建两BridgePlayer Human，AI声明与多防御者流程的正常启动入口尚不支持，原条件边界及原R37/R02支持事项保留，启用模式后再补真实流程。旧战斗排序同为当前入口源代码未启用。R47动态问句/长文及全部语言、A01工厂可达性和A06全局依赖仍开放。 证据`.tools/camouflage-boundary-1010/audit.json`。

2026-10-10 R03.02阻挡交换边界：R03.02阻挡交换原边界补证：真实攻击组合并入同band，Jarkeld原子保留；多对多交换完整阻挡关系并实际分配战斗伤害；Sorrow响应原Bolt杀死一目标阻挡者后不半交换，原已阻挡攻击者不对玩家造成伤害；飞行导致任一交换配对非法则整组保持。Wall of Glare/Palace Guard原生CanBlockAny不等于canBlockAdditional：修复接收者无限容量被误判有限的生产反例；额外阻挡一只的Brave the Sands有限容量二允许交换，两个方向的有限容量不足均拒绝。6新实际差异加2定向回归共8真实流程，2337驱动/6980冻结检查，209生产Poll与实际射线UI检查，含band/额外阻挡零点分配、攻击者与防御者分配、原ID/金额/确认/等待隐私/同会话过期重复/结算清理。 总57/81，剩24（15证据/1审查/8决定）；本批完成R03内部边界，整项仍开放。GainControlVariant五原生模式最小差异引用原direction/control-variant证明；原AllValid其他实际载荷、离场/资格/当前控制者与来源依赖、生成工厂仍保留原A01/R03完整字段，不按每牌扩运行任务。SwitchBlock同band、目标阻挡者离场、飞行非法、多对多完整交换、无限与额外/不足容量已有实际证据；旧战斗排序当前生产入口源代码不启用，保留条件身份，未来GameRules入口变化再验证。R03.02仍保留原Vote完整flags/child payload核对、Camouflage额外阻挡容量/原生AI/多人每防御者攻击者集合，按原R03/A01/R37归属。四席位客户端与拥有者3/4投影保留R37/R02，动态问句/堆叠长文英文与裁切留R47.05，A06全局依赖仍开放。 证据`.tools/switchblock-boundary-1010/audit.json`。

2026-10-10 R03.02控制权原生差异：R03.02原GainControlVariant剩余三种真实差异：先实际5法术力Switcheroo交换原熊/蜘蛛，再实际3法术力Donate捐原Anthem，Homeward真实横置归还各自拥有的生物，非生物Anthem仍对手控制、静态攻防精确重算，8法术力与来源横置/两咒语入坟墓场准确。Scrambleverse实际6RR，8原非地按原生标准Random四席位抽样分配，收集前不转移，受未横置Guardian Beast保护的Sol Ring虽抽中外国接收者仍保留控制；原生Untap子效果包含受保护物，准确8法术力/原来源入坟墓场。Inniaz天然三飞行者实际宣攻触发，原施放者连续替四接收者从右邻原候选选牌，全部收齐才按原ID转移、拥有者/时间戳/攻防/伤害/指示物不被伪造，原三攻击者保持战斗且恢复原操作者。三真实流程/1208原生驱动检查/11049冻结对照，202定向UI检查原候选与接收者、确认门槛、等待私有信息/过期重复/旧回调/结算清理。 五原模式最小差异完成，引用原方向结果不重跑。总57/81，剩24（15证据/1审查/8决定），无生产变更。GainControlVariant五种原生模式的明确最小差异已有实际证据：方向两模式与空候选引用原direction-choice-1009，本批补归还拥有者/随机/施放者替各接收者选择及CantGainControl。原AllValid的其他实际生产载荷、离场/失去资格/当前控制者与来源依赖、生成工厂可达性仍按原A01/R03的完整原字段保留；不新增每张牌运行任务。R03.02仍保留原Vote完整flags/child payload及SwitchBlock同band、目标阻挡者离场、受限合法性、旧排序设置；Camouflage额外阻挡容量/原生AI/多人每防御者攻击者集合按原R03/A01/R37。生产四席位仍只导出两个玩家且无Gui3/4的拥有者为0，明确保留R37.03/R02；动态问句/堆叠长文英文与裁切保留R47.05，A06全局依赖不核销。 证据`.tools/control-variant-1010/audit.json`。

2026-10-09 R03.02阻挡交换/伪装增量：R03.02原SwitchBlock/Camouflage最小实际差异：General Jarkeld真实横置/两原攻击者目标，合法关系精确交换且武士道只一次；飞行阻挡不合法整组不变；真实Bolt令一个目标离场，不半交换。Sorrow's Path真实横置/两原阻挡者目标，准确2自身伤害与生命-2，移出再阻挡令武士道第二次触发，真实战斗导致原Spider死亡；不限阻挡者同时阻挡两攻击者的交换容量不合法整组不变。两条实际G施放Camouflage/宣攻后替代宣阻挡：四原堆按原生随机Fisher-Yates配对，普通候选选过移除、不限阻挡者可重复、允许空堆，飞行/威慑数量不足过滤、Rhino上限精确单选，准确战斗伤害/唯一墙死亡及恢复。零合法阻挡者原生跳过宣阻挡/替代与随机，实际咒语入坟墓场、5无阻挡伤害/MAIN2优先权；原已完成结果通过冻结复核，保留驱动错误，不重跑。 修复原生伪装候选过滤，foreach移除跳过紧邻横置生物，改倒序删除。8实际流程/2343有效原生驱动检查、10117冻结字段对照、418定向UI；源包仅新增Camouflage覆盖类，原类/库全部字节不变。总57/81、剩24（15证据/1审查/8决定），原11组最小验证及本批两API最小消费者完成，整项仍保留明确边界。R03.02原11组最小断言全部完成；SwitchBlock/Camouflage本批只完成明确最小实际消费者和上述差异。原同band入口、目标阻挡者离场与受限合法性分支、旧排序设置/多对象原入口仍按R03/R29；Camouflage原生AI/额外阻挡容量及多人每防御者攻击者集合原入口/结果按R03/A01/R37保留。原转入Vote/GainControlVariant完整flags/child payload及51/52声明原身份保持，未删完整原字段。R47动态问句英文/全部长正文/语言、R02/R37实时客户端/Commander及A06全局依赖保持。 证据`.tools/combat-choice-1009/audit.json`。

2026-10-09 R03.02多对手重复反派选择组已核销：R03.02原多对手/重复反派组：实际Davros1UBR、Valeyard2UBR、两次Bolt各R，准确只对原玩家2/3造成3伤害，玩家4不变；天然本回合结束步骤生成唯一自身3/3黑色神器Dalek/威慑，原玩家2先收齐Draw/Discard才逐项执行，抓准确牌库顶1ID并由玩家2选自己原Runeclaw弃牌；原玩家3合法原生输入连续Draw两次，两项收齐前手牌未变，准确原牌库顶2ID进入施放者手牌；未受损原玩家4未询问，所有原对象/拥有者/时间戳/其他区域不变，准确原玩家2记忆在弃牌期间存在、转下一玩家前及结束清除，回到原玩家1优先权。两支持视图精确公共ID/生命/隐藏区数量/自身私有手牌，实际Poll移除等待方请求。原生4席位另2只是合法AI夹具，不是生产四客户端。 417原生驱动/409快照对照、172定向UI检查；原11组完成11组，整项仍保留原转入API/完整payload。总57/81、剩24（15证据/1审查/8决定）保持。实际修复共用选项行遗漏行距导致长文本超行，最大1.3/三字体及短玩家选项验证。旧失败保留，已完成施放不重跑。证据`.tools/villain-multi-1009/audit.json`。

2026-10-09 R03.02对象票数/弃权/逐玩家后续选择三组已核销：R03.02原对象票数/可选弃权/EachVote三组：Trap实际2U，三原投票者票数2/1精确原生tap与stun、未获票对象不变、同对象时间戳与记忆清除；Vault11实际2WB，天然章节I生成两个独立1/1人类士兵，轮序至II全员弃权各抓准确牌库顶ID，III两对象1/1平票全且仅最高票生物摧毁与传记自动牺牲，无额外抓牌；Expropriate实际7UU，Time真正进入额外回合，Money由施放者选原投票者拥有而非控制的SolRing，原玩家3唯一拥有物自动原生选择，准确保留拥有者/更改控制者/来源放逐；Elrond实际3GU，Fellowship由原玩家2选自身生物，保留原拥有者且不能攻击拥有者，Aid两票准确当前自身三生物各两指示物及实际攻防，作用域记忆/imprint/Aid清除。追加实际U Illusion后2U Trap同一玩家连续代三票，原归属1/2/3、额外AI未代答；真实Poll与射线点击、原生确认门槛/弃权/等待权限/过期重复/恢复/结算后清除。 340定向UI检查；原11组完成10组、剩1组及原转入API。总57/81，剩24（15证据/1审查/8决定）保持。修复可选投票“取消/必须一张”误导及连续代投标题缓存；原生运行包未改动，旧失败与成功版本保留。证据`.tools/vote-resolution-1009/audit.json`。

2026-10-09 R03.02秘密/受控投票组已核销：R03.02原秘密/玩家/Other/受控投票组：Círdan实际3GU，秘密收票前不公开，三玩家票数2/0/1准确抓原牌库顶ID，零票玩家2自己的合格手牌选择原Runeclaw入场；Mob实际2RR，排除原投票者自身，按原票数准确玩家/生物伤害、致命离场及施放者抓牌。Illusion实际U后再付2RR代全部三玩家投票，Other候选相对原获票玩家，票数归属仍原玩家；有Ballot时原玩家2先自己选额外1票，再由玩家1代两票。四新真实流程/六次付费操作，追加两个共享字段受影响的普通异能/卡牌投票定向回归；实际Poll/射线确认玩家选择、原生门槛、代投标题、等待权限、过期重复旧回调、非投票/结算后上下文清除，新标题中英日及选效果/选卡路由。 330定向UI检查；原11组完成7组、剩4组及原转入API。总57/81，剩24（15证据/1审查/8决定）保持。发现并修复代投Other候选错误；旧失败与断言修正原快照保留。证据`.tools/vote-followup-1009/audit.json`。

2026-10-09 R03.02投票三组已核销：R03.02原三组投票结果最小门槛：Plea实际3U施放，多数Time安排并实际进入额外回合，平票准确抓原牌库顶3ID并进入下一对手回合；Tivit实际3WUB进场，Brago强制额外票与Tivit/Ballot可选0/1票，连续原投票者/原票数归属，3/3、2/2及0/6准确Clue/Treasure原生生成ID和接收者；Council实际1WW，平票全且仅两个最高票原ID放逐，空候选无选择/事件直接恢复；Custodi实际4W，自己坟墓场合格池排除瞬间/他人牌，准确多数/平票原ID回手。全部投票收齐前不执行子效果，未受影响区域、公共两视图原ID/数量、记忆清除/下一操作者。9实际流程/2306规则断言，228定向UI验证同一原玩家连续投票、换投票者、0/1草稿与确认、等待/过期/重复/窗口退出；卡牌投票的相同单选合同引用原101点击，不重复跑。 累计原11组完成6组，剩5组及原转入API范围；总57/81，剩24（15证据/1审查/8决定）保持。证据`.tools/vote-choice-1009/audit.json`。

2026-10-09 R03.02方向生命周期已核销：R03.02原direction-restriction-lifecycle最小门槛：实际5法术力MysticBarrier进场选左，自然四席位轮序后自己维持改右；实际Pramikon支付URW，在尚未进战场的ETB替代选择右后正常进入；实际Teyo从3忠诚-2选择右，真实Command效果复制方向，在全部3对手回合持续并于自己下回合移除。四原生席位各针对最近对手/其旅法/战役的准确限制矩阵，真实攻击请求准确合法选项；改右及到期后真实canAttack反转/恢复。3实际流程/3453规则断言，48定向双视图UI核对同原ID294效果持续同实例及到期消失；4个方向输入与上一轮相同合同复用101点击证明，不重跑。 累计原11组完成3组，剩8组及原转入API范围；总57/81，剩24（15证据/1审查/8决定）保持。证据`.tools/direction-lifecycle-1009/audit.json`。

2026-10-09 R03.02方向增量：R03.02原方向两组最小门槛：实际Order左右施放及空候选，四原生席位按方向选择，收集前不转移，准确所选原ID/拥有者/时间戳/控制者，3U付款及坟墓场；实际Aminatou左右-6统一转移所有合格非地，原来源/地排除，忠诚8→2，无逐人选牌，原回合/空堆叠/下一操作者。5实际流程，750规则断言、278原生/两支持视图对照，101定向UI。第三四席为普通原生AI只读观察，不是生产四玩家客户端。 原11组中核销2组，仍有9组及原转入API范围；总57/81闭合，剩24（15证据/1审查/8决定）不变，新增0。原错误期望/空card测试失败保留，只重试选牌差异；未重跑方向或结算。证据`.tools/direction-choice-1009/audit.json`。

2026-10-09 R44.02及R44原最小门槛闭合：R44.02原多对象替代最小门槛完成：真实Spark施放及Interlude三对象同时返回，旧候选排除同时进入者；非传奇生物/旅法复制各按新类型追加指示物2/4，不复制来源7/8，准确原ID/新时间戳。另实际双对象返回拒绝复制，原0/0正常进坟墓场且另一张继续返回。实际Goose响应Cutpurse后依次控制者/种类/翻倍/追加衍生物：食物线索珍宝各2、松鼠6，共12不同ID，控制者2、拥有者1/2。Arc Trail原2/1与加伤/系命准确聚合；EachDamage来源1+2、自伤/互伤/来源防止选择/旧目标闪烁不命中新对象、系命1及同次Enrage只触发一次抓原顶牌、Pridemate加1，费用/区域/下一操作者核对。170实际Poll/射线验证原选项、复制候选、选中确定、可选使用/拒绝、受影响玩家来源选择、替代排序及恢复/只读/过期/重复守卫。 原81编号保持，57/81已闭合，剩24项（15补证据/1审查/8决定），新增0；552/41首验保持。测试期望两处误判用冻结原生结果复核，未重跑成功结算，未修改生产代码。数字生成/抚育融合/平面装配的原A01/R38/专属模式所有者仍开放，R47长正文截断/英文也保留。证据：`.tools/multi-object-replacements-1009/audit.json`。

2026-10-09 R44.01最小规则/交互门槛闭合：R44.01原最小操作/决策者/计数与UI门槛：真实Ichor Rats两玩家各自顺序得毒3/4，Leeches按原3伤害移除自身；Attune、Amplifier、Vorel、Invigorating Surge验证玩家/永久物、全部/指定类型及Vorinclex加法替代；同玩家Energy/Poison与熊P1P1/Charge混合增殖、Tekuthal两次/空选择及Ezuri每次抓牌准确顶牌ID，未选对手不变；Solemnity禁令仍保留搜地/抓牌结果；实际Ephemerate令旧Vorel目标时间戳失效，不改变返回的新对象；Fallout实际X2两玩家各辐射4，双方自然Main1各磨准确4张，非地2失生命2并剩辐射2；X0及禁止添加；原生Mine/ALL/Clear All循环只修改待确认选择。 修复玩家选中状态漏导出、混合窗口按钮重叠及批量选择含义，三语言/最大字号验证。原81编号保持，56/81已闭合，剩25项（16补证据/1审查/8决定），新增0；旧552/41首验保持。证据：`.tools/player-counters-1009/audit.json`。R44.02/联网/多人离场/指示物cue等原归属继续开放。

2026-10-09 R36.01及原包现已闭合：原QuasiLibrarySearch风险已实测：真实支付Doubling Chant，Opposition Agent原生控制同名合法牌库候选，接受/取消后临时控制条目清零、静态搜寻时间戳不变、下个操作者归还；准确牌号/牌库差额/费用6/生命/手牌/来源入坟墓场对照原生观察。接受原流程漏放逐已复现并修复Searched清除FoundSearchingLibrary；选中的准确熊现在被放逐并生成密探权限效果标记。真实支付Dichotomancy搜寻对手牌库原错误接管/泄露施放者手牌已复现，修复只在搜寻自身牌库时接管，取消用finally局部归还；搜对手准确熊正常由施放者选择并获控，9法术力/来源/私有信息正确。普通Rampant Growth密探搜寻实际支付2、准确Island放逐正向回归通过。实际原生输入经生产Poll和Unity射线，原候选/选中确认/取消、双方私有候选移除、返回后的窗口清除及旧/重复/只读操作拒绝77检查。 加上原终局证据，原风险接受/取消/外层归还和对手牌库隐私均完成。原81编号保持；55/81已闭合，剩26项（17补证据/1审查/8决定），新增0；原552/41首验保持。证据：`.tools/controlled-search-1009/audit.json`。

2026-10-09 R36.01核销纠正：部分付款/多来源替代中的实际认输终局、等待释放、旧双方响应拒绝、正常抓8张、客户端窗口与回放清理、终局注册席位修复已完成。QuasiLibrarySearch可选选牌拒绝后的临时搜寻控制归还未验证；沿原R36.01保留，不能用终局认输替代。 最后复核原pending-source-risk记录后撤回55/81核销声明，准确为54/81已闭合、剩27（18补证据/1审查/8决定），新增0。两处生产修复与成功原生/UI证据均保留，仅续测剩余受控搜寻分支；原旧风险没有被新增成另一个事项。证据：`.tools/controlled-concession-1009/closure-review.json`。

2026-10-09 R36.01及原包闭合：R36.01当前1v1控制者在部分付款队列或双来源替代modal等待时实际认输：准确赢家、只终结一次、旧双方响应拒绝、原生worker释放；部分付款原生取消未完成施放、手牌数恢复并重置已付地；已付替代链保留资源/堆叠目标。修复终局活动玩家移除导致回合/优先权/堆叠席位重编号，保持注册席位。修复modal Future仍等待的真实阻塞：终局取消已登记回调、晚到publish拒绝、只识别专用终局异常；正常原生替代链仍抓8张并恢复控制者优先权。客户端实际wire经Poll/Present清掉两类窗口、双方请求权限、旧按钮与回放；普通战斗旧请求只补清理合同适配器。已知手牌知识不强行删除，原生投影政策保留。存活多方归还R37、实时网络R02、总体依赖A06继续开放。 原552/41首验身份保持；81项已闭合55项，剩26项（17补证据/1审查/8决定），新增0。证据：`.tools/controlled-concession-1009/audit.json`。

2026-10-09 R47.01闭合：中英日静态选牌/查看/确认/搜证标题、玩家默认名、忠诚值徽章、卡牌排序按钮、占卜/刺探去向、关键词与当前/原始状态标签修复；实际原始编号射线、重复/只读守卫、查看返回固定字号及默认/最大字号容纳。新增搜证/刺探/泛型标题仅展示适配器，原生结果复用五份冻结请求；资源未就绪/首帧/切语言过渡与任意长正文仍归R47.05。 原生规则未运行，既有552/41首验身份保持；全部81项已闭合54项，剩27项（18补证据/1审查/8决定），新增0。证据：`.tools/static-labels-1009/audit.json`。

2026-10-09 R04.02及整包R04闭合：四条实际原生流程核销三组：同名双Mage和异名Voidstone，Needle/Revoker非mana及mana禁令、双方精确合法选项/canRespond，逐源实际Disperse移除后剩余禁令保持、最后实际施放/起动恢复；Archon选Instant/Iona选Red，全玩家类别与仅对手颜色禁令同时存在，己方红色法术不受对手颜色条款限制，依次Spellbomb移源后准确恢复；实际施放Council/Key/Stenn/Curse/Peacekeeper/Flute并原生选名/类型/附自己，己方Electrolyze加3+2减2+1+1成2UR，只剩3mana不可用，移Flute后UR、部分tap取消精确退款/原牌/空栈，重试付2；对手先被Council禁，实际移Council后只受Peacekeeper加2，支付3UR=5，双方2伤害各抓原牌库顶1；两方Icy被Flute禁止，移Flute后各实际付3，移Peacekeeper后己方另一准确Icy付1，两个准确Llanowar在同名Flute仍在时实际各产G且无税，剩余来源/精确ID/区域/资源/下个优先权分别核对。 42项主语言定向UI检查：真实mandatory Card type直接1选项和color1-of1选中/确认门槛，实际射线原生索引、查看返回固定字号、无虚构取消、过期/重复/只读守卫及禁令解除前后同一Shock原选项ID。既有指定牌名全池/过滤/分页/本地图/三语言、look/player/card/targets、费用符号/取消/优先权合同复用，未因6来源或数值再乘语言测试。 仅关闭R04.02原三组并与R04.01归档R04。候选计算/排除/Note/秘密/随机A02、独立trigger/look/其他payload A01、声明及费用/持续事件A03/A04、未知量宣布后限制、Vanguard模式、R47长文本/语言资源和A06整体依赖仍开放。Union生产者保留原086e运行包，其余三流程当前cae3；费用驱动最后58常量错误原FAIL不改，独立捕获核对60+2=62→61及准确牌库顺序/ID/资源，未为驱动统一重跑。首552/41及原81编号保持，四新增实际流程、0成功原生重跑；失败诊断/驱动修正均保留。不授所有费用/所有卡牌、实时联网、四人或当前全量PASS。 总53/81已闭合，待办28（19证据/1审查/8决定），新增0。只改稳定选择禁令过滤，完整运行包仅两类变化；4原生流程及42UI检查，未重跑成功生产者。

2026-10-09 S03.01及整包S03闭合：两条真实原生流程：实际施放Esgaroth，Notion Thief令对手抓其原牌库顶准确ID，自己牌库顺序不变；选择自己Shock弃牌后被Rest in Peace改为放逐，仍生成正确归属1/1人类士兵；实际起动Champion支付生命1并由自己选择准确Champion衰萎2，Vorinclex翻倍成4且5/5变1/1；目标对手由第二席选择准确Grizzly，衰萎2减半成1且2/2变1/1；再实际施放Morcant，Defined Opponent由第二席选择另一准确Runeclaw，衰萎1向下取整成0，既有指示物不变。双方投影对照独立原生观察、准确ID/区域/资源/费用/私密性及空堆叠后恢复优先权。 87项定向窗口检查：实际衰萎费用选择三语言提示、准确己方卡ID与费用数量、对手targeted/Defined窗口准确第二席及各熊ID、真实射线选项/原生确定、过期/重复/只读守卫、查看返回固定字号、最大字号完整显示、取消按钮回调和恢复后清除。另24项中英日实际堆叠来源/受影响玩家2/数量2、Text与Symbols子控件一致、固定字号完整显示/查看返回及未知扩展句原文回退。 仅关闭S03.01原受影响玩家/抓弃及指示物替代边界，并与既有S03.02和原十场共同归档S03。固定运行卡池10条直接Recruit均为默认自身，不为不存在的直接对手Recruit造场；对手实际受到抓牌替代且原生已验证。原十场旧生产者身份保留，不冒充当前全量。静态替代来源安排在开局，不授其施放/进场证明；费用取消仅新增UI回调检查，原生多段费用计数回滚沿用R12。R44其余替代、R39永久日志、A01独立payload、R02联网、A06整体依赖保持开放；不授四人/全部卡牌/全部费用互动PASS。 总待办29（20证据/1审查/8决定），关闭S03.01、归档S03，新增0；312驱动检查/1022冻结断言/111UI检查是流程内断言数，不是机制数。

2026-10-09 S03.02闭合：一条真实原生连续流程：施放维苏瓦化妖复制啸风食人魔，原生热情赋敏捷后两者实际攻击，各自1伤害并强度1→2；准复体复制已增强来源产生新衍生物，起始1/自身增量0，来源与化妖各自增量1保持独立；Unsummon使原来源准确ID回手，复制品不变；下次维持化妖实际复制灰棕熊，保留自身增量1、失去起始强度关键词及食人魔攻击触发，总值1/基线0，仍有复制维持异能。双方可见投影、独立原生字段、准确身份/区域、费用5/3/1、伤害1+1+6、无隐藏手牌泄漏与继续主阶段分别核对。 278项中英日/双方视图定向UI检查：复制起始1保持基线、自身增量标蓝2、同ID再复制无起始关键词仍显示蓝1；实际射线切原始/返回、固定字号、正常来源原始1、复制衍生物完整印刷起始文本。修复仅已知实体原牌的复制品原始详情，不附加复制状态的强度基线；未知实体原牌回退、暗置过滤单列适配器。实际截图查看蓝2/蓝1及实体原始无强度。 仅关闭S03.02原复制/起始关键词变化边界。S03.01征募/凋萎其他玩家及抓弃/指示物替代、R39永久修改日志、A01独立payload、R02真实联网及A06整体依赖继续开放。开局来源/热情仅安排在场，不授其施放/进场证明。旧552/41首验身份保留；1新增实际流程、0原生重跑，检查数不当机制数；不授当前全量/四人/所有数值组合PASS。 总待办30（21证据/1审查/8决定），关闭S03.02，新增0。

2026-10-09 R47.03闭合：修复真实暗置私有预览牌名/原文与五条准确原生状态段落，中英日、双方身份/牌图权限；真实截水跨区可选目标与铸阳锤卸装提示三语言、准确卡牌/确认射线、过期/重复/只读守卫、查看返回固定字号及最大字号容纳；卡尔杜I/II/III与反身正文按来源准确匹配既有翻译，Added once来源编号和实际卡牌/玩家目标保留并本地化；未知段落/不同来源尾注及缺译原文回退保留。 三语言311定向执行检查，另1次直接完整TMP/私有预览截图取证；复用18份实际历史输入，0原生重跑。仅关闭R47.03登记的实际已知句型与R18转交章节例子。原生结果复用原始批次，不授当前全量、实时联网或全卡池翻译PASS；共用静态语言残留R47.01、资源未就绪/切换R47.05、任意长正文滚动与A06总体依赖仍保留。截图中的原卡图语言按本地实际印次保留，不重绘卡图。 总待办31（22证据/1审查/8决定），关闭R47.03，新增0；原552/41首验身份保持。

2026-10-09 R18.02及原包闭合：四条真实原生流程：两次施放Founding分别从II/III开始，原生1/2/3枚举，准确跳章/源ID/磨四张顶牌顺序/原Opt目标放逐/拒绝施放复制及正常最终章牺牲；实际施放Kardur由Vorinclex将入场Lore1翻至2，同时独立I/II，牺牲准确熊后才选择反身3伤害目标，每位弃准确牌；最终III挂栈时源保留，实际两Conduit各移1降到Lore2，返回原熊/两指示物/4-4/敏捷且Saga保留，下次原生增Lore4再返回另一熊并正常牺牲；Forum与Parallel Lives真实两Inkling、两个不同Immediate触发各记一个准确对象，子异能脱离父链、两件武具分别正确附着且攻防4/1和3/2；Etched Slith实际战斗1伤害，Solemnity使记忆计数0，不产生反身触发或多余目标请求。 61项主语言UI检查：ReadAhead原生1/2/3真实枚举复用右下数值控件，最小/最大、本地草稿、确定2/3射线原选项ID、无虚构取消、只读/过期/重复/查看返回字号；牺牲后的真实目标/选中确认准确玩家射线及来源、已牺牲熊不在战场；结束后控件清除。旧APNAP四场/Fable三场及三语言排序/普通目标/数字合同保留。 只关闭R18原章节与转交ImmediateTrigger消费者门槛，不代表全部章节payload通过；独立效果与Skateboard准确tap仍A01，旧生产者未知/全局当前依赖A06保持。实际截图的Kardur章节和Added once动态英文仍归R47.03，未知自定义文字原文保留。无生产代码/原生运行包/卡牌修改；没有重跑已成功ReadAhead及旧七个基线，无实时联网/全语言/四人PASS。 总待办32（23证据/1审查/8决定），本批关闭R18.02/归档R18，新增0；原552/41首验身份保持。1399驱动检查与2834捕获断言是四流程内检查数，不是测试机制数。

2026-10-09 R35.01及原包闭合：R35缺字段增量：复用真实Kick in the Door效果183等的双方存在/到期输入，真实射线核对来源AFR/153卡图、完整原生正文、准确来源标题、同ID无重复及到期移除；复用真实Stomp历险许可148/CLB781全卡图、完整许可及施放本体后移除；真实预备咒语145与原生施放选项保留，隐藏内部辅助对象146；真实Bauble非离场延迟保留钟且没有临死复制品外框；非公开来源匿名字段/无图只做明确展示适配器。57项UI执行，中文主语言，动态英文完整保留。 桥接Command投影遵循原生getRenderForUI，移除原生明确setRenderForUI(false)的空白Prepared辅助对象；实际预备牌/权限/原生结算不变。仅此一个源码条件，运行包两类变化，switch helper全部字节码指令相同；六份完整双方视图除146移除外逐字段等值。 原R35.01/Twisted Caverns不可反击Effect标签错误：实际脚本DBCaverns是Pump临时不能攻击，不创建该Effect；同流程快照183/185/186是Kick in the Door不能被墙阻挡的效果。真实不能攻击/到期证明引用既有R23/dungeon-restrictions，不制造不存在的标记。保留originalTarget/originalFinish及旧审查身份。 仅关闭R35原字段/配方展示门槛；原多徽记三条真实结果与折叠/分页/延迟生命周期证据保留，不重跑。匿名源是展示合同适配器，不授予新隐藏源原生效果PASS；当前原生运行包只有一次既有Prepared流程投影回归，0新首次规则场景。英文动态正文/未知自定义配方与缺图缺译沿R47，独立payload A01/A03、全局当前依赖A06及联网/四人/支持决定继续开放。 总待办33（24证据/1审查/8决定），本批关闭R35.01/归档R35，新增0，原552/41首验身份不变。

2026-10-09 R47.02闭合：全控制真实费用排序：放逐手牌两张绿色牌/牺牲两山脉/回手两海岛的三语言准确文案、法术力1图标、原生顺序/确认/重排和原编号；9旧原生快照、默认/最大字号、真实射线/旧重复只读守卫、查看返回固定字号及优先权清除，1071执行检查。长自定义选项滚动独立算适配器，不重跑原生效果。 仅现有费用排序消费者与三条实际费用文本改变，未知/自定义原文完整保留；原生运行包/卡牌/付款路由未变，0原生重跑。其他R47静态语言、暗置/跨区/卸装句型、资源未就绪/切语言与A06总体依赖继续；非全量/联网PASS。总待办34（25证据/1审查/8决定），关闭R47.02，0新增，原552/41首验保持。

2026-10-09 R23.01及原包闭合：六条真实流程：Flight/Jump两个飞行来源，Disenchant只移除一个、清理移除最后一个；Wishmonger由目标操控者选色、辐射准确跨玩家同色集合、类别保护实际脱落武具/排除神器目标、授予者死亡后保留；Opal Titan由对手真实多色生物取得永久蓝红保护；Samite多色、Brave白色子集、Faith同时保护牌手与永久物，Crypt Rats/Pyroclasm实际伤害与死亡对照；两只无色Snitch分别移除Spectra Ward型和旧each-color型的黑色保护，黑色目标/伤害恢复、红色仍防止、到期还原；Hammerheim移除原生/灵气赋予两种地行者，实际阻挡/伤害后恢复；Floating Shield实际选色/牺牲、保留ChosenColor临时保护及到期。 玩家保护接入现有状态栏的单一盾牌图标和三语言详情；准确内部Keyword Effects隐藏图容器在仅保护/空时不重复画，混合/其他关键词容器保留；悬停不再显示缺失卡图，回普通牌清除盾牌。没有改原生规则/支付/输入路由。 只关闭R23原行及已分类Protection/ProtectionAll/Debuff最小结果；ProtectionAll字面Gains在冻结候选中无实际声明生产者，生成器/模式入口未知仍归A01，未注入任意分支。复制多态KeepName Vanguard入口、CDA/等级独立结果原归属、R47英文/长文本、A06总体依赖与实时联网/四人/支持决定继续开放。 总待办35（26证据/1审查/8决定）；本批关闭R23.01、归档R23，新增0，原552/41首验不变。6新增真实流程、2次仅投影回归；324原UI已完成检查保留，仅35项修正状态图/详情，359执行。

2026-10-09 R20.02普通连体复制增量：真实施放Fire//Ice的Ice右半1U，Twincast付UU复制后改选另一只熊。复制品与原咒语都保留Original/LeftSplit/RightSplit三个准确状态，当前均RightSplit/Ice/1U；准确不同堆叠/卡/目标ID。复制品先横置第二熊并抓1、消失，原件后横置第一熊并抓1，原件进坟墓场恢复Original，实际施放次数2/费用4/生命不变。 修复InspectIntegrated连体咒语详情：Stack上的非Room LeftSplit/RightSplit使用准确选面feature正文，避免把未施放另一半的效果也显示；保持原全卡图。 R20.02多状态复制八个分支已完成；只余独立KeepName Vanguard原A01/A05可达入口/实际结果，不注入未支持模式取得假通过。R51实际Fuse仍另有边界；R23多来源关键词、R47长正文/语言及A06总体依赖保持。 总待办36（27证据/1审查/8决定）；核销multi-state-copy-baseline子输入，0整项关闭/0新增。

2026-10-09 R23.01合变原物展示修复增量：修复真实合变原物显示：导出merged独立标记及当前顶牌卡图，当前顶牌印刷关键词/异能作为基线，下层Flash/合变费用/伤害抓牌异能标蓝。原物保留普通永久物外框，原物回手清除合变显示字段；前后复制品特征不变。战场实际缩略图/攻防、原物与旧复制品分开显示、原始物理熊2/2与当前返回点击和双方视图已核对。 R23.01仍余Protection/ProtectionAll/Debuff多来源、移除其中一来源/到期与合法性；R20.02仍余普通非永久连体复制选面和KeepName Vanguard可达入口；R47长正文完整滚动/混合语言与A06总体依赖保持。 总待办36（27证据/1审查/8决定）；0整项关闭/0新增。

2026-10-09 R20.02多状态复制补充增量：四条真实流程：Clone复制Silverflame Squire保留本体/On Alert两个准确状态与费用，原牌回手后独立保留；Clone复制Quill-Blade实际进场预备，自己生成的Twofold Intent145被真实支付/施放，自己变2/1且预备消耗，原牌回手不影响；Mirrormade复制已开左门的Room得全锁定状态，独立付费开左右门，左门实际回手另一张对手熊，来源被Disenchant消灭后自己双门保留；Octopus实际置于熊下、Greathorn后置其上，前后Clone分别得2/2熊与3/4野兽且都有下层抓牌异能，原合变体三组件全部回手后，两复制品实际攻击造成共5伤害并各抓一张。 修复InspectIntegrated的全锁定复制Room：EmptyRoom原生正文为空时保留为空，避免回退印刷左门Oracle而误显示未解锁异能。此前原始物理面/印刷攻防修复保持；新条件仅EmptyRoom路径，先前三条复制流程无该状态，保留152去重标签原证据。 4新增原生流程、2219离线捕获断言/1423驱动检查；206去重UI标签/301次检查。引擎/资源哈希未变；仅EmptyRoom展示修正。原合变体正文/关键词基线/卡图漏传不是PASS，已准确交R23原项；复制品详情单独通过。未验证普通非永久连体选面、实时联网/全部语言/长文本滚动。 R20.02：multi-state-copy-baseline只余普通非永久物连体咒语复制的实际选面/状态，先核对R20.01/R51既有证明再补最小差异；Room永久物分支已完成。copy-name-and-keyword-source只余原A01/A05归属KeepName Vanguard可达入口/实际结果。原合变体展示字段/卡图遗漏留R23.01，长正文滚动/混合语言留R47。此前transform/flip/face-down及这四分支保留成功生产者，不重跑。 总待办仍36（27证据/1审查/8决定）；本批核销4个状态分支，0整项关闭/0新增。

2026-10-09 R20.02多状态复制增量：三条真实流程：Delver复制衍生物保留双面，来源回手后由下次自身维持公开实际翻面；普通Clone复制反面只得当前特征、不能翻面。Student被Jump赋飞行后实际翻转，Clone自身触发再翻转，来源回手/Jump到期仍保留自己的Tobita飞行静态。Mystic实际变身施放、Clone只得正面朝上的无名无色2/2，原牌实际翻正并回手不影响复制品；双方权限只差原牌拥有者可知的背面资料。 修复InspectIntegrated原始状态：双面复制衍生物按实际物理面取卡名/类别/费用/正文；普通Clone仍用物理正面，与当前反面卡图独立。无名正面复制品不再漏出Clone名称。原始攻防取物理印刷值/表达式，Clone0/0、Sakashima3/1、Consuming Aberration */*正确，不借用当前复制攻防。 3新增原生流程、1465离线捕获断言/1296驱动检查；152去重UI标签/189次检查。引擎/资源哈希未变，UI修复后只重跑受影响展示并复用两份旧原生快照。混合语言静态标题仍归R47，未验证实时联网、全部语言/长文本或其余复制状态。 R20.02：multi-state-copy-baseline只余Secondary/历险、PreparedSpell、split/Room与mutate合变上下层固有来源；copy-name-and-keyword-source只余原A01/A05归属的KeepName Vanguard可达入口/实际结果。Meld/Specialize沿原S01/S02/R44归属，不重复建项。已通过的transform/flip/face-down、普通反面Clone、身份/Duration/全文流程不重跑。 总待办仍36（27证据/1审查/8决定）；本批核销3个状态分支，0整项关闭/0新增。

2026-10-09 R20.02/A01.01名称/Duration/全文字段增量：Sakashima字面名称/传奇类别及回手异能再复制，复制品实际延迟回手；Shapesharer暂时继承后下次自身回合还原1/1/蓝/原异能并移除继承回手。Volrath激活GainThisAbility根绑定受赠者，原牌与再复制品各自再复制Air、来源离场，下次自身回合分别还原Volrath和7/5熊。独立Volrath’s Shapeshifter真实施放/继承伤害命中/两次弃牌换坟墓顶，准确同ID名称费用颜色异能攻防及非生物还原。 修复ByText独立表漏传当前正文的guard；无原生规则/UI生产代码改动。3新增原生流程、1次仅Fulltext修复后投影回归，1596驱动检查/991捕获断言；94去重UI标签/125执行检查。旧成功Sakashima/Volrath不为新哈希重跑：捕获ByText表均0，新增guard分支不适用。仅英语动态正文语义，中文/日文动态正文、既有混合语言标题和长移除段落的完整滚动仍归R47；未验证实时联网/全机制。 R20.02只余multi-state-copy-baseline及copy-name-and-keyword-source中的KeepName Vanguard可达入口/实际结果；普通NewName、AddTypes、保留异能根与Clone Duration已核销，不重跑。KeepName接收者原状态名的源码分支已读，不以源码代替Vanguard实际结果，沿原A01/A05支持/入口归属保留。 总待办仍36（1审查/27证据/8决定），本批完成2具体输入及名称子范围，0整项关闭/0新增；明确区分两个Volrath，不再混用其证明。

2026-10-09 R20.02/R23.01复制固有异能增量：Evil Twin复制后再复制，保留UB/T消灭异能实际作用于准确同名对手目标，错误名称不在合法候选；Dollhouse的0/0复制品保留静态X/SVar按组构体2→3→2重算，外部敏捷不复制且到期移除；Inchblade原生重构复制品去掉准确Attached固有触发，保留重构与武具加成，实际再附着不再产衍生物；Flesh Duplicate对已有/新增/再复制Vanishing各只进3个时间指示物、下次自己维持各减为2；Vesuvan准确自身维持WrappedAbility保留，第二次复制及再复制后各自仍能在后来维持复制，原来源离场不影响。 修复复制详情使用原生当前类别、准确新增/移除固有异能和印刷关键词基准；原始卡图/原始状态保留，新字段进入分组刷新。新增5条原生流程，2306驱动检查/1309捕获断言；另1条Dino仅为改变桥接投影后的已移除CDA/当前类别回归，不算新的首次机制。106去重UI检查标签、145范围内已完成执行检查；保留失败driver/旧投影与已完成中文，不因标签/locale/文件哈希重做成功结算。未关闭R20.02：多牌面、名称例外、Clone Duration还原/保留异能到期边界未验。英文/日文关键词状态标签仍中文，记R47.01；非完整语言/联网/全复制PASS。 总项仍36（1审查/27证据/8决定），本批0整项关闭/0新增；五类具体差异已核销，保留Duration/多面/名称剩余，不把它们再整包重测。

2026-10-09 R20.02复制例外增量：R20.02的copy-exception-fields三条真实流程：Ratadrabik死亡复制的非传奇2/2/白黑Angel+Zombie可再复制，外部指示物/变蛙SetPT颜色类别/敏捷不复制，移走Rat后外部警戒消失；Dino DNA实际印记/支付6复制为绿色6/6Dinosaur践踏，移除PT定义CDA而保留施放触发，两次真实施放磨准确两张牌；增幅Jace产生非传奇起始忠诚1复制品，+1变2后再复制仍以1进场，复制后+1作用于接收者，来源离场后独立保留。 三原生流程/1211驱动检查/887捕获断言含双方逐快照字段比对，85去重UI已完成检查（88执行含3重复准备），不是85种机制。未关闭R20.02全项：多牌面/保留与排除异能/名称例外仍待验。复制后类别详情及已移除CDA正文显示仍有已复现缺口，归R23.01，未授这些字段显示PASS。无生产/卡牌/资源/运行包改动；新Editor测试和记录。 剩36（1审查/27补证据/8决定），本批0整项关闭/0新增编号；原552/41首验保持，不是没有进展。UI原83已完成检查及整套驱动FAIL冻结，只补菜单5，不重跑规则成功流程；明确保留R23显示缺口。

2026-10-09 R19.02及原包闭合：13条准确原生流程：Footsteps→Cloudshift→Shock→再次Footsteps的三时间戳/两独立安排；Towershell指定玩家优先ThisTurn且错玩家不触发；Gideon Upcoming清理注册/静态维持/实际强制攻击（空宣攻和错误目的地原生拒绝）与回合末耗尽；创建条件false、Shirei触发前/结算前旧来源消失与true对照；Sauron最终动作执行/因持戒者跳过及之后换持戒者不重触发；未触发ThisTurn清理；Scattering实际反击2MV/赢交锋，记忆整数2，接受产2C/拒绝产0且下一主阶段不再触发。4943驱动检查，1314捕获断言含逐快照双方公开字段等值；59项UI增量。 未重跑已通过宝物NextTurn/灵庙触发复制与旧三语言合同；保留所有失败/成功producer身份。剩36（1审查/27补证据/8决定），本批关闭R19.02/归档R19，新增0；原552/41首验不变。只关闭R19原行与R19.02已列生命周期/时点/条件门槛。独立payload A01、Gut字段S02、配方R35、动态英文R47、全局依赖A06与联网/四人范围保留原归属。Shirei条件false的安排仍在原生队列，但StrictlySelf旧时间戳不能复活，未声称已清除此惯性安排。无生产/卡牌/资源/规则运行包变更。

2026-10-09 R19.03收尾补齐非空记忆对象：宝物的空记忆不替代对象继承证明；新增灵庙真实7/1衍生物131的结束步骤放逐触发，原堆叠2/复制6共用触发50004/来源121、异能ID独立且准确记忆131。复制先移除衍生物，原触发有效空操作、所有其他区域不变，下次结束步骤没有重新安排；357驱动/23离线检查、35项双视角长文本/LKI卡图展示增量。保留原宝物33+412及41UI成功，不重跑。R19.03完整门槛核销，剩37（1审查/28补证据/8决定），首验552/41不变。

2026-10-09 R19.03完成：宝物真实下回合维持延迟触发经共鸣器复制，原堆叠3/复制7共用源121和触发50001、异能ID独立，分别抓准确两张牌；一次性触发已移除，复制不新建安排，下一维持不重复，准确普通抓牌/生命/资源/私有知识断言。33捕获断言+412驱动检查；41项Unity检查覆盖双席位独立展示/移除和原目标射线。原两次驱动失败保留；R20共用、Bauble NextTurn子范围抵扣R19.02，其他注册/条件/非空记忆对象与复制例外仍开放。剩37（1审查/28补证据/8决定），关闭R19.03，新增0，原552/41首验与运行包不变；非全量/联网/语言PASS。

2026-10-09 R51.03完成：Foundry实际原牌免费重施，Opt接受/拒绝后跨回合无残留许可及下次激活重试；Fling原1R免除，抗拒法球1费与第二只熊牺牲仍需支付，准确对手18→16/原牌坟墓场，44捕获断言。确认长英文/自动缩字已修复，三语言79UI检查及中文措辞两卡32增量；首次Opt规则完成后驱动误判，直接复核已存轨迹不重跑。旧失败与原552/41首验保留，剩38（1审查/29补证据/8决定），本批关闭R51.03，新增0。R51.02及A01/A06/R47继续；非全量/联网PASS。

2026-10-09 R12.03及原包闭合：真实祭坛牺牲三张荒疫巨像→公开/洗牌→Ogre/Ashiok放逐三张，牌库最终仍为空；旧按ID序列判断误把不可逆操作退款已复现并修复。付款期间记录实际洗牌/牌库换区事件，finally解除临时监听；普通牺牲/展示取消重试+6生命当前对照通过，925驱动检查及13捕获断言。Game既有字节码指令逐项相同，仅增加解除监听方法；现有UI合同复用。R12原行和四编号完整归档，剩39（1审查/30补证据/8决定），本轮再关闭R12.03，新增0；552/41首验保留，非全量/联网PASS。

2026-10-09 R12.01完成：类别费用取消清除旧类别/实际法术力退款及重试3/3；Celestial两张手牌/战场同类别候选、X3实际3G付款、匹配进战场抓牌/不匹配及拒付进手牌、取消重试与洗牌清理，五种原生结果208捕获断言。修复可取消类别请求误入普通异能列表，38项Unity射线/必选与可取消/精确两张确认门槛通过。保留原成功身份，仅补一次清理观察，不重跑全部。剩40（1审查/31补证据/8决定）；R12.03仍开放，552/41首验身份不变，非全量/联网PASS。

2026-10-09 R09.02闭包：玛瑙宫殿实际支付1产白，再从统帅区施放今田猎犬勇丸；历史次数0/2的夹具分别实际支付W/4W，原生进场1/3个指示物、攻防3/3和5/5，资源及下一优先权正确。修复原生已提供指挥官选项但公开区域卡牌未显示的入口；Unity准确点选、指示物/绿色攻防与原始2/2切换通过。首次原生完成后的驱动列表格式错误保留，直接核对已存快照不重复运行。R09原行及四个事项完整归档，不泛化四人/全限制产费效果。剩41（1审查/32补证据/8决定），本批关闭R09.02，新增0；552/41首验身份不变。

2026-10-09 Assist差异核销：协助2/选择0、协助方部分付款取消、施放者在协助完成后取消、已有Shock堆叠时Out of Bounds协助支付后取消，5具体流程通过。修复取消结果未传回施放、退款事务漏掉单法术力费用的协助，以及施放者单独付不起时合法协助入口被隐藏；原有堆叠身份/顺序与双方资源恢复有断言。Unity补验协助席位数值、展开分组地后精确成员、只读/取消及施放者返回；驱动误把展开当提交的失败保留，仅重试未完成部分。原552/41身份保留；剩42（1审查/33补证据/8决定），核销A01具体子范围，其他原结果/A06依赖/支持决定继续开放。

2026-10-09 Companion付款/UI缺口核销：三费回手与已横置一地后取消退款再重试，两条真实原生流程通过；修复原生有命令区合法选项而Unity无入口的问题。三语言准确射线、同ID移入手牌移除命令标记、过期/重复/只读保护、接收方及观察者范围通过；旧放逐入口定向重放通过。运行包53d90e22，仅桥接两类字节变化，原生覆盖类不变。原552/41首验身份保留；剩42（1审查/33补证据/8决定），本轮核销A01子范围，未关闭整个A01，其他行侣限制/子对局及Assist付款差异继续保留。

2026-10-09 Companion真实开局阻塞修复：原生可选选择返回null后仍移区导致崩溃，新增拒绝时提前返回。接受/拒绝Companion与普通开局3真实流程均恢复第1回合维持优先权，双方生命20/手牌7/牌库53及接受有/拒绝无命令区辅助效果分别断言。保留原失败与驱动漏确认尝试；运行包89e76392，55823文件哈希登记，其他已有覆盖类字节不变。原552/41首验身份不变，A01只核销开局子范围；付费回手、UI及网络未授通过。剩42（1审查/33补证据/8决定），继续准确结果与A06依赖，不再以整理阶段禁止已明确的增量运行。

2026-10-09 A01源码归类与补证据分离：220原身份均有准确消费者或明确保留的缺口，21个工厂外入口补齐定位。A01.01保留原编号/范围/全部结果断言，类型由审查改为补证据，仍PENDING；没有关闭实际结果。剩42（1审查/33补证据/8决定）；下一项为A06当前依赖判定。Assist不同付款人、Companion可选取消及3表达式生产者门槛仍待验，210共享输入与552/41首验原账不变，0规则/UI运行。

2026-10-09 A01原220机制入口增量：148关键词声明/34直接API（有重叠）及802有效原生源码核对完成；40非直接名称分为37别名候选和3个表达式生产者门槛。原0计数不当无来源；Cleave双SP与Fateseal施放者处理对手牌库保留具体差异。准确消费者角色、旧断言与实际结果仍待核销，A01不关闭；剩42（2审查/32补证据/8决定），210共享身份及552/41首验不变，0规则/UI运行。

2026-10-09 R49接收审查完成：10处引用/8原身份明确唯一结果与入口负责人A01、当前依赖A06及关闭条件；变体支持、轮抽记录、赌注所有权/跨局移除等未知完整保留。剩42（2审查/32补证据/8决定），210共享输入身份不变，0新增编号/规则/UI运行，552/41首验原账未改。

2026-10-09 A03共享字段范围审查关闭：18原字段组/8原输入明确写入、消费、知识与生命周期差异；Winter's Night即时隐藏重置标记与Vorinclex入堆叠对比细化原queue输入，不新建组合游戏。8原结果身份完整转A01，210共享输入不变；剩43（3审查/32补证据/8决定），0新goal编号/规则/UI运行，552/41首验原账完整。关闭的是组合范围，实际退款/历史/私有知识和队列结果仍待证明。

2026-10-09 A03维度定位审查关闭：原313身份（144触发/90持续/45费用/34替代）与准确消费者、别名/模式集合和源字段完整保存；268事件/持续维度的原命名卡数量逐行一致。10原待验结果身份转A01，原生旧证明只抵扣原断言；210共享输入不增游戏。剩44（4审查/32补证据/8决定），0新goal编号/规则/UI运行，552/41首验不变。关闭定位审查不代表313效果通过。

2026-10-09 A03费用入口增量：45原费用身份落实到35解析类/33 Human支付选择入口及实际付款实现，保留11091字面费用位置与50条原生旧证明身份。8个无直接字面token继续保留生成入口审查，不凭同类授予效果/UI通过。313维度已有45条费用记录，剩268触发/持续/替代维度待核销；总待办仍45（5审查/32补证据/8决定），0新goal编号/规则/UI运行，552/41首验与210共享输入不变。

2026-10-09 A02入口审查关闭：18专项API/183脚本位置核销，112原API都有准确消费者/旧证据或缺口记录。所有原A02待验结果/权限/恢复完整转A01共享核销；12原操作身份复用，计划牌生效/弃置同属原112范围、归R49支持审查，不新建goal编号。共用表210输入，不等于游戏次数。A02.01关闭后剩45（5审查/32补证据/8决定），0规则/UI运行，552/41首验不变，无全API/当前版本/联网PASS。

2026-10-09 A02选择/指示物/属性/战斗增量：21个API及1692脚本位置完成有界消费者归类，原112矩阵推进94条记录/18待核销。3原操作别名合并，EachNotOn可回退自定义类型、Defined双对象与唯一Optional显式DOOM来源已核对，未把假设分支当新故障。共用表209输入/225引用/16重复合并。剩余46（6审查/32补证据/8决定），本批0关闭/新增goal编号、0规则/UI运行，552/41首验不变。

2026-10-09 A02复合结果增量：新增28个生命/区域/衍生物/复合结果API消费者记录及4973实际脚本位置；冻结112API矩阵明确73条有界记录/39待核销（不是通过率，也不是39场新游戏）。Repeat、反身触发与Darkpact区域交换合并原操作；当前Surveil无Optional、ExchangeZone唯一Darkpact来源，不凭无来源分支造测试。共用表191输入/204引用/13重复合并；剩余46（6审查/32补证据/8决定），0新goal编号/规则/UI运行，552/41首验不变。

2026-10-09 A02流程增量：22个回合/战斗/二选一/重生/私有查看API及1232脚本位置已定位消费者与缺少断言。共用输入表166个准确编号/176引用，10重复引用合并；结束战斗、强制产费沿用原输入，重生两个API共用真实销毁流程。当前ActivateAbility两条字面来源均为地的法术力异能，无来源的非法术力实现分支不另造测试。A02仍开放，剩余46（6审查/32补证据/8决定）；0新增goal编号/规则/UI运行，552/41原首验不变。

共用输入表已同步：135处原引用展开为154处具体引用，保留146个准确输入编号，8处重复引用合并。旧127个编号、原断言、消费者及历史身份完整保留；本次补入16个API差异和3条原声明缺口，均归现有事项。Vanguard/Conspiracy重复引用也合并，未新增游戏要求。输入编号不是游戏次数，剩余仍46（6审查/32补证据/8决定）。

2026-10-09 A02增量审查：16个状态/分支/终局API及403实际脚本位置已定位准确消费者、不同结果断言和原归属；普通非生物永久物经SpellPermanent自动进入共用PermanentEffect，复用continuous-state/multiple-sources原生证明。未把直接中毒等同战斗侵染、修改X等同宣布X、Arc Trail聚合伤害等同Arc Lightning分配伤害。A02仍开放；剩余46（6审查/32补证据/8决定），0新增goal编号/规则/UI运行，552/41首验不变。

2026-10-09 结构/声明审查：A04.01关闭，2016结构身份及100声明/1873原行完成有界源码归类；3条模式/结果缺口与5附带字段完整转已有事项。剩余46（6审查/32补证据/8决定）；0新增goal编号/规则/UI运行，552/41首验不变，无当前全量/联网PASS。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-09 模式/期限归类：新增116模式+27期限，共6355位置/4615来源/184路径，A04累计794/2016；剩1222参数及100声明。5附带字段和74AI上下文完整保留。总待办仍47（7审查/32补证据/8决定），0规则/UI运行，552/41首验不变，无当前全量/联网PASS。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-09 结构维度部分归类：A04固定651/2016准确身份（599描述前缀、40动态/组合键、12AI专用提示），2408位置/2098卡源保留；真实Condition标签反例及100原声明/1873行未删除。剩1365结构条目继续审查，总待办仍47（7审查/32补证据/8决定）；0新运行/编号，552/41首验不变。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-09 接口/间接入口审查：A05.01关闭，145控制器/153GUI声明/615保存调用候选与14必选输入发布分支完成有界分类。六条特殊模式效果原记录转A01.01、明确入口支持归R49.02，未删除未知范围。剩余47项（7审查/32补证据/8决定）；0新运行/编号，552/41首验不变，无当前全量/联网PASS。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 共用补验输入归并：111原引用展开为130具体引用，保留127原输入ID、完整原断言/分支/来源，3重复引用只保留一份执行入口；2旧审查输入按已有关闭结论转交活动事项，不丢失payload或原producer未知。剩余仍48项（8审查/32补证据/8决定）；未关闭未完成审查、未新增事项/对局/UI首验。该表仍待冻结消费者与当前依赖归类，不是127场游戏或最终执行清单。 见[共用输入表](../.tools/shared-consumer-review-1008/intake.zh-CN.md)。

2026-10-08 历史依赖记录审查：A06.02关闭，68批增量/31原关闭包共99记录、314直接证据哈希全符；43重复引用、失败/撤回/替代与旧producer未知完整保留。A06.01当前实际依赖仍待核销。当前48项（8审查/32补证据/8决定），本轮累计完成S02.01/R02.01/A06.02三项审查，0新运行，552/41原首验不变，无当前全量/联网PASS。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 专精/Heist与请求恢复审查：S02.01、R02.01关闭，193功能声明/27来源含95专精颜色面及五类等待合同固定；两批真实本机网络和Gonti/旧UI证据按原范围复用。取消/牌面源码风险、私有候选与部分付款/时序差异留现有补证据项。当前49项（9审查/32补证据/8决定），0新编号与运行，552/41首验证据不变，无专精/Heist/当前全量/联网PASS。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 翻牌与会话生命周期审查：S07.01、S08.01关闭，9声明/8来源/57消费者固定。翻牌/领奖实际差异、子游戏缺接线、卡恩同局重置与已有外层BO3换备分别归类；具体支持/反馈决定仍开放。当前51项（11审查/32补证据/8决定），0新编号与运行，552/41原账不变，无效果/模式/当前全量PASS。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 辅助区域消费者审查：S04.01、S05.01关闭，733声明/295来源/48消费者含83变体灯号归类。选择控件可复用；缺生产辅助组/模式注册、精确状态字段与吸引力结果格式，留现有证据/决定事项。当前53项（13审查/32补证据/8决定），0新编号与运行，552/41原账不变，无模式/当前全量PASS。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 关键词/融合消费者审查：R49.01、S01.01关闭，157声明/7融合牌对归类，Roku旧证明仅抵扣战斗法术力生命周期。确实缺的效果/界面差异留既有事项，融合只读原件预览方案待决定；两处源码风险未实际复现。当前55项（15审查/32补证据/8决定），新增编号与运行0，552/41首验不变，无当前全量PASS。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 章节/数字消费者审查：R18.01、R38.01关闭，247章节/363数字声明逐行归类；Fable3/APNAP4和原数字6+永久2正确抵扣。ReadAhead/最终章节/反身目标与多生成/跨回合/接收者差异留既有事项，Giant Secrets超法术书空值为待复现源码风险。当前57项（17审查/32补证据/8决定），新增编号和运行0，552/41首验不变，无当前全量PASS。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 效果/控制等待审查：R35.02与R36.02关闭；1931 Effect/12控制声明及五标记/六等待消费者定位。原多徽记/嵌套/分页证明抵扣，仅缺字段展示和队列/modal生命周期留R35.01/R36.01；当前1v1终局不冒充存活多人归还。当前59项（19审查/32补证据/8决定），新增编号与运行0，552/41首验不变，无当前全量PASS。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 替代/展示共享消费者审查：R44.03与R40.02关闭，六组字段顺序及八展示分支/62源码定位；22native/6UI/10增量/3基线UI保留原证明。Spark/Interlude实际候选/额外指示物/多对象显示共用R20/R44/R40流程；生命/数值末态不冒充动画时序。当前61项（21审查/32补证据/8决定），新增编号与运行0，552/41首验不变，无当前全量PASS。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 复制消费者审查：R20.01关闭，854声明/800来源及297关键词来源行定位复制字段与牌面。14原生旧结果与两批UI证明抵扣，子裔/增援/Steel真实背面复制不重跑；剩确切例外/异能继承/状态差异归R20.02，特殊API与替代共用A02/R44，展示/永久记录共用R23/R39。当前63项（23审查/32补证据/8决定），新增编号与运行0，552/41首验不变，无当前全量PASS。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 基线/永久修改审查：R23.02与R39.02关闭；284CDA声明、26升级/57条件、264永久声明定位基线和八记录族，547来源保留。原两条Hypnotic真实回手重施直接抵扣，14旧copy/continuous/prototype结果保留；具体CDA/等级/多牌面与永久自移除/复制/替代差异归既有A01/A03/R20/R39/R44共用流程。当前64项（24审查/32补证据/8决定），新增编号与运行0，552/41首验不变，无当前全量PASS。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 动态特征审查：R23.03关闭，五字段矩阵/37有界声明/32来源对齐原修改器与九条真实continuous-state/Caverns结果；三旧快照核对实际变蛙颜色恢复。独立ByText导出条件差异留A01真实Volrath流程，可共验四字段；其余名称/改字/永久费用/公开hidden归既有A03/A02/R39/A01。当前66项（26审查/32补证据/8决定），新增编号及运行0，552/41首验不变。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 类型/牌文回退审查：R47.04关闭，准确28中文/31日文缺译键仍保留规范名及原选择编号；牌文缺键/空白回退与非空英文保留区分，当前341类型声明与历史356输入不混同。资源失败可能沿用前语言牌文归R47.05待复现。当前67项（27审查/32补证据/8决定），新增编号及运行0，552/41首验不变。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 延迟调度审查：R19.01关闭，679声明/639来源/161签名核销六到期+Earth两条+Ring3+Rebound原范围；源码/JAR确认Gut特化自身Your-end-step字段错配，待S02真实复现。注册时点/条件与旧对象身份保留R19.02，复制R19.03，其余差异A01/R35。当前68项（28审查/32补证据/8决定），新增编号及运行0，552/41首验与34开放包不变。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 命名/类别/颜色审查：R04.01关闭，416声明/406来源/70候选谓词组对齐13命名及Tablet/Cavern原证明；七组具体差异归既有R04/A02/A01，旧运行/UI身份保留。当前69项（29审查/32补证据/8决定），新增编号及运行0，552/41首验与34开放包不变。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 旧驱动差异审查：A01.02关闭，四驱动21原引用与15登记重叠结果核对；原缺失运行身份保持未知，Endurance/Skateboard/No Admittance具体差异归既有R51/R18/A01，来源依赖A06。当前70项（30审查/32补证据/8决定），新增/运行0，552/41首验及34开放包不变。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 外部移牌接口审查：A05.02关闭，确认当前控制器/启动/选项协议不进入休眠MoveCards/toAnywhere；原生网络反射入口单独定位，保留未验证适配器及变化重开条件。复用R16七条原生/一条UI原证据，DigMultiple排序缺口仍归A02.01。当前71项（31审查/32补证据/8决定），新增与运行均0，552/41首验及34开放包不变。见[工作表](MechanicValidationWorklist.zh-CN.md)。

2026-10-08 数值消费者整理：A02.02源码审查完成，146行/68张/7Type/41表达式组对齐10份原结果，保留原UI/adapter身份；11组精确差异/前置归既有A01/A05，解析器A04，历史字段共用A03。当前72项（32审查/32补证据/8决定），新增ID为0，552原生/41UI及31关闭/18部分/34开放不变，0规则/0UI运行，goal active。不是146或41场测试，尚未确定全局最小运行集。见[完整待办表](MechanicValidationWorklist.zh-CN.md)及[逐行分类](../.tools/variable-consumer-review-1008/classification.json)。

2026-10-08 付款快照整理：R12.04源码审查完成，53费用索引/40类及18组字段/队列对齐50份原结果；六组差异/核销前置完整转既有A03.02，R12.01/03保留。当前73项（33审查/32补证据/8决定），新增ID为0，552原生/41UI及31关闭/18部分/34开放不变，0规则/0UI运行，goal active。总表改为当前范围优先、历史记录置后；整理阶段尚未确定新游戏数量，不把事项当轮数。见[完整待办表](MechanicValidationWorklist.zh-CN.md)及[分类](../.tools/payment-snapshot-review-1008/classification.json)。

2026-10-08 受限产费与费用谓词整理：完成R09.01/04，245声明/14消费者/100完整谓词对齐45份原结果；14花费触发的独立payload及五组精确差异仍归既有A01/A03，Opal结果留R09.02，结构归A04，依赖归A06。明确保留Piracy运行时追加限制入口。当前74项（34审查/32补证据/8决定），新增ID为0；552原生/41UI及31关闭/18部分/34开放不变，0规则/0UI运行，goal active。没有全效果/联网通过。见[工作表](MechanicValidationWorklist.zh-CN.md)及[分类](../.tools/mana-restriction-review-1008/classification.json)。

2026-10-08 法术力生命周期源码整理：完成R09.03，125声明定位当前消费者，复用九份原结果；九组准确结果差异继续归既有A02/A03，独立payload归A01，R09.01/02/04仍开放。当前76项（36审查/32补证据/8决定），新增ID为0；552原生/41UI及31关闭/18部分/34开放不变，0规则/0UI运行，goal active。没有全效果/联网通过。见[工作表](MechanicValidationWorklist.zh-CN.md)和[本批分类](../.tools/mana-scope-review-1008/classification.json)。

2026-10-08 多方选择源码整理：完成R03.01，52声明逐个对应三类消费者和准确决策/等待；复用四条既有多方流程及两条历史Hunted结算证明。11组具体结果断言留R03.02，完整payload与A01共用审查。当前77项（37审查/32补证据/8决定），新增ID为0；552原生/41UI及31关闭/18部分/34开放不变，0规则/0UI运行，goal active。没有全效果/联网通过。见[工作表](MechanicValidationWorklist.zh-CN.md)和[本批分类](../.tools/multiplayer-choice-review-1008/classification.json)。

2026-10-08 施放身份去重审查：完成R51.01，固定51身份/9其他入口，对齐167条历史结果，22身份有命名原生结果，旧14个堆叠样本是另一口径。未证明的语义保留既有A01/R51/R09/R12/A06归属，四RevealChosen结果转到A01；没有授予全机制通过。当前78项（38审查/32补证据/8决定），新增ID为0。累计552原生/41UI、31关闭/18部分/34开放不变，0规则/0UI运行，goal active。见[工作表](MechanicValidationWorklist.zh-CN.md)与[身份审查](../.tools/cast-entry-review-1008/classification.json)。

2026-10-08 Behold入口去重审查：完成R51.04，20条声明归6种入口，已测部分引用原证据，7组具体断言归现有R51.02/R12.01，独立payload仍由A01审查。当前79项待办（39审查/32补证据/8决定），新增事项ID为0；没有把入口审查算效果通过。累计552原生/41UI、31关闭/18部分/34开放不变，本批0规则/0UI运行，goal active。见[明确工作表](MechanicValidationWorklist.zh-CN.md)与[入口审查](../.tools/behold-entry-review-1008/classification.json)。

2026-10-08 源码去重审查：完成R12.02的四张RevealChosen来源及可取消节点分类，当前80项待办（32待补证据/40待审查/8待决定），新增事项ID为0。53个费用索引对应40个解析类，区域/资源/秘密信息差异保留；外部加费后的恢复风险及四种实际结果明确交R12.04/R51.01，未计效果通过。累计552原生/41UI、31关闭/18部分/34开放不变，本批0规则/0UI运行，goal按继续授权active。见[明确工作表](MechanicValidationWorklist.zh-CN.md)与[分类证据](../.tools/scope-classification-1008/classification.json)。

2026-10-08 全部剩余范围整理：34包拆成81条有编号事项（32待补证据/41待源码审查/8待展示或模式决定），每项写明关闭门槛。历史完成与当前待办分离，旧34行/family gaps冻结保留；A01–A05先共享冻结来源审查，再确定最小验证集，不继续逐个猜测验证。见[明确工作表](MechanicValidationWorklist.zh-CN.md)。本次0规则/0UI运行，累计552原生/41UI及31关闭/18部分不变；goal保持paused。事项数不是剩余测试数或轮数。

2026-10-08 普通Behold费用补验：手牌/战场两条实际付款B→取消→返还浮费→重试及-3/-3结算611项通过。合法手动产费保留横置地与池中B，初版过严断言已纠正，未修改生产代码。实际混合区域选牌合同与上轮一致，复用三语言/射线证明。R12待办改为四组明确边界，保留原行与全部已完成历史；含关键词生成的20条普通Behold声明仅作分类。累计552原生/41UI，112引擎任务/770规则起手/12适配器；31关闭/18部分/34开放/7批次。见[本批证据](../.tools/plain-behold-boundary-1008/result.json)。goal继续，其他边界仍开放，没有全量或联网PASS。

2026-10-08 放逐式Behold费用：修复取消后丢失所付牌及手牌退款排列变化；两条手牌/战场实际费用、部分法术力取消/重试/关联返还849项首验通过。混合区域同名候选增加区域标签，原生用途区分展示/放逐；481项当前回归、普通Behold请求取消43项、三语言真实窗口67项终结增量及保留中文已完成步骤通过。累计550原生/41UI、31关闭/18部分/34开放、7批次；运行包6ee7b2fa，规则首验058888db身份保留。见[本批证据](../.tools/behold-exile-1008/result.json)。按用户继续授权推进，其他派生/牌库/事件/不可逆边界仍开放，没有全量/联网PASS。

2026-10-08 展示手牌费用退款补验：修复先牺牲、展示后取消法术力时来源未退回；普通非牌库CostReveal纳入既有局部退款，派生类仍单列。两取消节点/重试及嵌套牌库放逐不可逆边界三条1121项规则通过，另2项原视图法术力断言；来源已离场的选牌/取消步骤保留原证据，对手部分公开手牌列表确认13项增量通过。累计548原生/41UI、31关闭/18部分/34开放、7批次；运行包6d385d55仅CostPayment改变。见[本批证据](../.tools/reveal-cost-rollback-1008/result.json)。goal保持active，其他派生/牌库/历史/不可逆边界仍开放，没有全量/联网PASS。

2026-10-08 多堆叠/目标/复制品恢复补验：真实Opt/电震目标及Opt/Twincast复制品两流程509项，取消后原堆叠/目标/复制标志保留、依序结算及准确施放计数通过；双方完整堆叠投影4项等值。修复复制品费用选项后缀不匹配导致英文长文本，三语言114项展示增量通过，普通三选项原射线步骤复用。累计545原生/41UI、31关闭/18部分/34开放、7批次；运行包b01c8f63未改。见[本批证据](../.tools/stack-target-restore-1008/result.json)。goal保持active，其他可变/不可逆状态仍开放，没有全量/联网PASS。

2026-10-08 放逐堆叠费用边界：修复Synthesis Pod先放逐Opt再取消法术力时丢失原咒语，以及原生快照重新入栈造成施放计数重复；仅该费用保留原堆叠对象恢复。两条真实规则442项、历史增益/督战812项回归及三语言列表/取消102项UI增量通过。累计543原生/41UI、31关闭/18部分/34开放、7批次；运行包b01c8f63，MagicStack原方法字节码一致。见[本批记录](../.tools/stack-exile-cost-1008/audit.md)。goal保持active，其他堆叠/不可逆边界仍开放，没有全量/联网PASS。

2026-10-08 费用取消前已有状态补验：真实巨大化→督战付款取消/重试812项通过；既有5/4增益、本回合/本局施放计数1保留及自然到期另24项捕获断言通过。无生产变更，普通目标/确认UI合同复用；早期错误身材预期与较窄观察版本保留，不重复计首验。累计541原生/41UI、31关闭/18部分/34开放、7批次。见[有界证明](../.tools/payment-history-1008/result.json)。goal保持active，其他历史/持续/不可逆边界仍开放，没有全量/联网PASS。

2026-10-08 督战费用取消：修复已督战再取消法术力后残留次数和下次不重置标记；762项原生首验、20项捕获视图断言及搜证229项回归通过。保留对手既有标记，重试只产一猫/触发一次，次回合跳过、再下一回合恢复重置。UI按准确合同复用。累计540原生/41UI首验、31关闭/18部分/34开放、7批次，R12继续部分。运行包f69a418d，Card编译差异已单独复核。见[本批记录](../.tools/exert-cost-1008/audit.md)。goal保持active，没有全量/联网PASS。

2026-10-08 搜证与觅食费用：三条原生首验706项、已捕获特征9项、处理体232项回归及241项展示增量通过。修复两类费用未退款、部分坟墓场顺序改变及觅食提示/日文数量与按钮裁切。累计539原生/41UI首验、31关闭/18部分/34开放、7批次，R12/R47部分。运行包58a28bf2，两条觅食成功保留原运行身份并作影响审查。见[本批记录](../.tools/grave-cost-boundary-1008/audit.md)。goal保持active，没有全量/联网PASS。

2026-10-08 处理体窗口补验：四个真实请求、取消重试编号0→1、确认/重选与权限/生命周期、三语言正文及共用标题首帧共174项展示检查通过。规则232项及Sunforger240项沿用原证据；修复正文翻译和标题首帧消失。累计仍536原生/41UI首验、31关闭/18部分/34开放、7批次，R12/R47继续部分。见[窗口补验](../.tools/processor-cost-1008/ui-followup/audit.md)。旧待补记录保留历史身份；goal保持active，没有全量/联网PASS。

2026-10-08 处理体费用：修复Cryptic Cruiser先处理对手放逐牌再取消2U后未退款；232项原生首验及Sunforger240项回归通过。新选牌窗口点击因Unity执行权限待补，正文中日翻译归R47。累计536原生/41UI首验、31关闭/18部分/34开放、7批次；R12部分，运行包4fe63714。见[本批记录](../.tools/processor-cost-1008/audit.md)。goal保持active，没有全量/联网PASS。

2026-10-08 卸下装备费用：修复Sunforger先卸下再取消RW付款后未恢复装备的问题；新增一条240项原生首验，重试免费施放原牌库闪电击造成3伤害及指示物退款回归通过。沿用确认UI合同，卸下正文中日翻译留R47。累计535原生/41UI首验、31关闭/18部分/34开放、7批次；R12部分，运行包c8f1a39a。见[本批记录](../.tools/unattach-cost-1008/audit.md)。goal继续，没有当前全量/联网PASS。

2026-10-08 指示物费用边界：五条新原生首验和两条相关回归通过；修复翻倍后取消残留指示物、任意类型指示物输入fatal及首次提示高度。377项UI检查验证类型选择、同牌多枚/多牌耗尽和缓存刷新。累计534原生/41UI首验、31关闭/18部分/34开放、7批次；R12仍部分，运行包a2b53e36。见[本批记录](../.tools/counter-cost-boundary-1008/audit.md)。goal保持active，没有当前全量/真实联网PASS。

2026-10-08 磨牌费用边界：四条新原生首验验证激活/结算入口的等量及不足；修复CostMill恰等于牌库数量时错误拒付，一条长牌库取消回归通过，确认UI按准确合同复用。累计529原生/40UI首验、31关闭/18部分/34开放、7批次；R12仍部分，运行包97931a53，没有当前全量/真实联网PASS。见[本批记录](../.tools/mill-cost-boundary-1008/audit.md)。goal保持active。

2026-10-08 R12费用原语增量：指示物两种付款顺序取消及牌库顶放逐/磨牌取消四条原生首验通过；修复指示物未退款、放逐确认数量缺失及两条牌库文案，两个原退款消费者定向回归和确认UI增量通过。累计525原生/40UI首验、31关闭/18部分/34开放、7批次；R12保留具体未验边界，运行包ba34d156，没有当前全量/真实联网PASS。见[费用原语审计](CostPrimitiveBoundaryAudit-2026-10-08.zh-CN.md)。goal保持active。

2026-10-08 R31原有界行已关闭：两条真实四人Forge流程验证魔戒四级三名对手各失去3生命、防守者另受2伤害，攻击者不失血；准确资源／抓弃／区域／阶段及67项冻结合同审查通过。累计521原生／40UI首验、31关闭／18部分／34开放、7批次；生产代码和运行包9a6f9701未改，四人客户端／轮序R37、指挥官未来支持（未验证）、联网R02保持开放。见[多人魔戒审计](MultiplayerRingAudit-2026-10-08.zh-CN.md)。goal保持active，没有当前全量／四人客户端／真实联网PASS。

2026-10-08 R31部分完成：十条原生首验及196项真实输入／射线UI通过；修复攻击／阻挡税付款预览复制EmptySa导致的fatal，部分付款取消准确退款；第二次战斗和玩家／旅法师／战役点击已验。累计519原生／40UI首验、30关闭、19部分、35开放、8批次；R31只剩多人魔戒四级各席失血边界，其他共用控制／轮序保留负责包。见[攻击与额外战斗审计](CombatPhaseBoundaryAudit-2026-10-08.zh-CN.md)。运行包9a6f9701，系统goal保持active，没有当前全量／真实联网PASS。

2026-10-08 R30原有界行已关闭：五条实际战斗预防/翻倍/共享致命/跳过/任意分配首验通过；修复稍后分配挡数字、清空后context编号复用保留旧行/卡图。最终三语言1946项准确候选/卡图/射线检查与普通分配标题增量通过，初次弱证明撤回后保留并重验。累计509原生/39UI首验、30关闭、18部分、35开放、8批次；运行包76dcae76未改。见[战斗分配边界审计](CombatDamageBoundaryAudit-2026-10-08.zh-CN.md)。系统goal已确认active；没有当前全量或真实联网PASS。

2026-10-08 R29联合宣攻原有界行已关闭：修复缺失的原生选组/加入入口，双方显示联合组；新增5条原生首验、1套UI首验，普通重复指定及旧防守分配仅定向回归。输入/结构55项及正式语言/分组9项复核通过，初版裁字、组标和语言别名失败保留。累计504原生/38UI首验、29关闭、18部分、36开放、8批次；运行包76dcae76。见[联合宣攻审计](AttackBandConsumerAudit-2026-10-08.zh-CN.md)。用户本次继续授权有效；工具旧paused状态不能由状态API主动恢复，不再自行暂停；没有全量/真实联网PASS。

2026-10-07 R29部分推进：新增9条特殊战斗关键词原生首验、19项既有UI结构/点击检查和3项修复后字形复核；修复永久物指示物数字裁切，复现联合宣攻入口缺失并保留待修。累计499原生/37UI首验、28关闭、19部分、37开放、8批次；运行包ec324ea3未变。见[特殊战斗关键词审计](CombatKeywordAudit-2026-10-07.zh-CN.md)，没有完整R29或全量/联网PASS。

2026-10-07 R17揭示与短时信息原行已关闭：5条真实规则流程、24项UI增量、两张原版本悬停检查及10项只读资源/隐私断言通过。修复公开牌入手的已知身份继承、私人排序顶牌记忆及占卜保留唯一顶牌的内部移区差异。累计490原生/37UI首验、28关闭、18部分、37开放、8批次；运行包ec324ea3。见[揭示与知识审计](KnowledgeLifecycleAudit-2026-10-07.zh-CN.md)，不授予全量或真实联网PASS。

2026-10-07 R28跨区域许可期限与控制变化原行已关闭：4条真实规则流程、23项UI增量及14项原轨迹资源/隐私补充断言通过。修复有权限的贡堤玩家只能看到“面朝下的牌”文字入口的问题，现沿用私有已知牌图正常施放；其他接收方保持隐藏。累计485原生/37UI首验、27关闭、19部分、38开放、8批次；运行包d581bde4。见[跨区域许可审计](PermissionLifecycleAudit-2026-10-07.zh-CN.md)。

2026-10-07 R15区域浏览与空候选筛选原行已关闭：双方区域入口、准确身份、只读/选择权限、全部不可选和实际展示确认通过；提示按用户改为“成为XX的目标（堆叠X）”。新增1条原生首验、20项UI增量，累计481原生/37UI首验、26关闭、19部分、39开放、8批次；运行包未改。见[区域浏览审计](ZoneBrowserBoundaryAudit-2026-10-07.zh-CN.md)。知识R17、未知语言R47、真实联网R02等仍开放。

2026-10-07 区域目标提示增量：坟墓场和放逐区显示“成为XX的对象（堆叠X）”，来源/编号随堆叠刷新，多来源与长内容可读。10项首版局部检查及4项最终差异检查通过，复用原生结果；首验480/37及开放40不变。见[区域提示记录](ZoneTargetHintAudit-2026-10-07.zh-CN.md)。

更新：2026-10-07 堆叠目标可见性补齐：双方悬停显示已确认目标卡、所属玩家和区域；多目标/同名身份、离场标记、横向浏览只读与三语言增量通过。6条实际原生回归、4项隐私投影适配器、20项UI检查，不增加首验或关闭包。运行包更新为6aea3341，既有证明保留旧运行身份；累计仍480原生/37UI首验、25关闭、20部分、40开放。见[堆叠目标审计](StackTargetVisibilityAudit-2026-10-07.zh-CN.md)，没有当前全量/真实联网PASS。

前一批记录：2026-10-07。R27运土/截气原行已关闭：9条真实规则流程、21个资源末态、四次实际两点替代支付及23项UI增量通过；运土返回/特征清理、截气拥有者/来源离场/取消/咒语/衍生物/零目标已验。修复跨区域目标把信息标题当按钮的问题，确认目标三语言与相邻普通目标回归通过。累计480原生/37UI首验，25关闭、20部分、40开放、8批次；92引擎任务（691规则/起手、12适配器）、83UI任务/25族。保留各成功生产版本与第九条报告失败/独立恢复身份，运行包未改。见[运土与截气审计](BendingLifecycleAudit-2026-10-07.zh-CN.md)。goal按继续授权推进，未完成，没有当前全量/联网PASS。

## 目标与范围

审查当前 MTGB 卡池和可达 Forge 对局流程，找出缺少桥接、缺少可操作 UI、提交错误身份及无法继续结算的分支。按原生请求合同复用客户端控件；项目接入差异、受本项目补丁影响的结果及已知失败保留实际结算断言。未修改Forge的普通组合不因整牌缺少实测而自动追加验证。

范围由当前覆盖清单、可达消费者及审查队列确定。参数、标签、同义声明按执行分支归类；为独立语义和已知相互影响的组合补样本。分类表必须能追溯每个条目的归属或范围决定，未知语义继续开放。有限验证的结论限于列出的分支、条件和版本。

## 减少工作量的执行方式

**34 条开放边界现合并为 7 个执行批次，6 类共用检查指定负责批次。** 原R48、R45、R42、R46、R43、R16、R01、R11、R10、R21、R22、R41、R24、R26及R27关闭后保留完整证据。逐条归属、复用条件和补验条件见 [自动剩余报告](MechanicValidationRemaining.zh-CN.md)。批次数表示排期组织，不能换算成剩余轮数或节省测试数量。

| 工作 | 执行方式 |
|---|---|
| 已完成首次验证 | 引用原 case、断言和证据哈希，核销包内边界；相关代码变化才增加回归 |
| 权限、请求版本、确认门槛等共用合同 | 同一实际消费者/处理分支引用已有证明；新消费者、字段或合同变化补验 |
| 新规则使用已测控件 | 保留独立原生结果断言；对比实际请求合同，相同合同引用已有点击/语言证据，变化分支补代表输入 |
| 语言、字号、图片和资源过渡 | C05归E04负责，随受影响消费者执行；相同格式路径引用已有证明，补新文案/资源/布局 |
| 大量未登记结构条目 | 按消费者/原生分支归类一次，保留条目到分类/证明的引用；不按数量逐个生成游戏测试 |
| 组合边界 | 先比较与已有样本的规则及请求差异；费用、控制、隐藏信息或生命周期存在相互影响时单独断言 |
| 全量回归 | 集中在稳定的阶段收尾；核心路由改动、运行包升级或影响无法界定时按方法扩大 |

不同资源的取消恢复、多方控制、私有身份、到期/换区和真实网络仍需各自证明。共用面板通过不替代这些原生语义。A06先完成可审计的逐批依赖/影响记录；自动失效计算与证据合并是可选优化，总指纹失效继续如实显示。

### 本次进一步减量

- **先筛出真正缺证据的差异**：每批先把候选分成“已有证据覆盖、具体待测、待分类、需支持范围决定”；后两类先审查，不直接变成新增测试。选定实际分支时说明旧证明缺哪项断言，以最小真实流程补验。
- **运行扩展有依据**：新增消费者/分支、共享可变状态、权限/可见性差异、生命周期依赖、规则交互或实际失败触发补验。“其他组合”保留待分类身份，不成为无限追加测试的完成门槛。同一传输/权限/恢复路径共享联网证据，特殊控制和付款等差异另补。
- **每批一次登记**：沿用现有驱动、输入和结果，批量完成相关断言，只重试失败/受改动影响部分。用本批已有记录说明复用及剩余差异，复杂生产修改或闭包需要时才新增专项审计。按 [skill 的运行前减量顺序](../.agents/skills/mtgb-mechanic-validation/SKILL.md) 执行。
- **UI按合同取代表输入**：相同控件、提交分支、操作/可见席位、身份类型、数量/顺序、确认/取消门槛和窗口生命周期，可引用已测真实输入及点击证明。每个规则case核对自身输入差异，不重复整套点击/截图。仅请求类名相同不够。
- **三语言按文案与格式路径采样**：数值/对象实例变化沿用已有语言证明；新增文本、符号、资源或布局才补对应语言与长文本边界。
- **自动工具移出必做范围**：验证需要依赖和证据可追溯；不要求先实现自动PASS合并平台。A06保留记录工作，不关闭未知依赖。
- **核销完整有界成果**：此前Discover九场原生证明沿用，本轮UI合同验证和Empower关联已完成，原R48关闭；其余费用/控制组合保留R51/R36。
- **审查共用一次结果**：A01–A05复用条目到消费者、请求合同及证据的归属，R49只接收新发现且无归属的分支，避免再次遍历同一清单。
- **共用检查跟随改动**：语言、资源过渡和A06影响记录随对应批次完成；普通选项/数字候选、触发/对象生命周期、卡牌/玩家状态合并排期。
- **组合按相关性选样本**：独立语义保留断言，多个断言可用同一次真实流程取证；未知依赖先查清，已知交互及失败才扩大矩阵，避免默认穷举所有机制组合。
- **特殊模式先审查再实现**：八个特殊包仍开放，先确认可达性和既有控件差异；现有控件可承接时复用，确需新交互的再列具体方案/范围决定。

例如Discover保留九条不同原生结果断言；UI按施放/入手、揭示确认、再次请求与清理等差异合同组织代表检查，语言只随文案/格式变化补验，不机械扩成九场×三语言。已按此方式操作验证，并修复提示本地化及高度裁切；未知长牌名/窄屏仍留R47。最新整理见 [工作量复核补充](GoalWorkloadReviewFollowup-2026-10-06.zh-CN.md)；[原复核](GoalWorkloadReview-2026-10-06.zh-CN.md)和[Discover消费者审计](DiscoverConsumerAudit-2026-10-06.zh-CN.md)保留各自历史身份。

### 剩余边界的具体去重（2026-10-06）

这是2026-10-06的历史去重记录：当时有54个开放追踪ID，19条边界已指定共用检查的负责包。原始文字和转交关系保存在 `plan-provenance.json.boundaryOwnershipReview`；随后R45、R42关闭至52，本轮R46再关闭至51。各包特有原生结果继续验证，共用检查不再各跑一遍。

| 重叠范围 | 统一负责与剩余差异 |
|---|---|
| R10/R11/R12 的非生命部分付款撤销 | R12统一检查；R10保留产费撤销/法术力恢复，R11保留资源选择与实际支付结果 |
| R11/R51 的罕见费用与宣布选择 | R51统一审查缺样本分支；已有费用矩阵沿用，控制归还共用检查引用R36 |
| R19/R35 的延迟阶段、来源与控制变化 | R19验证规则生命周期；R35只补标记身份、配方及悬停展示差异 |
| R21/R22/R41 的叠放和展开 | R21统一分组/浏览；R22保留非生物身份/资源差异，R41保留实际附着关系 |
| R15/R17/R46 的区域和揭示 | R15负责浏览/筛选合同，R17负责知识与到期，R46保留搜索合法性/替代结果；共用入口相互引用 |
| S03及各机制重复的控制/网络/语言 | 共用部分分别引用R36/R02/R47；各机制只补实际请求字段、权限或状态变化的独特差异 |
| A01–A05 的重复清单审查 | 一张消费者分类表提供不同维度；R49仅接现有包尚未包含的具体缺口，A06随批登记 |

“其他组合/消费者”保留为待分类范围，先找到具体入口/缺失断言才安排运行。交叉引用只负责某项共用检查，不要求等对方整个包关闭，也不把其所有规则当作已验证。本次没有新增效果PASS或关闭包；工作量减少体现为共用检查只执行一次，不将保留的追踪ID数当作实际测试数。

## 完成门槛

1. **审查有归属**：已登记机制、无场景 API、事件/替代/费用、声明与可达接口有消费者/语义分类；每个未知执行分支有待办或明确范围决定。
2. **原生规则可继续**：范围内不同项目接入分支及本项目修复有真实施放/激活、支付/选择和结束状态证据；既有原生实现和普通组合按对应合同复用，未穷举的整牌语义保留验证限制；适用的不足资源、部分取消、非法输入、空候选、失效目标及到期能恢复。
3. **界面合同可操作**：真实请求显示原生合法身份、数量、权限；真实点击能提交并继续。共用权限、私有信息、版本和展示证据按相同消费者/合同引用；差异部分新增证明。
4. **边界逐项核销**：每条开放边界关联规则/UI证明或明确的支持范围决定。特殊表现先对比现有控件，确需用户决定时提供具体差异和可审阅方案。范围决定与效果 PASS 分开记录。
5. **收尾有稳定基线**：完整登记回归、运行包和输入身份归档；失败与历史批次保留原状态。每批影响/复用判断可追溯，未知依赖保守回归；自动依赖计算/证据合并不是完成门槛，也不能用手工记录改写自动失效状态。

## 当前进度

| 统计 | 当前值 | 口径 |
|---|---:|---|
| 执行批次 | 7 | 34条开放边界恰有一个排期归属；不是7轮 |
| 开放边界包 | 34 | 20共用流程、8特殊表现、6审查/工具 |
| 已关闭有界包 | 31 | 原始行及证据SHA-256保留于首验账，含最新R29/R30/R31 |
| 开放包内部分核销 | 18 | 已完成范围按准确边界核销，特有剩余待办保留 |
| 其余尚无包内核销记录 | 16 | 34开放包减18部分包；不意味着没有历史基线证明 |
| 当前已知边界包总数 | 65 | 31完整关闭+18部分完成+16其余开放；不换算工时百分比 |
| 暂停基线后新增原生首验 | 552 | 已归档命名场景及具体断言；UI/网络增量不重复计数 |
| 暂停基线后新增 UI 首验 | 41 | 已归档首验，弱证明和后续差异复核分别保留 |
| 登记引擎任务 | 112 | 770规则/起手场景、12适配器 |
| 登记 Unity 套 / 共用流程族 | 97 / 25 | 归族不等于当前全量PASS |
| 未登记结构条目 | 2666 | 2016结构、650机制/执行维度；不是剩余游戏次数 |
| 尚未分类输入 | 0 API、100声明、0脚本 | 分类不计效果PASS |

当前自动状态、变化及精确边界读取 [剩余报告](MechanicValidationRemaining.zh-CN.md)。原生/UI首验数量反映命名场景与列出断言，不表示独立机制总数。审查队列仍可能发现新分支，当前无法给出可靠的剩余测试数或固定轮数。

## 接续顺序

1. **E01**：按局部消费者范围复核旧证据与未知项，形成真正缺样本的分支；A06随批次记录依赖/影响，不先开发自动平台。归类与实际结算分别验收。
2. **E02/E03/E04 的 P0**：请求/权限恢复、未覆盖费用与合法目标。引用已有费用矩阵，只补剩余边界。
3. **E04/E05/E07**：分别按选择/数字候选、触发/对象生命周期、卡牌/玩家状态差异补验；共用检查引用负责批次的证明。E09的R29/R30/R31原有界行已关闭，战斗相关共享控制、多人客户端和联网差异保留E02负责。
4. **E12**：特殊模式逐项判断复用与支持范围，确需新表现的单列。展示检查与A06记录随对应批次完成，稳定后完成收尾回归。

每轮在选定缺口、相关消费者与适用恢复断言通过后停止扩大；新发现的具体分支、失败或变更才增加回归。按关闭缺口、证据复用及具体阻塞汇报，测试条数仅作辅助。有限真实联网证明见R02本批审计，其他时序仍开放；合并排期本身不核销边界。

## 证据与维护入口

- [运土与截气生命周期](BendingLifecycleAudit-2026-10-07.zh-CN.md)：9条真实流程、21资源末态、23项UI增量；跨区域标题/结束目标按钮修复及普通目标回归。

- [暗置私有身份与翻正生命周期](FaceDownLifecycleAudit-2026-10-07.zh-CN.md)：5条实际规则、13个资源末态、29项UI，动画命中/提前离场及持戒者公共标识。

- [付款退款的已有状态增量](MultipartCostStateAudit-2026-10-07.zh-CN.md)：已有堆叠/延迟、牺牲卡指示物三差异和准确multiset恢复；当前四场定向回归。

- [多段费用取消与替代增量](MultipartCostConsumerAudit-2026-10-07.zh-CN.md)：8条实际规则、8条关联回归、9项UI射线；R12/R47明确保留未完成边界。

- [起手顺序、配置与取消重入](OpeningBoundaryConsumerAudit-2026-10-07.zh-CN.md)：五条原生起手、真实LAN大厅、生产会话释放重开及Auto射线；旧三语言/联网保持原身份。

- [牌库顺序与双区域移动](LibraryOrderConsumerAudit-2026-10-07.zh-CN.md)：七真实规则、三语言/相关消费者增量及标题测高；未可达插入审查归A05，原行/分类/PASS分别保留。

- [多堆与预定义堆审计](PileBoundaryConsumerAudit-2026-10-07.zh-CN.md)：R43有界关闭、11原生首验及接收者查看/首帧行高修复；原失败与生产者身份保留。

- [首验核销账](../.agents/skills/mtgb-mechanic-validation/references/verified-progress.json)：539原生/41UI、部分及关闭范围、原批次与SHA-256。
- [余下API分类](RemainingApiConsumerAudit-2026-10-06.zh-CN.md)：四API获得源码归属，StoreSVar七类计算、空间字段与原生翻牌模拟仍需实际验证；分类不增加效果PASS。
- [征募/凋萎/强度审计](StateBranchConsumerAudit-2026-10-06.zh-CN.md)：十场实际原生、强度字段与原始切换、一套三语言真实点击及同ID刷新，S03部分完成。
- [Discover消费者审计](DiscoverConsumerAudit-2026-10-06.zh-CN.md)：九条原生沿用、一套新增UI、四场Empower回归、三API归类及A06本批影响记录；原失败/中断与最终通过分别保留。
- [费用组合专项](CostInteractionAudit-2026-10-06.zh-CN.md)：本次126条新增组合、135条相关回归与失败/重试的独立身份。
- [雪境/混合及历史X登记](ManaVariantCostAudit-2026-10-06.zh-CN.md)、[生命支付修复](PhyrexianPaymentAudit-2026-10-06.zh-CN.md)：R06/R07/R08有界关闭证明。
- [旧脚本分类](LegacyScriptClassificationAudit-2026-10-06.zh-CN.md)：分类依据与历史证明迁移待办。
- [验证方法与历史入口](../.agents/skills/mtgb-mechanic-validation/references/verification-method.md)：局部回归、原始失败、运行身份和此前批次。
- [执行归属](../.agents/skills/mtgb-mechanic-validation/references/execution-plan.json)、[机制验证 skill](../.agents/skills/mtgb-mechanic-validation/SKILL.md)：排期与检查方法。

系统 goal 保留原objective，用户已明确继续；get_goal仍为paused且状态工具不能resume，本文件不假报系统active。本批新增十条规则/一套UI运行，S03保留未测边界；未关闭包。历史审计与证据原样保留，已有UI样式决定沿用对应审计。

- 2026-10-06 目标分支：[九场规则与三语言界面审计](TargetBranchConsumerAudit-2026-10-06.zh-CN.md)，R13/R14原行核销；其他控制/复制/随机/网络边界仍开放。

- 2026-10-06 耗竭：[原生费用／攻击及状态审计](ExertBranchConsumerAudit-2026-10-06.zh-CN.md)，六场规则、一套三语言UI及双攻击者定向补验，R50原行存档核销；荣光竞技场产费／敏捷、原生叠放门槛与其他控制／战斗边界继续开放。

- 2026-10-06 数字／分配：[消费者及影响审计](NumberAllocationConsumerAudit-2026-10-06.zh-CN.md)，11新原生／2UI，修复任意数字默认99上限及竞价高度／语言；R05原行有界关闭。Int32限制、R09/R18/R36组合等组合继续开放；R47 Hellion触发详情已在后续有界核销，其他缺译/未知文本仍开放。


- 2026-10-06 触发文本：[实际段落／目标／滚动审计](AbilityTextConsumerAudit-2026-10-06.zh-CN.md)，一套新增UI、两个中文消费者补验；日文既有英文回退如实保留，R47继续开放。

- 2026-10-06 昼夜／速度：[九场原生及状态展示审计](PlayerStatusConsumerAudit-2026-10-06.zh-CN.md)，R25原行保存有界闭包，其他消费者/控制/组合仍开放。

- 2026-10-06 生成预防效果：[来源卡图／标题及说明审计](EffectSourceConsumerAudit-2026-10-06.zh-CN.md)，一套新增UI、一项直接投影适配器、两条原生相关回归；R47/R18及隐藏区域权限继续开放。

- 2026-10-06 同时触发／章节：[消费者审计](TriggerChapterConsumerAudit-2026-10-06.zh-CN.md)，七条原生首验、两套UI首验及相关展示回归；R18核销完成范围，其他组合仍开放。

- 2026-10-06 延迟离场：[消费者审计](DelayedExpiryConsumerAudit-2026-10-06.zh-CN.md)，三条原生首验及既有UI增量回归；R19核销完成范围，其他组合仍开放。

- 2026-10-06 风暴复制：[消费者审计](StormCopyConsumerAudit-2026-10-06.zh-CN.md)，四条原生首验及既有UI增量回归；R20核销完成范围，其他组合仍开放。

- 2026-10-06 君主/先制：[状态审计](RoyalStateConsumerAudit-2026-10-06.zh-CN.md) 六条真实规则+三语言状态首验；[边界/来源审计](RoyalBoundaryConsumerAudit-2026-10-06.zh-CN.md) 四条真实规则、一个直接投影适配器及共用异能消费者增量。R32核销范围，玩家离场/多人仍待验。

- 2026-10-06 魔戒：[战斗与持戒者审计](RingCombatConsumerAudit-2026-10-06.zh-CN.md) 七条实际规则、一套新UI首验、三条当前规则回归，R33原行有界关闭；R26/R31组合与R47数量字号新观察继续开放。

- 2026-10-06 地城：[路径与消费者审计](DungeonPathConsumerAudit-2026-10-06.zh-CN.md)，13条原生首验、1当前回归和4已有UI增量，R34原行有界关闭；[叠放数字行高](StackCountGlyphAudit-2026-10-06.zh-CN.md)核销R47该观察。多人、控制和其他hidden/public限制仍保留原包。

- 2026-10-06 多徽记：[消费者审计](MultiEmblemConsumerAudit-2026-10-06.zh-CN.md)，3实际原生首验及既有三语言UI增量；两项准确徽记图/索引、导入副类型漏项修复。R35部分核销，复杂/动态/延迟和真正联网仍开放。

- 2026-10-06 相互/嵌套玩家控制：[消费者审计](NestedPlayerControlConsumerAudit-2026-10-06.zh-CN.md)，2实际原生首验及已有UI增量；逻辑控制者与实际操作席位分别核对。R36部分核销，控制者离场/其他请求/真实网络边界开放。

### 2026-10-06 本次接续

- 网络普通负载：共享UTP队列由128扩至2048，dedicated/LAN实际两场双席BO1、三次原请求重连及胜局结束通过，五份进程日志无队列满错误。[冻结结果](../.tools/network-queue-1006/result.json)保存原失败、配置来源与构建身份；极端多房间、乱序/重复、其他机制中断仍属R02。
- 旧效果证据：四驱动的原结果与九项历史末态已独立核对，原执行身份缺失保持未版本化历史，不增加当前PASS或首验；具体差异交原负责包，不以整体迁移为门槛重跑四套。[复核结果](../.tools/legacy-proof-review-1006/result.json)。
- R09：魂灵洞窟类别/非生物限制、远古金字塔生物限制、映景湖不继承用途限制/无可反射颜色、污染将树林产费替换为黑色，五项真实原生流程通过。356生物类别改为搜索/分页字符串列表，原索引、权限、必选/重复/旧请求、固定字号与三语言射线通过；相邻实际异能/颜色控件定向核对。[原证据与影响记录](../.tools/restricted-mana-1006/result.json)。
- 原有中文28、日文31个类别缺译保留规范名，范围写入R47；其他受限用途分类仍在R09。本次未升级运行包、未执行完整回归，自动失效状态继续保留。

- R09追加两场反击差异：洞窟支付的生物抵抗实际Counterspell；映景湖支付的生物被正常反击。保存原先五场驱动身份，未重跑五场或UI。[增量原证据](../.tools/restricted-mana-1006/counter-increment.json)。洞窟不可反击原生effect存在/到期已核对，其辅助图配方/悬停另列R35。

## 2026-10-06 荣光竞技场与花费附带效果增量

新增八条实际原生首验：荣光竞技场付费耗竭、两份法术力分付给两个生物、回合末敏捷到期和跳过/恢复重置、拒绝耗竭退款重试、非生物花费；耶甘沙有色/非通用费用、液压助手神器限制、生物法师入场+1/+1及护目镜花费触发复制。具体场景/断言和原版本在 `.tools/arena-mana-1006/result.json` 与 `.tools/mana-spending-1006/result.json`，相同语义已通过项不重跑。

只为原生耗竭确认提示增加三语言本地化，复用紧凑双按钮；精确Yes/No射线、过期/重复/非操作拦截和查看战场往返字号通过，`.tools/arena-mana-1006/ui-increment.json` 为UI增量，33UI首验不增加。旧点击PASS后截图窗口已消失的尝试保留，仅修截图时机重采，未重新跑原生。

按原生构造器九字段盘点245条AB/SP/DB Mana/ManaReflected声明，归为14分支并保留具体谓词/本地SVars；补入初版遗漏的TriggersWhenSpent/PersistentMana/CombatMana/SP入口。`.tools/mana-consumer-classification-1006/result.json` 只作R09与A01/A03/A04共用源码分类，不能当效果通过。R09/R12具体完成范围已核销；其他用途公式、关键词期限、条件指示物/花费触发、法术力保留、辅助effect展示及网络仍开放。累计352原生/33UI首验；goal继续且未完成。

### 2026-10-06 受限用途与法术力期限接续

本批7条原生首验均由实际激活/施放、合法性拒绝、费用选择及结算取得：Rosheen受限法术力支付XX=2、生物2/2与两指示物；James和Thran的仅激活/非咒语限制正反例；Vhal支持坟墓场Deep Analysis闪回并扣3生命、抓2与放逐；Adarkar支付Remora累计维持并保留一蓝、拒绝Opt；Birgi红费跨阶段保留并在下一回合清空；Roku六红跨战斗阶段保留并在第二主阶段清空。实际资源、换区及继续优先权都有独立断言，范围限于这些样本。

原生结果与三份成功驱动/夹具版本、失败和完整轨迹见 [.tools批次证据](../.tools/mana-lifecycle-1006/result.json)。UI仅增量：从原trace提取牺牲/可选触发/生命确认请求，修复精确提示本地化和正常卡牌Phase尾注阻碍正文翻译；三语言实际射线、旧/重复/非操作请求守卫、查看往返字号与Thran正文取样通过，见 [UI增量](../.tools/mana-lifecycle-1006/ui-increment.json)。第一次扩展UI在日文完成前退出运行，保留RUNNING与说明，仅重试该界面检查。未重跑原生，也未增加UI首验。

R09保留CostContainsC、结算UnlessCost/激活X、其他区域/谓词及关联SVars、RIOT等关键词期限、条件指示物/其他花费触发，以及保留/替代/抽空法术力和额外战斗的期限差异；资源取消和标记/控制/网络分别引用既有负责包。本批刷新来源仅familyContracts变化，来源一致；2690未登记/197需重测仍是总指纹工具状态，没有改为当前全量PASS。skill只补实际合同复用与中断取证方法，原证据身份不变。

### 2026-10-06 激活与结算费用谓词接续

本批五条原生首验及四条当前相关回归见 [冻结证据](../.tools/mana-predicates-1006/result.json)。Rosheen支付Banshee激活X=3、Condescend结算X=3、Thassa二倍X=4均核对池、生命、换区和继续优先权；固定Mana Leak费用3不能使用Rosheen法术力，点击后资源/费用不变，再取消正常反击；两份Cultivator Drone法术力支付Bearer触发1C并实际牺牲对手生物，独立命中含C费用谓词。五条均由真实操作取得，夹具只安排开局。

发现二倍X费用在原生展开为数字后丢失X来源，导致费用点击无进展。修复仅识别准确原生配方 `SVar$X/Twice` 与 `Count$xPaid`，不依赖卡名；其他公式继续开放。运行包由 `b3c1f760…` 到 `b455ce96…`，两份清单校验确认仅新增AbilityManaPart及编译器switch辅助class，卡牌资料、桥接/协议、JRE和其他原生补丁完全不变。修复后普通XX施放、激活X、结算X及含C触发四条同函数回归通过，不重复统计首验。

[UI合同复用](../.tools/mana-predicates-1006/ui-reuse.json)保存逐场原生字段、付款/操作席位、准确对象与选项身份、门槛和旧证明引用；付款、数值、模式、堆叠目标、牌列表合同相同。本批没有新增UI点击/截图/语言/联网运行，也没有新增UI首验。原生提供池颜色不代表该受限法术力能付款，拒付由原生决定，保留既有无进展刷新与取消恢复路径。

R09核销本批具体范围，剩余Echo/Volcano Hellion费用来源、其他关联SVars、区域/合法性谓词、关键词/指示物/花费触发及法术力生命周期差异；R12/R35/R36/R02/R47仍保留各自恢复/标记/控制/网络/动态文案边界。原成功版本、实际无进展失败和驱动错误均保留；首次与当前回归分开。全局指纹失效保持原状态，阶段收尾仍需稳定的完整基线。

### 2026-10-06 回声费用来源接续

三条实际首验见 [冻结证据](../.tools/mana-echo-1006/result.json)：Volcano Hellion施放2RR、入场选择16点伤害后生命20->4，下次维持用Rosheen四点受限C支付回声4并保留6/5；对手在回声触发后施放Shock使生命4->2，费用实际变为2，支付后还剩C2；Urza's Blueprints回声6不能用Rosheen法术力，手动点击费用/池均不变，取消后原生牺牲并恢复维持优先权。数值和区域都来自实际施放/触发/响应，夹具只安排开局。

原生限制先读取空的触发激活费用，漏掉独立Echo费用；修复只在结算中的准确Echo参数读取原生Cost，保留X符号，金额仍由原生计算。生命变化取结算时当前值符合 [CR702.1b/702.30a](https://media.wizards.com/2026/downloads/MagicCompRules%2020260925.txt)；X用途规则参照 [官方Rosheen说明](https://magic.wizards.com/en/news/feature/modern-horizons-3-release-notes)。运行包 `b455ce96…` -> `f9cf6599…`，清单精确仅AbilityManaPart.class改变，其他类/资源/桥接/协议/JRE不变；此前二倍X和固定税拒付两个真实回归通过。

[UI复用记录](../.tools/mana-echo-1006/ui-reuse.json)保存费用2/4/6、原生ok/cancel、付款席位、来源按钮和空预览字段，与已验证外部费用合同一致；无新增界面/文案/布局/网络运行。来源产费会被原生立即分配，不强求出现四次池按钮点击。原始规则失败、错误显式付款次数断言和读取非授权席位手牌的驱动失败均保留，只重试失败/未跑案例。独立补验三条原轨迹的最终UPKEEP条件，不为统一驱动哈希重跑。

R09回声金额/来源/固定拒付这一具体待办已核销，其余费用公式、区域/合法性谓词、关键词期限、条件入场指示物、花费触发及法术力生命周期仍开放；其他回声免付/控制/替代组合引用R51/R36/R44/R19。当前仍11完整关闭、21部分、33其余开放，本轮没有新关闭包；不能把三条新增场景换算成总体剩余工时。


### 2026-10-06 花费法术力附带效果接续

六条真实原生流程通过：Domri实际施放并+1产红，支付后躁乱两种入场结果；Hall实际付3生命产无色，敏捷跨清除步骤保留；Jade实际支付龙咒语，+1/+1指示物保留且辟邪在下个己方回合前到期；Foundry支付Opt，占卜抓牌后接受或拒绝可选放逐，原咒语身份及区域准确、资源和优先权恢复。Hall开局夹具安排在场，不授予其进场横置验证。

一套新增UI以真实请求重放，三语言分别核对颜色/躁乱/可选替代及两种触发堆叠段落，实际射线提交原选项、固定字号、滚动和查看战场往返。初次整套FAIL中的独立中文已完成分支保留，其他语言与受影响颜色/牌名以后续选定子范围PASS补齐；不把原FAIL改成全套PASS。两个原生驱动失败和一次UI编译错误保留，只重试相关项。支付、占卜和未改的权限/状态合同引用旧证明。en-US静态卡窗标题和忠诚标签仍有中文，明确留R47；改名负例仅是UI投影检查。

[本批冻结证据](../.tools/mana-spent-effects-1006/result.json)及[UI组成矩阵](../.tools/mana-spent-effects-1006/ui/aggregate.json)记录原文件、SHA-256、修复和具体复用范围。R09核销上述六条差异；其他关键词/条件指示物/花费触发/法术力期限仍开放，Foundry后续免费重施归R51，未结算和生命周期差异归R19/R44。累计373/34、登记75引擎任务（583规则/起手、12适配器）及67UI套/25族；没有新增完整关闭包或当前全量/联网PASS。


### 2026-10-06 花费条件正反例接续

新增六条实际激活/产费/手动花费/施放结果：Animal Attendant支付非人类Goblin Piker进场3/2及一个+1/+1指示物，人类Bloodlust Inciter仍1/1无指示物；Guildmages' Forum实际支付1费用、选择条件产费异能与颜色后，双色Watchwolf进场4/4/一个指示物，单色Goblin Piker仍2/1无指示物；Carnelian Orb支付龙Dragon Hatchling授予敏捷，清除步骤后进入对手回合时失去敏捷，非龙Goblin Piker从未获得敏捷。原卡身份、来源横置、生命20、空法术力池、空手牌及下一优先权核对；Forum同时核对三个准确费用来源横置。

真实ManaColor六选项/取消和两异能原索引，与旧Cavern请求的操作席位、门槛、身份类型、选项启用/选中、预览/排序字段逐项一致；支付合同保持既有池/来源/自动/取消分支。引用已有三语言实际点击和权限证据，不重复UI/网络，也不把新卡牌实例的所有翻译/窄屏变化授予PASS。原生第一次驱动把ManaColor误作异能列表，保留FAIL并只修驱动；随后六条首验一次通过。

[本批原证据](../.tools/conditional-spent-mana-1006/result.json)与[准确合同复用](../.tools/conditional-spent-mana-1006/ui-reuse.json)保存生产驱动、夹具、轨迹、结果及SHA-256。生产规则/桥接/协议/资源/Unity/运行包均未改；来源仅开局摆放，不授予这些来源的施放或ETB证明。R09的非Human/多色和Dragon谓词具体差异已核销；指挥官金额、其他关键词/谓词/期限仍开放。累计379/34、登记76引擎任务（589规则/起手、12适配器）及67UI套/25族，没有新增完整关闭包或当前全量/联网PASS。


### 2026-10-07 R45抓牌/空牌库/预防到期闭包

七条真实原生差异包括自己的抓牌步骤第一张豁免、额外抓牌的两种替代顺序、空牌库输局/实验室狂人的替代胜利、Familiar接受与拒绝跳过，以及Ethereal Haze预防跨回合到期。实际施放/支付/激活/阶段推进产生结果，完整身份、私有手牌、牌库顺序、资源、胜者和继续优先权独立核对。夹具只安排开局，不授予初始来源自身的施放/进场证明。

新增结果消费者UI以原生ended封包重放双方最终视图，共四格。真实射线发现并修复结果按钮best-fit及只读浏览遮罩阻挡返回结果按钮，四格字体/边界/只读ID/继续和返回均通过。替代顺序、optionalReplacement、支付/目标/生命控件按准确原请求合同引用旧证明，不重复无关UI或联网。

[有界闭包审计](DrawBoundaryConsumerAudit-2026-10-07.zh-CN.md)与[冻结证据](../.tools/draw-boundaries-1007/result.json)保存完整原R45行、七个原生身份、三个成功驱动版本、两处驱动FAIL与两个实际UI失败。只重试失败或新增差异，聚合没有改写原整套FAIL。原边界关闭后53开放/12关闭/21部分/32其余，当前已知总包65；并非53条测试或53轮。其他抓牌谓词、期限、控制/多方/可选搜索和语言缺口留原归属，没有宣称当前全量/联网PASS。


### 2026-10-07 R42多骰差异闭包

五条实际流程覆盖数值修正后的分支与原通知值区分、两骰指定低/高结果、同一骰子的两个修改来源，以及两骰同时选择重掷。完整原行与原生资源/身份/结算、39格现有消费者三语言增量见[有界审计](DiceCombinationConsumerAudit-2026-10-07.zh-CN.md)及[冻结证据](../.tools/dice-combinations-1007/result.json)。UI属于原控件增量，不增加首验；原三场成功、两生产驱动版本、三原生驱动FAIL、UI部分PASS和中断保持原身份。

唯一展示修改是骰子结果/紧凑增减/移去指示物费用提示；源选牌窗口的Artwork可点击，误点后面战场牌的测试被修正，遮罩无需修改。52开放/13关闭/21部分/31其余，65总包未变。吸引力/平面/物理模式、其他存储重掷/事件、多方/控制/未知文字等留原归属；没有当前全量或实时联网PASS。


### 2026-10-07 R46可选搜索与来源闭包

十条原生真实流程覆盖可选进场搜索的接受/拒绝/空池、顶四候选/预览限制及取消、禁搜牌库、混合坟墓场/手牌来源与对手可选搜索的接受/拒绝及后续次数。每条核对准确牌号/牌区、双方牌库集合或原顺序、实际横置支付、零池/生命与恢复优先权。普通导师/多张搜索/Portal限定堆证明沿用，四份成功驱动身份和两次原生夹具误判保留，不为统一版本重跑。

仅Notices.cs修复准确搜索提示和带触发来源的多行确认本地化。52格UI矩阵由原两次整体FAIL中的18格和1格完成记录，加最终33格组成；主语言新来源/预览/取消真实射线，变化文案三语言/双方决策/旧请求/重复/服务禁用输入/字体和旧二元消费者回归。手牌暂选后确认与下一原生ok/cancel/reset的advanceButton为两个步骤，测试按实际路由修正。见[有界审计](SearchBoundaryConsumerAudit-2026-10-07.zh-CN.md)和[冻结矩阵](../.tools/search-boundaries-1007/ui-matrix.json)。截图中的通用Keyword Effects辅助标记仍显示缺失卡图；这属于原R35辅助展示范围，本轮未修改或核销该缺口。

51开放/14关闭/21部分/30其余，已知总包65未变，401原生/35UI首验；8执行批次不等于剩余轮数。泛区域检视R15、揭示知识期限R17、控制/多方R36/R03和未知文本/谓词R47/A03/R49继续开放。清单一次不运行刷新，2683未登记/204需重测不变；只有familyContracts登记与边界说明变更。没有当前全量或实时联网PASS，goal继续。
