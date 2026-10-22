# 项目目录整理（2026-10-22）

本轮仅整理 `D:/project/MTGB`。桌面客户端和更新包仅读取核验，未修改；服务器未操作。

## 已完成

- 删除 7 个过时回滚包／重复更新包，共 2,623,815,277.0 字节（2.444 GiB）。
- 对 6 份已关闭编辑器留下的日志应用 NTFS LZX 压缩，节省存储 637,741,529 字节（0.594 GiB）；原路径、逻辑长度和 SHA-256 均保持。
- 目标文件合计节省约 3.038 GiB。D 盘空闲由 3.022 GiB 增至 6.058 GiB，卷净增加 3.035 GiB。目标文件节省与卷净变化分别计量，卷数据受其他进程影响。

## 删除依据与历史记录

桌面 `MTGB-客户端更新包-20261021.zip` 通过完整 ZIP CRC 校验，SHA-256 为 `6a148cbacb3a774f51e0776bf13ba30120fd0271f696f5438c90e6055e1ac883`；桌面 `Assembly-CSharp.dll` 的 SHA-256 为 `350e7b25487db6e5dc1d31f7e7d8721ac55db6fe684c803459c2f38f2365eb10`，与最新发布回执一致。重复副本字节相同，其余为该版本之前的发布事务备份；7 个包均不含 Decks/Replays/CardImages/UserSettings 目录。

原发布目录中的脚本、清单、回执和哈希继续保留。历史回执所指的以下大文件已经退役，不能再把这些路径视为可用回滚包：

- `.tools/release-1021/desktop-rollback.zip`
- `.tools/release-1021/previous-combined-update.zip`
- `.tools/release-actions-1021/combined-update-new.zip`
- `.tools/release-actions-1021/desktop-rollback.zip`
- `.tools/release-actions-1021/previous-combined-update.zip`
- `.tools/release-bo3-1021/desktop-rollback.zip`
- `.tools/release-bo3-1021/previous-combined-update.zip`

## 保留与验证

- 保留卡图、牌组、录像、录屏、全部 Git 历史、ForgeRuntime 运行资料及授权／重建所需源码归档。
- 保留主项目 Library，避免下次打开 Unity 重新导入。
- 保留当前机制基线、近期增量、唯一证据和未解决问题的验证资料；未按目录日期或隐藏属性批量删除。
- 不移动源代码、资料与取证目录，避免破坏脚本和报告引用。无法证明冗余的旧卡牌资料归档继续保留。
- 清理前记录的 71 个修改／新增文件，清理后 SHA-256 全部相同；引擎与 Unity 完整基线结果文件仍存在。
- 本轮未修改游戏逻辑，不启动 Unity，也不重跑无关机制测试。

## 详细记录

所有精确路径、删除前大小与哈希、压缩前后存储量及保留校验位于 `.tools/workspace-cleanup-1022/`：`inventory.json`、`protected.json`、`archive-review.json`、`retained-release-verification.json`、`deletion-plan.json`、`deletion-receipt.json`、`compression-plan.json`、`compression-journal.jsonl`、`compression-result.json`、`preservation-verification.json`、`summary.json`。

初始目录盘点跳过 junction／symlink，单列链接及错误；文件使用实际 inode 去重。存储估算不含 NTFS 元数据和目录本身分配，不能把逻辑大小当作可回收空间。清理后仅复核目标及卷空闲量，未重复进行全盘扫描。
