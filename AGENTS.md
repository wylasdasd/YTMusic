# YTMusic：Agent 约定（当前代码基线）

> 最后与代码对齐：2026-06-29

## 仓库结构
- `YTMusic/`：.NET MAUI Blazor 主应用
  - `Components/Layout/`：全局布局（`MainLayout.razor`、`GlobalAudioPlayer.razor`）
  - `Components/Pages/`：页面组件与对应 VM（`*VM.cs` 同目录）
  - `Services/`：业务服务（播放器、下载、全局状态等）
  - `Services/Abstractions/`：服务接口（`IYouTubeService`、`IFavoriteService`、原生播放接口等）
  - `Services/Abstractions/Playback/`：播放管线接口（`IPlaybackInstance`、`IPlaybackHost`）
  - `Services/Playback/`：播放实现（`PlaybackSwitcher`、`PlaybackInstances`、`PlaybackModels`）
  - `Platforms/Android/Services/`：ExoPlayer 前台服务、全屏视频 Activity、`ExoPlayerStreamSourceFactory`
  - `wwwroot/js/`：前端脚本（`ytmLayout.js`、`audioPlayer.js`、`mouseInterop.js`）
- `CommonHelp/`：共享类库
- `memory-bank/`：决策、进度与播放架构文档（改播放逻辑前先读 `playbackArchitecture.md`）

## 构建与验证
- Windows 快速构建：`dotnet build YTMusic/YTMusic.csproj -c Debug -f net10.0-windows10.0.19041.0`
- 若可执行文件被占用（VS 正在调试），使用临时输出验证：
  - `dotnet build YTMusic/YTMusic.csproj -c Debug -f net10.0-windows10.0.19041.0 -o YTMusic/bin/Debug/net10.0-windows10.0.19041.0/win-x64-temp`
- 设计时 TFM 限制仅在 `YTMusic/Directory.Build.props`；**不要**在仓库根放 `Directory.Build.props`（会污染 `CommonHelp`）。

## 通用开发约定
- 命名空间与 `RootNamespace`（`YTMusic`）及目录结构一致。
- 服务接口放 `Services/Abstractions/`（`YTMusic.Services.Abstractions`）；播放接口放 `Services/Abstractions/Playback/`（`YTMusic.Services.Abstractions.Playback`）；实现类留在 `Services/` 及 `Services/Playback/`。
- Razor 组件命名空间与 `YTMusic/Components/**` 对齐（例如 `YTMusic.Components.Pages`）。
- 组件与 VM 同目录：
  - ViewModel 文件必须以 `VM.cs` 结尾（例如 `Search.razor` -> `SearchVM.cs`）。
  - 禁止跨目录放置组件与对应 VM。
- 优先复用 `CommonHelp` 现有能力，避免新增零散工具函数。
- 避免无关格式化，尽量保持原有代码风格与换行。
- 播放相关日志使用 `[YTMusic:Playback]`（`PlaybackDiagnostics`）。

## 播放架构（改代码必读）
- 状态源：`MusicPlayerService` 实现 `IPlaybackHost`；切换走 `PlaybackSwitcher.SwitchAsync`（`SemaphoreSlim` 串行）。
- 五种 `PlaybackKind`：`NativeAudio`、`NativeVideo`、`WebAudio`、`WebMuxedVideo`、`Hybrid`。
- 任意时刻仅一条活跃管线；切歌 / 切模式必须先 `Detach` 旧 `IPlaybackInstance`。
- Android 在线视频走 `NativeVideo`（ExoPlayer 全屏），**不要**回退到 WebView 主路径。
- 播放器页进度条由 `audioPlayer.mountProgressBar` 托管，**禁止** MudSlider 绑 `CurrentTime`。
- 完整路由表与勿回归清单：[`memory-bank/playbackArchitecture.md`](memory-bank/playbackArchitecture.md)。

## UI/布局强约束（按当前实现）
- 顶部栏：`MainLayout` 中保留品牌、搜索入口、三横杠菜单按钮。
- 主题切换：
  - 点击三横杠打开右侧主题侧边栏（不是直接切换）。
  - 主题由侧边栏列表选择，当前内置 5 套（含 2 套亮色）。
  - 主题逻辑在 `MainLayout.razor` 中维护，使用 `ThemePreset`。
  - 侧边栏还包含：分离流视频画质、视频后台预检、两行显示标题、`Favorites Image` 等（`UiPreferencesService`）。
- 底部导航：
  - 顺序：Favorites → Player → Home → Other；全端固定在最底部（`position: fixed; bottom: 0`），不能回退到侧边 Rail。
  - 需要兼容输入法弹出场景：依赖 `wwwroot/js/ytmLayout.js` 的 `visualViewport` 修正。
- 移动端安全区：
  - Web 样式使用 `env(safe-area-inset-*)`。
  - 原生层在 `MainPage.xaml.cs` 已补充 iOS/Android 顶部安全区处理，修改时不要破坏。

## Home/Search 页约定
- Home（Search 页初始态）不显示页面标题与引导提示块。
- 搜索框回车搜索需保证“所见即所得”：
  - `Immediate="true"` + `OnKeyUp` 触发。
- 搜索页样式主要由 `Search.razor.css` 管理，颜色优先跟随布局定义的 CSS 变量。

## 稳定性约定
- 主题索引必须防越界：
  - 读取主题统一走安全访问器（如 `ActiveTheme`）。
  - 选择主题时先做边界校验。
- 修改 `MainLayout` 时，优先保证以下行为不回归：
  - 顶栏元素不越界（手机小屏）
  - 底部导航在键盘弹出/收起后可回到底部
  - 主题抽屉可打开/关闭、可重复切换

## 禁止项
- 未明确要求，不新增/删除 NuGet 包，不安装 workloads。
- 不将底部导航改回仅移动端显示。
- 不在未沟通的情况下移除已有主题或主题侧边栏交互。
- 不在未沟通的情况下把播放器进度条改回 MudSlider 绑 Blazor 状态。
