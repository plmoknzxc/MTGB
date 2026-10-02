# MTGB

MTGB 是使用 Unity 6 和 Forge 制作的万智牌组卡与联机对战项目，当前主要面向 Windows 双人对局。

## 当前功能

- **组卡与搜索**：按具体印刷版本浏览卡牌，保存主牌和备牌；支持卡名、效果文本、类别、颜色、费用、系列、合法性及纸质／MTGA／Magic Online 条件筛选。
- **卡牌详情**：提供简体中文、日文和英文界面与卡牌资料，显示多面牌、关键词说明、关联衍生物及地城、魔戒等机制辅助图。
- **联机对局**：支持局域网房主、独立服务器、BO1／BO3、局间换备和断线重连；每位玩家只接收其有权查看的状态。
- **战场交互**：展示手牌、永久物、堆叠、指示物、徽记和延迟效果，支持费用、目标、攻击／阻挡等选择，以及墓地和放逐区查看。
- **本地卡图**：按界面语言读取本地卡图，缺少对应语言时回退英文，缺图显示缺失提示；异画选择界面提供独立预览与下载入口。

项目仍在开发。卡牌目录可浏览不代表全部卡牌及组合都已验证，复杂交互和视觉表现持续补充；DIY 编辑接口、离线 AI 和多人 Commander 尚未完成。

## 构建与运行

需要 Unity **6000.3.22f1**、Java 17、Maven、Python 3.11+ 和 PowerShell 7。

1. 用 Unity 打开项目。
2. 在项目根目录执行 `pwsh -File Tools/ForgeBridge/Build-Bridge.ps1`，生成便携运行资源。
3. 在 Unity 执行 `MTGB/验证/项目整体`，检查卡牌目录、真实对局会话和进程隔离。
4. 执行 `MTGB/构建/Windows 客户端（开发版）`，客户端输出到 `Builds/ForgeValidation`。
5. 启动客户端，在组卡界面准备 `.mdk` 牌组，再从大厅创建或加入房间。手动验证可使用 `Decks/Starter.mdk`。

独立服务器通过 `MTGB/构建/Windows 独立服务器` 构建，启动命令：

```powershell
Builds/Server/MTGBServer.exe -batchmode -nographics -mtgb-server -port 7777
```

Windows 构建包含 Java、固定 Forge 资源和对应源码。卡图包另行放置在程序旁的 `CardImages`；生成的运行包、构建产物和本地卡图包不提交到 Git。

## 项目资料

- [项目架构](Docs/ForgeArchitecture.zh-CN.md)
- [构建与验证](Tools/ForgeBridge/README.zh-CN.md)
- [组卡与牌组格式](Decks/README.md)
- [战场操作](Docs/ForgeBattlefield.zh-CN.md)
- [联机架构](Docs/OnlineMultiplayer.zh-CN.md)
- [卡图导入](Docs/CardImages.zh-CN.md)
- [多语言展示核查](Docs/LocalizedPresentationAudit.zh-CN.md)
- [剩余日文资料清单](Docs/RemainingJapaneseRules.zh-CN.md)
