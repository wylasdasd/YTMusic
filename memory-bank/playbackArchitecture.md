# 播放架构 (Playback Architecture)

> 基线：方案 B — `ActivePlayback` + `PlaybackSwitcher`，单音轨串行切换。  
> 最后更新：2026-06-29

## 1. 总览

```
UI (PlayerAudio / PlayerVideo / GlobalAudioPlayer)
        ↓ 事件 / Seek / Play
MusicPlayerService (IPlaybackHost)
        ↓ ActivatePlaybackAsync(kind, source, options)
PlaybackSwitcher (SemaphoreSlim 串行)
        ↓ SwitchAsync → Detach 旧实例 → Attach 新实例
IPlaybackInstance × 5
        ↓
┌─────────────────┬──────────────────┬─────────────────────────────┐
│ NativeAudio     │ NativeVideo      │ WebAudio / WebMuxed / Hybrid│
│ ExoPlayer 前台  │ VideoPlayerAct.  │ GlobalAudioPlayer + JS      │
│ Service (Android)│ (Android 全屏)  │ audioPlayer.js              │
└─────────────────┴──────────────────┴─────────────────────────────┘
```

**设计目标**
- 任意时刻只有一条活跃播放管线（无双音轨）。
- 音频 ↔ 视频、Web ↔ 原生切换时，旧管线先 `Detach`，再 `Attach` 新管线。
- `MusicPlayerService` 仍是唯一状态源（`CurrentVideo`、`IsPlaying`、`ActivePlaybackKind` 等）。

## 2. 核心文件

| 路径 | 职责 |
|------|------|
| `Services/MusicPlayerService.cs` | 播放入口、`PlayInternalAsync`、流解析、路由决策、实现 `IPlaybackHost` |
| `Services/Playback/PlaybackSwitcher.cs` | `SemaphoreSlim` 串行切换、`DetachAllAsync` |
| `Services/Playback/PlaybackModels.cs` | `PlaybackKind`、`PlaybackSource`、`PlaybackOptions` |
| `Services/Abstractions/Playback/IPlaybackInstance.cs` | 实例生命周期：`Attach` / `Detach` / Play / Pause / Seek |
| `Services/Abstractions/Playback/IPlaybackHost.cs` | 宿主回调：代理、Web 同步、原生服务访问 |
| `Services/Playback/PlaybackInstances.cs` | 五种 `IPlaybackInstance` 实现 |
| `Services/Abstractions/INativeAudioPlaybackService.cs` | 原生音频抽象 |
| `Services/Abstractions/INativeVideoPlaybackService.cs` | 原生视频抽象（含 `companionAudioUrl`、`autoPlay`） |
| `Platforms/Android/Services/PlaybackForegroundService.cs` | Android 音频 ExoPlayer + 前台通知 |
| `Platforms/Android/Services/AndroidExoPlayerFactory.cs` | 创建音频/视频 ExoPlayer 实例 |
| `Platforms/Android/Services/VideoPlayerActivity.cs` | Android 全屏视频 ExoPlayer |
| `Platforms/Android/Services/ExoPlayerStreamSourceFactory.cs` | 单流 / `MergingMediaSource` 合并分离流 |
| `Components/Layout/GlobalAudioPlayer.razor` | 跨页持久 `<audio>` / `<video>`，Web/Hybrid 同步 |
| `wwwroot/js/audioPlayer.js` | Web 播放、进度条 `ytm-player-progress`、Hybrid 画面 |

## 3. PlaybackKind 与引擎

| PlaybackKind | 引擎 | 典型场景 |
|--------------|------|----------|
| `NativeAudio` | Android `PlaybackForegroundService` ExoPlayer | 在线/本地音频（Android） |
| `NativeVideo` | Android `VideoPlayerActivity` ExoPlayer 全屏 | 本地 `.mp4`、在线视频（Android） |
| `WebAudio` | `GlobalAudioPlayer` `<audio>` + 代理 | Windows 在线音频、Android 无原生兜底 |
| `WebMuxedVideo` | `GlobalAudioPlayer` `<video>` + 代理 | Windows 在线 muxed / video-only 单流 |
| `Hybrid` | 原生音频轨 + Web `<video>` 画面（静音） | 非 Android 分离流（iOS 有原生音频；Windows 无原生音频时为已知 gap，依赖 muxed 优先） |
| `None` | 无活跃实例 | 停止 / 失败清空后 |

**派生 UI 属性**（`MusicPlayerService`）
- `ActivePlaybackKind` ← `_playbackSwitcher.ActiveKind`
- `IsUsingNativePlayback` → `NativeAudio`
- `IsUsingNativeVideoPlayback` → `NativeVideo`（Android 全屏时不进 `/player/video`）
- `IsUsingHybridWebVideo` → `Hybrid`
- `UsesWebPlaybackSink` → `WebAudio | WebMuxedVideo | Hybrid`

## 4. 平台 × 内容路由表

### 4.1 本地文件

| 条件 | Kind | 说明 |
|------|------|------|
| 非 `.mp4` 或未标记 `IsVideo` | `NativeAudio` / `WebAudio` | `ShouldPlayLocalAsVideo` 仅 `.mp4` + `IsVideo==true` |
| `.mp4` + `IsVideo`，Android | `NativeVideo` | `ShouldUseNativeLocalVideo` |
| `.mp4` + `IsVideo`，其他平台 | `WebMuxedVideo` | `LocalFileProxy` 代理 |

### 4.2 在线远程

| 平台 | 视频请求 | 流类型 | Kind |
|------|----------|--------|------|
| Android | 是 | muxed 单 URL | `NativeVideo` |
| Android | 是 | video-only + audio-only | `NativeVideo` + `MergingMediaSource` |
| Windows / iOS | 是 | video-only + audio-only | `Hybrid`（Web 静音视频 + `INativeAudio` 播 companion；Windows 无原生音频时需 muxed 或后续改进） |
| Windows / iOS | 是 | muxed / 单 video URL | `WebMuxedVideo` |
| 任意 | 否（音频） | audio-only WebM 优先 | `NativeAudio`（Android）/ `WebAudio`+代理（Windows） |

入口：`PlayInternalAsync(PlayingItem)`  
- `video.IsVideo == true` → 远程视频分支  
- 否则 → 音频分支  
- 显式播视频时 `shouldAutoPlay = true`（`video.IsVideo == true || !isTrackSwitch || IsPlaying`）

## 5. 在线流解析 (`ResolveRemoteWebVideoStreamsAsync`)

**顺序（固定）**
1. 有 **muxed** → 优先 `GetWithHighestVideoQuality()`，单 URL，不走合并。
2. 无 muxed → **video-only (mp4)** + **audio-only (WebM 优先)** 分离流。

**分离流画质**（`UiPreferencesService.RemoteVideoStreamQuality`，默认 **最低**）
- `Lowest`：`SelectLowestVideoOnlyStream` — mp4 中分辨率最低档，起播更快。
- `Highest`：`GetWithHighestVideoQuality()`。
- 设置位置：三横杠 Theme 侧边栏 →「分离流视频画质」。

**后台预检**（`PrefetchRemoteVideo`，默认 **关**）
- 开启后：在线音频播放成功 → `PrefetchRemoteVideoAsync` 后台解析并缓存 30 分钟。
- 点「视频」确认播放 → `GetRemoteWebVideoStreamsForPlayAsync` 优先复用预检结果（画质设置须一致）。
- 弹窗**不再**预检可用性（避免弹窗前长时间 loading）；失败在确认后提示。

## 6. PlaybackSwitcher 切换语义

```csharp
// PlaybackSwitcher.SwitchAsync
var preserveNative = previous.Kind.SharesNativeAudioBackend(next.Kind);
// NativeAudio ↔ Hybrid 共享 ExoPlayer 音频后端，Detach 时不 Stop 前台服务
await previous.DetachAsync(preserveNative);
await next.AttachAsync(host, source, options);
```

| 从 → 到 | preserveNative | 行为要点 |
|---------|----------------|----------|
| NativeAudio → Hybrid | true | 音频 ExoPlayer 不拆，只加 Web 视频 |
| Hybrid → NativeAudio | true | 停 Web，保留原生音频 |
| NativeVideo → * | false | `NativeVideo.StopAsync()` 关全屏 Activity |
| * → NativeVideo | — | Intent 带 `autoPlay`；ExoPlayer `PlayWhenReady` |

`DetachAllAsync`：停止 Web + 原生音视频 + `ActivePlaybackKind = None`（失败/切歌清理用）。

**Android 原生音频 Detach**  
`NativeAudioPlaybackInstance.DetachAsync` → `INativeAudio.DetachAsync` → `PlaybackForegroundService` `ActionDetach`（避免 `StopSelf` 与切歌竞态）。

## 7. 各 IPlaybackInstance 要点

### NativeAudio
- `Attach` → `NativeAudio.PlayAsync`；`AutoPlay=false` 则 `PauseAsync`。
- 进度/状态：服务事件 → `MusicPlayerService.OnNative*`。

### NativeVideo
- `Attach` → `NativeVideo.PlayAsync(..., companionAudioUrl, autoPlay)`。
- 在线合并流：`CompanionAudioUrl` 非空 → `ExoPlayerStreamSourceFactory` 反射创建 `MergingMediaSource`。
- **防熄屏**：`VideoPlayerActivity` `KeepScreenOn` + `WindowManagerFlags.KeepScreenOn`。

### WebAudio / WebMuxedVideo
- `Attach` 只更新 `CurrentStreamUrl` 等 presentation + `RequestWebStateSync`。
- 实际加载由 `GlobalAudioPlayer` 监听 `OnChange` → `audioPlayer.loadSource` / `syncWebVideoState`。

### Hybrid
- `Attach`：先 `NativeAudio.PlayAsync(companionAudioUrl)`，再 `RequestWebStateSync`（Web 视频静音）。
- `PlayAsync`：原生 Resume + `PlayWebPlaybackAsync(videoOnly: true)` 同步。

## 8. UI 层协作

### GlobalAudioPlayer
- 持久 `<audio>` / `<video>`，路由切换不销毁。
- `ActivePlaybackKind` 为 `NativeAudio` / `NativeVideo` 时**不**走 Web 同步（原生自管进度）。
- 原生进度：`OnTimeChanged` → `audioPlayer.setProgress`（播放器页 JS 进度条）。
- Web 停止：`OnRequestStopWebPlayback` → `audioPlayer.stopWebPlayback` + `CompleteWebStop()`。

### PlayerAudio / PlayerVideo
- 进度条：**禁止** MudSlider 绑 `CurrentTime`；用 `audioPlayer.mountProgressBar`。
- 在线视频：`PlayVideoAsync` → 立即确认弹窗 → 确认后 loading → `PlayRemoteVideoForCurrentAsync`。
- Android 原生全屏：`UseNativeVideoPlayback` 时不导航 `/player/video`。

### 代理
- `LocalAudioProxy`：YouTube 直链 → 本地 HTTP（绕 CORS）。
- `LocalFileProxy`：本地文件 → HTTP；URL 带 `&f=路径` 防同端口误判切歌。

## 9. 播放设置（`UiPreferencesService` / Theme 侧边栏）

| 键 | 默认 | 说明 |
|----|------|------|
| `playback.prefetchRemoteVideo` | `false` | 播在线音频时后台预解析视频流 |
| `playback.remoteVideoStreamQuality` | `Lowest` | 分离流 video-only：最低 / 最高 |

还原默认会重置上述两项。

## 10. 诊断

- 日志标签：`[YTMusic:Playback]`（`PlaybackDiagnostics.Log` / `LogError`）。
- 关键关键字：`PlaybackSwitcher active=`、`ResolveStreams`、`PlayRemoteVideo`、`ExoPlayerStreamSource`、`PrefetchRemoteVideo`。

## 11. 修改时勿回归

- [ ] 切换曲目/音轨时无双音轨（必须先 `Detach` 旧 `IPlaybackInstance`）。
- [ ] Android 在线视频走 `NativeVideo`，不回到 WebView 主路径。
- [ ] muxed 仍优先于分离流合并。
- [ ] 本地 webm 不当视频；仅 `.mp4` + `IsVideo` 走视频。
- [ ] 全屏视频 `autoPlay` 从 Intent 传到 ExoPlayer，不在 Attach 后再 `PauseAsync`。
- [ ] 全屏播放期间屏幕常亮。
- [ ] 播放器进度条保持 JS 托管，不用 Blazor 高频绑 `CurrentTime`。
- [ ] Android 已下载切歌：`Stop` + `ClearMediaItems`；Web 代理 URL 含 `&f=` 区分文件。

## 12. 相关决策索引

- 方案 B 串行切换、Android 在线 `MergingMediaSource`：见 `decisionLog.md` 2026-06-11 条目。
- 进度条 JS 托管：见 `decisionLog.md` 2026-06-07。
- Android 原生视频主路径：见 `decisionLog.md` 2026-03-25。
