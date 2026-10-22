## 2026-10-16 牌库知识与驱动更正

原测试假设 Elixir 只洗自己的库，把另一方丢失旧位置误判成刷新；同步调用栈证明是实际 shuffle。移除据错误假设添加的 StackWalker 过滤，另用 Evolving Wilds 验证未变化库的保留。首版多牌守卫以全列表判断，错误隐藏双方各一张，修为逐所有者。截图驱动重复开详情或在延迟销毁同帧取图，最终图不支持布局结论；改为稳定帧并逐图看。优先权合成选项误加 card DTO 触发 picker，纠正夹具后仅补对应 6 条适配器边界。保留原 FAIL，真实原生结果、合成布局和快照 UI 分开记；不恢复旧 goal 或授全卡池 PASS。具体结果及身份见 `.tools/library-browser-1016/audit.json` 和 Docs/LibraryBrowser.2026-10-16.zh-CN.md。

## 2026-10-16 选牌排版检查漏项

用户指出已发布截图仍有重复选择说明和滚动条干扰。前批 171 项检查只确认文本内容高度及不覆盖卡图，没有检查提示重复、实际 viewport 或无溢出时滚动条，因而错误扩大为排版通过。复核发现卡牌内容高度还包含说明增高量，产生虚假溢出。

当前真实请求先稳定复现重复提示，修复去重后再以实际控件复现虚假滚动；修复共同布局后通过三语、三档字号、射线／原选项提交及缓存刷新 199 项，与长网格／只读原入口 6 项分开登记。首次边界驱动错误假设当前画布会溢出，又直接向只读渲染器传入可操作选项；保留失败，改为明确受限高度与生产 `ShowReviewCards` 入口后通过，不把驱动错误记为新生产缺陷。验收条目集中维护于 `ui-reuse.md` 的“选牌说明与滚动验收”。证据 `.tools/picker-layout-1016/`；本轮仅源码和编辑器复验，未再次打包／部署。

## 2026-10-12 N65：整体闭合的证据不足，撤回N64完成结论

用户指出：取消两类整链审查不代表1000多条都已判定为重复。原目录实际1095条：70准确结果、10后置、1015未关联；77执行器旧结果和两个代表对照只支持各自复用范围，没有完成整个目录的接入差异归类。N64从“没有发现具体新增差异”直接推定整个剩余范围无需核对，整体闭合过早。

恢复原A01.01一项有界差异筛查，不新增编号或逐卡测试配额。批量识别请求/身份/权限/私有信息/恢复合同，只对具体旧断言未覆盖的项目差异补跑。原真实结果与准确原卡身份不变，后置范围不恢复。N64取消的逐卡整链重验不自动恢复；完成结论和两原组整体核销撤回。检查生成器及原身份/排除范围一致性，不以文档修改新增效果PASS。见[更正记录](../../../../.tools/redundant-work-disposition-1012/closure-clarification.json)。

## 2026-10-12 N64：取消不必要工作，不能只换统计口径

触发事实：用户指出N62/N63从1015/806改报3/2类合同，仍没有判断剩余审查本身是否必要。错误决定：把“旧证据未证明另一张牌整条链”视为继续比较/补跑的理由，实际扩成重验未修改Forge的卡牌组合。

修正：先要求具体项目接入差异/本项目补丁影响/实际失败，再比较缺少断言。Discord循环次数和Goblin Crash Pilot力量公式由原生引擎执行，客户端没有新的循环或计算分支；生成牌施放与期限、Kardur先牺牲后选目标、独立Immediate记忆、离场力量记忆和延迟恢复已有真实证明，故取消两类追加整链审查，而非把未测整牌授PASS。原明确尾段N61已补完。119/125原输入以当前有界接入范围闭合，6原模式组仍未验证；70准确原卡结果、首验552/41与失败保持。详情见[实际取消依据](../../../../.tools/redundant-work-disposition-1012/audit.json)。

统计器收尾修正：A01归档后仍从所有原事项读取125输入和排除组，防止显示0/0；后置输入可在groups内，按原嵌套身份检查明确未验证状态。生成器和负例检查覆盖归档保留/漏标后置状态，未运行新效果。

2026-10-12 N63旧缺口复活反例：N62示例仍引用N44的Caged Sun非mana-ability分支，当前原声明已EXACT_RESULT_JOINED，N51实际Sun/蓝色/红色对照与87UI检查已完成。选比较样本前先查当前原声明处置和后续增量，不从旧remainingScope恢复待办。77共享执行器源码与39控制器候选已对齐，选择合同复用；生成授权和延迟反身结果仍开放，不从路由存在授PASS。依据[本批](../../../../.tools/shared-contract-routing-1012/audit.json)。

2026-10-12 N62工作量反例：N57已找到77共享实现的旧结果，后续仍把1015未逐卡关联和806细分上下文保留为执行队列；修正为共用合同核对与具体补测表，原声明归零不再是完成门槛。reconcile.py的storedReaders扫描整个牌面，Faerie Dragon延迟根包含其他随机分支；只有证明读取本次所产生值的边才成为义务。旧结果不改写，0新运行、无新增效果PASS；核对未完成的未知仍保留。依据[复核](../../../../.tools/workload-contract-review-1012/review.json)。

2026-10-12 N55进度口径反例：两组原minimum明确要求关联共享消费者、不按卡牌各开测试，状态回答却突出393+672未关联声明，造成剩余工作量再次扩大。准确原结果关联是证明来源统计；用批量当前源码/牌面/消费者归属表衔接已有断言，再列具体缺失结果/权限/生命周期/UI合同作为补跑表。十二根API队列、81现有效果消费者、相同链签名只用于审查路由，不授PASS或充当净新场景数。一个准确旧结果与十条已授权后置声明直接登记；其余声明不能伪造完成。依据[批量处置与原证据](../../../../.tools/payload-bulk-reconciliation-1012/audit.json)。

2026-10-12 N54费用/命名消费者反例：canRespond的准确原生可用选项计算只在该接收方当前InputPassPriority上下文执行；在另一牌手末态读取它不能证明本牌手禁用。按原物理ID与真实actor优先权请求核对禁用/恢复，保留已完成支付/抽牌/复制结果及原rawFAIL，未来驱动改正确时机，不重跑付费流程。Garth复制品setZone(None)尚未进入None集合，确认预览应沿原生confirmAction的准确cardToShow参数暂存，并在finally恢复上下文；源/目标权限仍由原生决定。当前零效果回调适配器与旧实际复制施放分列。依据[本轮审计](../../../../.tools/name-consumer-delta-1012/audit.json)。

2026-10-12 N53旧证据合并反例：Killer Cosplay克隆期间同一物理ID155的name和originalName均变为Whitemane Lion，按Silvercoat Lion名字查找会漏原对象；改为冻结初态ID后跟踪。Nissa38付款在触发内部，raw paid为空，原独立审计和38真实Added/Removed记录仍有效。DCI实际选的是Grizzly Bears，共六物理原件含备牌，不能按印象猜五Shock。七原声明以冻结执行源码、准确face/line和实际后继49检查关联，不重跑任何付费流程；原失败及更正依据见[本批离线修正](../../../../.tools/payload-proof-joins-1012/driver-corrections.json)。

2026-10-12 N50排期生成反例：游乐设施普通1v1完成后，S04原Planechase分支仍由用户后置。生成器仅接受活动owner导致有证据关闭被错误拒绝；现在归档owner也须保留准确原input、明确DEFERRED_UNVERIFIED、用户依据/恢复条件及已核验的当前范围关闭证据。空活动批次保留到批次证据后移出活动排期，不放宽空批次守卫。实际N50账本生成与缺失延期状态的负例检查；原生结果及生产文件不变。

2026-10-12 N45分区投影增量：共享规则与551原选择窗口证明已覆盖时，仅补公开字段/分组/卡上归属与详情；原实付施放六分配前缀因Gson省略空属性而KeyError，冻结前缀，只补未执行忠诚尾段，明确独立开局不可拼为同局。UI计数节点按实际Visual直接子级判分组，Counter_P1P1/Count不是重叠数量；实际源层级先查再断言。独立零效果投影adapter不记真实结算，startGameHook在Mulligan后运行，驱动保持stdin并处理实际开局请求，否则EOF退出/未写结果不算PASS。已知离场/LKI宿主的chosenSector由原生值决定，所属sector标记只在战场显示。依据 [N45分区展示闭包](../../../../.tools/sector-display-1012/audit.json)。

2026-10-12 N44父选择与旧子效果复用：已登记的生成牌子根不一定包含上游NameCard行，不能因此重复跑父流程。反向绑定准确source/face/SubAbility与旧子链，检查同次原来源无预设名称、真实选择或随机结果被子效果消费及清理；九原父据此引用旧结算，不以签名相同授PASS。旧Elesh Norn使两次ETB产生两复制品，按原实际场景保留。颜色咒语进坟墓场成为新对象会清所选值，核对占栈实际值→候选→弃牌及新时间戳，不为误设保留断言重跑；对手公开手牌选择用实际Hand根点击，旁观者JsonUtility默认request不当活动输入。原UI失败与有效前缀保留，仅补差异。依据[三选择消费者与九旧父链](../../../../.tools/choice-payload-tail-1012/audit.json)。

2026-10-12 N42生成延迟与精确旧声明复用：夹具读取实际getTriggerRemembered，原生衍生物名为Zombie Token；无动作优先权可自动跨回合，断言保留实际终点。相同基本地已按状态重叠，UI不得要求每个物理ID独立GameObject，改核对相关代表永久物；保留68成功语言前缀，仅补47生命周期尾部。精确生成正文与清理步骤标签在共用文本入口翻译，未知附加条件保持原文。CopyPermanent的Choices由getValidCards解释为牌谓词，不是假缺SVar；签名只用于归类，不能授结果通过。旧声明须准确path/face/line落在原已通过根链，且核对实际复制/返回/自然到期结果及版本，才登记复用；没有新运行。依据[本批结果及保留失败](../../../../.tools/generated-delayed-tail-1012/audit.json)。

2026-10-12 N41公开临时牌与真实起手反例：Game.getCardsInGame不包含None，原生明确公开的临时CardView可能仅序列化名字；必须查看实际截图/卡图，不以存在9个tile代替9张卡面。只解析本次原生传给GUI的准确None对象，不能扩成普通视图公开整个None区。Card.isTokenCard与isToken是两种标志，四张Time Walk能保留牌库而非普通衍生物；真实opening action早于startGameHook，必须用实际注册套牌/抓牌/开局动作，不在hook里塞手牌。旧Nissa11忠诚缺精确+1可对冻结原结果加离线断言，不重跑38mana。Avatar随机落永久物是未到咒语分支，原正常MAIN2末态保留；真实免费咒语下一选择只是驱动未匹配，保留完整前缀及原FAIL，不授所有随机咒语payload通过。原始公开整包只有接收者2，owner取得完整原name选择，不猜两公开recipient。赠礼一般字符串不是追加mana，费用保持原费用。依据[原21生成链闭合](../../../../.tools/booster-copy-tail-1012/audit.json)。

2026-10-12 N40生成牌反例：Doom Blade不能目标黑色Bone Rattler，不归规则失败；保留Leech正确前缀，后继用真正合法Go for the Throat。随机Praetor产生Elesh Norn使后续ETB复制两次，先查真实静态消费者再定数量；不同宿主尾段单列。公开后的手牌是已知牌，隐私断言仅排除未公开原牌；RevealCards按原接收者及实际HandAction确认，不猜席位/按钮。整包未公开与只公开所选牌分开保留；通用消费者不逐牌重复三语言。依据[本批审计](../../../../.tools/payload-tail-1012/audit.json)。

2026-10-12 特化最后21原根与精确显示：恢复已有放逐上下文需同时恢复目标exiledWith与来源exiledCards两方向，原生Defined ExiledWith读取后者；夹具缺失不作规则失败。必选弃牌的selectedCount>0不等于确认已启用。选择类型取实际提示段而非完整异能段，ParentTargeted可能尾随，不用最后一行猜提示。派生牌面同printingKey仍匹配自己的原印刷段落；特殊形态后缀先分离、保留准确公开来源。实际消息含全规则时核对展示出的文本和固定字体滚动，不能只用反射返回字符串替代。测试按钮名字误用保留有效原点击前缀，只补生产真实按钮；旧成功流程不为纯文案hash重复。依据[原52根闭合及真实蓝色两段选择](../../../../.tools/specialize-final-1012/audit.json)。

2026-10-12 特化面内定义与免费选择：同一牌面重复SVar会覆盖前定义；完整核对引用所在牌面、实际后继参数和StaticAbility的激活层后才修复。MayPlayWithoutManaCost仍需MayPlay许可，不拿字段存在当原生免费支付通过。Keyword CounterType保留精确Lifelink大小写，展示仅映射已知图标别名，其他自定义符号身份不变。原生施放dialog缺castingFace时仍按实际getAbilityToPlay合同；费用许可先于可滚动规则正文显示，三语言检查首屏而非只查底层text存在。测试按钮名称错配先检查生产控件/实际Ray，保留正确前缀只补差异。N37的pool类型文字更正见本批审计，原冻结结果不改。依据[蓝色两后继失败与实际补齐](../../../../.tools/specialize-more-1012/audit.json)。

2026-10-12 引用键与特化后继：效果引用键按实际消费者的大小写追踪；Animate使用`staticAbilities`和`sVars`，仅追AbilityFactory大写表会漏原5行后继。字段存在并已生成能力时，先查优先权候选/费用预测，不把大小写误当规则脚本错误。完整提示含多个子效果时按原合法候选ID确定当前选择；保留原成功前缀，独立后继明确初始条件和不同宿主，费用正例与普通牌负例同时核对。依据[原失败与实际增量](../../../../.tools/specialize-payload-batch-1012/audit.json)。

2026-10-12 牌面局部效果链：按准确path+line和Name牌面边界绑定冻结根，核对SPECIALIZE牌面；旧faceIndex错误保留并单列纠正。Choices先依实际API消费者区分玩家/颜色/卡牌谓词与SVar引用。恢复特化前缀的独立夹具同时保留state、specialized标记和有效触发，记录不同宿主，后续仍用真实支付/死亡/新时间戳证明；缺标记的夹具失败不能当规则错误。实际默认Self持有死亡前LKI时，按当前对象守卫定位原因；卡脚本以TriggeredCard修正确切死亡对象，并核对相同形态链后复用。战场裁图用RawImage纹理、窗口全图用Image sprite分别取证。依据[本批原始失败与补齐](../../../../.tools/payload-reconciliation-1012/audit.json)。

2026-10-12 BO3有界闭包与排除归档：真实原生第一局已完成而UI夹具失败时保留完整FAIL及执行源码；第二局尾段读取准确ended.series及实际UI SubmitDeck，分别验证新session的原生主/备印次与第二个ended，不声称同host连续运行。UI Create可能在selectedDeck前Refresh并冻结空基准，夹具需在第一次正式Refresh前初始化选择和基准轮数；实际 chooser 遮罩覆盖底层准备按钮时，不用穿透点击授可见PASS，准备取消点击在对方未准备时验证，遮罩中的取消仅按权威处理器界定。实际worker exit7→Poll Failure/abort证明不计分，实际新正常worker开局证明重试。已排除输入在剩余当前部分闭合后随原事项归档；生成器仍核对精确原ID及全部重复记录的EXCLUDED状态，不以owner不再pending删除排除或授PASS。依据[BO3实际换备与尾段](../../../../.tools/bo3-sideboard-1011/audit.json)。

2026-10-11 名称条件与原生自动推进反例：必选单候选名称会自动选择，没有名称请求不代表漏UI；以实际新选择及子效果原ID核对。ExcludeChosen历史应保留，但本次空选择不能令后续Play沿用历史最后一名。精确费用替换使用原生getShortString完整格式（如`1 {W}`），候选适配器须有正例与负例，两个错误的空集合相等不授PASS。XMin1是最低X标记，不是额外一个X；按实际支付与打印费用核对。ImmediateTrigger结算内支付可能无占栈payingMana快照，此时保留真实支付请求、唯一原法术力身份的Added/Removed事件与原来源，并核对数量；不补跑已成功效果来生成缺失观察器字段。原名称/复制已完成后另一个衍生物ETB的驱动失败，只核销准确成功前缀，并写明未完成尾段。依据[名称条件实测与原FAIL](../../../../.tools/name-predicate-tail-1011/native-final.json)。

2026-10-11 清理父链反例：实际支付须在付款后的占栈优先权快照核对；Whimsy子效果可重置已经付款的地，结算后横置净数9不代表X8UU只付9。保留实际已付10/八随机结果和原FAIL，离线补断言，不重跑。GameEventZone的Stack Added也可代表来源同ID的触发异能，不能当原永久物再次移区；原进入BF与后续清理以当前区/游戏时间戳、BF移除事件共同核对。被消灭衍生物先入坟墓场再因SBA消失，最终集合无该ID不代表未消灭。旧压印条件消费者结果可作明确推论，不能伪称直接看到中间字段。依据[本批证据](../../../../.tools/cleanup-tail-flows-1011/audit.json)。

2026-10-11 原生字段与已完成结果反例：付款界面列出全场法术力候选时，canPlay探测会把共享异能的activator改成最后付款人，Rhystic实际生成到对手池；按原操作者、两池和重复选择核对，预览后恢复原activator，实际激活仍设操作者。随机NameCard紧接Conjure时，背面空printed费用不代表该牌MV0；核对实际生成牌原身份/MV和命名清理，候选只在此子链取主牌面。Mana Clash第二次合法全正面没有伤害事件，按原flip通知/每方tails与生命差断言零伤害，保留原FAIL并离线恢复完整结果，不重跑。依据[本批证据](../../../../.tools/player-control-memory-flows-1011/audit.json)。

2026-10-11 真实网络夹具与增量恢复反例：开局证明未覆盖首个大操作视图，当前真实客户端复现超512KiB StateUpdate被拒绝；定位传输层后仅加入protocol18有界gzip，真实原视图/hash及后续操作证实修复，不扩成无限容量PASS。测试身份须检查实际NormalizeId24/NormalizeRoomId16结果，唯一后缀被截断曾造成重连串令牌；缩短room修复夹具。手动/AUTO读ForgeControlPolicy，On为手动、Off会自动让过；默认controllingSeat0是无外部控制，用原actor和自然归还验证。Windows快照共享/原子替换失败短重试，一次条件读取一次pending；冻结成功前缀只补失败尾段。随便旧数字不证明曾有效原生ID：只追加原Mulligan1对新priority5的真实拒绝，无成功付款重跑。七实际等待恢复、真实30秒判平/判负、18mana六操作、103冻结断言/43引用有界通过。依据[本批审计](../../../../.tools/network-boundaries-1010/audit.json)。

2026-10-10 记录值/卡脚本反例：原StoreSVar父条件失败不代表后续Effect不执行，不能全局改子异能语义（Splendor不抓时仍需更新生命）；用实际正/零/负牌状态定位本卡Effect门槛。Attacks发布Attacker，不是Card；仅更换键仍不能修复xCount解析，TypeCalculate需真实响应/结算/后续费用证据。auto付款可能拿最后绿色支付generic，夹具须保证真实后续有色支付，不预测付款分配。保留失败与成功前缀，只补失败/新消费者。资源补丁不改固定上游，校验源SHA/上下文/目标/未知改动并在完整复制后重套；7guard与精确runtime文件差异已证。详情状态数字与已知规则段分别翻译，避免格式参数吞掉后续段落；115实际UI检查仅此消费者，不重跑无关语言族。依据[本轮证据](../../../../.tools/noted-1010/audit.json)。

2026-10-10 横置/重置合批与计数纠正：同一夹具承载四个原API输入，共用二选/选牌UI；Turnabout两次实际整组结果完成后，后续法术驱动错过主阶段，冻结完整原结果只补Hidden Strings尾段。至多0/2与必须1分别核对原生min/max/selectedCount；不要把选完后的ok当成提前确认。ETB不触发事件须配同一Orb的真实普通重置正对照，两个原牌库顶准确磨入。原中文/英文UI完成而日语加载未就绪时保留FAIL和有效前缀，只等实际locale Ready后补日语。A01原88是混合引用，展开131/去重125，具体核销14、尚未完整核销111；生成器按原叶ID动态计算并拒绝不存在的完成ID，不能把账数当新测试/轮数。真实原生request中canRespond可能暂为false，非接收者不可为true；实际响应和UI权限独立证明，不能要求所有瞬间视图接收者必为true。依据[本批审计](../../../../.tools/tap-flows-1010/audit.json)。

2026-10-10 原控制者付款与编译反例：用实际targetId/controller seat、原付款来源横置增量和伤害/归零核对，不从费用文字预测实际结果。GameEvent源vars可能在排队发布前已被后续DBReset清零，不能当作预重置数值；结合原选择、付款与准确防止伤害结果。UI修改后0编译错误不证明新源码已加载；scripts-only compile可能未刷新外部改动，须明确导入/刷新并检查实际运行树，保留旧程序集UI失败，只重试失败UI不重做已付费对局。依据[本轮实际结果](../../../../.tools/rhystic-1010/audit.json)。

2026-10-10 Transmute链反例：首次驱动写前列全链原输入，牺牲、牌库InputSelectEntities、对手RevealCards确认均复用既有合同；法术先等实际MAIN1。搜索子异能先清空remembered再保存值消费，核对实际原SpellAbility保存值而非清空后的宿主默认。已完成冻结流程离线补精确差额/原mana来源扣费，不为新增断言重做施放。数值/对象相同合同复用，仅新增实际漏译键补真实控件/点击。依据[原输入链证据](../../../../.tools/transmute-1010/audit.json)。

2026-10-10 静态语言漏项修正：盘点范围包含 uGUI Text、TMP 与 IMGUI，不只原生长句。组合提示中角色/阶段/准备状态先翻译固定词再组合，用户名称和牌组参数保持 Literal/原样。空席位恢复语言绑定。短原生提示与英文界面残留中文均进入漏译诊断。新增键和新增消费者按差异验证，不为新增词句重跑所有旧效果；保留组合测试失败及此前独立通过的合同证明。依据[最终语言增量](../../../../.tools/language-unification-1010/audit.json)。

2026-10-10共用语言反例：规范英文协议必须留给分支判断，翻译只在显示边界。NativeDisplayText用接收者可见卡的准确编号/卡名绑定现有印刷规则，不查询隐藏对象。多语言别名保持打开窗口刷新，uGUI/TMP字号只乘一次，用户名称/输入/牌组参数保持原文。复用固定Forge词库前检查三语言/参数/歧义；相邻无分隔参数不能从拼接文本可靠恢复，不猜拆分，退役键要从实际表删掉。未知原生提示留原义并记录UntranslatedNativeMessages；盘点未匹配词句须区分已有规则消费者/卡图/实际漏译。局部语言增量复用已验证键和UI合同，只补新增键/格式/公开源绑定，不重跑原效果。依据[语言证据](../../../../.tools/language-unification-1010/audit.json)。

2026-10-10 Orgg与目标确认反例：原目标仍在候选中不代表需要再点；按原remainingCount/selectedCount提交确认，防止反复切换。Java ManaPool.toString为对象身份字符串，余额核对原序列化六通道；已完整结算的原生失败仅修判读，不重做真实施放。原组部分结果须登记子边界，不把Orgg完成宣称整个Transmute/Orgg组关闭。依据[原实际结果](../../../../.tools/orgg-life-1010/audit.json)。

2026-10-10进场生命X合同：Mandatory PayLife<X>先经原生数值one-dialog选择，不保证ChooseNumber，也不额外请求Yes/No；实际候选0..life及Nameless0..4，零不是拒绝。原首次错误驱动选0已完整施放/SBA/返回优先权，应恢复准确结果、保留FAIL，只补未完成正数和独立Token结果，不重复零值。数值控件实际节点为CardAbilityChoices，按运行树核对，测试节点猜名失败不修改生产代码。依据[本批原结果](../../../../.tools/etb-life-1010/audit.json)。

2026-10-10公开移入手牌与未完成效果：First Contact从公开坟墓场取回原牌会令对手合法继续知晓该ID，不能把对手Hand非空一律当泄漏；检查仅被公开的原ID允许显示、其他私有原牌仍隐藏。原FAIL停在对手RevealCards确认之前，gain/cleanup尚未发生，保留此前真实静态X1与响应计数2证据，但不能授予效果PASS；只重试未完成效果链，三施放同局同时验证第二原副本X3拒绝且不回血。新Search/Select跨坟墓场+放逐格式不在单区域翻译regex中，分别补精确问题/指示，不变原生身份/私有策略；原34UI检查保存，只补后加列表指示布局三locale，43标签去重。依据[原第一施放结果](../../../../.tools/first-contact-1010/audit.json)。

2026-10-10共同出力结果增量：同一数字/Unless/累加链只复用操作合同，找地/抓牌/磨牌/强化四独立下游分别核对真实原ID与资源。原主驱动后段未知InputAttack导致整体FAIL，已通过的四施放前缀保留，单补龙实际宣攻尾段；attack-remove:id才是选中状态，不能依赖为false的option.selected。未知请求立即失败，不能泛用OK继续空跑多回合。字段解码产生空CardDTO时按真实非空name核对候选归属，与生产HasChoiceCard一致；第一窗口已通过的十检查保留，只补第二私有窗口及清理，14标签去重。生产无改动，不扩展回归乘积。依据[原四结果增量](../../../../.tools/joint-payload-1010/audit.json)。

2026-10-10伤害观察合同反例：纯旅法师没有GameEventCardDamaged，因为原生仅生物发该事件。实际已付6并回血2的原FAIL不得误判为规则没造成伤害；只补观察器，记录忠诚2→0事件中的原Limit2，再结合移区前同源原生damage history4与回血事件2。忠诚只失去2不代表伤害只为2，移区后的printed SVar也不代表结算时上限。Soul Burn须分别核对基础2+B及X实际颜色Map，不按总黑mana3推断回血3。两精确原结果共享一局两施放；相同PW存储脚本arm按全文一致复用，独立黑X下游另断言；四重复BR是新格式，单补费用mesh与实际按钮，不重跑全部语言/目标族。依据[两原结果增量](../../../../.tools/stored-drain-1010/audit.json)。

2026-10-10真实顺序/主卡图路径：默认Specialize先mana再弃牌不覆盖ChooseCostOrder先色-弃-费；已弃原Bolt并花1尚欠1取消复现漏快照。仅产mana的合法退款floating/横置不假当回滚错误；比较资源数组要复制而非别名修改预期。Reveal原recipient按request.seat，不从陈旧canRespond猜。图中TryBattlefieldArt调用不证明primaryStack使用它；核对真实移区原ID及主Hand-Stack当前Sprite/缓存、Current-Original详情。95关联图/解码仅数据不是效果suite。实际Gut self生成器是SpellAbilityEffect.addSelfTrigger，旧CardFactoryUtil描述纠正保留原owner；精确非空玩家字段修正，未限定self和外部one-shot合同复用。中文pending提示与英文临时异能并存说明只断言含中文会漏fallback，应单核对新增异能原句三locale。依据[特化原范围闭合](../../../../.tools/specialize-tail-1010/audit.json)。

2026-10-10私有随机与成功前缀：随机候选核对每次<=3、各原非地/Library/owner、第二次排除已移除ID，未选候选可重复出现；不要把随机集合不相交当规则。暗置放逐的合法离区须看真实卡文，Pull from Eternity只指正面朝上，旧完整Heist/Donate/Disperse/双G以两红付款的成功前缀留FAIL，不为错误后段重跑；实际Processor费用与Cling新对象补生命周期。SelectCards的真实按钮是select:ordinal与confirm-cards，不能从card-ID对象身份推造card:或从其他picker推造ok；Unity按钮名SelectionAction_<原ordinal>，驱动找不到按钮保留已通过locale/mesh前缀只补确认尾段。依据[窃取原组](../../../../.tools/heist-tail-1010/audit.json)。

2026-10-10手牌放逐成本取消/文字费用合同：取消不只比对卡ID集合与区名，还核对原Hand顺序、mana池、实际产生并已消费的mana及尚欠费用；FullControl NoPaymentFromManaAbility产mana后须真实mana:G消费，不能把生成误记付款。旧mana符号高度证明不覆盖新增非mana长文字费用，按真实请求补固定字号的测量/mesh/标题和卡图分区、原按钮ray序号；原Spree+前缀保留。旧单席位UI投影不能假充双席位生产Poll，直接原投影复用与真实双view Poll分别记载；驱动封装FAIL及已经成功前缀保留，只补未验证尾段。完成包的共用检查负责人必须明确转交给仍消费的开放包，本次C02由E03转E02/R02.03，合同和旧证据不丢弃。依据[费用原范围闭合](../../../../.tools/cast-cost-closure-1010/audit.json)。

2026-10-10 额外mana/Flashback反例：不足总费时牌仍可出现在PP候选，原生可在目标之后的1B支付阶段不给Auto并允许cancel；不得仅凭能点击判成费用合法性错误。实际选项缺失与disabled字段都可能表达不可支付，按enabled原ID核对。Flashback3成本已结算/copy已结束牺牲后，清理弃牌4不是Behold3请求；停止点按原最低结果/真实END_OF_TURN/后继priority，不为未要求的下一主阶段额外动作重跑已成功原效果。观察器在mulligan后才建立，对早期无观察文件不制造数据；部分选择后的非法confirm使用实际更新序号的只读原选择/资源/源/优先权核对。依据[本批审计](../../../../.tools/behold-alt-flashback-1010/audit.json)。

2026-10-10 Sarkhan触发成本反例：可选触发Cost Behold的拒绝可以是选择器cancel，不保证先InputConfirm；必须按真实kind及source已经BF/AB栈身份判定。Token原生名可为Treasure Token，正确已结算末态不能因字面Treasure期待失败而重复施放；冻结原FAIL及request/observer，独立核对真实token种类、owner/controller、资源和后继优先权后只恢复已完成原结果。依据[本批审计](../../../../.tools/behold-trigger-1010/audit.json)。

2026-10-10 可选Behold精确目标反例：费用flag正确与结算条件正确，不证明施放目标门槛正确；Hulk未选Generic仍要求第三神器目标，按CR601.2c只修正明确正向条件的min/max。负向OptionalCost根异能可能给另一条Defined Targeted分支共享目标（Secret of Bloodbending），不能一概按条件不满足抹掉目标。连续自动支付可先耗尽下一次所需有色源；冻结已完成前缀，只补后续开局资源充分的尾段。区分原Opt施放U与反击结算4/2；Stack目标的真实kind为SelectStackTarget，沿原返回ID，勿猜InputSelectTargets。依据[本批审计](../../../../.tools/behold-generic-tail-1010/audit.json)。

2026-10-10精确费用反例：Harmonize的选power是dialog原choice ordinal，不保证ChooseNumber；手动mana响应须匹配剩余颜色，错误反复PayG不能证明付款卡死。已经横置的生物在拒绝下一次tap后仍横置，核对原pre状态，不写死false。第二次施放中断时保留原accepted成功prefix和已付未结算尝试，只補缺失decline结果尾段，不重做已完成accepted付款。两个独立Kicker1/2不是同一MULTIKICKER计数；礼赠承诺者与咒语目标可不同，核对原registeredPlayer及token实际owner/controller。依据[原四费用子边界](../../../../.tools/cast-cost-tail-1010/audit.json)。

2026-10-10轮序输入与条件模式：GenericChoice原select只切选中，需要等原confirm-cards才能完成；getNewChooser玩家列表可能是InputSelectEntitiesFromList，保留原playerID与ok确认，不能假定名称为ChoosePlayer或ID等于seat。四人轮序可用仅3人数bounds的隔离native夹具证明真实规则，生产四客户端/战场仍归原决定；NoSchemes仅模式专属producer时，完整原源/参数/操作/断言与期限转既有scheme生命周期PENDING，不以普通额外回合通过删去该结果义务。成功主流程冻结，驱动尾失败仅补尾。依据[六原生轮序与原边界闭合](../../../../.tools/turn-wheel-1010/audit.json)。

2026-10-10终局与成功前缀：完整原生终局和当前生产方法绑定分开记录。真实两席位完整payload→标准session/Poll/EndRoomMatch→个性化消息与客户端最终view/BO3可以核销该当前终局分支；一个NGO本机ID承接多个逻辑收件人不能证明不同远程clientID/UDP或四人生产。观察跨区后的牌要用game.getCardState定位当前实例；JsonUtility空嵌套对象权限检查内容而非只检查null。验证房间ID使用短且规范化后不同的值，先核对TryGetOrCreate返回身份；正常ID规范化导致的测试用例碰撞不改生产。完整wrapper后续夹具失败但前缀已通过时，冻结原FAIL/原输出，尾段读取真实前缀BO3结果，只补缺失尾段，不重跑成功付款。依据[当前终局绑定与尾段补证](../../../../.tools/terminal-binding-1010/audit.json)。

2026-10-10共享控制输入与驱动：存活被控制者不能沿用离场者物理权限；保留准确native input/future/原回调队列和已付资源，仅重发本人新wireID，并补继续/取消/新私有牌差异，不用强制stop替代归还。原生卡选项未必有嵌套card，以独立native对象ID核对；多选排除已选项直到原生确认可用；stale拒绝会重发同一当前请求，驱动直接处理交回ID后须标为已见，勿重复付款；mulligan早于fixture hook不等待不存在的observer；费用资源在准确支付完成节点核对，后续自然untap不算退款。成功前缀按原证据补断言，不因驱动尾段/manifest标签错再跑已付效果。依据[真实存活控制归还与驱动边界](../../../../.tools/control-return-1010/audit.json)。

2026-10-10席位投影与夹具竞态：活跃玩家集合用于资格筛选，固定注册集合用于Player→协议席位，离场不能把4序列化成3。已知同源字段先一次复现所有差异，再仅修负责映射；直接native属性/上下文只证明序列化，不授实际控制/投票/督战/替代/战役效果或四人UI通过。真实非操作方离场仍响应存活原pending；专门投影夹具主动覆盖pending做独立元数据探针时，不竞态补答被探针覆盖的旧pending，把此stale误报当驱动问题，勿改生产权限。当前仅6个表达式差异时，保留正常资格/响应守卫，不因运行包hash变化重做旧规则/语言矩阵。依据[六字段准确注册身份修复](../../../../.tools/seat-projection-1010/audit.json)。

2026-10-10目标ID与分层字段反例：player选项的原生ID不等于注册席位，实际目标断言必须取真实ID并核对堆叠playerSeat，不能以某个玩家未离场推定其防输触发。终局native payload没有Unity侧awaitingInput/inputPending时，不给native捏造字段；原生终局/具体outcome与Unity Poll清理分别验证。原支付/规则已完成而错层字段断言失败，保留原FAIL和trace，只补离线字段与相应层适配器，不为绿结果重做成功付款。适配器执行当前组件不充当4客户端/真实网络/EndRoomMatch全绑定。依据[本批准确目标与终局证据](../../../../.tools/loss-draw-native-1010/audit.json)。

2026-10-10多人离场与原生失败前缀：非操作方离场时，存活玩家原pending仍需原编号响应，不等待重复prompt；操作方离场要原等待释放，并核对后续原生过渡不再询问旧玩家。存活集合缩位不能充当注册席位。多次原生效果作用同一对象时核对准确旧基线→新增量，旧2→4是追加2，不因驱动误写绝对2而重做已成功付款。保留原FAIL和操作前缀，离线准确快照补错误断言；真正请求恢复修复另用零付款差异证明，不把这些适配器算付费机制/新首验。生产2与隔离4驱动改动分开，四人原生不证明生产四客户端或Unity布局。依据[持有者离场与请求恢复](../../../../.tools/royal-multiplayer-1010/audit.json)。

2026-10-10归档范围统计反例：开放排期删除完整闭合R47后，number/replacements/reveal仅存旧包originalPlanRow，原统计器误报未分配。来源归属检查须先校验closedPackages的原生/UI增量、实际付费结果与闭合审计哈希，再合并其原family身份；旧CSV字符串与新列表均规范化。不得为了消除此误报重新分派已完成验证或增加待办；新增无归属family仍须报警、篡改归档证据仍须失败。条件模式差异完整转入既有PENDING决定项，保留原操作/断言/来源，不能冒充支持通过。依据[统计器反例与负例](../../../../.tools/incubate-native-1010/planner-audit.json)。

# 验证证据与复用修改方法

2026-10-12 辅助牌 BO3 来源反例：Collectible 辅助牌离开辅助牌组后，当前区域不能代表原注册区域；不使用赌注的移除也须在开局捕获物理来源。主机按原 Main/Sideboard/Attractions/Contraptions 更新本场卡池，仅准确授权的剩余／赢得卡池可越过新构筑最低数量与同名限制，普通注册和客户端伪造仍拒绝。下一局必须核对生产 worker 中实际辅助数量，不能只看暂存 DTO。相同 NameCard 控件不证明候选来源完整，本批原生 CommonCards 候选漏掉 VariantCards 中的辅助牌，需补准确候选和原生有效性判定；原随机分支未改。未分配组别的初始装置会被引擎移入垃圾场，不能为错误的 Battlefield 期待重跑已完成禁用；保留游乐设施真实交换成功前缀，只补有效组别的装置尾段。归属交换的真实规则、终局、主机和零效果字段适配器分别记录，不捏造缺失终局。依据[辅助 BO3 增量](../../../../.tools/auxiliary-series-1012/audit.json)。

2026-10-11 奇偶与展示手牌反例：测试基线中同名牌可能同时在手牌和牌库，原对象须按操作的实际区域、owner及ID定位，不能只取第一个同名对象。可选展示的拒绝必须实际出现min0选择器；无合法候选自动跳过只能证明空候选，不能冒充拒绝。原生RevealCards的只读牌在previewCards，来源的ChosenEvenOdd在结算期间核对，入墓重置不算丢失选择；动态赋予异能核对currentAbilityText/hasAbilityChanges，currentRules仍可保留印刷正文。上下游效果与相关字段分别验证，驱动字段/起点错误保留原FAIL，只补缺失尾段。统一导入表也可能有语义错译，本批实际Ready的Odds被译成“赔率”；用项目精确词条覆盖，重建后核对三语言原按钮和响应ID，不泛化为整个语言系统已验证。依据[原两输入与记忆清理部分证据](../../../../.tools/selection-memory-flows-1011/audit.json)。

2026-10-11 批量移区共用夹具：请求操作席位有权让过，不代表该席位当前能施放法术；实际法术操作等其自己的主阶段，不能尝试在对手回合主阶段施放。原生modal问句字段为message，玩家目标ID取targetId而不是注册seat。开局忠诚和装备附着只是测试起点；实际费用扣除、消灭/牺牲的不同资格、全部对象与实际移区对象的记忆子效果、压印条件和清理必须由真实操作证明。完整固定问句覆盖半翻译参数，仅补该问句界面/三语言射线。成功结果按各原驱动冻结，驱动更新不重跑已通过效果。依据[批量结果审计](../../../../.tools/mass-results-1011/audit.json)。

2026-10-11 再生夹具反例：只读战斗观察取当前Combat并保护null，Card保存的战斗LKI不代替当前成员关系。Bridge的canRespond是可用行动元数据，提交权限按原request席位/启用ordinal及已冻结R02传输证明判断。Unity进入Play的回复可能早于实际进入，临时界面建立前检查Application.isPlaying；读取实际TMP子层级，不猜Symbols/SymbolRules路径。生成护盾来源用原牌名加生产者ID，完整意义留正文，最大字号英文也检查。已成功规则流程冻结后，展示修复只重放原请求；原观察器失败、字号溢出和临时重复UI均保留。依据[再生审计](../../../../.tools/regeneration-flows-1011/audit.json)。

2026-10-10 KeepName前状态与模式边界：工厂局部originalState不必等于实体印刷原牌；先找赋值来源，再用真实Clone改名后复制区分“本次效果前名字”和“实体原名”。条件注册使用既有RegisteredPlayer入口时，单列原生消费者结果，绝不授正式模式/Command UI通过；若核销复制结果最低门槛，必须在原A01入口待办保留完整原operation/missingAssertions、新owner、生产未支持状态与全部已有来源，不能只凭来源不存在关闭。有效末态已捕获而驱动最终过滤断言错误时，冻结原FAIL和完整trace再离线核对，勿重跑已成功流程。依据[本批闭合与完整转交](../../../../.tools/keep-name-1010/closure-audit.json)。

2026-10-10通知消费者减量：普通骰/硬币已有实际结果不覆盖同kind的不同原生正文格式。先核对真实通知调用者、受众及source ID，再只补缺失格式与共用解析回退；本批实际插队客ETB两接收者结果证明新格式，普通骰/coin解析适配器不计新效果。时空骰原生模式早退与真实模式支持分开，保留同一S04.02决定；无辅助牌库的真实roll不能核销亮灯/Visit。原游乐设施子边界完成后从remainingAssertions移除，历史missingAssertions以originalMissingAssertions保留，避免整包开放导致重复做已完成结果。依据[本批审计](../../../../.tools/auxiliary-result-1010/audit.json)。

2026-10-10 R47语言资源故障反例：立即中文/英文连续切换会在ReadCards前使旧请求过期，原失败断言不成立；保留原FAIL及25有效故障/恢复前缀，只补真实I/O错误日志触发新请求的19项差异。工具返回超时或结果RUNNING时核对实际Play/编译状态，确认已退出Play后才续尾段，保留中断记录。独占只读文件锁验证实际加载与搜索失败，不改翻译文件；冷缓存只是故障适配器。三语言字号相同不意味着面板高度必须相同，按实际字体和本地化内容记录两者。依据[本批审计](../../../../.tools/locale-fallback-1010/audit.json)。

2026-10-10 R39复制/换区核对反例：复制源的外部永久改写与复制例外分别核对，Kiki 的 AddKeywords Haste 可复制，独立 AtEOT 牺牲不随复制转移；Clone 回手恢复自身0/0和原ETB关键词，不要求内部关键词只剩显示出来的系命。详情按实际所在区域判定物理/复制基线，类型文字以实际本地化结果核对。抓牌按施放前准确牌库ID及手牌数量判断，普通回合抓牌单独扣除；结算后优先权可能先给非施放者，按实际请求席位立即消费，不等双方轮流后错过主阶段。LKI必须从原生 getChangeZoneLKIInfo 的已记录表查询，拒绝将普通旧Card引用或人工getLKICopy当作实际事件证据。本轮初次Play UI出现等待门槛失败，原FAIL保留，稳定后仅续局部展示，不据重试成功宣布启动时序已修复；A06仍保留该风险。依据[本批生命周期审计](../../../../.tools/perpetual-lifecycle-1010/closure-audit.json)。

2026-10-10 R39永久字段流程反例：开局摆放不等于真实入场，旅法需实际施放获取初始忠诚；生产者筛选条件（如Saga费用<=2）须先按完整脚本确认。静态异能列表还包含原有飞行等关键词产生的异能，按修改记录时间戳、受赠者ID及ReduceCost/MayPlay模式判断，不用总数写死。ChooseModes选中后提交confirm-modes，替代费用从已捕获的真实by paying选项识别；先按完整流程预算有色来源，避免自动支付耗尽黑色后又添加非必要来源重施。结束点设为必验结果恢复优先权，额外动作须有原待办断言；完整最低目标已完成而驱动额外步骤失败时，冻结原FAIL、独立逐字段/真实资源/公私/时间戳核对，只核销准确成功前缀，不改写整驱动PASS、不为删尾重新运行成功流程。依据[本批字段审计](../../../../.tools/perpetual-retention-1010/audit.json)。

2026-10-10 R38基线消费者核销反例：R38原六数字场景属于旧完整基线，没有新增552首验身份；其真实增量已完成，但生成器强制关闭包必须有新增nativeTargets，会迫使重复登记或重跑。关闭账增加nativeIncrements，按已登记job、regressionBatches审计成员、文件哈希、实际nativeFlows和付费操作结果校验；首验552/41保持。用本批三个实际原生增量及删证据、错job、错哈希、重复和仅源码证明反例检查；UI既有增量机制不变。原生与UI仍独立，未把驱动末态断言错误改写成整套PASS。依据[本批闭合审计](../../../../.tools/digital-identity-1010/closure-audit.json)。

2026-10-09 玩家指示物与混合选择反例：玩家席位不是原生实体ID（首席位对应player:0），从合法视图targetId读取；目标选择后提交实际ok，替代排序按OrderAbilities/selectedCount确认。原生增殖已选玩家但桥接selected=false，需分别核对selectedCount、每个实体的selected字段和实际窗口反馈；不能用数量正确代替可操作展示。增殖的cancel ID实际是Mine→ALL→Clear All循环，其中Mine只选我方永久物；按真实标签和结果判断，不能当取消。混合单卡窗口预留新增确认/重选和玩家按钮，三语言及偏好字号测量而不缩字。原生自然辐射在Main1，修正活动清单的upkeep字样并保留历史原文/证据。重放不同真实session必须分别创建生产Poll适配器；仅原生wire.views未经过Poll还没有awaitingInput，不能直接当可交互UI。已有成功的排序/中文范围按准确行冻结复用，只重试驱动错位及新布局；内部时间戳或Inactive/无实际生产者abort条件不授人工分支PASS，原R37/A01范围保留。依据[本批审计](../../../../.tools/player-counters-1009/audit.json)。

2026-10-09 R36核销反例：关闭事项前读取其引用的风险记录正文，逐条对照原operation/missingAssertions，不只看当前finish或重写后的state。受控付款/替代等待中的认输终局通过，不覆盖ChooseCard QuasiLibrarySearch可选拒绝后的控制归还；该记录的sourceQuestionOwner=A02.01也不等于把实际结果owner=R36.01移交。原记录未有完成证明时保留同编号待办、只核销已完成子范围；显式移交须保存新owner及原全部断言。本批撤回提前核销的55/81声明为54/81，原成功生产者与两个生产修复均保留、不重跑。依据[核销纠正与原风险](../../../../.tools/controlled-concession-1009/closure-review.json)。

2026-10-09 新Editor验证器导入反例：新建.cs后仅refresh_unity(scope=scripts,compile=request)返回成功不代表文件已被AssetDatabase导入；本批实际出现新类型不存在。新文件先做资产刷新（本环境force/all），确认类型已加载且无error CS，再进入Play并确认Application.isPlaying后运行；不要重复启动尚未导入的验证器。连体咒语的原生RightSplit与完整物理卡图分别断言，悬停不能因全卡图而混入未施放半张的正文；语言回退单独留R47。依据[本批审计](../../../../.tools/split-copy-1009/audit.json)。

2026-10-09 多状态复制驱动反例：扩展原生驱动前先从已捕获同合同请求核对option.card.id与提交ID：SelectCards用select:index，宣攻用attack:实体:目的地，attack-remove是撤销；泛型自动支付可能耗掉后续唯一有色来源，按完整流程预算或实际手动支付。阶段完成条件用准确ID/原生状态，不依赖可能漏传的currentRules；结算断言再独立检查伤害与实际触发抓牌。UI富文本先剥离标记核对可见数值/空名称，再单独验证颜色；文字原样Contains会误报2/1与空Room标题。失败驱动及其生产者冻结，已通过的历险/Prepare/Room不随合变驱动重跑。依据[本批审计](../../../../.tools/copy-state-remainder-1009/audit.json)。

2026-10-09 延迟条件与驱动阶段反例：同一原生实体选择须提交选中后的ok，不能连续点击同一选项来等待结束；对手手牌ID只能从其自身合法视图取，observer用于断言而非公开UI输入。黑色Shirei不能用Doom Blade构造离场，改为实际Cloudshift新时间戳，分别在触发前/结算前执行。强制攻击的正确结果还需原生拒绝空宣攻/错误目的地，不能只按测试意图攻击正确目标就声称约束生效。13成功流程保持各自producer，后补guard只补新断言；旧成功不为统一驱动重跑。依据[本批审计](../../../../.tools/delayed-lifecycle-1009/audit.json)。

2026-10-09 最小样本仍须覆盖准确payload：延迟触发复制的空remembered只能证明空值保持，不能核销“准确记忆对象”要求。按完成门槛补一个实际非空对象，检查复制先移除后原触发有效空操作；原成功的数值结算/UI目标合同保留不重跑。依据[非空记忆补充](../../../../.tools/delayed-trigger-copy-1009/remembered-audit.json)。

2026-10-09 延迟触发复制与已知牌：peek之后不能把对手hand投影一概断言为空；精确记录被允许窥视的卡ID，待其进入手牌后只允许该ID继续已知，同时断言未见牌与反向席位仍隐藏。原生observer可读全部区域仅作规则断言，不能混入公开协议/截图输入。第一次遗漏真实牺牲确认、第二次合法已知牌被过度断言均保留原FAIL；只补后续回合清理差异。复制的对象ID/triggerID/来源/控制者/记忆值与安排队列独立核对，同一次样本供R19/R20引用；本例记忆值为空，不扩张为非空对象继承已证明。依据[本批审计](../../../../.tools/delayed-trigger-copy-1009/audit.json)。

2026-10-09 Foundry重施与驱动误分流：原生确认提示包含完整来源异能描述，不能因其中含exile就把后续“Do you want to play”误分成先前可选放逐。按当前原生请求/操作阶段及最终问题区分；源牌ID、免费重施的原牌和追加费用单独核对。原生Opt已完成后驱动进入错误阶段，应复核已存完整轨迹/真实区域与资源，保留原FAIL，不重跑成功规则。UI同控件仍须检查实际新问题是否走自动缩字；本批只修精确来源问句，三语言原成功与中文措辞补验分开归档。依据[本批审计](../../../../.tools/foundry-recast-1009/audit.json)；不将单张原牌证明泛化AmountAll所有聚合/来源重新进场。

2026-10-09 不可逆事件与相同末态反例：R12.03中，祭坛实际牺牲三张荒疫巨像并分别公开/洗牌，随后Ogre/Ashiok将三张放逐，牌库最终与开局同为空序列。只比较牌库ID顺序会错误恢复已牺牲/放逐的牌；取消判断改为付款期间实际洗牌/牌库换区事件，临时监听在finally解除，普通展示手牌的可退款对照保留。新Game方法仅解除监听，全部既有字节码指令与同编译器的固定原源码相同。不要用随机排列改变作为发生事件的唯一证明。最初Millikin样本按当前CR605.1a正确不能在付款中激活，保留为不可达样本/驱动尝试，不能算产品失败；补测前检查原生异能分类及间接替代入口。依据[本批审计](../../../../.tools/library-event-1009/audit.json)及[官方规则变更](https://magic.wizards.com/en/news/announcements/the-hobbit-update-bulletin)。仅关闭准确R12原行，其他语义仍按原归属。

2026-10-09 完整包归档计数反例：R09最后一个证据事项完成后，直接删除开放包会把其四个原编号一起移出总量，误显示原事项总数减少。开放排期移除R09，但在remaining-work.archivedPackages和进度账closedPackages.concretePackage保存相同完整记录；生成器检查归档身份互斥、编号唯一、全部关闭及原证据哈希。81原编号保留，仅PENDING从42减至41；同一原生流程完成后发生驱动列表格式断言错误，直接复核已存快照/真实payingMana，不为修驱动重跑成功结果。依据`.tools/opal-commander-1009/verification.json`及`archive-check.json`，未把独立增量加入552/41首验计数。

2026-10-09 审查与结果归属反例：A02/A03/R49把未验证结果转给A01.01后，该项仍仅标审查，旧“整理期间不运行”排期又挡住已明确的增量，导致反复源码归类。原220身份落实消费者或明确缺口后，保留A01原编号/范围/全部转交断言，类型改为补证据且继续PENDING；用户继续goal后按具体运行条件执行。实际反例是Companion可选取消：生产Match开局返回null后原生移区崩溃，修复后接受/拒绝及普通开局三条真实流程通过。检查保留全部81事项/210共享输入、旧552/41首验身份和历史失败，未把源码归类算效果关闭。见 `.tools/mechanic-consumer-role-1009/result.json` 与 `.tools/companion-entry-1009/verification.json`；A06及Companion付费回手/UI仍开放。

2026-10-09 共享输入转交反例：A03.02 关闭源码审查后，Type/Player 揭示输入保留旧别名并新增转交别名，原归并脚本固定要求两个别名而拒绝合法第三个；静态转交表也把已关闭 A03.01 留作活动负责人。检查改为原别名/材料集合完整保留、原输入身份不增不减；活动负责人按当前 PENDING 状态筛选，历史 declaredOwners 仍保留。用本批两个真实三别名输入、210 个原身份、全部旧材料及已关闭负责人的过滤结果验证，不新增效果/UI 首验。依据 `.tools/shared-field-consumer-review-1009/registration-recovery.json` 和 `result.json`。

2026-10-08 辅助区域声明采集反例：首版只认行首 `Lights:`，漏掉 `Variant:A:Lights:` 等82条变体灯号；裸搜 `Junkyard` 又将两张卡名误当区域入口。登记前按字段位置修正，固定22份吸引力来源的83条灯号，并保留实际 functionalVariant→灯号身份。审查参数时包含脚本的嵌套变体前缀，名称/Oracle/注释命中不充当执行入口；同名变体不因此变成等量测试。原生 `lblAttractionRollResult` 与普通掷骰文案不同，选择同一结果控件前比较实际原消息格式，不能拿普通骰子动画证明吸引力结果可见。依据 `.tools/auxiliary-zone-consumer-review-1008/classification.json`；本批源码修正与归类，0规则/UI运行。

2026-10-08 施放身份与可变报告路径：51身份/9其他入口对齐41施放选择及126费用组合原结果，22身份有命名原生结果、14身份有旧堆叠样本是不同维度，不能相减当新增测试数。旧索引22个活动报告已被覆盖，冻结目录原件全部匹配原哈希及kind/count；保存逐份原路径→原哈希→冻结原件映射，不为修引用重跑对局或重写旧索引为当前PASS。AlternativeCost.Harmonize与追加横置的Harmonize共用实际流程；多个增幅/礼赠对象/调和仍保留具体断言，未证明的语义与冻结A01共用审查，四RevealChosen结果转移归属必须留下记录。依据`.tools/cast-entry-review-1008/classification.json`与`stack-sample-resolution.json`。

2026-10-08 Behold入口与旧索引去重：`CastChoicePaths`只在ModeChoices/CastChoiceFamilies两个旧目录查堆叠身份，37个无样本标签不能直接变37个待测效果；先补对后来费用与基线证据。20条普通Behold声明归6种入口，当前Caustic两流程与历史Theorist三结果原样引用。结算费用走CostReveal/payCostPart，施放费用走HumanCostDecision.BeholdInput；同一费用类并不保证相同用途字段或请求生命周期，复用前比较实际合同。7组明确缺失断言归既有R51.02/R12.01，20条payload保留A01审查；只关闭R51.04入口审查，不计效果/UI通过，也不宣称已得到全局最小运行数。依据`.tools/behold-entry-review-1008/classification.json`。

2026-10-08 消费者源码审查：先核对当前运行包的`ForgeRuntime/source/native-patches`覆盖，再引用固定上游；未打补丁的上游CostPayment不能说明当前退款范围。RevealChosen visitor直接返回不等于没有后续取消：抑制力场等外部RaiseCost可添加法术力步骤，全控制可调整付款顺序。53费用索引只映射到40解析类，区域/资源/秘密信息谓词仍需保留；分类不计效果PASS。R12.02节点审查完成，实际恢复与结果差异明确交R12.04/R51.01，不因关闭审查丢掉待补证明。依据`.tools/scope-classification-1008/classification.json`。

2026-10-08 普通Behold驱动反例：产出法术力、从池中实际支付、取消时退款、撤销合法产费异能是四个不同节点。NoPaymentFromManaAbility下手动产生的法术力可在取消后保留为浮费，地仍横置不等于资源丢失；先核对池、已付资源与原生追踪，再依据CR733.1判定是否需要修复。不要为了通过过严测试把合法保留浮费改成强制撤销。普通Behold会展示所选战场卡，隐私断言禁止的是未选手牌。两实际单张手牌/战场流程611检查，现有UI合同与生产字节相同直接复用。旧42原语表只扫描直接Cost/UnlessCost/RaiseCost；审查类型整体范围时也要核对K:生成入口，本批补出20条普通Behold来源，不按卡数重复测试。依据 `.tools/plain-behold-boundary-1008/result.json`。

2026-10-08 BeholdExile反例：CostBeholdExile虽派生自CostReveal，实际同时展示并放逐资源，不能据普通展示的无资源变化推断退款。限定准确类型加入局部事务后，还要核对手牌原排列，不能只检查牌回来；只有这个新增费用填充手牌顺序日志，其他旧消费者保留空日志。两真实手牌/战场路径849首验保留058888db身份，新增用途字段后只补受影响流程481与普通Behold请求取消43，不为版本统一重跑所有成功规则。新增内部类会改变其他nest成员哈希；先复编译冻结旧源码并匹配原字节，再比较方法指令，不能凭哈希变化自动扩大回归。当前只Behold visitor和publishInput改变，旧内部方法相同。依据 `.tools/behold-exile-1008/result.json` 与 `runtime-context-delta.json`。

2026-10-08 展示费用反例：普通 CostReveal 与牺牲混合时，原白名单拒绝整个局部快照，先牺牲再取消后续付款会丢失来源。仅准确普通类型且不含 Library 可纳入原有资源事务，不按继承关系自动放行 Behold/BeholdExile。退款比较资源与已公开知识分别进行：已展示手牌的身份应保持已知，未展示牌仍隐藏。三条实际流程1121项和嵌套Ogre/Ashiok牌库放逐不可逆边界另2项mana捕获断言通过；其他费用判断和恢复源码相同的旧消费者引用原证据。对手只知2/4手牌时使用授权子集列表，不能因数量少就假设整手直选。初次来源离场卡牌/取消射线已通过，只重试误断言之后的对手列表13项；保留原FAIL和成功生产版本。依据 `.tools/reveal-cost-rollback-1008/result.json`。

2026-10-08 多堆叠/复制恢复补验：实际 Opt→Shock 与 Opt→Twincast 两流程验证原目标、原堆叠实例顺序和复制标志取消后恢复，再分别依序结算。SelectStackTarget 先选再提交原生 ok；重复点已选对象会取消选择。咒语复制品离开堆叠后由原生状态动作消失，放逐付款的中间断言与普通牌分开，不能要求它长期停留放逐区。初次 UI 已完成普通三个选项，但复制品固定后缀导致英文回退；保留这三个实际射线步骤，只补受影响复制品分支三语言114项。509项规则与双方完整堆叠投影4项等值检查分别记账；运行包未变，不重跑812项历史回归。依据 `.tools/stack-target-restore-1008/result.json`。

2026-10-08 堆叠费用取消反例：CostExileFromStack 独立继承 CostPart，不能由普通放逐类退款推断覆盖。Synthesis Pod 实际放逐 Opt 后取消先复现丢牌；只加入局部快照白名单仍失败，因为 GameSnapshot 的重新 add 会冻结原咒语并重复施放计数，随后取消清理删除它。限定该事务保存原堆叠实例/顺序，资源快照不重新复制堆叠；准确比较取消前后实例 ID、区域、资源和本回合/本局历史，并分别让原咒语正常结算及重试费用，不能仅以卡重新显示算恢复。两条规则442项和既有历史812项回归通过；原成功驱动冻结，追加结局/命名分发不强制重跑旧通过。占卜初选与确认分别使用实际 scry-top/confirm-scry ID，原测试字段/协议错误保留。已有 MagicStack 方法字节码一致和两文件运行差异单独审查，不扩大为所有堆叠历史通过。依据 `.tools/stack-exile-cost-1008/result.json`。

2026-10-07 多段成本反例：先证明卡已实际进入坟墓场/放逐/手牌，再在后续节点取消；初始选牌取消不足以发现默认空refund造成的丢牌。付款局部快照还需检查准确区域顺序和原生源牌返回位置，不启用全局实验开关。生命付款后原生必选继续与缺少取消UI分开记录；涉及牌库的不可逆信息保留边界，按当前CR分类，不能用Chromatic Sphere的旧法术力异能假设构造样本。嵌套替代请求等待真正输入，跳过已回答requestId，不对暂存外层输入重复提交；成本提示按最后实际指令区分，不按含全成本的来源描述猜节点。七条成功/相关回归/UI保持855原身份，最终牌库守卫只补不可逆场景和普通回归；费用排序裸符号/英文/裁切虽点击通过仍归R47。新增counter断言误放未执行分支时，原PASS不授予counter证明；先读已存实际快照，确认真实丢失即可修复，不为同一反例再启动引擎。原生快照不会自动恢复全部属性，指示物另存准确multiset；已有堆叠/延迟与指示物变化只跑四个相关场景。聚合的永久证据使用冻结调用/版本路径，live run-summary仍只供下一次运行器读取；路径迁移逐份验证原字节哈希，保留原聚合。见Docs/MultipartCostConsumerAudit-2026-10-07.zh-CN.md及MultipartCostStateAudit-2026-10-07.zh-CN.md。

2026-10-07 R01起手补验：首个优先权实际是UPKEEP，不把驱动的MAIN1预期当规则缺陷。London cancel合法ID对应Auto，先读原生onCancel和按钮label：它按手牌顺序补选并立即返回，不是取消/清空；注册合同允许真实cancel且核对Auto语义。晚发现该分支只追加原生/主语言射线，不重跑四个成功场景，分别保留驱动身份。测试默认场景尚未生成controller时沿用已有创建fallback；射线断言与同帧清屏后才渲染的截图分开，后者不计视觉证据。真实大厅config分别传生产会话，Dispose后旧输入拒绝/新会话初始手牌正确；旧三语言及网络引用保持历史版本，见Docs/OpeningBoundaryConsumerAudit-2026-10-07.zh-CN.md。

2026-10-07 R46搜索驱动反例：直接摆放零忠诚旅法师会被状态动作移除，限制来源须在开局优先权确认实际存在；单张公开来源可能由原生自动选择，不能据缺少picker判为未接入。UI授权由service传入enabledInput，受控制玩家与本地席位不同，不能向无权限上下文传true后要求localSeat自动拒绝。手牌的本地暂选/确定与后续原生ok/cancel/reset属于不同步骤，三操作时确认沿用右下advanceButton；按实际入口取按钮，不猜名称。保留已完成部分格的原整套FAIL和生产源码身份，只重试剩余格。见Docs/SearchBoundaryConsumerAudit-2026-10-07.zh-CN.md；这些修正限定测试驱动，不添加破坏控制回合的产品seat限制。

2026-10-06 StoreSVar结构反例：临时只扫描AB/DB得到65张/140行，遗漏三个SP根咒语和Count/Targeted分支。已复用覆盖工具的AB/SP/DB语法重扫得到68张/146行/七类计算，初次输入保留非权威身份。选规则样本前同时核对根咒语与子链；源码/API归族不计效果PASS。空间选择、领奖和翻牌按原生消费者判断复用，不因名称预设小游戏，仍保留真实分配/生命周期及模拟方式差异。见 Docs/RemainingApiConsumerAudit-2026-10-06.zh-CN.md。

2026-10-06 强度刷新反例：引擎getIntensity有效，但卡牌协议没有当前值，普通规则文本不能代替有效特征投影。新增类型化字段并核对原生值、暗置过滤、原始切换及分组键。UI驱动原本每次清空Present并选择不同同名ID，虽数值通过却不能证明同ID刷新；改为连续更新同一原生ID，在三语言检查3→4→5及实际蓝色TMP字符后才记录该断言。旧PASS保留原范围，新证明独立归档。见 Docs/StateBranchConsumerAudit-2026-10-06.zh-CN.md。

2026-10-06 费用组合反例：单独核对非瑞／三定不能覆盖双色择一泛用的奇数减费、AI策略挡人类最后生命／寇瑞克、献祭／化生意图被自动支付预览清除，以及非生命资源已横置后取消不恢复。新增 `cost-interactions` 与 `cost-optional-combinations`：预期费用独立声明，读取原生堆叠实际支付资源与结算，不以尚存地的横置数代替被牺牲／返回前的支付。取消必须实际走到部分资源已经付出的节点；仅在初始付款窗口取消会漏项。预览用复制意图，不污染实际牺牲／横置对象；恢复限定原生付款实际记录的身份。多个同色及异色可选追加减色费用分别采样，不能凭预测只尝试一项就猜测组合不能支付。源码／夹具／运行包哈希在启动固定，每次尝试独立保存，修复后重跑受影响矩阵；构建便携Java之前结束本任务测试进程，避免DLL占用。解析器token和卡牌样本清单保留为结构证据，不计效果PASS。详见 Docs/CostInteractionAudit-2026-10-06.zh-CN.md。

2026-10-05 X 行后续反馈：用户指出插入带残线、字号过小；复核发现底色纵向采样和原正文裁片带入边框，费用覆盖位置还偏高，XX 旧图标下缘未完全遮盖。修正空白底色采样、正文起点、X 可读下限和费用位置。后续用户认为14px过大，改为约11px、独立检查X与正文间距，原批次保留。本轮只重放单 X 加费、双 X 加一费、天空之怒的三语言堆叠／悬停／浏览，不重复原生七场已通过结算。双 X 加一的真实⑦／X=3／7点付款证据直接引用上一轮 trace；结构标签正确不代表底色和遮盖正确，必须检查实际截图。

2026-10-05 X 卡面后续修订：用户拒绝把普通 X 咒语套进 Omen 重建牌面，并要求宣布 X 与加减后的施放费用各自显示。旧 source.manaCost 只替换 X，不含外部费用修正；新增施放付款边界的原生快照字段，不能在堆叠重算（减费来源可能已离场），也不能用累计耗蓝量推算（会混入反击补交等效果费用）。原真实 XX 批次和旧图不覆盖；新七场费用／结算证据与当前 UI 分开归档到 x-stack-insert-1005。截图发现副牌面富文本标签被按字面绘制，说明结构检查不能替代视觉检查；改为独立 X 标签，保留中断批次。参见 XSpellPrintedCostAudit-2026-10-05。

2026-10-05 X 牌面局部复核：统一 Omen／普通 X 的右上费用图标，修正副牌面译文；三语言切换暴露了移除子节点时跳项的真实缓存残留，保留首轮失败并对活动费用控件唯一性断言。双 X 的实际 0／2／7 分开验证费用、支付与状态动作；大 X 首次失败是测试只点五张山脉，修正为原生合法资源后通过，未改规则。扩展堆叠列表仍只算布局适配器。见 XSpellFaceAudit-2026-10-05 审计，不重跑无关机制或恢复暂停 goal。

## 现有证据入口

2026-10-06 同墓地回归首场出现Windows证据文件已存在但写入尚未关闭的PermissionError，之后六场通过。读取观察器证据时在原期限内同时等待可打开和完整JSON，仍核对原请求编号；超时保留失败，不能把不存在／不完整记录替换为预期数据。修正只在测试驱动，保留原失败和新七场重跑，不把总指纹失效误当游戏规则全部重新失败。

截至 2026-10-04，最新完整批次：

| 项目 | 记录 |
|---|---|
| 固定 Forge | `4ec5f1a2c32fa90ecb983a72b9eb47aa5c5d7676` |
| 引擎 | `20261004T000513050100Z`，30 任务；210 实际规则/起手场景，10 适配器检查 |
| Unity | `20261004T0037163401098Z`，22 套；691 张已解码 1560×898 截图，22 组输入归档哈希 |
| 来源指纹 | `79d42df98522521fe09576ae70d9fa870bcf47e5641c162f318b558ea96fff8a` |
| 清单 | 121 条有列出场景证据，2,757 条未登记；25 共用流程 |

这是有版本的历史基线，使用时核对当前报告与来源；不是全卡池通过率。生成脚本直接读取最新清单，不从本表推算当前状态。

具体断言在 `Tools/ForgeBridge/MechanicCoverage.json` 和 `PresentationFamilies.json`；当前报告为 `Tools/Validation/Reports/MechanicCoverage/{coverage,families,interaction-contract}.json`。原始证据在 `.tools/mechanic-coverage/runs/<id>/` 与 `unity/<id>/`，汇总证明 `.tools/combat-allocation-final-verification.json`。运行包逐文件核对支持运行素材一致性，不能当作效果通过。

阅读具体改动时选择对应审计文档：

- `Docs/MechanicInteractionAudit-2026-10-03.zh-CN.md`：交互接口、控制玩家、起手、名称、数字生成和两堆。
- `Docs/LibraryCostBoundaryAudit-2026-10-04.zh-CN.md`：多阶段原生费用、取消与基础接口。
- `Docs/ReplacementBoundaryAudit-2026-10-04.zh-CN.md`：替代来源、顺序与同帧销毁控件。
- `Docs/DamageDrawBoundaryAudit-2026-10-04.zh-CN.md`：伤害/预防/抓牌、可选替代和来源选择。
- `Docs/CombatAllocationBoundaryAudit-2026-10-04.zh-CN.md`：原生伤害分配、威慑拒绝后输入恢复和叠放展开。
- `Docs/RandomResultInteractionAudit-2026-10-04.zh-CN.md` 与 `RandomBoundaryAudit-2026-10-04.zh-CN.md`：随机通知、付款取消、限次与持续期。
- `Docs/ArenaReferenceAudit-2026-10-03.zh-CN.md` 与 `ArenaReferenceAudit-2026-10-03-0816.zh-CN.md`：录屏观察和时间点。观察参考与真实操作验证分别记录。

失败、主动中断、过时截图、原始异常保持原始状态。已结束归档可透明压缩或校验后搬移，保留可追溯路径与哈希；输出应写入未占满的磁盘。存储操作不会增加规则覆盖。

## 复用判断

盘点受限法术力及花费附带效果时，可运行 `python -X utf8 Tools/ForgeBridge/ClassifyManaConsumers.py`。它按当前 `AbilityManaPart` 的九个限制/附带/保留字段归类 AB/SP/DB Mana/ManaReflected 声明，保存原谓词、牌面索引及变量声明行；每个牌面使用自己的本地 SVars，不将双面牌同名变量相互覆盖。同名 `TrigSpent` 不等于相同效果。输出只供 R09 与 A01/A03/A04 共用审查，不证明可达性、所有产费表达式/替代/持续效应或结算通过。新增测试由具体缺少的原生断言决定。

复核“所有施放前选择”时，运行 `python -X utf8 Tools/ForgeBridge/Audit-CastChoicePaths.py` 读取固定来源包的可选／替代费用身份与额外关键词分支，检查 `Tools/Validation/Reports/CastChoicePaths/`。这只是结构／样本盘点；施放面、融合、试作、面朝下、其他效果施放、外部追加／减费以及宣布变量必须另外保留，不因枚举有样本就关闭全部效果或界面边界。

按原生请求合同复用，比较以下内容：

1. 谁操作、谁可见；卡/玩家/堆叠对象的准确编号。
2. 原生候选、最少/最多数量、顺序或分配用途。
3. 可取消/可选/强制、确认门槛与无合法候选的行为。
4. 消耗哪类资源、是否部分支付，以及取消/错误后的下一输入。
5. 结果涉及区域、持续期、复制/衍生物、附着和控制权。

上述相同时复用协议与控件，保留每个不同规则语义的实际引擎断言。比如召集/拼造/截水共用选牌UI，但合法资源、颜色支付和取消需要不同场景；抓牌/伤害替代共用来源与顺序UI，但伤害和抽牌结果各自断言。普通战斗不能归到未实际调用的 `API:DealDamage`。

只增加原生已提供、客户端确实需要的类型化字段。Unity 依原生合法选项启用按钮，不根据英文提示猜费用/合法性。复用选择、目标、排序、分配、揭示、通知及战场状态组件；专属空间/区域/生命周期先列出与共用合同的差异，再决定新控件。

排查对局阻塞时保存失败请求、响应、原生当前输入、版本/consumed状态和下一优先权。修复定位后的分支，例如威慑拒绝只重发同一 `InputBlock`；其他输入先用真实反例确认，不广泛增加重发。

## 增量回归决策

运行前采用 SKILL.md「选择回归，避免重复」的五步减量判断。网络证据复用按实际传输/权限/恢复路径及输入合同核对，保留控制、部分支付、隐藏信息等差异；源码分类和复用依据不能伪装成新增实际PASS。用现有批次记录集中登记，未改变来源的文档维护只检查一致性。

| 变化 | 选择 | 旧证据处理 |
|---|---|---|
| 仅文档/skill/盘点 | 文档链接、脚本、清单一致性 | 效果证据不重跑 |
| 新夹具或测试断言 | 新/改任务和直接输入消费展示套 | 未改效果保持历史批次身份 |
| 单个面板布局/字体/文案 | 该面板真实点击、溢出和受影响语言/字号 | 无引擎变化时沿用原生输入归档 |
| 特定桥接输入分支 | 该输入所有已登记消费者及适用取消/非法/空池边界 | 其他分支记录依赖判断 |
| 共用协议字段/序列化/操作权限 | 实际消费者套、双方/非操作/重复/旧请求及隐藏信息 | 未查清依赖列为待复核 |
| 卡牌脚本/上游规则/运行包变化 | 受影响规则和请求消费者；升级或影响未知时全量 | 按新版本失效，重新产生证据 |
| 核心请求队列/路由或阶段收尾 | 全量登记任务/展示及包来源核对 | 保留新完整基线和失败 |

### 当前工具的限制

`MechanicCoverage.py:inventory/build_report/run_jobs` 将规则脚本、运行二进制、大量测试、全部 C# 与登记文件合成总指纹。`PresentationCoverage.py:build` 也要求相同指纹和输入哈希。**部分运行只记录选中任务，不继承其他任务的历史 PASS。** 这会扩大失效范围，但不是每次局部修改都需要立即全量重跑的理由。

当前选择性引擎命令确实存在：

```powershell
python -X utf8 Tools/ForgeBridge/MechanicCoverage.py --run --jobs <已登记任务ID>
```

执行前保存旧 `latest.json`、`unity-latest.json` 对应归档入口。它们是最新运行指针，不是全部历史的合并账本。未选任务保持“本轮未运行/历史通过”，不要篡改自动报告。UI按 `PresentationFamilies.json` 的具体 `type` 调用现有验证入口并单独存结果；当前批量工具是否支持筛选先读实现，不编造选择参数。若仅能单套运行，单套结果保持单套身份。

自动逐任务依赖计算与证据合并尚未实现。A06先完成可审计的逐批影响记录，不要求为结束机制验证先开发自动化平台。手工记录与未来工具均至少保存：

```text
任务ID；caseID；断言范围；运行版本；规则/二进制依赖；桥接/协议/消费者依赖；
测试实现/配置哈希；原生输入归档与哈希；语言/字号/分辨率/资源；
结果及原始路径；新增/重测原因；当前/历史/待复核状态。
```

依赖遗漏或未知时使用保守的相关范围；源码全局指纹相同不证明资源全相同。字体、翻译、布局、卡图变化重新检查受影响展示。跨版本复用需可审计的依赖与原始证据，不直接将旧 PASS 改为当前通过。

## 每轮停止条件与进度

2026-10-07 分堆反例：原生 envelope 的当前 request 与 seat view 可分开归档，重放必须只给实际接收者组合两份原输入并保留各自哈希；不能把 awaitingInput=true 的缺请求快照当产品 NullReference。工具 Run 返回错误仍可能调度过代码，先查当前 Output、Running 和结果文件，已完成的结果不得因报错重跑。首次可操作截图仍需查看，字段/字形生成通过不代替首帧可见；本次标题和数量旧行高小于 preferredHeight，只补三语言受影响行高回归。生命周期字段必须读准确 DTO 键，不能以不存在字段的默认空数组证明清理；Raging River 原轨迹 effectMarkers 战斗时2/战后0追加冻结输入审计，未来驱动修正，不重跑成功流程。依据 Docs/PileBoundaryConsumerAudit-2026-10-07.zh-CN.md。

同批运行入口核对：人工合并 aggregate.json 是历史生产者集合，不是未来每次启动的新结果。R46/R43 驱动已写独立 run-summary，但登记仍指向 aggregate；运行器的新鲜文件门槛会拒绝后续合法重跑。仅修 structuredResult 为 run-summary.json，抽取实际 main 在隔离目录检查整套、部分、失败六格；没有运行 Java/Unity，不增加效果首验。登记新任务时逐项核对输出文件名、字典状态与每次启动写入，保留历史 aggregate 及原生产身份。依据 .tools/pile-boundaries-1007/runner-contract.json。

2026-10-07 R16：先查实际调用再把参数当卡牌机制。固定上游manipulateCardList的规则路径来自scry且toAnywhere=false，MTGB又覆盖scry/surveil；insertInList是阻挡顺序，不能拿它的适配器通过证明牌库插入。当前可达排序/双区域七真实流程验证，GUI-only外部/动态适配器审查转A05保留，不授予虚构效果PASS。历史对手底牌排序的非接收视图request为空是正常隐私；从原envelope实际请求和只读原库/终库追加精确底二断言，不重跑旧成功。标题实际44.01227px超过38px，仅OrderCards测高并扩提示，三语言排序序列和相关底牌/耗竭选择回归；英文按钮仍中文归R47，不把排版通过当翻译完成。Run报错后已检查终态PASS，未重复启动。依据 Docs/LibraryOrderConsumerAudit-2026-10-07.zh-CN.md。

选定缺口的真实结算、结束状态与涉及UI达到列出断言，相关回归通过后停止扩大测试。新的变更或失败才触发继续重测。语言切换等待资源就绪，首次呈现/字体过渡另列断言；结算重放不是完整动画或实时网络证明。

工作包只有全部列出的边界都有证据或有明确范围决定时才能关闭；范围决定单列，不称 PASS。报告显示本轮新增规则语义、关闭/新增包、重复回归及其理由、尚未分类量。多个包共用同一证据不重复增加场景数。发现新的调用/机制分支及时扩展计划，完成已知包不自动完成全卡池goal。

## 已确认的原生恢复与分类反例

| 事实／误判 | 修正与已保存证据 |
|---|---|
| 原生拒绝威慑阻挡后没有下一输入 | 仅刷新同一 InputBlock；保存拒绝请求与后续原生确认，见 CombatAllocationBoundaryAudit |
| 池颜色虽被提供，实际不能支付当前费用 | consumed 后仍停在同一 InputPayMana 时，刷新未变化输入；资源／费用／候选不变、新版本及正确支付恢复分别断言，见 ManaPoolAnimationAudit |
| 录像的费用顺序被单帧误读 | 荣光竞技场先选颜色再横置生物；实际流程跟随 Forge 请求，不硬编码习惯顺序，见 ArenaReferenceAudit-2026-10-04-1016 |
| 中文耗竭与竭绝混归 | Exert 的下次重置与 Exhaust 的限次起动分开，R50 仍保留未验证效果 |
| 费用身份枚举被当全部施放路径 | 保留枚举外路径和无样本；25 个新增实际场景不能关闭 R51，见 CastChoicePathAudit |

Unity 的字体首帧、插画构图、窗口恢复、选定模式、手牌权限、图标和动画反例按消费者整理在 [ui-reuse.md](ui-reuse.md)，只加载本次相关内容；原审计文件保留具体失败与结果。

## skill 与 goal 的维护

- 2026-10-06 同时触发／章节：新增七条真实规则、两套三语言 UI，核销 R18 已完成的具体范围。预览缺控制字段，按来源编号绑定请求方战场，在实际堆叠核对控制者；不能从 ownerSeat 或不存在的字段推断控制。排序选项按 order: 身份筛选，JsonUtility 对省略嵌套对象可能补出空实例。UI 图片遵循既有语言／偏好，而非强制原印次；手牌按钮可查看详情不等于允许提交，权限用真实点击与提交结果分开断言。首次驱动遗漏 FromList、初次 UI 误判图片／空对象／只读可查看的失败保留；修正后七条规则与两套 UI 通过，相关替代排序／目标消费者仅计回归。依据 Docs/TriggerChapterConsumerAudit-2026-10-06.zh-CN.md。

- 2026-10-06 工作量进一步整理：语言/资源与依赖审计仍有独立排期，入口历史62包/暂停与最新61包/active相冲突。合并为8个执行批次，保留61条开放边界和6类共用检查的唯一归属；语言/资源检查及A06记录随实际消费者执行。A01–A05引用同一次条目/消费者/合同审查，R49只接新发现无归属项；独立断言可共享真实流程，组合按相关分支取样，已知交互/失败扩大。特殊包先审查可达性和复用差异，不预设开发新UI；未决支持范围仍开放。刷新清单发现新Test-StateBranches未登记，三场独立PASS不加入234/19登记首验，也不关闭S03；记录到A01/S03。原工作量复核有冻结哈希，保留原文件另写补充；入口注明历史身份。本次不执行规则/UI测试，检查来源、排期、证据哈希及skill格式。

- 2026-10-06 Discover消费者复核：九条原生已通过的场景没有再次执行；八条找到牌请求比较完整picker合同，UI按Cast/Hand三语言及第二席位/再次请求/双方揭示差异取样。首次驱动把CanvasGroup隐藏误判为未隐藏，改查alpha/交互/射线及隐藏期间提交；新增本地化后发现当前字号下提示约62px超出58px，按实际高度扩展对应窗口而不缩字。中断通过实际Play=false/缺失handle确认后才重试，原失败/中断目录保留。最终一套UI通过，Empower四场独立回归；Discover/Empower/BlankLine消费者归类，其中BlankLine仅排版，不计效果PASS。关闭原R48并保存原行/规则/UI/审计哈希；费用/控制/未采样长文本/联网仍开放。A02/A06只核销本批记录，参见 Docs/DiscoverConsumerAudit-2026-10-06.zh-CN.md。

- 2026-10-06 再次整理工作量：新规则沿用同一请求合同却要求每个case都重复UI点击，会把规则样本数再次放大为展示工作；goal还把自动依赖工具列成完成门槛。改为按真实请求合同/格式路径引用已有点击证据，差异合同补代表输入，独立规则语义继续逐项断言；A06保留可审计的逐批依赖记录，自动失效/合并工具移出必做范围。Discover已有9条原生归档补进首验账，UI尚未完成，不关闭R48；系统goal当前active，修正文档旧paused说明。本轮只做规划、证据哈希和生成器检查，不运行规则或Unity效果。

- 2026-10-06 工作量整理：62条开放边界分别包含重复权限/取消/文本等检查，旧排期只口头说合并，缺少唯一归属；A01还保留已过时716数量。增加 execution-plan.json：全部开放边界恰一次归入12批次，6类共用检查指定负责批次和复用/补验条件；生成器拒绝漏项/重复/未知归属。清单按消费者/原生语义归类，已有费用矩阵只按新增影响回归；真实网络、资源取消语义、隐藏身份和生命周期保留独立证明。A01数量改读自动清单。goal缩短历史复述、指向原账本和审计；225/18/3的证据与全局失效状态保持原身份。本轮仅检查规划工具、链接、证据哈希和skill格式，未运行规则/UI，未完成A06或恢复goal。

- 2026-10-05 结算选项与牌库顶：以12个实际规则场景和1个同ID洗牌适配器检查分开记录；共用展示不核销其他隐藏区域或观战记忆。只对本轮请求做三语言射线、权限、只读、字号与已知顶牌清除检查，旧支付规则保留原批次。新增 X 堆叠文字／能量图标与渲染帧检查。工具超时重入造成驱动相互覆盖，增加运行中保护，原失败保留，依据见 ResolutionChoiceAudit-2026-10-05.zh-CN.md。

- 2026-10-05 支付界面复用：施放费用与结算额外付款共用 InputPayManaOfCostPayment，不能按类名让 AUTO 跳过后者。以真实 Mana Leak 自动／取消／不足／手动四种结算和 Hallowed Fountain 是／否独立规则结果验证；Unity 只重放本次请求并补跑手动池消费者。初版按钮与 AUTO 重叠、驱动未同步 service 请求的失败原样保存，修正与复核见 Docs/PaymentDockAudit-2026-10-05.zh-CN.md；新 UI 套不核销全部费用机制。

- 2026-10-05 用户授权淘汰过时验证结果：历史资料不再一律保留。保护当前完整基线／近期增量／进度账本原证据及失败，旧全 PASS 且任务被后续基线包含的批次可删除大文件并保留版本摘要。筛选出 12 批约 10.14 GiB，无当前账本／机器报告引用；执行删除仍被工具 blocked by policy 拒绝，清单状态保持 blocked、释放 0，见 Docs/OldValidationRetirement-2026-10-05.zh-CN.md。

- 2026-10-05 用户明确要求“只验证指示物”：将本轮尺寸验证限制到双方符号网格、原数字排版和偏好刷新；不因使用 skill 自动刷新全机制清单或跑其他 UI 套。相同 Rect 未保证视觉等大，新增 PlayerCounterSymbolFit 以实际网格最长边归一，原比例保留。缓存清理只校验删除范围、前后占用与保留文件，不触发效果回归。

- 2026-10-05 本地占用实测包含隐藏目录、跳过 junction 目标：卡图约 20.73 GiB，但根目录约 61.34 GiB，.tools 验证／工具资料约 28.07 GiB；不能把卡图目录大小当全项目大小。缓存删除自动审查拒绝，未释放空间，保留计划与限制见 Docs/WorkspaceStorage-2026-10-05.zh-CN.md。新增 workspace-maintenance 引用，不把证据档案当缓存，也不关闭正在运行的编辑器。

- 2026-10-05 用户纠正“经验图标上沿”被误判为数字重叠：撤回整行数字右移，保留原用户排版，检查完整源单元与内缩矢量网格／截图。实际运行又发现能量的已缩放字号被 PreferenceText 当作基准，27 并非预期 21；改成 SetBaseSize(16) 后跨扫描／三语言验证。旧批次与首轮失败保留，局部纠正不增加规则 PASS，见 PlayerCounterIconCorrection 审计及 ui-reuse。

- 2026-10-04 用户再次指出工作量应减少：只报 65 开放包会隐藏包内完成量。新增 verified-progress.json 并检查去重 case／原证据哈希，核销 43 个规则首验、8 套 UI 首验以及五个包的对应范围；保留原始边界与具体剩余。首次验证进度不改写总指纹失效状态，不充当 A06 当前依赖账本。
- 用户指出重复回归后，新增影响范围决策并登记 A06；它仍是开放工具包，不意味着已经支持自动合并增量 PASS。
- 原计划保存全部原始边界及结构条目。新边界、缺失接口与未归族 API 的模拟检查要求来源复核；开放队列关闭前须保存完整行与证据到审计文档。不得为减少剩余数直接删掉未知项。
- 2026-10-04 整理发现剩余 Markdown 仍写 28 套 UI，而登记已有 30 套。刷新清单再生成报告，数值读取当前文件；将固定生成日期改为实际日期。计划来源哈希一致只说明审查输入没变，不证明语义齐全。
- 修订目标与验收保存于 `Docs/MechanicValidationGoal.zh-CN.md`；系统 goal 当前 paused，工具不能重写 objective。文档、盘点与 UI 修复不会自动恢复长期 goal。65 开放包保留排期身份，不承诺全卡池全部通过或精确剩余轮数。
- 入口按任务路由：长期接续读 goal；规则／桥接读本文件；展示读 ui-reuse。仅文档整理验证链接、脚本与 skill 格式，不重复运行规则／Unity。

格式检查用 skill-creator 的 quick_validate.py；若环境没有 PyYAML，沿用隔离目录 `.tools/skill-validation-deps`，不修改项目运行依赖。格式通过不代替行为验证。此次图服务 list_projects／check_index_coverage 返回 Transport closed，读取准确源码回退。

- 2026-10-06 StoreSVar七类消费者：原生卡换区会重建对象并恢复印刷SVar，不能以坟墓场最终Number$0判定施放中累加失效；在真实后续请求或同步伤害事件只读取证，再独立断言效果结果。JsonUtility可能把JSON null请求反序列化为空DTO；隐私检查应核对实际awaitingInput、ID和选项内容。Unity验证驱动须递归执行嵌套IEnumerator并捕获异常，避免子协程失败留下RUNNING；原失败保留，不回写成PASS。实际反例、修正及九场原生/三语言UI证据见 Docs/VariableBranchConsumerAudit-2026-10-06.zh-CN.md。第二次UI失败缺乏实际语言诊断，原因未定；新增诊断后本批通过不代表该失败原因已查明。

- 2026-10-05 右下操作／已知区域增量：用户取消常驻顶牌设计，改点击区域只读查看；可选手牌需要草稿→确定，不能假造原生确认。新增真实思绪→脑力激荡连续隐私检查及 >10 张成长涡旋消费者。回放中修正了测试错误路径、把专用 Yes/No 误当列表及扇形手牌中心射线假设；失败保留在新批次，不反复运行无关规则。内存原生测试 TEMP／APPDATA 与 java.io.tmpdir 指向 D 盘批次，避免 C 盘不足造成假机制失败。新增手牌浏览／残留牌背仅运行 RunHandBrowserOnly，范围与当前限制见 ResolutionDockKnownZoneAudit 审计。

- 2026-10-06 临时复制品真实操作发现两次静态快照漏项：来源为 LKI 无当前 Zone 时被隐私过滤全部略过；排序窗口把触发移出登记后关联暂时消失。复用前追踪登记→排序→堆叠→结算／反击，并区分外部安排与复制品自身触发。修复只读关联，新增三条实际操作；同编号连续 UI 变化独立检查，徽记数量适配器不计规则效果。原始失败与范围见 TemporaryCopyAudit 审计。

- 2026-10-06 用户定义“临时”为与原始不同，并限定基础攻防蓝色；旧路径只覆盖土地规则与关键词枚举，漏改名、非关键词异能和关键词参数。增加原始／当前投影及撤销恢复，直接原生修改器仍作为适配器而非施放首验。复制敏捷反例独立重跑 reflection-twice；反击后不再用“全文没有任何蓝色”代替“已取消的安排不再显示”。TMP Unicode 符号须看真实字符类型／颜色，未格式化花括号和 spriteCount 均曾导致测试误报。相关证据与限制见 CharacteristicChangeAudit 审计。

- 2026-10-06 goal 工作量复核发现新增 temporary-copies 原生三场及五套 UI 未记入旧43／8首验统计。保存独立结果副本、实际 case 身份与原断言哈希后改为46／13；九个包按具体完成范围缩减待办，仍65开放。未登记七场 X 脚本留在 A03，不能混入登记数量；修改器投影仍属适配器。清理淘汰旧成果的大文件时保留摘要和替代关系，不把首验进度倒退，也不升级为当前全量通过。来源变化仅影响未登记脚本审查队列，其11项仍开放；SOURCE_SNAPSHOT_MATCHES不是效果证明。

- 2026-10-06 用户明确要求继续 goal：按当前授权推进，工具仍返回 paused，不能用只支持 paused/blocked/complete 的 update_goal 假装恢复或关闭。登记七条历史 X 费用证据时保留原运行包、结果与轨迹哈希；不为登记重跑。新增关闭包后旧生成器只检查部分完成，增加 closedPackages 原行、原生/UI登记、审计哈希与开放队列互斥检查。实际关闭和支付漏接复核见 ManaVariantCostAudit 与 PhyrexianPaymentAudit；来源摘要改变需复核，不能只改计数。

- 2026-10-06 旧脚本复核发现未登记并不等于未知效果测试：卡图 HTTP 服务器、四个直接状态投影和一套输入可用性合同不能按卡牌施放计数。逐个读取驱动/夹具并冻结分类与源码哈希到 manifest.reviewedScripts；代码变化重新进入复核队列，报告保留完整分类。四个历史操作脚本仍须核对原证据/重叠再迁移，A01 只核销初步分类，A03 事件/替代/费用审查仍开放。八项生成器检查含驱动/夹具变化与“分类不授予效果PASS”反例通过，见 LegacyScriptClassificationAudit。

- 2026-10-06 目标与耗竭真实请求反例：普通咒语伤害分配实际为AssignAmount，堆叠视觉行编号是位置而不是原生ID；查看战场窗口枚举还曾漏掉目标来源／箭头。以实际输入／窗口根对象修正驱动和消费者，不能按猜测请求类或视觉名字核销。R13/R14有界闭包后targets仍有控制／网络消费者，归入原R02/R36而不删掉未完成维度。

- 2026-10-06 耗竭反例：OrderCards在攻击耗竭中表示可选集合，不是排序；按已知原文用途保留零选择确认与多对象身份。原生本回合次数和跨回合待重置玩家列表分别投影、到期分别检查。中文先核对固定卡文，截图检查补文字行数，不能仅用点击PASS证明布局。便携打包尚未结束时runtime.json会再次更新；等待构建进程实际完成再运行，原生生产者／夹具／运行包身份在启动冻结，不能在进程仍执行时编辑生产者。UI隐藏使用父CanvasGroup，分组仍服从原生eligibility，不能为让预期数量通过而强制改字段。具体失败和最终证明见 Docs/ExertBranchConsumerAudit-2026-10-06.zh-CN.md。

- 2026-10-06 数字／分配反例：任意数字默认99不能当规则上限，先用真实123操作复现，再限定补丁到无Max且非Random分支，有限范围及共同付款消费者分别回归。竞价确认测量须与实际字体/lineSpacing一致，点击通过后仍检查两行裁切。TMP Unicode图标的spriteCount可为0，须检查实际字符类型、资源、Unicode及可见网格并看截图。一般AssignAmount按原生入口区分已选目标分配、颜色组合和伤害处理中预防分配，不能只按请求名合并。核销R05后number流程族仍需归属其他多方/动态文本边界；声明/效果名沿用真实DealDamage，不新增不存在的Damage API。证据见 Docs/NumberAllocationConsumerAudit-2026-10-06.zh-CN.md。

- 2026-10-06 异能卡面反例：原生Targeting尾部使准确印刷段落匹配失效；分离已知尾部再匹配并保留实际目标，动态改写负例不得替换原始规则。长文加入滚动条须使用ScrollRect.AutoHide，默认Permanent会在后续帧覆盖手动SetActive(false)。先记录首次测量和后续实际高度，不能把浮点/字体猜测写成根因。共享格式三语言后按实际消费者补目标预览和替代排序中文输入/点击；初始化验收先等待资料Ready，不以创建瞬间的缺译核销所有资源竞争。日文现有英文回退仍按缺译记录。证据见 Docs/AbilityTextConsumerAudit-2026-10-06.zh-CN.md。

- 2026-10-06 昼夜／速度反例：原生效果对象 `Max Speed!` 与辅助图索引 `Max Speed` 标点不同，图查找已有归一化不代表新说明／数字分支自动识别；用真实极速输入检查，不能靠手造索引名。数字基准20在1.3偏好下实际26，CJK字形行高又大于字号；同时验证偏好缩放、preferredHeight／Width和完整可见框，不能只断言fontSize等于常数。界面缺状态值时核对原生玩家读数与效果标记，保持对象ID／席位，未知文本仍保留。R25九条实际操作和一套双方三语言展示有界核销，普通状态／未知消费者不因辅助图加载获得效果证明。见 Docs/PlayerStatusConsumerAudit-2026-10-06.zh-CN.md。

- 2026-10-06 生成预防效果卡面反例：玩家效果列表补了来源印刷，不代表同一效果在替代排序中的裸card投影也有图。读取原生getEffectSource及image关系，公开来源/图片一致才投影独立art字段，效果保留自身ID；通用名按原生关系判定，不能解析调试编号当身份或覆盖自定义名。两条真实预防回归不重复计首验，八个直接投影条件单独计适配器。夹具回调在开局调度之后执行，测试要回答正常开局输入；无结果超时不可归因为引擎。新增Editor源码须Asset刷新且确认实际类型已编译，脚本刷新/空Console本身不保证新文件导入。剩余归属核对TSV原边界，不能凭包号记忆把隐藏信息误放到数字永久修改R39。见 Docs/EffectSourceConsumerAudit-2026-10-06.zh-CN.md。

- 2026-10-06 君主／先制驱动补回调时，已通过君主场景与后来先制场景来自不同驱动版本。为凑一个当前驱动哈希重跑所有通过场景会重复工作；冻结每个真实生产驱动，结果保存 driverArtifacts 并逐条验证实际哈希，聚合只合并这些原始 PASS，不将它们重标为当前驱动全量通过。搜地需要 InputSelectEntitiesFromList，随后另一席 RevealCards 才能继续；带额外手牌的开局不能固定断言搜地后只有一张。各失败驱动／结果保留，只补失败分支。六场及三语言UI检查见 Docs/RoyalStateConsumerAudit-2026-10-06.zh-CN.md。

- 2026-10-06 同源君主双生物触发反例：相同source.id不等于相同ability.id，排序按原生异能身份分别保留。状态列表辅助图成功不替代裸card用于堆叠/排序的元数据；旧辅助对象移除后待结算异能仍要保留资源。君主/先制公开EFFECT、源为空的两个闭合分支补投影，同名普通卡和源非空派生效果负例检查。结束步骤Phase和先制Player尾部也会使准确段落翻译失配，按实际事件尾部保留身份再翻译已知段落。尝试盖放无PaperCard规则helper是无效夹具，保留失败并换可构造负例，不据此扩大效果范围。三原始真实流程冻结旧运行包，两个当前回归和一条旧抓牌触发当前首验独立保留；来源相同/不同与数量从原轨迹断言，不为补断言重跑。详见RoyalBoundaryConsumerAudit。

- 2026-10-06 魔戒阻挡/控制驱动反例：阻挡选项实际使用defenderId，不能按不存在的blockerId断言；玩家目标按原生targetId，不按席位拼ID。已通过的离场/控制保留真实生产版本，四级阻挡断言修改才补相关四场。当前运行包测试进程因系统内存不足在规则操作前退出，hs_err为环境证据；限制单个Java-Xmx768m及ActiveProcessorCount4、顺序执行，只补失败启动，保留先前PASS。CJK英文字形在22号字体下实际preferredHeight61.855、旧58高会裁切；复用现有按实测行高扩提示区分支，固定字号，不能猜字体原因或重新启用BestFit。R33审计保留原首验/当前回归/新UI身份，首次成果不为统一脚本哈希重跑。

- 2026-10-06 地城实际请求反例：单合法免费施放是InputConfirm携带previewCards，现有卡窗SelectionAction提交，不能按类名假设右下ValueOption。Undercity脚本Archive/Oracle Archives、中文～与日文―分别是已知源名和格式差异，准确完整段落匹配后才能使用现有翻译；未知改写仍保留原文。原生玩家targetId与seat不是同一编号；InputAttack选项selected不一定反映当前已攻击，核对真实对象/步骤。Windows原生只读审计文件短暂占用时仅同request限时重读，失败/驱动身份保留。当前公开限制补丁对48份旧/新投影逐份比较，仅删新准确限制后完全一致，界面再验蓝色/原始/同ID到期；不据此扩大到其他hidden内部词或Kick外部CantBlockBy效果。PowerShell5.1正常语言stderr导致构建误报时改用pwsh并等打包结束，未完成构建不能冻结运行身份。证据见DungeonPathConsumerAudit。
- 2026-10-06 叠放数量首帧反例：稳定帧数字恢复不能否定截图中的首次裁切。Label在PreferenceText创建后设Truncate，固定24高小于字号1.3的CJK实际35.6856232；后续绑定改变溢出方式。只改Count为实际preferredHeight，双方三语言/三个字号分别在首EndOfFrame及后续帧核对网格、边界与字号，核销R47这一具体观察，不重测无关玩家状态或BO3。证据见StackCountGlyphAudit。

- 2026-10-06 地城闭包暴露首验统计重复要求：旧verified_progress强制closedPackages有uiTargets首验，导致已经通过且已登记的共享控件增量无法核销。新增uiIncrements独立引用，必须匹配已登记UI job/result、A06 regressionBatches证据、文件哈希与PASS；不增加uiTargets首验。六个真实账本/负例检查验证无重复首验、缺审计/未知job/重复/改哈希/RUNNING均拒绝。原closedPackages验证保持。来源与完成量分开，不为满足生成器伪造新消费者。

- 2026-10-06 用户指出整体效率低并要求尽可能减重复工作。计划虽有合同复用，剩余行仍大量使用“其他组合/消费者”，容易被直接转成追加运行；分轮盘点、三语言和专门审计也使管理成本偏高。修正：运行前先引用证据，再明确缺失断言及扩展依据；共用UI/网络路径取代表，控制/付款/私有信息差异保留；驱动改动保留已成功版本，只补失败/改变断言；批末一次登记，复杂变更/闭包才新增审计。真实反例参考R02：三进程起手恢复只核销已有消费者的联网增量，337/32首验不增加，队列错误仍开放，不能机械给所有机制重跑该场景。检查既有哈希/排期/来源及skill格式；本次只维护方法，未运行规则/Unity/联网，goal仍暂停，未删除未知范围或伪造PASS。

- 2026-10-06 剩余工作包复核：R10/R12重复写非生命撤销、R11/R51重复写罕见费用、R19/R35重复写延迟生命周期，多包重复控制/网络/语言及分组检查。19条boundary改为共用检查单一负责、其他包保留独特结果；原文字/负责关系存plan-provenance.boundaryOwnershipReview，partial.remainingBoundary同步，原首验/闭包/审计哈希不改。54追踪ID继续开放，不伪称19条已通过或已关闭；跨包引用只引用具体证明，不等其他包整体关闭。生成器检查排期唯一归属、边界与部分账一致及原证据哈希，格式检查通过后生成剩余报告一次；不运行机制/UI/联网。

- 2026-10-06 接续：普通负载真实network-send队列4错误由共享128槽容量不足触发，扩大2048并实际dedicated/LAN两场/三次重连核对，0队列错误；范围仍不覆盖极端多房间。四历史效果原结果九末态独立核对但执行哈希缺失，保留历史未版本化证明和具体负责包，不以哈希统一重跑。七条受限/反射/替代产费及反击差异，只追加真正缺少的两条反击断言；前五原驱动身份冻结，不重跑。真实356生物类别被渲染成重复卡图异能选择，定向新增搜索分页字符串列表；三语言/原索引/射线/权限及相邻实际异能/颜色验证，未跑无关UI或重新网络构建。Unity调用超时后检查现存PASS/截屏而非重复启动；清单生成器首次读到写入修正前的断言摘要而拒绝，统一使用原有JSON规范摘要后核对。原文档/原失败/版本和缺译名单保留，依据 .tools/network-queue-1006/result.json、.tools/legacy-proof-review-1006/result.json、.tools/restricted-mana-1006/result.json及counter-increment.json。

- 2026-10-06 Arena/spending接续：新增八条原生差异，一套紧凑确认三语言增量，不增加UI首验。取消测试发现Mountain仅派生stackablePermanent变化，资源/规则恢复；明确只排除该UI提示后重试成功，不修改生产退款。候选扫描错用Triggers且漏SP/PersistentMana/CombatMana，改由原生构造器九字段扫描，245条按14分支分类并保存各来源SVars，四个已知反例/身份唯一性检查通过；分类不计效果。同名TrigSpent不能跨卡合并语义。截图提交后窗口消失，保留原点击PASS但不用该截图做视觉证据；仅移到提交前后重采，原生不重跑。前后成功驱动分别冻结；R09/R12核销具体差异，其他期限/谓词/触发/标记保留原负责包。依据 .tools/arena-mana-1006/result.json、ui-increment.json、.tools/mana-spending-1006/result.json 与 .tools/mana-consumer-classification-1006/result.json。

- 同批分类收尾：三份双面脚本重用TrigTransform/TrigMana/X，原文件级字典覆盖前面牌面。改为保留每条SVar的原行/牌面和本面查找；245行身份与14分支未改变，逐行本面一致与Kessig双面反例通过。原分类/驱动/审计冻结为initial-*，仅分类检查重跑，原生/UI不重跑。

- 2026-10-06 法术力限制/期限七条：成功驱动三版本及夹具冻结，新增坟墓场初始摆放不改变旧证明执行身份；ChooseNumber漏路由与exileCards键错误的原失败保留，只重试失败/未运行项。确认界面完成后从原trace提取生命/牺牲/可选触发请求，无需为截图重跑引擎。新增Phase正文断言的第一次UI被编辑器域重置打断，RUNNING与部分文本保留、不登记PASS；只重试此UI增量并在三语言完成后取证。登记原生359/UI33；其他谓词/花费触发/期限互动保持开放，见 .tools/mana-lifecycle-1006/result.json。

- 2026-10-06 费用谓词五条：原生结算二倍X将费用展开成数字，丢失X来源而拒绝Rosheen法术力；只保留准确SVar配方的宣布X来源，不按牌名/英文猜费用。修复后补普通XX施放、激活X、结算X及含C触发四条同函数回归，固定费用拒付作独立负例；原生候选提供颜色不代表该受限法术力可付款，必须核对池/剩余费用变化与取消恢复。已知Scry/ChooseModes先读取真实ID，驱动遗漏与实际无进展失败分开保存；正例下一支付请求立即检查进展，避免循环到大量超时。首验三版本保留，重构第三版本时发现Windows换行改变哈希，保留CRLF副本并另存与执行哈希完全匹配的LF版本。运行包清单精确仅新增目标class与编译器switch辅助class，其他条目不变；全局指纹失效保留。UI按实际付款席位、选项门槛、身份与确认合同引用旧证明，不再运行截图/联网。累计364原生/33UI；Echo/其他SVar配方仍开放，依据 .tools/mana-predicates-1006/result.json。

- 同批登记检查：`structuredResult` 运行器读取新生成字典的status，不能指向人工汇总的历史结果列表。两个相关任务改为启动写RUNNING、完整成功写PASS、异常写FAIL的独立run-summary对象；六个隔离main检查覆盖整套/部分/失败，不启动Java。实际操作函数和已执行驱动副本未改，不重跑规则。汇总合同变化后仅刷新清单/来源与生成器校验。

- 2026-10-06 回声费用：触发异能的激活费用为空，不代表结算中独立Echo费用没有X；真实Volcano施放/ETB/下次维持重现无进展，原生限制按准确Echo参数读取Cost后修复。来源产费可被原生立即分配给当前成本，不能强求随后四次池回调；以请求成本、准确来源横置、同维持资源变化与继续优先权断言。响应测试只能从操作席位授权视图读取私有手牌，不借非操作视图推牌号。保持同Java文件的补丁在一个定义内累积，当前编译器会覆盖相同javaPath的独立定义；精确源码和运行包清单核对仅目标class变化。三首验/两关联回归、原失败/成功生产版本与合同复用见 .tools/mana-echo-1006/result.json；追加UPKEEP断言直接查已有最终轨迹，不重跑已通过原生。当前367/33，回声具体范围核销，其他生命周期/控制/免付保持原归属。

- 2026-10-06 花费附带效果：Domri原生两色InputConfirm不能按类名误认躁乱；关键词按原生RIOT枚举。准确花费触发段落附remembered-ability/事件尾部，仅完整已知正文剥离再翻译，身份尾部保留；堆叠/预览牌名纳入现有原始/当前名称守卫。初次UI原FAIL里的独立完成中文子范围分别核验，后续只补未运行/裁切/名称受影响项；15格矩阵引用各自原结果，不重标整套PASS。固定字号实测61.855px超58px，复用测高分支。UI登记须同时包含engineJobs、assertions和family.ui引用；遗漏字段使清单刷新KeyError，修登记后只刷新清单，不重跑效果。603原文件哈希及图像解码通过；静态英文标题/忠诚中文留R47，改名负例仅UI投影。依据 .tools/mana-spent-effects-1006/result.json。

- 2026-10-07 R45闭包：旧EndReview检查直接onClick.Invoke能通过但实际返回按钮被只读浏览遮罩阻挡，不能将事件回调等同射线可点击。新四格实际射线同时发现结果按钮best-fit，局部禁自动缩放/应用字号偏好及快捷按钮Canvas层级修复，仅重试相关UI；旧失败及源码保留。两个原生失败分别为错认替代请求和UPKEEP先于DRAW的驱动期待，前五原成功不重跑，预防单独重试，Familiar拒绝只追加一条；三成功生产身份聚合不伪称最新驱动整套PASS。R45完整原行与规则/界面证据关闭，53开放/12关闭/21部分、386原生/35UI首验；其他语义原包开放。依据 Docs/DrawBoundaryConsumerAudit-2026-10-07.zh-CN.md 与 .tools/draw-boundaries-1007/result.json。

- 2026-10-07 R42：新驱动误把带堆叠InputPassPriority当未处理、漏成本确认，原失败保留，前三成功不重复跑，后二重试；原成功摘要错称额外拒绝，真实轨迹没有该步，核销断言明确剔除。UI初始无动态Controller的setup错误留下无主RUNNING，核对资源/场景无owner后记录ABORTED，复用既有创建Controller方式；单一候选窗口以Artwork直接选，猜Select按钮/后面战场牌会错误诊断正常遮罩。按实际ChoiceCard身份的Artwork射线修驱动，不修改产品；六中文已完成子范围保留，仅补33剩余格。原数值通知在固定Modifier之前，与实际最终规则分支分别断言。依据 .tools/dice-combinations-1007/result.json 与ui-matrix.json，原R42关闭、391/35、13关闭/52开放；只注册UI增量。

- 2026-10-07 R11：真实原生费用碎片顺序不影响等价比较，颜色标签按实际大小写归一、未付零值实际为0；可见施放按钮不等于可支付，必须实际尝试/取消。按源牌ID识别堆叠，允许并独立断言真实后续触发。规则资源比较仅排除已知派生stackablePermanent提示，不忽略身份/顺序/横置/池/生命/牌库。旧跨区域审查按准确卡ID，不能把夹具保留的起手手牌全部当成本；只读旧证明不增加首验。五次成功保留各自驱动身份，六步UI作为合同增量，召集COLORLESS通用费用误用C图标另留R47。依据 Docs/MixedTapCostConsumerAudit-2026-10-07.zh-CN.md；R11有界关闭，440/36，18关闭/20部分/47开放。

- 2026-10-07 R10：确认初始夹具flag是否改变待验证语义。NoPaymentFromManaAbility会让产费漂浮，手动池支付不建立自动来源链，不能据此要求默认嵌套退款；实际默认五条自动部分支付/取消/重施通过。固定InputPayMana请求可能只有提示源牌ID而没有sourceCard，按真实合同读取。嵌套完成的父付款会先发暂态请求再自动刷新，等待自然刷新证明可继续，不以点击其他卡制造刷新；快速旧响应被拒绝后核对当前请求继续。仅排除已知两项派生分组提示，资源/ID/顺序仍精确。三步UI补嵌套生命周期，旧三语言/池合同沿用。工具execute超时不表示验证未执行，先读result/steps/console再决定重试，避免重复启动。依据 Docs/ManaSourceRefundConsumerAudit-2026-10-07.zh-CN.md，445/36，19关闭/19部分/46开放。

- 2026-10-07 A04：旧正则把关键词参数/列表及LifeCondition等后缀字段当独立未知机制；现用完整字段与原生关键词解析顺序分类，逐项保留原文/路径/行号/牌面/哈希和未知成员。2732写法与当前运行包解析器对照、13工具检查，275旧项归并，Visit新保留后100未知。K行不能按AddKeyword列表拆分；Gson原生未知输出须serializeNulls防漏键。分类不授予执行语义/规则/UI PASS，不按声明数扩测试，A01–A05共用表。依据 Docs/DeclarationClassificationAudit-2026-10-07.zh-CN.md。

- 2026-10-07 R22：真实Steel复制暴露物理印刷与当前图混用；投影当前资源及独立背面标志，原始切换保留物理印刷，隐藏守卫保持。排列断言不能拿神器生物或不同卡高的中心Y作同一支援行参照；按实际X读序修驱动，未改正常布局。三条原生首验、Manufactor旧语义回归及三语言资源/射线增量分开登记，R21和替代输入合同引用旧证明。驱动汇总状态用四个stub检查，不重跑已通过原生；通用证据递归器不能把历史运行包清单内的相对路径当工作区证据，清单自身校验哈希，当前改变class另外校验。611项引用哈希、生成器/skill检查通过；依据 Docs/NoncreatureCopyConsumerAudit-2026-10-07.zh-CN.md 与 .tools/noncreature-copies-1007/result.json。

- 2026-10-07 R41反例与方法修正：引擎真实装备已有关系但协议缺字段，先保留失败再补公共身份；玩家targetId=0不能判空。初次UI只断言desiredTo，截图却在旧端点；改查实际from/to并等待过渡。R22旧证明覆盖卡图，不覆盖复制详情的规则/类别/费用；以原快照补详情及物理原始切换，保留旧结果界限，不为统一源码重跑原生。背面no cost测试误期望文字，修正断言后只继续未完成步骤；英文悬停一次被挡未重现，保留失败不杜撰原因。8条原生、17资源末态、33项UI及汇总四stub通过，更新ui-reuse对应规则并检查skill格式。

- 2026-10-07 R24：到期快照处于对手第2回合MAIN1，不能要求己方地已重置；按真实turn/阶段与实体支付审计修正只读期待，不重跑八条成功流程。截图核对土地符号时检查TMP实际Sprite的R/B身份，不以已染蓝的颜色猜测法术力。32项UI成功保留，只补四个已有快照的符号断言；无生产改动，复用旧三语言/双方特征合同。依据 Docs/ContinuousStateConsumerAudit-2026-10-07.zh-CN.md。

- 2026-10-07 R26：暗置非生物可浏览不代表可翻正，检查原生候选缺失和真实点击不提交，不要求Button禁用。离场公开正面后准确已知手牌可见是合法知识，不误判泄露。翻正重建先隐藏再延迟销毁旧Border，动画断言取active同名对象，不能用Transform.Find旧边框误判；保留7+14+6项成功，仅续最后2项。Unity原生崩溃后检查现存结果/外部重启状态，恢复只补未完成项，不重跑5条原生。全文翻译差异归R47，字体/隐私PASS不等于全文本地化。依据 Docs/FaceDownLifecycleAudit-2026-10-07.zh-CN.md。

- 2026-10-07 R27：跨区目标的原生数组含六类区域信息标题，不能将全部无卡字段当动作；只在准确含卡dialog合同过滤标题，保留原选项索引并验证相邻普通footer。提交后CanvasGroup主动关闭射线、非操作视图没有playableExile都是正确守卫，不能要求第二次可点击或所有视图都有施放区；此前屏幕外中心诊断未证实，实际第一次点击成功。TMP PUA费用核对实际TMP_SpriteCharacter.name，不猜sprite标记。第九条原生完成后汇总函数被局部字符串覆盖，保留原FAIL/缺失结果目录，以独立只读轨迹/资源/身份核对恢复证据，不能重标原运行或重跑八条已过流程；四stub仅检汇总合同。9原生/21资源/23UI增量，三语言新标签与相邻footer，旧特征/支付合同复用。依据 Docs/BendingLifecycleAudit-2026-10-07.zh-CN.md。

- 2026-10-07 R17反例：只检查抓走旧公开顶牌会遗漏已知身份没有继承到手牌；追加准确ID与其他手牌不泄露断言，真实Mulch/Opt暴露并修复。Index完成后的自有顶牌及Scry1内部移区另验差异，不把所有LibraryAdded当私下重排。UI首次猜错DirectHandSelection，后续重复重放入口缺失根因未证实；保存FAIL与源码，加运行互斥后单次24项通过，不能写成已证实产品竞争修复。成功Mulch/Peek、旧悬停与Brainstorm回归分别保留原输入/版本，后者原驱动未自记runtimeHash，版本关联限制如实记录。runtime备份目录曾覆盖旧class，修为每运行身份一份，未伪造已丢原类备份。依据Docs/KnowledgeLifecycleAudit-2026-10-07.zh-CN.md；本批5原生/24UI增量/2悬停/10只读审计，不重复计UI首验。

- 2026-10-07 R29：对象text/count正确不能证明数字已绘制。原生侵染/干枯快照UI结构PASS后截图发现Counter_M1M1只有底色；实际字号21/框高21/preferredHeight29.738、Truncate生成0顶点。保留原19结构/点击证明及失败截图，只修改永久物计数字段溢出，针对三帧补非空字形与截图，不重跑九条成功规则。无合法阻挡者原生可自动跳过InputBlock，合法性反例增加另一个合法阻挡者；Wall of Air为1/5，修正驱动仅重试三条失败。联合阻挡成功不覆盖联合宣攻；真实重复宣攻仍两单成员组，桥接早返回的缺口保留R29。依据Docs/CombatKeywordAudit-2026-10-07.zh-CN.md。

- 2026-10-08 费用源码分类补正：单条件正则漏掉 Cost.java 的 `Untap || Q` 分支；初次修正按首个右花括号取正文又截断两种移除指示物分支。改为完整条件/顶层分支提取，并断言全部42个构造类均有映射及两别名均存在。33980份卡脚本重新归类仅CostUntap声明改变，其他41行/全部源码身份一致；初版分类、脚本和规则证明冻结，补正见 `.tools/unattach-cost-1008/classification-amendment.json`。没有新增规则/UI首验，不重复引擎/Unity。R47正文边界同时同步TSV与partial进度账，生成器继续检查一致性。

- 2026-10-08 处理体取消反例：原驱动遗漏SelectCards且误读扁平cardId，按实际option.card.id修正后复现跨拥有者费用未退款；生产修复后剩余失败仅正面公开放逐排列改变。依据CR400.5仅规范化该无序区，保持身份/属性/牌库及坟墓场顺序检查，重试原ID索引从0变1仍正确。旧失败冻结，不为测试假设改生产排序。规则232项和相关240项回归通过，Unity执行被审批策略拒绝，新点击保持待补；不把源码/过旧editor_state或旧R15证明提升为新UI通过。见 `.tools/processor-cost-1008/result.json`。

- 2026-10-08 处理体窗口增量：同布局窗口会复用Button并RemoveAllListeners后重绑新request/index。旧对象引用不等于旧回调，不能调用已重绑按钮后期望拒绝；分别检查旧request拒绝、同对象新编号0→1射线及一次提交。131项后错误假设失败保留，新174项含纠正后合同和展示修复。外部源码修改后若Unity仍显示旧字符串，先确认当前程序集/编译，再重放受影响窗口；不把磁盘源码或compiling=false当作已加载新代码。首帧标题被Label重设Truncate，周期偏好扫描后才恢复Overflow；构建末立即应用现有绑定，并采样三语言首帧/扫描稳定与两个共用标题。见 `.tools/processor-cost-1008/ui-followup/result.json`；旧pending证明不改写。

- 2026-10-08 搜证/觅食边界：全部候选出区并退回的样本不足以发现子集退款把原坟墓场顺序变成“未选牌+已选牌”。Hero选择Bolt/Opt并保留Negate复现；原生局部快照另记准确坟墓场序列，不能像无序Exile一样规范化。Forage两支付源成功留原0f996b9b，新增重排对已证同序的状态无事件且无改变；只重试Hero失败与最终处理体回归。UI确认需按native action ID匹配，部分选择的Reset不能被当成Confirm；截图发现真正指令被来源描述挤掉、日文数量/按钮截断，点击PASS不授予完整文本展示。136控件证明保留，新增105当前文本/布局射线；不重跑原生效果。见 `.tools/grave-cost-boundary-1008/result.json`。

- 2026-10-08 督战取消反例：旧Arena证明拒绝发生在Exert提交前，不能核销提交后退款。Basri真实提交后取消残留次数/重置义务；只为该事务捕获准确原次数和玩家，不能全部清零，费用触发和对手已有标记分别断言。原生阶段流程需按真实请求/观察计数设置足够的有界步数；180预算在turn5前耗尽是驱动失败，保留后重试未通过场景。Card首次覆盖编译带出九个伴随类；原jar仅getAbilityText的两个无用final常量局部存储与javac不同，逐指令核对值/索引/跳转，不能将原始字节不同误判规则变更或无依据忽略。UI字段/格式和源分支完全相同，复用原三语言射线/状态证明，不因换牌名或运行哈希重复UI。图内置连接失效时，本地CLI同一项目/coverage可查，已验证后按原图证据等级继续；不为恢复连接重建整个索引。依据 `.tools/exert-cost-1008/result.json`。

- 2026-10-08 全部剩余范围整理：family.gaps及包内旧描述仍混有后续已完成内容，“其他组合”又没有终结条件，导致34包计数不变而剩余工作无法评估。冻结原计划/证据及2890条审查输入、100种未归类声明，活动范围改用remaining-work.json的81个有编号事项（41审查、32证据、8决定）；旧完成证明直接引用，不重写为新PASS。此数量是工作事项而非测试数；审查先完成消费者/旧证明归类，之后才能提出最小缺样本集。新发现分支必须保留源码/请求依据、归属和净增量，不能因标签或任意组合继续扩张。生成器检查34包唯一归属、事项ID/结束条件、冻结输入哈希及历史证据；用户只授权整理，goal保持暂停、未运行新对局。完整清单见Docs/MechanicValidationWorklist.zh-CN.md，冻结及一致性检查见.tools/goal-scope整理-1008/。

- 2026-10-09 R36可选搜寻实测：原生 `InputSelectEntitiesFromList` 先发 card:ID，再由更新请求提供 ok；选牌后 cancel 仍是拒绝，不能据此期待移区。被控制者的手牌已合法看过后可继续保留知识，只核对没有新私密信息泄露。必须按实际搜寻牌库的拥有者区分接管：Doubling Chant 搜自身牌库适用 Opposition Agent；Dichotomancy 搜对手牌库不能把施放者当作正在搜自身牌库。真实流程分别暴露 Searched 丢失 FoundSearchingLibrary（选中熊错误进战场）与错误接管/手牌泄露，修复两处原生效果并完成接受/取消/对手牌库及普通搜寻差异。旧终局和成功原始生产者保留，只重试失败或确实被生产修复影响的分支。原55/81提前核销已撤回的记录保留，此次全部原命名风险有证据后重新闭合，26待办；来源和77实际UI点击见 `.tools/controlled-search-1009/audit.json`。


2026-10-09 R03方向增量：JsonUtility可把JSON缺失的嵌套card构造成非null但id=0的对象；候选身份断言须同时验证正ID，不能只按card!=null计数。标准控制器只读观察真实AI返回值不等于人类客户端支持。测试期望字符串格式错误可从冻结原生快照独立补断言，原FAIL与先前通过UI前缀保留，只补失败选择分支；不可重写旧成功或伪造整套PASS。依据`.tools/direction-choice-1009/audit.json`。


2026-10-09 原生指示物单个数量的toString可省略x1，显示字符串不能作数值预期；观察器保存getCounters返回的整型以避免格式造成重复运行。Match.startGame开局夹具在mulligan之后启动，观察器不覆盖早期mulligan；早期操作照真实输入继续，不伪造快照。Barrier/Pramikon成功冻结，只重试失败Teyo，方向按钮四个实际合同等价引用旧101 UI，只增量查看同效果ID跨回合持续/消失。依据`.tools/direction-lifecycle-1009/audit.json`。


2026-10-09 投票可选额外数量的原生Human请求实际为numeric dialog（one: How many additional votes do you want?），不是ChooseNumber。先核对真实请求，再按原合法ID输入；UI按数字枚举走已有右下草稿/确认。仅重试失败Tivit，成功Plea/Judgment/Squire来源冻结；卡牌投票按准确数量门槛/动作/身份/选中状态合同复用方向单选证明，数值票数/不同公共区域原ID由实际结算和原生投影补验。原英文正文/数字用途无标题保留R47.05，不由交互PASS核销。依据`.tools/vote-choice-1009/audit.json`。


2026-10-09 受控投票真实反例：Other按原获票/投票玩家排除，不能排除替他选择的控制者；可选额外数量仍由原玩家选择，每票由控制者选择且记入原玩家。原生PlayerControllerHuman.vote忽略forPlayer展示，桥接用局部回调/finally投影准确身份。实际玩家候选请求是InputSelectEntitiesFromList/player:并用ok确认，不能据Vote或ChooseOptions名字假定select:/confirm-cards。对新增字段先核对实际消费者路由；主语言点击完成后仅失败语言重试，准确AvailableLocale代码沿原en-US/ja-JP规范。空输入positive guard，原FAIL保留，不让0次迭代得到有效PASS。依据`.tools/vote-followup-1009/audit.json`。


2026-10-09 实际投票与快路径反例：Saga阶段按原生lore/turnSeat，不能把全局turn2当原玩家第二回合；原生Money唯一拥有物自动选取，不应伪造第二窗口。可选card-vote以原投票上下文+合法cancel判定弃权，文案可写至多而不改原生min/ID。连续Illusion代卡牌投票的同池布局缓存漏原投票者，真实voter2标题停留voter1；将original voter/secrecy加入布局身份，只重试同一冻结原请求，不重跑已通过原生场景。UnityEvent容器被重绑不等于保留旧闭包，过期应提交原requestId验证。依据`.tools/vote-resolution-1009/audit.json`；第三原生fixture拥有者在无Gui3投影时为0，必须区分两支持视图与原生三玩家观察，保留R37/R02。


2026-10-09 多对手反派/长行反例：同一原玩家的两项反派选择先收齐再逐项执行，转下一玩家前remembered清空；真实4cast/结束步骤可同时证明多受影响/未受影响、重复相同及不同选项、私有弃牌、原ID抓牌/令牌/恢复，不按每个标签另开游戏。小于等于10张可见自己手牌走DirectHandSelection，不因InputSelectCardsFromList名字强制断言有窗口。JsonUtility无card也可能构造空对象，候选须用真实正ID/非空名字；失败检查不授PASS，已经通过前缀冻结，只补失败确认/恢复。uGUI TextGenerationSettings默认lineSpacing为0，测量两行26但实际52；测量须包含实际行距和前缀，并核对真实字体/最大字号及相关短行，共用显示修复不触发原生全量。依据`.tools/villain-multi-1009/audit.json`与`ui-frozen-review.json`。


2026-10-09 阻挡交换/伪装反例：同一控件不替代不同原生结算：Jarkeld不重新触发阻挡、Sorrow移出再阻挡触发，非法合法性/容量须整组原子不变；用原攻击者/阻挡者ID关系、实际战斗伤害与来源横置/生命证明。Camouflage候选CardCollection由ArrayList支持，foreach中移除会跳过紧邻末尾候选（本批不能阻挡Interloper后横置Elf）或抛错；倒序原索引删除保留合法顺序。固定标准Random并仅记录真实nextInt，按原生抽样还原Fisher-Yates与合法性/min/max；不把一个排列当所有随机证明。零合法阻挡者的phase早退无替代/随机，原已完成结果的错误RNG期望从冻结轨迹复核，不重跑。精确单选后原生ok与confirm-cards不是同义ID，读取真实返回选项。补丁只编译新Effect覆盖类并核对所有旧类/库哈希，相邻无调用消费者保留旧证明；完整payload/攻击群/旧排序/多人/语言仍原归属。依据`.tools/combat-choice-1009/audit.json`。


2026-10-10 SwitchBlock容量反例：原生CanBlockAny独立于canBlockAdditional，两者分别进入接收容量判断；无限/额外有限/不足两个交换方向按真实阻挡关系验证。零战斗伤害也可能进入AssignAmount，沿实际零总量确认继续。驱动在完整结算保存后失败时，冻结结果独立核验并保留原FAIL，不重跑已完成效果或增加驱动PASS计数。旧排序仅在生产入口启用orderCombatants时可达，当前源码不启用，保留条件身份按入口变化再激活。依据`.tools/switchblock-boundary-1010/audit.json`。

2026-10-10排期减量：用户反馈每轮只核销一组，原88顶层引用被误报独立组。当前批量规则已改为先对齐多个共享夹具输入、结果各核销、UI取合同代表并批末一次登记；重复ID/嵌套汇总保留来源但不按引用次数新增运行。指挥官专属整项与分支按用户后置，通用机制保留普通1v1，后置不授PASS。报告分别统计当前与后置并校验原待办身份；原验证账本byte-identical。

本次排期校验：当前11／整项后置1／条件分支后置3；推荐A01输入4＋5＋2共11个不同ID、联网2个原事项，口径分开。未知、已关闭、重复整项与不存在的条件输入均拒绝；skill格式通过，原规则／Unity运行次数0。结果`.tools/goal-priority-1010/check.json`。

2026-10-10 指示物批量增量：Eventide真实只移除2个指示物却抓3/失3，原因是候选Multiset.remove只消耗一次，已经用完的类型仍被再次提供；局部覆盖CountersRemoveEffect并以同生产者准确数量、两类型、玩家、零停止和共享预算补验。原生必选单候选及固定数量可能自动确定，Goldberry/Amy不因缺少预期窗口判产品失败；Ashling伤害3使3/3 Hill死亡，按冻结真实结果纠正驱动期望。伤害触发须等实际COMBAT_DAMAGE，不在宣攻后的空堆叠提前断言完成。保留原FAIL及5个准确完成前缀，不为绿化驱动重跑已完成结果。原counter类型/数量问句误走“发动能力”窗口，按实际问句进入已有值选择，388相关UI检查；四原输入核销，Add条件/权限/记忆子效果保留。依据`.tools/counter-flows-1010/audit.json`。

2026-10-10 增减权限尾段：Clockspinning已选M1M1仍把null传给canReceiveCounters，Melira带类型的禁止导致真实付费结算NPE；权限判断统一用chosenType并验证只能加、只能减、都禁止及原不限类型二选。Lavabrink可选问句不带sourceCard，拥有者从真实公开战场牌验证，不能检查JsonUtility默认空对象；Guile模式选中后还需原confirm-modes，保留成功施放/空自动加前缀，仅续记忆伤害尾段。Optional固定类型消费者与假设无类型分支区分，原11候选没有该假设不造测试。首次新类安装后未等manifest发布就启动after case，旧runtime元数据保持原样并明确限制；后续驱动增加两受测类与files.json一致的启动守卫，发布返回session时必须等同一操作终止后再验证。UI首轮新问句已加载但新Doom映射尚未加载，反射确认编译后末日译名再续相关检查；原FAIL不改写。8完整/前缀流程17结果、9付费20mana、212UI；原输入核销，18→19／107→106。依据`.tools/counter-addflags-1010/audit.json`。

2026-10-11 原生空攻击/阻挡反例：拘留唯一阻挡者时Human自动跳过InputBlock，唯一攻击者被禁时也自动跳过InputAttack；等待不存在的窗口导致驱动过多回合后停在无关弃牌。按真实阶段/原精确对象canAttack/canBlock及启用选项和实伤确认，保存原FAIL、仅核销request123前已有三禁止/自然来源到期/恢复；未完成后段激活不冒称完成。未来驱动据实际空合法集合推进，不伪造窗口，不重跑成功付费操作。拘留引擎正确但wire无字段是独立展示遗漏，补公开bool及分组/蓝详情；新实际换区重施证明当前投影，旧完整结果保持各自runtime，不为凑哈希重跑。两清伤入口和普通blocked/显式unblock按真实差异分别取结果。依据`.tools/current-object-flows-1011/audit.json`。

2026-10-11 战斗关系增量反例：InputBlock确认可启用但由原生validateBlocks拒绝非法声明；不能猜按钮应禁用，实际提交后核对同阶段的新InputBlock及修正。单选实体先切原card选项后还须原ok，不能反复点同牌。AssignAmount等自定义Pending.playerSeat可为0，观察器只在实际InputBlock读取该字段，其他请求不把0当索引-1。Fog连续两次真实付款复现多触发，判定用当前Combat.isBlocked转换而非永久一次性历史；显式unblock后再阻挡仍须触发。Noble配对不等于blocked标志，实伤前分别断言。保留全部原FAIL及Blaze有效前缀，已成功Portal旧runtime不重跑。依据`.tools/combat-relations-1011/audit.json`。

2026-10-11 生命/攻防交换驱动反例：保存数值列表可包含两个相同40，不能以label唯一强制拒绝原生合法ordinal；UI按数值排序后仍须提交原ID，重复值分别测试实际ordinal而非仅文字相同。纯玩家实体选择走战场头像与推进确认，数值枚举走已有加减/确定，不假设每个原option有独立Option按钮。保留原两个头像＋确认42检查成功前缀，仅续数值及未验证语言；实际Life Total原问句缺接收者表示，沿现数值控件补准确三语言玩家名。基础攻防与净值分开断言，普通增益不被交换覆盖，实际到期及来源回手由原生流程证明。依据`.tools/life-power-flows-1011/audit.json`。

2026-10-11 续战/探查反例：军队生成被翻倍后会实际询问选一个，另一0/0在结算后正常死去；不能按原单衍生物自动假定无窗口。生物咒语与后续同来源ETB触发共用sourceID，响应来源离场须等来源真正进场/剩下触发，不能看到stackCount1就当ETB。线索牺牲付款另有真实确认。保存两续战结果及Will接受成功前缀，只补未完成尾段。全屏文本可能从stack/card规则含精怪/探查误判问句翻译，须读取实际Instruction/ValueTitle，固定字体且测完整高度。日文可选探查原英文与续战参数问句实测后只补对应消费者，不重跑原生。依据`.tools/token-explore-flows-1011/audit.json`。

2026-10-11温习原生反例：Sideboard原牌公开后移入Hand，不属于既有Library记忆路径；实际对手确认后已知身份仍应保留，受控温习则真实排除整个Sideboard。记忆只从原生reveal实际接收者进入，不能扫描隐藏备牌。UI同InputSelectEntities并不固定用CardSelection：可选手牌先暂存再确认；已选ok可绑定既有阶段位置主按钮；optionalReplacement用ConfirmOrder。先检查真实活动树和合同，保留成功前缀，只补失败差异，不把测试节点假设改成生产UI。Face-down card是公开占位名，不等于泄漏：另查knownFaceName/printingKey的席位权限。替代结果的初始预期未经规则确认时保持原输入开放，不能按猜测改引擎。依据`.tools/selection-results-1011/audit.json`。

2026-10-11选牌尾段反例：同名基本地同时在战场和手牌，弃牌必须用实际候选/原Hand ID，不能按牌名取第一张。原问句包含整卡正文（正文也含discard），决策识别应核对实际候选牌区与最后准确提示，而非contains。X真实ChooseNumber是增减/确认范围，不是固定枚举。零库显化Watcher在MAIN2结束时只等当前真实优先权，错误等待MAIN1导致稍后空库输局不否定此前完成结果；原request55及快照独立补证，不重跑两施放。Priest替代的中途放逐与最终入墓分别由原生事件/CR同效果公开区域追踪核对，保留旧错误预期。依据`.tools/selection-results-tail-1011/audit.json`。

2026-10-11 N11反例：SelectStackTarget原牌点选只是草稿，必须沿更新请求ok确认；再次点同牌会取消草稿，驱动不能当产品卡住。实际0/0SBA之后castSA自然重置，原同栈X0与最终入墓分别核对，保存原FAIL不重跑已经完成零费施放。source.manaCost可转换X而originalManaCost才是印刷基线；真实扣费从原资源/付款记录核对。TMP preferredHeight含字体行度空间，不能仅按它判字形裁切；测实际visible character底顶在Rect内并检查规则偏移和截图后决定是否产品修复。本批原区域几何完整，无生产改动。旧审计内活的Assets缓存引用允许与现版本不同，保存旧/现哈希和依赖限制；不可把它们当不可变结果而强迫效果重跑，也不可改写旧身份。依据`.tools/branch-x-flows-1011/audit.json`。

2026-10-11 N12跨回合验证反例：刚结算后等待同一玩家MAIN1会立即满足，不能证明经过下次重置。验证下一己方重置时先保存对手回合检查点，再保存回合数增大的己方MAIN1；核对原对象ID、时间戳、附着、跃离与held状态和无换区事件。Oubliette本批跨真实重置后仍保持、来源离场再横置返回已按此方法取证。Time and Tide只让带跃离异能的场上生物跃出，按实际AllValid选择对象，不能期待所有生物都跃出。原错误驱动FAIL保留；有完整末态则独立核对，补缺失尾段时记录原进程已终止且没有原生恢复检查点的限制。依据 .tools/look-phase-flows-1011/audit.json。

2026-10-11 N23法术力尾段反例：匿名AbilityStatic的类名可为空，按实际instanceof/静态操作旗标及成功支付判断；同一个Mana对象可能代表多点，池差量用多重集，并区分原生复制的AbilityManaPart与各自lastProduced历史，不能累加旧生产记录。原七个命名产费替代中的旧数量证明先查登记复用，真实ChooseOptions、确认和RE原ID按冻结合同处理；驱动原FAIL与成功前缀分开，新驱动508保存合同检查不算新游戏。提前结束战斗漏正常onPhaseEnd池清空是真缺陷，局部修复沿既有保留/转换/燃烧路径，真实付款/跳过步骤负例分别取证；发布只新增两受测class，全库存其余身份保持。

N23 UI增量：RectMask外glyph没有mesh是离屏驱动假设，改查可见范围，保留失败不计PASS。旧UI只留源码哈希时不得声称方法逐字未变；记录匹配分支及缺失源码限制，只补当前三/四项实际排序合同。生成规则先读实际wire精确正文（Deep Water为裸U），仅翻译确切三条消费者并复验两原请求三语言/最大字号，未知扩展正文保留。依据`.tools/primitive-api-tail-1011/audit.json`；原最低输入仅核销11，原生结果、UI、离线合同、远程网络各自口径保留。

2026-10-11 N24特征/待机御火/取消费用反例：自然类型/颜色基线读取本牌原生 intrinsic CDA 表并保留 timestamp/staticID，外部 CDA 形状不自动算本牌自然值；原生 canBlock 与公开详情分别验证。回滚按真实 GameSnapshot.assignPlayerState 重建 addSpellCast 导致的本局/成就重复计数定位，保留原完整堆叠源/目标/队列；局部事务同时恢复 hidden keyword 表。手动合法法术力动作的 fallback 保留付款及立即触发，local 完整逆转则一同恢复，不能混成一种撤回。

N24验证方法：WrappedAbility 精确解包读取原实质异能，空 MapParams 不能当原生无异能；合法阻挡选择确认后继续原 ok，重复点选错误单列，已完成前缀冻结。源码复用提取须锚定方法声明，第一次名称命中可能是调用；本批四个旧渲染方法确切相同，新增 cannot-block 句子取三语言/到期/原始状态差异。真实 wire 是 request/seat-view envelope，按原 seat 合并再呈现；InputConfirm 带 previewCards 时核对实际 CardSelection，不假定 ValueTitle 控件。保留驱动失败，只续差异。

未来驱动成功依据为真实终态 PASS_BOUNDED 且无 violations/failures，再输出成功标记并以0退出；重复审计调用、适配器、UI及前置/失败付款各自计数。依据 `.tools/characteristics-keywords-payments-1011/audit.json`：10原输入、14流程40末态、28正mana付款55mana另1次T、66UI；Goal仍 active，其他生产者/表达式/远程与指挥官范围保持原归属。



2026-10-11 N25保存值反例：Spark Fiend原生骰子4+4为8，CountSVar却把纯数字Result消费为0；旧驱动只比后续Safe=0，形成两个错误零相等的假正例。先断言实际dice/Noted8，再证明匹配归零及后续不再掷骰；仅重验原失败场景，旧Number$/Count$与超int回退用33行新旧适配器差异检查。同宿主Helm重置取真实连续activation，独立replacement尾段保持独立身份，不拼同局。依据 `.tools/repeat-reveal-names-1011/audit.json`，实际结算、零付款适配器与UI复用分别登记。

2026-10-11 N26测试前提反例：en/ja不是本项目正式Locale，SetLanguage未切换时旧截图仍为中文；仅复用其中文布局，不授外语PASS，改读en-US/ja-JP并断言实际SelectedLocale后补差异。进入Play后等待资源Ready，再采样真实Graphic绘制；原Text控件不按TMP猜测。JsonUtility克隆null请求可得到seat0/kind空的默认对象，非操作方权限按awaitingInput/seat核对。原FAIL及成功双门点击前缀保留；native后段驱动先用保存的真实模式请求核对已选→confirm-cards，再只补缺失的实际尾段。原生CardView含关键词的全文与wire的trait文本/关键词分列对齐，不能混用检查点或字段；当前规则中的strike片段是旧词历史，实际目标与显示正文分别验证。依据 `.tools/layers-spellbook-reconcile-1011/audit.json`；7原ID完成、1资源partial，各native尝试、只读adapter与UI独立计数。
2026-10-11景点奖励反例：触发异能正在选目标时可能还不在堆叠；从实际当前InputSelectTargets的SA只读观察准确Card／Player，不凭空堆叠判事件缺失。原生衍生物名可能带Token后缀，同名施放必须用原实付source ID定位，不能取剩余手牌的第一张。驱动因此失败时保留原FAIL，离线复核已冻结的真实末态；资源不足前未启动的操作不计支付。需要续跑时将已观察状态写成新历史的开局前提，明确不能拼成同局。辅助牌组夹具通过不授予生产特殊模式接入PASS。依据[景点奖励增量](../../../../.tools/payload-tail-1011/audit.json)。

2026-10-11 N31融合反例：共享GameAction整类重编译前叠加已发布Craft修复，逐项核对旧两段修改保留；仅追加融合拆分分支。牌库与坟墓场索引方向分别核对，不能复用同一插入条件；坟墓场排序还须经过原生顺序敏感牌及玩家偏好门槛。setTokenCard不等于真正衍生物，使用GamePieceType.TOKEN；真实Clone复制后Original字段和cloneOrigin可能重建，按实际施放、选择及物理ID追踪。驱动失败保留原结果；冻结原生成功前缀与独立尾段分别审计，不能拼为同局。共享Bridge变化使旧Clash适配器分类守卫失效时，保留旧身份并归A06待审查，不授当前PASS。依据 .tools/meld-tail-1011/audit.json、source-snapshot-review.json 和 registration.json；仅关闭普通Meld最低输入与S01.02，其他背面效果及远程联机不随之核销。
2026-10-11 N32 AWOL真实反例：先ChangeZone再RemoveFromGame的子链仍保存移区前目标LKI，直接ceaseToExist旧对象只从旧战场再删一次，当前对象留在Exile。按Game.getCardState(target,null)取当前物理对象再移除，保留原生目标合法性与skipTrig。只读观察器追踪消失对象须区分getCardState默认回退旧对象与明确null；旧指针的zone文字不证明仍存在。wire中graveyard是摘要、graveyardCards/exileCards才是卡数组；驱动误读导致最终FAIL时，完整真实结果和付款先冻结，再独立核对准确双方DTO，不重跑已完成施放。依据 .tools/removed-object-1011/native-final.json；目标实际响应回手使AWOL失效、双方公共区域及下一优先权单列证明。25实际UI检查不充当远程联机，单native override其余原运行文件字节保持。


2026-10-11 N33方法维护：原工作表顶端仍写N24的88/125、9事项，而机器账已108/125、当前7。同步当前进度并明确旧段为历史；skill仅增加登记后同步便读段，未新增规则回归。翻牌结果首次测试前缀中文完成，英文仅因大小写判断失败；保留前缀，只补英/日。截图另发现Outline整块遮图、分组销毁旧Count和布局后缺少高亮绑定，分别用边框和分组适配器补差异；旧原生规则结果不重跑。依据 `.tools/flip-feedback-1011/audit.json`。


2026-10-11 N34实际同局重开增量：原生卡恩触发startGameHook再次执行，夹具仅首轮布置，避免验证时重新注入旧场。实际延迟触发由1归0，playerDefinedDelayedTriggers为真实空对象字符串{}；原FAIL保留，准确末态独立审核，不为格式误判重复已完成流程。UI分页清除后会合法建立0页，断言检查旧页值已消失；文本mesh完整不证明可见，截图发现重开通知被对面手牌遮挡，仅改重开位置并补23位置/文字检查，沿用179原交互证明。完整可变状态差异用真实Turn to Frog→Kiki复制→卡恩同局重开，未用新进程拼接同局证明。子游戏恢复范围依据明确排除清单后置，不记PASS。证据 `.tools/restart-series-1011/audit.json`。


2026-10-12 N46跨局物理身份：跨局卡池夹具使用真正注册主牌/备牌中的原始对象，先核对主备总复制限制；临时生成同名Card不能替代collectible/PaperCard/原owner证明。移除事件在cease之前保留精确物理ID、版本、原区域和库存标记，外层BO3按这些数据消费，保存卡组/收藏只读。实际原生结果与host消费者通过冻结ended连接时分别标明原进程和后续生产worker，不拼成同一原生Match。原主备五张同名夹具结果保留，生产流程改为合法四张总数。JsonUtility可把null ownDeck恢复为空对象，观战者按无私有条目检查。首次Create早于Show载入卡组的空重置基准是生产缺口；重载脚本先AssetDatabase.Refresh并核对程序集已更新，再只补失败的UI尾段。未刷新造成旧代码重复FAIL的两次尝试保留；清空牌池用显式adapter，后续真实native判负单列，不伪称实际移除了六十张基本地。依据 `.tools/series-removal-1012/audit.json` 与 verified.json。


2026-10-12 N47赌注归属：原生ChooseOptions的select:i只是切换草稿，沿confirm-cards确认；InputConfirm的Sacrifice提示是成本确认，不当成可选追加赌注而取消。对象类型用原生候选核对（Refurbish返回神器，Reanimate只返回生物）；支付分支在已有非零归属历史之后检查保持该历史，不误要求变空。Match可能已结算AnteResult，测试oracle先读已有结果，生产不发布反射oracle。原生注册身份、实际规则结果、冻结ended的主机消费与正常后续worker分别留证，赚到第五张只在主机精确授权卡池中放行；原owner在交换后移除时按开局注册身份扣除，转移和移除同ID拒绝。通用多选窗口实际走GenericChoices文案，不凭RequestOptionLabel新增分支就假定绘制已使用；MenuVisualTheme会覆盖初始位置，检查最终主机房间设置截图并只补布局消费者。旧场景仅有迁移提示时接受夹具创建真实控制器，旧wire先走生产Poll合并请求；缺少正常大厅service的孤立夹具先按生产EnsureInstance初始化。房间创建先展开局域网入口、等待发现完成/控件启用；同面板重开保留用户选项，不能反复要求默认off。说明文字允许换行时按实际高度和截图判断，不拿未换行preferredWidth判裁切。原FAIL/成功前缀均保留，不为驱动修正重跑已付费流程。依据 `.tools/series-ownership-1012/verified.json` 与 audit.json。


2026-10-12 N48范围决定：用户明确后置Planechase/Vanguard/Archenemy/征召笔记/密谋牌。原6个A01输入、S04混合输入中的Planechase分支及全部原断言保留scope-exclusions.json；游乐设施和装置仍为普通1v1。原生成器固定仅允许两个排除类别，且把后置输入仍算当前未完成；改为校验新增类别的明确authority/resumeWhen，并分别输出原始未核销、当前待做、后置数，禁止后置同时计完成。114/125效果完成数不变，当前待做从11到5，另6未验证后置。只执行计划/格式与计数反例检查，不重跑机制。依据`.tools/ordinary-scope-1012/audit.json`。


2026-10-12 N49装置驱动反例：攻击选项的defenderKind筛选排除了attack-remove，导致同一攻击者反复指定并生成31,303份冗余快照；不是规则死锁。按实际attack-remove确认已选，并限定一次指定/一次确认。完整trace/result经逐字SHA核对后无损gzip，保留最初三与最后两次请求和逐文件淘汰manifest，释放约3.58GB。原零启动/Cogmentor实付4前缀保留；后继准备开局不得拼成同局。主动推进选择全部产生OrderAbilities时，须先按order:i排完才会出现confirm-order。对未知合同冻结请求并短时保留原宿主，按实际启用选项准确回应；Copy Artifact的成本已付后保留原宿主完成确认/复制，避免重做付费前缀。新控制者更换齿轮组有原生-1重置，可选自己的全部三组；普通Cogmentor同控制者只能另两组。UI对象真实根为Card_ID，最大字号badge/三组计数高度分别按实际preferredHeight调整，原失败保留。证据`.tools/auxiliary-lifecycle-1012/audit.json`。

N49登记器导入copy时，工作目录里的copy.py遮蔽Python标准库，误启动原生驱动。改名为copy-rigger.py并更新生成器/未来正式入口；只停止确认为本批的Java及其登记器父进程。误启动宿主不计结果，保留原已完成Copy/Rigger实付7结果。后续驱动文件避免与实际用到的标准模块同名；正式入口按新目录数量检查一次只启动一局。


## N57：结果标签缺失不重新开测试

触发事实：N56只从A01的api-*完成标签定位旧范围，77种共享实现中仅20种有候选，遗漏基线实际PASS日志、552原首验断言及专项闭合。修正后46种由正式API标签关联，另31种引用原A01／准确专项结果，77种全部有旧有界范围；212重复效果链／读取字段只共享核对引用。检查实际证据哈希、排除adapter-only结果，保留31准确原声明结果关联，不把共同API、212别名或842核对上下文提升为整牌效果PASS。剩余补跑只由具体未匹配结果／权限／生命周期／UI差异产生。用户将外机验证自行承担和全局A06核对后置，不能继续把两项当完成门槛；本轮局部资料／Unity检查与原影响记录仍保持。详见Docs/CurrentPayloadWork.zh-CN.md与.tools/incubob-art-and-reuse-1012/audit.json。

## 2026-10-13 生成异能与编辑器旧程序集

触发事实、错误决策、修正及真实原生/UI检查见 [交互修复记录](../../../../Docs/InteractionPresentationFixes.2026-10-13.zh-CN.md)。源卡译文与旧截图不能覆盖带 CARDNAME/事件后缀的实际文本；刷新成功不能替代脚本编译身份，不能将仍显示的禁用堆叠异能算成清晰目标选择。只验证受影响真实分支，不扩成全卡重复回归。


2026-10-13 快照序列化反例：133025 双客户端建房/开局驱动报告 PASS，但完整日志分别含八个 Unity DTO 深度警告；驱动只统计 Error/Exception，漏掉 Warning，独立审计标 FAIL，不改原始报告。进一步核对携带快照的四类消息（开始、更新、重同步、结束）、结束拷贝/历史及卡牌 UI 签名/公开区域/录像拷贝，统一字段序列化；驱动补警告门槛及真实结束快照/隐藏手牌断言。只重验此传输消费链，不重跑八个原生规则案例。检查结果和最终发布身份见 Docs/InteractionPresentationFixes.2026-10-13.zh-CN.md 与 .tools/interaction-fixes-1013/final-shipping.json。


2026-10-13 位置确认角色反例：旧锐敏样本取消目标，未实际进入对手咒语拥有者的牌库位置决策，不能覆盖其预览身份。真实 Air Elemental 施放→对手锐敏呼魂→命中该咒语复现预览来源锐敏；只改 ChangeZoneToAltDestination 的唯一受影响目标预览，保留 native decider/ok/cancel，真实置顶/置底各一次通过。提示卡名采用准确预览对象，不按正文中的印刷别名猜牌名。独立场景UI截取只清 stackItems 而未清 legacy stack/count 会绘制旧英文fallback，属于构造的矛盾快照，不作产品失败；纯字体边界样本同步清三字段。初次按钮查找误用 Text 子项，改按原生索引对应 SelectionAction 控件射线，原FAIL保留。证据 .tools/choice-presentation-1013。
# 2026-10-13 已有译文关联的显示反例

已有中文不能按缺翻译处置：真实走骨行尸的免付费用／结算附加提示使整段规则匹配失效；副牌面按物理印刷主牌面查找同样会遗漏。按公开当前牌面关联原文与译文，附加事件／费用独立保留；完整规则匹配不依赖两种语言的编辑分行一致，子段关联仍需准确且不歧义，不泛化删除条件或数值。证据 `.tools/known-rule-language-1013/audit.json`，25项显示检查，不代表新效果通过。

正文断言定位准确控件，不能只搜全Canvas任意中文（会命中独立样本／上下文）；关键词费用另外定位 ExtraCost。CardSymbolText.Format 使用PUA字符映射现有图标，不能假设必须有 `<sprite>` 字串；以实际Format结果、准确TMP正文及截图核对。原角色猜测、控件范围过宽和图标编码断言失败保留，不改产品去迎合错误夹具。

2026-10-13 补充反例：Pinnacle 选择正文中文，但 FaceName 和 Type 仍直接使用 native 字段，且费用模式标题后来被共用“施放”覆盖。已有正文 PASS 不覆盖卡名、类别、模式标题或多行追加说明；按准确控件分别采样并复核截图。费用模式读取现有关键字表及准确提醒文字，不把普通牌面元数据套给不同费用的原生选项。动画在调度点与稳定帧分别采样，冷加载结束后的过期帧不能证明没有动画。外部卡组编辑使用项目真实 !side 格式；原子快照和实际菜单入口分开验证，两个入口要读同一持久档案且不同会话卡池。见 Docs/LiveGameplayFixes.2026-10-13.zh-CN.md；只跑受影响消费者，不复跑已通过的规则生产者。



## 2026-10-14 局后联网驱动增量记录

复用旧 EndReviewValidation 时，接收器跳过生产 OnlineEnvelopeCodec 解码，且假设未携带的私有数组一定非空；压缩封套报 JSON 首字符 H，随后观众结果为空。另一项 Canvas 名称查找错配了实际控制器拥有的画布。前缀、FAIL 与准确源码已保存在 `.tools/live-fixes-1014`，没有将这些驱动错误改成生产规则故障。

修正本批接收器为生产解码／字段序列化路径、缺省私有数组的空值合同，以及控制器实际窗口引用；把无关牌库分页移出本轮局后导航检查。新驱动逐项保存检查，等待条件超时明确失败。真实成员退出后还发现目录的 0.2 秒缓存保留旧状态，生产 BroadcastLobby 事件已令缓存失效，重新验证局间／残局状态与晚加入。

依据：`.tools/live-fixes-1014/network-compressed-payload-fail.txt`、`network-null-array-fail.txt`、`network-canvas-lookup-fail.txt`、`network-directory-after-fail.json`、最终 `end-review-manual-result.txt.checks`。保留原始验证身份，不为付款／产色已通过结果重跑，只补本次消费者和导航差异。
## 2026-10-22 独立打包与实际发布状态

本次先按用户要求独立打包，用户随后再授权覆盖客户端、更新服务器，并补充不用备份。原发布引用只详细描述累计合并且固定要求备份，缺少独立增量基底与分阶段状态，容易把“包生成”当作已经安装或部署。现补齐独立／累计分支、协议兼容、实际生效状态与当前不新增备份归档的偏好；覆盖前已完成的本轮临时备份后来移除，已有版本与新增备份分开处理。以本轮独立包59项、安装验证及Linux23部署记录核对这些分支；本次 skill 更新只做格式、引用与源码检查，不重跑已经通过的对局。见 [发布记录](../../../../Docs/Release.2026-10-22.zh-CN.md)。

## 洗切与起手统计

本项目洗切沿用用户选择的有限分堆、插洗与切牌，模型及次数从当前原生实现／审计读取；统计不添加按牌种筛起手或补偿地数的生产逻辑。固定实际保存的逐张牌序、卡组身份与运行身份，按用户要求确定样本数，种子在运行前保存；比较模型或调度时复用同一组种子，保留每手结果。

起手目前按用户口径记录0～1地、4地及以上，5地及以上另作细分；同名牌跨印次合并，同时列含地／仅非地、最大重复张数及多对组合。调度规则与放回选择单独声明，沿用当前牌库及随机流，记录重抓、最终张数和地数；阈值调整只重汇总已有原始记录，不能擅改调度或生产算法。具体策略与本批身份见 [含调度统计](../../../../Docs/PileMashMulligan10000.2026-10-22.zh-CN.md)。

大量样本在单个原生Game反复实抽会累计不属于独立起手的对局历史；可逐次运行原生洗牌、记录顶部手牌，并用有界真实抓牌及原生调度／放回流程核对。分开报告模拟次数与真实交互次数，保留夹具失败及已成功前缀。起手地数不代表保留后的断地、连续抽地或整局卡爆率，统计策略也不是客户端自动调度功能。

2026-10-22 skill 引用检查发现主文件11条历史证据链接和方法文档1条链接的父目录层数错误；目标原文件均存在。按各文件真实位置修正相对路径，核对新旧引用目标，不重跑已经有原证据的机制。
