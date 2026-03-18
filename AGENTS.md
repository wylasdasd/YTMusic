# YTMusic：Agent 约定（当前代码基线）

## 仓库结构
- `YTMusic/`：.NET MAUI Blazor 主应用
  - `Components/Layout/`：全局布局（`MainLayout.razor`、`GlobalAudioPlayer.razor`）
  - `Components/Pages/`：页面组件与对应 VM
  - `Services/`：业务服务（播放器、下载、全局状态等）
  - `wwwroot/js/`：前端脚本（含 `ytmLayout.js`、`audioPlayer.js`）
- `CommonHelp/`：共享类库

## 构建与验证
- Windows 快速构建：`dotnet build YTMusic/YTMusic.csproj -c Debug -f net10.0-windows10.0.19041.0`
- 若可执行文件被占用（VS 正在调试），使用临时输出验证：
  - `dotnet build YTMusic/YTMusic.csproj -c Debug -f net10.0-windows10.0.19041.0 -o YTMusic/bin/Debug/net10.0-windows10.0.19041.0/win-x64-temp`

## 通用开发约定
- 命名空间与 `RootNamespace`（`YTMusic`）及目录结构一致。
- Razor 组件命名空间与 `YTMusic/Components/**` 对齐（例如 `YTMusic.Components.Pages`）。
- 组件与 VM 同目录：
  - ViewModel 文件必须以 `VM.cs` 结尾（例如 `Search.razor` -> `SearchVM.cs`）。
  - 禁止跨目录放置组件与对应 VM。
- 优先复用 `CommonHelp` 现有能力，避免新增零散工具函数。
- 避免无关格式化，尽量保持原有代码风格与换行。

## UI/布局强约束（按当前实现）
- 顶部栏：`MainLayout` 中保留品牌、搜索入口、三横杠菜单按钮。
- 主题切换：
  - 点击三横杠打开右侧主题侧边栏（不是直接切换）。
  - 主题由侧边栏列表选择，当前内置 5 套（含 2 套亮色）。
  - 主题逻辑在 `MainLayout.razor` 中维护，使用 `ThemePreset`。
- 底部导航：
  - 全端固定在最底部（`position: fixed; bottom: 0`），不能回退到侧边 Rail。
  - 需要兼容输入法弹出场景：依赖 `wwwroot/js/ytmLayout.js` 的 `visualViewport` 修正。
- 移动端安全区：
  - Web 样式使用 `env(safe-area-inset-*)`。
  - 原生层在 `MainPage.xaml.cs` 已补充 iOS/Android 顶部安全区处理，修改时不要破坏。

## Home/Search 页约定
- Home（Search 页初始态）不显示页面标题与引导提示块。
- 搜索框回车搜索需保证“所见即所得”：
  - `Immediate="true"` + `OnKeyUp` 触发。
- 搜索页样式主要由 `Search.razor.css` 管理，颜色优先跟随布局定义的 CSS 变量。

## 稳定性约定
- 主题索引必须防越界：
  - 读取主题统一走安全访问器（如 `ActiveTheme`）。
  - 选择主题时先做边界校验。
- 修改 `MainLayout` 时，优先保证以下行为不回归：
  - 顶栏元素不越界（手机小屏）
  - 底部导航在键盘弹出/收起后可回到底部
  - 主题抽屉可打开/关闭、可重复切换

## 禁止项
- 未明确要求，不新增/删除 NuGet 包，不安装 workloads。
- 不将底部导航改回仅移动端显示。
- 不在未沟通的情况下移除已有主题或主题侧边栏交互。
