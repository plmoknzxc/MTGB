# 法禁与 Arena 100张争锋接入验收

2026-10-24：F01.01、F01.02已在当前工作区完成接入与有界验收。桌面尚未打包覆盖，远端尚未部署，未提交或上传Git。N81只证明普通1v1共用消费者；本次另有赛制注册、规则、统帅区操作和结束恢复证据。

## 最终规则和界面

| 范围 | 法禁 Duel Commander | Arena争锋 |
|---|---|---|
| 构筑 | 含主将100张，允许合法共同主将 | 1位主将＋99张 |
| 开局 | 20生命，London调度，无免费调度 | 25生命、BO1，首次免费调度 |
| 统帅区 | 施放、加税、移区选择；首位共同主将从统帅区施放后锁定另一位 | 施放、加税、移区选择 |
| 游戏外 | 无备牌／wish牌池，可指定合法行侣 | 无备牌／wish牌池，可指定合法行侣 |
| 局间 | 原100张内换主将，原卡池、标识色及行侣保持 | BO1结束，不进入换备 |

两种赛制均不以21点主将伤害判负。服务器按房间格式检查构筑和原生资格，客户端自报其他格式不能绕过。法禁主将专属禁牌与主牌禁牌分开；Lutri只禁止担任行侣，不因此禁止放主牌或担任主将。争锋使用用户指定的普通Arena100张模式，本次不增加60张或Competitive Brawl入口。

双方及观众可查看公开统帅区的主将与行侣，支付后列表准确刷新；主将身份仍单独保存，旧录像无新字段时回退原主将列表。

真实手牌、可用坟墓牌、可用放逐牌和统帅区分别切换并显示张数，默认手牌；直接选手牌／开局时保留真实手牌。选中来源消失后切回有效来源，保留物理ID、原生费用和操作权限。双方信息栏可浏览公开统帅区；行侣与主将可同时展示，支付行侣费用后它只出现在手牌，主将保留。

## 八项验收

规则、服务器、UI各自证据分别固定在[native-verified.json](../.tools/duel-brawl-integration-1024/native-verified.json)、[server-verified.json](../.tools/duel-brawl-integration-1024/server-verified.json)、[ui-verified.json](../.tools/duel-brawl-integration-1024/ui-verified.json)。总审计[audit.json](../.tools/duel-brawl-integration-1024/audit.json)包含实际范围、准确文件哈希及运行身份。

- **构筑：已验。** [native-audit.json](../.tools/duel-brawl-integration-1024/inspector/native-audit.json)、[construction-boundaries-result.json](../.tools/duel-brawl-integration-1024/construction-boundaries-result.json)、[server-verified.json](../.tools/duel-brawl-integration-1024/server-verified.json)、[format-validation-result.json](../.tools/duel-brawl-integration-1024/format-validation-result.json)
- **房间权威：已验。** [server-verified.json](../.tools/duel-brawl-integration-1024/server-verified.json)、[companion-authority-result.txt](../.tools/duel-brawl-integration-1024/companion-authority-result.txt)、[ui-lobby-click-result.txt](../.tools/duel-brawl-integration-1024/ui-lobby-click-result.txt)
- **身份禁牌：已验。** [format-validation-result.json](../.tools/duel-brawl-integration-1024/format-validation-result.json)、[ui-companion-ban-result.txt](../.tools/duel-brawl-integration-1024/ui-companion-ban-result.txt)
- **开局注册：已验。** [opening-duelcommander-1-result.json](../.tools/duel-brawl-integration-1024/opening-duelcommander-1-result.json)、[opening-brawl-1-result.json](../.tools/duel-brawl-integration-1024/opening-brawl-1-result.json)、[opening-brawl-2-result.json](../.tools/duel-brawl-integration-1024/opening-brawl-2-result.json)、[sessions-result.txt](../.tools/duel-brawl-integration-1024/sessions-result.txt)
- **主将规则：已验。** [cast-return-duelcommander-result.json](../.tools/duel-brawl-integration-1024/cast-return-duelcommander-result.json)、[cast-return-brawl-result.json](../.tools/duel-brawl-integration-1024/cast-return-brawl-result.json)、[partners-result.json](../.tools/duel-brawl-integration-1024/partners-result.json)、[damage-duelcommander-result.json](../.tools/duel-brawl-integration-1024/damage-duelcommander-result.json)、[damage-brawl-result.json](../.tools/duel-brawl-integration-1024/damage-brawl-result.json)、[ui-verified.json](../.tools/duel-brawl-integration-1024/ui-verified.json)
- **游戏外区域：已验。** [no-wish-prefix-result.json](../.tools/duel-brawl-integration-1024/no-wish-prefix-result.json)、[companion-duelcommander-result.json](../.tools/duel-brawl-integration-1024/companion-duelcommander-result.json)、[companion-brawl-result.json](../.tools/duel-brawl-integration-1024/companion-brawl-result.json)、[companion-authority-result.txt](../.tools/duel-brawl-integration-1024/companion-authority-result.txt)、[ui-companion-result.txt](../.tools/duel-brawl-integration-1024/ui-companion-result.txt)、[public-native-verified.json](../.tools/duel-brawl-integration-1024/public-native-verified.json)、[public-session-result.txt](../.tools/duel-brawl-integration-1024/public-session-result.txt)、[ui-verified.json](../.tools/duel-brawl-integration-1024/ui-verified.json)
- **Unity交互：已验。** [ui-verified.json](../.tools/duel-brawl-integration-1024/ui-verified.json)、[ui-result.txt](../.tools/duel-brawl-integration-1024/ui-result.txt)
- **局间及结束恢复：已验。** [authority-swap-result.txt](../.tools/duel-brawl-integration-1024/authority-swap-result.txt)、[sessions-checks.txt](../.tools/duel-brawl-integration-1024/sessions-checks.txt)、[sessions-result.txt](../.tools/duel-brawl-integration-1024/sessions-result.txt)、[ui-lobby-click-result.txt](../.tools/duel-brawl-integration-1024/ui-lobby-click-result.txt)、[ui-lobby-heading-ja-result.txt](../.tools/duel-brawl-integration-1024/ui-lobby-heading-ja-result.txt)

原生实测包括：法禁调度一次留6、争锋首次留7／第二次留6；两赛制各两次主将施放及加税、实际放逐回统帅区；法禁首搭档统帅区施放锁定后，第二搭档通过统帅信标进手牌仍可施放；实际21战斗伤害后继续；Living Wish两种限制与普通对局正对照；两赛制各实付3费取行侣。原前缀有15个独立原生对局夹具，17次咒语施放、1次统帅信标激活、2次行侣付费动作；公开统帅区修正后另增2个原生流程和2次行侣付费，合计17个原生夹具、22次付费动作。

另有4个生产OnlineMatchSession／BeginRoomGame／EndRoomMatch小局：法禁第二局真实注册更换后的主将，原牌池外注入拒绝；争锋BO1结束；公开结束视图保留主将，观众不见私有手牌。构筑原生检查单列，不计新对局。

UI按原生请求重放并检查EventSystem射线／拖动：区域ID与费用、双方公开浏览、两种回统帅区确认；三语三档字号、密集双排地避让及移区刷新、原否认之力选手牌到支付后恢复；1024×768、1366×768、1920×1080实际GameView切换按钮。局间标题另核对最终三语文字和最大字号，不以文字尺寸代替本地化。

## 修正与原始失败

- BO3空行侣占位对象与null被误判为变更，改按有效行侣引用比较。
- 混合扇形牌区隐藏统帅区，改为来源切换并保留原卡拖动。
- 原生Commander Effect编号前缀导致替代正文漏译，仅匹配准确主将规则。
- 缺少Lutri行侣专属禁用，补资料字段、解析、服务器及编辑器角色提示；不改牌面效果脚本。
- 大字号局间标题高度不足、偏好变更未刷新牌组缓存；扩大最终标题区、纳入偏好版本并缩短英文文案。
- 最终公开信息审计发现列表只传主将、漏行侣：撤回前一收尾状态，增加独立公开统帅区字段，双方／观众／局后视图均同步，原生两赛制支付移区及实际UI浏览／旧录像回退分别复验。
- 同一Java文件的后项补丁覆盖旧项；改按文件依序组合并核对原来源。

完整FAIL保留；只复用明确完成的前缀，失败／未完成尾段另有完成结果。驱动纠正包括错误禁牌假设、等待Living Wish入坟（实际自我放逐）、未渲染便点控件、用点击代替拖动、错误非空手牌夹具、点击隐藏推进按钮、忽略本地费用草稿，以及语言尾段未结束便切场景。前缀截止行在server/ui-verified中固定，未将整份FAIL改为PASS。

Unity环境内存压力曾导致连接中断，已恢复，已保存源文件和原证明保留。当前本地ForgeRuntime为`7e4e169a5b149c0eb76c864c17469aaa560ba56a9739d817a6d65ab451ae08cd`。原15个规则流程保留其运行身份`26ee31049be1f65ab4d3358fe9562f74faf532edc9c2d8944a962b6c352f5999`，公开字段修正后的两原生流程及新增C#会话独立记证；验证classpath优先覆盖的157个实际生产类与本地发布包逐字节一致，额外类仅为夹具。54个原生补丁生成107个覆盖类。

## 结论边界

两人法禁及100张争锋当前工作区接入完成。初始资源／选定区域由夹具安排，后续费用、选择、施放及战斗为原生实际流程；21伤害场景初始40生命用于区分主将败北条件。Wish未指定外部牌为原生对抗夹具，C#房间权威会拒绝此牌组。UI重放不是新的原生效果或联网证明。

仅刷新机制分类，不授全卡池PASS。历史REVIEW_SOURCE_CHANGES、A06全局依赖、跨机器／容量及其他特殊模式继续按用户后置；未新增UTP或外网验收。桌面打包／覆盖、远端部署和Git发布均未执行。

官方依据：[Arena争锋](https://magic.wizards.com/en/formats/brawl)、[Duel规则](https://www.duelcommander.org/rules/duelcommander_comprehensiverules/)、[Duel禁牌表](https://www.duelcommander.org/banlist/)、[Wizards禁牌表](https://magic.wizards.com/en/banned-restricted-list)。2026-10-24重查现有普通Brawl和法禁类别，补齐Lutri行侣例外。
