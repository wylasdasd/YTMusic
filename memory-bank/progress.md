# 项目进度 (Progress)

> 最后更新：2026-07-05

## 已完成
### AList
- [x] AList 上传设置持久化（`BaseUrl`、`Token`、`RemoteDirectory`）。
- [x] 上传目录 `Remote Directory/<歌名md5>`，先 `mkdir` 再上传。
- [x] 上传顺序：`metadata.json` → 主音视频；JSON 与 `DownloadedTracks` 字段子集对齐（`RemoteTrackMetadata`）。
- [x] 不再上传封面文件；`thumbnailUrl` 写入 JSON。
- [x] 上传进度分阶段加权，完成前封顶 99%。
- [x] `Upload Local` 基于本地下载列表上传；上传中隐藏 Uploaded 标签。
- [x] 标签页 `BadgeData`（选中数 / 远端文件数）；`Select All`/`Clear` 合并为 toggle 图标按钮。
- [x] `Uploaded Files` 简化列表；`MudTabs` 小屏适配（`MinimumTabWidth=50%`、短标签+图标）。
- [x] AList 远端目录下载、`Transfers` 接入、重复执行允许、同名覆盖。
- [x] `DownloadedTracks` 上传/来源追踪字段。

### 播放
- [x] 基础搜索与播放、本地代理、收藏、列表播放模式。
- [x] 无缝单曲循环（JS `currentTime` 重置）。
- [x] Android Media3 + ExoPlayer + 手写前台通知；平台 MediaStyle 三键。
- [x] Android 原生视频主路径；`PlaybackStopped` 状态链。
- [x] **方案 B 播放架构**：`PlaybackSwitcher` + 五种 `IPlaybackInstance`；文档见 `memory-bank/playbackArchitecture.md`。
- [x] Android 在线视频 ExoPlayer（muxed / `MergingMediaSource`）；全屏自动播放 + 防熄屏。
- [x] 播放设置：分离流画质（默认最低）、视频后台预检（默认关）；在线视频弹窗去掉预检。
- [x] **已下载歌曲切歌**：Web 代理 URL 误判修复 + Android ExoPlayer 切歌清理。
- [x] **播放器进度条 JS 托管**（`ytm-player-progress`）：替代 MudSlider+Blazor 重绘，解决不跟手；音频/视频页共用；本地仅 `.mp4`+`IsVideo` 走视频流。
- [x] 在线播放统一 WebM 偏好（移除 High Quality Audio 设置）。
- [x] `NetworkErrorService` / VPN 建议弹窗（播放、搜索失败）。

### UI / 平台
- [x] Windows 自定义窗口壳层、拖拽、顶栏布局。
- [x] `UiPreferencesService`：主题、`Favorites Image`、**两行显示标题**（`MediaTitleTwoLines`）。
- [x] `MediaTitle` 组件统一各页音视频标题展示。
- [x] Android 顶栏三横杠右对齐（`ytm-theme-toggle-mobile`）。
- [x] `Directory.Build.props` 仅作用于 `YTMusic` 项目（修复 CommonHelp Android 还原错误）。
- [x] 底部导航 / `Other` / 运行期播放历史 / 还原默认等。

- [x] **文档与代码对齐**（2026-06-29）：README、ARCHITECTURE、CORE_LOGIC、PROJECT_ANALYSIS、AGENTS、memory-bank 全量更新。

### 架构拆分（BLL / DAL）
- [x] 新建 `YTMusic.BLL`、`YTMusic.DAL`；仓储接口在 BLL，实现在 DAL。
- [x] 业务服务迁入 BLL：`YouTubeService`、收藏/下载/AList/网络错误等。
- [x] UI `Adapters/` 实现 BLL `Ports`；`MauiProgram` 注册 `AddYTMusicDal()` + `AddYTMusicBll()`。
- [x] UI `Services/` 仅保留播放管线与 UI 壳层。
- [x] 两层 `AppGlobal.cs` 集中常量与运行时状态。
- [x] **文档与 AGENTS 对齐**（2026-07-05）：各库职责、依赖约束、AppGlobal 约定。
- [x] **UI `Infrastructure/`**（2026-07-05）：`Proxies/`、`Storage/` 从 `Services` 迁出。
- [x] **BLL `Infrastructure/`**（2026-07-05）：`YoutubeExplodeClient`、`AListFsApiClient`、`LocalFileSystem`；`AListUploadService` / `YouTubeService` 瘦身。

## 进行中
- [ ] Android 已下载切歌实机回归（列表 + 通知栏）。
- [ ] Player 页面继续向 YTube Music 风格靠拢。
- [ ] 锁屏媒体控件不同 ROM 一致性验证。

## 未来路线图
- [ ] 播放历史 SQLite 持久化。
- [ ] YouTube 下载网络/VPN 提示与入队前探测。
- [ ] 高级均衡器、离线歌词、收藏批量操作等。
