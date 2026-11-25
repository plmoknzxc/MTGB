# 战场、手牌、堆叠与详情：交互布局探索

这里保留 2026-10-25 的平面战场截图和 A—F 设计草案。该场景已退役。历史结果见 `Docs/BattlefieldRedesignScene.2026-10-25.zh-CN.md`。展示检查脚本使用项目当前的双人战场入口，旧版截图仅用于设计对照。

后续修订：堆叠默认放大、底框按条目数收起，恢复隐藏按钮；手牌悬停取消横向避让。法术力按用户选择固定到双方玩家信息区。最新实际截图 `stack-fit-player-mana-final.png`，专项检查 `stack-fit-player-mana-validation.json`；此前截图和报告保留各批身份。

参考样式再调整：堆叠增加统一渐变外框、衬线标题、结算顺序与摘要／目标提示，进一步放大并下移；所属条目和目标卡名后都显示当前堆叠编号。此版截图 `stack-reference-final.png`，专项脚本／报告 `validate-stack-reference-style.cs`／`stack-reference-style-validation.json`。

用户校正：B 站视频仅参考横排手牌，战场效果不得参考该视频。第一轮过于平面，第二轮按这个边界重做战场材质、光影与卡牌落场效果。第一轮总览保留为 comparison-round1.png，避免混淆前后方向。

打开 http://127.0.0.1:18264/index.html?variant=5 。默认展示新增的 F「Unity战场＋新UI」：使用实际 Unity 战场截图，叠加新手牌、阶段、堆叠及详情／历史交互。原生底图为静态组合场面。最新边界、原生地牌压左侧的修复与验证见 `native-hybrid-spec.md`。顶部保留 A—E 供回看，F 不提供伪造的拥挤切换。

F 最新调整：暂不展示详情／历史，右上改为可滚动长堆叠；右下缩小，法术力、撤回和当前操作分开。顶部“布局检查”可切换 23 类操作／窗口样本，新增操作文字已核对三语三档字号及较小窗口。手牌保持点击战场下沉、点击手牌上浮。法术力和原生窗口仍是截图，操作不连接对局。最新证据与限制见 `compact-ui-audit.md`；此前阶段记录仍在 `native-hybrid-spec.md`。

| 方案 | 主要处理 | 需要权衡 |
| --- | --- | --- |
| A 清醒竞技场 | 曜石台面、卡牌接触阴影；横排全牌；堆叠、详情集中右侧 | 右栏同时容纳堆叠与详情，长规则需要滚动 |
| B 阅读桌面 | 古典台面；左侧大图阅读；右侧展开堆叠与目标 | 两侧占用宽度较多；多张手牌横向滚动 |
| C 沉浸牌桌 | 遗迹场景与中央竞技场；横排手牌与横向阅读台组成底部 | 长规则主要靠文字阅读，需要时打开完整牌面 |
| D 详情与历史 | 沿用 C 的战场与右侧堆叠；详情与图文历史共享左侧标签栏；双方独立玩家面板与大号生命值 | 玩家面板占用右上空位与手牌左侧；多张手牌需要横向滚动 |
| E 完整区域 | 双方完整战场、指示物、法术力、统帅区／徽记／延迟效果／持续状态／地城；保留共用侧栏与堆叠 | 为验证所有区域主动组合展示数据；牌列中卡图较紧凑，详细阅读使用左侧详情 |

最新采用 F 的边界：保留 Unity 战场与资源展示，只探索新手牌、阶段操作、堆叠及详情／历史。下面 A—E 的过程说明保留为历史，不代表当前采用其战场。

最新的 E 用完整区域纠正 D 依赖空场面的错误。区域清单、源码对应、组合数据说明和验证边界见 `full-layout-spec.md`。`e-all-regions.png` 是完整场面，`e-dense.png` 是各类永久物四倍数量的压力场面，`e-dungeon.png` 展示地城详情。不要把 E 的组合图与 A—D 的真实录像时刻混为一谈。

用户最新方向已在 D 中实现：参考用户提供的 Master Duel 图文历史结构，共用“详情 / 历史”标签侧栏。历史显示实际记录的卡图、动作、归属与目标，蓝己红敌。切到历史后，悬停场上或手牌不会抢回详情；明确点击卡牌则显示其详情。支持标签键盘左右键、Home/End，保留固定详情、完整牌面、当前／印刷费用。历史只有原录像中的 3 条记录，不补造已结算或已支付事件。右侧堆叠的位置、目标逻辑与样式保持 C 版。

随后修正玩家信息不明显的问题：生命字号由设计基准的 29px 改为 61px，头像位置由 25px 改为 54px；双方有独立面板，敌红己蓝，名称 18px，公开区域数量 17px。对手面板位于地牌右侧，自身面板位于手牌左侧。隐藏手牌移到上沿，避免密集永久物下遮挡卡牌。原紧凑版截图另存为 d-details-compact-players.png、d-history-compact-players.png；当前截图已更新。qa-player-info.json 记录本轮布局检查。

`d-details.png`、`d-history.png` 分别展示两个标签；`preview-d.png` 为对比页缩略图。`qa-sidebar.json` 记录 D 版正常 1600×900、1280×720、两种窗口的拥挤演示、标签与卡图点击、键盘、墓地浏览、完整牌面及印刷费用验证。此原型不证明正式联机历史采集或同名实体标识已经接入。

## 可以实际操作

- 悬停或聚焦卡牌更新详情，点击固定，再点击关闭；C 还可打开完整牌面。
- 点堆叠条目查看对应内容与目标关系：己方蓝色、对手红色，目标提示用来源方的颜色，并保留文字说明。
- 打开双方墓地／放逐区的公开牌列表，再返回战场。
- 打开、关闭左侧对局记录。
- 拥挤演示复制同一批卡牌，检查滚动或分页及区域边界。

“让过优先权”等操作仅演示反馈，不连接真实对局。设置入口与完整菜单不是此次交付范围。

## 内容与资产

A—D 共用 `.tools/hand-disappear-1022/replay.json` 第 280 帧、第一个座位视图。生命、牌库、隐藏手牌数量、横置、堆叠目标和当前费用取自该视图；这些版本双方没有的永久物不会在正常模式中补造。对手的否认之力指向己方的天空之怒（X=2），当前手牌没有可施放选项，因此不制作可用发光。E 另用明确标注的组合展示场面，见上方说明。

卡图来自现有本地 CardImages，路径与哈希见 `asset-provenance.json`。食品原印次 TLTR/10 本地缺失，本样本使用已目视核实的 TWOE/13 食品异画。其余优先使用现有中文卡图，旧印次没有中文图时保留英文原图，中文名称及规则仍展示。没有修改生产卡图。

字体为本地 Source Han 字体的子集，许可证随 assets 保留。卡图嵌入 scene-data.js，战场原材质另存为 assets/battlefield-source.png（从共用背景无损提取，未修改内容），没有 CDN 或外部运行依赖。

参考视频为用户提供的 B 站 BV1fy4Q67E6U，只查看了免登录开头片段，未声称看完。参考范围严格限于横排手牌；战场构图、材质、空间、光影与卡牌落场效果全部独立探索，堆叠和详情也独立设计。

当前 Unity 同场面对照见 baseline.png、baseline-details.png。为稳定比较，展示辅助脚本省略了过期的临时通知浮层；原数据保存在 baseline-seat.json。截图后已恢复 Unity Main 场景的编辑状态。

## 验证范围

实际浏览器操作记录见 qa.json（包括过程中发现的问题，未抹去旧失败记录）。三版均检查了悬停、固定／关闭详情、堆叠目标、墓地浏览及历史开关。C 检查了完整牌面和拥挤永久物分页。B 修正了历史中的重复主语；C 修正了地牌名称间距及透明分区拦截点击。

正常独立页面在 1600×900、1280×720 检查：5 张常态手牌与 13 个战场对象没有几何重叠，战场对象底部点击点没有被其他区拦截，图片完整、字体加载完成。拥挤演示在同样的外层视口中检查，实际 iframe 高度分别为 792、612；三版可见且可点的战场对象与常态手牌没有交叠。滚动区中的屏外对象不计为可见对象；这不代表任意数量、任意分辨率或全部后续交互都已验证。最终比较页无浏览器控制台错误。

主要交付仍为 HTML 视觉与交互样本。另修复原生战场发现的普通地牌避让与辅助永久物尺寸问题；没有将新 UI 整体替换进客户端，没有修改规则引擎、服务器或卡组，也没有把这次验证记为机制效果通过。

第二轮记录见 qa-round2.json：补测三版材质修订后的正常场面与两种窗口下的拥挤演示，可见常态手牌与战场仍无交叠，正常场面牌张底部命中未被装饰层拦截。发现 A/C 原先内嵌的长背景引用没有实际应用，已改用上述本地 PNG，并从实际 DOM 和截图确认显示。预览图、拥挤图及总览均已更新，第一轮记录仍保留。

## 本地预览

当前服务仅监听 127.0.0.1:18264，供本机查看。若服务停止，在此目录运行：

```powershell
python -m http.server 18264 --bind 127.0.0.1
```

入口为 index.html。preview-a/b/c.png 是三版正常截图，dense-a/b/c.png 是拥挤演示截图，comparison.png 是总览。

## Unity 副本最新增量：整体右滑与 UI 清晰度

最新实际效果为 `stack-slide-final.png`，隐藏状态为 `stack-slide-hidden-1920x1080.png`；`stack-slide-1280x720.png`／`stack-slide-1920x1080.png`／`stack-slide-1024x768.png` 是三种实际 Game View 截图。本轮在已复制的 Unity 场景中修改，不是新的 HTML 视觉稿。

堆叠连标题和底板整体右滑，两个控件固定；条目底色加深区分敌我，只在顶部保留结算方向，目标提示包含具体来源名字及堆叠编号。法术力按数值宽度排间距，经验数字放到轮廓右侧。清晰度按用户澄清只处理 UI 素材：小字号被网格放大的能量图标、斜线图标抗锯齿和实际尺寸的按钮圆框，保留原有图集分辨率。

`validate-stack-slide-ui.cs` 对应 604 项检查，`validate-stack-slide-windows.cs` 对应 48 项实际窗口检查，最终均通过。报告文件 `stack-slide-ui-validation.json`／`stack-slide-windows-validation.json`；初始缺 CanvasGroup 的异常单独保留 `stack-slide-ui-round1-missing-group.json`。本批只证明 UI 适配和本地回调，未打包或发布。

## 手牌底板与悬停在堆叠上方

当前常态实际截图为 `hand-tray-normal.png`，下沉为 `hand-tray-lowered.png`；背景参考最终网页 `.dock` 的深色渐变与顶边线。仅悬停／拖动牌面临时提高层级，底板和其余手牌保留原顺序。

`validate-hand-tray-hover.cs` 的 311 项窗口、字号、手牌数量、导航、悬停交叠射线和本地原索引回调检查通过；初轮透明翻页区域吞底板点击的问题保留在 `hand-tray-hover-round1-navigation-input.json`。`capture-hand-tray.cs` 补直接选手牌的悬停点击；`hand-hover-over-stack.png` 是 12 手牌／12 堆叠的显示扩展样本，原数据另存 `hand-hover-over-stack-input.json`，不作为新实局／规则结算证明。

堆叠行内说明由通用摘要提取逻辑生成，不是为截图逐牌写入；完整规则保留原悬停详情，不宣称每张牌的摘要语义已核实。

## 最新：取消语义摘要，通用展示完整效果

上面的首句摘要策略已撤销。当前行内保留完整公开效果、已选模式、X、复制及施放信息；长内容折叠为三行视窗，提供“展开完整效果”和有界滚动。不是给每张牌编写摘要，不重新验证已有卡牌机制。模式匹配按完整来源文本，副本牌面和详情使用同一路径；新／改动文本完整保留，旧记录缺来源有安全回退。

验证入口 `validate-stack-complete-effects.cs`，结果 `stack-complete-effects-validation.json`。旧空来源对象导致 CARDNAME 残留的首轮报告另存 `stack-complete-effects-round1-placeholder.json`；详见 `Docs/BattlefieldRedesignScene.2026-10-25.zh-CN.md` 的“完整效果合同”。

完整效果合同 461 项通过，缺卡名缩略图回退的最后差异另 24 项通过（`stack-legacy-thumbnail-validation.json`），共 485 项。本批没有逐卡重新跑机制。最新实际截图为 `stack-complete-collapsed.png` 和 `stack-complete-expanded.png`；只作用于副本场景，未打包或部署。

## 当前：完整效果直接展示

用户后续决定取消上批三行折叠和展开按钮。短效果直接显示完，长效果默认有界滚动；只有正文溢出才显示内部滚动条。异能保留当前进入堆叠的那条完整效果，不展示整张来源卡的其他异能。目标身份和敌我颜色、外层列表滚动不变。

增量验证入口 `validate-stack-direct-effects.cs`／结果 `stack-direct-effects-validation.json`，当前实际截图 `stack-direct-effects.png`；旧 485 项报告和截图保留原批次身份，不能当作本轮直接滚动检查。只修改 Unity 副本，未打包部署。

本轮最终 280 项显示／滚动／当前异能边界检查通过，含三语、三字号、三种窗口、真实滚轮射线及末字可见；没有新增规则效果 PASS。原生产场景哈希不变，控制台零错误。

## 正文称呼颜色

共用描述格式器已区分普通代称和真实房间昵称：“你／我／对手”等用正文普通颜色，实际名字仍己蓝敌红。堆叠所属标签、条目底色、目标提示颜色不变。验证入口 `validate-player-prose-colors.cs`／`player-prose-colors-validation.json`；首轮服务生命周期及第二轮恢复到错误场景的夹具失败报告独立保留，见主文档。没有规则／服务端变更，未打包部署。

最终 63 项显示检查通过，三语三字号的实际堆叠文本和昵称身份边界分别记录，控制台零错误；截图 `stack-player-prose-neutral.png`。

## 必填名字与所属标签

空白玩家名现在由界面和连接／主机入口先拦截，不再由输入流程默补 Player；服务器原有握手拒绝沿用共用名字校验。玩家真的叫“你”仍可用，堆叠所属行改用实际昵称＋堆叠编号，保留敌红我蓝。入口 `validate-player-name-required.cs`／结果 `player-name-required-validation.json`；未打包部署。

最终 217 项入口／名字身份／三语字号显示检查通过，控制台零错误；未进行新的公网对局。

## 目标卡名颜色与实际组合截图

指定目标的卡名按目标一方区分蓝／红，“指向”及编号用中性色；当前战场控制方与准确堆叠 controller 优先，非战场牌用拥有者，不把来源咒语的颜色套给所有目标。

`stack-target-abilities-long-names.png` 展示两条泰坦异能、三目标和双方 24 字符昵称；`stack-target-spells-long-names.png` 展示双方咒语、指向的卡名与堆叠编号。输入和范围记录在 `stack-target-display-samples.json`，属于实际 Unity 显示适配器截图，不是新实局／效果证明。

增量入口 `validate-stack-target-colors.cs`、结果 `stack-target-colors-validation.json` 最终 121 项通过：三语三字号、两种窗口和目标身份／控制权边界。控制台零错误，未打包部署。

## 当前：整条信息限高滚动

用户反馈目标和长名字仍把条目撑高。右侧完整信息现在统一放入 `CompactDetails`，单条最大 300 Canvas 单位、左图固定；短内容不显示滚动条，长内容只保留一层内部滚动，外层列表仍可滚动。所属格式改为“堆叠几（玩家名）”。

新验证入口 `validate-stack-entry-scroll.cs`，结果 `stack-entry-scroll-validation.json`；实际截图 `stack-entry-scroll-start.png`／`stack-entry-scroll-end.png`。原摘要、正文单独滚动、原层级的旧报告均保留当时身份，不作为新结构的几何检查通过。仅副本，未打包部署。

主批 382 项中 352 项有效通过；30 条错误范围／短条目定义断言保留在 `stack-entry-scroll-round1-scope.json`。只补右侧滚动范围、真实短元数据及不换行编号前缀，`validate-stack-entry-scroll-delta.cs`／`stack-entry-scroll-delta-validation.json` 最终 109 项通过，不重复已通过的其他检查。最终两张截图已更新，控制台零错误。

## 当前：固定条目与上移

每条统一 394×240，右侧完整信息视窗固定高 212；短内容不缩条目，长内容内部滚动。整列和按钮轨上移 48，距顶 24，右滑动画只改横向坐标，避免恢复旧 y。常规窗口完整显示三条，更多条目外层滚动；原生底部操作区保持。

入口 `validate-stack-fixed-entries.cs`／结果 `stack-fixed-entries-validation.json`，截图 `stack-fixed-three-start.png`／`stack-fixed-three-scrolled.png`。只修改 Unity 副本，未打包部署。

首轮距顶 28 的 175 项中 174 项通过，1920 窗口因底部预留比预计多而少 4 个高度，正确拦住“三条完整显示”；保留 `stack-fixed-entries-round1-height.json` 和对应部分裁切截图。最终上移到 24，只补 `validate-stack-fixed-position.cs`／`stack-fixed-position-validation.json` 的新位置、三条完整显示与隐藏返回，不重跑已通过的尺寸／文字组合。

最终位置增量 28 项通过，三种窗口均完整显示三条、保留底部控件并通过隐藏／返回／刷新和滚动末目标检查；控制台零错误。最终三条截图已更新为长玩家昵称样例。
# 对手手牌横排增量

最新：双方底板改成边缘渐隐，并增加手牌／放逐区／坟墓场固定页签，统帅与备法入口仍按已有来源保留。`hand-source-tabs-hand.png`、`hand-source-tabs-graveyard.png` 展示；`hand-source-tabs-round1-immediate-click.json` 保留 492 项通过及一项驱动失败，`hand-source-tabs-delta-validation.json` 9 项定向复核通过，旧录像交集另有 3 项检查。公共坟墓来源的真实引擎投影记录在 `.tools/hand-source-tabs-1025/result.json`（9 项），尚未发布服务器／客户端。完整限制见项目报告最新段落。

对手现在也采用新版横排，常态按战场实际边界避让；自己的展开／下沉仅作用于本地。`opponent-horizontal-own-raised.png` 和 `opponent-horizontal-own-tucked.png` 可比较对手位置不变。

三窗口、双席位、0/1/7/24 张及密集地的显示适配器检查，和三语言真实逼从请求记录的点击／隐私检查见 `opponent-horizontal-round1.json`（原始失败保留）与 `opponent-horizontal-delta-validation.json`（49 项通过）。测试清理错误及限制详见项目 `Docs/BattlefieldRedesignScene.2026-10-25.zh-CN.md` 最新段落。仅复制场景 UI，未发布。

