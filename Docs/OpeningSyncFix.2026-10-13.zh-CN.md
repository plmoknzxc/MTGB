# 2026-10-13：双开起手缺失与启动等待

用户日志中一席位持续 `Forge view hash mismatch`，重同步也失败；截图停留于空准备视图。客户端增加 ForgeOption／ForgeStackItem 的可选 rulesText 后，旧服务器 JSON 没有这些字段；旧校验按新 DTO 重新序列化，把新增 null 字段加入校验，改变发送方摘要。以前“可选字段保持旧协议兼容”的说明没有覆盖快照哈希，已由这次失败更正。

## 修复

ServerSnapshotJson 在读取 OnlinePlayerViewState 时，保留发送方真实字段集合的摘要，同时记录解析后的本地 DTO 摘要。校验同时要求收到的摘要匹配真实发送体、DTO 在解析后未被篡改；保留席位、对局和序号校验，不关闭校验。已封存快照不再被客户端验证过程修改。起始、状态更新、恢复以及结束快照都使用同一个 field-only codec；起始消息也先验证再显示。

首次真实起手快照未到时保留“正在准备起手牌…”提示，接收两玩家视图后移除，避免用零张牌、零牌库的裸空板误示已经准备完成。

## 验证

- `.tools/opening-sync-fix-1013/before-result.txt`：两席位旧服务器字段形状均重现 hash mismatch。
- `compatibility-result.txt`：18 断言通过。两席位七张私有手牌、旧字段形状、新字段形状、未来未知字段；传输体及 DTO 篡改、错误席位／对局、旧序号仍拒绝，当前 SealHash 保持有效。
- `release-build.txt`：StandaloneWindows64 非 Development 发布构建成功。managed-only 编译用于快速搭建夹具，最终公网复测换为实际发布 DLL。
- `open909568300400-result.json`：两个独立客户端实际连接 39.96.209.123:2323 的现有服务器，独占测试房间，双方原生七张与实际界面七张手牌、对手身份隐藏、保留起手后进入第一回合通过，未再出现 hash mismatch。只在独占夹具 DLL 的 Boot 插入测试入口，产品逻辑取实际发布 DLL，原桌面客户端不被测试替换。

首个真实驱动 `open654211604100-result.json` 未通过并保留：它只在收到新版大厅先后攻消息时记时间戳，现有服务器未产生这一消息，因此该夹具未进入手牌通过判定，结果里的固定“7”字样不是已验证数据。修正驱动为记录原生及实际界面数量；未有选择消息时以 StartMatch 为计时起点，最终发布 DLL 流程通过。不能把这两次驱动当作先攻／后攻两种选择分别验证。

## 等待时间与未完成优化

现有服务器从 StartMatch 到双方实际手牌分别为 15.198／15.156 秒；这是实测启动路径等待，不能写成已缩短为即时出牌。首个可见请求是原生 InputConfirmMulligan；本次旧服务器没有新版大厅选择时间戳，所以不是准确的“点先后攻按钮至手牌”测量。Unity 已显示明确等待提示，冷启动缩短尚未实现。

只读资源检查：MTGB cgroup MemoryHigh=750 MiB、MemoryMax=900 MiB、CPUQuota=1.25 CPU；空闲时服务 MemoryCurrent 约 585 MiB。累计 high=1326、没有 OOM，累计 memory pressure 约 0.626 秒，不能据此断言 15 秒全部由内存回收造成。未修改服务器内存、CPU、Java 参数或进行部署；其他服务不受改动。

## 交付

新的“起手同步修复更新包”适用于上一版 20:36 约 12.1 MiB 更新包已覆盖的客户端。只打包本次差异，保留卡图、牌组与回放。ZIP 的 payload SHA 和 CRC 全部检查，身份见 `.tools/opening-sync-fix-1013/package.json`。这一哈希兼容修复能直接与现有旧服务器通信；上一版全部攻击／共同宣攻的服务端功能仍另需同步。

本轮不恢复或核销旧机制 goal，不重跑既有卡牌效果，不把发布启动或网络边界算作整卡 PASS。图谱连接关闭，按准确源码回退。临时构建／夹具保留，上一轮删除同类产物被 automatic approval blocked by policy，不重写工具去绕过。
