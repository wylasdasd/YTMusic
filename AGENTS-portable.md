# MAUI Blazor 分层项目：Agent 约定（可移植版）

> 来源：YTMusic 2026-07 架构整理与播放子系统拆分经验  
> 用途：**复制到其他项目**时作为 Agent / 协作者约定起点；不替代各项目自己的 `AGENTS.md`（项目内基线文档仍以仓库内 `AGENTS.md` 为准）。

---

## 简介

本文档总结一次典型的 **UI → BLL → DAL** 分层落地过程，重点说明：

1. **为什么**要把业务、数据、视图交互拆开  
2. **ViewModel / ViewLogic / ViewModel（展示模型）/ Mapper** 四类东西不要混在一个文件夹里  
3. **Adapters** 与 **Infrastructure** 在各层分别放什么  
4. 给 Agent 的可复用提示词（文末）

适用技术栈：**.NET MAUI + Blazor**、MudBlazor、SQLite + Dapper、DI 扩展方法注册。

---

## 推荐分层（通用模板）

```
App（UI / MAUI Blazor）
  ├── Components/Pages/    # {Page}.razor + {Page}VM.cs + {Page}.razor.css 同目录
  ├── Components/Layout/   # MainLayout.razor + MainLayoutVM.cs 等同目录
  ├── ViewModels.Shared/   # 跨页共享 ViewLogic（PlayerVM、ViewModelBase、ThemePresets）
  ├── ViewModels/          # （可选）仅供 View 绑定的展示 DTO
  ├── Adapters/            # 实现 BLL Ports（平台 / UI 框架适配）
  ├── Infrastructure/      # UI 层技术实现（代理、路径、平台细节）
  ├── Services/            # 仅保留 UI 壳层 / 实时状态机（如播放器）
  └── Mappers/             # BLL/DTO → UI 展示模型（可选，见下文）

BLL（业务逻辑）
  ├── Abstractions/        # I*Service
  ├── Abstractions/Data/   # I*Repository（接口归 BLL，实现归 DAL）
  ├── Services/            # 业务编排
  ├── Models/              # 领域 / 持久化模型
  ├── Ports/               # 需 UI 或外部技术实现的抽象
  ├── Infrastructure/      # YoutubeExplode、HTTP、文件系统等技术实现
  └── Mappers/             # Entity/DTO ↔ BLL Model 也可以是其他数据转换（可选）

DAL（数据访问）
  ├── Repositories/
  ├── Infrastructure/      # SqliteConnectionFactory、Migration
  └── Mappers/             # DB Row ↔ BLL Model  也可以是其他数据转换（可选）

CommonHelp                 # 无业务语义并且具有普适性的共享工具类库和方法库和 极为基础的常用的model 异常类....
```

**依赖方向（必须遵守）：**

| 调用方 | 允许 | 禁止 |
|--------|------|------|
| UI | BLL 接口与模型；在 `MauiProgram` 注册 DAL | Razor / ViewLogic 直接 `using DAL`、直接 SQL |
| BLL | CommonHelp、自身 Abstractions/Ports | MAUI、Blazor、SQLite、DAL 实现类 |
| DAL | BLL `Abstractions/Data`、AppGlobal 常量 | 业务判断、UI API |

---

## 命名说明：ViewModel vs ViewLogic vs 展示 Model vs Mapper

这是本次整理最重要的认知，**比文件夹叫 `ViewModels` 还是 `ViewLogic` 更重要**。

### 四个概念（理想分工）

| 概念 | 英文建议 | 放哪 | 职责 | 不应包含 |
|------|----------|------|------|----------|
| **视图展示模型** | View Model（展示模型） | 各层 `ViewModels/` 或 `Presentation/` | 仅供 View 绑定的数据结构：列表项、表单字段、只读展示字段 | 调 Service、弹窗、导航、JS interop |
| **视图交互逻辑** | View Logic | 与对应 `.razor` 同目录的 `*VM.cs`；**共享**放 `ViewModels.Shared/` | 用户操作编排：调 BLL、确认对话框、Toast、加载态、刷新列表 | 业务规则、SQL、播放器状态机 |
| **业务模型** | Domain / BLL Model | `BLL/Models/` | 收藏、下载记录、播放历史等领域对象 | MudBlazor、Razor、平台 API |
| **模型转换** | Mapper / Projection | **各层各自** `Mappers/` | `PlaybackHistoryRecord` → `HistoryListItem`；DB 行 → BLL Model | 业务判断、UI 交互 |

### 为什么业界仍普遍叫 VM？

- 来自 MVVM 历史习惯，**.NET / MAUI / Blazor 生态默认叫 ViewModel**
- 教程、招聘、搜索都认这个词，沟通成本低
- 实际项目里的 `SearchVM` 往往是 **View Logic + 少量展示状态** 的混合体

### 本项目的务实约定（YTMusic 2026-07）

**页面 VM 与 Razor 同目录；共享 VM 放 `ViewModels.Shared/`：**

```
Components/Pages/History.razor
Components/Pages/HistoryVM.cs
Components/Pages/History.razor.css   # 若有样式

Components/Layout/MainLayout.razor
Components/Layout/MainLayoutVM.cs

ViewModels.Shared/PlayerVM.cs        # PlayerAudio + PlayerVideo 共用
ViewModels.Shared/ViewModelBase.cs
ViewModels.Shared/ThemePresets.cs
```

命名空间：`YTMusic.Components.Pages`、`YTMusic.Components.Layout`、`YTMusic.ViewModels.Shared`。

> **`XxxVM` = View Logic（页面交互层）**，不是经典 MVVM 全状态 ViewModel。

**若将来拆展示 DTO**（可选）：

```
ViewModels/SearchItem.cs              # 仅 UI 绑定字段
Mappers/SearchResultMapper.cs
```

### ViewLogic 典型职责（以播放器页为例）

- 查收藏状态 → `IFavoriteService`
- 打开收藏夹弹窗 → `IDialogHost`
- 触发下载 → `IDownloadManagerService`
- 确认「是否播在线视频」→ `IDialogHost.ConfirmRemoteVideoPlayAsync`

**留在 Razor / UI Service 的：**

- `MusicPlayerService` 播放状态、切歌、进度（实时状态机）
- `audioPlayer.mountProgressBar`、全屏、路由跳转
- 窗口拖拽、底部导航高亮

### Mapper 层为什么「理论上每个类库各一份」

避免：

- BLL Model 泄漏 Mud 专用字段到数据库层  
- Razor 里手写 `new PlayingItem { ... }` 到处复制  
- DAL 实体与 UI 卡片字段耦死

示例：

| 层 | Mapper 做什么 |
|----|----------------|
| DAL `Mappers/` | `PlaybackHistoryRow` ↔ `PlaybackHistoryRecord` |
| BLL `Mappers/` | 外部 API 原始结构 ↔ `BLL.Models` |
| UI `Mappers/` | `PlaybackHistoryRecord` → `HistoryListItem`（若与 BLL 字段不同） |

当前 YTMusic 尚未单独建 UI `Mappers/`，History 页由 `HistoryVM` 直接使用 `PlaybackHistoryRecord`——**可接受的小项目做法**；字段分化后再抽 Mapper。

---

## 各文件夹职责（Adapters vs Infrastructure）

### UI 层

| 目录 | 职责 | 示例 |
|------|------|------|
| `Adapters/` | 实现 **BLL 定义的 Ports** | `MudUiNotifier` → `IUiNotifier`；`MudDialogHost` → `IDialogHost` |
| `Infrastructure/` | UI/播放 **技术实现**，不必跨层接口 | `LocalAudioProxy`、`StoragePaths` |
| `Services/` | **仅** UI 壳层 + 实时状态 | `MusicPlayerService`、`UiPreferencesService` |

**判断口诀：** 如果是 BLL `Ports/` 里的接口 → `Adapters/`；如果是 HttpListener、平台路径、播放管线内部件 → `Infrastructure/` 或 `Services/Playback/`。

### BLL 层

| 目录 | 职责 |
|------|------|
| `Services/` | 业务编排（收藏、下载、历史、YouTube 搜索） |
| `Infrastructure/` | `IYouTubeApiClient`、`IFileSystem`、AList HTTP 等 **技术实现** |
| `Ports/` | 需要 UI 或 OS 实现的抽象（`IUiNotifier`、`IDatabasePathProvider`） |

### DAL 层

| 目录 | 职责 |
|------|------|
| `Repositories/` | 实现 BLL `I*Repository` |
| `Infrastructure/` | SQLite 连接、Bootstrap、**列级迁移**（`PRAGMA table_info` 防重复 ALTER） |

**仓储接口定义在 BLL，实现只在 DAL**——这样 UI 永远见不到 SQLite。

---

## 本次 YTMusic 修改经验总结（做了什么 & 为什么）

### 1. 播放子系统拆分（MusicPlayerService 减负）

**做了什么：**

- 抽出 `PlaybackStreamResolver`（`IYouTubeApiClient` 解析 manifest/流）
- 抽出 `PlaybackProxyCoordinator`（本地 HTTP 代理生命周期）
- `PlayingItem` 等移至 `PlaybackItemModels.cs`
- 删除 `MusicPlayerService` 内重复的 `YoutubeClient` 与代理方法

**为什么：**

- 单文件 1300+ 行同时管状态机、网络解析、代理、历史 → 难测、难改、易回归
- YouTube 解析应走 BLL `IYouTubeApiClient`，UI 不应再 `new YoutubeClient()`
- 播放器服务应专注 **IPlaybackHost 状态源 + 切歌编排**

### 2. 播放历史落库（BLL + DAL，不是 UI 新层）

**做了什么：**

- `PlaybackHistoryRecord`（BLL Model）
- `IPlaybackHistoryRepository` + `PlaybackHistoryRepository`（DAL 表 `PlaybackHistory`）
- `IPlaybackHistoryService` / `PlaybackHistoryService`
- `MusicPlayerService` 仅 `RecordPlayAsync`；`AppResetService` 清空时 `ClearAsync`

**为什么：**

- 内存列表重启即失，且 History 页与播放器耦死
- 持久化属于 **数据层职责**，不能放在 Razor 或 `MusicPlayerService` 私有 List
- UI 通过 `HistoryVM` 调 BLL，**不** `using DAL`

### 3. Player / History / MainLayout 补 VM（统一走 BLL）

**做了什么：**

- `HistoryVM` → `IPlaybackHistoryService`
- `PlayerVM` → 收藏、下载、远程视频确认（`IDialogHost`）
- `MainLayoutVM` → 主题、还原默认、网络错误提示；主题数据抽 `ThemePresets.cs`
- 扩展 `IDialogHost`：`ConfirmAppResetAsync`、`ConfirmRemoteVideoPlayAsync`

**为什么：**

- Razor `@code` 膨胀后重复注入 `IFavoriteService`、`IDialogService`
- 统一 **Ports 适配**（`IUiNotifier`、`IDialogHost`）避免页面直接绑 MudBlazor
- 布局 600+ 行主题/重置逻辑下沉，Razor 只留窗口拖拽与导航

### 4. BLL Infrastructure 前置（会话早期）

**做了什么：**

- `YoutubeExplodeClient` → `IYouTubeApiClient`
- `LocalFileSystem` → `IFileSystem`
- AList HTTP 工具迁入 `BLL/Infrastructure/AList/`

**为什么：**

- 业务服务不应直接依赖具体 SDK/HTTP 细节
- 方便单测替换 Fake Client

### 常见坑（本次亲历）

1. **大文件 partial 重构**时删除重复方法易截断文件 → 改完必须 `dotnet build`
2. **IPlaybackHost 显式实现**与私有方法重复 → 只保留 delegate 到 Coordinator 的一份
3. **MainLayout 主题数组**数百行放 Razor → 抽静态 `ThemePresets` 才能给 ViewLogic 用
4. **AGENTS 写「主题在 MainLayout」**与代码漂移 → 项目内 `AGENTS.md` 跟代码一起更新

---

## DI 注册顺序（模板）

```csharp
builder.Services.AddYTMusicDal();   // Repository 实现
builder.Services.AddYTMusicBll();   // 业务服务 + Infrastructure
// UI 单例：播放、偏好、代理协调器
builder.Services.AddSingleton<PlaybackProxyCoordinator>();
builder.Services.AddSingleton<PlaybackStreamResolver>();
builder.Services.AddSingleton<MusicPlayerService>();
// UI 适配器与 ViewLogic（Scoped）
builder.Services.AddScoped<IUiNotifier, MudUiNotifier>();
builder.Services.AddScoped<IDialogHost, MudDialogHost>();
builder.Services.AddScoped<HistoryVM>();
```

---

## 构建验证

```bash
dotnet build App/App.csproj -c Debug -f net10.0-windows10.0.19041.0
# 可执行文件被占用时：
dotnet build App/App.csproj -c Debug -f net10.0-windows10.0.19041.0 -o App/bin/.../win-x64-temp
```

---

## 禁止项（Agent 通用）

- 未明确要求：不新增 NuGet、不装 workload
- UI 不直接 SQLite / Dapper / Repository
- BLL 不引用 MAUI / Blazor / DAL 实现
- 不在 ViewLogic 里写业务规则（应加在 BLL Service）
- 不为了「准确命名」做全仓库 VM → ViewLogic 重命名（除非用户明确要求）
- 只改文档时：**不要动代码**

---

## Agent 提示词（复制即用）

### 新项目搭分层

```
请按 UI → BLL → DAL 分层 scaffold MAUI Blazor 项目：
- BLL：Abstractions/I*Service、Abstractions/Data/I*Repository、Models、Ports、Services、Infrastructure
- DAL：Repositories、Infrastructure（Sqlite + Dapper + 列迁移）
- UI：Components、ViewLogic（*VM.cs）、Adapters（实现 Ports）、Infrastructure、MauiProgram 先 Dal 后 Bll
- 写一份 AGENTS.md 说明依赖方向与 ViewLogic 边界
不要 UI 直接访问 DAL。
```

### 页面逻辑下沉到 ViewLogic

```
把 {Page}.razor 里的 @code 业务交互抽到同目录 {Page}VM.cs：
- VM 命名空间 YTMusic.Components.Pages（或 Layout）
- VM 只调 BLL I*Service 与 Ports（IUiNotifier、IDialogHost）
- 多页共用 VM 放 ViewModels.Shared/（如 PlayerVM）
- 保留 Razor 内：布局、JS interop、NavigationManager、ElementReference
- OnInitialized 设 VM.StateHasChanged = StateHasChanged
不要往 VM 塞播放器状态机或 SQL。
```

### 胖 Service 拆分

```
{Service} 超过 800 行且混合多职责时：
1. 列出职责：状态机 / 外部 API / 技术代理 / 持久化
2. 状态机留原 Service；API 走 BLL Ports；代理抽 Infrastructure；持久化走 BLL+DAL
3. 原 Service 只保留编排与 IHost 实现
4. 注册 DI 后 dotnet build 验证
每步说明为什么拆、拆到哪一层。
```

### 新增持久化功能

```
新增 {Feature} 持久化：
- BLL：Models/{Entity}.cs、Abstractions/Data/I{Entity}Repository、I{Feature}Service、Services 实现
- DAL：Repositories/{Entity}Repository、SqliteSchemaMigration 补列
- UI：{Feature}VM 调 I{Feature}Service，不 using DAL
- AppReset 若需清空则注入 I{Feature}Service.ClearAsync
不要建 UI 专属 SQLite 文件。
```

### 只改文档

```
只更新文档，不要改任何代码。
对照当前代码库修订 {AGENTS.md / ARCHITECTURE.md}，并单独写 AGENTS-portable.md 经验总结：
含 ViewModel vs ViewLogic vs Mapper 说明、本次重构做了什么、为什么、可复用 Agent 提示词。
```

### 命名澄清（给 Agent 的硬约束）

```
本项目 *VM.cs 语义是 ViewLogic（页面交互编排），不是经典 MVVM 全状态 ViewModel。
- ViewLogic：调 BLL、弹窗、Toast、列表刷新
- ViewModels/（若存在）：仅 UI 绑定用的展示 DTO
- Mappers/：各层模型转换，不放业务判断
播放实时状态留在 MusicPlayerService，不迁入 PlayerVM。
```

---

## 附：与项目内 `AGENTS.md` 的关系

| 文件 | 作用 |
|------|------|
| `AGENTS.md`（仓库内） | **当前项目**代码基线、路径、禁止项、播放架构细节 |
| `AGENTS-portable.md`（本文件） | **跨项目**经验模板、命名哲学、重构复盘、提示词 |

复制到新项目时：先拿本文件作骨架，再按实际目录名、TFM、业务模块改成新项目自己的 `AGENTS.md`。

---

*文档版本：2026-07-05 · 对应 YTMusic 播放子系统拆分 + History/Player/MainLayout VM + PlaybackHistory 落库*
