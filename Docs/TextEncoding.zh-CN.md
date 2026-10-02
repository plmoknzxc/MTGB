# 文本编码约定与验证

- 运行时文本进程管道统一 UTF-8、无 BOM，启动前调用 `Utf8ProcessIO.Configure`。输入、输出、错误流三个方向都要配置；不能只设置 JVM 的 `file.encoding`。
- Forge JVM 指定 `-Dfile.encoding=UTF-8`；Java 协议与诊断流也显式指定 UTF-8。协议消息不能混入普通日志。
- 视频输入仍是原始 RGB 字节，直接写 BaseStream；错误文本按 UTF-8 读取。
- Netcode 的 `WriteValueSafe(string)` / `ReadValueSafe(out string)` 必须配对，不启用仅支持单字节字符的选项。不为统一外观擅自改变既有网络格式。
- 卡组、配置等 `.NET File.ReadAllText/WriteAllText/ReadLines` 默认 UTF-8；录像、目录缓存显式 UTF-8。不能替换为系统区域编码或用 ASCII 处理正文。录像魔数是固定 ASCII 字节，属于格式标记。
- 解码、JSON 或协议异常不得静默忽略并推进游戏；仅在服务器本地保存诊断，不把包含隐藏卡信息的消息发给对手。

## 回归

1. `python Tools/Validation/Test-TextEncoding.py` 检查项目 C# 文本进程管道缺失编码声明和 `Encoding.Default`。
2. `javac -encoding UTF-8 -d .tools/encoding-tests Tools/ForgeBridge/tests/Utf8PipeFixture.java`，然后 Unity 执行 `TextEncodingValidation.Run()`。
3. `ForgeFetchValidation.Run()` 验证真实繁茂丘陵联机会话，包含“不可选”中文与混合卡牌印刷。

字符测试覆盖中文、日文、英文、重音、组合字符、补充平面字符、emoji、引号、反斜杠、换行。Java 使用 GBK 与 Shift-JIS 默认编码分别测试；管道故意分割多字节字符，验证读写无损且不吞掉 JSON 边界。还覆盖网络字符串、录像压缩编解码及中文/日文文件路径。
