# 项目分析

> 与代码对齐：2026-07-05

## 整体判断

这是一个以 `MusicPlayerService` 为核心状态机的 `.NET MAUI Blazor Hybrid` 项目。UI 使用 Razor + MudBlazor；业务逻辑已拆至 `YTMusic.BLL`，SQLite 访问在 `YTMusic.DAL`。播放能力通过 **方案 B**（`PlaybackSwitcher` + 五种 `IPlaybackInstance`）保留在 UI 层。

结构为「Blazor 页面 + `ViewModels/` + BLL 服务 + UI 播放单例」，分层后业务可测性更好，播放器服务仍偏重。

## 仓库结构

| 目录 / 项目 | 说明 |
|-------------|------|
| `YTMusic/Components/` | UI、布局、页面、弹窗 |
| `YTMusic/ViewModels/` | 页面 VM（`*VM.cs`），注入 BLL `I*Service` |
| `YTMusic/Adapters/` | BLL `Ports` 的 MAUI 实现 |
| `YTMusic/Services/` | 播放管线 + UI 壳层（非业务编排） |
| `YTMusic/Services/Abstractions/Playback/` | `IPlaybackHost`、`IPlaybackInstance` |
| `YTMusic/Services/Playback/` | `PlaybackSwitcher`、`PlaybackInstances` |
| `YTMusic/AppGlobal.cs` | UI 全局常量与 `Runtime.Services` |
| `YTMusic.BLL/` | 业务服务、模型、仓储接口、Ports |
| `YTMusic.DAL/` | SQLite 仓储实现与迁移 |
| `CommonHelp/` | 文件、字符串、网络、时间等工具 |
| `memory-bank/` | 决策、进度、播放架构 |

各库依赖约束见 [`../AGENTS.md`](../AGENTS.md)。

## 依赖注入与运行入口

入口：`MauiProgram.cs`（`AddYTMusicDal()` → `AddYTMusicBll()` → UI 服务）。

**BLL Singleton（`AddYTMusicBll`）：**
- `INetworkErrorService`、`IYouTubeService`、`IFavoriteService`、`ILocalMusicService`
- `IDownloadManagerService`、`IUploadManagerService`
- `IAListUploadService`、`IAListUploadSettingsService`、`IAListRemoteDownloadManagerService`

**DAL Singleton（`AddYTMusicDal`）：**
- `IFavoriteRepository`、`IDownloadedTrackRepository`

**UI Singleton：**
- `MusicPlayerService`、`UiPreferencesService`、`GlobalStateService`、`WindowChromeService`、`AppResetService`
- Ports 适配：`IPreferencesStore`、`IDatabasePathProvider`、`IDownloadMusicDirectoryProvider`

**Scoped VM：**
- `SearchVM`、`DownloadsVM`、`TransfersVM`、`FavoritesVM`、`FavoritesFolderVM`、`UploadVM`

**原生播放（按平台）：**
- Android：原生音频 + 原生视频
- iOS：原生音频 + 空视频实现
- 其他：`NullNative*` → Web + 代理

## UI 与布局

`MainLayout.razor` 承担：

- 顶栏品牌、三横杠菜单、Windows 窗口控制
- 右侧主题抽屉（5 套主题 + 播放/显示设置）
- 固定底部导航：**Favorites → Player → Home → Other**
- `GlobalAudioPlayer` 全局挂载
- `ytmLayout.js` 初始化（底栏键盘、`visualViewport`、滚动位置缓存）

`Other` 页承接：本地资源、传输任务、AList 上传、播放历史。

稳定性要点：主题索引边界校验、`ActiveTheme` 安全访问、小屏顶栏不越界。

## 搜索与发现

`Search` 页即 Home 初始态（无多余标题/引导）。

`SearchVM`：搜索、分页、`LoadNextPageAsync`、批量收藏状态、下载入队、收藏对话框。

主链路：输入 → `SearchAsync` → 分页结果 → `MusicPlayerService.PlayAsync` → 导航播放器。

## 播放器架构

### 状态机与管线

`MusicPlayerService` 仍负责：

- 当前曲目、流 URL、播放/暂停、Seek
- 播放列表、模式（顺序/随机/单曲循环）、运行期历史
- 远程流解析、`PlayInternalAsync` 路由决策
- 实现 `IPlaybackHost`（代理、Web 同步、原生访问）

**已拆出的管线层：**

```
MusicPlayerService (IPlaybackHost)
    → PlaybackSwitcher (SemaphoreSlim)
    → NativeAudio | NativeVideo | WebAudio | WebMuxedVideo | Hybrid
```

| 平台 | 音频 | 视频 |
|------|------|------|
| Android | ExoPlayer 前台服务 | ExoPlayer 全屏 Activity |
| iOS | 原生音频服务 | Web 兜底 |
| Windows | Web + `LocalAudioProxy` | Web `<video>` / Hybrid |

### Web 桥接

`GlobalAudioPlayer.razor`：

- 持久 `<audio>` / `<video>`
- `NativeAudio` / `NativeVideo` 时不走 Web 同步（原生自管进度）
- 原生进度：`OnTimeChanged` → `audioPlayer.setProgress`
- Web 停止：`OnRequestStopWebPlayback` → `stopWebPlayback`

### 进度条

`audioPlayer.js` 的 `mountProgressBar`（`ytm-player-progress`）直接更新 DOM；`.NET` 侧 `OnTimeUpdate` 约 500ms 节流，**不**用于刷新进度条 UI。

### 代理

- `LocalAudioProxy`：远程 YouTube 流 → 本地 HTTP
- `LocalFileProxy`：本地文件 → HTTP，URL 含 `&f=` 区分文件

## 下载、收藏与本地库

### YouTubeService（BLL）

搜索、流 URL、下载；Android 下部分调用 `Task.Run` 规避主线程网络限制。

### DownloadManagerService（BLL）

任务队列、去重、进度、完成后经 `ILocalMusicService` 写库、回填收藏本地路径。

### FavoriteService（BLL）

经 `IFavoriteRepository` 访问 SQLite：默认/自定义收藏夹、批量查询、本地路径关联。

### LocalMusicService（BLL）

经 `IDownloadedTrackRepository` 维护 `YTMusicDownloads.db3`；孤儿清理、删文件同步删记录。

收藏与下载为**双库**，一致性靠业务层主动维护（如删除、下载完成回填）。

## 页面职责

| 页面 | 职责 |
|------|------|
| `Search` | 搜索与发现 |
| `Player` / `PlayerAudio` / `PlayerVideo` | 播放器（按类型路由） |
| `Favorites` / `FavoritesFolder` | 收藏夹与文件夹内播放 |
| `Downloads` | 已下载媒体 |
| `Transfers` | 下载/上传任务 |
| `Upload` | AList 配置与上传 |
| `History` | 播放历史 |
| `Other` | 次级入口聚合 |

## 优点

- 跨端播放策略务实（原生 + Web + 代理 + 方案 B 串行切换）
- 搜索、播放、下载、收藏、AList 主链路完整
- Android 后台、主题越界、底栏键盘、切歌同步等真实问题有针对性处理
- 文档与 `memory-bank` 决策记录较完整

## 风险与维护成本

### 1. MusicPlayerService 仍偏大

管线已拆到 `PlaybackSwitcher`，但队列、解析、历史、代理、路由决策仍在同一服务。后续功能（断点续播、缓存、歌词）会继续增加复杂度。

### 2. 双库一致性

收藏与下载分表，删除/覆盖需同步多处；AList 重复下载允许覆盖，需留意路径一致性。

### 3. Hybrid 与 Windows

分离流在线视频在非 Android 平台走 `Hybrid`（Web 静音视频 + `INativeAudio` 播 companion 音频）。Windows 注入 `NullNativeAudioPlaybackService`，该路径下原生音频为 no-op；实际依赖 muxed 优先或后续改进。

### 4. VM 组织

ViewModel 已统一至 `ViewModels/`；`Search` 的 partial 与 `SearchVM` 同文件，与「一组件一 VM 文件」约定略有偏差。

### 5. 测试依赖网络

`YouTubeServiceTests` 直连 YouTube，适合联调而非稳定 CI。

## 建议演进方向

1. 继续拆分 `MusicPlayerService`：队列、流解析、历史持久化独立服务。
2. 补充离线单元测试：播放列表切歌、下载去重、收藏筛选。
3. 评估 Windows 分离流策略（Web 双轨或强制 muxed）。
4. `PlaybackHistory` SQLite 持久化。

## 结论

项目已跑通真实用户主流程，不是 demo。价值在于完整产品链路、跨端播放的工程化处理，以及可继续演进的服务层结构。优先投入：**播放器服务进一步解耦**、**离线测试**、**播放历史落库**。

## 相关文档

- [`../AGENTS.md`](../AGENTS.md)
- [`ARCHITECTURE.md`](ARCHITECTURE.md)
- [`CORE_LOGIC.md`](CORE_LOGIC.md)
- [`../memory-bank/playbackArchitecture.md`](../memory-bank/playbackArchitecture.md)
- [`../memory-bank/progress.md`](../memory-bank/progress.md)
