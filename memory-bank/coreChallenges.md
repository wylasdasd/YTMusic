# 核心难点 (Core Challenges)

> 路径均相对于仓库根目录。最后更新：2026-06-29。

## 0. 播放管线串行切换（方案 B）

- **难点**:
  - 音频 ↔ 视频、Web ↔ 原生切换时曾出现双音轨、状态残留。
  - 路由分散在 `PlayInternalAsync` 与各 `IPlaybackInstance`，排查需先确认 `ActivePlaybackKind`。
- **解决逻辑**:
  - `PlaybackSwitcher` + `SemaphoreSlim`：`Detach` 旧实例 → `Attach` 新实例。
  - `NativeAudio` ↔ `Hybrid` 可 `preserveNative` 共享 ExoPlayer 音频后端。
  - 完整路由表见 [playbackArchitecture.md](./playbackArchitecture.md)。
- **核心代码**:
  - `YTMusic/Services/MusicPlayerService.cs`
  - `YTMusic/Services/Playback/PlaybackSwitcher.cs`
  - `YTMusic/Services/Playback/PlaybackInstances.cs`
- **注意事项**:
  - 切歌 / 失败清理走 `DetachAllAsync`。
  - Android 在线视频必须走 `NativeVideo`，勿回 WebView 主路径。

## 1. YouTube 流播放在不同平台上的链路分流

- **难点**:
  - 并非所有平台走同一播放器链路。
  - Android 音频/视频有原生实现；Windows 等主要走 WebView + 代理。
  - 在线、本地、音频/视频分支集中在 `MusicPlayerService.PlayInternalAsync`。
- **解决逻辑**:
  - `PlaybackSwitcher` 按 `PlaybackKind` 激活对应实例。
  - Android：`AndroidNativeAudioPlaybackService` → `PlaybackForegroundService`；视频 → `VideoPlayerActivity`。
  - 无原生：`LocalAudioProxy` / `LocalFileProxy` + `GlobalAudioPlayer` + `audioPlayer.js`。
- **核心代码**:
  - `YTMusic/Services/MusicPlayerService.cs`
  - `YTMusic/Components/Layout/GlobalAudioPlayer.razor`
  - `YTMusic/wwwroot/js/audioPlayer.js`
  - `YTMusic/Platforms/Android/Services/PlaybackForegroundService.cs`
- **注意事项**:
  - 勿笼统说「全项目靠代理播放」——仅适用于 Web 分支。
  - 排查时先查 `ActivePlaybackKind` 与 `IsUsingNativePlayback`。

## 2. 播放器跨页面持久化与播放队列一致性

- **难点**:
  - 多页面可触发播放；状态放页面内易中断。
  - 顺序/随机/单曲循环逻辑易冲突。
- **解决逻辑**:
  - 队列、索引、模式收敛到 `MusicPlayerService`。
  - 随机用预生成 `ShuffleIndices`。
  - 单曲循环：`OnRequestReplay` + JS `currentTime = 0`，跳过二次解析。
- **核心代码**:
  - `YTMusic/Services/MusicPlayerService.cs`
  - `YTMusic/Components/Layout/GlobalAudioPlayer.razor`
  - `YTMusic/Components/Pages/Player.razor`
- **注意事项**:
  - 新播放入口只调 `MusicPlayerService` 公开方法，不在页面维护平行队列。

## 3. Android 后台播放通知与锁屏媒体控件

- **难点**:
  - Android 13+ 运行时通知权限。
  - Media3 绑定环境下自动通知不稳定；部分 ROM 缺上一首/下一首。
- **解决逻辑**:
  - **Media3 框架 + ExoPlayer 实现**（非两套并行播放器）。
  - Manifest + `MainActivity` 请求 `POST_NOTIFICATIONS`。
  - `PlaybackForegroundService`：Media3 Session + 手写 MediaStyle 通知 + 平台 MediaSession Token。
- **核心代码**:
  - `YTMusic/Platforms/Android/AndroidManifest.xml`
  - `YTMusic/Platforms/Android/MainActivity.cs`
  - `YTMusic/Platforms/Android/Services/PlaybackForegroundService.cs`
  - `YTMusic/Platforms/Android/Services/AndroidExoPlayerFactory.cs`
- **注意事项**:
  - 「有声音无通知」先查权限与前台服务，再查业务代码。
  - 勿轻易回退到纯自动通知假设。

## 4. Windows 自定义窗口壳层与页面顶栏交互分层

- **难点**:
  - MAUI 默认窗口难以做出完整桌面标题栏体验。
- **解决逻辑**:
  - `Platforms/Windows/MainWindow.xaml` 接管窗口。
  - `MauiProgram` 配置 `AppWindow.TitleBar` / `OverlappedPresenter`。
  - `MainLayout.razor` 提供窗口按钮与拖拽热区。
- **核心代码**:
  - `YTMusic/Platforms/Windows/MainWindow.xaml`
  - `YTMusic/Platforms/Windows/MainWindow.xaml.cs`
  - `YTMusic/App.xaml.cs`
  - `YTMusic/MauiProgram.cs`
  - `YTMusic/Components/Layout/MainLayout.razor`
- **注意事项**:
  - Windows UI 用 `IsWindowsDesktop` 分支，勿压到移动端。

## 5. Windows 窗口拖拽与系统按钮

- **难点**:
  - `HTCAPTION` / `cursor: move` 手感差；`MoveWindow` 误改尺寸；最大化拖拽白闪。
- **解决逻辑**:
  - `WindowChromeService` + `mouseInterop.js`：mousedown + 全局 mousemove + `SetWindowPos(..., SWP_NOSIZE)`。
  - 拖拽区 `cursor: default`。
- **核心代码**:
  - `YTMusic/Services/WindowChromeService.cs`
  - `YTMusic/wwwroot/js/mouseInterop.js`
  - `YTMusic/Components/Layout/MainLayout.razor`
- **注意事项**:
  - 最大化拖拽白闪在 MAUI + WebView2 下难彻底消除，只能缓解。

## 6. 已下载本地文件切歌时 UI 与音频不同步

- **难点**:
  - 多文件共用 `LocalFileProxy` 同源；JS 去 query 比较会跳过 `loadSource`。
  - Android 已下载走 ExoPlayer，不经 `audioPlayer.js`。
- **解决逻辑**:
  - Web：完整 URL 比较；`&f=文件路径`；切代理前 `OnRequestPause`。
  - Android：`Stop()` + `ClearMediaItems()` + `SetMediaItem`。
- **核心代码**:
  - `YTMusic/wwwroot/js/audioPlayer.js`
  - `YTMusic/Services/MusicPlayerService.cs`
  - `YTMusic/Platforms/Android/Services/PlaybackForegroundService.cs`
  - `YTMusic/Components/Layout/GlobalAudioPlayer.razor`

## 7. 播放器进度条卡顿（已解决，勿回归）

- **难点**:
  - MudSlider 绑 `CurrentTime` 导致 WebView2 + Blazor 高频 `StateHasChanged`。
- **解决逻辑**:
  - `audioPlayer.mountProgressBar`（`ytm-player-progress`）由 JS 直接更新 DOM。
  - 原生进度：`OnTimeChanged` → `setProgress` + `setNativeProgressMode`。
- **核心代码**:
  - `YTMusic/wwwroot/js/audioPlayer.js`
  - `YTMusic/Components/Pages/PlayerAudio.razor`、`PlayerVideo.razor`
- **注意事项**:
  - **禁止**改回 MudSlider 驱动进度条。

## 8. 构建产物残留导致的重复定义假报错

- **难点**:
  - 临时 `obj-temp` 被 SDK 当源码编译，出现重复特性错误。
- **解决逻辑**:
  - `YTMusic.csproj` / `CommonHelp.csproj` 排除 `obj-temp`。
  - 临时输出尽量放项目树外或及时清理。
- **核心代码**:
  - `YTMusic/YTMusic.csproj`
  - `CommonHelp/CommonHelp.csproj`
