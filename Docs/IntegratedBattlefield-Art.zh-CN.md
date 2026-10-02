# IntegratedBattlefield：石台遗迹视觉调整

本轮只面向新场景 `Assets/Scenes/IntegratedBattlefield.unity`。背景为 imagegen 生成的石台遗迹美术，保存于 `Assets/Art/Battlefield/ObsidianSanctum.png`（约 2.5 MB），作为 Canvas 环境层显示。它是固定视角的二维背景，不是新增的三维场景模型。

## 调整

- 保留现有 Canvas 对象路径及 Forge 接口，加入不拦截点击的全屏背景。
- 左侧详情栏重新安排卡图、编号、名称、费用及规则文本；后续交互改版将其改为按需显示，见 [战场交互改版](Battlefield-Interaction.zh-CN.md)。
- 右侧分开堆叠、选择列表、优先权与回合操作；压缩法术力文本宽度，避免侵入手牌区域。
- 结束回合与让过按钮改为横向矩形，认输按钮增加可读文字；未实现的设置按钮隐藏。
- 手牌增加轻微弧线与旋转，不修改服务器提供的可选动作。

`Assets/Scripts/Editor/IntegratedBattlefieldArtPass.cs` 提供编辑器菜单 `MTGB/Art/Apply Obsidian Sanctum`，可重新应用这套布局。它会保存当前 IntegratedBattlefield 场景；后续手动编辑布局后不要随意重跑，以免覆盖手工位置。修改前场景备份位于 `.tools/scene-backups/IntegratedBattlefield-before-sanctum.unity`。

本轮未改旧场景 MTGAPrototype。后续调度故障修复已清理 IntegratedBattlefield 中 17 个失效旧组件引用（含 6 个旧牌堆点击组件），重新加载检查无缺失组件，战场施法回归通过。清理前备份位于 `.tools/scene-backups/mulligan-fix/`。后续仍可继续制作三维环境、动画、头像素材与不同屏幕比例的专项适配。

验证：真实 Starter 牌组的施放、玩家目标、费用与优先权点击通过（闪电击 20→17）；扇形手牌版本的施放、敏捷攻击与战斗伤害通过（20→19），包含射线遮挡与重复点击检查。最终截图见 `Captures/IntegratedBattlefield-Sanctum.png`。详情卡文换行在保存场景后补充校正。
