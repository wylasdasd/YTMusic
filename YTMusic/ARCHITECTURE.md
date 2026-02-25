# 项目框架思路 (Project Architecture & Design)

YTMusic 采用了 **.NET MAUI Blazor Hybrid** 架构。这种混合架构结合了原生应用的系统级访问能力和现代 Web 开发的灵活性。

## 1. 核心架构模式 (Core Architecture Pattern)

项目主要遵循 **MVVM (Model-View-ViewModel)** 以及 **依赖注入 (Dependency Injection)** 的设计模式：

*   **View (视图):** 使用 Blazor (`.razor` 文件) 和 MudBlazor 组件库来构建跨平台的用户界面。UI 是响应式的，能够适应桌面和移动端屏幕。
*   **ViewModel (视图模型):** 位于 `Components/Pages/` 目录下（例如 `SearchVM`, `DownloadsVM`）。它们负责处理页面特定的业务逻辑、状态管理，并与后台服务进行交互，保持 UI 代码的整洁。
*   **Services (服务层):** 位于 `Services/` 目录下。这些服务被注册为单例 (Singleton) 或瞬态 (Transient)，并在整个应用中共享核心逻辑。

## 2. 服务层职责拆分 (Service Layer Breakdown)

应用将不同的业务域划分到了具体的接口和服务中：

*   `IYouTubeService`: 负责与 YouTube 后端交互。它封装了 `YoutubeExplode` 库，处理搜索、流媒体地址解析和文件下载的核心逻辑。
*   `MusicPlayerService`: 充当全局的播放器状态机。它保存当前播放的曲目、播放状态、进度等，并作为 UI 组件（如 `Player.razor`）和底层 JavaScript 播放器之间的桥梁。
*   `IDownloadManagerService` & `ILocalMusicService`: 负责处理本地文件系统的 I/O 操作，确保在不同操作系统的沙盒环境（如 Android/iOS 的 LocalAppData）下正确地保存和读取音乐文件。

## 3. UI 与原生交互 (UI and Native Interop)

*   **Blazor WebView:** 整个应用的 UI 运行在 MAUI 提供的原生 WebView 控件中。所有的 HTML/CSS 渲染均在这个容器内完成。
*   **JS Interop:** 由于部分多媒体控制（特别是复杂的音频解码）在 Web 层处理更佳，项目使用了 Blazor 的 JavaScript 互操作功能 (`IJSRuntime`)。C# 代码可以通过它调用 `wwwroot/js/audioPlayer.js` 中的方法来控制媒体播放，同时 JS 也能触发 C# 的事件（如更新播放进度）。
