# 衍生物悬停与退出清理回归（2026-09-30）

## 复现与修复

- FRA/363（The Theorist, Jace Beleren）关联的 Illusion / TFRA/4 正确，但手写类型表遗漏 Illusion，中文正文「虚影」没有链接。
- 新增从本地牌面类别与中文类别生成的 561 项映射，作为中文衍生物类型名称来源；保留旧译名表兼容。生成时按不同牌名计票，拒绝不明确的对应，不让少量类型顺序错误覆盖常见译名。专名仍按卡牌关联处理，不将任意卡名当成衍生物。
- Build-RelatedTokenIndex.py 现在同步重建 TokenTypeAliases.json；可以单独运行 Build-TokenTypeAliases.py --check 检查资料是否过期。
- 退出异常的实际调用链为 MainMenuController.OnDestroy → DeckHoverArtPopup.Hide → 已销毁的 gameObject。将弹窗调用点改为 Unity 对象存活判断，关闭方法同时能容忍销毁后的重复调用；禁用时取消计时与加载回调。
- 换卡时拒绝来自上一张卡的悬停回调；衍生物名称提示只显示卡图，不回退到空白关键词说明框。囤兵关键词仍显示卡图与机制说明。

## 验证

- 修改前：FRA/363 中文虚影链接数量 0；销毁 popup 后 Hide 确实触发 MissingReferenceException。
- 修改后：KeywordReferenceValidation.Run 通过，包括虚影、象、分灵、变形兽、惊惧兽、害虫、僧侣以及此前元素/关键词边界回归，及销毁后的重复 Hide/RequestHide。
- 通过实际 TMP 指针坐标悬停「虚影」，显示 TFRA/4 本地卡图，不含 KeywordHelp。截图：.tools/token-hover-fix/illusion.png。
- Play Mode 验证换卡关闭、旧卡回调拒绝、收起左栏关闭、禁用后延迟关闭状态清理、囤兵灵俑卡图与说明共存。
- 分别在虚影弹窗和囤兵弹窗打开时停止 Play Mode，清空 Console 后停止，均没有新的错误。

覆盖范围为上述类型和生命周期场景，不能视为所有专有衍生物名称、所有语言与所有卡牌逐项验收。缺失本地卡图继续显示缺图；没有引入联网加载。
