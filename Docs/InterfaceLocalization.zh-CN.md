# 界面语言与动态 Forge 文本

界面语言以 Unity Localization 的 SelectedLocale 为唯一来源，支持 zh-CN、en-US、ja-JP。Forge 的协议仍使用规范英文，选项编号、请求消息和分支判断不因客户端语言而改变。

## 共用入口

- `GamePreferences.T`：静态界面和带参数的界面提示。参数保持原样，避免把玩家自行填写的牌组名称当成术语翻译。
- `GamePreferences.NativeText`：只处理对外展示的原生文本；翻译完整格式和公开卡名参数，保留数字、重复参数及法术力花括号。
- `ForgeBattlefieldController.NativeDisplayText`：在接收者已经可见的对象中匹配准确卡名和编号，复用现有卡牌规则翻译，再翻译选择指示。不查询背面隐藏对象的印刷规则。
- `PreferenceText`：统一 uGUI Text 与 TMP 的字体、字号和语言刷新。保存原始字号，不重复乘缩放；输入框和 Literal 用户名称保持原文。

`Interface.tsv` 和 `RuntimeInterface.tsv` 保存本项目的词句。`ForgeInterface.tsv` 从当前固定 Forge 版本的语言资料导入，三者生成同一个 Unity Interface 表；不另建运行时语言开关。旧式 IMGUI 提示也使用此入口与语言字体。

## 更新与检查

1. 用 `python -B -X utf8 Tools/Localization/ImportForgeInterface.py` 更新导入词库。源文件哈希、缺少译文、冲突和无法安全恢复的格式保存在 `ForgeInterface.import.json`。
2. 用 `python -B -X utf8 Tools/Localization/AuditInterface.py` 检查重复键、空译文、参数一致性，并盘点归档请求。泛用词库未匹配的原文不自动判为展示失败：其中还有卡牌规则和已有的专用消费者。
3. 在 Editor 调用 `PresentationLocalizationSetup.Build()`，再构建 Addressables 内容。生成器先校验，且删除已经退役的键，防止不安全的旧格式继续匹配。
4. 新机制展示验证记录 `GamePreferences.UntranslatedNativeMessages` 和 `UntranslatedInterfaceMessages`，区分公开卡名、印刷卡图、现有规则消费者和真正漏翻译的指示；短原生提示与英文界面里漏出的中文同样记录。修后按消费者验证三语言及最大字号。

组合提示中的固定角色、阶段和状态先调用 `T`，再与玩家名称、牌组名称等原样参数组合。玩家名称绑定 `Literal`，空席位恢复普通语言绑定；输入框正文保持用户原文。

只有完整格式才替换。未知文本保留含义，并留下漏译诊断；不把未知的强制选择改成泛泛的“选择一项”。相邻无分隔参数不能可靠从英文拼接结果恢复，不能猜测拆分。卡图内文字由所选版本决定，不属于运行时界面译文。

本轮证据在 `.tools/language-unification-1010/audit.json`：2,765 个键三语言完整，8,295 次准确键检查、990 次动态参数检查；真实确认、数值原编号、跨区域选牌、CJK 字体、字号不累乘、用户输入、公开源规则绑定和房间角色／准备状态已验证。新增词句按差异验证，旧合同引用原证明。此证据不代表所有自定义规则正文、卡图语言或真实远程网络均已覆盖。
