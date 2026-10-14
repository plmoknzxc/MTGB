# 项目占用与缓存清理记录

统计位置 `D:\project\MTGB`，包含隐藏目录；不跟随目录 junction／symlink，共跳过 44 个链接，无读取错误。单位 GiB=1024³。以下是文件逻辑大小；不是 NTFS 分配空间。独立 os.stat 文件身份检查未发现硬链接重复，避免使用 scandir 在 Windows 下的零 inode 去重误判。

| 内容 | 大小（GiB） |
|---|---:|
| CardImages（全部语言） | 20.73 |
| 其中中文 zh-CN | 7.38 |
| 其中日文 ja-JP | 3.64 |
| .tools（工具与验证档案） | 28.07 |
| 其中 mechanic-coverage | 13.88 |
| 主项目 Library | 4.38 |
| .codex（含旧验证工作区） | 3.68 |
| Builds | 1.82 |
| ForgeRuntime | 0.77 |
| .git | 0.69 |
| Tools | 0.65 |
| Assets | 0.20 |
| Docs | 0.18 |
| 根目录合计 | 约61.34 |

子行已经包含在对应父行中，不应重复相加。CardImages 有约 21 万个文件，包含多语言、多系列与印刷版本，并非只存一套英文卡图。因此约 21G 可以对应卡图目录，但不代表完整项目。

## 清理范围与实际结果

主 Unity 项目仍由编辑器及两个导入工作进程使用，保留其 Library／Temp／PackageCache。旧 `.codex/validation/description-unity` 工作区未被这些 Unity 进程使用，其 Library 约 3.10 GiB，确认候选包／编译缓存约 3.00 GiB。`.tools/arena-video-review/packages` 约 0.082 GiB 是可重新安装的工具包。

尝试删除旧验证工作区 Library、再缩小到其 PackageCache、以及独立视频工具包目录，工具均返回 `blocked by policy`，没有提供更具体的拒绝原因。所有删除命令在执行前被拒绝，实际释放量 **0 GiB**；目录仍保留。没有换其他工具或脚本绕过拒绝。

`.tools/mechanic-coverage` 包含历史批次、原生输入、结果、源码与截图；`.tools/card-names/attempts` 约 2.55 GiB 包含失败与回放证据。它们虽占空间，不能当普通缓存删除。卡图、运行资料、Git、源码和全部验证证据未动。

## skill 更新

- PlayerCounterSymbolFit 以实际图标网格统一最长边，保留比例和原数字排版。
- 指示物局部修改只验证指示物，不自动重跑其他 UI／规则、全机制覆盖刷新。
- 新增 workspace-maintenance 引用：先统计及分类，保护活动项目和证据，验证绝对删除边界，清楚区分已删／未删与释放量。
- quick_validate 检查 skill 格式；没有运行 Forge 或 Unity 对局测试，长期 goal 保持暂停。

详细只读统计与待清理清单位于 `.tools/storage-audit-1005/before.json`、`unique-before.json` 和 `cleanup-plan.json`。这些本地资料含目录明细，不含进程完整命令行。
