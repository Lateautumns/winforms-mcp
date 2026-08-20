# 发布架构

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](Release-Architecture.md)

可分发的组件包含三个相互独立的层：

```text
MCP Server (net8.0-windows)
  |-- UIA/UIA Worker 用于动作和有界探测
  |-- RuntimeContracts (netstandard2.0) DTOs
  `-- 目标应用中的 RuntimeBridge (net48; net8.0-windows)

RendererHost
  |-- net48
  |-- netcoreapp3.1
  `-- net8.0-windows
```

RuntimeBridge 通过每个进程一条的 Named Pipe 与服务器通信。管道只承载协议版本化的
JSON 快照，绝不传递活动的 WinForms 或 UIA 对象。`processId`、`bridgeInstanceId`
和托管 ID 共同构成运行时标识元组。UIA 仍然是动作层；RuntimeBridge 保持只读。

独立 ZIP 包含服务器输出，并为每个受支持的渲染器目标提供一个
`rendererhost/<tfm>` 文件夹。NPM package 封装了这种 ZIP 风格的 `dist/`
目录布局，并在 Windows x64 上启动同一个可执行文件。NuGet package 则为服务器、
RuntimeContracts 和 RuntimeBridge 分别独立生成。

`scripts/package-local.ps1` 是可复现的本地组装路径。它只构建包和归档；发布由现有
的发布工作流负责，位于无人值守开发范围之外。
