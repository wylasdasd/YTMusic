# 项目框架思路 (Project Architecture & Design)

YTMusic 采用 **.NET MAUI Blazor Hybrid** 架构，结合原生系统能力与 Web UI 的迭代效率。业务与数据已拆分为 **BLL / DAL** 类库，UI 层只保留播放管线和平台壳层。

## 1. 分层架构

```
┌─────────────────────────────────────────────────────────┐
│  YTMusic（UI）                                           │
│  Components / ViewModels / Adapters / Services(播放+UI) │
│  AppGlobal.cs                                            │
└───────────────┬─────────────────────┬───────────────────┘
                │ I*Service / Ports   │ AddYTMusicDal() 仅 DI
                ▼                     ▼
┌───────────────────────────┐  ┌──────────────────────────┐
│  YTMusic.BLL              │  │  YTMusic.DAL             │
│  Services / Models / Ports│◄─│  Repositories / SQLite   │
│  AppGlobal.cs             │  │  Infrastructure          │
└───────────────┬───────────┘  └──────────────────────────┘
                │
                ▼
         CommonHelp（通用工具）
```

| 层级 | 项目 | 职责 |
|------|------|------|
| UI | `YTMusic` | MudBlazor 页面、ViewModel、播放状态机、MAUI 适配器、主题/窗口 |
| BLL | `YTMusic.BLL` | 收藏、下载、YouTube、AList、网络错误等业务编排 |
| DAL | `YTMusic.DAL` | SQLite + Dapper 仓储实现 |
| 共享 | `CommonHelp` | 文件、JSON、字符串、时间等无业务工具 |

**依赖规则：** UI → BLL（接口）；DAL → BLL（仓储接口）；BLL 不引用 DAL；UI 除 `MauiProgram` 注册外不直接引用 DAL 类型。

## 2. 核心架构模式（UI 内）

项目遵循 **MVVM** 与 **依赖注入**：

| 层级 | 位置 | 职责 |
|------|------|------|
| View | `Components/**/*.razor` | MudBlazor UI，响应式布局 |
| ViewModel | `ViewModels/*VM.cs` | 页面状态、命令、与 BLL `I*Service` 交互 |
| UI Services | `Services/` | 播放管线、UI 偏好、全局 Loading、窗口壳层 |
| Adapters | `Adapters/` | 实现 BLL `Ports`（数据库路径、Preferences、对话框等） |
| UI Infrastructure | `Infrastructure/` | `Proxies/`（HTTP 代理）、`Storage/`（平台存储路径） |
| Platform | `Platforms/` | Android ExoPlayer、Windows 窗口壳层、iOS 原生音频等 |

页面尽量轻量；播放队列、当前曲目、平台分流等全局状态集中在 `MusicPlayerService`。

## 3. 播放架构（方案 B）

播放是项目最复杂的子系统，保留在 UI 层（不迁入 BLL）：

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

## 4. 服务层职责拆分

### BLL（`YTMusic.BLL/Services/`）

| 接口 | 实现 | 职责 |
|------|------|------|
| `IYouTubeService` | `YouTubeService` | YoutubeExplode 搜索、流解析、下载 |
| `IFavoriteService` | `FavoriteService` | 收藏夹与文件夹 |
| `ILocalMusicService` | `LocalMusicService` | 本地下载记录 |
| `IDownloadManagerService` | `DownloadManagerService` | 下载任务队列与进度 |
| `IUploadManagerService` | `UploadManagerService` | 本地上传 AList 任务 |
| `IAListUploadService` | `AListUploadService` | AList HTTP 上传与元数据 |
| `IAListRemoteDownloadManagerService` | `AListRemoteDownloadManagerService` | 远端目录下载 |
| `IAListUploadSettingsService` | `AListUploadSettingsService` | AList 连接设置（Preferences） |
| `INetworkErrorService` | `NetworkErrorService` | 播放/搜索失败时的 VPN 提示 |

### BLL Infrastructure（`YTMusic.BLL/Infrastructure/`）

| 组件 | 职责 |
|------|------|
| `YoutubeExplodeClient` | 实现 `IYouTubeApiClient`（搜索、流清单、下载） |
| `AListFsApiClient` | AList 上传/下载 HTTP 传输 |
| `AListHttpClients` / `AListApiHelpers` / `AListFileHelpers` | AList 共享客户端、响应解析、临时文件 |
| `LocalFileSystem` | 实现 `IFileSystem` |

### UI（`YTMusic/Services/`）

| 服务 | 职责 |
|------|------|
| `MusicPlayerService` | 播放状态机、队列、历史、`IPlaybackHost` |
| `UiPreferencesService` | 主题、播放设置、标题展示偏好 |
| `GlobalStateService` | 全局 Loading 遮罩 |
| `WindowChromeService` | Windows 窗口拖拽与系统按钮 |
| `AppResetService` | 还原默认设置 |
| `StoragePaths` | 本地下载目录（`Infrastructure/Storage/`，常量见 `AppGlobal.Storage`） |

### DAL（`YTMusic.DAL/Repositories/`）

| 接口（BLL） | 实现（DAL） | 职责 |
|-------------|-------------|------|
| `IFavoriteRepository` | `FavoriteRepository` | 收藏夹、文件夹、曲目 CRUD |
| `IDownloadedTrackRepository` | `DownloadedTrackRepository` | 下载记录 CRUD |

原生播放按平台注入（`MauiProgram.cs`）：

- **Android**：`AndroidNativeAudioPlaybackService` + `AndroidNativeVideoPlaybackService`
- **iOS**：`IosNativeAudioPlaybackService` + `NullNativeVideoPlaybackService`
- **其他**：`NullNative*` → Web 播放 + 本地 HTTP 代理

## 5. AppGlobal

| 层 | 文件 | 用途 |
|----|------|------|
| BLL | `YTMusic.BLL/AppGlobal.cs` | 数据库名、传输阈值、AList/网络常量、运行时冷却 |
| UI | `YTMusic/AppGlobal.cs` | 存储目录、UI 偏好键、播放日志前缀、`Runtime.Services` |

## 6. UI 与原生交互

- **Blazor WebView**：UI 在 MAUI WebView 内渲染。
- **JS Interop**：`audioPlayer.js` 控制媒体元素；`ytmLayout.js` 处理底栏、滚动缓存、Tab 触摸滑动；`mouseInterop.js` 配合 Windows 拖拽。
- **本地代理**：`Infrastructure/Proxies/LocalAudioProxy`、`LocalFileProxy`（`HttpListener`）为 WebView 提供可访问的 HTTP 地址，绕过 CORS 与本地文件限制。
- **Windows 窗口**：`Platforms/Windows/MainWindow.xaml` + `MauiProgram` 生命周期配置 + `MainLayout` 顶栏按钮。

## 7. 数据持久化

- **SQLite + Dapper（DAL）**：收藏夹、下载记录；业务经 BLL `I*Service` 访问，UI 不直连。
- **Preferences**：主题、AList 设置、播放偏好（`UiPreferencesService`、`AListUploadSettingsService`；键名集中在各层 `AppGlobal`）。
- **播放历史**：当前为 `MusicPlayerService` 运行期内存列表，尚未落库。

## 8. 项目结构速览

```
YTMusic/                 # MAUI Blazor 主应用（UI）
  Components/            Layout、Pages、Dialogs
  ViewModels/            *VM.cs
  Adapters/              BLL Ports 的 MAUI 实现
  Infrastructure/        Proxies/、Storage/（UI 技术实现）
  Services/              播放 + UI 壳层
  AppGlobal.cs
YTMusic.BLL/             # 业务逻辑
  Abstractions/          I*Service、I*Repository
  Services/              业务实现
  Models/、Ports/
  Infrastructure/        YouTube/、AList/、FileSystem/
  AppGlobal.cs
YTMusic.DAL/             # 数据访问
  Repositories/
  Infrastructure/
CommonHelp/              共享工具库
YTMusic.Tests/           单元测试
memory-bank/             决策、进度、播放架构
```

## 延伸阅读

- [`../AGENTS.md`](../AGENTS.md) — 各库职责、依赖约束、开发约定
- [`CORE_LOGIC.md`](CORE_LOGIC.md) — 核心难点与解决思路
- [`PROJECT_ANALYSIS.md`](PROJECT_ANALYSIS.md) — 代码结构与维护风险分析
- [`../memory-bank/playbackArchitecture.md`](../memory-bank/playbackArchitecture.md) — 播放管线详细设计
