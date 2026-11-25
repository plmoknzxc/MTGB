---
id: kd_builtin_memory_project_mistake_note
injectMode: full
aiEditMode: auto
maintenanceRules: |-
  - Record only verified problems, rework causes, and avoidance steps
  - Prioritize recurring pitfalls, constraints, regression points, and confirmed fixes
  - Keep each entry short and focused on one lesson or constraint
  - Keep the list within 20 items and merge duplicates regularly
  - Remove outdated issues, non-reproducible issues, and unsupported guesses
---

- `BattlefieldStaticScenePreview` 会在编辑态改写原有 HUD 的布局/组件，而 `Clean()` 仅还原可见性；直接保存 `IntegratedBattlefield` 可能写入无关预览改动。做持久场景配置前先暂停预览构建、清理并重新加载已保存场景，保存后对比任务前快照，确认只有目标字段变化。
- 手写 `ForgeCardView` 展示数据需补齐基准：牌色需 `manaCost`、`originalManaCost`、`hasColorBaseline`、`originalColors`、`currentColors`；攻防变化配色需 `hasStatBaseline`、`basePower`、`baseToughness`。缺基准会让预览回退为无色框或中性攻防。按准确印次补齐，不要为修预览而推断真实对局中的无色状态或基础攻防。

