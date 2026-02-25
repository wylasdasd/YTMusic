# 意图
不想给youtube music 充钱，所以才开发了这个。使用自己挂vpn
# YTMusic

YTMusic 是一款基于 **.NET MAUI** 和 **Blazor Hybrid** 构建的跨平台音乐流媒体与下载应用。它利用庞大的 YouTube 曲库，让您可以直接在设备上搜索、播放和下载音乐与视频。

## 功能特性

- 🔍 **YouTube 搜索:** 使用 YouTube 搜索引擎快速查找歌曲、艺术家或视频。
- 🎵 **音乐流媒体:** 直接从 YouTube 播放高品质音频。包含一个高级播放器，能够在所有平台上处理 WebM/Opus 格式。
- ⬇️ **媒体下载:** 下载音频或视频文件以便离线播放。
- 📂 **本地库管理:** 无缝查看、播放和删除您下载的媒体文件。
- 💻 **跨平台:** 原生运行在 Windows、Android、iOS 和 MacCatalyst 上。

## 技术栈

- **框架:** .NET 10 MAUI & Blazor Webview
- **UI 组件:** MudBlazor (Material Design 风格)
- **YouTube API:** YoutubeExplode (用于搜索和提取流媒体 URL，无需官方 API 密钥)
- **音频播放:** HTML5 Audio API & `ogv.js` (WebAssembly 解码器，提供广泛的格式支持)
- **架构:** MVVM (Model-View-ViewModel 模型-视图-视图模型)

## 开始使用

### 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 带有 .NET MAUI 工作负载的 Visual Studio 2022，或安装了 C# Dev Kit 和 MAUI 扩展的 Visual Studio Code。

### 编译与运行

1. 克隆此仓库。
2. 在您的 IDE 中打开 `YTMusic.slnx`。
3. 选择您的目标框架（例如，`net10.0-windows10.0.19041.0` 或 `net10.0-android`）。
4. 编译并运行应用程序。

## 项目结构

- `YTMusic/`: 主要的 MAUI Blazor 应用程序。
  - `Components/`: Blazor UI 组件和页面（`Search`、`Player`、`Downloads`）。
  - `Services/`: 核心业务逻辑（`YouTubeService`、`MusicPlayerService` 等）。
  - `wwwroot/`: 静态 Web 资源，包括自定义的 `audioPlayer.js`。
- `CommonHelp/`: 包含实用工具和辅助函数的共享库。
- `YTMusic.Tests/`: 应用程序的单元测试。
