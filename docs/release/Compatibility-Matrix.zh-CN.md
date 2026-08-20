# 兼容性矩阵

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](Compatibility-Matrix.md)

本矩阵描述了各包的目标框架以及当前发布准备分支上可用的证据。标记为 `designed`
（设计支持）的目标由项目 TFM 或兼容性映射支持，但未在当前环境中独立运行验证。

| 领域 | 目标 | 状态 | 证据 / 限制 |
| --- | --- | --- | --- |
| MCP Server | .NET 8 Windows | 已验证 | 本地 Release 构建/测试以及 Windows Core CI |
| RuntimeContracts | netstandard2.0 | 已验证 | 本地 Release 构建和包准备 |
| RuntimeBridge | .NET Framework 4.8 | 已验证 | 本地多目标 Release 构建 |
| RuntimeBridge | .NET 8 Windows | 已验证 | 本地 Release 构建/测试以及 Windows Core CI |
| RendererHost | net48 | 已验证 | 本地和 Windows 多目标 Release 构建 |
| RendererHost | netcoreapp3.1 | 已验证 | 本地和 Windows 多目标 Release 构建 |
| RendererHost | net8.0-windows | 已验证 | 本地和 Windows 多目标 Release 构建 |
| 标准 WinForms | .NET 8 测试应用 | 已验证 | RuntimeBridge、UIA、诊断以及完整测试套件 |
| AntdUI | .NET 8 上的 AntdUI 2.4.5 测试应用 | 已验证 | Provider、语义、弹出层、渲染和运行时测试 |
| .NET Framework 4.8 应用 | RuntimeBridge 引用 | 设计支持 | Bridge net48 目标；未单独运行客户应用 |
| .NET 10 应用 | RuntimeBridge 引用 / 渲染器映射 | 设计支持 | Net8 程序集和渲染器回退按设计兼容；未运行 .NET 10 SDK |
| Windows 10 | 操作系统特定验证 | 未验证 | 当前 CI 报告 `windows-latest`，不是独立的 Windows 10 运行 |
| Windows 11 | 操作系统特定验证 | 未验证 | 当前 CI 报告 `windows-latest`，不是独立的 Windows 11 运行 |

CI 工作流是对进度日志中所列提交的权威 Windows 检查。本文档不声称任何实际未运行的
操作系统或 SDK 测试。
