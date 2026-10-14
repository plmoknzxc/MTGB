# 相互与嵌套玩家控制消费者审计

## 实际原生流程

使用未改动RoyalStateFixture安排初始手牌/地，结果由真实施放、目标、法术力支付、原生控制期限与结算产生。运行包`b3c1f7604b168fa54075d81674a745ba185d4581a9742ed46e06408bc55f8bed`不变；夹具09d4cfbdbaf1ab92d749fc7731414f837f441e943d10e2a669f277528f10b60e。`.tools/nested-control-1006/native/results.json`保留逐case原驱动哈希与driverArtifacts，不为凑统一哈希重跑已通过流程。

- `mutual-next-turn`：玩家1施放Worst Fears控制玩家2第二回合；玩家2在被控制时实际施放Worst Fears指定玩家1，玩家1第三回合由玩家2操作，实际施放Opt、支付/占卜/抓牌，第四回合归还正常席位。第一次控制期间request.playerSeat2/seat1；相互后playerSeat1/seat2；Opt归原玩家1坟墓场，两张Worst Fears各归自己放逐区。
- `nested-word`：玩家1控制玩家2第二回合，玩家2实际施放Word of Command指定玩家1。Word公开选择玩家1唯一可施放Opt；支付与Opt结算占卜时logical controllingSeat为[2,1]，原生实际request.playerSeat1/seat1。Word强制施放结束后恢复外层[0,1]，Opt结算时短暂再嵌套控制、之后恢复外层；第三回合[0,0]归还正常。Word归玩家2坟墓场、Opt归玩家1坟墓场；费用、占卜与抓牌来自实际原生流程。

`contract-trace-review.json`按两个原始trace哈希复核69/51请求和4/2次wrong-seat拒绝、初始双方未公开手牌隐藏、外层操作席位、占卜操作席位与正常归还。双方快照含实际可见/记忆身份；不声称每种复杂控制关系的所有私有权限已穷尽。

首个nested-word驱动错误要求经过选牌回调才能记住Opt编号，但原生只有一个合法候选时自动选择。实际支付和占卜已完成，Opt152已在坟墓场；修正为从初始实际手牌记录准确ID，失败及原驱动保存在attempt1与失败case目录，只补失败流程。没有修改单候选原生行为、规则、桥接或生产UI。

## 依赖与验证范围

准确读取固定上游ControlPlayerEffect、PlayEffect和Player.add/removeController；图覆盖metadata_changed后按准确源文件读取，upstream排除范围直接读源。桥接createMindSlaveController沿当前master控制器的Gui取得实际seat，publish独立提供playerSeat，快照controllingSeat只是逻辑控制关系；客户端ActionPlayerSeat按实际request.playerSeat显示手牌/费用。不能用controllingSeat推导嵌套请求实际操作席位。

本批不修改生产实现或运行包，原三语言player-control消费者合同沿用；只对新嵌套/相互请求差异补中文真实射线与准确索引、手牌/支付拥有者、旧/重复/非操作守卫、观战过滤和控制归还。新增原生2首验；UI属于已有控制/支付/占卜消费者增量，不重复增加UI首验。

当前`.tools/nested-control-1006/ui/20261006T0739272895430Z/result.txt`PASS，六份实际输入为mutual-scry/released及nested-payment/nested-scry/resolved-2/released；正确物理操作方视图，不改DTO授权字段。原生请求索引经真实按钮射线提交一次；旧ID/重复拒绝，另一物理席位只读，观战投影不含手牌/请求，外层恢复与普通归还分别显示准确手牌。源/输入哈希保存于producer-hashes.tsv/input-hashes.tsv；nested-payment截图复核右下取消/自动支付与费用，牌面/阶段完整。

UI两个原始失败保留：073513驱动假设空场景已有控制器而空引用，改为沿用既有验证入口的创建后重放；073716驱动在request.seat2时仍读取seat1非操作视图，awaitingInput为false，错误要求显示被控制者手牌。改为读取原生seat2对应视图，未放宽生产权限或手工改awaitingInput。两个失败驱动冻结ui-attempt1/driver.cs、ui-attempt2/driver.cs；仅补本套，未重跑原生或全部旧UI。

R36仍保留控制者离场、多方控制、嵌套其他选择/替代/战斗与归还时正在等待输入的网络时序。以上原生回调顺序进行期间不允许未经响应就跨阶段，不能拿顺序驱动证明异步断线/旧输入恢复。真实网络延迟/乱序/重连仍为R02；多方/离场/游戏结束分别保留原包。
