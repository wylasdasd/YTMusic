# YTMusic：Agent 约定

## 仓库结构
- `YTMusic/`：.NET MAUI Blazor 应用（Razor 组件位于 `YTMusic/Components/`）
- `CommonHelp/`：共享类库

## 构建
- Windows 快速构建：`dotnet build YTMusic/YTMusic.csproj -c Debug -f net10.0-windows10.0.19041.0`
- 构建整个解决方案：`dotnet build YTMusic/YTMusic.slnx -c Debug`

## 约定
- C# 命名空间与 `RootNamespace`（`YTMusic`）及目录结构保持一致
- Razor 组件命名空间与 `YTMusic/Components/**` 目录保持一致（例如 `YTMusic.Components.Pages`）
- 优先复用现有 `CommonHelp` 代码；避免随手新增零散工具函数；确需新增请放在 `CommonHelp/` 下
- 组件与 VM 同目录：所有 Blazor 组件（`.razor`）及其对应的 ViewModel 必须放在同一个文件夹下，禁止跨目录存放
  - ViewModel 文件名必须以 `VM.cs` 结尾（例如 `Counter.razor` → `CounterVM.cs`）
  - 推荐使用 `partial class`（分部类）或依赖注入方式，将 ViewModel 与 `.razor` 视图解耦
- 避免无关格式化；尽量保持现有编码/换行风格

## 主题与布局

### 主题色

用于 MudBlazor 主题和控件但不要滥用。

| 角色 | 颜色预览 | 十六进制 |
| :--- | :--- | :--- |
| Primary | ![#111111](https://via.placeholder.com/15/2F6DF6/2F6DF6.png) | `#111111` |
| Secondary | ![#0E7490](https://via.placeholder.com/15/0E7490/0E7490.png) | `#0E7490` |
| Tertiary/Accent | ![#F59E0B](https://via.placeholder.com/15/F59E0B/F59E0B.png) | `#F59E0B` |
| Surface | ![#FFFFFF](https://via.placeholder.com/15/FFFFFF/FFFFFF.png) | `#FFFFFF` |
| Background | ![#F6F7FB](https://via.placeholder.com/15/F6F7FB/F6F7FB.png) | `#F6F7FB` |
| Text Primary | ![#0F172A](https://via.placeholder.com/15/0F172A/0F172A.png) | `#0F172A` |
| Text Secondary | ![#475569](https://via.placeholder.com/15/475569/475569.png) | `#475569` |
| Divider | ![#E2E8F0](https://via.placeholder.com/15/E2E8F0/E2E8F0.png) | `#E2E8F0` |


## 安全
- 未明确要求时，不要随意新增/删除 NuGet 包或安装工作负载（workloads）
