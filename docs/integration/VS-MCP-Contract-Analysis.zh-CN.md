# VS MCP 契约分析

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](VS-MCP-Contract-Analysis.md)

## 范围

本文档记录本地 VS-MCPServer 参考仓库在提交 `1d020ae` 处可观测的源码导航契约。它只是一份集成分析。WinForms MCP 不引用、不启动、也不调用 VS MCP。

已审阅导航、文档、解决方案、构建、调试器和共享模型相关的源码。本分析描述的是一个 MCP 客户端可以组合使用的契约，而不是 VS-MCPServer 的内部 API。

## 相关工具契约

| 工具 | 必需的交接字段 | 对 WinForms 工作流重要的结果 |
| --- | --- | --- |
| `symbol_document` | 绝对源码 `path` | 一个已打开解决方案文件中的符号 |
| `symbol_workspace` | 名称或部分名称 `query` | 候选符号与位置；不是直接的 FQN 查找 |
| `goto_definition` | 绝对 `path`、从 1 开始的 `line`、从 1 开始的 `column` | 该位置符号的定义 |
| `find_references` | 绝对 `path`、从 1 开始的 `line`、从 1 开始的 `column`、可选的最大数量 | 该位置符号的引用 |
| `document_open` / `document_read` | 绝对 `path` | 编辑器导航与有界的文件检查 |
| `selection_set` | 绝对 `path`、从 1 开始的起止位置 | 光标或选中的源码范围 |
| `build_project` | 完整的绝对 `.csproj` 路径 | 项目构建；其参数名具有误导性 |
| `debugger_add_breakpoint` | 绝对 `path`、`line` | 事件处理器或声明的断点 |

`SymbolInfo` 返回 `Name`、`FullName`、`Kind`、`FilePath`、从 1 开始的起止位置、容器名称和子项。`LocationInfo` 返回绝对文件位置以及预览。调试器状态和断点同样使用文件和行坐标。

## 坐标与路径规则

1. WinForms MCP 为所有现有源码位置保留绝对文件路径。VS MCP 在导航、文档、构建和调试时都需要它们。
2. WinForms MCP 的所有源码坐标都是从 1 开始的。这与 VS MCP 一致。
3. `fullyQualifiedSymbol` 有助于发现，但不能调用 `goto_definition` 或 `find_references`；这些工具需要源码位置。
4. 事件处理器位置通常是最佳断点目标。控件声明或初始化位置更适合 Designer 连线。
5. `symbol_workspace` 是名称/部分名称搜索。使用简单成员名或类型名，然后用绝对路径和源码位置消歧。

## 推荐交接

对于一个名为 `btnUpgrade` 的运行时控件，当存在 Click 处理器时使用事件源码身份：

```json
{
  "file": "C:/repo/Forms/DeviceManagementForm.cs",
  "line": 823,
  "column": 18,
  "fullyQualifiedSymbol": "NGUS2.UI.Forms.DeviceManagementForm.BtnUpgrade_Click"
}
```

客户端随后可以：

1. 调用 `document_open(path)` 或用 `selection_set` 放置光标。
2. 对被引用的符号调用 `goto_definition`，或对处理器本身调用 `find_references`。
3. 在重现 UI 操作之前调用 `debugger_add_breakpoint(path, line)`。
4. 使用 WinForms MCP 的 UIA 工具执行操作，使用 RuntimeBridge 获取托管状态。

## 边界

WinForms MCP 不假定存在打开的 Visual Studio 解决方案、不修改文档、也不调用调试器求值。它只产出只读的有界元数据。项目发现缺失时省略可选元数据；已经找到的源码位置仍然可用。
