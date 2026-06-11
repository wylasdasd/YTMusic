# 项目框架思路 (Project Architecture & Design)

YTMusic 采用 **.NET MAUI Blazor Hybrid** 架构，结合原生系统能力与 Web UI 的迭代效率。

## 1. 核心架构模式

项目遵循 **MVVM** 与 **依赖注入**：

| 层级 | 位置 | 职责 |
|------|------|------|
| View | `Components/**/*.razor` | MudBlazor UI，响应式布局 |
| ViewModel | `Components/Pages/*VM.cs` | 页面状态、命令、与服务交互 |
| Services | `Services/` | 业务逻辑、持久化、播放状态机 |
| Platform | `Platforms/` | Android ExoPlayer、Windows 窗口壳层、iOS 原生音频等 |

页面尽量轻量；播放队列、当前曲目、平台分流等全局状态集中在 `MusicPlayerService`。

## 2. 播放架构（方案 B）

播放是项目最复杂的子系统，已从单一巨型方法拆出管线层：

```
UI (PlayerAudio / PlayerVideo / GlobalAudioPlayer)
        ↓
MusicPlayerService (IPlaybackHost — 唯一状态源)
        ↓ ActivatePlaybackAsync
PlaybackSwitcher (SemaphoreSlim 串行)
        ↓ Detach 旧实例 → Attach 新实例
IPlaybackInstance × 5
        ↓
NativeAudio | NativeVideo | WebAudio | WebMuxedVideo | Hybrid
```

- **`PlaybackSwitcher`**：保证任意时刻只有一条活跃管线，避免双音轨。
- **`IPlaybackHost`**：宿主回调（代理配置、Web 同步、原生服务访问）。
- **`GlobalAudioPlayer.razor`**：跨页持久 `<audio>` / `<video>`，Web/Hybrid 同步入口。

完整路由表、平台差异与勿回归清单见 [`../memory-bank/playbackArchitecture.md`](../memory-bank/playbackArchitecture.md)。

## 3. 服务层职责拆分

| 服务 | 职责 |
|------|------|
| `IYouTubeService` / `YouTubeService` | YoutubeExplode 搜索、流解析、下载 |
| `MusicPlayerService` | 播放状态机、队列、历史、`IPlaybackHost` |
| `IDownloadManagerService` | 下载任务队列与进度 |
| `ILocalMusicService` | 本地下载记录（SQLite） |
| `IFavoriteService` | 收藏夹与文件夹（SQLite） |
| `AListUploadService` / `UploadManagerService` / `IAListRemoteDownloadManagerService` | AList 上传与远端目录下载 |
| `AListUploadSettingsService` | AList 连接设置（Preferences） |
| `UiPreferencesService` | 主题、播放设置、标题展示偏好 |
| `GlobalStateService` | 全局 Loading 遮罩 |
| `NetworkErrorService` | 播放/搜索失败时的 VPN 提示 |
| `WindowChromeService` | Windows 窗口拖拽与系统按钮 |
| `AppResetService` | 还原默认设置 |

原生播放按平台注入（`MauiProgram.cs`）：

- **Android**：`AndroidNativeAudioPlaybackService` + `AndroidNativeVideoPlaybackService`
- **iOS**：`IosNativeAudioPlaybackService` + `NullNativeVideoPlaybackService`
- **其他**：`NullNative*` → Web 播放 + 本地 HTTP 代理

## 4. UI 与原生交互

- **Blazor WebView**：UI 在 MAUI WebView 内渲染。
- **JS Interop**：`audioPlayer.js` 控制媒体元素；`ytmLayout.js` 处理底栏、滚动缓存、Tab 触摸滑动；`mouseInterop.js` 配合 Windows 拖拽。
- **本地代理**：`LocalAudioProxy` / `LocalFileProxy`（`HttpListener`）为 WebView 提供可访问的 HTTP 地址，绕过 CORS 与本地文件限制。
- **Windows 窗口**：`Platforms/Windows/MainWindow.xaml` + `MauiProgram` 生命周期配置 + `MainLayout` 顶栏按钮。

## 5. 数据持久化

- **SQLite + Dapper**：收藏夹、文件夹、下载记录（`FavoriteService`、`LocalMusicService`）。
- **Preferences**：主题、AList 设置、播放偏好（`UiPreferencesService`、`AListUploadSettingsService`）。
- **播放历史**：当前为 `MusicPlayerService` 运行期内存列表，尚未落库。

## 6. 项目结构速览

```
YTMusic/
  Components/Layout/     MainLayout, GlobalAudioPlayer, PageListScroll
  Components/Pages/      页面 + *VM.cs
  Components/Dialogs/    确认/收藏/重置等弹窗
  Services/              业务服务
  Services/Abstractions/ 接口（含 Playback/）
  Services/Playback/     PlaybackSwitcher, PlaybackInstances
  Platforms/             平台特定代码
  wwwroot/js/            audioPlayer.js, ytmLayout.js
CommonHelp/              共享工具库
YTMusic.Tests/           单元测试（含 YoutubeExplode 联网测试）
memory-bank/             决策、进度、播放架构
```

## 延伸阅读

- [`CORE_LOGIC.md`](CORE_LOGIC.md) — 核心难点与解决思路
- [`PROJECT_ANALYSIS.md`](PROJECT_ANALYSIS.md) — 代码结构与维护风险分析
- [`../memory-bank/playbackArchitecture.md`](../memory-bank/playbackArchitecture.md) — 播放管线详细设计
