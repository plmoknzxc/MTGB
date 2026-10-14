# 玩家指示物图标与数字排版纠正

## 后续：统一指示物视觉尺寸

用户要求五种指示物同样大小。新增仅用于玩家指示物的 PlayerCounterSymbolFit，将图片、字体和矢量图标的实际网格按比例居中，最长边统一 28 Canvas 单位，32×32 容器留出边缘。数字保持原样。本轮仅检查双方指示物的实际尺寸、比例、边缘及偏好刷新后稳定性；不执行其他 UI／规则套，也不刷新全机制清单。新证据独立存于 `.tools/player-counter-size-1005`，不覆盖上批截图／结果。

## 用户要求与诊断

用户所指为 RAD 左侧数值 25 旁的 EXPERIENCE 图标上沿；数字应保留原来的右下角排版。上一批将图标／数字相交认作问题并右移数字，判断错误，已撤回，原批次不改写。对手整组上移 32 Canvas 单位保持。

本地图集 EXPERIENCE 单元为 `(280,120,40,30)`，与 pinned Forge `FSkinProp.IMG_EXPERIENCE` 完全一致。原图的上沿／两侧紧贴单元边界，细线缩小后不清楚；源单元没有少截一行，实际祖先没有 Mask／RectMask2D，cull 与 hasRectClipping 均为 false。扩大裁切会带入相邻图形，因此改为 `ExperienceCounterIcon` 内缩矢量轮廓，保留盾形与中线。

数字恢复基准 18、chip 左侧 22／y=-7、原实际宽度计算和 280 宽打包。ENERGY 继续使用 ManaGlyphs 的 U+E907，基准由 22 降到 16。初次仅直接写已缩放字号导致 PreferenceText.Awake 记住 21，周期 BindScene 再放大到 27；实际运行断言捕获失败，现用 SetBaseSize(16)，1.3 倍时稳定为 21。

## 局部验证范围

独立批次 `.tools/player-counter-icon-correction-1005`，当前 Unity DX11 Play Mode，1605×925，字号比例 1.3。中文／英文／日文分别重放已有密度展示夹具，经过偏好扫描、重复快照及查看战场／返回后检查两侧真实 CanvasRenderer 网格、字号和位置。

- 两侧五种常用指示物同页；数字基准、原位置保留，能量字体／准确 glyph 及字号正确。
- EXPERIENCE 实际 76 顶点（18 段边框与中线），轮廓包围盒完全在 28×28 Rect 内；未 cull、未矩形裁切。顶部完整性另看实际截图，几何断言不能代替观感。
- 对手头像位置仍 `(205,-120)`，自己仍 `(205,72)`。未重跑无关规则、BO3、随机结果或原生玩家目标选择。
- 保留首轮字号失败、执行检查源码与各语言采样。展示夹具每侧 575 个不同牌号仅用来检查界面密度，不计为卡牌效果通过。

本次 coverage 仅刷新、不执行规则套：2741 未登记效果测试、137 需重测。长期 goal 继续暂停；旧 OpponentHud“不相交”断言与截图只证明被用户拒绝的旧版排版，不能用于当前验收。

## 方法维护与限制

skill 的 ui-reuse 与 verification-method 已纠正错误诊断和重复字号来源，quick_validate 通过。图谱代次 `2026-10-04T23:20:26Z`，相关代码 metadata_changed，准确源码补读；图集 excluded/ignored-suffix，直接检查像素及上游裁切定义。当前结论限定本次图标显示路径；不宣称全机制或 DX12 已通过。

最新截图在 `Docs/References/PlayerCounterIconCorrection-2026-10-05/zh-CN-latest.png`，源码、检查脚本、输入、失败、结果和截图哈希见本批次 manifest。
