# 君主／先制边界与异能来源消费者审计

## 原生首次验证

首验运行包 `fab8031f7b97fdc498631137a3247e8aced754e6f74ae02978f6af6b3b577ae1`，三条真实操作保留 `.tools/royal-boundaries-1006/native/results.json`、冻结驱动 `native-first.py` 和 `first-runtime.json`；复制品 `first-native` 只用于归档，不增加首验数量。

- initiative-repeat：两次实际施放白羽冒险家，同一玩家重复掌握已有先制；同一个地底城从秘密入口前进到选择的锻炉，原冒险家获得两个指示物为5/5，第二个为3/3，不依赖维持。
- monarch-multi-combat：两个不同生物实际攻击、未阻挡造成4伤害，君主转移。原始轨迹断言两个异能ID不同、来源同一个君主效果，排序不能按来源合并。
- initiative-multi-combat：同一操控者两个生物实际攻击造成4伤害，先制只产生一个合并转移触发；新操控者进入自己的地底城，原地城保留。

只读轨迹检查 `trace-assertions.json` 引用原轨迹哈希，补充触发数量与身份断言，不作为新增执行计数。实际结算、生命、身份、双方公开投影和恢复优先权分别记录。

## 反例与修正

状态栏里的辅助图字段此前只在玩家effectMarkers投影补齐；裸card用于异能排序、堆叠时字段为空。原生card为GamePieceType.EFFECT、没有effectSource、名字准确为The Monarch或The Initiative的规则辅助对象补effectMarker/markerKind/helperArtName及实时规则；保留自身ID。不要求仍在指挥官区，否则旧君主移除后的第二个待结算触发再次缺图。普通同名卡与来自其他卡的派生效果不进入分支。

Unity沿用TryBattlefieldArt/AbilityFace；辅助名与准确已知规则段落沿用MarkerName/MarkerRules。仅剥离已知Damage Source/Damaged/Phase/Player原生事件尾部进行准确段落匹配，再保留事件身份和数量。未知附加条件不猜译，不用整张辅助图全部规则替代当前异能。

## 改动影响与当前回归

当前运行包 `84de4fda0004ea15e731a1d8dec734bfe900d3e453940febe2172069b2629d8f`。`current-regression.py` 只重跑君主和先制双生物战斗两条真实流程，逐条断言排序和堆叠的辅助图、实时规则字段；均通过，不重复登记首验。桥接修正范围是两个源为空的EFFECT，未变规则补丁、请求路由、费用或私人信息政策。

`Test-RoyalHelperProjection.py`/RoyalHelperProjectionFixture八个子条件通过：两种规则helper在场与移除后保留图、规则和身份；两种源非空的同名派生效果不误用helper；同名普通手牌和牌库卡不误分类。计一适配器任务，不是八次实际施放。

原生适配器首次失败因测试试图把无PaperCard规则helper盖放，Forge CardDb在此无效构造报错；保留失败驱动/夹具/日志，改用实际可构造的源非空效果反例。原生真实双生物流程此前已通过，不重复运行。

Unity首轮失败保留：伤害事件分离通过，但结束步骤的[Phase: Player 1]尾部未分离而漏译。修正已知Phase/Player尾部后仅重跑相关展示，不重跑原生结算。修正后三语言展示/射线点击通过，结果 `.tools/royal-boundaries-1006/ui/20261006T0510248116629Z/result.txt`：实际排序准确同源异能ID、确认/重置/旁观门槛、旧效果移除后的堆叠图/规则、查看战场返回字体、相邻普通Soul触发三语言段落回归。既有AbilityFace/OrderAbilities/Stack合同增量，新增辅助字段文本和资源在本组补验，不追加UI首验数量。

## 边界

追加第四条实际首验 `Test-MonarchOldEndstep.py`：Palace成为君主、结束步骤抓牌触发进入堆叠后，对手激活并牺牲高城王座；旧触发保持原来源ID和操控者1，辅助效果已移除仍有图，最后原牌手抓1且新君主留对手。`.tools/monarch-old-endstep-1006/native/results.json` 为当前运行包实际结果。其Phase尾部格式与本组三语言结束步骤输入相同，辅助对象移除消费者与双生物旧第二触发相同；无需重复整套UI。费用/牺牲提交使用既有原生支付合同，仍未授予网络证明。

R32仍有玩家离场和多人交互。R47未知动态文本、私密分支与窄屏保留开放。真实传输/联网和全局当前PASS均未验证。前一RoyalState六场/三语言状态首验保持历史身份，本次辅助异能消费者回归另列；状态资源不因新运行包而宣称全量当前通过。
