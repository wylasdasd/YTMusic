# 活动上下文 (Active Context)

## 当前焦点
当前焦点已转为移动端主导航收敛与播放器体验统一：
- 底部导航已将 `Transfers` 收敛为 `Other`，并把下载任务页作为 `Other` 下的二级入口。
- 播放器正在从“通用卡片页”转向更接近 YouTube Music 的沉浸式布局，但 Android/Windows/原生视频分流仍需谨慎验证。
- Android 原生视频已被明确视为主视频播放路径，Blazor `/player/video` 在 Android 上只保留兜底角色。

## 最近的变更
- **设置抽屉持久化**：
  - 新增 `UiPreferencesService` 持久化三类轻量设置：主题索引、`Favorites Image`、`High Quality Audio`。
  - 三横杠抽屉新增“还原默认”危险操作按钮，并改为应用内 `MudDialog` 二次确认（`OK / Cancel`）。
- **数据重置链路**：
  - 新增 `AppResetService`，统一负责清空收藏表、下载表、下载目录文件、偏好设置和播放器状态。
  - 收藏数据通过 `FavoriteService.ResetAllAsync()` 重置，下载数据通过 `LocalMusicService.ResetAllAsync()` 重置。
- **主导航重构**：
  - 底部导航 `Library` 文案改回 `Download`。
  - 原 `Transfers` 主入口收进 `Other` 页面，`Other` 下包含“下载页面”入口，下载页内部保留三级筛选 `All / Active / Completed / Failed`。
  - `Other` 页面新增“历史播放列表”入口。
- **播放历史**：
  - `MusicPlayerService` 新增运行期内的 `PlaybackHistory`（最多 50 条），统一由播放器服务记录。
  - 现已覆盖 `Home/Search`、`Favorites`、`Library/Download`、历史页重播等播放入口。
- **播放器分流**：
  - Android 视频播放继续走原生全屏 `VideoPlayerActivity`。
  - 安卓场景下 `/player` 统一留在 `/player/audio`；`/player/video` 仅保留给 Windows/非 Android 的视频播放和兜底。
  - Android 原生视频退出时，`VideoPlayerActivity -> AndroidNativeVideoPlaybackService -> MusicPlayerService` 已补齐 `PlaybackStopped` 事件链，避免页面卡在“原生播放中”的假状态。
- **Player UI 方向**：
  - `PlayerAudio`/`PlayerVideo` 已去掉中心卡片，改成沉浸式背景布局。
  - 音频页与视频页样式已拆到各自 `.razor.css`；后续平台细调必须注意 Razor scoped CSS 不会跨组件共享。
- **列表播放逻辑**：在 `MusicPlayerService` 中实现了顺序和随机播放模式。
- **UI 布局调整**：在 `Player.razor` 中，将播放模式切换按钮移动到最右侧，收藏和下载按钮移动到最左侧。
- **播放优化**：
    - 修复了非列表歌曲无法循环的 BUG。
    - **单曲循环优化**：通过 JS 层面重置 `currentTime` 实现重播，避免了完整的网络请求和 UI 加载动画（Loading），提升了平滑度。
- **模式强制执行**：播放非收藏夹歌曲时，强制开启单曲循环并禁用模式切换按钮。
- **Android 通知权限**：在 `MainActivity` 恢复 Android 13+ `POST_NOTIFICATIONS` 运行时请求。
- **Android 后台服务**：`PlaybackForegroundService` 当前采用 `Media3` 播放内核 + 手写前台通知更新（含进度条）作为稳定方案。
- **关键实机结论**：目标设备上需使用平台原生 `Notification.MediaStyle + platform MediaSession.Token`，否则可能出现“有进度条但没有上一首/下一首”。
- **代码清理**：删除无效通知权限服务抽象，减少多链路并行导致的排障噪音。
- **Windows 窗口壳层**：
  - 已新增 `Platforms/Windows/MainWindow.xaml(.cs)`，Windows 平台不再走默认 `new Window(new MainPage())`。
  - `MauiProgram.cs` 已配置 `AppWindow.TitleBar` 与 `OverlappedPresenter`，保留边框和缩放能力。
  - `MainLayout.razor` 已接入 Windows 顶栏按钮（最小化/最大化/关闭）和桌面专用拖拽热区。
  - 拖拽逻辑已切换为模板同款 `WindowChromeService + mouseInterop.js` 持续移动方案，且修正了“错误调用 `MoveWindow` 导致窗口尺寸变化”的坑。
  - 顶栏搜索胶囊已删除；Windows 下三横杠位于窗口按钮组左侧；顶栏已改为全宽，放大窗口时左右控件可贴边。

## 下一步计划
- 实机验证 Android 原生视频“进入/退出/恢复音频页”链路，重点检查是否仍有假状态或死按钮。
- 若继续调播放器 UI，优先收敛为“平台分支 + 页面独立 scoped CSS”，避免音频页改动误以为自动影响视频页。
- 评估是否将 `PlaybackHistory` 从运行期内存提升为 SQLite 持久化数据。
- `Other` 页面后续可继续收纳低频入口（例如设置、关于、历史、调试页），但底部主导航尽量保持 5 个以内。
