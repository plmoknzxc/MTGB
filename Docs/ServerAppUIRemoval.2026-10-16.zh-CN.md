# 专用服务端移除 App UI（2026-10-16）

此前实际线上 Linux14 启动时包含六个 `Unity.AppUI` 运行程序集、运行初始化回调及预加载 `libAppUINativePlugin.so`。插件依赖服务器未安装的 GTK，导致 `DllNotFoundException`；App UI 捕获后回退，服务器仍能监听并回答目录查询。本次按用户要求从专用服务端排除这部分界面依赖。

`DedicatedServerBuildMenu.Build` 只在服务端的 `BuildPlayerOptions.extraScriptingDefines` 添加包原生支持的 `APP_UI_EDITOR_ONLY`，使该包运行 asmdef 不参与玩家构建；同时在本次 BuildPlayer 调用期间通过 `PluginImporter.SetIncludeInBuildDelegate` 排除 App UI 原生插件，并在 finally 恢复。没有修改 PackageCache、包导入元数据、全局脚本宏、客户端或编辑器包。

排查确认 Standalone 目标原本已包含 `APP_UI_EDITOR_ONLY`，但 Server 有独立的目标宏配置，原值为空。新增的构建参数明确覆盖服务端目标，避免依赖客户端宏配置；构建前后的 `ProjectSettings.asset` SHA-256 与先前发布输入相同。平台恢复后 Windows64／Player、Main 未修改，App UI 编辑器程序集与原生插件仍可用。

验证及发布记录位于 `.tools/server-appui-1016/`。旧包使用 `check_package.py` 检查为 FAIL；新包必须同时通过无 App UI 程序集／插件／运行回调检查、实际 Linux 无 GTK 环境的隔离启动检查，以及切换后的公网目录与完整启动日志检查。Forge 运行清单、规则资料、协议和启动器内存策略保持本批之前的已发布身份，不把这次构建／启动检查当作新的全机制或长局性能回归。

## 最终验证、部署及清理

Linux 构建成功，BuildReport 为 0 错误／929 警告；其中 920 条为 Inference 的 ConvGeneric 着色器变体警告，其余 9 条为既有 API、字段、导入器和 Pipeline 提示，明细已保存。平台切换期间另有一次 Addressables 生成 `link.xml` 暂态读取日志，保存记录并恢复、刷新 Windows 后，编辑器错误日志为空。没有将清空控制台代替构建或运行检查。

新包交付 38,267 个文件，六个 App UI DLL、一个原生插件及 App UI 运行初始化回调均不存在。37,463 个 Forge 运行清单文件逐项核对，runtimeHash 仍为 `de2e59cfe33c3d4d60ba679c87aec332567935d8e8adec016f004a7e6849de94`；启动脚本 SHA-256 不变。新服务端程序集 SHA-256 为 `b116c257ea10bc1d4c85c1768758647910403e1a2e5dfbbdc801f58b888b08d1`。

归档 `.tools/server-appui-1016/MTGB-server-no-appui-20261016.tar.gz` 共 483,944,016 字节，SHA-256 为 `ebc11e65aae5a9d17aac0172b0a9589de01066752254ac92399eb14dc2a44e51`；归档内容及执行权限已核对。公网候选目录先通过全部交付文件校验，并在实际服务器上用独立端口 23234、独立临时服务启动，目录响应正常，日志无 App UI／GTK／异常，测试实例已停止。

切换前再次确认公网房间为空，无已建立 TCP 客户端；随后发布到 `/opt/mtgb/releases/20261016-linux-15`。新服务 active／running、重启 0，公网目录连续查询正常，完整启动日志不再出现 App UI、GTK 或异常。原 service unit 未修改；750／900 MiB 内存门槛、256 MiB 交换上限、125% CPU、Java 384 MiB 堆上限与堆收缩参数、录像目录写入和其他三个服务均核对通过。旧 Linux14／Linux13 保留回退；不以本次空闲启动内存宣称长局性能改善。

按用户之前“无问题后删除旧产物”的授权，移除 12 个旧构建目录与 4 个旧服务器归档／临时核心包，共 377,496 个文件、6,787,120,802 字节逻辑长度。清理窗口 D 盘空闲净增 7,252,705,280 字节（约 6.75 GiB），结束空闲约 11.64 GiB；净增可能包含同期写入，不能与逻辑大小混用。保留当前完整客户端、最新服务端、上一发布的回退构建／归档、桌面新更新 ZIP、桌面回退、源码、运行资料、卡图、真实牌组、录像及历史验证记录。旧构建中的 24 份示例牌组输入先保留带哈希的小型快照。

最终机器可读结果见本批 `audit.json`、`deployment.json`、`isolated-smoke.json`、`post-health.json`、`cleanup-plan.json`、`cleanup-journal.jsonl` 与 `cleanup-result.json`。旧内存批次的待部署计划标为已被部署及清理取代，并保留原计划快照；原性能／机制审核结果保持原批次身份。此次没有进行新的完整公网对局或长局回归，也没有恢复暂停的机制 goal。
