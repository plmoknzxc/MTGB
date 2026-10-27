# 2026-10-27 独立更新包与服务端

最新状态：已完成桌面安装更新及启动／导出检查；随后按用户要求整理文件并发布本轮源码。更新公告见 [更新说明](UpdateNotes.2026-10-27.zh-CN.md)，清理与源码收尾见 [整理记录](WorkspaceMaintenance.2026-10-27.zh-CN.md)。以下“未覆盖／未推送”等描述保留各批次当时身份。

按用户“打更新，独立，更新服务器”发布。已制作桌面独立ZIP并切换正式服务器；没有覆盖桌面安装版、合并旧ZIP、创建备份归档或提交推送Git。旧目标保持暂停。

## 交付

- 文件：`C:/Users/Sam/Desktop/MTGB-独立更新包-20261027.zip`
- 大小：75,470,865字节，43项程序载荷，另含更新说明和client-update-manifest。
- SHA256：`b6e75d9ad5842c75641985541dc02c752c025c600bf8ffbbe07dcbad7692eb26`
- 基底：已安装26号独立更新的客户端；程序集 `2e69c8dd71d5b140a18bbd0f3d6a7bb4c0cf6b4d2e90d8f6483a83f47e21c4af`。
- 新客户端程序集：`967dafe0100ec1e633eb23dceaad39dea0bb55b95e04bdc7eb477b397c05837c`；协议22。

关闭客户端，将ZIP内MTGB文件夹的内容覆盖原MTGB目录，不再套一层目录。卡图、Decks、Replays及偏好不在增量覆盖范围；旧累计ZIP和26号独立ZIP哈希保持。首次交付时本机桌面安装仍是原基底；随后已按追加授权覆盖，不需用户再次应用此包。

本包纳入工作区已完成的线上1v1 AI、起手加载优化、放逐／备牌费用和许可、Stock Up排序、空步骤与连续选择响应、观战／私有施放准备、部分模式和文本修复。原功能证明各自保留：OnlineAi、OpeningLatency、SideboardAndStockUp、ExileCastingAndIdleLatency、GameplayInteractionFixes、BattleTextReplay、CardStatusStrip、CommandModes对应文档。本轮发布不增加全卡池或AI决策质量结论；没有升级Forge上游、限制赛或四人接入。

## 构建、来源和包验证

构建前冻结2969项Assets／Packages／ProjectSettings输入及Git状态。Windows构建215.32秒，0错误、494警告；Linux构建95.83秒，0错误、930警告。完整警告分别保留于 `.tools/release-independent-1027/build-warnings.json`、server-build-warnings.json；不宣称零警告。最终仅Windows Addressables状态和Unity云设置生成变化，产品代码与场景未漂移；中日字体与本轮准确构建前副本逐字段比较和字体来源校验通过。

客户端和服务端保留新构建于 `C:/Users/Sam/AppData/Local/MTGBBuilds/20261027-independent/`。Windows37558项原生清单、Linux37500项清单逐项哈希通过；31份桥接源码与origins、已编译原生补丁来源一致。

- Windows裁剪运行清单：`cebb491673cc66aeae667d4fa3ad5162f4007a1395bec6b4343129d425de3da9`
- Linux裁剪运行清单：`b20bd4f8e43e5ac4e50ccbbc54c571c0f280f4c330620e6b984327c500c9c2e4`
- Linux程序集：`583becb512c5de271165b89c5198db4fd1d14d1aab33f2b7c8c4a488073508bd`

独立包同时核对上次分发的92项载荷与保留基底（包括编码器），不是只相信后来可能被修改的构建目录。ZIP CRC、大小写路径唯一性、每项SHA256及虚拟覆盖后的完整运行清单通过。

## 实际程序与服务端

新客户端以隔离配置正常进入Main、资料就绪、5次采样、退出码0，检查的异常列表为空。使用交付exe及自带编码器、剥除开发工具PATH导出4秒视频：1920×1080、30FPS、120帧、BT.709，实际截帧确认新版战场、手牌和卡图；不将其当长局导出／文件对话框全流程验证。

初次及第二次导出夹具未正确接入已有卡图（首次启动自动创建了空CardImages，第二次条件判断跳过链接创建），只证明编码器和场景渲染，不能授卡图显示通过。保留两次输出；随后仅移除已核对为空的构建内CardImages目录，创建指向既有桌面卡图的目录链接，不修改原卡图；第三次client-export-final检查通过。链接不在ZIP或服务端载荷中。

服务器候选 `20261027-linux-26` 原位硬链接复用旧版本，34项变化文件通过临时文件原子替换，37809项发布文件哈希通过，无退役文件删除。旧正式版本保持原身份。候选先在23234独立端口启动并查询通过，随后正常停止。

候选自带JRE/classes/lib实际原生启动与结束三项通过：本地练习AI自定义37生命；线上争锋AI固定25；线上法禁AI固定20。此为原生开局／认输增量，不是三场完整公网对局。

正式切换前房间及TCP连接均为空，未中断对局。已由 `/opt/mtgb/releases/20261026-linux-25` 切到 `/opt/mtgb/releases/20261027-linux-26`。PID1986250，Active/running，NRestarts=0；750MiB软限、900MiB硬限、256MiB交换限、125%CPU及既有Java参数保持。其他服务和systemd配置保持，未触发OOM。正式目录与公网2323查询身份一致。

持久Replays链接和服务挂载环境内写权限通过；既有39项录像相关文件大小及SHA256保持。没有新增备份归档，保留原正式目录供回切。

证据集中于 `.tools/release-independent-1027/final-audit.json`、package.json、build-integrity.json、server-integrity.json、stage.json、isolated-smoke.json、linux-native-result.json、deployment.json、server-after.json与client-export-final。编辑器已恢复Windows Player目标，Main未运行。图服务关闭，发布脚本与构建入口按精确源码回退核对。

## 同日追加：覆盖桌面安装

按“桌面也覆盖”更新 `C:/Users/Sam/Desktop/MTGB`。复用本次已验证构建，43项变化文件临时写入并原子替换，37927项程序文件与构建SHA256逐项一致；安装清单更新到协议22及上述新程序集／运行身份。没有新增备份、修改独立ZIP、重新部署服务器或推送Git。

覆盖前后及实际程序检查后，36862项卡图、5项Decks文件、3项Replays文件保持；卡组和录像额外核对内容SHA256。通过桌面实际exe隔离配置进入Main、资料就绪、正常退出；无检查项异常。从桌面exe剥除开发工具PATH后后台导出4秒1080p／30FPS视频，120帧、BT.709，实际截帧确认战场、手牌和卡图正常。这些检查不新增完整公网对局或长局证明。

追加证据：`.tools/release-independent-1027/desktop-installed.json`、desktop-protected-before.json、desktop-smoke.json、installed-desktop-export及desktop-final-audit.json。首次final-audit保留原发布批次身份，以本追加记录表示桌面最新状态。
