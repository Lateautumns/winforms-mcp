# 迁移指南

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](Migration-Guide.md)

## 从纯 UIA 自动化迁移

现有的 UIA 工具仍然兼容。继续使用 `find_element`、`get_element_tree` 以及交互
类工具执行动作。RuntimeBridge 是一个可选的只读理解层；未接入它的应用仍然可以通过
UIA 正常工作。

要在目标 WinForms 应用中启用托管检查，请引用以下包：

```xml
<PackageReference Include="Rhombus.WinFormsMcp.RuntimeContracts" Version="1.5.12-beta" />
<PackageReference Include="Rhombus.WinFormsMcp.RuntimeBridge" Version="1.5.12-beta" />
```

开发期间在 UI 线程启动桥接，并在窗体关闭时停止它：

```csharp
form.Shown += (_, _) => McpRuntimeBridge.Start();
form.FormClosed += (_, _) => McpRuntimeBridge.Stop();
```

桥接只返回快照 DTO。它不会跨管道暴露 setter、任意方法调用、反射执行或业务对象。

## 运行时标识

诸如 `ctrl_18` 这样的托管 ID 作用于某个进程和桥接实例。请把 `runtime_status` 或
托管标识返回的 `processId` 和 `bridgeInstanceId` 与该 ID 保存在一起。在重放已保存
的引用时，把 `bridgeInstanceId` 传给现有的运行时和诊断工具。标识不匹配意味着应用
已经重启；此时应刷新托管树，而不是重试过期的控件或事件跟踪 ID。

`bridgeInstanceId` 字段是可选的，以兼容旧客户端和旧桥接。省略它会保留旧有行为，
而提供它则启用严格的过期引用保护。

## 目标框架

RuntimeContracts 面向 `netstandard2.0`。RuntimeBridge 面向 `net48` 和
`net8.0-windows`。服务器本身面向 `net8.0-windows`；RendererHost 是多目标的，
面向 `net48`、`netcoreapp3.1` 和 `net8.0-windows`。

## 发布准备

在 Release 构建之后，在 Windows 上运行 `scripts/package-local.ps1`，以创建本地
NuGet 包、一个 NPM tarball 和一个独立 ZIP。该脚本从不发布包，也不会创建 GitHub
Release。
