# 愿望术的延后备牌行动

官方 AFR 发行说明：愿望术在本回合提供一次使用许可，实际使用时才选牌，仍遵守全部费用和时机：https://magic.wizards.com/en/news/feature/adventures-forgotten-realms-release-notes-2021-07-09 。原生 wish.txt 已有 MayPlay、MayPlayLimit 1 和本回合持续时间，本轮未改卡牌脚本。

接入缺口：ForgeBridge 优先权候选枚举遗漏 Sideboard；现有备牌列表仅查看，通用区域列表的施放按钮未包含 BrowseSideboard，缓存也没有跟随当前请求变化。修复让备牌复用原生合法动作预测及原生 card:id／请求编号提交，预留同一施放按钮行高；浏览窗口跟随请求、可行动、已提交状态重建。地牌按钮为“使用／Play／プレイ”。不预先选牌、不移入手牌、不在客户端授予权限。备牌仍仅自己可见，对手／观战没有备牌面。

真实原生夹具 Tools/ForgeBridge/tests/WishSideboardFixture.java，只安排初态；驱动 design-demos/battlefield-redesign-2026-10-24/validate-wish-sideboard-native.py，使用真实施放、付款、优先权、目标及结算：

- .tools/wish-sideboard-1026/result.json：15 项。实际付费／结算愿望术后暂不使用；对手启动 Jayemdae Tome 时，从备牌施放 Stifle 反击该异能，没有抓牌；同一许可被消耗。另一张愿望术提供的新许可未使用，到回合末失效。Divination 不可错误响应，Counterspell 没有合法咒语目标也不可响应。
- no-blue/result.json：7 项。三座山支付愿望术，没有蓝费时 Stifle 不提供选项，canRespond 为 false；主阶段仍能获得备牌地的合法操作。
- land/result.json：7 项。实际使用备牌 Mountain，同一物理 ID 移至战场并消耗许可。

修复前对照：仅把枚举行还原为无 Sideboard 后独立编译、运行同一真实流程，.tools/wish-sideboard-1026-baseline/result.json 在愿望术结算后缺少备牌选项处失败，保留此复现。地牌驱动首轮误将已进战场、可产费的同一 Mountain ID 当作仍可使用的备牌；断言改为仅检查仍在 Sideboard 的牌，保留 land/result-round1-land-id-reused-in-battlefield.json，没有为驱动错误改规则。

候选 classpath：本轮 classes → strict-source-1025/classes → ForgeRuntime/classes/lib。基础 runtimeHash、桥接与依赖候选哈希见结果及 build-manifest.json。初轮标准夹具和后续可选 noBlue 夹具的默认布置等价；新增条件仅用于第二条无蓝费边界，未把后编译夹具哈希伪称初轮执行哈希。候选隔离运行，未覆盖运行包。

Unity 消费者：validate-wish-sideboard-ui.cs 加载实际请求，按接收者补 request／awaitingInput。三窗口、中／英／日、.85／1／1.3 字号，171 项通过：真实射线提交准确施放／地牌使用选项；浏览不提交；费用不足／非行动视角无按钮；开着的窗口随消耗和到期撤掉按钮；对手备牌访问拒绝；文字及按钮边界。结果 wish-sideboard-ui.json，截图 wish-sideboard-response.png。原生结算与 Unity 回调分别记证，不冒充同一次联网操作。前一轮 135 项仅缺新增地牌／无蓝费展示，最终由 171 项替代当前结果。

MechanicCoverage 只刷新分类，保留原 2582 未登记／318 需重测，不运行全量 --run、不恢复暂停 goal、不授所有局外效果通过。图索引相关文件 metadata_changed，均读当前源码核对。Unity 编译与最终控制台零错误，原生产场景哈希不变。未打包、覆盖客户端、更新服务器或提交 Git；下一包需同时包含桥接与客户端改动。
