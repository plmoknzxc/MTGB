# 特殊效果展示覆盖审计

## 范围与结论

检查固定 Forge 版本中的 Command 效果对象、延迟触发储存及两席公开投影。源代码扫描找到 30 个延迟登记位置。图谱不包含 `.tools` 上游源码，因此这些结论来自直接读源代码和实际运行测试。此清单不是所有万智牌效果或所有牌的规则认证。

## 已补齐

| 引擎类别 | 原缺口 | 当前处理 |
|---|---|---|
| 普通延迟触发 | 已接入 | handler 延迟列表 |
| 本回合限定触发 | 普通列表已包含 | 去重导出，清理时移除 |
| 指定玩家下回合 | 已接入 | 按玩家登记列表 |
| NextTurn / UpcomingTurn | 清理阶段回调执行前漏显示 | 只读取阶段命令捕获的 Trigger，不执行回调 |
| 额外回合 / 额外阶段 | 开始前不在普通列表 | 从等待中的额外回合和阶段读取 |
| 花费法术力附带触发 | 未设置 spawningAbility 时漏显示 | 回退到公开来源的控制者 |
| 君主、主动权、辐射、城市祝福、玩家关键词效果等 | 无 effectSource 时被排除 | 统一导出 Command 中的 EFFECT 对象 |
| 隐藏来源的持续效果 | 原来完全不显示 | 显示匿名持续效果，不下发来源名称、图号或异能 |

不逐牌硬编码；同类效果走同一投影。徽记和效果的当前位置保持用户确认的布局。

## 防漏检查

- `python -X utf8 Tools/ForgeBridge/Audit-EffectMarkerCoverage.py`：PASS，登记入口和储存字段清单输出到 `Tools/Validation/Reports/MarkerCoverage/source-audit.json`。
- `Build-Bridge.ps1` 在构建/打包前运行检查。新增或移除 Trigger、GameCommand、额外回合/阶段储存字段，以及新的延迟登记方法会报错，要求核查接入和测试后更新清单。
- 检查是针对固定上游源码的结构守卫，不能发现所有语义变化，也不替代对局测试。
- `Test-MarkerCoverage.py`：PASS，直接调用真实 Forge 的登记和阶段 API，覆盖 7 路延迟、4 个 Command 效果对象、唯一 ID、两席一致、隐藏来源脱敏。此 fixture 验证投影入口，不声称实际施放了全部代表牌。
- `Test-EffectMarkers.py`：PASS，实际施放并结算流光烁影，待弹回记录出现，下个自己的维持阶段触发后移除。

## 仍明确未覆盖或需另行验证

- 玩家指示物已统一导出并同步给观战者，五种图标已就绪。用户尚未决定摆放位置，因此暂不新增 HUD 位置。
- 本次列明的特殊效果辅助牌面已全部补到本地；未知对象仍显示缺失卡图，不临时联网取图。
- 只存在于任意 GameCommand 闭包中的持续效果撤销逻辑、没有 Trigger 或 Command 效果对象的记录，不会被伪造成延迟标记；场上卡状态通过已有状态展示。尚未逐个验证所有这些修改类别。
- 延迟触发的来源处于手牌、牌库、备牌或面朝下时，触发牌名与卡图不导出；这次未为所有隐藏来源设计匿名延迟提示。
- 未穷举每张卡的规则、场上细节和选择界面。不能据此宣称全部机制已经完善。

Unity 运行验收：11 个效果标记能进入界面；无来源牌的君主详情展示真实异能，匿名持续效果详情不暴露来源。停止运行后 Console 无错误。生产桥接源码、编译类和运行时哈希已同步。


## 素材与遗漏补齐（后续验收）

- 玩家指示物：遍历真实 Player 的整个 CounterType 多重集，全部种类统一导出；中毒、能量、经验、辐射和票券五种图标取自已随 Forge 分发的本地皮肤，保存于 `Assets/Resources/UI/PlayerCounters/`，由 `PlayerCounterIcons` 提供，尚未摆放到 HUD。
- 18 个特殊辅助牌面已下载到 `CardImages/<SET>/<COLLECTOR>`，映射保存在 `Assets/Resources/SpecialEffectArt.json`。包含君主、主动权、城市祝福、辐射、魔戒两面、昼夜两面、速度/极速、四种地城、三种玩家指示物提示牌，以及 Enduring Story。
- Enduring Story 从实体辅助牌的零售展示图补齐，已视觉核对姓名、THOB 14、画家、完整正面；其他辅助牌使用 Scryfall 精确印刷图。各条来源记录在映射和 `Tools/Validation/Reports/SpecialEffectAssets/`。
- 地城并非 EFFECT 对象，新增导出 Command 中 Dungeon 的入口，详情展示引擎的当前房间。已完成地城列表也作为公开状态导出。
- 魔戒引诱次数、持戒者对象 ID、速度数值均已导出。魔戒及速度的标记详情包含当前状态。
- 昼夜状态是全局状态，新增全局效果记录；在自己现有的效果区域显示一次，两席和观战者均能看到。辅助牌图通过桥接明确给出的 helperArtName 读取，不从延迟法术名称猜测，避免普通同名牌误用图片。
- 自动源码审计还会检查 GamePieceType 类型变化，新增对象类型时必须复核 Command 显示入口。

验证：`Test-MarkerCoverage.py --special-states` PASS，真实引擎 API 设置 5 种玩家指示物和地城/魔戒/速度/祝福/Enduring Story/白昼状态，检查两席数值及入口；不是完整实际施放所有相关牌的对局认证。Unity 验证五种图标、双面辅助牌的不同图片、观战投影和地城详情；完整项目校验 `mtgb_validate_project` PASS（临时补齐缺失的 Forge-Validation 测试卡组，校验后移除）。

运行时不联网取这些素材；图集已禁止 NPOT 缩放并保留原始 680×1920 尺寸。卡图和辅助资源导入不改旧 .mtgbdb、Lua 或 Data Editor schema，适用文档中的 Forge 迁移流程，未对旧库运行 --validate。
