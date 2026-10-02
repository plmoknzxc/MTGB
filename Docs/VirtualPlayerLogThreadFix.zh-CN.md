# 虚拟玩家日志线程异常（2026-09-22）

## 原因

Unity 6000.3.22f1、Multiplayer Play Mode 2.0.2。Player 2 日志堆栈：

`VisualScripting.BackgroundWorker → Debug.LogWarning → StandardCloneWorkflow.OnLogMessageReceived → MessagingService.Broadcast → SessionState.GetBool`

`StandardCloneWorkflow` 位于编辑器内置 `UnityEditor.MultiplayerModule.dll`。它订阅了多线程日志事件，却直接访问只允许主线程调用的 SessionState。

## 修复

`Assets/Scripts/Editor/VirtualPlayerLogThreadFix.cs` 仅在指定 Unity 版本的虚拟玩家中生效。识别并包装该模块的日志回调，后台日志入并发队列，EditorApplication.update 调用原回调；保留消息、堆栈和等级。其他日志订阅者不变，玩家构建不包含此 Editor 脚本。

这是依赖内部回调字段的兼容措施；升级 Unity 后必须重新评估，不能视作引擎源码修复。

## 验证

- 修复前，在真实 Player 2 内执行 `Task.Run(() => Debug.LogWarning("[MTGB-LogThread-Probe] before fix"))`，Editor.log 稳定新增同一 GetBool 主线程异常。
- 修复后，确认原回调已被包装；连续 100 条 Task.Run 后台日志全部经过主线程调用原回调，异常计数没有增加。
- 后台 Warning 和主线程 Warning 对照也未新增该异常。此时主窗口均未显示两类测试日志，原因尚未验证；本次不声明跨窗口显示链路已验证。
- 重新启动 Player 2 后，兼容回调自动安装（数量 1），启动日志中 GetBool 异常与 C# 编译错误均为 0。
- C# 编译未出现错误。

原有 Locus broker 启动错误是另一条编辑器插件错误，不在此次 GetBool 修复范围内。
