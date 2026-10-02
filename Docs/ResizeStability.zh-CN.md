# 卡组编辑界面缩放稳定性验证

日期：2026-09-22。环境：Windows，Unity 6000.3.22f1，Direct3D12，Main 场景 Play Mode。

## 结论

确认卡图缓存缺少容量限制；浏览新页面会持续保留完整卡图纹理，返回旧页不释放。本次未复现窗口缩放崩溃，尚不能证明缓存是历史崩溃的唯一原因。本次只做诊断，没有修改运行时代码或切换图形 API。

## 实测

- 初始卡图纹理 114 张，Unity 报告的纹理运行时占用约 265 MiB。
- 等待 8 秒后为 119 张、276 MiB，仍有少量图片加载完成。
- 连续浏览六个新页面后为 225 张、523 MiB；返回原页并等待 4 秒，仍为 225 张、523 MiB。
- 该过程中监测的 Noto TMP 字体图集数量保持为 1；托管内存有 GC 回落，没有表现为单调增长。
- 通过 Win32 MoveWindow 调整实际 Unity 主窗口大小 12 次，宽度交替为 1180/1580，高度为 720/920。全部调用成功、进程存活，之后恢复原窗口位置和状态。
- 缩放后 Play Mode 仍在运行，Console 错误查询为 0；卡图仍为 225 张、523 MiB。
- 缩放测试设有系统剩余提交额度低于 2 GiB 时停止的保护。

纹理数字来自 Profiler.GetRuntimeMemorySizeLong，不等同于独立测量的显存占用。缓存中的纹理仍被引用，属于无上限保留，不能直接称为不可达对象泄漏。

## 代码和历史崩溃证据

`Assets/Scripts/UI/CardImageLibrary.cs` 使用静态字典保留已加载 Sprite 和 Texture2D。没有容量或逐页淘汰机制；ClearCache 及同一路径文件变更时会释放相应资源。

历史崩溃日志 `Crash_2026-09-21_223855960/Editor.log` 出现 `d3d12: swapchain ResizeBuffers failed (8007000e)`，调用栈涉及交换链重建和窗口 Reshape。另一份 `Crash_2026-09-21_194750425/Editor.log` 明确出现 `Could not allocate memory: System out of memory!`。这些支持调查内存分配压力，但不能确定全部由卡图缓存造成，也不能宣称显存耗尽。

## 边界与后续

本次调整的是编辑器主窗口，未完整复现用户拖动内部 Game 面板分隔条的操作；也未进行长时间浏览、低内存压力或 Windows 玩家构建测试。12 次不崩溃不代表问题已经解决。

下一步应限制卡图缓存并保护正在显示的图片，评估卡池缩略图，随后重复翻页和内部 Game 面板缩放测试。若仍失败，再做 Direct3D11 对照，以区分渲染后端影响。

## 原始记录

- `Tools/Validation/Reports/resize-memory-before.csv`
- `Tools/Validation/Reports/resize-window.json`
- `Tools/Validation/Test-UnityWindowResize.ps1`（在项目根目录运行，传入当前 Unity 进程 ID）
