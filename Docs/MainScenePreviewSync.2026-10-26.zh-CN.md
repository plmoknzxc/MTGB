# 主菜单首页编辑态同步

主场景未运行时仍是三个570×86按钮的旧首页，运行时改成四个520×76按钮，并新增本地模式及箭头。已通过同一个MenuVisualTheme.ApplyStartScreen入口把固定首页外观保存回Main.unity。

- 本地模式按钮由公共工厂取得或创建，运行时绑定现存按钮，不再无条件克隆在线入口。克隆与绑定均清空原事件集合，避免串到在线入口。
- 四个入口的间距、圆角、箭头、字号及普通／悬停／选择配色保存在主场景中。程序化圆角贴图另存为MenuNavigationShape.asset，关闭／重开场景后仍可显示。
- 编辑器菜单MTGB/UI/Sync Main Menu Scene Layout可在Main场景非运行状态重新同步首页；它不加载卡库、不创建比赛或网络会话，不改卡组编辑动态内容。

实际重新打开场景与运行后的四入口位置、尺寸、Image颜色、CanvasRenderer普通颜色、文字、字号24项逐项相同。再次同步后只有一个本地入口，素材均为持久引用。运行时四入口存在且可交互，本地模式实际点击进入设置页，返回恢复首页。

证据：`.tools/main-scene-sync-1026/`下edit-before、edit-after、edit-reloaded、runtime、comparison和runtime.png。interaction-result保留首轮导航结果：七项通过，返回相关三项因新Canvas刚创建、驱动过早取射线而失败；等待渲染边界后的back-retry三项均通过。早期探针将MainMenuController所在对象误当Canvas父级，抛出查询异常，已修正探针；不是产品运行异常。

本轮只同步主菜单首页，卡组编辑、联机房间和本地设置属于各自运行时界面；新战场的完整动态编辑预览尚未在本轮实现。未打包、覆盖客户端、更新服务器或上传Git。
