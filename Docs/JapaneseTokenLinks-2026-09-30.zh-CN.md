# 日文衍生物名称提示检查

检查范围：本地 RelatedTokenArt 关联索引、Forge 牌面文本、日文基础与补充翻译。按牌名去除重复印次，扫描了 3,729 个含日文「トークン」文字的牌面。这不是对所有翻译或卡图是否齐全的保证。

## 修复

- 补充衍生物专用类型、旧写法与特殊名称的本地日文别名。
- 双面衍生物分别匹配各面名称，包括培養器和角色。
- 从本地牌文提取特殊名称；只接受一个英文关联名称和一个日文名称的明确配对。
- 识别引号中的完整异能，避免其中的句号截断外层衍生物描述。
- 识别生成衍生物后列出的模式选项，以及 PDF 排版造成的名称换行。
- 用本地缓存的正式 printed_text 修正 Leering Onlooker 的机器人误译，以及 Wolverine Riders 遗漏的精灵类型。修正清单只替换精确匹配的旧文本。

修复后，离线候选检查匹配到 3,848 个牌面与衍生物关联。余下 93 个未直接匹配名称的关联已归类：53 个同牌复制，18 个徽记，8 个调查产生的线索，8 个其他牌面或类型描述关联，6 个没有独立名称的复制关联。未发现未分类的名称遗漏。这些关联不能简单当作应该新增的名称链接。

## 验证

- KeywordReferenceValidation：中、英、日文名称匹配、无关类别的负例、特殊名称、双面衍生物、引号中的异能、模式选项、换行均通过。
- 日文实际界面检查：Genku、Outlaws' Merriment、Bloodtithe Harvester、Cogworker's Puzzleknot、Minsc & Boo、Mishra、Leering Onlooker、Wolverine Riders 的对应名称已正确关联。
- 实际悬停 Outlaws' Merriment 的「人間・戦士」，确认显示对应的本地衍生物卡图。
- Unity 无错误日志。卡图仍仅从本地读取。

可重复检查：`python Tools/ForgeBridge/Build-TokenTypeAliases.py --check`、`python Tools/ForgeBridge/Audit-JapaneseTokenLinks.py`，以及 Unity 中执行 `KeywordReferenceValidation.Run()`。

离线检查是名称覆盖候选审计；实际交互由 Unity 验证。报告：`Tools/ForgeBridge/Reports/JapaneseTokenLinks.json`。
