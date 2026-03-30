# 项目进度 (Progress)

## 已完成
- [x] AList 上传设置持久化（`BaseUrl`、`Token`、`RemoteDirectory`）。
- [x] 上传页设置面板默认收起，并支持 `Create Directory` 创建 AList 目录。
- [x] `Upload Local` 支持基于本地已下载文件批量上传到 AList。
- [x] 上传目录约定为 `Remote Directory/<歌名md5>`，上传前自动 `mkdir`。
- [x] AList 上传支持主音视频 + 封面一起上传。
- [x] AList 浏览页 `AList Files`：支持目录浏览、返回上级、单文件下载、整目录下载。
- [x] AList 目录整首下载按本地下载数据库逻辑入库（主媒体 + 封面）。
- [x] AList 下载封面改存 `data:image/...;base64,...`，修复下载页/播放页封面显示问题。
- [x] `DownloadedTracks` 新增上传与远端来源追踪字段（`HasUploaded`、`UploadedDate`、`UploadedRemotePath`、`RemoteSourcePath`）。
- [x] 上传状态直接内嵌到 `Upload Local` 文件列表项中，移除单独的上传任务页签。
- [x] AList 上传与目录下载允许重复执行；本地同名下载默认覆盖。
- [x] `AList Files` 目录展示直接读取主音视频文件名作为显示名。
- [x] 修复不可播放 YouTube 视频导致播放器回退到上一首的状态残留 bug。
- [x] 基础搜索与播放 (YouTube API 集成)。
- [x] 用于流媒体的本地音频代理。
- [x] 收藏夹文件夹及歌曲持久化。
- [x] 本地音频文件播放支持。
- [x] 顺序播放、随机播放列表逻辑。
- [x] 播放模式 UI (图标与切换逻辑)。
- [x] 优化：无缝单曲循环（避免 Loading 重载）。
- [x] Android 13+ 通知权限请求链路恢复（`POST_NOTIFICATIONS`）。
- [x] Android 后台播放通知链路稳定化（恢复前台通知与进度更新）。
- [x] 清理无效通知权限服务抽象，降低排障复杂度。
- [x] 实机确认并固化“平台原生 `Notification.MediaStyle + MediaSession.Token`”方案，恢复上一首/下一首显示。
- [x] Windows 自定义窗口壳层接入（`MainWindow.xaml` + `MauiProgram` 生命周期配置）。
- [x] Windows 顶栏窗口控制按钮与拖拽热区接入（`WindowChromeService` + `mouseInterop.js`）。
- [x] 修复 Windows 拖拽导致窗口尺寸变化的问题（改用 `SetWindowPos(...SWP_NOSIZE...)`）。
- [x] 调整 Windows 顶栏布局：删除搜索胶囊、三横杠移至窗口按钮组左侧、顶栏全宽贴边。
- [x] 三横杠设置持久化：主题、`Favorites Image`、`High Quality Audio`。
- [x] “还原默认”重置链路：清表、删下载目录、恢复默认设置、停止播放。
- [x] 底部导航重构：`Transfers` 收进 `Other`，下载页使用页内二/三级筛选。
- [x] `Other` 页面新增“历史播放列表”入口。
- [x] 运行期播放历史列表（`MusicPlayerService.PlaybackHistory`）接入。
- [x] Android 原生视频退出后恢复状态链路修复（`PlaybackStopped`）。
- [x] 安卓视频主路径调整为原生全屏；Blazor `/player/video` 在安卓上弱化为兜底。

## 进行中
- [x] 初始化中文化 Memory Bank。
- [ ] 评估 YouTube 下载的网络/VPN 失败提示与入队前探测方案。
- [ ] Player 页面继续向 YouTube Music 风格靠拢（封面、视频区、标题区、平台差异样式）。
- [ ] Player 视频页在 Windows/Android 的最终居中与视觉重心校准。
- [ ] 锁屏媒体控件在不同 ROM 的一致性验证与适配。

## 未来路线图
- [ ] 高级均衡器 (Equalizer) 设置。
- [ ] 离线歌词支持。
- [ ] 更好的 Android/iOS SDK 适配（针对移动端构建）。
- [ ] 收藏夹中的批量操作功能。
- [ ] 播放历史 SQLite 持久化（跨重启保留）。
