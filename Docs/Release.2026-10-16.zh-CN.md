# 2026-10-16 合并更新：内存、动态语言及组卡／战场界面

## 内容与构建范围

本批按用户授权构建当前完整工作区，包含其他任务完成的客户端／服务端内存与读取优化、动态对战文本来源与漏译记录，以及此前的房间、卡组、数量角标、费用曲线、手牌扇形／浏览／悬停／拖曳等修改。构建前冻结 3,123 个受版本管理或未忽略的新建构建输入文件，保留当时 Git 状态；构建后继续检查源码身份。没有筛选成仅当前任务自己的修改，也没有新增规则或翻译资料修复。

Windows 与 Linux Dedicated Server 均从该工作区重新构建。实际 BuildReport 分别为 0 错误／8 警告和 0 错误／9 警告。程序集与前一任务已验证的最新语言构建逐字节一致；内存、语言及原交互证明继续保留原批次身份，不把重建、目录查询或截图当作全机制、完整公网对局或新性能基准。

| 构建 | 输出 | Assembly-CSharp.dll SHA-256 |
| --- | --- | --- |
| Windows 客户端 | `Builds/ClientRelease1016` | `2cce72f79681fff94c53ccb2bcc4ec98af99523827c2aa2bc985899c1c12fda1` |
| Linux 服务端 | `Builds/LinuxServerRelease1016` | `46322fe8434326ab2c65d78ae7a9110a9fc574427d3af0359d2e6feaa856d021` |

## 客户端更新包

桌面合并包为 `C:/Users/Sam/Desktop/MTGB-客户端更新包-20261016.zip`，359,361,643 字节，SHA-256 `2517e5cfbc529116fc7218b9ba90af047ea2e04d19de30f3f3c6c57b7d2d9a47`。核对旧包中的 41 个路径，按当前构建取其版本并纳入合并清单；已经过时的 Addressables 路径另列，当前目录与 bundle 一起交付，不要求先安装旧包。

包内 654 个覆盖／新增文件通过 ZIP CRC 与逐项 SHA-256；桌面客户端已应用并逐项核对。旧 ZIP 移至 `.tools/release-1016/previous-combined-update.zip`，变化文件另保留短期回退副本，桌面仅保留新合并 ZIP。覆盖清单不含用户 `CardImages`、`Decks`、`Replays` 和偏好设置。

本包合并开源字体替换。当前字体策略检查 13 份字体／14 份审核文件；实际新 `resources.assets` 中 9 份模板字体完整字节均存在，五份禁用旧字体指纹命中为零。Forge 固定上游完整源码 ZIP、当前桥接与原生修改，以及当前仓库的构建／审核／测试输入也随包交付于 `ForgeRuntime/source`；因此本包比旧纯运行差异包更大。来源、许可和构建输入见包内 `SOURCE_CODE.md`、`origins.json`、`source/repository` 与 `release-build-inputs.json`。本次没有新增源码 Git 提交，不能将旧公开 overlay 指认为这些新增修改的唯一交付。

37,521 个 Windows 运行清单文件逐项核对，runtimeHash 为 `90d62ad34eb8e72e20238a264fd5ecc742de6fc006b38cff1bf2e47463e1c84c`。桌面正式程序以隔离配置无图形启动，读取 97,784 条卡牌和中文菜单后正常退出，`client-smoke.json` 为 PASS。退出日志有既有 ComputeBuffer 清理提示；启动验证不代表完整图形对局或所有平台性能。

## 服务端包与线上部署

Linux 包 `.tools/release-1016/MTGB-服务端更新包-20261016.tar.gz` 为 484,401,920 字节，SHA-256 `2f56737b5b40b330eb84b80d45a51012d33b9e229f522d72c98d91d7679296bc`。38,274 个交付文件及归档内容逐项核对，运行清单为 37,463 项，runtimeHash 为 `de2e59cfe33c3d4d60ba679c87aec332567935d8e8adec016f004a7e6849de94`。程序、JRE bin 和启动脚本的归档执行权限已核对。

发布前实际线上仍是 `/opt/mtgb/releases/20261015-linux-13`，不是本地已准备好的优化包。TCP 没有已建立连接；旧进程的生产目录查询在本机回环也超时，不能由 TCP 状态推定所有 UDP 玩家状态。

已发布到 `/opt/mtgb/releases/20261016-linux-14`。变化文件 626 个、合计 360,363,217 字节；切换前逐项校验全部 38,274 个交付文件，旧 Linux13 保留回退，原生清单没有需退役的路径。

服务使用本次 `run-server.sh`，保留原 750／900 MiB 软硬内存门槛、256 MiB 交换上限、125% CPU 与 Java 384 MiB 堆上限，并实际确认 `MinHeapFreeRatio=10`、`MaxHeapFreeRatio=20`、`-ShrinkHeapInSteps` 已进入进程环境。主进程 active、重启数 0、TCP／UDP 2323 监听，生产回环目录返回 `MTGB-ROOMS/1`。在服务挂载命名空间内以 `mtgb` 用户实际写入并清理临时文件，确认 `Replays` 仍指向 `/var/lib/mtgb/Replays`；其他三个服务的切换前后 PID／状态一致。

最终结果见 `.tools/release-1016/deployment-stage.json`、`deployment.json`、`wan-query.json` 与 `post-health.json`。此次没有把回环／公网目录查询、冷启动内存或目录写入当作完整公网对局、长局性能或一局新的录像存档证明。

上线后从 Windows 实际公网查询三次，均返回同一新实例，耗时约 0.262／0.332／0.234 秒。后续采样主 PID 498391 未变、重启数 0，当前进程组 max／OOM 事件为零，memory PSI 为零；这些是此次冷启动／目录查询窗口的健康数据，不能拿来与旧长期进程直接计算内存降幅。初始及后续进程组约 121.5／125.8 MiB，也不代表真实对局及 Java 全卡库加载后的占用。

## 发布检查与边界

后续专用服务端移除 App UI 依赖，并已部署到 Linux15；详见[对应构建、部署及清理记录](ServerAppUIRemoval.2026-10-16.zh-CN.md)。本页 Linux14 的哈希和健康数字保留原发布批次身份，不能作为 Linux15 的新测试结果。Git 整理时再次只读检查线上为 Linux15、active／running、重启 0，程序集哈希与该后续记录一致。

证据目录 `.tools/release-1016/`。包和桌面安装均按冻结清单核对，后续 Git 文档与 Unity 生成的 Addressables 状态不伪装成新代码回归。平台恢复时出现一次生成的 `Assets/AddressableAssetsData/link.xml` 暂态读取错误；保存记录后刷新，编辑器当前错误日志为空，恢复 Windows64／Player、Main、非 Play 且场景未修改。

保留打包驱动修正：第一次差异部署把完整清单放进 SSH 命令，连接被重置；改为 SFTP 传送清单后继续。Python 的时区资料库缺失只影响清单时间生成，改用日本固定 UTC+9；Forge 源码封装脚本与仓库脚本存在导出入口差异，保留原封装，并额外交付当前仓库脚本。上述失败未切换线上或覆盖未校验客户端。

本批仅发布现有实现，不恢复暂停的机制 goal，不通过重跑旧效果凑当前通过数。原内存／长局指标、动态文本样本与交互证明见各专项记录，未知机制及未穷尽动态文本仍保留原限制。


## 自动优先权追加发布（同日）

本次把「自动模式遇到可用牌或非产费异能时停住」合并到桌面现有累计更新包，并重新构建两端。264 组策略组合、四类原生优先权输入和 31 项界面自动提交检查保持原验证身份；本轮再次核对受测源码哈希与构建输入一致，不将发布检查计作新的卡牌效果或长局回归。

- 桌面仍使用 `MTGB-客户端更新包-20261016.zip`，共 359,364,908 字节，SHA-256 `f46adfc8de4be3e3ecf2b9da3413b21f21ef72f82e2ab7f15c8cd688ba2d7987`；包含此前全部 654 个累计路径，包含开源字体替换，无须再安装旧更新包。
- 桌面 `MTGB` 已覆盖并逐项核对，卡图／用户牌组／录像／偏好不在覆盖清单中。客户端程序集 SHA-256 `071a2d6454ed9e86218a74a0dd549480bddc234ecdade7741b1bcf3105d10427`；独立配置下实际桌面程序启动、资料就绪、Main 界面与退出通过。
- 客户端构建：result = Succeeded；errors = 0；warnings = 494；bytes = 471535027；seconds = 344.641479。Linux 构建：result = Succeeded；errors = 0；warnings = 929；bytes = 270959640；seconds = 275.3521581。警告原文保留在本批目录；未把构建成功写成零警告。
- Linux16 交付程序集 SHA-256 `cd6272171ab02da7d94f2973227317d07c354adb58dbf39cf5ab125a5a8e6fd8`。全部 38,267 个交付文件在切换前核对，App UI 程序集／原生插件／初始化回调继续排除；Forge 运行清单 `de2e59cfe33c3d4d60ba679c87aec332567935d8e8adec016f004a7e6849de94` 与已发布 Linux15 相同，保留完整对应源码。
- 公网已切换至 `/opt/mtgb/releases/20261016-linux-16`，Linux15 保留回退。先在独立端口实际启动候选包，再切换生产；原服务配置、750／900 MiB 软硬内存门槛、256 MiB 交换上限、125% CPU 与 Java 384 MiB 堆策略保持，持久 `Replays` 目录在服务用户／挂载权限下可写，其他三个服务状态／PID 不变。
- 三次公网目录查询通过，同一新进程 active／running、重启 0，无 App UI／GTK 启动错误或采样窗口内的 max／OOM 事件。这些是发布健康检查，不是新的完整公网对局或长局性能结论。

证据目录 `.tools/release-auto-1016/`。保留打包跨盘替换失败：新 ZIP 位于 D 盘，直接 `os.replace` 到 C 盘返回 WinError17；新旧已校验文件均保留，改为复制到桌面同盘临时文件、核对哈希后再原子替换，完成复验。旧包及被覆盖文件保留回退。构建期间生成的 Addressables 状态与临时云设置变化单独记录，未混入新的功能验证。
