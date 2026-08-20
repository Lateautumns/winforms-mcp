# WinForms MCP 中文文档索引

[English README](../README.md) · [中文主页](../README.zh-CN.md)

本文是 WinForms MCP（Rhombus.WinFormsMcp）全部中文文档的入口。英文原文保留
在仓库中，中文版本统一使用 `原文件名.zh-CN.md` 命名，位于英文文件同目录。

## 快速开始

- [中文主页](../README.zh-CN.md) — 项目介绍、安装方式、能力概览、权限边界
- [中文配置与 AI 使用指南](Chinese-Configuration-and-AI-Usage.md) — 跨电脑安装、客户端配置、环境变量、故障排查、可复制给 AI 的提示词

## MCP API

- [MCP API 完整说明](MCP-API.zh-CN.md) — 46 个 `winforms_*` 工具的输入/输出契约、公共协议与错误结构、兼容性规则（v1 freeze）

## RuntimeBridge 和运行时检查

- [发布架构](architecture/Release-Architecture.zh-CN.md) — MCP Server、RuntimeContracts、RuntimeBridge、RendererHost、Named Pipe 的分层与打包结构
- [迁移指南](release/Migration-Guide.zh-CN.md) — 从纯 UIA 自动化迁移到 RuntimeBridge 只读检查
- [MCP API](MCP-API.zh-CN.md) — 运行时检查工具的输入/输出契约

## AntdUI

- [AntdUI 架构分析](antdui/AntdUI-Architecture-Analysis.zh-CN.md) — 控件继承关系、Provider 检测策略、安全公共属性
- [LayeredWindow 分析](antdui/AntdUI-LayeredWindow-Analysis.zh-CN.md) — 分层窗口生命周期与枚举注意事项
- [Provider 映射](antdui/AntdUI-Provider-Mapping.zh-CN.md) — 控件到 Provider 的映射、只读限制、分页与超时边界

## VS MCP / CodeGraph

- [Cross-MCP Metadata Schema](integration/Cross-MCP-Metadata-Schema.zh-CN.md) — 源映射元数据字段含义与坐标约定
- [VS MCP 契约分析](integration/VS-MCP-Contract-Analysis.zh-CN.md) — 与 VS MCP 导航/调试工具的交接
- [CodeGraph MCP 契约分析](integration/CodeGraph-MCP-Contract-Analysis.zh-CN.md) — 与 CodeGraph MCP 查询的交接

## 发布

- [兼容性矩阵](release/Compatibility-Matrix.zh-CN.md) — 各包目标与验证证据
- [RC1 检查清单](release/v1.0.0-rc1-checklist.zh-CN.md) — v1.0.0 RC 验证项
- [Release Notes](release/Release-Notes-1.5.12-beta.zh-CN.md) — 1.5.12-beta 发布准备草稿
- [变更日志（中文）](../CHANGELOG.zh-CN.md)

## 开发进度

- [自主开发进度记录](development/AUTONOMOUS_PROGRESS.zh-CN.md) — 阶段记录、提交 SHA、PR、CI Run ID、验证结果与已知限制

## English

- [English README](../README.md)
- English 原始文档：与每个中文文件同目录的英文 Markdown（如 [MCP-API.md](MCP-API.md)、[Release-Architecture.md](architecture/Release-Architecture.md)）

## 内部文件说明

- `CLAUDE.md` 是面向 AI 协作的内部英文指令文件（机器执行规则），不是面向用户的
  文档，保持英文原文不变；请勿把它当作普通用户手册阅读。
