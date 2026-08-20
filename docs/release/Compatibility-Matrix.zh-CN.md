# 兼容性矩阵

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](Compatibility-Matrix.md)

本矩阵描述了各包的目标框架以及当前发布准备分支上可用的证据。标记为 `designed`
（设计支持）的目标由项目 TFM 或兼容性映射支持，但未在当前环境中独立运行验证。

| 领域 | 目标 | 状态 | 证据 / 限制 |
| --- | --- | --- | --- |
| MCP Server | .NET 8 Windows | 已验证 | 本地 Release 构建/测试以及 Windows Core CI |
| RuntimeContracts | netstandard2.0 | 已验证 | 本地 Release 构建、包准备和包资产检查（仅 netstandard2.0） |
| RuntimeBridge | .NET Framework 4.7.2（编译目标） | 已验证 | net472 Release 构建，以及两个真实 net472 消费者（SDK 风格与传统非 SDK 风格）从本地包恢复、构建、启动并通过 Protocol v1 返回真实控件树 |
| RuntimeBridge | .NET Framework 4.8 | 已验证 | 本地多目标 Release 构建 |
| RuntimeBridge | .NET 8 Windows | 已验证 | 本地 Release 构建/测试以及 Windows Core CI |
| RuntimeBridge 的 .NET Framework CLR | 实际执行的运行时 CLR | 本地已验证；托管 CI 待运行 | 本地消费者 E2E 在机器当前安装的 .NET Framework CLR（当前为 4.8.x）上运行；托管 Windows 工作流已经配置，但本分支尚无成功运行证据。断言只检查运行时属于“.NET Framework”，绝不写死 4.8 修订号 |
| 仅安装原始 4.7.2 Runtime 的独立机器 | 运行时 CLR 4.7.2 | 未验证 | 不宣称：未在仅安装原始 4.7.2 Runtime 的独立机器上验证 |
| RendererHost | net48 | 已验证 | 本地和 Windows 多目标 Release 构建 |
| RendererHost | netcoreapp3.1 | 已验证 | 本地和 Windows 多目标 Release 构建 |
| RendererHost | net8.0-windows | 已验证 | 本地和 Windows 多目标 Release 构建 |
| 标准 WinForms | .NET 8 测试应用 | 已验证 | RuntimeBridge、UIA、诊断以及完整测试套件 |
| AntdUI | .NET 8 上的 AntdUI 2.4.5 测试应用 | 已验证 | Provider、语义、弹出层、渲染和运行时测试 |
| .NET Framework 4.7.2 应用 | RuntimeBridge 引用 | 已验证 | 两个消费者项目都只通过 `PackageReference` 引用刚打包的 `Rhombus.WinFormsMcp.RuntimeBridge` 包 |
| .NET Framework 4.8 应用 | RuntimeBridge 引用 | 设计支持 | Bridge net48 目标；未单独运行客户应用 |
| .NET 10 应用 | RuntimeBridge 引用 / 渲染器映射 | 设计支持 | Net8 程序集和渲染器回退按设计兼容；未运行 .NET 10 SDK |
| Windows 10 | 操作系统特定验证 | 未验证 | 当前 CI 报告 `windows-latest`，不是独立的 Windows 10 运行 |
| Windows 11 | 操作系统特定验证 | 未验证 | 当前 CI 报告 `windows-latest`，不是独立的 Windows 11 运行 |

## 编译目标 vs 运行时 CLR

- 消费者针对 .NET Framework 4.7.2 targeting pack 编译
  （`TargetFrameworkVersion=v4.7.2`），这就是“为 .NET Framework 4.7.2 编译”的含义。
- 运行时进程执行在运行测试的机器所安装的 .NET Framework CLR 上。当前证据来自
  4.8.x 上的本地运行；将来托管 CI 执行时，其已安装 CLR 也可能高于 4.7.2。
- 本文档**不**宣称已在仅安装原始 4.7.2 Runtime 的机器上验证这些包。

CI 工作流只有在相关提交上成功运行后，才能作为权威的托管 Windows 证据。本文档不声称
任何实际未运行的操作系统、CLR 或 SDK 测试。
