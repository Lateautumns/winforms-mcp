# 变更日志

[返回中文文档索引](docs/Chinese-Documentation-Index.md) · [English 原文](CHANGELOG.md)

本项目的所有重要变更都将记录在此文件中。

格式基于 [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)，
本项目遵循[语义化版本规范](https://semver.org/spec/v2.0.0.html)。

## [1.5.12-beta] - 未发布

### 新增

- 按进程和 bridge 实例作用域划分的只读 RuntimeBridge 标识关联。
- 针对兼容性、迁移、架构和本地打包的发布准备文档。

### 变更

- 运行时和诊断引用可携带可选的 `bridgeInstanceId`，以便在保留旧客户端的
  同时拒绝陈旧的应用程序引用。
- 本地打包现在验证服务器、RuntimeContracts、RuntimeBridge、RendererHost 和
  NPM 分发产物，而无需发布它们。

### 兼容性

- 现有的 MCP 工具名称和必需参数保持不变。
- RuntimeBridge 面向 .NET Framework 4.8 和 .NET 8 Windows；RendererHost
  仍针对 net48、netcoreapp3.1 和 net8.0-windows 多目标构建。

### 发布状态

- 这是一个未发布的准备草稿。不会执行任何 NuGet 推送、NPM 发布、GitHub
  Release 或 `main` 修改。

## [1.0.0] - 2024-10-21

### 新增
- Rhombus.WinFormsMcp 的首次发布
- 使用 FlaUI 配合 UIA2 后端的 WinForms 自动化 MCP 服务器
- 面向 CI/CD 环境的无头 UI 自动化能力
- 按 AutomationId、Name、ClassName 和 ControlType 进行元素发现
- UI 交互方法：点击、输入、设置值、拖放
- 进程生命周期管理（启动、附加、关闭）
- 截图捕获和视觉验证
- 对现代 .NET 应用程序的完整 async/await 支持
- 全面的基于 mock 的测试套件（52+ 项通过测试）
- NuGet 包：`Rhombus.WinFormsMcp`
- NPM 包：`@fnrhombus/winforms-mcp`，支持 npx
- GitHub Actions CI/CD 工作流
- 多平台发布（NuGet、NPM、GitHub Releases）
- 分支保护和拉取请求工作流
- MIT 许可证

### 功能特性
- **自动化元素发现**：按各种属性查找 UI 元素
- **无头运行**：无需显示服务器或 GUI 交互
- **完整进程控制**：启动、附加和管理应用程序生命周期
- **视觉验证**：捕获截图以供分析
- **异步集成**：与现代 .NET 异步模式无缝集成
- **跨平台分发**：可通过 NuGet、NPM 和直接下载获取

### 测试
- 52+ 项通过的单元级测试
- 24+ 项集成级测试
- 19+ 项端到端测试
- 针对错误场景的全面负面测试覆盖
- 测试覆盖包括错误恢复和回退模式

### 已知限制
- 仅限 Windows（需要 x64 架构）
- 需要 .NET 8.0 运行时或 SDK
- UI 自动化仅限于 Win32 UI 框架（WinForms、WPF、原生 Windows）

---

有关更多信息，请访问 [https://github.com/fnrhombus/winforms-mcp](https://github.com/fnrhombus/winforms-mcp)
