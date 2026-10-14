# 2026-10-14 项目文件整理

## 追加深度整理：排除 Unity 生成目录

- 又删除两份非 Unity 下载缓存：Windows JRE ZIP 与第三份已解压的 Linux JRE，逻辑大小共 177.8 MiB。分别逐文件核对 316／258 个文件与保留版本一致，当前运行包与服务器构建仍在。
- 2769 个大型历史证据／原始数据文件升级 LZX，原路径与内容保持，每个文件操作前后 SHA256 核对通过；存储字节额外减少 5.83 GiB。这是压缩节省，不计作删除证据。
- 本阶段 D 盘净可用空间增加约 5.99 GiB，加上第一阶段约 2.4 GiB，累计净增约 8.40 GiB；当前 D 可用 18.74 GiB。
- 原失败、唯一断言、账本引用和当前基线保留。Library／Temp／Builds／Unity 测试播放器未在此追加阶段删除，未执行规则或 Unity 回归，旧 goal 保持暂停。
- 精确清单／哈希日志：`.tools/workspace-cleanup-1014/extra-plan.json`、`extra-receipt.json`、`compression-probe.json`、`compression-plan.json`、`compression-journal.jsonl`、`compression-result.json`、`deeper-summary.json`。维护 skill 已补充大型证据压缩升级的方法与实际反例。

以下为第一阶段原记录。

## 已完成

- 删除 63 个精确目标：两份旧客户端构建、一份旧 Linux09 服务端构建，五套旧测试客户端的生成目录／二进制及 Python 字节码缓存。
- 目标逻辑大小 3.873 GiB；D 盘净空闲空间增加 2.406 GiB。两者分列，不将文件长度当作真实释放量。
- 清理后 C 可用 41.28 GiB、D 可用 12.75 GiB；卷余量包含系统并发写入影响。
- 52 个保护文件逐一 SHA256 核对通过，计划目标全部移除，无删除失败。

## 保留

源码、Assets、卡图、ForgeRuntime、主 Library、Git、录像工具、原始录屏、验证原输入／结果／失败／截图／源码与进度账本保持。Temp 在清理前已不存在，本轮未处理；首次核对误假设其存在，修正为读取清理前扫描，不重做删除。旧测试目录的 Decks／Replays 保留，所有目录链接保持，未遍历链接目标。

最新客户端完整构建 `Builds/ClientEndMenu1014`、上一版 `Builds/ClientGameplayFix1014`、最新服务端 `Builds/LinuxServerGameplayFix1014`、前一版及 Linux JRE 来源 `Builds/LinuxServerUpdate1014` 和桌面回退文件保留。桌面当前客户端 SHA256 核对通过。结算菜单 ZIP 在清理前已不在原桌面路径，本轮未删除该 ZIP。

## 证据和边界

精确目标、大小、原构建身份与删除原因在 `.tools/workspace-cleanup-1014/plan.json`，真实删除及卷余量在 `receipt.json`，保护核对在 `summary.json`。旧发布身份、原生／UI 断言保留；淘汰的生成二进制路径不再可直接启动，重放需要重新构建。

未清理未知验证记录，未修改规则／机制进度，未运行对局或效果回归，未重建或更新服务器，暂停的 goal 保持暂停。`.tools` 的大型文本多数已经压缩，52.7 GiB 的逻辑大小不能当成物理占用或全部可删除空间。
