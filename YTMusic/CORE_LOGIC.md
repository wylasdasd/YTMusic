# 核心难点逻辑和内容 (Core Difficult Logic & Content)

跨平台 YouTube 音乐播放器在流解析、播放分流、代理与 Android 后台等方面有若干关键难点。以下按当前代码基线说明。

## 1. 播放管线分流（方案 B）

**难点：**
- 在线 / 本地、音频 / 视频、Android 原生 / WebView 并非单一路径。
- 音频切视频、Web 与原生并存时，容易出现双音轨或状态不同步。

**解决逻辑：**
- `MusicPlayerService` 作为唯一状态源，实现 `IPlaybackHost`。
- `PlaybackSwitcher` 用 `SemaphoreSlim` 串行执行 `Detach` → `Attach`，任意时刻仅一种 `PlaybackKind` 活跃。
- 五种实例：`NativeAudio`、`NativeVideo`、`WebAudio`、`WebMuxedVideo`、`Hybrid`。
- Android 在线视频统一走 `NativeVideo`（ExoPlayer 全屏）；Windows 走 Web `<video>` 或 Hybrid（分离流时 Web 静音画面 + 原生/iOS 音频轨）。

**核心代码：**
- `Services/MusicPlayerService.cs`
- `Services/Playback/PlaybackSwitcher.cs`、`PlaybackInstances.cs`
- `Services/Abstractions/Playback/IPlaybackHost.cs`、`IPlaybackInstance.cs`
- 详细路由表：[`../memory-bank/playbackArchitecture.md`](../memory-bank/playbackArchitecture.md)

## 2. 跨平台 Web 音频与代理

**难点：**
- YouTube 常提供 WebM/Opus 音频；WebView 对格式与 CORS 支持因平台而异。
- 本地文件路径不能直接在 `<audio>` 中使用。

**解决逻辑：**
- 无原生音频的平台（Windows 等）：`LocalAudioProxy` 将远程流暴露为 `http://127.0.0.1:...`；`LocalFileProxy` 代理本地文件（URL 带 `&f=路径` 防同端口误判切歌）。
- `GlobalAudioPlayer.razor` 持久挂载 `<audio>` / `<video>`；`audioPlayer.js` 负责加载、播放、进度条（`ytm-player-progress`）。
- WebM **音频**默认走隐藏 `<audio>` + 代理（`audio/webm`），不再默认切 OGV；`ogv.js` 仍保留初始化，作极端兜底。
- 播放器进度条由 JS 直接更新 DOM，**禁止** MudSlider 绑 `CurrentTime`（此前卡顿根因）。

**核心代码：**
- `Components/Layout/GlobalAudioPlayer.razor`
- `wwwroot/js/audioPlayer.js`
- `MusicPlayerService.cs` 内 `LocalAudioProxy`、`LocalFileProxy`

## 3. YouTube 流媒体地址解析

**难点：**
- 无公开 MP3 直链；DASH 分离音视频；签名算法频繁变化。

**解决逻辑：**
- 集成 `YoutubeExplode`：`GetManifestAsync` → 按场景选流。
- 音频：`.GetAudioOnlyStreams()`，WebM 优先。
- 视频：muxed 优先；无 muxed 时 video-only (mp4) + audio-only (WebM) 分离流。
- 分离流画质：`UiPreferencesService.RemoteVideoStreamQuality`（默认最低，Theme 侧边栏可改）。
- 可选后台预检：`PrefetchRemoteVideo`（默认关）；在线视频确认弹窗**不再**前置 manifest 检测。
- 流 URL 在用户点击播放时 **JIT** 解析，不长期缓存。

**核心代码：**
- `Services/YouTubeService.cs`
- `MusicPlayerService.ResolveRemoteWebVideoStreamsAsync`

## 4. Android 原生播放与后台

**难点：**
- 后台播放需前台服务；Android 13+ 通知权限；部分 ROM 通知按钮显示不一致。

**解决逻辑：**
- **Media3 框架 + ExoPlayer 实现**（非两套并行播放器）。
- `AndroidExoPlayerFactory` 创建音频/视频 ExoPlayer 实例。
- `PlaybackForegroundService`：音频 ExoPlayer + 手写 MediaStyle 通知 + 平台 MediaSession Token。
- `VideoPlayerActivity`：全屏视频；分离流用 `ExoPlayerStreamSourceFactory` + `MergingMediaSource`；`KeepScreenOn` 防熄屏。
- `MainActivity` 请求 `POST_NOTIFICATIONS`；Manifest 声明前台服务类型。

**核心代码：**
- `Platforms/Android/Services/PlaybackForegroundService.cs`
- `Platforms/Android/Services/VideoPlayerActivity.cs`
- `Platforms/Android/Services/ExoPlayerStreamSourceFactory.cs`
- `Platforms/Android/Services/AndroidNativeAudioPlaybackService.cs`

## 5. 跨平台文件系统与路径

**难点：**
- 各 OS 沙盒路径与权限不同；标题含非法文件名字符。

**解决逻辑：**
- 统一下载目录：`StoragePaths.GetDownloadedMusicDirectory()`（基于 `LocalApplicationData`）。
- 下载时净化文件名：`Path.GetInvalidFileNameChars()`。
- 下载记录入 SQLite（`LocalMusicService` / `DownloadedTracks`）。

## 6. AList 上传 / 远端下载

**难点：**
- 远端「一目录一首歌」；真实下载 URL 需 `POST /api/fs/get`；封面在 Blazor 中不宜用 `file://`。

**解决逻辑：**
- 上传：`Remote Directory/<歌名md5>`，先 `mkdir`，再 `metadata.json` + 主媒体；封面仅 `thumbnailUrl` 写入 JSON。
- 目录下载：识别主音视频 + 封面，写入 `DownloadedTracks`；封面转 `data:image/...;base64,...` 存 `ThumbnailUrl`。
- 设置持久化：`AListUploadSettingsService`（`BaseUrl`、`Token`、`RemoteDirectory`）。

**核心代码：**
- `AListUploadService.cs`、`UploadManagerService.cs`、`AListRemoteDownloadManagerService.cs`

## 7. Windows 窗口与布局

**难点：**
- 自定义标题栏拖拽、最大化白闪、WebView2 与 WinUI 重绘时序冲突。

**解决逻辑：**
- `MainWindow.xaml` + `MauiProgram` 配置 `AppWindow.TitleBar` / `OverlappedPresenter`。
- `WindowChromeService` + `mouseInterop.js`：mousedown 记录起点 + 全局 mousemove + `SetWindowPos(..., SWP_NOSIZE)`。
- 顶栏按钮与拖拽热区在 `MainLayout.razor`，通过 `IsWindowsDesktop` 分支。

## 8. 已下载本地文件切歌不同步

**难点：**
- 多文件共用 `LocalFileProxy` 同源；JS 曾去掉 query 比较导致跳过 `loadSource`。
- Android 已下载音频走 ExoPlayer，不经过 `audioPlayer.js`。

**解决逻辑：**
- Web：`loadSource` 比较完整 URL；`BuildLocalProxyStreamUrl` 增加 `&f=`；切代理前 `OnRequestPause`。
- Android：切歌 `Stop()` + `ClearMediaItems()` + `SetMediaItem`。

详见 `memory-bank/coreChallenges.md` 与 `decisionLog.md` 2026-06-07 条目。
