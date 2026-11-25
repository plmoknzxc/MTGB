# 2026-11-08 独立更新与完整免卡图客户端

## 发布身份

- 源码输入在构建前冻结为 `.tools/release-independent-1108/source-inputs.json`；原生 Forge 来源、32 个桥接源文件和 58 个已应用的原生补丁与 `ForgeRuntime/source/origins.json` 核对一致，运行清单逐项哈希通过。
- Windows 客户端程序集 SHA256：`46adddde1120d11f6c80199db18b3c24e72cbaeede3c016ab172bd22a54071c5`；客户端 Forge 运行清单 SHA256：`ea0c961c26fbe2941c3c660e0db00c425084a8618f07f5711b464b0b4c0e43b8`。
- Linux 服务端程序集 SHA256：`2bf3a6aa749e23faa91deab2fab37b4b768b6e0cb1b4f62fc0898530ac0b2898`；服务端 Forge 运行清单 SHA256：`6dc86ec402cd513730e49ef2a5a8ccdaf0ff007a54d09492f01421f4745c9662`。两端协议仍为 22。
- 客户端 BuildReport 为 Succeeded，隔离配置启动至 Main 且资料就绪。BuildReport 计入 1 条编辑器 URP 临时资源异常；交付客户端启动日志无对应运行异常。服务端 BuildReport 为 Succeeded、0 错误。构建时源码仅有 Addressables 状态和字体动态图集生成差异，见 `.tools/release-independent-1108/input-drift.json`。

## 交付

- 已覆盖 `C:\Users\Sam\Desktop\MTGB` 的 73 个差异文件，并逐项校验 37,943 个程序文件。原有 36,862 张卡图、5 个卡组文件和 1 个录像文件的受保护清单未变。桌面安装版隔离启动至 Main 且资料就绪。
- 独立增量包：`C:\Users\Sam\Desktop\MTGB-独立更新包-20261108.zip`，101,141,607 字节，SHA256 `41bd8cb867d00c17ad387d3af99ec4df453adf192729f6b74b7a99007697e204`。基底为 2026-10-27 桌面安装版；73 个载荷文件及虚拟覆盖后的 Forge 运行清单校验通过。
- 完整免卡图包：`C:\Users\Sam\Desktop\MTGB-完整程序-不含卡图-20261108.zip`，620,564,641 字节，SHA256 `9f1818ee280b94f9410fae3597d1477b3daf9e34cefeed10c6ca1624971a88cc`。含完整程序、Forge、Java、FFmpeg 和许可证；无 `CardImages`。逐项解压校验后，在独立目录无卡图启动至 Main 且资料就绪。
- 两包的逐项文件清单保留在 `.tools/release-independent-1108/`；同名 ZIP 已交付桌面，可凭上列 SHA256 核对。旧桌面 ZIP 未改写，没有新增备份归档。

## 服务端

- 由 `/opt/mtgb/releases/20261027-linux-26` 派生候选 `/opt/mtgb/releases/20261108-linux-27`，只替换 59 个差异文件；37,822 个发布文件校验通过。录像继续使用 `/var/lib/mtgb/Replays`。
- 候选在独立端口启动并通过目录查询，原正式服务保持运行。原生 Forge 开局分别验证普通模式、争锋及法禁的生命值与 AI 席位。正式切换前重新检查空房间与连接，随后切到 Linux27。
- 切换后正式服务 active/running、重启次数 0，公网目录可查询；录像目录写入、原内存与 CPU 限额及其他服务状态保持，上一正式版本保留供回切。证据见 `.tools/release-independent-1108/deployment.json`、`server-after.json` 和 `linux-native-result.json`。

## 验证边界与构建缓存

本次验证覆盖程序启动、目录、原生三类开局以及发布文件完整性；未把目录查询当作公网完整对局，也未重跑全卡池效果。当前工作区未执行 Git 提交或推送。

构建期间 C 盘空间不足，曾将可再生成的旧 Unity 包下载缓存无损移动到 `D:\UnityCacheRelocated-20261108`；随后已移回原位置，该临时目录目前不存在。没有删除项目、卡图或安装版文件。此缓存不属于发布载荷。

完整包启动验收后的临时解压目录 `.tools/release-independent-1108/full-zip-smoke-2` 目前不存在。2026-11-09 核对时，已验收的完整免卡图包仍在项目归档中，并另复制到桌面同名文件；两份 SHA256 一致。桌面原有的 `MTGB(不包含卡图).zip` 哈希不同，未改写。
