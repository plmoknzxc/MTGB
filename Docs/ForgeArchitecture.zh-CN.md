# 项目架构

MTGB 使用 Unity 6 展示组卡、房间和战场界面，使用固定版本的 Forge 执行对局规则。当前版本为 Forge 2.0.15，来源提交记录在 `Tools/ForgeProof/forge-version.txt`。

## 对局流程

1. 客户端按印刷身份保存和提交 `.mdk` 主备牌。
2. 局域网房主或独立服务器检查牌组并创建 `OnlineMatchSession`。
3. 每场对局启动独立 Java 进程，Forge 持有规则状态并生成选择请求。
4. 服务器按认证席位发送可见状态；客户端提交请求编号和候选，展示结果。
5. BO3 在局间执行换备；对局结束或异常终止时释放会话进程。

重复、过期、越权或超出候选范围的输入由权威端拒绝。手牌、牌库和面朝下对象按权限投影。入房检查通信协议，开局由权威端检查其运行资源和卡牌目录。

## 本地资料

- `ForgeRuntime`：构建生成的 Java、Forge 资源、桥接程序、目录与哈希清单。
- `Assets/StreamingAssets`：多语言文字、搜索资料、牌组赛制规则及图片关联索引。
- `CardImages`：本地卡图包；缺图显示缺失提示。
- `Decks`：玩家牌组及验证牌组。
- `Tools/ForgeBridge`：桥接源码、导入和审计脚本、真实引擎验证入口。

Windows 客户端和服务器构建携带便携 Java、固定 Forge 资源及对应源码。生成的运行包、构建产物和卡图包在本地准备。

## 验证与边界

`MTGB/验证/项目整体` 检查目录、牌组保存重载、真实会话、输入拒绝和进程隔离。卡牌机制、战场展示与联机行为分别通过专项入口验证；当前目录可浏览不等于每张牌及其组合都已完成行为验收。

当前以 Windows 双人对局为主，支持局域网房主、独立服务器和 BO1/BO3。DIY 编辑接口、离线 AI、多人 Commander 及全部复杂交互仍需继续开发。

构建命令见 [ForgeBridge](../Tools/ForgeBridge/README.zh-CN.md)，操作见 [战场说明](ForgeBattlefield.zh-CN.md)，联机细节见 [联机架构](OnlineMultiplayer.zh-CN.md)。
