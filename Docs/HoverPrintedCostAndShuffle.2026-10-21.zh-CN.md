# 悬停原始费用、反面异画来源与洗牌聚集检查

本批接续索尔选牌／录像显示的未发布改动。用户明确“卡爆”包含地太多、连续地与同名牌聚集，不能用起手 0–1 地概率回答。只改客户端展示／卡图来源解析；洗牌、桥接协议、费用规则与服务器运行包均未修改。未打包、覆盖桌面或部署，机制 goal 继续暂停。

## 改动及验证范围

- `LegacyCardCost` 从同一张扫描图揭开对应费用的遮盖，主／副费用独立比较；动态费用条保留。共用手牌、可用放逐、可用坟墓场的 pointer 悬停状态；移开、拖牌、ResetHover、停用／复用恢复遮盖。相同费用不重复揭开，空费用地没有新增费用或留白。
- 历险／Omen 的扫描图标题对应本体印刷费用；MDFC 背面标题与副牌面的动态费用比较，不能拿正面费用比较背面。房间两条旋转费用分别处理。
- 开发者组件卡面也支持相同功能：印刷费用回到标题槽，当前预览条显示在上方。长费用按槽宽／条宽缩小符号。默认仍使用扫描图。
- `CardArtSources` 精确解析 Forge 的 `☇` 编号后缀：同系列、同收藏编号、同规范牌名且来源恰有两张图时，使用来源的背面；保留原 `☇` 本地文件键与牌组身份。不按相似名字、其他版本或任意后缀回退。
- 下载器可检查剩余牌面是否有来源，异画列表对真正无来源且无本地图的版本显示“暂无图片来源”并禁用下载。

用户图中的牌为 Blood Crypt／血腥墓穴，准确缺图版本 `ECL/349☇`。后缀的含义来自固定 Forge 的 [ImageUtil.java](https://github.com/Card-Forge/forge/blob/4ec5f1a2c32fa90ecb983a72b9eb47aa5c5d7676/forge-core/src/main/java/forge/util/ImageUtil.java)：后缀移除后指定 `face=back`。本地对应上游源码亦已核对。现有 `CardArtSources.jsonl` 已保存 `ECL/349` 的两个准确图片 URL；本批未修改目录、原图或翻译资料。

## 界面与卡图证据

原始输入与结果保存在 `.tools/hover-cost-art-shuffle-1021/`。

- `hover-final.cs`／`hover-final-checks.txt`／`hover-final-complete.txt`：210 项最终检查。复用 10 月 8 日真实 Forge `before-hand.json` 的加费与历险预览，覆盖手牌、放逐、坟墓场、中英日、标准／大字号、相同费用、悬停中的快照费用刷新、重置及拖牌。费用更新的人工投影仅验证显示，不宣称新费用规则通过。
- `visible.cs`／`visible-checks.txt`：6 项可见性检查。取上述真实输入中每区一张的展示子集，明确断言牌面不透明且实际抬起；`visible-hover-152.png`、`visible-hover-163.png`、`visible-hover-170.png` 分别是手牌、坟墓场、放逐。较早密集九张截图有卡在浏览范围外，不能当作每区可见效果证明，以这组三张为准。
- `boundaries.cs`／`boundaries.txt`：24 项印刷面边界检查，CLB/781 历险、TDM/178 Omen、KHM/114 背面、DSK/335 房间，按实际目录／扫描图投影主费用相同而副费用不同的情况，另查停用复用与 Forest 无费用。不是上述牌的新效果验证。
- `art.cs`／`art-checks.txt`：18 项。五张 `ECL/347☇` 至 `351☇` 的来源、错误牌名拒绝、准确背面 URL、独立缓存实际下载、Unity JPEG 解码 **488×680**、真实无来源版本 DS0/2 的禁用状态。未更改用户卡图偏好或桌面客户端。
- `art-preview-final.txt`／`art-reverse-preview-final.png`：实际面板等待背面缩略图完成解码，再用 EventSystem 射线选择准确 `ECL/349☇`；确认对应下载按钮可用。
- 31 项已有来源／交互／机制分类守卫、文本编码检查和 `git diff --check` 通过；Unity 编译错误检查为空。机制覆盖仅刷新分类，仍为 2582 未登记、318 旧证明待核对，没有 `--run` 或全卡通过结论。

## 洗牌：爆地和同名牌聚集

检查生产链：Unity `OnlineMatchService` 的有符号 int seed → `OnlineMatchSession` → `ForgeBridge` 的 `MyRandom.setRandom(new Random(seed))` → `Player.shuffle` 的 `Collections.shuffle`。实际 BridgeController 没有重新排列这批牌；`filteredHandsEnabled=false`，未使用两手选一的土地平滑。

`unity-seeds.json` 由实际 Unity `Random.Range(int.MinValue,int.MaxValue)` 生成 20,000 个 seed，采集前后恢复 Unity Random.state；20,000 个值互异，9,982 个非负，覆盖正负范围。`ShuffleClumps.java` 经生产 ForgeBridge 启动，在真实游戏线程调用 `Player.shuffle(null)`。

60 张牌按同名先分组，18／24／28 地，地采用五种基本地、非地使用四张同名为主的普通牌。每种地数各测：

1. 每次恢复分组牌序，使用上述实际 Unity seed；20,000 次。
2. 不重置牌序，保持同一个原生 Random 连续洗；20,000 次。

最终 **120,000 次原生洗牌**；每组另以 SplittableRandom 和独立 Fisher–Yates 实现生成 100,000 个对照，共 600,000 次。逐次检查 60 个物理身份没有丢失／重复；每组最后实际 `drawCards(14)` 的返回 ID 顺序与原生洗后顶部 14 张完全一致，共 6 条实际抽牌顺序检查。

记录起手地数、前 14 张地数、全库／前 14 张最长连续地、同名相邻对数、全库最长同名串、起手最大同名张数。最终直方图在 `clumps.json`，统计在 `clumps-analysis.json`。合并样本计数至少 30 的直方图区间与对照最大标准化偏差约 **2.55**，未出现预设 6 标准差偏差。稀有小样本区间保留原计数，不宣称逐格统计确定。

| 地数 / 60 | 重置牌序：前14张至少8地 | 连续洗：前14张至少8地 | 独立对照 |
| --- | ---: | ---: | ---: |
| 18 | 1.630% | 1.720% | 1.561% |
| 24 | 11.940% | 12.110% | 11.773% |
| 28 | 27.940% | 27.345% | 27.678% |

本批未发现低地构筑异常爆地或同名牌聚集偏差，因此没有改洗牌算法、重排连牌或加土地平滑。随机分布本身允许连续地和同名牌相邻。上述是固定运行包、所列构筑／采样和抽牌顺序的有界证据；不是全部规则组合、Linux 大量完整实局或每一局抽牌体验的保证。

## 验证方法修正与保留失败

首次寻找真实缺图样本时，验证脚本启动异步来源加载后立即遍历目录，并用 `TryLoadExact` 判断缺图。索引尚未恢复到主线程，导致大量本地图在同一帧被解码；纹理无法及时回收，编辑器内存耗尽。`editor-oom.log` 保留该失败。恢复编辑器后，改为等待索引完成、只用 `TryFindImagePath` 查询路径，像素解码只取准确目标版本；`art.cs` 通过。生产缓存预算未改，此失败不当作普通对局内存测试。

已将该反例写入机制 skill 的 `ui-reuse.md`。skill 主文件／YAML 未修改；系统 Python 缺少 PyYAML 的 quick_validate 限制沿用上批记录，引用文件与主文件前置字段另核对。图谱工具持续 `Transport closed`，本批对相关源码逐项读取及定向搜索，没有据不可用图谱作完整性断言。
