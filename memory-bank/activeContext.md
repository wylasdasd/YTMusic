# 活动上下文 (Active Context)

> 最后更新：2026-07-05

## 当前焦点

- **分层架构**：`YTMusic`（UI）/ `YTMusic.BLL` / `YTMusic.DAL` / `CommonHelp`；约束见 [`../AGENTS.md`](../AGENTS.md)。
- **AppGlobal**：BLL 与 UI 各一份，集中常量与 `Runtime` 状态；成员需 `///` 注释。
- **播放架构**：`MusicPlayerService` + `PlaybackSwitcher` + 五种 `IPlaybackInstance`；详见 [playbackArchitecture.md](./playbackArchitecture.md)。
- **Android**：在线视频 `NativeVideo` + ExoPlayer；音频 `PlaybackForegroundService`。
- **AList**：业务在 BLL；设置键 `AppGlobal.AList.PreferenceKeys`。

## 最近有效能力（维持现状）

| 领域 | 状态 |
|------|------|
| 播放器进度条 | JS `ytm-player-progress`，非 MudSlider |
| 页面滚动 | `PageListScroll` + `ytmLayout._pageScrolls` 内存缓存 |
| 已下载切歌 | Web 完整 URL 比较 + `&f=`；Android `ClearMediaItems` |
| 在线视频弹窗 | 确认后才 loading，无弹窗前 manifest 预检 |
| 主题/设置 | `UiPreferencesService`：主题、两行标题、分离流画质、预检开关 |
| Windows 窗口 | `MainWindow` + `WindowChromeService` + `mouseInterop.js` |
| 播放历史 | 运行期内存，未落 SQLite |

## 进行中 / 待验证

- [ ] Android 已下载切歌实机回归（列表 + 通知栏）。
- [ ] 锁屏媒体控件不同 ROM 一致性。
- [ ] Player 页面 UI 继续优化。
- [ ] Windows Hybrid 分离流（无原生音频）体验评估。

## 下一步计划

- `PlaybackHistory` SQLite 持久化。
- 补充不依赖网络的单元测试。
- YouTube 下载入队前网络/VPN 探测（播放侧已有 `NetworkErrorService`）。

## 改代码前必读

1. [playbackArchitecture.md](./playbackArchitecture.md) — 路由与勿回归清单  
2. [decisionLog.md](./decisionLog.md) — 历史决策  
3. [../AGENTS.md](../AGENTS.md) — Agent / 协作者约定
