# 清理后占用复核

本轮用户说明：删除后 D 盘仅剩约 7G 可用空间，并要求继续保留录屏提帧工具包。

- 实测 D 盘可用空间：7.49 GiB。这个数是整块盘的剩余空间，不是项目大小或本轮释放量。
- 项目文件内容大小：51.20 GiB，含隐藏目录，不跟随 44 个链接；遍历无读取错误。
- CardImages：20.73 GiB；.tools：17.94 GiB；主 Library：4.38 GiB。
- 上轮 12 个旧批次和 4 张重复截图均已删除，删除记录位于 `.tools/validation-retirement-1005/user-cleanup-log.jsonl`。文件内容总量减少约 10.14 GiB；删除前未保存真实磁盘占用，不能把此数当成实际释放空间。
- Windows GetCompressedFileSizeW 报告的本轮文件存储字节总量约 46.63 GiB，与内容大小不同；普通文件该 API 不计簇尾取整，不等于磁盘属性中的精确占用空间。

## 下一批明确缓存：6.666 GiB

| 路径 | 文件内容大小 GiB |
|---|---:|
| Library/PackageCache | 2.295 |
| Library/Artifacts | 0.385 |
| Library/Bee | 0.259 |
| Library/BurstCache | 0.422 |
| Library/BuildCache | 0.125 |
| Library/ShaderCache | 0.025 |
| Library/ScriptAssemblies | 0.056 |
| .codex/validation/description-unity/Library | 3.099 |

脚本 `.tools/storage-audit-1005/delete_rebuildable_caches.py` 默认只预览，加 `--apply` 由用户执行删除。预览已通过，尚未执行删除。实际删除前检查 Unity 编辑器及编译进程已关闭、路径在项目内且无链接，同时核对保留证据未变化。主项目 `Library/VP` 内存在链接到 Assets/ProjectSettings 的虚拟项目，因此不删除整个主 Library，而仅选择表内缓存子目录。

卡图、录屏工具包、ForgeRuntime、源文件、当前验证证据及失败记录保留。主缓存会在下次启动 Unity 时重新生成，首次导入和编译更慢，空间可能重新增长；旧验证项目缓存只有再打开该验证项目时才重建。本轮不跑效果回归，不恢复暂停的目标。
