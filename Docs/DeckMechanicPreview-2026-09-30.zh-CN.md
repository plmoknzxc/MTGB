# 卡组编辑的地城与魔戒预览

## 展示

- 收起左侧详情后，悬停大图包含主卡、另一面、关联衍生物及地城／魔戒辅助图；侧栏只说明匹配到的关键词机制。
- 地城一次展示一张，标题显示当前序号和总数。鼠标保持在来源卡、左侧关键词或弹窗上时，右键循环切换。持续悬停和重复指针移动不会重置所选地城。
- 辅助图顶部使用居中凸起的黑色圆角标签，白色粗体名称，地城序号单独置于名称下方；图外围为黑色圆角边框，不显示“右键切换”提示文字。魔戒只显示名称。
- 左侧规则文字中的“深入地城”“先制”“深入地底城”“魔戒引诱你”沿用关键词链接样式。悬停这些词时显示相应辅助图和机制说明。
- 整张深入地城牌按当前卡组赛制预览：普通赛制三张原版地城；指挥官、决斗指挥官、Brawl 四张。明确传入当前卡组赛制，不读取全局默认赛制。
- 关键词链接按具体机制筛选：“深入地城”始终三张原版地城；“先制／深入地底城”只展示地底城。整张牌同时含两种机制时展示四张。
- 四张预览只是参考，不是可选地城列表。地底城并非指挥官专享，普通深入地城不能从游戏外选择它；深入地底城可以进入它或推进当前地城。
- 所有辅助图复用本地 `SpecialEffectArt` 索引，缺图显示缺失提示，不发起联网请求。
- 简体中文界面使用威世智官方中文素材：四张地城，以及魔戒正反面，共六张，位于 `CardImages/zh-CN/`。其他语言沿用原有素材。导入脚本为 `Tools/ForgeBridge/Import-LocalizedHelperImages.py`，来源与校验摘要保存在 `Tools/Validation/Reports/SpecialEffectAssets/localized.json`；英文素材重新导入时保留中文索引。
- 标题、地城序号在创建时立即设置字体缩放基准，标题标签高度同步缩放，避免首次显示后全局字体设置再次调整字号。

## 规则来源

[威世智：指挥官传奇争战博德之门机制](https://magic.wizards.com/zh-Hans/news/feature/commander-legends-battle-for-baldurs-gate-mechanics)

[威世智：被遗忘国度战记机制及中文地城图](https://magic.wizards.com/zh-Hans/news/feature/adventures-forgotten-realms-mechanics-2021-07-02)

[威世智：魔戒中洲传说机制及中文魔戒图](https://magic.wizards.com/zh-Hans/news/feature/the-lord-of-the-rings-tales-of-middle-earth-mechanics)

## 验证范围

- 使用实际目录中的 AFR/218（Barrowin）、CLB/49（White Plume Adventurer）、LTR/100（Nazgûl）验证三张／四张地城和魔戒本地图。
- 新增验证：普通整牌三张、指挥官整牌四张、指挥官深入地城关键词三张、先制关键词一张；六张中文素材均可本地加载。130% 字体缩放下首次显示、全局字体绑定和切换地城字号一致，并核对弹窗图片与中文本地 Sprite 一致。结果保存在 `.tools/deck-mechanic-proof/localized-result.txt`。
- 验证单个地城图位、循环回到首张、重复左侧悬停不重置、普通卡名不会误触发辅助图。
- 使用 Input System 注入右键按下／释放状态，验证来源区域内右键切换、按住不连跳，以及移出后的延时关闭。结果保存在 `.tools/deck-mechanic-proof/result.txt`。
- 关键词构建检查通过：286 条帮助、200 个 Forge 关键词、264 个 CR 标题。
- Unity 编译及运行时专项检查；验证的是编辑器展示，没有修改 Forge 的地城规则或选择流程。
- 中文素材接入后完整项目校验通过（`Temp/project-validation-result.txt` 为 `PASS`）；英文辅助素材重新导入仍保留六条中文索引。

## 截图

- `.tools/deck-mechanic-proof/venture.png`：收起左侧后的主卡＋中文地城＋机制说明。
- `.tools/deck-mechanic-proof/ring.png`：主卡＋中文魔戒＋死触和魔戒机制说明。
- `.tools/deck-mechanic-proof/left-dungeon.png`：此前左侧详情的地城辅助图验证截图。

截图通过运行时调用同一展示接口生成，不是 HTML 原型。
