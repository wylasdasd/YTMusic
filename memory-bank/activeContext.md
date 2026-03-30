# 活动上下文 (Active Context)

## 当前焦点
当前焦点已扩展到 AList 上传/下载工作流与 YouTube 失败容错：
- 上传页已具备独立的 AList 配置、远端目录浏览、本地已下载文件上传、AList 远端目录整首下载能力。
- AList 目录下载当前约定为“一个目录等于一首歌”，目录内通常只有一个主音视频文件和一个封面图。
- YouTube 下载本身未被本轮改动触碰，但已确认在某些网络/VPN 出口下会触发 `VideoUnavailableException`；播放器失败路径已补齐清空状态，避免跳回上一首。
- Android 原生视频仍被视为视频主播放路径，Blazor `/player/video` 在 Android 上只保留兜底角色。

## 最近的变更
- **AList 上传/下载工作流**：
  - 新增上传页 `/upload` 的 AList 配置持久化：`BaseUrl`、`Token`、`RemoteDirectory` 使用 `Preferences` 存储。
  - 上传设置面板支持默认收起；设置区新增 `Create Directory` 按钮，可对当前 `Remote Directory` 直接执行 `mkdir`。
  - `Upload Local` 以本地已下载文件为主入口，不再依赖文件选择器；文件选择器仍作为补充能力保留。
  - 上传目录固定为 `Remote Directory/<歌名md5>`，上传前会先创建目录，再上传主音视频文件与封面文件。
  - AList 浏览页新增 `AList Files` 页签，可在 `Remote Directory` 下浏览目录、下钻目录、返回上级，并下载单文件或整目录。
  - AList 目录展示规则已收敛为：目录项直接读取目录内主音视频文件名并去扩展名，作为展示名；仅在读取失败时退回目录名。
  - AList 目录下载会识别“主音视频 + 封面图”，下载后按本地下载数据库逻辑入库；封面会转成 `data:image/...;base64,...` 存入 `ThumbnailUrl`，避免 `file://` 路径在 MAUI Blazor 中不显示。
  - AList 下载/上传任务均已接入 `Transfers`；上传进度进一步内嵌到 `Upload Local` 每个条目中，单独的 `Upload Tasks` 页签已删除。
  - AList 目录下载和上传目前允许重复触发；本地同名下载默认覆盖，不再自动生成 `(1)`、`(2)` 重名副本。
- **本地下载记录扩展**：
  - `DownloadedTracks` 已扩展 `HasUploaded`、`UploadedDate`、`UploadedRemotePath`、`RemoteSourcePath` 字段，用于追踪上传状态和 AList 来源。
- **AList 下载兼容性**：
  - 远端文件下载改为先调用 `POST /api/fs/get` 获取 `raw_url/url`，再执行真实下载。
  - 对返回的下载地址采用“先无鉴权请求，401/403 时再带 `Authorization` 重试”的兼容策略。
- **播放器失败态修复**：
  - 修复 Home/Search 点击不可播放的 YouTube 视频后，播放器会残留上一首内容的问题。
  - `MusicPlayerService.PlayInternalAsync(...)` 在失败时现在会停止当前播放管线并清空 `CurrentVideo/CurrentStreamUrl`。
- **设置抽屉持久化**：
  - 新增 `UiPreferencesService` 持久化三类轻量设置：主题索引、`Favorites Image`、`High Quality Audio`。
  - 三横杠抽屉新增“还原默认”危险操作按钮，并改为应用内 `MudDialog` 二次确认（`OK / Cancel`）。
- **数据重置链路**：
  - 新增 `AppResetService`，统一负责清空收藏表、下载表、下载目录文件、偏好设置和播放器状态。
  - 收藏数据通过 `FavoriteService.ResetAllAsync()` 重置，下载数据通过 `LocalMusicService.ResetAllAsync()` 重置。
- **主导航重构**：
  - 底部导航 `Library` 文案改回 `Download`。
  - 原 `Transfers` 主入口收进 `Other` 页面，`Other` 下包含“下载页面”入口，下载页内部保留三级筛选 `All / Active / Completed / Failed`。
  - `Other` 页面新增“历史播放列表”入口。
- **播放历史**：
  - `MusicPlayerService` 新增运行期内的 `PlaybackHistory`（最多 50 条），统一由播放器服务记录。
  - 现已覆盖 `Home/Search`、`Favorites`、`Library/Download`、历史页重播等播放入口。
- **播放器分流**：
  - Android 视频播放继续走原生全屏 `VideoPlayerActivity`。
  - 安卓场景下 `/player` 统一留在 `/player/audio`；`/player/video` 仅保留给 Windows/非 Android 的视频播放和兜底。
  - Android 原生视频退出时，`VideoPlayerActivity -> AndroidNativeVideoPlaybackService -> MusicPlayerService` 已补齐 `PlaybackStopped` 事件链，避免页面卡在“原生播放中”的假状态。
- **Player UI 方向**：
  - `PlayerAudio`/`PlayerVideo` 已去掉中心卡片，改成沉浸式背景布局。
  - 音频页与视频页样式已拆到各自 `.razor.css`；后续平台细调必须注意 Razor scoped CSS 不会跨组件共享。
- **列表播放逻辑**：在 `MusicPlayerService` 中实现了顺序和随机播放模式。
- **UI 布局调整**：在 `Player.razor` 中，将播放模式切换按钮移动到最右侧，收藏和下载按钮移动到最左侧。
- **播放优化**：
    - 修复了非列表歌曲无法循环的 BUG。
    - **单曲循环优化**：通过 JS 层面重置 `currentTime` 实现重播，避免了完整的网络请求和 UI 加载动画（Loading），提升了平滑度。
- **模式强制执行**：播放非收藏夹歌曲时，强制开启单曲循环并禁用模式切换按钮。
- **Android 通知权限**：在 `MainActivity` 恢复 Android 13+ `POST_NOTIFICATIONS` 运行时请求。
- **Android 后台服务**：`PlaybackForegroundService` 当前采用 `Media3` 播放内核 + 手写前台通知更新（含进度条）作为稳定方案。
- **关键实机结论**：目标设备上需使用平台原生 `Notification.MediaStyle + platform MediaSession.Token`，否则可能出现“有进度条但没有上一首/下一首”。
- **代码清理**：删除无效通知权限服务抽象，减少多链路并行导致的排障噪音。
- **Windows 窗口壳层**：
  - 已新增 `Platforms/Windows/MainWindow.xaml(.cs)`，Windows 平台不再走默认 `new Window(new MainPage())`。
  - `MauiProgram.cs` 已配置 `AppWindow.TitleBar` 与 `OverlappedPresenter`，保留边框和缩放能力。
  - `MainLayout.razor` 已接入 Windows 顶栏按钮（最小化/最大化/关闭）和桌面专用拖拽热区。
  - 拖拽逻辑已切换为模板同款 `WindowChromeService + mouseInterop.js` 持续移动方案，且修正了“错误调用 `MoveWindow` 导致窗口尺寸变化”的坑。
  - 顶栏搜索胶囊已删除；Windows 下三横杠位于窗口按钮组左侧；顶栏已改为全宽，放大窗口时左右控件可贴边。

## 下一步计划
- 评估是否需要对旧的 AList 下载记录做一次数据修正，例如把历史遗留的 md5 标题改成本地媒体文件名。
- 视网络稳定性决定是否对 YouTube 下载增加“入队前探测 + 更友好的网络/VPN 提示”。
- 如果后续要提升 YouTube 视频下载成功率，优先评估“video-only + audio-only + FFmpeg 合并”的可选方案，而不是继续依赖 muxed stream。
- 实机验证 Android 原生视频“进入/退出/恢复音频页”链路，重点检查是否仍有假状态或死按钮。
- 若继续调播放器 UI，优先收敛为“平台分支 + 页面独立 scoped CSS”，避免音频页改动误以为自动影响视频页。
- 评估是否将 `PlaybackHistory` 从运行期内存提升为 SQLite 持久化数据。
- `Other` 页面后续可继续收纳低频入口（例如设置、关于、历史、调试页），但底部主导航尽量保持 5 个以内。
