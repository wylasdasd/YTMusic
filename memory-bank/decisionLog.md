# 决策日志 (Decision Log)

## 2026-02-26: 全局 Loading 状态管理
- **决策**: 引入 `GlobalStateService` 并结合 `MudOverlay` 在 `MainLayout` 中实现全局遮罩。
- **依据**: 解决从搜索页或收藏页点击歌曲到进入播放器之间的“解析间隙”（YouTube 流解析需要几秒钟），避免用户认为程序卡死。

## 2026-02-26: 列表播放逻辑实现
- **决策**: 将播放列表状态（Playlist, CurrentIndex）集中在 `MusicPlayerService` 中，而不是页面组件内。
- **依据**: 确保音频在页面导航过程中不会中断，且能跨页面维持播放队列的一致性。

## 2026-02-26: 随机播放 (Shuffle) 算法设计
- **决策**: 采用 `ShuffleIndices` 预生成索引列表的方案。
- **依据**: 相比于纯随机选取，预生成列表可以确保在播放完一整轮之前不会听到重复的歌曲，且方便实现“上一首”回溯。

## 2026-02-26: 单曲循环体验优化
- **决策**: 引入 `OnRequestReplay` 事件，在 `GlobalAudioPlayer.razor` 中通过 JS 操作 `currentTime = 0` 来实现重播，跳过 `PlayInternalAsync` 流程。
- **依据**: 避免了重新请求 YouTube 流地址带来的二次解析和 UI `Loading` 闪烁，提供无缝循环体验。

## 2026-02-26: 播放页按钮布局重排
- **决策**: 强制将功能性操作（收藏、下载）移至最左侧，播放序列控制（切换模式）移至最右侧。
- **依据**: 明确划分“内容管理”与“队列控制”区域，防止误触，并让中央核心播放区更突出。

## 2026-02-21: 本地音频/视频流代理 (历史决策)
- **决策**: 实现 `LocalAudioProxy` 和 `LocalFileProxy`（基于 `HttpListener`）。
- **依据**: 解决 WebView/浏览器直接访问某些 YouTube 流地址可能遇到的跨域（CORS）限制，以及方便将来处理加密流或本地文件路径。

## 2026-03-17: Android 后台播放通知采用“Media3 + 手写通知兜底”混合策略
- **决策**: 保留 `MediaSessionService/MediaSession/ExoPlayer(Media3)` 作为标准媒体架构，同时保留 `PlaybackForegroundService` 内手写前台通知更新逻辑（进度条、播放暂停、Stop）。
- **依据**:
  - 在当前 .NET MAUI + AndroidX.Media3 绑定环境下，尝试“纯 Media3 自动通知”后出现“无通知卡片”回归。
  - 手写前台通知能稳定满足“必须有通知卡片/进度条”的核心诉求，且在不同 ROM 上可控性更高。
  - 先保证可见性和稳定性，再逐步评估是否迁移到完整 Media3 `PlayerNotificationManager/MediaNotification.Provider`。

## 2026-03-17: Android 13+ 通知权限作为播放前置条件
- **决策**: 在 `MainActivity` 启动阶段显式请求 `POST_NOTIFICATIONS`；Manifest 保留 `FOREGROUND_SERVICE`、`FOREGROUND_SERVICE_MEDIA_PLAYBACK`、`POST_NOTIFICATIONS`。
- **依据**:
  - 用户实机验证显示：权限未授予时表现为“播放有声音但无通知/锁屏媒体控件”。
  - 权限问题比业务代码问题更容易造成误判，必须前置处理。

## 2026-03-17: 避免多套通知实现并行演进
- **决策**: 删除无效或重复的通知权限服务抽象（DI 层），只保留单一、可验证的权限与通知链路。
- **依据**:
  - 本轮多次修改里，重复链路增加了排障成本，且容易出现“改了但不生效”的假象。
  - 单链路策略更容易做 A/B 验证与回归检查。

## 2026-03-17: 某些 ROM 需“平台原生 MediaStyle + 平台 MediaSession Token”才显示上一首/下一首
- **决策**: 在 `PlaybackForegroundService` 中保留平台级 `android.media.session.MediaSession`，并优先使用 `Notification.MediaStyle` + `SetMediaSession(platformToken)` + `SetShowActionsInCompactView(0,1,2)` 构建三键通知。
- **依据**:
  - 仅使用 `NotificationCompat.MediaStyle` 时，实机出现“有进度条但无上一首/下一首”。
  - 切换到平台原生 `Notification.MediaStyle` 后，实机确认“上一首/下一首”恢复显示。
  - 该行为与设备/ROM 渲染策略强相关，应作为已验证结论固化，避免后续误回退。

## 2026-03-21: Windows 自定义窗口采用“壳层定制 + 页面交互分层”方案
- **决策**:
  - Windows 端通过 `ForWindows/Windows/MainWindow.xaml` + `App.xaml.cs` 平台分支接管默认窗口；
  - 在 `MauiProgram.cs` 中配置 `AppWindow.TitleBar` 与 `OverlappedPresenter`；
  - 在 `MainLayout.razor` 中单独实现 Windows 顶栏按钮和拖拽热区；
  - 使用 `WindowChromeService + mouseInterop.js` 实现模板同款拖拽。
- **依据**:
  - 仅修改 `Window.TitleBar` 外观并不能自动获得完整的桌面交互，窗口按钮与拖拽入口仍需要页面层主动承接。
  - 参考模板验证后发现，桌面端更稳定的方案是“窗口壳层负责能力、页面层负责入口”，而不是把所有行为都押在 WinUI 标题栏对象上。
  - 这样可以在不影响 Android/iOS 的前提下，独立优化 Windows 窗口体验。

## 2026-03-21: Windows 拖拽方式改为“持续跟随鼠标移动”，而不是 `HTCAPTION` 一次性交给系统
- **决策**: 放弃 `SendMessage(...WM_NCLBUTTONDOWN, HTCAPTION...)` 方案，改为模板同款“mousedown 记录位置 + JS 全局 `mousemove` + Win32 `SetWindowPos`”。
- **依据**:
  - 参考模板的拖拽手感与原生窗口更接近，且更容易控制双击最大化、拖拽热区范围、鼠标样式。
  - 直接使用 `cursor: move` 会出现十字光标，不符合目标体验；页面层驱动拖拽时保留 `cursor: default` 更自然。
  - 实现中若使用 `MoveWindow(hWnd, x, y, 0, 0, ...)` 会错误修改窗口尺寸，后续已确认为坑点，必须使用 `SetWindowPos(..., SWP_NOSIZE ...)` 只移动位置。

## 2026-03-21: Windows 顶栏布局需与移动端分离看待
- **决策**:
  - 顶部搜索胶囊从当前布局中移除；
  - Windows 下三横杠放在窗口按钮组左边，并与系统按钮保持明显间距；
  - 顶栏内部容器使用全宽布局，确保放大窗口后左右元素贴边。
- **依据**:
  - Android 上合理的顶栏排布，在 Windows 桌面窗口中不一定合理；尤其三横杠放在系统按钮旁边时容易被误认为“第四个窗口控制按钮”。
  - 居中限宽容器会导致放大后两侧控件离边太远，不符合桌面端标题栏直觉。
