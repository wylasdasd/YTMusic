# 系统模式 (System Patterns)

## 技术架构
- **框架**: .NET 10 Blazor MAUI (跨平台桌面/移动端)。
- **UI 组件**: MudBlazor。
- **服务导向**: 核心逻辑封装在服务类中（例如 `MusicPlayerService`）。

## 关键技术模式
- **音频代理**: `LocalAudioProxy` 和 `LocalFileProxy` 使用 `HttpListener` 为 HTML5 `<audio>` 标签提供流服务，从而绕过 CORS 限制或直接文件访问限制。
- **JS 互操作**: 广泛使用 `IJSRuntime` 来控制音频元素（播放/暂停、进度跳转、事件监听）。
- **全局状态管理**: `MusicPlayerService` 作为单例/作用域状态提供者，向整个 UI 提供数据，并通过事件 (`OnChange`) 通知组件更新。
- **Windows 窗口壳层定制**:
  - Windows 端使用 `ForWindows/Windows/MainWindow.xaml` 接管默认 MAUI 窗口，而不是只依赖 `new Window(new MainPage())`。
  - `MauiProgram` 中通过 `ConfigureLifecycleEvents` 配置 `AppWindow.TitleBar` 与 `OverlappedPresenter`，保留边框、缩放、最小化/最大化能力，同时折叠标题栏高度。
  - 页面层的窗口交互不要依赖 `Window.TitleBar` 自带行为；窗口按钮与拖拽热区由 `MainLayout.razor` 主动提供，调用 `WindowChromeService` 完成最小化、最大化、关闭、拖拽。
  - Windows 拖拽实现采用“按下记录起点 + JS 全局 mousemove + Win32 移动窗口”的方式，行为更接近参考模板；拖拽热区鼠标样式保持 `cursor: default`，避免出现 `move` 十字光标。
  - 涉及窗口按钮、拖拽区、桌面专用布局时，UI 层统一通过 `IsWindowsDesktop` 做平台分支，避免影响 Android/iOS。
  - 顶部工具栏在 Windows 放大窗口时应使用全宽布局，不要给 `.ytm-topbar-inner` 设固定 `max-width + auto margin`，否则左右控件不会真正贴边。

## 组件结构
- **GlobalAudioPlayer.razor**: 实现在 `MainLayout` 中，跨页面持久存在的音频组件。
- **Player.razor**: 全屏播放器视图，包含详细控制项和元数据展示。
- **Favorites.razor**: 负责收藏歌曲的管理及列表播放的触发。

## 数据持久化
- **SQLite**: 使用 `Dapper` 进行关于收藏夹、文件夹和音轨的数据库操作。
