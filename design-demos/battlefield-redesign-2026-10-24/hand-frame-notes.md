# 双方手牌底框素材接入（2026-10-25）

采用内置 imagegen 生成的暗蓝黑金属／细铜色边饰 PNG：`Assets/Resources/UI/BattlefieldRedesign/HandTrayMetal.png`。完整提示词、原图及 Sprite 导入切片配置保存在素材目录。2172×724 原始透明 PNG，实际框体 Sprite 2172×375，RGBA32，无有损压缩；九宫格保留边角比例。文字、页签、卡牌仍为独立 UI。

自己底框随手牌下沉，页签固定在底边；对手底框固定在对手手牌占用高度内，不拦截射线。旧渐变作为素材缺失回退。修改仅用于复制场景。

视觉复核发现旧 `SetSiblingIndex(otherHand.GetSiblingIndex())` 在底板原本位于手牌之前时，会因移除产生索引偏移，将底板移到牌面之上。现计算扣除偏移，重复布局保持底板在手牌之下。初批 46 项通过但未涵盖此层级缺陷，保留 `hand-frame-round1-validation.json`；增补每种窗口五次重复布局断言后，最终 `hand-frame-validation.json` 61 项通过。

本轮验证范围：1920×1080、1280×720、1024×768，九宫格/资源加载、水平边界、双方三种页签真实射线、自己下沉后可恢复、对手常态手牌与场上牌和法术力间隔、背景层级及本地切换不发出原生行动。中文标准字号；此前中英日和三字号证据属于前一轮，本轮不重复计数。不宣称新的卡牌机制结算验证。

截图：`hand-frame-raised.png`、`hand-frame-tucked.png`、`hand-frame-narrow.png`。Unity 最终错误日志 0。正式场景 SHA256 仍为 3FF3CB51506C876A021973B1852BEB6B6902FA12AB20DFDF964D8EC808633405。未打包、覆盖安装版、更新服务器或提交 Git。
