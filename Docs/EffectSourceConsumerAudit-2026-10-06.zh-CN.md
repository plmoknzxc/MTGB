# 预防效果的来源卡图与说明

## 复现与修改

R47替代效果排序中文补验的旧真实 Embolden 请求，效果对象187/188与来源咒语151不同，原生 `DamagePreventEffect` → `SpellAbilityEffect.createEffect` 保存来源 SpellAbility 和 `hostCard.getImageKey()`。但 `ForgeBridge.card` 只在效果标记列表补来源印刷，`OrderAbilities` 中裸效果对象无卡图字段；旧排序窗口显示空插画、`Embolden (151)'s Effect` 和英文预防说明。

Verify 图定位 `ForgeBridge.card/Gui.choiceCard/publicMarkerSource` 和客户端卡面／图片加载，metadata_changed 后读准确源码；被排除的固定上游 Card、SpellAbilityEffect、DamagePreventEffect 直接读取。不是按效果名字猜卡图。

新增 `effectSourcePrintingKey/effectSourceName/effectSourceBackFace/genericEffectName` 四个投影字段，仅不可变效果、非徽记、公开且非面朝下来源、效果图片键与来源图片键相同才提供。来源使用原生对象关系，效果仍是自己的ID，不把来源 printingKey 写成效果印刷身份。原生自定义效果名不套通用标题。

客户端优先沿用辅助图索引，随后可读取公开效果来源图。已知通用标题使用本地化来源名称＋“的效果”，避免引擎调试编号；准确匹配 `Prevent the next N damage. / Shields remain: M` 才翻译，保留实际N/M。未知或改变的规则文本不以原牌文覆盖。卡图缓存键包含新增来源字段，面朝下路径继续先显示牌背。

## 实际规则回归

最终运行包 `fab8031f7b97fdc498631137a3247e8aced754e6f74ae02978f6af6b3b577ae1`，固定上游和九个规则补丁未变。完整构建及便携打包完成后才启动实际测试；准备阶段的中间构建不提供验证身份。

`.tools/effect-source-1006/native-driver.py` 沿用数量分配实际驱动，独立输出 `.tools/effect-source-1006/native/results.json`：

- shield-division：实际 Embolden 分配3/1，后续 Pyroclasm，两处独立预防替代与分配；灰棕熊两点全被防止，Memnite 受1点死亡。新增核对替代请求真实来源图片字段、效果ID与来源分离、通用名标记，不伪造图片字段。
- shield-on-damage：实际 Divine Deflection X=3 后施放 Pyroclasm，原生 divideShield 分配2/1，伤害限制与预防／反射3点、下一优先权恢复。

这两条属于已登记规则的相关回归，不增加首验计数；未把其他数量／全部状态场景当成本轮通过。

## 展示和投影检查

`EffectSourcePresentationValidation` 使用本次真实 Embolden 排序输入，中英日、两条不同原生 order ID、可操作／只读身份，检查真实卡图、通用标题、1/3预防量、固定字号、查看战场／恢复、射线提交及重复守卫。自定义名／未知文本／面朝下图片是界面适配器负例，不计真实效果。中文截图 `.tools/effect-source-1006/ui/20261006T0337011857208Z/order.png` 已检查。

`Test-EffectSourceProjection.py` / `EffectSourceProjectionFixture` 是单独直接投影适配器，八个条件：战场／坟墓场公开来源、手牌／牌库私有来源、图片不同、自定义效果名、普通牌、面朝下来源。只证明新增字段的图片／名字投影守卫和效果自身ID；没有实际施放或全部隐藏信息权限证明。首版驱动未回答开局调度，夹具回调尚未执行而超时；保存失败后补正常开局回应，不能归因于产品卡图或引擎。

## A06与剩余边界

修改路径：ForgeBridge.java、ForgeWire.cs、TokenArt.cs、AbilityFace.cs、Integrated.cs、Arena.cs；新增投影适配器和一套受影响展示。旧生产者、真实输入、失败及最终结果由 review JSON 保存SHA-256。共享卡面未知文本／自定义名分别负验，既有目标预览／排序中文消费者按已知说明变更补验；不重复完整规则全量。

R47核销本次公开效果来源图、通用标题与已知预防数字说明；R18继续保留多源／APNAP／多替代组合，R15/R17/R26/R36保留其他隐藏区域／揭示／暗置／操作权限的真实流程，A06继续逐批依赖审查。没有关闭整个R47，也没有把新字段守卫等同于全部隐藏信息或真实联网通过。旧全局指纹状态不伪造当前PASS。
