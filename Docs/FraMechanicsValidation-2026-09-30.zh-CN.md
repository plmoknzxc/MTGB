# 撕裂现实机制代表卡验收（2026-09-30）

## 范围与方法

本次新增 10 张 FRA 代表卡、12 条真实 Forge 游戏循环用例。测试夹具只布置初始资源与手牌／墓地；施放、支付、选择和结算走当前生产桥接。Unity 展示相同引擎导出的快照，检查战场与选择界面。不是双客户端联机端到端测试，也不代表整个系列每张卡及每种模式已经验收。

代码图服务本次返回 Transport closed，使用源文件读取与搜索完成定位。

## 真实引擎通过项

| 卡牌 | 实际验证的分支 |
| --- | --- |
| Hungering Puppetbeast | 从手牌施放，进场触发生成 Heartwood 衍生物 |
| Heartwood Crafter | 进场已备法；施放 Soul Tether 后消耗准备状态，原生物保留，生成 Heartwood |
| Semester Foreseer | 进场已备法，刺探选择将牌库顶牌移入墓地 |
| Theorix Metamage | 墓地预置 7 张牌，进场后为 3/3，具有飞行 |
| Recursive Recruitment | 从墓地返照；墓地 6 张其他牌，生成两个 4/4 Cadet；本牌不再留在墓地 |
| Vinelasher Adept | 选择基本地循环而非施放，弃牌、搜基本地、展示、加入手牌 |
| Craftwork Crusher | 进场选择派出 Cadet 与抓牌两项，两项均结算 |
| Campus Crier | 从墓地启动，支付并放逐自身，生成忠诚 2 的杰斯 |
| Graft Surgeon | 从手牌施放，进场带一个 +1/+1 指示物，成为 3/3 |
| Theorist's Sanctum | 无杰斯时横置进场；展示手牌杰斯时未横置进场；选择场上杰斯时未横置进场，共三例 |

## Unity 检查与修复

呈现 8 份引擎快照：Heartwood、备法、返照 Cadet、刺探、基本地搜索、多模式、墓地聚能、Behold 手牌选择。

- 衍生物使用本地卡图；Heartwood 与 Cadet 使用衍生物外框，杰斯保留忠诚盾牌。
- 修复战场同名牌数量达到两位数时换行。
- 去掉触发异能效果栏尾部的 Forge `Zone Changer` 调试信息。
- 手牌中的鹏洛客不再错误显示“当前忠诚 0”；当前忠诚仅用于战场对象。
- 缩小选择窗口底部按钮字号并禁止换行，修复“重新选择”断行。

## 证据与复现

- `python Tools/ForgeBridge/Test-FraMechanics.py`
- `Tools/Validation/Reports/FraMechanics/results.json`：12 条结果。
- 同目录 `.jsonl`：完整真实引擎消息；`*-resolved.json`：结算结果。
- Unity Play 模式进入 IntegratedBattlefield 后执行 `FraMechanicsPresentationValidation.Run()`。
- `.tools/fra-mechanics-ui/`：截图与 UI 自动检查结果。

## 尚未覆盖

本次未验证：所有多模式组合、Heartwood 产费限制与祭献后的临时异能、门槛失效恢复、Graft Surgeon 死亡转移指示物、Countersculpt 的额外费用两分支、Codie 的备法复制、Uldaros 的多牌复制施放、Emrakul 放逐施放、全部战斗关键字交互。英文文本回退与模式选择标题的细化亦未在本次统一处理。
