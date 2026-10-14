# 密集滚动边界与默认场景复核

## 改动和原因

截图红线处是滚动区域边缘残留的卡片、数量和阴影碎片。密集生物区高度从 182 调至 192，顶部留出 16 像素，下端按实际末牌及其徽章留出 22 像素。新增 `DenseFieldViewport`，在布局更新后按卡面、数量、防御／忠诚徽章和阴影的联合边界决定是否完整显示；边缘不完整的牌拒绝射线，原对象、牌号和叠放数量仍保留，滚动到完整位置后恢复。

首次由普通区域切到溢出区域时从顶部开始。原本没有溢出的 ScrollRect 可能返回 normalizedPosition=0，不能把它当成用户选择的末尾；已有溢出则保留实际位置。恢复普通战场时重置射线状态，避免沿用密集区隐藏设置。

默认 `IntegratedBattlefield` 保存当前静态 HUD 的位置和控件，关闭旧样例牌和只应在对局里出现的窗口。程序生成图形保存在 `Assets/UI/BattlefieldHudShapes.asset`；箭头种类序列化，数字字体同时保留 material 和内部 texture，底板置于各控件后方。启动时复用头像、设置图标和 AUTO 控件，避免重复创建。场景不保存对局快照、永久物、计数器、状态、徽记或通知；姓名滚动生成的 `ScrollingName` 不保存，源 Name Text 保留并启用。

## 实际检查与范围

本批次身份为 `.tools/battlefield-row-boundary-1005`；原批次和失败记录保留身份，不改写为本次结果。

- `BattlefieldRowBoundaryPresentationValidation.Run()`：中文／英文／日文、字号比例 1.3，六个密集区域，顶部／末尾和 0.77／0.41 中间滚动位置；联合边界、隐藏牌拒绝射线、每个代表牌滚动可达并实际命中、每侧 575 个独立牌号完整保留、重复快照保留滚动、普通局面恢复可见性及射线。
- `BattlefieldStressPresentationValidation.Run()`：长堆叠异能正文遮罩／滚动，指示物分页、查看／返回、密集区末对象射线／悬停、滚动保持及普通布局清理。
- `BattlefieldCompactPresentationValidation.Run()`：同状态叠放和状态改变拆分、准确个体目标、支持牌 8 像素间距、选择窗口边距、堆叠列表、阶段／箭头／AUTO、字号和查看／返回。
- `DuelLayoutPresentationValidation.Run()`：原生玩家选择准确编号、双方池、状态／徽记分页、三语言固定字号和布局消费者。
- 静态场景另行保存／重载并检查 HUD 持久资源、层级、图标、数字和运行时复用；结果见 `scene.txt`。

截图按实际文件尺寸记录在 manifest，密集场景为 1605×927。575 张／侧为展示夹具，截图不代表同时产生这些对象的真实卡牌效果。以上均为 Unity 界面／现有数据适配器证据，不新增 Forge 效果 PASS，也不证明所有机制、网络同步或四人指挥官已经验证。长期 goal 继续暂停。

三语言 start/end 为滚动边界检查原始截图；删除生成姓名副本后另有 `scene-clean.png` 默认场景和 `en-US-clean-density.png` 密集展示补图。补图使用同一密度夹具，不作为额外效果验证。其初始截图文件误冠 zh-CN，已按实际英文 UI 保存正确名称；头像的“你／对手”来自夹具姓名，不是 UI 标签。结束时恢复 Main 编辑模式、中文、字号比例 1.3。

## 失败与验证方法修正

保留 `attempt-*.txt`：早期驱动在可见性更新前检查、堆叠占据独立射线区域、物理鼠标抬高手牌／创建 inspector，分别导致不可达或射线误报。改为先强制布局再等待、独立射线时隔离堆叠、先 ResetHover 再冻结指针；组合截图仍恢复堆叠。早期生物区高度 182 未能容纳两完整行，改至 192 后取得本次证据。

静态资源导出曾直接克隆非可读纹理和动态字体，随后改成纹理回读、字体重建并保存内部 texture。场景检查曾把预期的 EnvironmentLayer 编辑模式动态图形当成持久 HUD，检查范围已收窄。旧 `scene-default*.png` 和早期 `scene-final.png` 属于修正前画面，不列为最终截图。

启动检查也保留一次驱动范围修正：Avatar 下的 Mark 是既有装饰，不是第二个 Portrait；GearIcon 实际位于 GearBox/SettingsButton 下。按准确类型／路径检查后通过，无需产品修改。Unity 场景保存器的空 YAML 字段带尾随空格；C# 定向 diff-check 通过，场景这类自动序列化格式未手工改写。

Unity 编辑器恢复过程中发生 DX12 原生崩溃，栈包含 `CheckDeviceStatus`、`D3D12CommandList::PrepareExecute`、`GfxTaskExecutorD3D12::DoExecute`；不将其归为 Forge 规则失败，也不据此断定字体是崩溃原因。当前编辑器通过单次启动 `-force-d3d11` 恢复，项目图形设置未修改；上述最终检查在该会话完成，DX12 最终回归未完成。诊断摘要留在本批次。

图谱原代次 2026-10-04T21:02:29Z，随后更新至 21:48:58Z；相关改动文件记录 metadata_changed，使用精确源码补查。后续图谱服务 Transport closed，因此本轮收尾使用限定文件源码，不能由图谱结果做全项目无遗漏声明。

## 当前结果记录的边界

`ForgeBridge.addNotice` 按席位维护最近 16 条通知；分类为 coin、dice 和通用 notice。通用 notice 可能来自选颜色／类别／数字／玩家、部分选项、投票、竞价、分堆、搜索或没有可选牌、额外回合和操作拒绝等通知路径。是否记录取决于对应卡牌脚本是否调用通知，接收者与私密信息可见性仍受原生权限限制。这是固定 Forge 源码调用点观察，不是上述效果的完整实际验证。

Unity 的结果记录入口只在存在通知时出现，位于设置旁，展示当前收到的通知。完整施放、回合、战斗和对局历史未实现；本轮没有新增历史记录功能。

## 归档

最终截图见 `Docs/References/BattlefieldRowBoundary-2026-10-05/`；源码、输入、结果和截图 SHA-256 见 `.tools/battlefield-row-boundary-1005/manifest.json`。场景、资源、skill 及验证入口均按本次状态归档；配套输入只提供上下文，不扩张 PASS 范围。
