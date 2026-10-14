# Forge 官方卡图导入

图片来源为 Scryfall 的 `default_cards` 批量数据及图片 CDN。使用当前 `ForgeRuntime/catalog.jsonl` 的系列代码、收藏编号及卡名匹配，不跨印刷版本替代。允许来源提供的 printed_name / flavor_name，以及明确的 Unicode 标点等价形式；无法确认的条目留在缺图清单。

## 目录

### 基础卡图包

2026-10-12 按用户要求恢复基础包：按 Forge 规范规则牌名及牌面去重，先从所有非歧义印刷中选中文卡图；该牌面没有本地中文图时才选英文。双面牌保留正反面；衍生物、合体、特化与机制辅助图保留展示消费者需要的准确键，逐键中文优先。排除日文图与完整异画库，不重新压缩图片。项目根目录的完整 `CardImages` 保持原样，用于本机显示与按需下载源，玩家个人导入图片另行保存。

`Tools/ForgeBridge/Package-Client.py` 将选中的完整卡面原字节放在发布包的 `CardImages/<SET>/<NUMBER>.<原扩展名>`，供各界面语言使用；`package-manifest.json` 保留原始语言、来源路径、准确印刷/牌面、SHA-256、尺寸及缺图清单。中文资源只是采用通用加载路径，不被标为英文。运行包使用当前客户端的同规则牌回退；准确键辅助图不跨印刷替换。生成时逐张检查 JPEG/PNG 格式、尺寸及完整性，并核对运行资源哈希和最终 ZIP CRC。

本次 Windows x64 发布 ZIP 为 5,364,660,558 字节，包含 36,862 张卡面（31,710 中文、5,152 英文替补），覆盖 33,872 个规则牌名及准确键辅助图。全部图片像素解码/尺寸与 ZIP CRC 通过，独立核对 5,152 个英文替补均满足相应中文缺失条件；Unity 生产卡图加载器对十个发布路径样本解码通过，样本字节与 ZIP 清单一致。仍有 108 个牌名的 109 个牌面没有可用的非歧义本地图，逐项留在清单中，不视为完整卡图覆盖。

发布程序在本机持续测试服务器下两端 BO1、调度、选择中重连与终局检查通过。首轮并发校验期间的 Forge 启动超时和第二轮 LAN 房主测试进程退出后的客户端超时保留，不授完整 LAN 或跨机器通过。本批未重跑完整语言切换/详情 UI 矩阵和全卡效果；卡图解码不作为机制效果证据。成品、原报告和审计保存在 `C:/Users/Sam/MTGB-Releases/2026-10-12/`，核心审计为 `package-audit.json`。

- 正面：`CardImages/<SET>/<NUMBER>.jpg`
- 双面牌背面：`CardImages/<SET>/<NUMBER>_2.jpg`
- 默认普通尺寸卡面通常为 488×680；不是仅有插画的裁剪图。
- Unity 支持 PNG/JPG/JPEG；已有图片保留，不覆盖用户的本地图。
- 编辑器及 Multiplayer Play Mode 使用项目根目录；独立客户端从 EXE 同级 CardImages 读取。

当前 `Builds/ForgeValidation/CardImages` 已建立到项目 CardImages 的目录链接，供本机测试客户端共享图片。独立分发或移到其他机器时，应携带实际 CardImages 文件夹，不能只复制这条目录链接。

图片不放进 Assets，不需要重新编译或 Unity 纹理导入。已经显示的占位卡可能要等下一次界面刷新或重新开局才更新。

## Specialize 数字特化卡面

Assets/Resources/SpecializeCardArt.json 用原物理印刷键、原生 SpecializeW/U/B/R/G 状态和准确当前卡名映射该面官方印刷键。完整普通JPEG保存在 CardImages/HBG/<NUMBER>w.jpg 等，不挤入双面牌 _2 索引，原物理牌图保留。

Tools/ForgeBridge/Import-SpecializeImages.py --metadata <冻结来源目录> --output <工作目录> --install 从当前Forge卡文与Scryfall原印刷 all_parts 精确关联，不按相似名字猜面。2026-10-10的19原牌/95关联面导入和Unity解码通过，这是数据核对不是95效果case。关联图使用官方英文完整卡面，已有本地图不覆盖。原生状态/费用/移区证据见 .tools/specialize-tail-1010/audit.json，独立payload仍归机制goal。

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

## 完整 Windows 客户端图包（2026-10-13）

本次交付 `C:/Users/Sam/MTGB-Releases/2026-10-13/MTGB-Windows-x64-zhCN.zip`，桌面解压客户端为 `C:/Users/Sam/Desktop/MTGB/MTGB.exe`。图像是包内真实文件，不依赖项目目录链接。按规则名／牌面选一个版本，中文优先、无中文才使用英文，必要的双面与精确辅助牌面保留，不含日文或完整异画库。

包含 36,862 张卡面，其中中文 31,710、英文回退 5,152；图像 5,097,907,567 字节，ZIP 5,365,104,887 字节。仍有 109 个牌面本地无可用图，详情在交付 manifest 的 `missing`，不声称卡图全覆盖。压缩包 SHA-256 为 `b5c8523c66f80fc93817aafea428c44ccdaab1dfd769e6b26bf79202f7ff538b`。

打包器执行完整图片像素解码、尺寸和哈希、ZIP CRC 检查；独立解压文件与中文优先核对见交付目录 `package-audit.json`。实际 Unity 客户端的 10 个中文／英文／双面／特殊辅助键图像加载样本与完整牌图加载通过，见 `unity-image-samples.json`。取证与范围见 `.tools/client-full-1013/README.zh-CN.md`；仅验证图包，不替代规则效果或联网完整对局验证。

## 下载与续传

需要 Python、requests、Pillow：

```powershell
python Tools/ForgeBridge/Import-CardImages.py --workers 12
```

重复运行会检查已有文件并跳过，不重复下载。更新卡池后需要获取新的来源元数据：

```powershell
python Tools/ForgeBridge/Import-CardImages.py --refresh-metadata
```

每张新图片先检查 JPEG 格式、尺寸及完整性，再原子写入；单文件失败重试，磁盘剩余不足 4 GiB 时保留空间。下载不修改规则脚本、卡牌目录或运行哈希。

## 审计文件

`.tools/card-images/` 包含：

- `bulk-metadata.json`：来源快照与日期。
- `plan.json`：精确印刷/牌面对应的图片 URL。
- `results.jsonl`：本轮成功、已有、失败及新文件 SHA-256。
- `unmatched.json`：印刷歧义、名称不符或来源无图等缺项。
- `placeholders.json` / `placeholders/`：来源明确标记的占位图片，已隔离，不作为正式卡图加载。
- `summary.json`：该轮结束时的汇总；下载途中不能用上一轮汇总判断完成。

卡图 JPG 是可重新下载的外部资源，已排除出 Git。素材版权归原权利人，来源：https://scryfall.com/。


### Incubob 特殊衍生物配图（2026-10-12）

用户授权将 Incubob 生成的 Incubator Dark Confidant／Dark Confidant 以普通抚育器 TMOM/18 正面、黑暗亲信 RAV/81 背面显示。字节副本位于 CardImages/MTGB/Incubob.jpg 与 Incubob_2.jpg，准确来源链接及配图出处保存 Tools/ForgeBridge/ProjectTokenArt.json；Build-RelatedTokenIndex.py 在离线重建索引时保留该链接。这是展示替用，不声称存在该印刷的特殊官方双面衍生物；原生名字、类别、规则、指示物与攻防保持真实对局值，不给其他来源的普通抚育器换图。双方正反面／详情实际加载及数据检查见 .tools/incubob-art-and-reuse-1012/audit.json。
