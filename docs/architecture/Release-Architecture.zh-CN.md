# 发布架构

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](Release-Architecture.md)

可分发的组件包含三个相互独立的层：

```text
MCP Server (net8.0-windows)
  |-- UIA/UIA Worker 用于动作和有界探测
  |-- RuntimeContracts (netstandard2.0) DTOs
  `-- 目标应用中的 RuntimeBridge (net472; net48; net8.0-windows)

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
RuntimeContracts 和 RuntimeBridge 分别独立生成。服务器包内嵌未发布的 Rendering
程序集（`Rhombus.WinFormsMcp.Rendering.dll`），使其依赖闭包仅限于三个已发布包
加上 nuget.org 依赖。

`scripts/package-local.ps1` 是可复现的本地组装路径。它只构建包和归档；发布由现有
的发布工作流负责，位于无人值守开发范围之外。`scripts/pack-nuget.ps1` 是本地打包、
CI 包检查和发布工作流共用的统一打包步骤；在判定任何产物有效之前，它会校验包名、
版本、目标框架资产以及必需的项目间依赖。CI 与发布工作流只打包一次，把同一个包目录
交给两个 .NET Framework 4.7.2 消费者 E2E，并发布门禁验证过的同一组 `.nupkg`，
门禁通过后不再重新打包。

仓库采用 `main` 加短生命周期 feature/release 分支，不存在永久 `dev` 分支。稳定发布由
`main` 触发；Beta 仅允许手动触发，且必须从界面中选定的非 `main` 分支运行，避免 Beta
版本提交意外触发稳定发布工作流。

## NuGet 发布顺序与失败语义

将来真正发布时，三个包按依赖顺序推送——先 `Rhombus.WinFormsMcp.RuntimeContracts`，
再 `Rhombus.WinFormsMcp.RuntimeBridge`，最后 `Rhombus.WinFormsMcp`（服务器），每次都带
`--skip-duplicate`。NuGet **不提供跨包事务**：部分完成的推送（例如 Contracts 成功但
Bridge 失败）无法自动回滚。`--skip-duplicate` 让同一版本的失败重跑是安全的，但发布
工作流必须把任何失败的推送视为需要人工对账的点，而不能假设三个包是原子一致的。
