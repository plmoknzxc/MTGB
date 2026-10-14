# 真实网络请求恢复增量审计（2026-10-06）

## 结果与范围

一个实际 Windows 服务器进程和两个实际客户端进程，通过本机 UDP 完成 dedicated BO1。两席分别在首次待选请求中主动关闭 NetworkManager，走现有自动重连、身份/席位恢复和权威视图恢复。驱动断言恢复后请求编号及完整视图哈希等于断线前，之后通过真实 ForgeBattlefieldController.TrySubmitPresentedOption 提交合法选项，双方完成一次 London 调度后各有六张手牌，席位1认输，席位2胜一局并结束系列。客户端持续检查未经授权的对手手牌数组为空。

原始结果：`.tools/network-recovery-1006/attempt1/{client1,client2}.json` 均 success=true、games=1；同目录保留三份日志。聚合断言、驱动/生产来源及构建产物哈希见 `.tools/network-recovery-1006/result.json`。Forge 运行包 b3c1f7604b168fa54075d81674a745ba185d4581a9742ed46e06408bc55f8bed，源与构建内 runtime.json、files.json、ForgeBridge.class 核对相等。Java 子进程限制768MiB堆及4个CPU；不修改系统全局环境。

这是已有起手/交互消费者的真实传输增量，不增加原生规则或UI首验数量，不替换历史批次。测试启动工具增加可选 --output-dir，默认路径不变；本轮使用独立目录，原 dedicated-bo1 证据未覆盖。生产游戏/桥接代码未修改。

## 保留观察与未验证项

服务器日志出现4条 send queue full 错误；客户端最终验收仍通过。不能据此宣称网络无丢包、吞吐或队列配置正常；饱和原因及可靠交付边界保留在 R02，未掩盖日志。退出时客户端还记录引擎资源释放警告，本轮没有分析该警告。

未注入延迟、乱序、重复；未测试控制玩家中的付款/目标/战斗等其他待选请求、自动提交时序、宽限期耗尽、BO3换备、LAN/多房间或跨机器网络。没有图像/真实鼠标点击证据；自动提交使用运行客户端现有展示提交入口，不能代替视觉验证。此前离线守卫及本次网络恢复证明各自保留身份。

## 发现与证据口径

图项目 D-project-MTGB，Verify，generation 2026-10-06T07:41:47Z。ApplyResyncView 的一层双向调用显示 HandleClientMessage → BeginResync/ApplyPlayerView/HashReport；相关路径 coverage 没有记录缺口但 metadata_changed，因此使用当前准确源码核对。代码链只解释恢复流程，PASS来自实际三进程结果。本轮核销R02的双席起手请求断线重连，其他时序范围保持开放。

用户要求本轮完成后暂停；本审计及账本登记后停止，不启动下一批。
