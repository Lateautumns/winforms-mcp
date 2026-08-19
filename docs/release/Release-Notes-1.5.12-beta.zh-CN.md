# Rhombus.WinFormsMcp 1.5.12-beta

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](Release-Notes-1.5.12-beta.md)

这是一份发布准备草稿，不是已发布的版本。

## 亮点

- 针对托管 WinForms 控件、布局、绑定、HWND、源映射、诊断和有界事件跟踪的只读
  RuntimeBridge 检查。
- 可选的 AntdUI Provider 语义、复杂控件树、分层窗口元数据以及主题/DPI 渲染配置。
- 增量式源码索引，提供全限定符号，用于 VS MCP 和 CodeGraph MCP 交接。
- 可重启的 UIA Worker 隔离，用于有界高风险探测。
- 仅本地的桥接 IPC 安全，以及带过期引用拒绝的多进程运行时标识。

## 兼容性

参见 [Compatibility Matrix](Compatibility-Matrix.zh-CN.md)。具体来说，当前证据
涵盖 .NET 8 Windows 构建、RuntimeBridge 的 net48/net8 目标，以及 RendererHost 的
net48/netcoreapp3.1/net8 目标。Windows 10/11 以及独立的 .NET 10 运行仍未验证。

## 升级说明

现有的 UIA 工具和必需参数保持不变。`bridgeInstanceId` 是运行时和诊断工具上的可选
输入；当客户端需要在应用重启后获得过期引用保护时，可以添加它。

## 未包含内容

本草稿不授权 NuGet 发布、NPM 发布、GitHub Release 创建，也不授权对 `main` 的自动
修改。
