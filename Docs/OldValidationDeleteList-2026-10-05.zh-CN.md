# 已核对的过时验证目录

以下 12 个目录是本次可删除的完整清单，总计约 10.14 GiB。当前尚未删除。

只删除列出的批次目录；不要删除其上级 unity、runs 或 mechanic-coverage 目录。

| 可删目录（完整路径） | 大小 GiB |
|---|---:|
| `D:\project\MTGB\.tools\mechanic-coverage\unity\20261003T1500257447792Z` | 0.945 |
| `D:\project\MTGB\.tools\mechanic-coverage\unity\20261003T1608152246914Z` | 1.040 |
| `D:\project\MTGB\.tools\mechanic-coverage\unity\20261003T1803262632535Z` | 1.143 |
| `D:\project\MTGB\.tools\mechanic-coverage\unity\20261003T1909350814941Z` | 1.221 |
| `D:\project\MTGB\.tools\mechanic-coverage\unity\20261003T2035312710288Z` | 1.306 |
| `D:\project\MTGB\.tools\mechanic-coverage\unity\20261003T2153131061846Z` | 1.395 |
| `D:\project\MTGB\.tools\mechanic-coverage\unity\20261003T2301340600683Z` | 1.509 |
| `D:\project\MTGB\.tools\mechanic-coverage\runs\20261003T184531144704Z` | 0.347 |
| `D:\project\MTGB\.tools\mechanic-coverage\runs\20261003T201142171080Z` | 0.361 |
| `D:\project\MTGB\.tools\mechanic-coverage\runs\20261003T213214342756Z` | 0.369 |
| `D:\project\MTGB\.tools\mechanic-coverage\runs\20261003T224150142656Z` | 0.383 |
| `D:\project\MTGB\.tools\mechanic-coverage\runs\20261003T235625846979Z` | 0.118 |

## 必须保留

- `.tools/mechanic-coverage/runs/20261004T000513050100Z`：进度账本完整引擎基线。
- `.tools/mechanic-coverage/unity/20261004T0037163401098Z`：完整 Unity 基线。
- 所有 20261004 的近期引擎增量结果，以及未列出的失败／中断批次。
- `.tools/validation-retirement-1005/summaries`：已经保存的小型历史摘要。
- 卡图、源码、ForgeRuntime 和 Git。

## 手动操作

在资源管理器地址栏粘贴表中某个完整路径，确认最后一级批次名；返回上一级，只删除该批次文件夹。按清单逐个处理，避免选中上级目录。

工具拒绝了自动删除，所以此清单不表示已经释放空间。

## Docs 可清理的重复旧截图

四张旧指示物／HUD 截图合计约 8.31 MiB；已逐文件 SHA-256 核对，与原验证归档的保留副本完全相同。现在仍未删除，逐文件删除也被工具拒绝。

- `D:\project\MTGB\Docs\References\OpponentHud-2026-10-05\en-US-dense.png`（保留副本：`.tools/opponent-hud-1005/en-US-dense.png`）
- `D:\project\MTGB\Docs\References\OpponentHud-2026-10-05\ja-JP-dense.png`（保留副本：`.tools/opponent-hud-1005/ja-JP-dense.png`）
- `D:\project\MTGB\Docs\References\OpponentHud-2026-10-05\zh-CN-dense.png`（保留副本：`.tools/opponent-hud-1005/zh-CN-dense.png`）
- `D:\project\MTGB\Docs\References\PlayerCounterIconCorrection-2026-10-05\zh-CN-latest.png`（保留副本：`.tools/player-counter-icon-correction-1005/zh-CN-settled.png`）

Docs/References 整体约 186.67 MiB，文字文档本身很小。规则说明、缺口列表及 MTGA 录屏参考继续保留。
