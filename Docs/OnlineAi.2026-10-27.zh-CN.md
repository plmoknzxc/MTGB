# 线上 1v1 AI（2026-10-27）

后续交付：已按用户随后授权制作独立更新包并更新正式服务端，见 [10月27日发布记录](Release.2026-10-27.zh-CN.md)。下文保留实现批次当时的未发布状态及验证范围。

按“线上 AI 先看看能不能完成”接通保存牌组、房间 AI 席位、服务端原生 Forge AI 与结束／BO3。工作区实现及本机真实网络验证完成；没有更新桌面、更新包、公网或 Git。限制赛、四人指挥官仍沿用上一轮缺口，不恢复暂停的长期机制 goal。

## 使用与边界

- 参赛房主选择已保存卡组，添加、更换或移除 AI；不能替换真人席位，观众无配置权限。
- 服务端按赛制校验牌组、主将和行侣，通过才提交新 AI。错误卡组保留旧 AI；检查中不能开局；取消或身份变化后的旧检查不能写回。
- 使用已有 Forge PlayerControllerAi，由服务器运行。AI 自动准备，不占真人连接、不参与房主选举、不接收网络私有快照或录像。
- BO3 保留原 AI 牌组，不主动换备；AI 获得先后攻选择时选择先攻。真人可以换备、断线重连。
- 线上法禁／争锋保持20／25生命；本地练习的自定义生命改用独立 localPractice 标记。
- 协议升至22，发布时须同时更新两端。当前工作区 ForgeRuntime：`128186fd3307d95ed5926016350d9f53188ebb4c8b2af9517e9a726628dba7e7`。

## 证据

| 层级 | 实测结果与范围 |
|---|---|
| 真实网络 | 独立权威服务＋真实localhost UDP客户端，33项断言通过：配置权限、非法牌组、观众隐私、重连、伪造AI席位、录像、BO3、房间回收。第一局真人认输，第二局AI实际施放生物并自然获胜，不把认输当自然胜利。 |
| 法禁／争锋 | 14项：真实LAN房间、原生主将校验、已校验牌组的原生AI会话开局、双方生命与主将注册、非法标识色拒绝。开局是独立会话增量，不是两场完整赛制比赛；共用网络生命周期引用上一行。 |
| 行侣与异步 | 8项：真实LAN配置，原生行侣检查进入等待后取消；原检查成功也不恢复AI；合法行侣接受，非法限制保留旧牌组。 |
| 生命边界 | 6项原生适配器检查，覆盖三种格式的线上与本地练习分支；不是六场比赛。 |
| Unity | 真实射线点击添加／移除；截图确认已准备的AI席位。新增帮助与两按钮在三语、三种字体大小下，文字高度和射线命中45项有效检查通过；不代表整个项目UI。 |

真实网络驱动：`Assets/Scripts/Networking/OnlineAiValidation.cs`（仅UNITY_EDITOR）；结果 `.tools/online-ai-1027/run-20261026-190934/`，目录使用UTC时间。包括 network-result、network-checks 和客户端 wire JSONL。

增量：`.tools/online-ai-1027/formats.cs`、formats.txt、两份 *-opening.json；companion.cs／companion.txt；life-boundaries.txt。UI：ui-matrix.cs／ui-locales.cs及对应输出、lobby-final.png。发布清单变化见runtime.json。

![AI房间](../.tools/online-ai-1027/lobby-final.png)

## 保留的失败

1. 初版房间名超16字符被拒，修正测试名；未改生产校验。
2. 伪造playerId=2原本期待拒绝，但现有请求门会改回真人身份，只能使真人自己认输；修正断言，保留旧FAIL，未改身份门。
3. 首图发现最终主题布局造成AI栏与摘要重叠，修正后重新截图并验证。
4. 首次语言驱动用了en／ja，实际资源为en-US／ja-JP，旧输出后两种仍是中文。有效中文15项保留，英文／日文30项由ui-locales补齐，并检查实际locale。
5. 独立法禁开局驱动漏设firstPlayerSeat，原生拒绝。正式BeginRoomGame会设置先手；修正驱动，保留formats-first-player-failure，再补开局断言。

图服务list_projects／check_index_coverage返回Transport closed，没有可用generation；按精确源码回退核对，不作全库穷尽声明。机制分类已刷新，不当作运行效果证据。

未验证：公网延迟与并发容量、所有卡组的AI决策质量、自动换备、四人模式。规则、合法性和支付继续由Forge负责。
