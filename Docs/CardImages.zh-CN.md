# Forge 官方卡图导入

图片来源为 Scryfall 的 `default_cards` 批量数据及图片 CDN。使用当前 `ForgeRuntime/catalog.jsonl` 的系列代码、收藏编号及卡名匹配，不跨印刷版本替代。允许来源提供的 printed_name / flavor_name，以及明确的 Unicode 标点等价形式；无法确认的条目留在缺图清单。

## 目录

### 基础卡图包（2026-09-16）

分发文件：`Builds/CardImages-Basic.zip`。解压到游戏 EXE 同级，包内已有 `CardImages` 目录；客户端必须包含同规则牌缺图回退功能。请在干净的分发目录使用，直接覆盖到旧全量目录不会自动移除旧异画。

按 Forge 规范规则名每种卡选择一个印刷版本，保留所需正反面；优先牌面完整、高清来源、`artIndex=1`，最后按印刷键稳定排序。不修改图片内容或卡牌数据，不按文件哈希把不同牌合并。

本次保留 33,705 种卡、34,204 张卡面，图片内容共 3,661,879,971 字节（约 3.410 GiB），排除 63,735 张额外印刷卡面。目录中 116 种卡没有完整且无歧义的可用图片候选，详见包内 `basic-card-images-manifest.json` 的 `missing` 列表。清单同时记录每种卡选择的印刷键、各面路径及 SHA-256。

项目根目录的完整 `CardImages` 仍保留，作为 Ubuntu 异画下载源。玩家个人导入图片不参与此基础包筛选。

复现：`python Tools/ForgeBridge/Build-BasicCardImages.py --output Builds/CardImages-Basic-new.zip`。脚本拒绝覆盖已有输出，逐张检查来源清单哈希和图片完整性，打包后检查全部 ZIP CRC。JPEG 不再压缩，使用 ZIP 容器分发；卡牌规则与 ForgeRuntime 哈希保持原样。

- 正面：`CardImages/<SET>/<NUMBER>.jpg`
- 双面牌背面：`CardImages/<SET>/<NUMBER>_2.jpg`
- 默认普通尺寸卡面通常为 488×680；不是仅有插画的裁剪图。
- Unity 支持 PNG/JPG/JPEG；已有图片保留，不覆盖用户的本地图。
- 编辑器及 Multiplayer Play Mode 使用项目根目录；独立客户端从 EXE 同级 CardImages 读取。

当前 `Builds/ForgeValidation/CardImages` 已建立到项目 CardImages 的目录链接，供本机测试客户端共享图片。独立分发或移到其他机器时，应携带实际 CardImages 文件夹，不能只复制这条目录链接。

图片不放进 Assets，不需要重新编译或 Unity 纹理导入。已经显示的占位卡可能要等下一次界面刷新或重新开局才更新。

## 玩家按需下载（无 UI 接口）

### 玩家导入自己的卡图（无 UI 接口）

Unity 主线程调用 `CardArtImportService.Import(card, sourceFile, faceIndex)`，接受 PNG/JPG/JPEG 实际内容；每张图 16 MiB 以内，宽高各 100～4096 像素。当前输入是完整卡面，原样保存字节，不裁剪、不压缩、不自动套卡框。`faceIndex` 默认 0；背面使用 1，只允许卡牌数据已定义的面。

个人图片放在 `Application.persistentDataPath/CustomCardImages/<SET>/<NUMBER>.image`，背面为 `<NUMBER>_2.image`。`.image` 是内部统一文件名，内容仍是输入的 PNG 或 JPEG，避免换格式后旧文件抢占优先级。源文件保持原样，替换个人图先验证、再原子替换；导入失败保留旧个人图。

- `TryGetPath(card, faceIndex, out path)` 检查该面的个人图片。
- `Remove(card, faceIndex)` 移除该面的个人覆盖，恢复基础图/下载图或同卡回退图。
- `Changed` 事件带印刷键和牌面索引，UI 收到后重新加载对应卡图。
- 显示顺序为：精确版本个人图 → 精确版本基础/下载图 → 同规则卡已有官方图。其他版本的个人图不参与回退。
- 下载状态排除个人覆盖：导入个人图后仍可下载官方图，两者不会相互覆盖。
- 个人图只影响本机显示，不修改卡组身份、规则或联机哈希，也不上传给对手。
- 目前每个印刷版本每面保存一张个人覆盖；尚无文件选择 UI、个人异画收藏列表。为未收录异画导入图片只能作为现有版本的本地外观，不能凭图片新增 Forge 印刷版本或卡牌规则。

`CardArtImportValidation.Run()` 在 Unity Editor 中验证导入、替换、失败保护、双面、显示与恢复；测试使用独立随机印刷键，结束移除测试覆盖。

### 图包覆盖范围

2026-09-16 导入快照共 97,939 张卡面，对应 96,662 个印刷版本。还有 412 个条目未导入：378 个无法唯一精确匹配、27 个 Forge 印刷键歧义、7 个来源仅有占位图。这些统计限于当前 Forge 目录，不代表所有官方历史异画、语言版本或未来版本；完整性不能由下载计划全部完成推断。

`CardArtVersions.GetVersions(card)` 返回当前卡池同一规则身份下的非歧义印刷版本，按印刷键稳定排序。此查询只读本地目录，不请求网络。当前 Forge 使用规范规则名构成 oracleID；不按显示译名猜测同卡。

把根目录的 `card-images.example.json` 复制为 EXE 同级 `card-images.json`，填写 `baseUrl`。例如 `https://cards.example.com/CardImages/`。这份配置独立于 ForgeRuntime，不改变联机规则哈希。HTTP 仅本机测试默认允许，其他 HTTP 地址需显式设置 `allowHttp: true`。

在 Unity 主线程创建一个共用实例，并从按钮事件调用：

```csharp
var downloads = CardArtDownloadService.FromConfig();
var versions = CardArtVersions.GetVersions(card);
// UI 自行选择 versions 中的一个 CardDefinition。
var selected = versions[0];
bool installed = downloads.IsDownloaded(selected); // 所有牌面均有精确版本图片
bool busy = downloads.IsDownloading(selected);
await downloads.DownloadAsync(selected,
    new System.Progress<CardArtDownloadService.Progress>(p =>
        UnityEngine.Debug.Log($"{p.PrintingKey} face={p.FaceIndex} {p.Fraction:P0}")),
    cancellationToken);
// 下载完成后刷新当前卡图；捕获 IOException / InvalidDataException 显示失败，OperationCanceledException 显示取消。
```

- 一个服务实例最多同时下载两个版本；同一版本的重复调用返回同一个 Task，由首次调用的取消令牌和进度接收器控制。
- `Downloaded` 事件通知 UI 刷新；Task 抛出错误时可以再次调用重试。没有后台自动重试，也没有跨进程任务合并。
- 图片保存到 `Application.persistentDataPath/CardImages/<SET>/<NUMBER>.jpg`，重启继续复用。双面牌一起下载；一面失败时已完成的另一面保留，重试只补缺项。
- 只接收 JPEG，限制单文件 8 MiB、超时、禁止重定向，解码验证后先写临时文件再改名。已有本地图不覆盖。已有图片的状态检查依据文件存在；本地损坏图需用户移走后重新下载。
- `CardImageLibrary.TryLoadFullCard` 优先精确版本；缺图或解码失败时按稳定印刷顺序尝试同规则牌的已有图片。这里只替代显示图片，不更改卡组的印刷键。`TryFindImagePath` 和 `HasCardFace` 始终只检查精确版本。
- 这一版不生成缩略图、不自动联网预览、不实现最终 UI 或更换卡组版本的交互。列表可以使用 `GetImageUrl` 获取精确大图地址；未来预览应接单独的缩略图接口，避免浏览即下载所有大图。

### Ubuntu 静态托管

服务器无需运行 Forge、数据库或游戏进程。把现有 `CardImages` 目录上传到 `/srv/mtgb/CardImages`，保留大写系列目录和编号大小写；Linux 路径区分大小写。客户端请求示例：`/CardImages/2ED/162.jpg`，双面背面为 `/CardImages/ISD/51_2.jpg`。

在已有 HTTPS Nginx `server` 块内加入：

```nginx
location /CardImages/ {
    root /srv/mtgb;
    autoindex off;
    try_files $uri =404;
}
```

Nginx 工作进程需要目录读取权限；该位置应直接返回 JPEG 或真实 404，不跳转到登录页或站点首页。证书、域名、上传与公网访问需要在实际服务器上配置。本次仅提供配置范例，未部署远端服务器。

路径映射参考：[Nginx root 指令](https://nginx.org/en/docs/http/ngx_http_core_module.html#root)。

### 本机下载验证

先运行 `python Tools/ForgeBridge/Test-CardImageServer.py --image CardImages/2ED/162.jpg`，再通过 Unity Editor 调用 `CardArtDownloadValidation.Run("http://127.0.0.1:18779/", "一个新的临时目录绝对路径")`。读取 `CardArtDownloadValidation.Result` 获取结果。覆盖双面、重复点击、已有图跳过、404、错误图片、取消、重试、路径校验和临时文件清理。测试只在临时目录写入样本，不改正式卡图。

2026-09-16：上述 HTTP 下载验证在 Unity Editor 内通过，脚本编译无错误；真实目录查询 Lightning Bolt 得到 67 个版本，缺失精确图片时成功回退到 `2ED/162`。未验证实际 Ubuntu 域名、HTTPS 证书、公网带宽或独立客户端构建。测试服务已关闭。

## 下载与续传

需要 Python、requests、Pillow：

```powershell
python Tools/ForgeBridge/Import-CardImages.py --workers 12
```

重复运行会检查已有文件并跳过，不重复下载。更新卡池后需要获取新的来源元数据：

```powershell
python Tools/ForgeBridge/Import-CardImages.py --refresh-metadata
```

每张新图片先检查 JPEG 格式、尺寸及完整性，再原子写入；单文件失败重试，磁盘剩余不足 4 GiB 时保留空间。下载不修改规则脚本、卡牌目录或运行哈希。此前 Data Editor 的旧数据库/Lua 校验流程不适用于这次纯 Forge 卡图下载。

## 审计文件

`.tools/card-images/` 包含：

- `bulk-metadata.json`：来源快照与日期。
- `plan.json`：精确印刷/牌面对应的图片 URL。
- `results.jsonl`：本轮成功、已有、失败及新文件 SHA-256。
- `unmatched.json`：印刷歧义、名称不符或来源无图等缺项。
- `placeholders.json` / `placeholders/`：来源明确标记的占位图片，已隔离，不作为正式卡图加载。
- `summary.json`：该轮结束时的汇总；下载途中不能用上一轮汇总判断完成。

卡图 JPG 是可重新下载的外部资源，已排除出 Git。素材版权归原权利人，来源：https://scryfall.com/。
