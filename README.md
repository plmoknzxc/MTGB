# MTGB

MTGB 是使用 Unity 6 和 Forge 制作的万智牌组卡与联机对战项目，当前主要面向 Windows 双人对局。

## 当前功能

- **组卡与搜索**：按具体印刷版本浏览卡牌，保存主牌、备牌、行侣及辅助牌组；支持费用／类别排序和打乱，以及卡名、效果文本、颜色、费用、系列、合法性和平台筛选。指挥官卡组编辑支持统帅区、共同主将及可选的主将／行侣适配搜索，当前不开放指挥官对局。
- **卡牌详情**：提供简体中文、日文和英文界面与卡牌资料，显示多面牌、关键词说明、关联衍生物及地城、魔戒等机制辅助图。
- **联机对局**：支持局域网房主、独立服务器、BO1／BO3、局间换备和断线重连；每位玩家只接收其有权查看的状态。
- **战场交互**：展示手牌、永久物、堆叠、指示物、徽记和延迟效果，支持费用、目标、攻击／阻挡等选择，以及墓地和放逐区查看。
- **本地卡图**：按界面语言读取本地卡图，缺少对应语言时回退英文，缺图显示缺失提示；异画选择界面提供独立预览与下载入口。

项目仍在开发。卡牌目录可浏览不代表全部卡牌及组合都已验证，复杂交互和视觉表现持续补充；DIY 编辑接口、离线 AI 和多人 Commander 尚未完成。

## 近期更新（2026-10-15）

- 卡组编辑支持行侣和统帅区，直接拖入主将；主将颜色与行侣条件可通过高级搜索开关叠加，关闭后可继续浏览全卡池，牌组内合法性提示独立保留。
- 主牌数量、赛制下拉和排序自适应排列；修正缩略图等待、单张数量角标、字号跳变与选择器闪动。颜色筛选随中文／英文／日文显示，切换语言保留选择。
- 房间列表展示生命、赛制、DIY、赌注和 BO 设置，调整席位及观战名单；赌注仅在自由／自定义且允许 DIY 时可开启。
- 改进录像列表预览速度；独立服务器自动保存有有效画面的正常结束、中止及正常停止时的对局录像。
- 修正付费时区域浏览关闭、开局空黑框、地异能提示、宣攻编号和观战退出；法术力池避开地牌，观战显示双方公开法术力。

近期说明：[合并更新与发布](Docs/Release.2026-10-15.zh-CN.md)、[卡组搜索与稳定标签](Docs/DeckSearchAndStableLabels.2026-10-15.zh-CN.md)、[统帅区与构筑条件](Docs/DeckConstructionFilters.2026-10-15.zh-CN.md)、[房间与观战](Docs/RoomDirectoryAndSpectators.2026-10-15.zh-CN.md)、[服务器录像](Docs/ServerReplayArchiveAndAnte.2026-10-15.zh-CN.md)。

### 2026-10-14

- 费用和目标选择统一推进／取消，法术力付款等待「下一步」，优先使用池中资源；产色使用紧凑图标选择。
- 修正已有译文的规则／生成费用匹配、食品按实际状态合并、阶段条连续过渡，以及自动模式无合法攻击者时的战阶推进。
- BO3 换备和局后留房／晚加入观战修正；结算菜单为「继续查看／前往换备」或「继续查看／返回房间」，两个按钮居中。
- 调整移区通知、卡牌预览和选择界面；继续按实际消费者做增量验证，保留未验证范围。
- 重绘模板改用思源宋体／黑体、EB Garamond 和小型开源字形回退，核对中文／日文／英文排版；客户端构建随附字体和软件许可说明。

最近的说明：[实局修复与验证边界](Docs/LiveGameplayFixes.2026-10-14.zh-CN.md)、[机制验证进度](Docs/MechanicValidation.zh-CN.md)、[项目文件整理](Docs/WorkspaceMaintenance.2026-10-14.zh-CN.md)。

## 开源与第三方许可

自有 Unity／客户端代码采用 [MIT](LICENSE)，Forge 桥接及原生修改采用 GPLv3；字体、卡牌资料、卡图和 Unity 的许可保持独立。完整 Forge 对应源码通过[源码下载页](https://github.com/plmoknzxc/MTGB/releases/tag/forge-source-20261014)提供。

参阅[第三方声明](THIRD_PARTY_NOTICES.md)和[源码交付／发行边界](Docs/OpenSourceAndDistribution.zh-CN.md)。重绘模板已换用附带许可的开源字体；旧模板字体已移除，并由导入、构建及 CI 校验阻止重新引入。

## 构建与运行

需要 Unity **6000.3.22f1**、Java 17、Maven、Python 3.11+ 和 PowerShell 7。

1. 用 Unity 打开项目。
2. 在项目根目录执行 `pwsh -File Tools/ForgeBridge/Build-Bridge.ps1`，生成便携运行资源。
3. 在 Unity 执行 `MTGB/验证/项目整体`，检查卡牌目录、真实对局会话和进程隔离。
4. 执行 `MTGB/构建/Windows 客户端（开发版）`，客户端输出到 `Builds/ForgeValidation`。
5. 启动客户端，在组卡界面新建或导入 `.mdk` 牌组，再从大厅创建或加入房间。

独立服务器通过 `MTGB/构建/Windows 独立服务器` 构建，启动命令：

```powershell
Builds/Server/MTGBServer.exe -batchmode -nographics -mtgb-server -port 7777
```

Windows 构建包含 Java 和固定 Forge 资源；当前精简构建的对应源码单独提供。卡图包另行放置在程序旁的 `CardImages`；运行包、构建产物、本地卡图包、受限字体、恢复场景、凭据和个人录像不提交到 Git。重绘模板的开源字体与许可已随项目提供，见[字体说明](Assets/Resources/UI/CardTemplates/Fonts/README.md)。

## 项目资料

- [项目架构](Docs/ForgeArchitecture.zh-CN.md)
- [构建与验证](Tools/ForgeBridge/README.zh-CN.md)
- [组卡与牌组格式](Decks/README.md)
- [战场操作](Docs/ForgeBattlefield.zh-CN.md)
- [联机架构](Docs/OnlineMultiplayer.zh-CN.md)
- [卡图导入](Docs/CardImages.zh-CN.md)
- [多语言展示核查](Docs/LocalizedPresentationAudit.zh-CN.md)
- [剩余日文资料清单](Docs/RemainingJapaneseRules.zh-CN.md)
