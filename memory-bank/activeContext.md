# 活动上下文 (Active Context)

## 当前焦点
- **播放器进度条**：已改为 `audioPlayer.js` 托管（`ytm-player-progress`），音频/视频页共用；解决 MudSlider + Blazor 重绘导致的不跟手。本地非 `.mp4` 或未定 `IsVideo` 的文件不按视频播。
- **布局与滚动**：整页不滚；各页列表在 `PageListScroll` / Upload Tab 内滚。切换路由后滚动位置由 `ytmLayout._pageScrolls`（JS 内存）恢复，非 C#、非控件自带。
- **AList 上传/下载**：上传流程已改为 `metadata.json` 优先、仅 `coverUrl`（`thumbnailUrl`）入 JSON，不再上传封面文件；上传进度在任务完成前封顶 99%。
- **播放体验**：已下载歌曲切歌时 UI 与音频不同步的问题已修复（Web 代理 URL 去重误判 + Android ExoPlayer 切歌清理）。
- **设置与 UI**：三横杠抽屉新增「两行显示」标题偏好；上传页标签用 `BadgeData`；Android 顶栏菜单按钮右对齐。

## 最近的变更
### 播放器进度条（JS 托管）
- `audioPlayer.js`：`mountProgressBar`、`setProgress`、`seekProgressTo`、`setNativeProgressMode`；播放 rAF 更新 UI；webm 音频走 `<audio>` 非 OGV。
- `PlayerAudio` / `PlayerVideo`：移除 MudSlider，挂载 JS 进度条；视频页去掉原生 `controls`，补播放/切歌区。
- `GlobalAudioPlayer`：`OnProgressSeek`；原生 `OnTimeChanged` → `setProgress`。
- `MusicPlayerService.ShouldPlayLocalAsVideo`：仅 `.mp4` + 显式视频标记才本地视频流。
- 不跟手根因（已写入 `decisionLog.md`）：Blazor 绑 `CurrentTime` 高频 `StateHasChanged`、Mud `Immediate=false`、拖后防抖、曾用 OGV 播 webm。

### 布局与滚动
- `MainLayout`：`ytm-content` / `ytm-body` 替代 `MudContainer` 作 flex 高度链；`ytm-main` 不滚动。
- `PageListScroll.razor` + `app.css` 中 `.ytm-page__scroll`；各页 `PageKey`（如 `search`、`favorites`）。
- `ytmLayout.js`：`initPageScrollPersistence`、`saveAllPageScrolls`、`restorePageScrolls`；缓存 `window.ytmLayout._pageScrolls`。
- Upload：`ytm-page--tabs` + `data-page` 三 Tab；`initTouchScrollTabs` 横向触摸滑动。

### AList 上传/下载
- 上传顺序：`mkdir` → `metadata.json` → 主音视频文件；`metadata.json` 字段与 `DownloadedTracks` 子集对齐（`RemoteTrackMetadata`）。
- 不再上传封面文件；JSON 中只保留 `thumbnailUrl`（兼容反序列化 `coverUrl`）。
- 上传进度：metadata 约 10% + 媒体约 90%，完成前最高 99%，`Completed` 后才到 100%。
- `Upload Local`：上传进行中隐藏「Uploaded」标签；`Select All`/`Clear` 合并为 `MudToggleIconButton`；移除 Refresh；标签页 `BadgeData` 显示选中数/远端文件数。
- `Uploaded Files`：列表仅保留 md5、名称、封面、Download；`MudTabs` 使用 `MinimumTabWidth="50%"` + 短标签 `Local`/`Remote` 适配小屏。
- 远端目录下载读取 `metadata.json`（`RemoteTrackMetadata.FromDownloadedTrack` / `ToDownloadedTrack`）。

### 播放与网络
- **已下载切歌修复**：
  - `audioPlayer.js`：`loadSource` 用完整 URL 比较，避免本地代理同址不同文件被跳过。
  - `BuildLocalProxyStreamUrl`：URL 增加 `&f=文件路径` 标识。
  - `StopOtherPlaybackPipelineAsync`：切到 Web 代理前先 `OnRequestPause`。
  - Android `PlaybackForegroundService`：切歌前 `Stop()` + `ClearMediaItems()` 再 `SetMediaItem`。
- 在线播放/下载统一偏好 WebM（已移除「High Quality Audio」设置）。
- `NetworkErrorService` + `VpnSuggestionDialog`：播放/搜索失败时防重复弹窗提示 VPN。

### UI 与设置
- `MediaTitle` 组件 + `UiPreferencesService.MediaTitleTwoLines`：开启时标题最多两行+省略号，关闭时完整换行（已废弃横向滚动方案）。
- Android 非 Windows 顶栏：三横杠菜单 `ytm-theme-toggle-mobile` + `margin-left: auto`（`app.css` 全局兜底）。
- `Directory.Build.props` 从仓库根移到 `YTMusic/`，避免设计时 TFM 污染 `CommonHelp` 导致 Android NU1201。

### 历史仍有效的能力（未在本轮推翻）
- Android 原生视频主路径、`PlaybackHistory` 运行期内存、Windows 窗口壳层、`AppResetService` 还原默认等仍按既有决策运行。

## 下一步计划
- 实机验证 Android 已下载歌曲连续切歌（列表内、通知栏上一首/下一首）。
- 评估 `PlaybackHistory` SQLite 持久化。
- 视需要补充 Upload 页 `EnableDefaultCssItems=false` 下页面级 CSS 的注册方式（`*.razor.css` 需确认是否进 bundle）。
- 评估 YouTube 下载网络/VPN 失败提示与入队前探测。
