# YTMusic：Agent 约定（当前代码基线）

> 最后与代码对齐：2026-07-05

## 解决方案分层

```
YTMusic（UI / MAUI Blazor）
  ├── 引用 YTMusic.BLL、YTMusic.DAL（仅 DI 注册）、CommonHelp
  ├── Components/（Pages、Layout：`.razor` + 同目录 `*VM.cs` + `.razor.css`）、ViewModels.Shared/、Adapters/、Infrastructure/、Services/（播放 + UI 壳层）
  └── AppGlobal.cs

YTMusic.BLL（业务逻辑层）
  ├── 引用 CommonHelp
  ├── Abstractions/、Services/、Models/、Ports/、Infrastructure/
  └── AppGlobal.cs

YTMusic.DAL（数据访问层）
  ├── 引用 YTMusic.BLL、CommonHelp
  └── Repositories/、Infrastructure/

CommonHelp（共享工具库，无业务语义）
YTMusic.Tests（单元测试）
memory-bank/（决策、进度、播放架构文档）
```

**依赖方向（必须遵守）：**

| 调用方 | 允许引用 | 禁止 |
|--------|----------|------|
| `YTMusic`（UI） | `YTMusic.BLL` 接口与模型；`MauiProgram` 中注册 DAL | 页面/VM 直接 `using YTMusic.DAL`；UI 直接访问 SQLite / Repository |
| `YTMusic.BLL` | `CommonHelp`；自身 `Abstractions` / `Ports` | MAUI、Blazor、SQLite、DAL 实现类 |
| `YTMusic.DAL` | `YTMusic.BLL.Abstractions.Data`（仓储接口）、`AppGlobal` | MAUI、Blazor、业务编排逻辑 |
| `CommonHelp` | 无项目引用 | 引用 BLL/DAL/YTMusic |

---

## 各项目职责与约束

### `YTMusic`（UI / MAUI）

| 项 | 要求 |
|----|------|
| TFM | `net10.0-android` / `ios` / `maccatalyst` / `windows10.0.19041.0` |
| 职责 | Blazor 页面、ViewModel、平台适配器、播放管线、UI 偏好与窗口壳层 |
| 全局入口 | `AppGlobal.cs`：存储路径、UI 偏好键、播放诊断标识、`Runtime.Services` |
| ViewLogic（*VM.cs） | 与对应 `.razor` 同目录（`Components/Pages/` 或 `Components/Layout/`）；**共享** VM 放 `ViewModels.Shared/`（如 `PlayerVM`、`ViewModelBase`、`ThemePresets`）；继承 `ViewModelBase`（`ObservableObject` + Blazor `StateHasChanged` 桥接）；命令用 **CommunityToolkit.Mvvm** `[RelayCommand]` / `[ObservableProperty]` |
| 适配器 | `Adapters/` 实现 BLL `Ports/`（`MauiDatabasePathProvider`、`MudUiNotifier` 等） |
| 基础设施 | `Infrastructure/`：播放/平台相关技术实现（非独立类库）；`Proxies/`（`LocalAudioProxy`、`LocalFileProxy`）、`Storage/`（`StoragePaths`） |
| 保留在 UI 的 `Services/` | **仅**播放与 UI 壳层：`MusicPlayerService`、`Playback/*`、`UiPreferencesService`、`GlobalStateService`、`WindowChromeService`、`AppResetService`、原生播放 `INative*` |
| 播放接口 | 仍在 `Services/Abstractions/Playback/`（`IPlaybackHost`、`IPlaybackInstance`），**不**迁入 BLL |
| DI 注册 | `MauiProgram.cs`：先 `AddYTMusicDal()`，再 `AddYTMusicBll()`，再注册 UI 单例与 VM |

**`Adapters` vs `Infrastructure`（UI）：** `Adapters` 实现 BLL 定义的 `Ports` 契约；`Infrastructure` 放 UI/播放专用技术实现（如 `HttpListener` 代理、平台存储路径），无需跨层接口时可直用。

**禁止：** 在 Razor / VM 中新增业务服务实现；绕过 BLL 接口直接操作数据库或 Repository。

### `YTMusic.BLL`（业务逻辑层）

| 项 | 要求 |
|----|------|
| TFM | `net10.0` 类库 |
| 职责 | 业务规则、任务队列、YouTube/AList 编排；通过仓储接口访问数据 |
| 全局入口 | `AppGlobal.cs`：数据库文件名、传输阈值、AList/网络常量、`Runtime` 冷却状态 |
| 接口 | `Abstractions/`：`I*Service`；`Abstractions/Data/`：`I*Repository` |
| 实现 | `Services/`：`FavoriteService`、`LocalMusicService`、`YouTubeService`、下载/上传/AList 管理等 |
| 基础设施 | `Infrastructure/`：外部技术实现（非独立类库）；`YouTube/`（`YoutubeExplodeClient`）、`AList/`（HTTP 传输与解析）、`FileSystem/`（`LocalFileSystem`） |
| 模型 | `Models/`：收藏、下载、AList 等 DTO |
| 平台抽象 | `Ports/`：`IDatabasePathProvider`、`IPreferencesStore`、`IUiNotifier`、`IFileSystem`、`IYouTubeApiClient` 等（MAUI 相关由 UI `Adapters/` 实现） |
| NuGet | `YoutubeExplode`、`Microsoft.Extensions.DependencyInjection.Abstractions` |
| DI | `BllServiceCollectionExtensions.AddYTMusicBll()`（含 `IYouTubeApiClient`、`IFileSystem`、`AListFsApiClient` 注册） |

**`Services` vs `Infrastructure`（BLL）：** `Services` 负责业务编排；`Infrastructure` 实现 `Ports` 中的技术契约（YoutubeExplode、AList HTTP、本地文件系统），或承载 AList 内部 HTTP/解析工具类。

**禁止：** 引用 MAUI / Blazor / SQLite / `YTMusic.DAL`；在 BLL 内直接 `new` Repository 或写 SQL。

### `YTMusic.DAL`（数据访问层）

| 项 | 要求 |
|----|------|
| TFM | `net10.0` 类库 |
| 职责 | SQLite 连接、表结构迁移、Dapper 读写 |
| 实现 | `Repositories/`：`FavoriteRepository`、`DownloadedTrackRepository` |
| 基础设施 | `Infrastructure/`：`SqliteConnectionFactory`、`SqliteBootstrap`、`SqliteSchemaMigration` |
| 接口归属 | 仓储接口定义在 **BLL** `Abstractions/Data/`，DAL 只提供实现 |
| NuGet | `Dapper`、`Microsoft.Data.Sqlite`、`SQLitePCLRaw.bundle_e_sqlite3`（当前 3.0.3） |
| DI | `DalServiceCollectionExtensions.AddYTMusicDal()` |

**禁止：** 业务判断、MAUI API、直接供 UI 调用；新增表/列须走 `SqliteSchemaMigration.EnsureColumn()`（`PRAGMA table_info` 防重复 `ALTER`）。

### `CommonHelp`（共享工具）

| 项 | 要求 |
|----|------|
| TFM | `net10.0` 类库 |
| 职责 | 与业务无关的通用工具：文件、字符串、JSON、时间、网络请求等 |
| 引用 | 不引用 YTMusic / BLL / DAL |
| 设计时 | **不要**在仓库根放 `Directory.Build.props`（会污染 CommonHelp 的 TFM 还原） |

### `YTMusic.Tests`

| 项 | 要求 |
|----|------|
| 职责 | 单元测试；可引用 BLL / CommonHelp |
| 约定 | 优先补充不依赖外网的测试；YoutubeExplode 联网测试需明确标注 |

---

## AppGlobal 约定

两层各有一个 `AppGlobal` 静态类，集中**常量**与**进程内可变状态**；新增魔法值优先写入对应层，并补 `///` XML 注释。

| 层 | 文件 | 典型内容 |
|----|------|----------|
| BLL | `YTMusic.BLL/AppGlobal.cs` | `Database`、`Favorites`、`Transfers`、`AList`、`Network`、`Runtime` |
| UI | `YTMusic/AppGlobal.cs` | `Storage`、`Ui.PreferenceKeys`、`Playback`、`UploadPage`、`Runtime.Services` |

- BLL / DAL 项目已配置 `<Using Include="YTMusic.BLL" />`；UI 已配置 `<Using Include="YTMusic" />`。
- `AppGlobal.Runtime.Services` 由 `MauiProgram` 注入，供 Android 前台服务等无构造注入场景解析 `MusicPlayerService`。

---

## 仓库结构（UI 子目录）

- `Components/Layout/`：全局布局（`MainLayout.razor`、`GlobalAudioPlayer.razor`）
- `Components/Pages/`：页面（`Search.razor` + `SearchVM.cs` + `Search.razor.css` 等同目录）
- `Components/Layout/`：全局布局（`MainLayout.razor` + `MainLayoutVM.cs`、`GlobalAudioPlayer.razor`）
- `Components/Dialogs/`：弹窗
- `ViewModels.Shared/`：跨页共享 ViewLogic（`PlayerVM`、`ViewModelBase`、`ThemePresets`）
- `Adapters/`：BLL `Ports` 的 MAUI/MudBlazor 实现
- `Infrastructure/`：UI 层技术实现（`Proxies/` 本地 HTTP 代理、`Storage/` 平台路径）
- `Services/`：播放管线 + UI 壳层（见上文「保留在 UI 的 Services」）
- `Services/Abstractions/Playback/`：`IPlaybackInstance`、`IPlaybackHost`
- `Services/Playback/`：`PlaybackSwitcher`、`PlaybackInstances`、`PlaybackProxyCoordinator`、`PlaybackStreamResolver`、`PlaybackItemModels`
- `Platforms/Android/Services/`：ExoPlayer 前台服务、全屏视频 Activity
- `wwwroot/js/`：`ytmLayout.js`、`audioPlayer.js`、`mouseInterop.js`

---

## 构建与验证

- Windows 快速构建：`dotnet build YTMusic/YTMusic.csproj -c Debug -f net10.0-windows10.0.19041.0`
- 若可执行文件被占用（VS 正在调试），使用临时输出验证：
  - `dotnet build YTMusic/YTMusic.csproj -c Debug -f net10.0-windows10.0.19041.0 -o YTMusic/bin/Debug/net10.0-windows10.0.19041.0/win-x64-temp`
- 设计时 TFM 限制仅在 `YTMusic/Directory.Build.props`；**不要**在仓库根放 `Directory.Build.props`。

---

## 通用开发约定

- 命名空间与目录一致：页面 VM → `YTMusic.Components.Pages`；布局 VM → `YTMusic.Components.Layout`；共享 → `YTMusic.ViewModels.Shared`。
- 禁止跨目录放置「一对一」页面 VM（`History.razor` 与 `HistoryVM.cs` 必须同在 `Components/Pages/`）。
- Razor 组件命名空间与 `YTMusic/Components/**` 对齐（例如 `YTMusic.Components.Pages`）。
- 页面通过 DI 注入 BLL 的 `I*Service` 接口，不注入具体 Repository。
- 优先复用 `CommonHelp` 现有能力，避免新增零散工具函数。
- 避免无关格式化，尽量保持原有代码风格与换行。
- 播放相关日志使用 `AppGlobal.Playback.LogPrefix`（`PlaybackDiagnostics`，与 `audioPlayer.js` 一致）。

---

## 播放架构（改代码必读）

- 状态源：`MusicPlayerService` 实现 `IPlaybackHost`；切换走 `PlaybackSwitcher.SwitchAsync`（`SemaphoreSlim` 串行）。
- 流解析与本地代理：`PlaybackStreamResolver`（`IYouTubeApiClient`）、`PlaybackProxyCoordinator`；`MusicPlayerService` 不再内嵌 `YoutubeClient` 或代理实现。
- 播放历史：`IPlaybackHistoryService`（BLL）+ `PlaybackHistoryRepository`（DAL，`PlaybackHistory` 表）；`MusicPlayerService` 仅调用 `RecordPlayAsync`，History 页走 `HistoryVM`。
- 五种 `PlaybackKind`：`NativeAudio`、`NativeVideo`、`WebAudio`、`WebMuxedVideo`、`Hybrid`。
- 任意时刻仅一条活跃管线；切歌 / 切模式必须先 `Detach` 旧 `IPlaybackInstance`。
- Android 在线视频走 `NativeVideo`（ExoPlayer 全屏），**不要**回退到 WebView 主路径。
- 播放器页进度条由 `audioPlayer.mountProgressBar` 托管，**禁止** MudSlider 绑 `CurrentTime`。
- 完整路由表与勿回归清单：[`memory-bank/playbackArchitecture.md`](memory-bank/playbackArchitecture.md)。

---

## UI/布局强约束（按当前实现）

- 顶部栏：`MainLayout` 中保留品牌、搜索入口、三横杠菜单按钮。
- 主题切换：
  - 点击三横杠打开右侧主题侧边栏（不是直接切换）。
  - 主题由侧边栏列表选择，当前内置 5 套（含 2 套亮色）。
  - 主题逻辑在 `MainLayoutVM` + `ViewModels.Shared/ThemePresets.cs`。
  - 侧边栏还包含：分离流视频画质、视频后台预检、两行显示标题、`Favorites Image` 等（`UiPreferencesService`，键名见 `AppGlobal.Ui.PreferenceKeys`）。
- 底部导航：
  - 顺序：Favorites → Player → Home → Other；全端固定在最底部（`position: fixed; bottom: 0`），不能回退到侧边 Rail。
  - 需要兼容输入法弹出场景：依赖 `wwwroot/js/ytmLayout.js` 的 `visualViewport` 修正。
- 移动端安全区：
  - Web 样式使用 `env(safe-area-inset-*)`。
  - 原生层在 `MainPage.xaml.cs` 已补充 iOS/Android 顶部安全区处理，修改时不要破坏。

---

## Home/Search 页约定

- Home（Search 页初始态）不显示页面标题与引导提示块。
- 搜索框回车搜索需保证“所见即所得”：
  - `Immediate="true"` + `OnKeyUp` 触发。
- 搜索页样式主要由 `Search.razor.css` 管理，颜色优先跟随布局定义的 CSS 变量。

---

## 稳定性约定

- 主题索引必须防越界：
  - 读取主题统一走安全访问器（如 `ActiveTheme`）。
  - 选择主题时先做边界校验。
- 修改 `MainLayout` 时，优先保证以下行为不回归：
  - 顶栏元素不越界（手机小屏）
  - 底部导航在键盘弹出/收起后可回到底部
  - 主题抽屉可打开/关闭、可重复切换

---

## 禁止项

- 未明确要求，不新增/删除 NuGet 包，不安装 workloads。
- 不在 UI 层新增 SQLite / Dapper 或直接引用 Repository 实现。
- 不将底部导航改回仅移动端显示。
- 不在未沟通的情况下移除已有主题或主题侧边栏交互。
- 不在未沟通的情况下把播放器进度条改回 MudSlider 绑 Blazor 状态。
