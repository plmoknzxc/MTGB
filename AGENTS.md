# Agent instructions

- 创建、导入或修改 Forge 卡牌资料、翻译、卡图及规则脚本时，必须先完整阅读 `Docs/CardDataAuthoring.zh-CN.md`，完成其中的数据校验与运行验证，并记录未验证项。

- 涉及卡牌机制、对局交互或 Forge 同步时，先阅读 `Docs/MechanicValidation.zh-CN.md` 并刷新机制覆盖清单。主动从缺口中选择相关机制及边界补充回归，不等待用户逐项指出。卡牌加载、翻译、卡图和提示校验不能充当效果验证；引擎断言与 Unity 交互／展示证据分别记录，结论只限实际验证范围。

- 盘点剩余机制、复用结算／交互或选择回归范围时，使用 `.agents/skills/mtgb-mechanic-validation/SKILL.md`。按改动影响增量验证，保留旧批次身份；发现方法导致漏项或重复测试时，修正 skill 并记录依据。

