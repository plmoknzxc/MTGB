# 紧凑堆叠异能正文自适应

用户指出神秘巨著的“抓一张牌”在新版堆叠缩略牌里太小。现有 FitAbilityBodyText 已按正文视口测量，但仍沿用全尺寸牌面的13～18基础字号，240宽牌面再缩至116宽后文字过小。

本轮仅针对复制场景中归属 stackContent 的异能缩略牌，将正文拟合范围改为22～34。短句尽量使用大字号；长文降到22后保持可读下限并滚动，不删效果、不继续缩成小字。卡名、卡图、结算文本来源、当前异能提取和其他异能展示入口不改；旧配置保留13～18。未改原生结算／协议，也未打包部署。

验证脚本 design-demos/battlefield-redesign-2026-10-24/validate-compact-ability-autosize.cs 使用上一轮真实神秘巨著请求，以及明确的长文布局适配器；中／英／日，三窗口，.85／1／1.3字号，短→长→短复用，检查增大字号、全文／滚动条、实际滚轮命中、最末实际字形可见、无动作提交及旧配置隔离。结果 compact-ability-autosize.json。实际字形／缓存诊断 compact-ability-glyph-diagnostic.json：英文1302字后切到日文490字，TMP复用1302容量；实际第489字在视口内，缓存第1301字在视口外。首轮测试错误遍历整个容量导致日文3项误报，178项其他成功前缀保留于 compact-ability-autosize-round1-unused-glyph-capacity.json；改为 Take(characterCount) 后补验。进入Play初始化时 EventSystem.current 尚空的首轮工具异常也只修测试空值守卫，没有改生产逻辑迁就。

最终181项全部通过。Unity编译与控制台无错误，原生产场景哈希不变。图索引相关文件 metadata_changed，均读当前源核对；本轮纯字号展示，不重新跑全机制分类或效果、不恢复暂停goal。截图 compact-ability-autosize.png 为真实 Unity 原生请求重放展示，长文测试不冒充规则结算。
