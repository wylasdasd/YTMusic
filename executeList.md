# 开发里程碑（历史记录）

> 本文档记录项目从初始规划到当前实现的关键路径。早期方案（如 `CommunityToolkit.Maui.MediaElement`）已被实际架构取代，仅作背景参考。

## 已完成的核心能力

### 1. YouTube 数据与流解析
- 使用 **YoutubeExplode**（无需官方 API Key）完成搜索、分页与流地址提取。
- 默认播放 **audio-only** 流；视频模式支持 muxed 单流或 video-only + audio-only 分离流。
- 流 URL **即时解析**（JIT），不长期缓存（YouTube 链接会过期）。

### 2. 播放引擎（当前架构）
- **方案 B**：`MusicPlayerService`（`IPlaybackHost`）→ `PlaybackSwitcher` → 五种 `IPlaybackInstance`。
- **Web 播放**：`GlobalAudioPlayer.razor` + `audioPlayer.js` + 本地 HTTP 代理（`LocalAudioProxy` / `LocalFileProxy`）绕 CORS。
- **Android 原生**：Media3 ExoPlayer + `PlaybackForegroundService`（音频后台）+ `VideoPlayerActivity`（全屏视频）。
- **iOS**：`IosNativeAudioPlaybackService`（音频）；视频为空实现，走 Web 兜底。
- **Windows**：WebView2 + 代理；自定义窗口壳层（`MainWindow` + `WindowChromeService`）。
- 详见 [`memory-bank/playbackArchitecture.md`](memory-bank/playbackArchitecture.md)。

### 3. 默认音频 / 可选视频
- 音频：`GetAudioOnlyStreams()`，WebM 优先。
- 视频：muxed 优先；无 muxed 时合并或 Hybrid（分离流，见播放架构文档）。
- Android 在线视频走原生 ExoPlayer；Windows 走 Web `<video>` 或 Hybrid。

### 4. 后台播放（Android）
- `PlaybackForegroundService` 前台服务 + 手写 MediaStyle 通知 + MediaSession。
- Android 13+ 运行时通知权限（`POST_NOTIFICATIONS`）在 `MainActivity` 请求。

### 5. UI / UX（Blazor + MudBlazor）
- 搜索、收藏夹、播放器、本地下载、传输任务、AList 上传、播放历史。
- 固定底部导航（Favorites / Player / Home / Other）；主题侧边栏 5 套主题。
- 播放器进度条由 JS 托管（`ytm-player-progress`），避免 Blazor 高频重绘卡顿。

## 若继续演进，可参考方向

1. **播放历史 SQLite 持久化**（当前为运行期内存）。
2. **Windows Hybrid 分离流**：无原生音频时评估改为 Web 双元素或合并流策略。
3. **下载 / 播放失败时的 VPN 入队前探测**（已有 `NetworkErrorService` 弹窗提示）。
4. **补充不依赖网络的单元测试**（播放列表、去重、收藏筛选等）。

完整进度见 [`memory-bank/progress.md`](memory-bank/progress.md)。
