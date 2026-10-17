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


## 艾莉堆叠异能语言追加发布

Windows 客户端及原桌面累计 ZIP 已更新，保留前一自动模式发布的全部累计路径及源文件身份。纯客户端显示修复，公网仍为 Linux16。新构建、逐项覆盖、字体／原生运行时、启动检查及验证边界见 [本批记录](EmryAbilityLocalization.2026-10-16.zh-CN.md)。


## 展示一致性追加发布（同日）

本轮 Windows 客户端、桌面累计更新包和公网 Linux17 已更新。同名玩家只用原蓝／红颜色区分，不增加座位编号。私下检视后的普通抓牌不新增双方卡面提示；公开搜寻、磨牌提示保留。公共文本工厂首帧字号、长目标说明和双面地／生成卡文显示修复详见 [原因与分层验证](UIConsistency.2026-10-16.zh-CN.md)。

- 客户端程序集 SHA-256：`e9ea13b0cdec6c66e76d51158def4e4702b1177ab4eab49a32cfdd50ca92e069`；原生运行清单 `6b2a5c50f567dd95c8430f0aca6f39b96e6cafc6abbf59799276b3803e1b3f49`，37,521 文件逐项核对。
- 原桌面 `MTGB-客户端更新包-20261016.zip` 已替换为本批累计包，359,603,953 字节，SHA-256 `341a95ee4cff9a119fad8f6061bbbfbf0ed2a981cd91e4532f22a26f84e8e95e`；保留此前全部 654 个累计路径。实际桌面目录逐项安装核对并在隔离配置下启动至 Main、资料就绪和退出通过。卡图、用户卡组、录像及偏好不在覆盖清单中。
- Linux17 程序集 SHA-256：`3a76f8fb8ba15ac5eb7ad2395ecdd3511a297262afdd760c35245bc3949b2b86`；原生运行清单 `69affe22c440c3d449d40d48706c004404bdea4f896d421ac9c2abffebf7b266`，37,463 文件。交付包共 38,267 文件，App UI 的程序集／原生插件／初始化回调仍排除，准确对应 Forge 完整源码和修改来源随包交付。
- 两端相对各自上一发布运行清单，只有 `ForgeBridge.class`、`BridgeCardNotices.class` 两个文件内容变化，没有增加／删除清单路径；其他原生类、卡牌资料和 Linux Java 未变。
- Windows 构建成功，0 错误、8 警告；Linux 专用构建成功，0 错误、929 警告，原文均保留。包内 9 份实际字体字节核对通过，禁用字体命中为 0。字体临时字形缓存还原按明确字段和既有身份核对，没有放宽源文件检查。
- 公网 `/opt/mtgb/releases/20261016-linux-17` 已在独立端口先通过启动检查，再确认无房间和既有 TCP 客户端后切换；Linux16 保留回退。生产同一 PID 的三次目录查询通过，active／running、重启 0，采样窗口无 max／OOM 事件及 App UI／GTK 启动错误。750／900 MiB 软硬限额、256 MiB 交换、125% CPU、384 MiB Java 堆保持；持久录像可写，其他服务 PID／状态未变。

证据 `.tools/ui-consistency-1016/audit.json`。171 项三语／三档字号及实际消费者检查、旧文字回归、原生通知和地牌进场结果分别记录；没有新增完整公网实局、长局性能或全卡池机制通过结论。旧机制 goal 仍暂停，Editor 已恢复 Main、Windows Player、非 Play、中文、字号 1.0、场景无未保存修改。原桌面包和被覆盖文件保留在本批回退目录。


## 牌库、选牌排版与产费窗口追加发布（同日）

用户授权后，完整当前工作区已构建 Windows 客户端和 Linux Dedicated Server，并覆盖桌面 MTGB、合并原累计 ZIP、部署公网 Linux18。此前“未打包部署”是各功能验证批次当时的状态；本次交付覆盖那三批的最终源码，不改写旧证据身份。

- 选牌说明去重、按实际 viewport 测量及滚动修复；牌库从顶至底显示全部编号位置，隐藏牌显示卡背，按权限显示公开／私有已知位置，洗牌清除过时位置；纯产费候选改用紧凑窗口，复杂费用与副作用仍完整展示，混合非产费异能保留原选择合同。
- 客户端程序集 SHA-256 `85ad0f55444f0aa3f93aab8e47b2a3386fa721ef2fa4ccc0b8f8fa409eb622f5`；原生运行清单 `880c0d26ec70c69fbabd285dc75d39efe673cd58fbbc04e5773cb3bf5e86252f`，37,525 个文件逐项核对。Windows 构建报告原文见 `.tools/release-library-mana-1016/client-build-report.txt`。
- 原桌面 `MTGB-客户端更新包-20261016.zip` 已原位更新，359,699,262 字节，SHA-256 `e088a3e27c2875e91ad84f2b6583545660c941871baf6c9a13371615fded9f1c`，累计 663 个载荷路径。旧包载荷逐项保留最新版本，旧包与被覆盖文件保存在 C 盘本批回退目录。卡图、用户卡组、录像和偏好不在覆盖载荷内；桌面全部载荷与运行清单校验通过，隔离配置下启动至 Main、资料就绪与正常退出通过。
- Linux18 程序集 SHA-256 `f734e496cd15df12cc8abc1eaaf66d6189a3f8b7a234629dad86bb1621fcea1e`；原生运行清单 `59c45c8445b6298b1a3f84840ce6fb9ed6bde932154556ed1e2ba71374ddab1c`，37,467 个文件，交付包共 38,273 文件。准确 Forge 上游源码、当前桥接源码、NativePatches 及 GameAction 补丁随两端交付；来源哈希更新，包内 9 份开源字体字节核对、禁用字体零命中通过。
- 两端各比上次清单新增 4 个 BridgeLibraryPositions 类／内部类，已有 10 个原生类内容变化，无删除项；卡牌资料、目录及对应平台 Java 均未变。GameAction 完整补丁重放与已验证源码相等，保留既有 Craft／Meld 修改；新增观察范围和原生／UI 证据分别见 [牌库验证](LibraryBrowser.2026-10-16.zh-CN.md) 与 [产费选择验证](ManaChoices.2026-10-16.zh-CN.md)。
- 公网 `/opt/mtgb/releases/20261016-linux-18` 先独立端口启动检查，再确认无房间和既有 TCP 客户端后切换；Linux17 保留回退。正式服务 active／running、重启 0；服务器内及本机公网各三次目录查询通过，同一正式 PID 未变，检查窗口无 max／OOM 事件和 App UI／GTK 启动错误。750／900 MiB 软硬内存、256 MiB 交换、125% CPU、384 MiB Java 堆及持久录像权限保留，其他服务状态／PID 未变。

最终审计 `.tools/release-library-mana-1016/audit.json`，构建／字体缓存／源输入变更检查和部署回退证据在同目录。两端 BuildReport 均为 0 错误；Editor 另记录一条 Dedicated Server 构建前 Addressables `link.xml` 导入消息，保留原文和未完全建立的原因，未改上游包，不能声称控制台零错误；专用载荷检查、独立与正式启动另行通过。分类清单只刷新分类，仍为 2582 未登记效果测试、318 需重测；既有功能测试保持原批次身份。本次没有重跑完整公网对局或新长局性能基准，不新增全卡池机制或全部动态语言通过声明，旧机制 goal 仍暂停。
