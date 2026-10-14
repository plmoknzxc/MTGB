# 召集／拼造／截水的实际费用回归

运行 `python -X utf8 Tools/ForgeBridge/MechanicCoverage.py --run --jobs tap-payment` 生成当前代码的请求轨迹和局面快照。

独立测试预置局面，经生产 ForgeBridge 完成真实选择、支付、优先权和结算，覆盖八个场景：召集、零个召集对象、取消剩余支付、拼造、截水，以及只靠生物、神器或混合永久物支付的三个分支。

结果、JSON 和日志保留本地；批量运行会另归档到 `.tools/mechanic-coverage/runs/`。Unity 的 `TapPaymentPresentationValidation` 重放这些真实请求，检查当前费用、重选、稳定对象、点击 ID、提交保护、等待状态与三种语言标题。请求重放不是实际客户端与服务器往返的验证。

当前通过状态和未验证的费用边界见 [共用流程报告](../MechanicCoverage/families.md)。
