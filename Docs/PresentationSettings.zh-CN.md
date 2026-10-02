# 界面语言与文字大小

## 使用

- 主菜单「游戏设置」、卡牌详情顶部「设置」打开同一个全局面板。异画面板右上角只保留「关闭」。
- 简体中文 / 日本語 / English；文字大小 85% / 100% / 130%。金色按钮为当前选择。
- 设置即时生效，使用 `UI.Language`、`UI.TextScale` 保存，下次启动保留。
- 改语言不会改变卡牌身份、规则数据、牌组文件或异画偏好；搜索同时支持当前译名和英文原名。

## 实现与资源

- Unity Localization 1.5.13，SelectedLocale 是唯一语言状态；Interface String Tables，Presentation Font Asset Tables。
- `Assets/Localization/Interface.tsv`：源文字、英文、日文三列；四列形式为源文字、中文、英文、日文，适合英文源文字。占位参数使用 `{0}` 等。重复源键以最后一条为准。
- `MTGB/Localization/Rebuild presentation tables` 更新表；更新后需重新执行 Addressables BuildPlayerContent。
- GamePreferences 异步载入表、字体及译文。临时 Addressables handle 在跨帧读取期间持有引用，最后释放。
- PreferenceText 保留原始文字与基准字号，避免来回切换累计放大；不会翻译输入框内容和自定义牌组名。
- 官方译文复制自当前固定 ForgeRuntime 的 `assets/res/languages/cardnames-zh-CN.txt`、`cardnames-ja-JP.txt`，置于 StreamingAssets/CardTranslations。按英文原名投影名称、类别、规则文字，不写回 CardDefinition。
- Noto Sans CJK SC/JP 字体来自 https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF ，许可见 Assets/Fonts/Noto-OFL.txt。使用按语言切换的动态多图集 TMP 字体。

## 边界

- 卡图内印刷文字不随 UI 语言改变，仍按用户选择的版本显示。
- Forge 缺失或过时的译文不会自动联网补译；缺少条目时显示原始卡牌文字。
- Forge 译文没有风味文字；目前已有的英文风味文字继续显示英文。
- 主菜单、组卡、异画、设置以及常用联机/战场标签已接入。底层异常详情、部分联机动态组合消息和 Forge 任意效果提示仍保留原文，不宣称全游戏所有动态句子的翻译已经覆盖。
- 目前不翻译用户输入的名称；调整字号不放大卡图。

## 2026-09-22 验证

- Unity 6000.3.22f1 编译通过；Localization 和 Addressables 内容构建完成。
- 196 个界面键 × 3 语言 = 588 项，无空译文。
- Play Mode：闪电击 / 稲妻 / Lightning Bolt，各自规则文字正确切换；原始 cardName 仍为 Lightning Bolt。
- 中文卡名搜索通过；无译文测试卡保留原文。
- TMP 中文规则字体检查：缺失字符 0；日文截图可读。
- 85% → 130% → 100% 切换；130% 时牌组标题仍可见，长规则显示滚动条。
- 退出并重新进入 Play Mode，en-US / 130% 保留；验证后恢复中文 / 100%。
- 无 Unity Console error。验证截图：Captures/Settings-Chinese.png、Settings-English.png、Settings-Japanese.png。
- 本次没有重新打 Windows 可执行程序，也没有做联机全流程回归；不能据此声称上述两项已经通过。
