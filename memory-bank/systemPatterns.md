# 系统模式 (System Patterns)

## 技术架构
- **框架**: .NET 10 Blazor MAUI (跨平台桌面/移动端)。
- **UI 组件**: MudBlazor。
- **服务导向**: 核心逻辑封装在服务类中（例如 `MusicPlayerService`）。

## 关键技术模式
- **音频代理**: `LocalAudioProxy` 和 `LocalFileProxy` 使用 `HttpListener` 为 HTML5 `<audio>` 标签提供流服务，从而绕过 CORS 限制或直接文件访问限制。
- **JS 互操作**: 广泛使用 `IJSRuntime` 来控制音频元素（播放/暂停、进度跳转、事件监听）。
- **全局状态管理**: `MusicPlayerService` 作为单例/作用域状态提供者，向整个 UI 提供数据，并通过事件 (`OnChange`) 通知组件更新。

## 组件结构
- **GlobalAudioPlayer.razor**: 实现在 `MainLayout` 中，跨页面持久存在的音频组件。
- **Player.razor**: 全屏播放器视图，包含详细控制项和元数据展示。
- **Favorites.razor**: 负责收藏歌曲的管理及列表播放的触发。

## 数据持久化
- **SQLite**: 使用 `Dapper` 进行关于收藏夹、文件夹和音轨的数据库操作。
