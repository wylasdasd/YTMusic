# 意图

不想给 YouTube Music 充钱，所以才开发了这个。需自行挂 VPN 访问 YouTube。当前版本已支持 **Android 后台播放**。

**已适配：** Windows、Android  
**未适配：** iOS、macOS（本人不用苹果，未做验证，不做任何保证）

# YTMusic

YTMusic 是一款基于 **.NET MAUI** 和 **Blazor Hybrid** 构建的跨平台音乐流媒体与下载应用。它利用 YouTube 曲库，让您在设备上搜索、播放和下载音乐与视频，并管理收藏夹与本地库。

## 功能特性

### 搜索与播放
- **YouTube 搜索：** 关键词搜索歌曲、艺术家或视频，分页加载结果。
- **在线流媒体：** 默认播放纯音频流；可在播放器内切换为视频模式。
- **播放模式：** 顺序、随机、单曲循环；支持上一首 / 下一首（播放超过 3 秒时上一首从头播放当前曲）。
- **播放历史：** 记录最近播放，可快速重新播放。

### 收藏与本地
- **收藏夹：** 按文件夹管理收藏，支持文件夹内顺序 / 随机播放。
- **本地下载：** 下载音频或视频供离线播放；浏览、播放与删除本地文件。
- **本地文件：** 支持播放设备上的本地音频 / 视频（含从下载页、收藏夹直接播放）。

### 播放器
- **双页面：** `/player/audio` 与 `/player/video` 分离展示；根据内容自动路由。
- **全局播放器：** `GlobalAudioPlayer` 持久挂载 `<audio>` / `<video>`，跨页面连续播放。
- **进度条：** 由 `audioPlayer.js` 接管（非 Blazor 组件），降低拖动卡顿。
- **格式支持：** WebM/Opus 优先走 HTML5 `<audio>` / 本地代理（`audio/webm`）；`ogv.js` 仍保留为兜底；Android 走 ExoPlayer 原生解码。
- **CORS 代理：** 在线流经本地 HTTP 代理转发，绕过 WebView 跨域限制。

### 平台能力
- **Android：** ExoPlayer 原生播放、前台服务与 MediaSession，支持后台播放与系统通知栏控制。
- **Windows：** 自定义窗口拖拽 / 最大化；WebView2 播放与本地文件代理。

### 其他
- **主题：** 内置 5 套主题（含 2 套亮色），通过顶栏菜单打开主题侧边栏切换。
- **底部导航：** Favorites、Player、Home、Other（本地资源 / 传输 / AList / 历史在 Other 内）；全端固定底栏，兼容输入法弹出（`ytmLayout.js` + `visualViewport`）。
- **网络提示：** 连接失败时提示检查网络 / VPN。
- **AList 集成：** 配置 AList 远程存储，上传本地文件；从 AList 目录拉取远程曲目下载。
- **传输任务：** 统一下载 / 上传任务进度页。

## 技术栈

| 类别 | 技术 |
|------|------|
| 框架 | .NET 10 MAUI、Blazor WebView |
| UI | MudBlazor |
| YouTube | YoutubeExplode（搜索与流 URL 提取，无需官方 API Key） |
| 数据 | SQLite + Dapper（`YTMusic.DAL`，经 BLL 访问） |
| Web 播放 | `audioPlayer.js`、`ogv.js`、`ytmLayout.js` |
| Android 原生播放 | AndroidX Media3 / ExoPlayer |
| 播放架构 | `PlaybackSwitcher` + 五种 `IPlaybackInstance`（详见 `memory-bank/playbackArchitecture.md`） |
| 架构 | MVVM（`ViewModels/*VM.cs`）、BLL/DAL 分层、依赖注入 |

## 开始使用

### 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 带有 .NET MAUI 工作负载的 Visual Studio 2022，或 VS Code + C# Dev Kit + MAUI 扩展

### 编译与运行

1. 克隆此仓库。
2. 打开 `YTMusic.slnx`。
3. 选择目标框架（例如 `net10.0-windows10.0.19041.0` 或 `net10.0-android`）。
4. 编译并运行。

**命令行（Windows）：**

```bash
dotnet build YTMusic/YTMusic.csproj -c Debug -f net10.0-windows10.0.19041.0
```

若可执行文件被占用（例如 VS 正在调试），可用临时输出目录：

```bash
dotnet build YTMusic/YTMusic.csproj -c Debug -f net10.0-windows10.0.19041.0 -o YTMusic/bin/Debug/net10.0-windows10.0.19041.0/win-x64-temp
```

## 项目结构

```
YTMusic/                 # 主 MAUI Blazor 应用（UI 层）
  Components/
    Layout/              # MainLayout、GlobalAudioPlayer、底部导航与主题
    Pages/               # 页面组件（.razor）
    Dialogs/             # 弹窗组件
  ViewModels/            # 页面 ViewModel（*VM.cs）
  Adapters/              # BLL Ports 的 MAUI/MudBlazor 实现
  Infrastructure/        # Proxies/（HTTP 代理）、Storage/（平台路径）
  Services/              # 播放管线、UI 偏好、窗口壳层（非业务编排）
    Abstractions/Playback/ # IPlaybackHost、IPlaybackInstance
    Playback/            # PlaybackSwitcher、PlaybackInstances
  Platforms/             # Android / Windows / iOS / MacCatalyst 平台代码
  AppGlobal.cs           # UI 层全局常量与 Runtime.Services
  wwwroot/js/            # audioPlayer.js、ytmLayout.js 等
YTMusic.BLL/             # 业务逻辑层（YouTube、收藏、下载、AList 等）
  Abstractions/          # I*Service、I*Repository、IYouTubeApiClient、IFileSystem
  Services/、Models/、Ports/
  Infrastructure/        # YoutubeExplode、AList HTTP、本地文件系统
  AppGlobal.cs           # BLL 全局常量与运行时状态
YTMusic.DAL/             # 数据访问层（SQLite + Dapper）
  Repositories/、Infrastructure/
CommonHelp/              # 共享工具库（文件、时间、JSON 等）
YTMusic.Tests/           # 单元测试
memory-bank/             # 项目决策与进度记录
```

各库职责与依赖约束详见 [`AGENTS.md`](AGENTS.md)。

## 主要页面

| 路由 | 说明 |
|------|------|
| `/`、`/search` | 搜索与 Home |
| `/favorites` | 收藏夹文件夹列表 |
| `/favorites/folder/{id}` | 文件夹内曲目 |
| `/player`、`/player/audio`、`/player/video` | 播放器（自动路由到音频 / 视频页） |
| `/other` | 其他菜单入口 |
| `/downloads` | 本地资源 |
| `/transfers` | 下载 / 上传任务 |
| `/upload` | AList 远程存储配置与上传 |
| `/history` | 播放历史 |

## 延伸阅读

- [`YTMusic/ARCHITECTURE.md`](YTMusic/ARCHITECTURE.md) — 架构与设计思路
- [`YTMusic/CORE_LOGIC.md`](YTMusic/CORE_LOGIC.md) — 跨平台播放与流提取等核心难点
- [`memory-bank/playbackArchitecture.md`](memory-bank/playbackArchitecture.md) — 播放管线（方案 B）详细说明
- [`AGENTS.md`](AGENTS.md) — 开发约定与 UI 约束（供协作者 / Agent 参考）

## 免责声明

本项目仅供学习与个人使用。请遵守 YouTube 及相关服务的使用条款与当地法律法规。开发者不对因使用本软件产生的任何问题负责。
