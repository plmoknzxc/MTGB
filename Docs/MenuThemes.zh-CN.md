# 菜单主题 · 月夜秘殿

「月夜秘殿」是 MTGB 的默认菜单主题。英文名为 Moonlit Sanctum，日文名为「月夜の聖堂」，保存用的标识为 `moonlit-sanctum`。

## 画面与排版

主界面使用冷蓝色召唤大厅。联机、本地模式和录像页沿用大厅背景，并在内容区铺深色底板。卡组编辑使用独立的暗色殿堂底图；工具栏与弹窗标题共用月夜横幅。装饰集中在页面背景和标题带，卡面与规则文字周围留出阅读空间。[主菜单](../Captures/Menu-Archive-Navigation.png) · [卡组编辑](../Captures/Deck-Archive-Editor.png) · [联机房间](../Captures/Online-Archive-Lobby.png)

| 用途 | 月夜秘殿的取值 | 使用位置 |
| --- | --- | --- |
| 面板与输入区 | 面板 `#1B2B35`，输入区 `#0E1921` | 面板承载内容，较深的底色标识可输入或滚动的区域 |
| 操作层级 | 常规按钮 `#3F5667`，主要操作 `#6B5189` | 保存、确认、设为偏好使用紫色；返回、取消、恢复默认使用蓝灰色 |
| 文字 | 正文 `#E9F0F4`，辅助信息 `#AEC0C5`，光标 `#D4E8F3` | 说明和页码降低亮度，输入光标保持清晰 |
| 卡组编辑分区 | 主牌 `#95CAFA`，备牌 `#CAB6EA`，卡库 `#84DAD1` | 分区标题与顶边同色，并保留文字标签 |

中文标题使用项目标题字体 `NameCN`，正文使用 `NameWenKaiCN`，按钮、下拉选项和输入框使用文泉驿微米黑。按钮沿用切角轮廓与浅色描边。窄栏按钮使用短动词，选中的卡图版本、下载状态和失败原因放在相邻状态栏。

## 飘萍皇 · 潮灯

点主菜单右上角的齿轮，在「界面主题」中选择「飘萍皇 · 潮灯」即可立即切换。主菜单里，飘萍皇在神河的水巷边回望，双手握着一把绛红色和伞。银发与月白羽织映着灰紫色天光，伞沿垂着雨滴。左侧的柳枝与水巷压暗，衬出标题和菜单。主按钮呼应伞面的绛红色，次按钮用暗靛色。卡组编辑和录像详情使用渡口空景，工具栏和弹窗标题使用河岸横幅。主牌、备牌和卡库分别使用青碧、梅紫和靛蓝。主题标识为 `wandering-emperor-tidelights`。[主菜单预览](../Captures/Menu-Emperor-Tidelights.png)

运行时，主菜单播放飘萍皇撑伞的 8 秒待机动作。她胸前的衣襟随呼吸轻轻起落。倾头时，下方的手托起伞，上方的手沿伞柄微调握点。发根随头部移动，长发和发梢稍晚跟上，随后一起回落。游戏以每秒 30 帧播放定稿的动作，伞尖落雨、涟漪和花瓣在独立的天气层中运行。离开主菜单或关闭动态时释放角色动效资源，编辑模式显示原始静帧。[制作流程、复现入口与性能记录](MenuMotionWorkflow.zh-CN.md)

## 设置与后续主题

卡组编辑的「设置」页和「游戏设置」弹窗共用「界面主题」选择框。主题下方的「主界面动态」默认开启，有动画的主题会在主菜单播放。关闭后立即显示静态原画。选择「月夜秘殿」等静态主题时，开关置灰。切回动态主题时会恢复上次的开关状态。客户端用 `UI.MenuTheme` 和 `UI.MenuMotion` 分别保存两项选择，下次启动时沿用。首次使用或已选主题缺失时使用「月夜秘殿」。[设置界面](../Captures/Menu-Theme-Settings.png)

## 安装与管理

在设置中点「主题文件夹」，可以打开客户端旁边的 `Themes`。每个外部主题占一个子目录，游戏启动时读取该目录中的 `theme.json`。把解压好的主题文件夹整体放进去，重新启动游戏，即可在「界面主题」中选择。删除整个子目录即可卸载。月夜秘殿由游戏内置，首次启动和已选主题被移除后都使用月夜。

潮灯的发行文件包括 `Themes/Tidelights/theme.json`、`art.bundle` 和 `audio` 目录。移动或分享时复制整个主题文件夹。玩家头像由头像设置单独管理。

自制主题可用 `MenuMusic` 和 `BattleMusic` 指向主题内的 Ogg Vorbis 文件，例如 `"./audio/menu.ogg"`。两首曲子分别在菜单和战场循环播放。切换主题、进入战场或回到菜单时，旧曲立即停止，新曲就绪后短暂淡入。省略某一项或文件缺失时，该场景使用月夜配乐。所有主题共用「声音」中的音乐音量。

月夜秘殿的菜单使用 PeriTune 的《Lost Place4》。合唱和管风琴呼应月下的召唤阵。[配画面试听](../Captures/Menu-Music/Moonlit-Lost-Place-4.mp4) · [作者发布页](https://peritune.com/blog/2017/01/11/lost_place4/)

月夜秘殿的战场使用 request 的《Heartfelt Battle》，直接采用作者提供的 2 分 34 秒循环版，弦乐、钢琴与圆号伴随对局。[作者发布页与 CC0 许可](https://opengameart.org/content/heartfelt-battle-loopable-fantasy-stringspianohorn)

飘萍皇 · 潮灯的菜单使用 PeriTune 的《Oboro》。尺八、琴和钢琴搭配伞下的雨巷。[配画面试听](../Captures/Menu-Music/Tidelights-Oboro.mp4) · [作者发布页](https://peritune.com/blog/2019/04/08/oboro/)

两首菜单曲均使用 PeriTune 提供的循环录音，仅调整音量后转码。原版 Ogg 在 `Tools/Audio/Masters`，运行 `python Tools/Audio/menu_scores.py moon` 或 `python Tools/Audio/menu_scores.py tide` 可重新导出。分发客户端和独立主题时保留相应署名。[曲目设置](../Tools/Audio/menu_themes.json) · [第三方署名](../THIRD_PARTY_NOTICES.md) · [作者对旧曲授权的说明](https://peritune.com/blog/2026/03/01/terms-update/)

自制主题最少需要一个 `theme.json`，含唯一的 `Id` 和显示用的 `ChineseName`。未填写的视觉属性继承月夜。下面的示例读取同目录的 `hall.png`，并更换主要按钮的颜色。

```json
{
  "Id": "my-evening",
  "ChineseName": "暮色",
  "Hall": "./hall.png",
  "ActionFill": { "r": 75, "g": 91, "b": 132, "a": 255 }
}
```

大厅、卡组、录像和战场底图可分别使用 `Hall`、`DeckBackdrop`、`ReplayBackdrop`、`BattlefieldBackdrop`，路径以 `./` 开头时读取随包的 PNG 或 JPEG。继承内置资源时使用原有的 Resources 路径。需要九宫格、多切片、材质或专用着色器时，使用带 `art/` 地址的 Windows 资源包。潮灯的 Unity 制作源位于 `Assets/ThemeSources/Tidelights`，更新插画或动作后，从 Unity 菜单选择 **MTGB → Themes → Rebuild Tidelights artwork** 生成分发文件。

新版战场和主菜单共用主题选择。战场有独立的俯视桌面插画。「月夜秘殿」使用嵌有银色星盘刻度的深色大理石，「飘萍皇 · 潮灯」使用带有水纹倒影、落樱和朱栏的雨夜木台。两张底图均在本地 ComfyUI 中由 Anima-Turbo v1.1 独立生成，PNG 的 `prompt` 元数据保留生成参数。[月夜秘殿战场](../Captures/Battlefield-Moonlit-Sanctum.png) · [潮灯战场](../Captures/Battlefield-Emperor-Tidelights.png)

制作主题时，将战场桌面和席位背景分别填入 `BattlefieldBackdrop` 和 `DuelSeatBackdrop`。

战场面板放在 `BattlefieldFrameAtlas`。月夜秘殿使用冷蓝灰底纹，潮灯使用低饱和的梅灰底纹。两张底纹均由本地 ComfyUI 的 Anima-Turbo v1.1 生成，打包时压低明暗对比，并在面板上沿保留少许受光。外侧的柔和阴影使面板和桌面分开，头像保留窄圆环。PNG 的 `mtgb.comfy.frames` 元数据记录了每个部件的来源与生成工作流。

边框图集按 Sprite (Multiple) 导入。`Journal`、`Stack`、`Phase`、`Seat`、`Hand` 和 `HandRail` 分别用于记录、堆叠、阶段、席位、手牌与来源栏，九宫格切片保持边缘宽度。`Avatar` 是中央透明的头像框，`Ink` 用于目标提示与行分隔线。阶段区保留窄切角轮廓。

记录与堆叠的标题分别取主题横幅 `HeaderArt` 的不同区域，按原图比例裁切，向正文渐隐。堆叠向上收进标题栏。收起时显示对象数，右侧的总览和展开按钮仍可使用。向下展开后恢复结算方向和原来的滚动位置。

对局记录的页签平分标题下方的横栏，以嵌在基线上的菱形标记当前页。双人对局的玩家筛选靠近标题右侧，宽度不足时移到页签下方。状态条目先列牌名，再列效果类别，右侧取该牌插画。展开后正文沿用牌名的左边线，右边显示完整卡面。地城和魔戒等需要看图的状态保留大图。手牌和指挥官的名称与数量位于各自卡列中央，叠牌和冠形标记帮助区分两个来源。

堆叠从上到下编号。每项左侧展示卡面，右侧使用该公开卡面的局部插画，栈顶的底图稍亮。关系行的「指向」列出目标，「来自」列出以本项为目标的堆叠对象。行末编号对应同一面板里的对象，正在宣告的对象单独显示。对局历史按回合和阶段组织，事件以玩家色圆点标记，可查看的牌名使用主题链接色。

下次启动时，各个设置入口都会从同一份主题目录生成选项。卡图版本偏好以卡牌为单位独立保存。当前主题的完整参数见 [MenuThemeProfile.cs](../Assets/Scripts/UI/MenuThemeProfile.cs)，菜单控件如何使用这些参数见 [MenuVisualTheme.cs](../Assets/Scripts/UI/MenuVisualTheme.cs)。
