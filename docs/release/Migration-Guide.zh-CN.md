# 迁移指南

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](Migration-Guide.md)

## 从纯 UIA 自动化迁移

现有的 UIA 工具仍然兼容。继续使用 `find_element`、`get_element_tree` 以及交互
类工具执行动作。RuntimeBridge 是一个可选的只读理解层；未接入它的应用仍然可以通过
UIA 正常工作。

要在目标 WinForms 应用中启用托管检查，请引用桥接包（`Rhombus.WinFormsMcp.RuntimeContracts`
会作为传递依赖自动引入；显式引用它可以把版本写清楚，但不是必须的）：

```xml
<PackageReference Include="Rhombus.WinFormsMcp.RuntimeBridge" Version="1.5.12-beta" />
```

.NET Framework 4.7.2、4.8 和 .NET 8 Windows 应用可以引用同一个包；NuGet 会自动选择
匹配的 `net472`、`net48` 或 `net8.0-windows` 资产。

在 `Form.Shown`（此时窗体句柄已创建）中启动桥接，并在窗体关闭时停止它：

```csharp
form.Shown += (_, _) => McpRuntimeBridge.StartForControl(form);
form.FormClosed += (_, _) => McpRuntimeBridge.Stop();
```

`StartForControl(form, options)` 把桥接绑定到指定控件：

- `null` 抛出 `ArgumentNullException`。
- 已释放或正在释放的控件抛出 `ObjectDisposedException`。
- 尚未创建窗口句柄的控件抛出 `InvalidOperationException`，消息会建议在
  `Form.Shown` 中调用。
- 首次成功启动后绑定该控件；后续调用返回现有 Host，不会更换调度目标。

旧入口 `McpRuntimeBridge.Start(options)` 保持源码和二进制兼容。没有正在运行的 Host
时，它只接受已打开的窗体或可确认的 WinForms UI 同步上下文；两者都没有时立即抛出
带 `StartForControl` 迁移示例的 `InvalidOperationException`，而不是在管道线程上访问
控件。桥接绝不回退到跨线程控件访问：没有 UI 调度目标、或绑定控件已失效的请求都会
明确失败。

### 传统（非 SDK）的 .NET Framework 项目

通过 `PackageReference` 引用桥接包的传统 `.csproj` 项目应启用自动绑定重定向，确保
`System.Text.Json` 依赖闭包在运行时能解析到正确版本：

```xml
<PropertyGroup>
  <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
  <GenerateBindingRedirectsOutputType>true</GenerateBindingRedirectsOutputType>
</PropertyGroup>
```

SDK 风格项目针对 .NET Framework 目标时也应设置同样的属性。仓库会针对刚打包的包
验证这两种项目形态（`scripts/verify-net472-consumers.ps1`）。

桥接只返回快照 DTO。它不会跨管道暴露 setter、任意方法调用、反射执行或业务对象。

## 运行时标识

诸如 `ctrl_18` 这样的托管 ID 作用于某个进程和桥接实例。请把 `runtime_status` 或
托管标识返回的 `processId` 和 `bridgeInstanceId` 与该 ID 保存在一起。在重放已保存
的引用时，把 `bridgeInstanceId` 传给现有的运行时和诊断工具。标识不匹配意味着应用
已经重启；此时应刷新托管树，而不是重试过期的控件或事件跟踪 ID。

`bridgeInstanceId` 字段是可选的，以兼容旧客户端和旧桥接。省略它会保留旧有行为，
而提供它则启用严格的过期引用保护。

## 目标框架

`Rhombus.WinFormsMcp.RuntimeContracts` 是单目标 `netstandard2.0` 程序集，因此
.NET Framework 4.7.2/4.8 和 .NET 8 消费者共享同一份 DTO 程序集。
RuntimeBridge 面向 `net472`、`net48` 和 `net8.0-windows`。
服务器本身面向 `net8.0-windows`；RendererHost 是多目标的，面向 `net48`、
`netcoreapp3.1` 和 `net8.0-windows`。

编译目标和运行时 CLR 是两回事：消费者针对 4.7.2 targeting pack 编译，但运行它们的
机器执行的是当前安装的 .NET Framework CLR（通常是 4.8.x）。本文档不宣称已在仅安装
原始 4.7.2 Runtime 的机器上验证；详见[兼容性矩阵](Compatibility-Matrix.zh-CN.md)。

## 发布准备

在 Release 构建之后，在 Windows 上运行 `scripts/package-local.ps1`，以创建本地
NuGet 包、一个 NPM tarball 和一个独立 ZIP。该脚本从不发布包，也不会创建 GitHub
Release。`scripts/pack-nuget.ps1` 负责打包并检查三个 NuGet 包（包名、版本、目标框架
资产、项目间依赖版本），本地打包、CI 和发布工作流共用。`scripts/verify-net472-consumers.ps1`
会针对刚打包的包端到端运行两个 .NET Framework 4.7.2 消费者。
