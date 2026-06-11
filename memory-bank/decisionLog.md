# 决策日志 (Decision Log)

## 2026-06-29: 全仓库 Markdown 文档与代码基线对齐

- **决策**:
  - 更新 README、AGENTS、executeList、YTMusic/*.md 及 memory-bank 全部文档，反映方案 B 播放架构、当前服务列表、底栏结构（Favorites/Player/Home/Other）、WebM 音频主路径与 Hybrid 平台差异说明。
  - `executeList.md` 由早期规划稿改为「开发里程碑」历史记录。
- **依据**:
  - 文档多处仍引用旧方案（MediaElement、ogv 默认路径、桌面-only 产品描述、错误绝对路径等），与当前代码不一致，易误导后续改动。
- **相关文件**: 仓库内全部 `*.md`。

## 2026-06-11: 播放架构方案 B（`ActivePlayback` + `PlaybackSwitcher`）与 Android 在线原生视频
- **决策**:
  - `Services/Playback/` 引入 `PlaybackSwitcher`（`SemaphoreSlim` 串行）、五种 `IPlaybackInstance`、`IPlaybackHost`；`MusicPlayerService` 实现宿主并通过 `ActivatePlaybackAsync` 切换。
  - 任意时刻仅一条活跃管线；`NativeAudio`↔`Hybrid` 切换时 `SharesNativeAudioBackend` 保留 ExoPlayer 音频前台服务。
  - Android 在线视频统一走 `NativeVideo` / `VideoPlayerActivity`：muxed 单 URL；分离流用 `ExoPlayerStreamSourceFactory` + Java 反射 `MergingMediaSource`。
  - 流选择：muxed 优先；分离流 video-only 画质进 `UiPreferencesService.RemoteVideoStreamQuality`（默认最低）；可选 `PrefetchRemoteVideo` 后台预检（默认关）。
  - 在线视频 UI：确认弹窗不再前置 manifest 检测；确认后才 loading + 解析（避免弹窗前长时间等待）。
  - 全屏视频：`Intent` 传递 `autoPlay`；`VideoPlayerActivity` `KeepScreenOn` 防熄屏；禁止 Attach 后再 `PauseAsync` 造成竞态。
  - 完整架构说明见 **`memory-bank/playbackArchitecture.md`**。
- **依据**:
  - 音频切视频、Web/原生并存时曾出现双音轨与状态不同步；串行 Detach/Attach 可收敛。
  - Android WebView 播放在线分离流体验差；ExoPlayer 合并更接近原生播放器。
  - 弹窗前后两次 manifest 拉取是 loading 过长主因；预检改为可选设置项。
- **相关文件**: `Services/Playback/**`、`MusicPlayerService.cs`、`VideoPlayerActivity.cs`、`ExoPlayerStreamSourceFactory.cs`、`UiPreferencesService.cs`、`PlayerAudio.razor`、`GlobalAudioPlayer.razor`。

## 2026-06-07: 播放器进度条改由 JS 托管（`ytm-player-progress`），不再用 MudSlider 绑 Blazor 状态
- **决策**:
  - `audioPlayer.js` 提供 `mountProgressBar` / `setProgress` / `seekProgressTo`；`PlayerAudio` / `PlayerVideo` 仅提供空容器 `@ref`，进度与时间由 DOM 直接更新。
  - Web 路径：播放中用 `requestAnimationFrame` + `timeupdate` 读 `activePlayer.currentTime` 更新滑块；拖动时本地更新，约 120ms 节流 seek，松手精确定位；`OnTimeUpdate` 回 .NET 节流为约 500ms（仅更新 `MusicPlayerService` 状态，不触发播放器页 `StateHasChanged`）。
  - 原生播放（Android/iOS ExoPlayer 等）：`setNativeProgressMode(true)`，进度由 `GlobalAudioPlayer` 在 `OnTimeChanged` 时调 `setProgress`；拖动 seek 走 `OnProgressSeek` → `SeekAsync`。
  - webm **音频**统一走隐藏 `<audio>`（`audio/webm` 代理），不再默认切 OGV；视频页 `<video>` 去掉 `controls`，与音频页共用 JS 进度条。
  - 本地文件仅 `.mp4` 且在下载记录/调用方标记 `IsVideo` 时才 `IsCurrentStreamVideo`（`ShouldPlayLocalAsVideo`），避免 webm 音频被扩展名误判为视频。
- **原因（此前 Mud 进度条卡顿、不跟手）**:
  1. **架构**：`MudSlider.Value` 绑 `MusicPlayerService.CurrentTime`，每次 `timeupdate`（约 4Hz）经 JS 互调 → `UpdateTime` → `OnTimeChanged` → `StateHasChanged` 整页重绘，WebView2 + Blazor 下延迟大。
  2. **拖动**：`Immediate="false"` 导致松手才 `ValueChanged`；拖动期间服务端时间仍会回写，拇指与手指打架。
  3. **防抖过重**：拖后 1.5s 内屏蔽 `OnTimeChanged`、0.25s 变化阈值，松手后进度显示迟滞。
  4. **webm + OGV**：音频 webm 曾走 `ogvPlayer`，seek/`timeupdate` 不如原生 `<audio>` 稳定。
  5. **对比**：视频页曾用 `<video controls>` 原生条，控件直接绑媒体元素，故体感更跟手——根因是 UI 分层而非 `<audio>` 不能 seek。
- **样式**：`app.css` 中 `.ytm-player-progress__slider` 轨道 4px、拇指 14px，`margin-top: -5px`（WebKit）使拇指垂直居中于轨道。
- **相关文件**: `wwwroot/js/audioPlayer.js`、`wwwroot/app.css`、`Components/Layout/GlobalAudioPlayer.razor`、`Components/Pages/PlayerAudio.razor`、`Components/Pages/PlayerVideo.razor`、`Services/MusicPlayerService.cs`（`ShouldPlayLocalAsVideo`）。

## 2026-06-07: 页面滚动在列表容器内，位置用 JS 内存缓存（非 C#、非控件自带）
- **决策**:
  - 禁止 `html/body` 与 `ytm-main` 整页滚动；列表在 `.ytm-page__scroll`（`PageListScroll`）或 Upload 的 `MudTabPanel[data-page]` 内滚动。
  - Blazor 路由仍会销毁页面 DOM，滚动位置记在 `window.ytmLayout._pageScrolls`（`pageKey` → `scrollTop`），不落库、不写 C# Service。
  - `MainLayout`：`initPageScrollPersistence`（首屏）、`restorePageScrolls`（`LocationChanged` 后）、`saveAllPageScrolls`（底栏 `NavigateToTab` 前）。
  - 页面通过 `data-page` 标识：`search`、`favorites`、`upload-local` 等（见 `PageListScroll.razor` / `Upload.razor`）。
- **依据**:
  - 底栏 Tab 应用模型与默认 `RouteView` 销毁 DOM 不一致；恢复 scrollTop 必须操作 DOM，C# 无法纯托管读写元素滚动。
  - 未采用底栏 Keep-Alive（DOM 常驻可天然保留滚动）；当前以最小改动 + JS 缓存过渡。
- **相关文件**: `wwwroot/js/ytmLayout.js`、`wwwroot/app.css`、`Components/Layout/PageListScroll.razor`、`MainLayout.razor`。

## 2026-06-07: 已下载歌曲切歌必须区分 Web 代理与 Android 原生路径
- **决策**:
  - Web 代理：`loadSource` 用完整 URL 判断；本地代理 URL 增加 `&f=文件路径`；切歌前 `OnRequestPause`。
  - Android 原生：`PlaybackForegroundService` 切歌前 `Stop()` + `ClearMediaItems()` 再加载新媒体。
- **依据**:
  - 本地文件共用 `LocalFileProxy` 同源地址，`normalizeStreamUrl` 去 query 后会被误判为同一首，导致 UI 更新但音频不切换。
  - Android 已下载音频走 ExoPlayer，不经过 `audioPlayer.js`；需在服务层显式清理媒体项。

## 2026-06-07: 音视频标题设置改为「两行显示」，不做横向滚动
- **决策**:
  - `UiPreferencesService.MediaTitleTwoLines`：开启时 `-webkit-line-clamp: 2` + 省略号；关闭时完整换行。
  - 统一 `MediaTitle` 组件接入搜索/收藏/下载/历史/传输/上传/播放器/对话框。
- **依据**:
  - 用户明确不要 marquee；两行截断比单行滚动更适合列表与播放器标题区。

## 2026-06-07: AList 上传只写 `metadata.json` + 主媒体，不上传封面文件
- **决策**:
  - `metadata.json` 使用 `RemoteTrackMetadata.FromDownloadedTrack()`，字段为 `DownloadedTracks` 子集。
  - 封面仅 `thumbnailUrl` 写入 JSON；远端展示/下载从 URL 读封面，不再上传 `cover.*` 文件。
- **依据**:
  - 减少上传失败面（502）与重复存储；与远端 `metadata.json` 读取逻辑一致。

## 2026-06-07: 移除 High Quality Audio，在线播放统一偏好 WebM
- **决策**:
  - 删除 `PreferHighQualityAudio` / `UseWebM` 设置；解析与下载逻辑固定 WebM 优先。
- **依据**:
  - 设置项对实际链路影响有限，且增加心智负担。

## 2026-06-07: `Directory.Build.props` 不得放在仓库根目录
- **决策**:
  - 设计时单 TFM 限制仅放在 `YTMusic/Directory.Build.props`。
  - `CommonHelp` 保持 `net10.0`，不被设计时 `TargetFrameworks` 污染。
- **依据**:
  - VS 重新加载后根目录 `Directory.Build.props` 导致 `CommonHelp` 仅 windows TFM，Android 构建 NU1201。

## 2026-06-07: Android 顶栏三横杠与 Windows 分流
- **决策**:
  - 非 Windows：`ytm-theme-toggle-mobile` + `margin-left: auto`；`app.css` 全局兜底。
  - Windows 仍靠 `ytm-window-drag`（`flex:1`）推开布局。
- **依据**:
  - 移动端顶栏无 drag 占位，菜单按钮会贴在品牌旁而非右侧。

## 2026-03-30: AList 上传目录继续采用 `Remote Directory/<歌名md5>`
- **决策**:
  - 远端上传子目录保持 `Remote Directory/<歌名md5>` 格式。
  - 不直接使用歌名作为目录名，避免远端目录名中出现非法字符、兼容性差异或后续改名带来的歧义。
- **依据**:
  - 用户明确要求目录结构为 `Remote Directory/<歌名md5>`。
  - 目录名只承担稳定标识作用，显示名可通过本地媒体文件名恢复，不需要依赖目录名可读性。

## 2026-03-30: AList 目录下载按“一个目录等于一首歌”处理
- **决策**:
  - AList 目录下载不做复杂媒体集合管理，约定一个目录内只处理“一首歌的主音视频文件 + 一个封面图”。
  - 主媒体文件按体积优先挑选；封面图优先挑选 `cover.*`，否则取目录中首个图片文件。
- **依据**:
  - 用户明确说明当前远端目录结构就是“一目录一首歌”。
  - 收敛结构后，下载逻辑可以直接复用本地下载数据库模型，减少额外表结构与页面复杂度。

## 2026-03-30: AList 下载封面改存 Data URL，而不是本地 `file://`
- **决策**:
  - AList 目录下载后的封面继续落本地文件，但数据库中的 `ThumbnailUrl` 存储为 `data:image/...;base64,...`。
- **依据**:
  - MAUI Blazor 页面的 `MudImage`、CSS `background-image`、`video poster` 对本地 `file://` 图片路径兼容性不稳定。
  - Data URL 可以复用现有所有 `ThumbnailUrl` 显示链路，无需新增本地图片代理层。

## 2026-03-30: `Uploaded Files` 目录展示直接读取主音视频文件名
- **决策**:
  - 远端目录项展示名不再尝试从本地上传/下载记录推导，只要是目录就直接读取内部主音视频文件名，去扩展名后显示。
- **依据**:
  - 用户明确要求“不要判断 md5，直接读文件名”。
  - `md5` 目录名本身不可逆，展示层应优先面向用户可读内容，而不是远端内部标识。

## 2026-03-30: AList 上传/下载允许重复执行，默认覆盖本地同名文件
- **决策**:
  - AList 上传和远端下载任务队列取消“相同资源正在进行时不重复入队”的拦截。
  - AList 下载到本地时，同名文件直接覆盖，不再生成 `(1)`、`(2)` 副本。
- **依据**:
  - 用户明确要求“可以重复上传和重复下载（不要怕覆盖）”。
  - 当前场景更看重手动重复执行的自由度，而不是系统主动防重。

## 2026-03-30: 上传进度内嵌到 `Upload Local` 列表项
- **决策**:
  - 不再为上传进度单独保留一个 `Upload Tasks` 页签。
  - 每个本地下载条目直接展示最近一次上传任务的状态、进度、失败信息和目标路径。
- **依据**:
  - 用户希望上传状态与本地文件上下文绑定，避免在多个菜单之间来回切换。
  - 对于“选择文件 -> 立即上传 -> 看进度”的主流程，内嵌式展示更顺手。

## 2026-03-30: YouTube 播放失败时清空状态，不回退到上一首
- **决策**:
  - `MusicPlayerService.PlayInternalAsync(...)` 在解析流失败时，会停止当前播放管线并清空 `CurrentVideo`、`CurrentStreamUrl` 以及时间/播放状态。
- **依据**:
  - 用户反馈网络或受限视频导致播放失败时，Home 页会跳回之前播放的内容，形成明显错误心智。
  - 失败时清空状态比保留旧状态更符合用户预期，也更利于后续叠加友好的错误提示。

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
  - Windows 端通过 `Platforms/Windows/MainWindow.xaml` + `App.xaml.cs` 平台分支接管默认窗口；
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

## 2026-03-25: 轻量 UI 设置统一走 `Preferences`，不单独建设置表
- **决策**:
  - 主题索引、`Favorites Image`、`MediaTitleTwoLines` 等轻量设置统一收口到 `UiPreferencesService`。
  - 不额外建立 SQLite `Settings` 表。
- **依据**:
  - 当前设置项数量少、类型简单（`bool` / `int`），`Preferences` 的键值模型更轻更稳。
  - 收藏/下载/播放历史才是结构化数据，更适合继续走 SQLite。

## 2026-03-25: 底部导航控制在 5 个以内，低频页收进 `Other`
- **决策**:
  - 底部导航将原 `Transfers` 改为 `Other`。
  - `Other` 页面承接下载任务页、历史播放列表等低频入口。
  - 下载任务页内部使用页内二/三级筛选，不再继续膨胀底部主导航。
- **依据**:
  - Android 小屏下超过 5 个主入口会快速进入“挤、缩、易误触”的状态。
  - 页内层级导航比继续堆叠全局导航更可维护。

## 2026-03-25: Android 视频播放以原生全屏为主，Blazor 视频页只做非安卓/兜底
- **决策**:
  - Android 继续使用 `VideoPlayerActivity` 作为视频主播放路径。
  - 安卓场景下 `/player` 默认留在 `/player/audio`，不再把 `/player/video` 当主场景。
  - 退出原生视频时增加 `PlaybackStopped` 事件链，保证 `MusicPlayerService` 能退出假视频态。
- **依据**:
  - 原生全屏 Activity 已经承担实际视频播放职责，再强行让 Blazor `/player/video` 承接主流程会造成双重心智模型。
  - 之前 Android 上出现“退出原生视频后页面仍显示 ExoPlayer 播放中、且无法继续播放”的问题，本质是退出事件未回传到服务层。

## 2026-03-25: 播放历史先做运行期统一入口，再决定是否落库
- **决策**:
  - 在 `MusicPlayerService` 中维护运行期内的 `PlaybackHistory`，并统一由播放器服务记录。
  - 暂不先建 SQLite 表，先确保入口统一。
- **依据**:
  - Home/Search、Favorites、Library/Download、本地文件播放路径此前不完全一致，优先解决“有没有记历史”的一致性问题。
  - 运行期内存方案改动小、验证快，等体验确认后再决定是否持久化到 SQLite。
