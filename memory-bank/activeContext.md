# 活动上下文 (Active Context)

## 当前焦点
Windows 自定义窗口与桌面顶栏交互已完成一轮稳定化；后续若继续调 UI，需保持 Android 布局不被桌面端回归影响。

## 最近的变更
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
- 实机验证 Windows 顶栏在不同窗口尺寸下的观感（重点：拖拽热区范围、按钮间距、三横杠与系统按钮的分组感）。
- 若继续调整桌面顶栏样式，优先做平台分支，避免误伤 Android 顶部栏布局。
- Android 方向仍保留：锁屏媒体卡片一致性验证、通知链路回归清单。
