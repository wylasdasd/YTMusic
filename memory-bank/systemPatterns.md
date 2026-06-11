# 系统模式 (System Patterns)

> 最后更新：2026-06-29

## 技术架构
- **框架**: .NET 10 Blazor MAUI (跨平台桌面/移动端)。
- **UI 组件**: MudBlazor。
- **服务导向**: 核心逻辑封装在服务类中（例如 `MusicPlayerService`）。

## 关键技术模式
- **音频代理**: `LocalAudioProxy` 和 `LocalFileProxy` 使用 `HttpListener` 为 HTML5 `<audio>` 标签提供流服务，从而绕过 CORS 限制或直接文件访问限制。
- **JS 互操作**: 广泛使用 `IJSRuntime` 来控制音频元素（播放/暂停、进度跳转、事件监听）。
- **播放器进度条（高频 UI 不进 Blazor）**:
  - `audioPlayer.js` 在 `PlayerAudio` / `PlayerVideo` 容器内 `mountProgressBar`，类名 `ytm-player-progress`。
  - 播放进度由 JS 读 `activePlayer.currentTime`（rAF + `timeupdate`）直接改 `<input type="range">` 与时间文本；**禁止**用 MudSlider 绑 `CurrentTime` 驱动重绘（此前卡顿/不跟手根因）。
  - Seek：Web 侧 `seekProgressTo` → `setCurrentTime`；原生侧 `OnProgressSeek` → `MusicPlayerService.SeekAsync`；`setNativeProgressMode` 切换数据源。
  - `.NET` 侧 `OnTimeUpdate` 仍节流回写 `MusicPlayerService`（约 500ms），供逻辑/历史用，不用于刷新进度条 UI。
  - webm 音频走 `<audio>` + 文件代理 `audio/webm`；视频画面用 `<video controls=false>`，进度条与音频页共用 JS 组件。
- **页面内滚动（非整页）**:
  - `app.css`：`html/body/#app` 与 `ytm-main` 固定视口、`overflow: hidden`；列表在 `.ytm-page__scroll` 内 `overflow-y: auto`。
  - `PageListScroll.razor`：Header 固定 + `ChildContent` 进可滚动区；`PageKey` 写入 `data-page`。
  - Upload：`ytm-page--tabs`，各 `MudTabPanel` 带 `data-page`（`upload-local` / `upload-remote` / `upload-settings`）。
- **滚动位置缓存（`ytmLayout.js`）**:
  - 存储：`window.ytmLayout._pageScrolls`（内存字典，刷新丢失）。
  - 写入：滚动监听 + `saveAllPageScrolls`；读取：`restorePageScrolls`（最多 10 次重试，应对异步列表高度）。
  - 初始化：`initPageScrollPersistence` 监听 `.ytm-body` DOM 变化以绑定新页面滚动区。
  - C# 仅调度，不保存数值：`MainLayout.OnAfterRenderAsync` / `NavigateToTabAsync`。
- **Upload MudTabs 触摸横向滑动**:
  - `ytm-tabs-touch-scroll` + `initTouchScrollTabs`：隐藏 Mud 箭头，用 `.mud-tabs-tabbar-content` 原生横向滚动。
- **全局状态管理**: `MusicPlayerService` 作为单例/作用域状态提供者，向整个 UI 提供数据，并通过事件 (`OnChange`) 通知组件更新。
- **轻量设置持久化**:
  - `UiPreferencesService` 统一承接主题索引、`Favorites Image`、`MediaTitleTwoLines`、分离流画质、视频预检等。
  - `AListUploadSettingsService` 承接 AList `BaseUrl` / `Token` / `RemoteDirectory`。
  - 这类设置不落 SQLite，而是走 MAUI `Preferences`。
- **标题展示**:
  - `MediaTitle` 组件读取 `UiPreferencesService.MediaTitleTwoLines`，全局统一两行截断或完整换行。
  - 播放器 `.player-title` 等样式在 `app.css` 中维护部分全局规则。
- **设计时构建隔离**:
  - `YTMusic/Directory.Build.props` 仅在设计时将 `YTMusic` 限制为 Windows TFM；不可放在仓库根，否则会污染 `CommonHelp`。
- **页面级 CSS**:
  - 项目 `EnableDefaultCssItems=false`；`*.razor.css` 会进 `YTMusic.styles.css`，但不宜假设所有页面样式自动生效，关键布局应优先 `app.css` 或确认 bundle。
- **导航分层**:
  - 底部导航：**Favorites / Player / Home / Other**（4 项）。
  - `Other` 承接本地资源、传输任务、AList 上传、播放历史；`Transfers` 等页内二/三级导航。
- **播放历史统一入口**:
  - 播放历史不应在页面层各自维护，而是统一在 `MusicPlayerService` 成功开始播放后写入。
  - 页面层只负责展示与回放历史项。
- **播放架构（方案 B）**:
  - 详见 **[playbackArchitecture.md](./playbackArchitecture.md)**（路由表、五种 `PlaybackKind`、`PlaybackSwitcher`、流解析、UI 协作、设置项）。
  - 概要：`MusicPlayerService` → `PlaybackSwitcher`（`SemaphoreSlim`）→ `IPlaybackInstance`；任意时刻单活跃管线；`NativeAudio`↔`Hybrid` 可 `preserveNative` 共享 ExoPlayer 音频后端。
  - Android 在线视频：`NativeVideo` + ExoPlayer（muxed 单流或 `MergingMediaSource` 合并分离流）；Windows 分离流走 `Hybrid`。
  - 在线流：muxed 优先；分离流画质由 `UiPreferencesService.RemoteVideoStreamQuality`（默认最低）；可选后台预检 `PrefetchRemoteVideo`（默认关）。
- **Android 视频分流**:
  - Android 视频播放主路径为原生 `VideoPlayerActivity`（`KeepScreenOn` 防熄屏）。
  - `INativeVideoPlaybackService` 需要提供足够的事件（包括 `PlaybackStopped`），让 `MusicPlayerService` 能同步原生退出、结束、暂停等状态。
  - 在线视频确认弹窗不再预检 manifest（确认后才 loading + 解析）。
- **Windows 窗口壳层定制**:
  - Windows 端使用 `Platforms/Windows/MainWindow.xaml` 接管默认 MAUI 窗口，而不是只依赖 `new Window(new MainPage())`。
  - `MauiProgram` 中通过 `ConfigureLifecycleEvents` 配置 `AppWindow.TitleBar` 与 `OverlappedPresenter`，保留边框、缩放、最小化/最大化能力，同时折叠标题栏高度。
  - 页面层的窗口交互不要依赖 `Window.TitleBar` 自带行为；窗口按钮与拖拽热区由 `MainLayout.razor` 主动提供，调用 `WindowChromeService` 完成最小化、最大化、关闭、拖拽。
  - Windows 拖拽实现采用“按下记录起点 + JS 全局 mousemove + Win32 移动窗口”的方式，行为更接近参考模板；拖拽热区鼠标样式保持 `cursor: default`，避免出现 `move` 十字光标。
  - 涉及窗口按钮、拖拽区、桌面专用布局时，UI 层统一通过 `IsWindowsDesktop` 做平台分支，避免影响 Android/iOS。
  - 顶部工具栏在 Windows 放大窗口时应使用全宽布局，不要给 `.ytm-topbar-inner` 设固定 `max-width + auto margin`，否则左右控件不会真正贴边。

## 组件结构
- **GlobalAudioPlayer.razor**: 实现在 `MainLayout` 中，跨页面持久存在的 `<audio>` / `<video>`；`OnProgressSeek` / 节流 `OnTimeUpdate`；原生播放时 `OnTimeChanged` → `audioPlayer.setProgress`。
- **MediaTitle.razor**: 各页音视频标题统一组件，跟随「两行显示」设置。
- **Upload.razor**: AList 双标签（`Local`/`Remote`），`BadgeData` 显示计数；上传任务状态内嵌列表项。
- **Player.razor**: 全屏播放器视图，包含详细控制项和元数据展示。
- **PlayerAudio.razor / PlayerVideo.razor**:
  - 两者现在都依赖各自独立的 scoped CSS。
  - Razor scoped CSS 不会跨组件共享，不能假设在 `PlayerAudio.razor.css` 中定义的基础类会自动作用到 `PlayerVideo.razor`。
- **Favorites.razor**: 负责收藏歌曲的管理及列表播放的触发。

## 数据持久化
- **SQLite**: 使用 `Dapper` 进行关于收藏夹、文件夹和音轨的数据库操作。
- **下载库**: `LocalMusicService` 使用 SQLite `YTMusicDownloads.db3 / DownloadedTracks` 维护下载记录；文件本体位于 `StoragePaths.GetDownloadedMusicDirectory()`。
- **播放历史**: 当前为 `MusicPlayerService` 运行期内存列表，尚未落库。
